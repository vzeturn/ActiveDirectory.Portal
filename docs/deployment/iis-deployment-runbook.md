# AD Manager — IIS Deployment Runbook

This runbook covers deploying AD Manager, a .NET 10 ASP.NET Core application, from source to on-premises Windows Server 2022 with IIS hosting.

---

## 1. IIS Prerequisites

Before deploying, verify your Windows Server environment meets the following requirements:

### Windows Server 2022
- Confirm the server runs Windows Server 2022 or later.
- Check via: `winver` or `Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name ProductName`

### IIS Role with ASP.NET Core Module v2

1. Open **Server Manager**.
2. Click **Manage** → **Add Roles and Features**.
3. Select the server and proceed to **Server Roles**.
4. Check **Web Server (IIS)** (if not already installed).
5. Install the **.NET Hosting Bundle** for .NET 10:
   - Download from: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
   - Select the **Hosting Bundle** for your architecture (x64 or ARM64).
   - Run the installer: `dotnet-hosting-10.0.x-win.exe`
   - Restart IIS after installation: `iisreset`
6. Verify ASP.NET Core Module v2 is installed:
   - Open IIS Manager.
   - Select the server node.
   - Look for **ISAPI and CGI Restrictions** and verify `AspNetCoreModuleV2` is listed and enabled.

### Application Pool Configuration

1. In **IIS Manager**, expand **Application Pools**.
2. Create or select the application pool for AD Manager.
3. Right-click → **Advanced Settings**:
   - **.NET CLR Version**: set to `No Managed Code` (IMPORTANT — the app is .NET Core, not Framework).
   - **Managed Pipeline Mode**: `Integrated`
   - **Start Mode**: `AlwaysRunning` (recommended for production).
   - **Idle Time-out**: `0` (disable auto-shutdown, or set to your maintenance window).

---

## 2. Generating Encrypted Secrets

AD Manager uses DPAPI (Windows Data Protection API) for secret encryption at the machine level. Secrets are encrypted once on the deployment machine and cannot be decrypted on another machine. Follow this process for each secret.

### Secret Encryption Tool

Run the encryption command on the **deployment target machine** using the published `ADManager.Api.dll`:

```bash
dotnet ADManager.Api.dll encrypt-secret "<plaintext>"
```

The tool prints DPAPI-encrypted ciphertext to stdout. Copy the entire output (including any whitespace) into the corresponding field in `appsettings.Production.json`.

### Example Workflow

```bash
# Example 1: Encrypt the service account JSON blob
dotnet ADManager.Api.dll encrypt-secret "{\"username\":\"svc-ad-manager\",\"password\":\"<password>\",\"server\":\"10.0.0.10\",\"domain\":\"corp.example.com\",\"baseDn\":\"DC=corp,DC=example,DC=com\",\"ouBaseDn\":\"OU=Users,OU=Company,DC=corp,DC=example,DC=com\"}"

# Copy the output into appsettings.Production.json under Secrets:ServiceAccountEncrypted
```

**CRITICAL**: Do not encrypt secrets on your development machine and copy them to production. Encrypt on the production machine to ensure DPAPI can decrypt them later using the machine's local encryption key.

---

## 3. Required Secrets

Configure the three secrets in `appsettings.Production.json` after encrypting on the deployment machine.

### Secrets:ServiceAccountEncrypted (required)

A JSON object (encrypted as a whole) containing Active Directory connection credentials:

```json
{
  "username": "svc-ad-manager",
  "password": "<password>",
  "server": "10.0.0.10",
  "domain": "corp.example.com",
  "baseDn": "DC=corp,DC=example,DC=com",
  "ouBaseDn": "OU=Users,OU=Company,DC=corp,DC=example,DC=com"
}
```

**Fields**:
- `username`: Service account name (e.g., `DOMAIN\svc-ad-manager` or just `svc-ad-manager`).
- `password`: Service account password.
- `server`: FQDN or IP of an Active Directory domain controller (e.g., `10.0.0.10` or `dc01.corp.example.com`).
- `domain`: AD domain name used when binding as `domain\username` (e.g., `corp.example.com`).
- `baseDn`: LDAP base DN for the directory (e.g., `DC=corp,DC=example,DC=com`).
- `ouBaseDn`: LDAP base DN for the organizational unit containing managed users (e.g., `OU=Users,OU=Company,DC=corp,DC=example,DC=com`).

