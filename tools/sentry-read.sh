#!/usr/bin/env bash
# Read-only access to the Sentry project sr-l6/ad-manager (de.sentry.io) for people and AI agents.
#
#   tools/sentry-read.sh project [--raw]
#   tools/sentry-read.sh issues [--limit N] [--query Q] [--raw]
#   tools/sentry-read.sh events [--limit N] [--raw]
#   tools/sentry-read.sh event <eventId>
#   tools/sentry-read.sh pii-check <eventId>
#
# Every call is an HTTP GET; the script has no code path for any other method and refuses
# method or body options. The token comes only from the SENTRY_AUTH_TOKEN environment variable,
# is sent to curl on stdin (never on the command line) and is never printed.
#
# pii-check also takes `--file <path>` instead of an event id: it scans a local event JSON
# without any network call (used by tools/test-sentry-read.sh).
#
# Exit codes: 0 ok / clean, 1 PII found (pii-check), 2 usage or configuration error,
# 3 HTTP or network error, 4 internal error.
set -euo pipefail

readonly SENTRY_HOST="https://de.sentry.io"
readonly SENTRY_ORG="sr-l6"
readonly SENTRY_PROJECT="ad-manager"
readonly API="$SENTRY_HOST/api/0"
readonly PROJECT_PATH="/projects/$SENTRY_ORG/$SENTRY_PROJECT"

die() {
  local code=$1
  shift
  printf 'sentry-read: %s\n' "$*" >&2
  exit "$code"
}

usage() {
  cat <<'EOF'
Usage: tools/sentry-read.sh <command> [options]   (read-only, HTTP GET only)

Commands (project sr-l6/ad-manager on https://de.sentry.io):
  project [--raw]                         project settings (scrubbing flags) as JSON
  issues  [--limit N] [--query Q] [--raw] issues, one JSON line each (N: 1-100, default 10)
  events  [--limit N] [--raw]             latest error events, one JSON line each
  event   <eventId>                       full event JSON (may contain personal data)
  pii-check <eventId>                     scan one event for personal data; prints only a
                                          verdict per category and JSON paths, never values

Needs SENTRY_AUTH_TOKEN in the environment (RT kit / cloud_secrets); it is never printed.
Exit codes: 0 ok/clean, 1 PII found, 2 usage/config, 3 HTTP/network, 4 internal error.
EOF
}

# Refuse anything that could turn a request into a write, wherever it appears.
for arg in "$@"; do
  case "$arg" in
    -X* | --request | --request=* | --method | --method=* | -d | --data* | -F | --form* | -T | --upload-file*)
      die 2 "read-only: only HTTP GET is supported; refusing option '$arg'"
      ;;
  esac
done

TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT

require_token() {
  [[ -n "${SENTRY_AUTH_TOKEN:-}" ]] ||
    die 2 "SENTRY_AUTH_TOKEN is not set: load it from the RT kit (cloud_secrets) into the environment first"
  [[ "$SENTRY_AUTH_TOKEN" =~ ^[A-Za-z0-9_=+/.-]+$ ]] ||
    die 2 "SENTRY_AUTH_TOKEN contains unexpected characters (value not shown)"
}

