#!/usr/bin/env bash
# Offline tests for tools/sentry-read.sh: the pii-check scanner on synthetic fixtures (via the
# hidden `pii-check --file` mode) and the argument/token guards. No network call is made.
#   bash tools/test-sentry-read.sh
set -euo pipefail

here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
tool="$here/sentry-read.sh"
clean="$here/testdata/event-clean.json"
pii="$here/testdata/event-pii.json"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

passed=0
failed=0
pass() {
  passed=$((passed + 1))
  printf 'ok   %s\n' "$1"
}
fail() {
  failed=$((failed + 1))
  printf 'FAIL %s\n' "$1"
}

# run <name> [env assignments...] -- <args...>: runs the tool, stores output and exit code.
run() {
  local name=$1
  shift
  local envs=()
  while (($#)) && [[ "$1" != -- ]]; do
    envs+=("$1")
    shift
  done
  shift
  out="$work/$name.out"
  code=0
  env -u SENTRY_AUTH_TOKEN "${envs[@]}" bash "$tool" "$@" >"$out" 2>&1 || code=$?
}

expect_code() {
  if [[ "$code" == "$2" ]]; then pass "$1 (exit $2)"; else fail "$1: expected exit $2, got $code"; fi
}

expect_text() {
  if grep -qF -- "$2" "$out"; then pass "$1"; else fail "$1: output lacks '$2'"; fi
}

expect_no_text() {
  if grep -qiF -- "$2" "$out"; then fail "$1: output contains '$2'"; else pass "$1"; fi
}

categories=(email upn ipv4 ipv6 domain_user dn secret_keys user ip_address request_fields resource_id)

# --- clean event: exit 0, every category clean -------------------------------------------------
run clean -- pii-check --file "$clean"
expect_code "clean fixture" 0
expect_text "clean fixture verdict" "verdict: clean"
for c in "${categories[@]}"; do
  if grep -qE "^$c +clean +0 " "$out"; then pass "clean: $c clean"; else fail "clean: $c not clean"; fi
done

# --- PII event: exit 1, every category hit, no value printed ---------------------------------
run pii -- pii-check --file "$pii"
expect_code "pii fixture" 1
expect_text "pii fixture verdict" "verdict: PII FOUND"
expect_text "pii fixture masks values" ": [hit]"
for c in "${categories[@]}"; do
  if grep -qE "^$c +HIT +[1-9]" "$out"; then pass "pii: $c hit"; else fail "pii: $c not reported"; fi
done
expect_text "pii: key that is itself PII is masked" '$.context[*] (key): [hit]'
expect_text "pii: header hit names the header" '$.entries[1].data.headers[1] (Authorization): [hit]'
expect_text "pii: address field inside SDK metadata" '$.sdk.client_ip: [hit]'
expect_text "pii: account name in API path" '$.entries[1].data.url: [hit]'
expect_text "pii: group name in API sub-path" '$.tags[3].value: [hit]'
# Every synthetic personal value in event-pii.json; none may appear in the output.
values=(
  "jane.testperson" "example.com" "jtestperson" "Testperson" "CONTOSO" "contoso" "Synthetic Staff"
  "203.0.113.77" "203.0.113.78" "198.51.100.23" "2001:db8::4242" "192.0.2.45" "424242"
  "synthetic-set-cookie-value" "synthetic-query-value" "synthetic-password-value"
  "synthetic-cookie-value" "synthetic-bearer-value" "SyntheticAgent" "Synthetic-Finance"
)
for v in "${values[@]}"; do
  expect_no_text "pii: value '$v' not printed" "$v"
done

# --- input errors on the file mode -----------------------------------------------------------
run missing -- pii-check --file "$work/does-not-exist.json"
expect_code "pii-check missing file" 2
printf 'not json' >"$work/bad.json"
run badjson -- pii-check --file "$work/bad.json"
expect_code "pii-check invalid JSON" 2
printf '[1, 2]' >"$work/list.json"
run notobject -- pii-check --file "$work/list.json"
expect_code "pii-check non-object JSON" 2

# --- guards: read-only, token handling, argument validation (all fail before any network) -----
run noargs --
expect_code "no command" 2
run unknown -- frobnicate
expect_code "unknown command" 2
run method_x -- -X POST project
expect_code "refuses -X" 2
expect_text "refuses -X message" "read-only: only HTTP GET"
run method_request -- issues --request=DELETE
expect_code "refuses --request" 2
run method_data -- events --data foo
expect_code "refuses --data" 2
run notoken -- project
expect_code "project without token" 2
expect_text "missing token message" "SENTRY_AUTH_TOKEN is not set"
run notoken_pii -- pii-check 0f1e2d3c4b5a69788796a5b4c3d2e1f0
expect_code "pii-check without token" 2
run badtoken SENTRY_AUTH_TOKEN='tok with spaces must-not-leak' -- project
expect_code "token with bad characters" 2
expect_no_text "bad token not printed" "must-not-leak"
run badid SENTRY_AUTH_TOKEN=tok_must_not_leak_123 -- event 3e95dd3f
expect_code "short event id rejected" 2
expect_no_text "token not printed on error" "tok_must_not_leak_123"
run badid2 -- pii-check '../../organizations/'
expect_code "path-like event id rejected" 2
run limit0 -- issues --limit 0
expect_code "--limit 0 rejected" 2
run limitbig -- events --limit 101
expect_code "--limit 101 rejected" 2
run limitabc -- issues --limit abc
expect_code "--limit abc rejected" 2
run eventsquery -- events --query is:unresolved
expect_code "events --query rejected" 2
run help -- --help
expect_code "help" 0

# --- static: one network call site, always GET ------------------------------------------------
if [[ $(grep -c 'curl -q' "$tool") == 1 ]] && grep -q -- '--get --proto' "$tool"; then
  pass "single curl call site uses --get"
else
  fail "curl call site is not a single --get request"
fi
if grep -nE 'curl .*(-X|--request|--data |--form)' "$tool" >/dev/null; then
  fail "curl invoked with a method or body option"
else
  pass "no method/body option on curl"
fi

printf '\n%d passed, %d failed\n' "$passed" "$failed"
((failed == 0))