Encrypt this entire JSON blob:

```bash
dotnet ADManager.Api.dll encrypt-secret "{\"username\":\"svc-ad-manager\",\"password\":\"<password>\",\"server\":\"10.0.0.10\",\"domain\":\"corp.example.com\",\"baseDn\":\"DC=corp,DC=example,DC=com\",\"ouBaseDn\":\"OU=Users,OU=Company,DC=corp,DC=example,DC=com\"}"
```

Paste the encrypted output into `appsettings.Production.json`:

```json
{
  "Secrets": {
    "ServiceAccountEncrypted": "<paste-encrypted-blob-here>"
  }
}
```

### Secrets:SessionSigningKeyEncrypted (required)

A random string used to sign session cookies (prevents tampering). Generate a cryptographically secure random key:

```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Example output:
```
aBcDeFgHiJkLmNoPqRsTuVwXyZ1234567890abcd=
```

Encrypt it:

```bash
dotnet ADManager.Api.dll encrypt-secret "aBcDeFgHiJkLmNoPqRsTuVwXyZ1234567890abcd="
```

Paste the encrypted output into `appsettings.Production.json`:

```json
{
  "Secrets": {
    "SessionSigningKeyEncrypted": "<paste-encrypted-key-here>"
  }
}
```

### Secrets:AlertHookUrlEncrypted (optional, recommended)

Operational alerts go to the Request Tracking hub (Discord is no longer used). Get the
project's hook URL with the RT `set_alert_hook` tool (project `ad-manager`; the URL is shown
once and kept in the project's RT cloud kit as `RT_ALERT_HOOK_URL`), then either pass it to
the deploy script:

```powershell
.\deploy\deploy.ps1 -AlertHookUrl "https://rt.hubvn.dev/hooks/alerts/ad-manager/<token>"
```

or encrypt it by hand and paste the output into `appsettings.Production.json`:

```bash
dotnet ADManager.Api.dll encrypt-secret "https://rt.hubvn.dev/hooks/alerts/ad-manager/<token>"
```

```json
{
  "Secrets": {
    "AlertHookUrlEncrypted": "<paste-encrypted-url-here>"
  },
  "Alerts": {
    "Tenant": "<site name shown on the RT incident; defaults to the machine name>"
  }
}
```

Severity handling: `info` (successful operations) is only written to the log; `warning`
(for example a password reset whose follow-up AD step failed) and `critical` are posted as
JSON `{severity, title, text, tenant}` and the hub opens one incident request per alert.
Alerts never carry passwords. Check the hook end to end from the server:

```bash
dotnet ADManager.Api.dll send-test-alert
```

It prints `Alert outcome: delivered (HTTP 204)` and an incident request appears on RT; close
it there. `/api/health/detail` reports `alertHookConfigured`. Leave the field empty to only
log alerts.

### Sentry error reporting (optional, recommended)

Unhandled server errors (`LogError` and above) and browser errors go to the Sentry project
`ad-manager` (free plan, org `sr-l6`, EU region). The DSN is kept in the project's RT cloud
kit as `SENTRY_DSN`; it only allows sending events and is also served to the browser by
`GET /api/client-config`, so it is plain configuration rather than a DPAPI secret. Pass it
to the deploy script, which writes `Sentry:Dsn` on every deploy:

```powershell
.\deploy\deploy.ps1 -SentryDsn "https://<key>@<org>.ingest.de.sentry.io/<project>"
```

or set it by hand in `appsettings.Production.json` (or the `SENTRY_DSN` environment variable):

```json
{
  "Sentry": {
    "Dsn": "https://<key>@<org>.ingest.de.sentry.io/<project>",
    "Environment": "production",
    "TracesSampleRate": 0.05
  }
}
```

Privacy: `SendDefaultPii` is off and every event is scrubbed before it leaves the server or
browser. Users, IP addresses, cookies, headers (except Accept/Content-Type/Content-Length/
Host), query strings and request bodies are dropped; user and group names in `/api/users/...`
and `/api/groups/...` paths, distinguished names, e-mail/UPN, `DOMAIN\user` logons and quoted
values are replaced with `[redacted]`; module paths keep only the file name. Outgoing HTTP
calls are not recorded (the RT alert hook URL is secret). The Sentry project also has server-
side data scrubbing and "prevent storing IP addresses" turned on. Only 5% of requests are
traced for performance. Check it end to end from the server:

```bash
dotnet ADManager.Api.dll send-test-error
```

It prints `Sentry test error sent: event <id> (environment production)`; the event appears
in Sentry as `SentryTestException`. Resolve it there. Leave `Sentry:Dsn` empty to turn
reporting off (the browser SDK is then never downloaded).

### Secrets:ApiKeyEncrypted, ApiKeyOwner, ApiKeyRole (optional)

Enables `X-Api-Key` header authentication for automation/integration clients (leave `ApiKeyEncrypted` empty to disable):

```bash
dotnet ADManager.Api.dll encrypt-secret "<your-api-key-value>"
```

```json
{
  "Secrets": {
    "ApiKeyEncrypted": "<paste-encrypted-key-here>",
    "ApiKeyOwner": "automation-bot",
    "ApiKeyRole": "Viewer"
  }
}
```

`ApiKeyRole` must be `"Admin"` or `"Viewer"` (case-sensitive); any other value (or omitting it) falls back to `Viewer` and logs a warning. **Viewer is the default and the least-privilege choice** — set it to `"Admin"` explicitly only if the API-key client needs write access (account/group management, password resets). Earlier versions of this app granted API keys Admin access unconditionally; upgrading without setting `ApiKeyRole: "Admin"` will silently drop an existing automation client to read-only.

---

## 4. Build, Publish, and Deploy

### Step 1: Build the Frontend

On your build machine (or the source machine), build the frontend:

```bash
cd src\frontend
npm run build
```

This generates optimized JavaScript and CSS in the `dist/` directory.

### Step 2: Copy Frontend Artifacts into the Backend wwwroot

Copy the entire `dist/` directory into the ASP.NET Core application's `wwwroot/` directory:

```bash
# From the repository root
Copy-Item -Path "src\frontend\ad-manager-ui\dist\*" -Destination "src\backend\ADManager.Api\wwwroot\" -Recurse -Force
```

This allows the backend to serve the frontend as static files.

### Step 3: Publish the Application

Run a Release build and publish:

```bash
cd src\backend
dotnet publish ADManager.Api -c Release -o "C:\temp\ad-manager-publish"
```

This generates:
- `ADManager.Api.dll` — the compiled application.
- `web.config` — IIS configuration (auto-generated, uses ASP.NET Core Module V2).
- `wwwroot\` — static files (frontend + assets).
- Other runtime files and dependencies.

### Step 4: Stop the IIS Site

Stop the IIS application before replacing files to avoid file-in-use errors:

```powershell
Stop-WebSite -Name "ad-manager"
```

Or via IIS Manager: right-click the site → **Manage Web Site** → **Stop**.

### Step 5: Copy Published Output to Deployment Path

Copy the published files to the IIS application directory. Assume your IIS site root is `C:\inetpub\ad-manager`:

```powershell
# Remove old files (backup if needed)
Remove-Item -Path "C:\inetpub\ad-manager\*" -Recurse -Force