# Python helpers: formatting and the PII scan. Program on stdin, data files as arguments.
py() {
  python3 -I - "$@" <<'PY'
import ipaddress
import json
import re
import sys

MAX_PATHS = 20


def fail(code, message):
    sys.stderr.write(f"sentry-read: {message}\n")
    sys.exit(code)


def load(path, bad_code):
    try:
        with open(path, encoding="utf-8") as fh:
            return json.load(fh)
    except OSError as exc:
        fail(bad_code, f"cannot read {path}: {exc.strerror}")
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        fail(bad_code, f"{path} is not valid JSON ({type(exc).__name__})")


def tag(item, name):
    for pair in item.get("tags") or []:
        if isinstance(pair, dict) and pair.get("key") == name:
            return pair.get("value")
    return None


def detail(path):
    # Sentry error bodies look like {"detail": "..."}; anything else prints nothing.
    try:
        with open(path, encoding="utf-8") as fh:
            body = json.load(fh)
    except (OSError, ValueError):
        return
    if isinstance(body, dict) and isinstance(body.get("detail"), str):
        print(" ".join(body["detail"].split())[:200])


def project(path):
    data = load(path, 3)
    keys = ("id", "slug", "name", "platform", "status", "dateCreated", "firstEvent",
            "dataScrubber", "dataScrubberDefaults", "scrubIPAddresses", "sensitiveFields",
            "safeFields", "storeCrashReports", "allowedDomains")
    out = {k: data.get(k) for k in keys if k in data}
    if isinstance(data.get("organization"), dict):
        out["organization"] = data["organization"].get("slug")
    print(json.dumps(out, indent=2, ensure_ascii=False))


def load_list(path):
    data = load(path, 3)
    if not isinstance(data, list):
        fail(3, "unexpected response from Sentry: expected a JSON list")
    return data


def issues(path, limit):
    for item in load_list(path)[:limit]:
        keys = ("id", "shortId", "title", "culprit", "level", "status", "count", "userCount",
                "firstSeen", "lastSeen", "permalink")
        print(json.dumps({k: item.get(k) for k in keys}, ensure_ascii=False))


def events(path, limit):
    for item in load_list(path)[:limit]:
        out = {
            "eventID": item.get("eventID") or item.get("id"),
            "groupID": item.get("groupID"),
            "dateCreated": item.get("dateCreated"),
            "platform": item.get("platform"),
            "level": tag(item, "level"),
            "environment": tag(item, "environment"),
            "release": tag(item, "release"),
            "title": item.get("title"),
        }
        print(json.dumps(out, ensure_ascii=False))


def pretty(path):
    print(json.dumps(load(path, 3), indent=2, ensure_ascii=False))


# ---- PII scan -------------------------------------------------------------------------------

CATEGORIES = (
    ("email", "e-mail address"),
    ("upn", "UPN user@DOMAIN"),
    ("ipv4", "IPv4 address"),
    ("ipv6", "IPv6 address"),
    ("domain_user", "DOMAIN\\user logon"),
    ("dn", "distinguished name CN=/OU=/DC="),
    ("secret_keys", "cookie/authorization/set-cookie/api-key key"),
    ("user", "user object or user tag"),
    ("ip_address", "ip_address field"),
    ("request_fields", "request data/headers/cookies/query/env"),
    ("resource_id", "user/group name in an /api/users|groups|saved-searches path"),
)

# Replacement markers written by the app, by Sentry's scrubbing and by this tool.
MARKER = re.compile(r"\[(?:redacted|filtered|hit)\]", re.IGNORECASE)
EMAIL = re.compile(
    r"(?<![A-Za-z0-9._%+\-])[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}"
    r"(?![A-Za-z0-9\-])")
UPN = re.compile(
    r"(?<![A-Za-z0-9._%+$\-])[A-Za-z0-9._%+$\-]+@[A-Za-z][A-Za-z0-9\-]*(?:\.[A-Za-z0-9\-]+)*"
    r"(?![A-Za-z0-9\-])(?!\.[A-Za-z0-9])(?!:/)")
IPV4 = re.compile(r"(?<![0-9A-Za-z.])(?:\d{1,3}\.){3}\d{1,3}(?![0-9A-Za-z])(?!\.\d)")
IPV6 = re.compile(
    r"(?<![0-9A-Za-z:.])(?:[0-9A-Fa-f]{0,4}:){2,7}(?:(?:\d{1,3}\.){3}\d{1,3}|[0-9A-Fa-f]{1,4})?"
    r"(?![0-9A-Za-z:])")
DOMAIN_USER = re.compile(
    r"(?<![A-Za-z0-9._$\\/:\-])[A-Za-z0-9][A-Za-z0-9\-]{0,14}\\[A-Za-z0-9._$\-]*[A-Za-z0-9_$]"
    r"(?![A-Za-z0-9_$\-\\/])(?!\.[A-Za-z0-9])")
# The portal's URLs carry account and group names (the app redacts them, see SentryPrivacy.cs).
RESOURCE_ID = re.compile(
    r"/api/(?:users|groups|saved-searches)/(?!(?:search|bulk)\b)(?![{\s])[^/\s?#\"']+", re.IGNORECASE)
RESOURCE_SUB = re.compile(
    r"/api/(?:users|groups)/[^/?#\"']*/(?:members|groups)/(?![{\s])[^/\s?#\"']+", re.IGNORECASE)
DN = re.compile(r"(?<![A-Za-z0-9])(?:CN|OU|DC|UID)=(?![\s,;])[^,;=\r\n\"]+", re.IGNORECASE)
# A dotted quad right after "Version=", "v" or "Product/" is a version number, not an address.
VERSION_PREFIX = re.compile(r"(?:version\s*[=:]?\s*v?|\bv|[A-Za-z]/)$", re.IGNORECASE)

SECRET_KEYS = {"cookie", "cookies", "set-cookie", "authorization", "proxy-authorization", "x-api-key"}
USER_KEYS = {"user", "userreport"}
SAFE_HEADERS = {"accept", "content-type", "content-length", "host"}
REQUEST_FIELDS = ("data", "cookies", "query_string", "query", "env")
# Version and package metadata: dotted quads and name@version there are not personal data.
TECH_KEYS = {"sdk", "sdkupdates", "packages", "package", "modules", "debug_meta", "release",
             "dist", "version"}
# ...except fields that name an address (e.g. sdk.client_ip), which are always scanned in full.
IP_FIELD = re.compile(r"(?:^|_)ip(?:_|$)|addr", re.IGNORECASE)
IDENT = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
SAFE_KEY = re.compile(r"[A-Za-z0-9_.\-]{1,64}")


def is_empty(value):
    if value is None:
        return True
    if isinstance(value, str):
        return not MARKER.sub("", value).strip()
    if isinstance(value, dict):
        return all(is_empty(v) for v in value.values())
    if isinstance(value, list):
        return all(is_empty(v) for v in value)
    return False


def version_like(text, match):
    return bool(VERSION_PREFIX.search(text[max(0, match.start() - 16):match.start()]))


def text_hits(text, tech):
    """Categories whose pattern occurs in text; tech=True skips version-shaped matches."""
    text = MARKER.sub(" ", text)
    found = set()
    if EMAIL.search(text):
        found.add("email")
    if not tech:
        for m in UPN.finditer(text):
            if not EMAIL.fullmatch(m.group(0)):
                found.add("upn")
                break
        for m in IPV4.finditer(text):
            try:
                addr = ipaddress.IPv4Address(m.group(0))
            except ValueError:
                continue
            if addr.is_loopback or addr.is_unspecified or str(addr) == "255.255.255.255":
                continue
            if not version_like(text, m):
                found.add("ipv4")
                break
    for m in IPV6.finditer(text):
        try:
            addr = ipaddress.IPv6Address(m.group(0))
        except ValueError:
            continue
        if not (addr.is_loopback or addr.is_unspecified):
            found.add("ipv6")
            break
    if DOMAIN_USER.search(text):
        found.add("domain_user")
    if DN.search(text):
        found.add("dn")
    if RESOURCE_ID.search(text) or RESOURCE_SUB.search(text):
        found.add("resource_id")
    return found


class Scan:
    def __init__(self):
        self.hits = {name: [] for name, _ in CATEGORIES}
        self.seen = set()
        self.strings = 0

    def record(self, category, path):
        if (category, path) not in self.seen:
            self.seen.add((category, path))
            self.hits[category].append(path)

    def key_path(self, path, key):
        if SAFE_KEY.fullmatch(key) and not text_hits(key, tech=False):
            return f"{path}.{key}" if IDENT.fullmatch(key) else f'{path}["{key}"]'
        return f"{path}[*]"

    def walk(self, node, path, tech):
        if isinstance(node, dict):
            # Sentry API tags are {"key": name, "value": v} pairs: treat the name as the field.
            tag_key = node.get("key") if isinstance(node.get("key"), str) and "value" in node else None
            for raw_key, value in node.items():
                key = str(raw_key)
                if path == "$" and key == "_meta":
                    continue  # Sentry's remarks about what it scrubbed; mirrors keys, holds no values
                lower = key.lower()
                path_k = self.key_path(path, key)
                self.strings += 1
                for category in text_hits(key, tech):
                    self.record(category, f"{path_k} (key)")
                if not is_empty(value):
                    if lower in SECRET_KEYS:
                        self.record("secret_keys", path_k)
                    if lower == "ip_address":
                        self.record("ip_address", path_k)
                    if lower in USER_KEYS and path == "$":
                        self.record("user", path_k)
                field = tag_key.lower() if tag_key is not None and key == "value" else lower
                if key == "value" and tag_key is not None and not is_empty(value):
                    if field in USER_KEYS:
                        self.record("user", path_k)
                    if field in SECRET_KEYS:
                        self.record("secret_keys", path_k)
                child_tech = (tech or field in TECH_KEYS) and not IP_FIELD.search(field)
                self.walk(value, path_k, child_tech)
        elif isinstance(node, list):
            for index, value in enumerate(node):
                self.walk(value, f"{path}[{index}]", tech)
        elif isinstance(node, str):
            self.strings += 1
            for category in text_hits(node, tech):
                self.record(category, path)

    def requests(self, event):
        if isinstance(event.get("request"), dict):
            yield "$.request", event["request"]
        for index, entry in enumerate(event.get("entries") or []):
            if isinstance(entry, dict) and entry.get("type") == "request" and isinstance(entry.get("data"), dict):
                yield f"$.entries[{index}].data", entry["data"]

    def check_requests(self, event):
        for path, request in self.requests(event):
            for field in REQUEST_FIELDS:
                if not is_empty(request.get(field)):
                    self.record("request_fields", f"{path}.{field}")
            headers = request.get("headers")
            if isinstance(headers, dict):
                pairs = [(str(k), v, self.key_path(f"{path}.headers", str(k))) for k, v in headers.items()]
            elif isinstance(headers, list):
                pairs = []
                for index, pair in enumerate(headers):
                    if isinstance(pair, list) and len(pair) == 2:
                        name = str(pair[0])
                        label = f" ({name})" if SAFE_KEY.fullmatch(name) and not text_hits(name, False) else ""
                        pairs.append((name, pair[1], f"{path}.headers[{index}]{label}"))
            else:
                pairs = []
            for name, value, header_path in pairs:
                if is_empty(value):
                    continue
                if name.lower() in SECRET_KEYS:
                    self.record("secret_keys", header_path)
                if name.lower() not in SAFE_HEADERS:
                    self.record("request_fields", header_path)


def pii(path, label, bad_code):
    event = load(path, bad_code)
    if not isinstance(event, dict):
        fail(bad_code, f"{label}: expected one event JSON object")
    scan = Scan()
    scan.walk(event, "$", tech=False)
    scan.check_requests(event)

    total = sum(len(v) for v in scan.hits.values())
    print(f"pii-check: {label} ({scan.strings} strings and keys scanned)")
    print(f"{'category':<15} {'verdict':<7} {'hits':>4}  what")
    for name, what in CATEGORIES:
        paths = scan.hits[name]
        print(f"{name:<15} {'HIT' if paths else 'clean':<7} {len(paths):>4}  {what}")
        for hit_path in paths[:MAX_PATHS]:
            print(f"    {hit_path}: [hit]")
        if len(paths) > MAX_PATHS:
            print(f"    ... {len(paths) - MAX_PATHS} more")
    if total:
        categories = sum(1 for v in scan.hits.values() if v)
        print(f"verdict: PII FOUND ({total} hits in {categories} categories; values masked as [hit])")
        sys.exit(1)
    print("verdict: clean (values Sentry or the app already replaced, e.g. [redacted], count as clean)")


def main(argv):
    mode = argv[1]
    if mode == "detail":
        detail(argv[2])
    elif mode == "project":
        project(argv[2])
    elif mode == "issues":
        issues(argv[2], int(argv[3]))
    elif mode == "events":
        events(argv[2], int(argv[3]))
    elif mode == "pretty":
        pretty(argv[2])
    elif mode == "pii":
        pii(argv[2], argv[3], int(argv[4]))
    else:
        fail(4, f"unknown helper mode {mode}")


try:
    main(sys.argv)
except SystemExit:
    raise
except Exception as exc:  # report the type only: messages can echo event values
    fail(4, f"internal error ({type(exc).__name__})")
PY
}

