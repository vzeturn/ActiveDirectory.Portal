# CLAUDE.md — AD Manager (ActiveDirectory.Portal)

Web portal for managing Active Directory users and groups: ASP.NET Core API in `src/backend`, React + Vite UI in
`src/frontend/ad-manager-ui`, deployed to IIS on Windows (`deploy/`, runbook `docs/deployment/iis-deployment-runbook.md`).
Overview and commands: `README.md`.

## Gotchas

- Sentry (org `sr-l6`, project `ad-manager`, https://de.sentry.io) is read only through `tools/sentry-read.sh`
  (`project`, `issues`, `events`, `event <id>`, `pii-check <id>`; see README "Reading Sentry as AI (read-only)").
  It needs `SENTRY_AUTH_TOKEN` in the environment, taken from the RT kit (`cloud_secrets`); never print, echo or
  commit it. Use the token for reads only; the script sends nothing but HTTP GET. The hosted Sentry MCP server is
  not used (it needs a per-user OAuth login).
- There is no admin/ops MCP console for this project: no ops tools to call, changes go through code and `deploy/`.
- Sentry events can hold personal data from the directory (names, DNs, e-mails, IPs). Check an event with
  `pii-check` and report only its verdict and paths; never paste event values that contain personal data into
  RT, chat, commits or logs.
- Offline test of the Sentry reader: `bash tools/test-sentry-read.sh` (fixtures in `tools/testdata/` are synthetic;
  keep them that way).