# Copy new files
Copy-Item -Path "C:\temp\ad-manager-publish\*" -Destination "C:\inetpub\ad-manager\" -Recurse -Force
```

### Step 6: Update appsettings.Production.json

Verify or update `C:\inetpub\ad-manager\appsettings.Production.json` with encrypted secrets:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "ADManager": "Information"
    }
  },
  "Secrets": {
    "ServiceAccountEncrypted": "<your-encrypted-service-account-blob>",
    "SessionSigningKeyEncrypted": "<your-encrypted-session-key>",
    "AlertHookUrlEncrypted": ""
  },
  "Cors": {
    "AllowedOrigins": []
  }
}
```

### Step 7: Start the IIS Site

Start the application:

```powershell
Start-WebSite -Name "ad-manager"
```

Or via IIS Manager: right-click the site → **Manage Web Site** → **Start**.

---

## 5. TLS Certificate Binding

### Obtain or Import a Certificate

If you don't already have a certificate:
- Obtain one from your organization's certificate authority (CA) or use Let's Encrypt.
- Import it into the **Local Computer** certificate store:
  1. On the server, open **Certlm.msc** (Certificate Manager).
  2. Navigate to **Personal** → **Certificates**.
  3. Right-click → **All Tasks** → **Import**.
  4. Select your `.pfx` or `.cer` file and provide the password (if `.pfx`).