# sentry_get <path under /api/0> <output file> [--data-urlencode name=value ...]
# The only network call in this script: always GET (--get), https only, no redirects, ~/.curlrc ignored.
sentry_get() {
  local path=$1 out=$2 status rc=0 reason
  shift 2
  require_token
  status=$(printf 'Authorization: Bearer %s\n' "$SENTRY_AUTH_TOKEN" |
    curl -q --silent --show-error --get --proto '=https' --max-redirs 0 \
      --connect-timeout 10 --max-time 60 \
      -H @- -H 'Accept: application/json' \
      --output "$out" --write-out '%{http_code}' \
      "$@" "$API$path") || rc=$?
  if ((rc != 0)); then
    die 3 "GET $path failed: network error (curl exit $rc)"
  fi
  if [[ "$status" != 2?? ]]; then
    reason=$(py detail "$out" || true)
    die 3 "GET $path failed: HTTP $status${reason:+ ($reason)}"
  fi
}

parse_limit() {
  if ! [[ "$1" =~ ^[1-9][0-9]{0,2}$ ]] || (($1 > 100)); then
    die 2 "--limit must be a number from 1 to 100"
  fi
  printf '%s' "$1"
}

event_id() {
  local id=${1//-/}
  [[ "$id" =~ ^[0-9a-fA-F]{32}$ ]] || die 2 "event id must be the full 32-character hex id from Sentry"
  printf '%s' "${id,,}"
}

cmd_project() {
  local raw=0
  while (($#)); do
    case "$1" in
      --raw) raw=1 ;;
      *) die 2 "project: unknown option '$1'" ;;
    esac
    shift
  done
  sentry_get "$PROJECT_PATH/" "$TMP_DIR/project.json"
  if ((raw)); then py pretty "$TMP_DIR/project.json"; else py project "$TMP_DIR/project.json"; fi
}

cmd_list() {
  local kind=$1 limit=10 raw=0 query="" has_query=0
  shift
  while (($#)); do
    case "$1" in
      --limit)
        (($# >= 2)) || die 2 "--limit needs a value"
        limit=$(parse_limit "$2")
        shift
        ;;
      --limit=*) limit=$(parse_limit "${1#--limit=}") ;;
      --query | --query=*)
        [[ "$kind" == issues ]] || die 2 "$kind: --query is only supported for issues"
        if [[ "$1" == --query ]]; then
          (($# >= 2)) || die 2 "--query needs a value"
          query=$2
          shift
        else
          query=${1#--query=}
        fi
        has_query=1
        ;;
      --raw) raw=1 ;;
      *) die 2 "$kind: unknown option '$1'" ;;
    esac
    shift
  done
  local args=(--data-urlencode "per_page=$limit")
  if ((has_query)); then args+=(--data-urlencode "query=$query"); fi
  sentry_get "$PROJECT_PATH/$kind/" "$TMP_DIR/$kind.json" "${args[@]}"
  if ((raw)); then py pretty "$TMP_DIR/$kind.json"; else py "$kind" "$TMP_DIR/$kind.json" "$limit"; fi
}

cmd_event() {
  (($# == 1)) || die 2 "usage: event <eventId>"
  local id
  id=$(event_id "$1")
  sentry_get "$PROJECT_PATH/events/$id/" "$TMP_DIR/event.json"
  py pretty "$TMP_DIR/event.json"
}

cmd_pii_check() {
  local rc=0 id
  if (($# == 2)) && [[ "$1" == --file ]]; then
    [[ -f "$2" ]] || die 2 "pii-check: no such file: $2"
    py pii "$2" "file $(basename -- "$2")" 2 || rc=$?
    exit "$rc"
  fi
  (($# == 1)) || die 2 "usage: pii-check <eventId>"
  id=$(event_id "$1")
  sentry_get "$PROJECT_PATH/events/$id/" "$TMP_DIR/event.json"
  py pii "$TMP_DIR/event.json" "event $id" 3 || rc=$?
  exit "$rc"
}

(($# >= 1)) || {
  usage >&2
  exit 2
}
command=$1
shift
case "$command" in
  project) cmd_project "$@" ;;
  issues | events) cmd_list "$command" "$@" ;;
  event) cmd_event "$@" ;;
  pii-check) cmd_pii_check "$@" ;;
  -h | --help | help) usage ;;
  *)
    usage >&2
    die 2 "unknown command '$command'"
    ;;
esac
