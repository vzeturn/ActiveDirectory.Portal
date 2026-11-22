# AD Manager

Web portal for day-to-day Active Directory administration, using a single shared AD
service account and session-cookie authentication (no JWT, no per-request AD impersonation).

**Stack:** ASP.NET Core 10 backend, React 19 + TypeScript + Vite frontend, SQLite for
sessions, audit log and saved searches. Hosted on IIS on Windows Server (on-premises or a VM).

## Features

- Search and look up users and groups
- Enable, disable and unlock accounts; reset passwords (single and bulk, CSV import)
- Manage group membership and groups
- Dashboard, audit log, saved searches
- Admin and viewer roles mapped from AD groups; optional `X-Api-Key` access for automation
- Operational alerts to the Request Tracking hub (warning/critical open an incident; info is logged)

## Architecture

Layered, with the dependency rule `Api -> Application -> Domain <- Infrastructure`.

```
src/
  backend/
    ADManager.sln
    ADManager.Api/               Controllers, auth, DI wiring, Program.cs
    ADManager.Application/       Use cases and abstractions (IActiveDirectoryGateway, ...)
    ADManager.Domain/            Plain models (AdUser, AdGroup, OperationResult, ...)
    ADManager.Infrastructure/    AD adapter, DPAPI secrets, SQLite stores, RT alert hook sender
    ADManager.Application.Tests/ xUnit tests (fake AD gateway, no domain needed)
  frontend/
    ad-manager-ui/               React app, built to static files served by the API site
deploy/                          IIS deployment scripts
docs/deployment/                 IIS deployment runbook
```

## Requirements

- Windows Server with IIS and the .NET 10 Hosting Bundle, joined to or able to reach the
  target Active Directory domain
- .NET SDK 10 and Node.js (with npm) to build from source

## Quick start

```bash
# Backend
dotnet build src/backend/ADManager.sln
dotnet test src/backend/ADManager.sln

# Frontend
cd src/frontend/ad-manager-ui
npm ci
npm run dev      # dev server, proxies /api to the backend
npm run build    # production static build
```

## Deployment

See the [IIS deployment runbook](docs/deployment/iis-deployment-runbook.md).

- `deploy/install.ps1` packages the certificate and runs `deploy.ps1` interactively.
- `deploy/deploy.ps1` publishes backend and frontend into the IIS site directory. It keeps
  `logs/` and `keys/` across deploys and backs up the previous deployment to
  `C:\ADManagerData\backups\<timestamp>`.
- `deploy/rollback.ps1 -BackupPath <backup folder>` restores that backup into the site
  directory (optional `-DeployPath`, default `C:\inetpub\wwwroot\ad-manager`, and
  `-AppPoolName`, default `ADManagerPool`), stopping and restarting the app pool.

## Security notes

- Secrets (AD service account, session signing key, RT alert hook URL, API key) are stored as
  DPAPI-encrypted values in `appsettings.Production.json` and decrypted on the same machine
  at startup. Encrypt them on the target server with
  `dotnet ADManager.Api.dll encrypt-secret "<plaintext>"`. Never commit real values.
- The committed `appsettings.Production.json` contains empty placeholders only.
- Login is rate limited. Responses set `X-Content-Type-Options`, `X-Frame-Options` and
  `Referrer-Policy` headers.
- API keys default to the Viewer role unless `ApiKeyRole` is set to `Admin`.
- Errors are reported to Sentry when `Sentry:Dsn` is set (`deploy.ps1 -SentryDsn`), with
  `SendDefaultPii` off and user names, DNs, e-mails, IPs, headers and bodies scrubbed before
  sending. Test with `dotnet ADManager.Api.dll send-test-error`. To read events back, see
  [Reading Sentry as AI (read-only)](#reading-sentry-as-ai-read-only).

## Reading Sentry as AI (read-only)

`tools/sentry-read.sh` lets an AI agent (or a person) look at the Sentry project `ad-manager`
(org `sr-l6`, `https://de.sentry.io`) through the Sentry REST API, without the hosted Sentry
MCP server (it needs a per-user OAuth login). It needs only `bash`, `curl` and `python3`.

```bash
export SENTRY_AUTH_TOKEN=...        # from the RT kit (cloud_secrets); never echo it
tools/sentry-read.sh project        # project settings, incl. dataScrubber / scrubIPAddresses
tools/sentry-read.sh issues --limit 20 --query 'is:unresolved'   # one JSON line per issue
tools/sentry-read.sh events --limit 10                          # latest events, one JSON line each
tools/sentry-read.sh event <eventId>      # full event JSON (can contain personal data)
tools/sentry-read.sh pii-check <eventId>  # personal-data check, values never printed
```

- Every call is an HTTP GET (`--raw` prints the unfiltered API JSON for `project`, `issues`
  and `events`). The script refuses method or body options and has no write path.
- The token is read only from `SENTRY_AUTH_TOKEN`, passed to curl on stdin and never printed.
  Missing token: exit 2. HTTP or network errors print the status and exit 3.
- `pii-check` fetches the event and scans every string and key for e-mail addresses, UPNs
  (`user@DOMAIN`), IPv4/IPv6 addresses, `DOMAIN\user` logons, distinguished names
  (`CN=..,OU=..,DC=..`), account or group names in `/api/users|groups|saved-searches` paths,
  cookie/authorization/set-cookie/API-key keys, a filled `user` object or `user` tag,
  `ip_address` fields and request data/headers/cookies/query/env. It prints one verdict per
  category and the JSON paths of the hits with the value masked as `[hit]`. Exit 0 = clean,
  1 = personal data found. Values the app or Sentry already replaced (`[redacted]`,
  `[Filtered]`) count as clean; version numbers in SDK, package and release fields are
  ignored. The check is pattern based: a plain name with no recognisable shape is not found.
- Tests (offline, synthetic fixtures in `tools/testdata/`): `bash tools/test-sentry-read.sh`.

## License

MIT. See [LICENSE](LICENSE).