If your CA issued the certificate as separate PEM files (`.crt` leaf + `.key`
private key + `.ca-bundle` intermediate chain — a typical CA delivery (PEM .crt/.key/.ca-bundle)), package them into a `.pfx` first with `deploy\create-pfx.ps1`
(requires OpenSSL — ships with Git for Windows):

```powershell
.\deploy\create-pfx.ps1 -CertDir "C:\path\to\cert-download-folder" -OutFile "C:\certs\example-wildcard.pfx"
```

The script prints a randomly generated PFX password once — save it immediately;
it is never written to disk. Pass the same `.pfx` path and password to
`deploy.ps1` (see §4b below) to import it and bind HTTPS automatically, or
follow the manual steps below.

### Bind the Certificate in IIS

1. Open **IIS Manager**.
2. Select your application under **Sites** → `ad-manager`.
3. In the right panel, click **Bindings**.
4. Click **Add**.
5. Configure:
   - **Type**: `https`
   - **IP address**: `All Unassigned` (or a specific IP)
   - **Port**: `443`
   - **Host name**: (leave empty for wildcard, or enter your domain)
   - **SSL Certificate**: Select your imported certificate from the dropdown.
6. Click **OK**.

### Redirect HTTP to HTTPS (Optional but Recommended)

`src/backend/ADManager.Api/web.config` already ships a `Redirect to HTTPS`
URL Rewrite rule targeting `https://ad.example.com`. If you deploy to
a different hostname, update the `action url` in that rule to match, or
configure the equivalent rule manually:

1. Select the site → **URL Rewrite** (install this IIS role if needed).
2. Add a new rule:
   - Name: `Force HTTPS`
   - Pattern: `.*`
   - Conditions: `{HTTPS} != on`
   - Action: `Redirect` to `https://{HTTP_HOST}{REQUEST_URI}` with status code `301 (Moved Permanently)`.

### 4b. One-Command Release with HTTPS (ad.example.com example)

`deploy\deploy.ps1` automates build, publish, IIS site/app pool setup, and —
when given `-PfxPath`/`-PfxPassword` — certificate import and HTTPS binding
in one run:

```powershell
# One-time: package the PEM cert into a PFX (prints the password once)
.\deploy\create-pfx.ps1 -CertDir "C:\path\to\cert-download-folder" -OutFile "C:\certs\example-wildcard.pfx"

# Deploy + import cert + bind HTTPS for ad.example.com
$pfxPassword = Read-Host -AsSecureString "PFX password"
.\deploy\deploy.ps1 -HostName "ad.example.com" -PfxPath "C:\certs\example-wildcard.pfx" -PfxPassword $pfxPassword
```

This adds an HTTPS binding on port 443 for host header `ad.example.com`
using SNI, alongside the existing HTTP binding on `-HttpPort` (default 8090,
kept for local/internal health checks). It also seeds
`Cors:AllowedOrigins` with `https://ad.example.com` on first deploy.
Re-running the script without `-PfxPath` skips certificate/binding work and
only redeploys application code — the HTTPS binding, once created, persists
across redeploys since IIS site config lives outside `$DeployPath`.

**DNS**: point `ad.example.com` at this server's public/internal IP
before running the HTTPS health check — the script's post-deploy check calls
`https://ad.example.com/health` directly and will warn (not fail) if
DNS/firewall isn't ready yet.

**Firewall**: ensure inbound TCP 443 (and $HttpPort if used externally) is
allowed through Windows Firewall and any upstream network firewall.

---

## 6. Verify Health Checks Post-Deployment

After the site starts, verify it is healthy before directing traffic to it.

### Liveness Check

Call the liveness endpoint:

```powershell
$response = Invoke-WebRequest -Uri "http://localhost/health" -SkipCertificateCheck
Write-Host $response.StatusCode  # Should be 200
```

Expected response: HTTP 200 with a text body (usually "Healthy").

### Readiness Check

Call the readiness endpoint (checks Active Directory and secrets configuration):

```powershell
$response = Invoke-WebRequest -Uri "http://localhost/health/ready" -SkipCertificateCheck
Write-Host $response.StatusCode  # Should be 200
Write-Host $response.Content
```

Expected response: HTTP 200 if both `active-directory` and `secrets` health checks pass.

**If readiness fails**:
- Check IIS logs (see Section 7 below).
- Verify `appsettings.Production.json` is correctly populated with encrypted secrets.
- Verify the service account has access to the specified Active Directory domain controller.
- Ensure the domain controller is reachable from the server (network connectivity, firewall rules).

---

## 7. Log Locations and Troubleshooting

### Application Data Directory

All application data and encryption keys are stored in `C:\ProgramData\ad-manager\`:

```
C:\ProgramData\ad-manager\
├── data\
│   └── ad-manager.db                # SQLite database
└── keys\
    └── (DPAPI encryption keys)      # Data Protection keys for decrypting secrets
```

Ensure the IIS Application Pool identity (usually `IIS AppPool\ad-manager`) has Read/Write permissions to this directory. Set via:

```powershell
# Get the app pool identity
$pool = Get-IISAppPool -Name "ad-manager"
$identity = $pool.ProcessModel.IdentityType  # e.g., "ApplicationPoolIdentity"

# Set NTFS permissions (use GUI or icacls)
icacls "C:\ProgramData\ad-manager" /grant "IIS AppPool\ad-manager:(OI)(CI)F" /T
```

### IIS Application Logs

By default, IIS logs are written to:

```
C:\inetpub\logs\LogFiles\W3SVC1\
```

(Replace `W3SVC1` with your site's ID if different.)

To enable ASP.NET Core application stdout logging in IIS:

1. Edit `C:\inetpub\ad-manager\web.config`:
   ```xml
   <aspNetCore processPath="dotnet" arguments=".\ADManager.Api.dll" 
               stdoutLogEnabled="true" 
               stdoutLogFile=".\logs\stdout" 
               hostingModel="InProcess" />
   ```

2. Create the `logs` directory in your site root:
   ```powershell
   New-Item -ItemType Directory -Path "C:\inetpub\ad-manager\logs" -Force
   ```

3. Grant permissions:
   ```powershell
   icacls "C:\inetpub\ad-manager\logs" /grant "IIS AppPool\ad-manager:(OI)(CI)F" /T
   ```

4. Restart IIS: `iisreset`

Logs will then appear in `C:\inetpub\ad-manager\logs\stdout_*.log`.

### Application Logs

The application logs via standard ASP.NET Core `Microsoft.Extensions.Logging` (no Serilog or other third-party sink is used). There is no dedicated application log directory — `C:\ProgramData\ad-manager\data\` contains only the SQLite database (`ad-manager.db`), not log files. Startup and runtime log messages are only durably captured when IIS stdout logging is enabled, as described above (`stdoutLogEnabled` defaults to `false` in `web.config` and must be manually enabled for troubleshooting).

### Troubleshooting Common Issues

| Issue | Diagnosis | Solution |
|-------|-----------|----------|
| **502 Bad Gateway** | Check IIS logs for startup errors. | Verify secrets are encrypted and `appsettings.Production.json` is valid JSON. Run `dotnet ADManager.Api.dll` locally to test. |
| **/health/ready returns 503** | Active Directory or secrets check failed. | Verify AD connectivity (`net user /domain`), service account credentials, and DPAPI permissions on `C:\ProgramData\ad-manager\keys`. |
| **File permission denied** | Application Pool identity lacks permissions. | Use `icacls` to grant `IIS AppPool\ad-manager` full access to `C:\ProgramData\ad-manager`. |
| **"Secrets configuration returned an empty service account"** | `ServiceAccountEncrypted` is empty or fails to decrypt. | Re-encrypt the service account on the deployment machine: `dotnet ADManager.Api.dll encrypt-secret "{...}"`. |
| **Application crashes on startup** | Schema initialization or secrets load failed. | Check IIS logs and stdout logs in `C:\inetpub\ad-manager\logs\`. |

---

## Summary

1. ✓ Install Windows Server 2022, IIS, and the .NET 10 Hosting Bundle.
2. ✓ Configure the Application Pool with "No Managed Code".
3. ✓ Generate DPAPI-encrypted secrets on the deployment machine.
4. ✓ Build frontend, publish application, deploy to IIS.
5. ✓ Bind a TLS certificate to the site (https).
6. ✓ Verify `/health` and `/health/ready` return 200 OK.
7. ✓ Monitor IIS stdout logs (once enabled) in `C:\inetpub\ad-manager\logs\`.

Once all steps are complete and health checks pass, AD Manager is ready to serve requests.
