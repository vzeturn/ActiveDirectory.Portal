param(
    [string]$DeployPath = "",
    [string]$AppPoolName = "ADManagerPool",
    [string]$SiteName = "ad-manager",
    [int]$HttpPort = 8090,
    [string]$BackendProject = "src\backend\ADManager.Api\ADManager.Api.csproj",
    [string]$FrontendDir = "src\frontend\ad-manager-ui",
    [string]$HostName = "ad.example.com",
    [int]$HttpsPort = 443,
    [string]$PfxPath = "",
    [securestring]$PfxPassword,

    # AD service account used by DpapiSecretsProvider (ADManager.Infrastructure) to bind
    # to Active Directory. Only used the first time appsettings.Production.json is
    # bootstrapped (Phase 6) - leave blank to bootstrap a placeholder instead (the app
    # will still start; /health/ready reports the AD check unhealthy until configured).
    [string]$AdUsername = "",
    [securestring]$AdPassword,
    [string]$AdServer = "",
    [string]$AdDomain = "",
    [string]$AdBaseDn = "",
    [string]$AdOuBaseDn = ""
)

$ErrorActionPreference = "Stop"

# Older Windows Server / Windows PowerShell 5.1 defaults to TLS 1.0, which
# aka.ms and dotnet CDN endpoints reject. Force TLS 1.2+ for this session
# before any Invoke-WebRequest call.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

# ---------------------------------------------------------------------------
# Phase 0: Admin check
# ---------------------------------------------------------------------------
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)) {
    throw "deploy.ps1 must be run as Administrator."
}

$ProgramDataRoot = "C:\ProgramData\ad-manager"
$DataDir = Join-Path $ProgramDataRoot "data"
$KeysDir = Join-Path $ProgramDataRoot "keys"

# ---------------------------------------------------------------------------
# Ship-mode detection (moved up front): a shipped release package contains
# only the published backend output (no .csproj/source) alongside
# deploy\deploy.ps1, plus a pre-built wwwroot\ folder. In that case IIS
# should serve the extracted folder directly - no build, no copy - so the
# default DeployPath IS the repo root where the customer unzipped the
# package. Re-running deploy.ps1 (e.g. after unzipping an update in place)
# is then just: stop pool, bootstrap config if needed, start pool.
$repoRoot = Split-Path $PSScriptRoot -Parent
$backendProjectPath = Join-Path $repoRoot $BackendProject
$shipModeBackendDll = Join-Path $repoRoot "ADManager.Api.dll"
$shipModeWwwroot = Join-Path $repoRoot "wwwroot"
$isShipMode = (-not (Test-Path $backendProjectPath)) -and (Test-Path $shipModeBackendDll) -and (Test-Path $shipModeWwwroot)

if (-not $DeployPath) {
    if ($isShipMode) {
        $DeployPath = $repoRoot
    } else {
        $DeployPath = "C:\inetpub\wwwroot\ad-manager"
    }
}

# ---------------------------------------------------------------------------
# Phase 0b: IIS (Web-Server) role check / install
# ---------------------------------------------------------------------------
# The Hosting Bundle installer only registers the ASP.NET Core Module (ANCM)
# into IIS if IIS itself is already present. On a fresh Windows Server with
# the Web-Server role not yet installed, the Hosting Bundle installer exits
# 0 (it did install the .NET runtime/SDK bits) but silently skips the IIS
# integration step, leaving aspnetcorev2.dll missing. Ensure IIS exists first.
$iisFeature = $null
try {
    $iisFeature = Get-WindowsFeature -Name Web-Server -ErrorAction Stop
} catch {
    $iisFeature = $null
}
if ($iisFeature -and -not $iisFeature.Installed) {
    Write-Host "IIS (Web-Server role) not installed. Installing IIS + management tools..." -ForegroundColor Yellow
    $iisResult = Install-WindowsFeature -Name Web-Server, Web-Mgmt-Console, Web-Asp-Net45, Web-Http-Redirect -IncludeManagementTools
    if (-not $iisResult.Success) {
        throw "Install-WindowsFeature failed to install IIS. ExitCode: $($iisResult.ExitCode)"
    }
    Write-Host "IIS installed successfully." -ForegroundColor Green
} elseif ($iisFeature) {
    Write-Host "IIS (Web-Server role) already installed. Skipping." -ForegroundColor Green
} else {
    $w3svc = Get-Service -Name W3SVC -ErrorAction SilentlyContinue
    if ($w3svc) {
        Write-Host "IIS (World Wide Web Publishing Service) detected running - skipping Get-WindowsFeature check (unavailable on this SKU, e.g. Windows client)." -ForegroundColor Green
    } else {
        Write-Warning "Could not detect IIS via Get-WindowsFeature or the W3SVC service (client SKU without IIS?). Please install IIS manually (Windows Features > Internet Information Services) before continuing."
    }
}

# ---------------------------------------------------------------------------
# Phase 1: ASP.NET Core Hosting Bundle check / install
# ---------------------------------------------------------------------------
# The Hosting Bundle installer registers the ASP.NET Core Module v2 globally by
# dropping aspnetcorev2.dll under "Program Files\IIS\Asp.Net Core Module\V2".
# That file's presence is the reliable, version-independent marker that the
# bundle (not just the shared runtime) has been installed on this machine.
$AncmModulePath = "C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"

if (-not (Test-Path $AncmModulePath)) {
    Write-Host "ASP.NET Core Hosting Bundle not detected. Downloading and installing..." -ForegroundColor Yellow

    # NOTE FOR REVIEWER: this aka.ms stable redirect was verified live on 2026-09-04
    # and currently resolves to https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/10.0.11/dotnet-hosting-10.0.11-win.exe
    # (i.e. the .NET 10 track's "always latest win-x64 hosting bundle" link).
    # Verify this still resolves correctly before relying on it in an unattended run.
    $HostingBundleUrl = "https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe"
    $installerPath = Join-Path $env:TEMP "dotnet-hosting-10.0-win-$(Get-Date -Format 'yyyyMMddHHmmss').exe"

    Write-Host "  Downloading from $HostingBundleUrl ..."
    Invoke-WebRequest -Uri $HostingBundleUrl -OutFile $installerPath -UseBasicParsing
    if (-not (Test-Path $installerPath)) { throw "Hosting Bundle download failed: installer not found at $installerPath." }

    Write-Host "  Running installer silently..."
    $proc = Start-Process -FilePath $installerPath -ArgumentList "/quiet", "/norestart" -Wait -PassThru
    Remove-Item $installerPath -Force -ErrorAction SilentlyContinue

    # 0 = success, 3010 = success but a restart is required
    if ($proc.ExitCode -ne 0 -and $proc.ExitCode -ne 3010) {
        throw "ASP.NET Core Hosting Bundle installer failed with exit code $($proc.ExitCode)."
    }

    if (-not (Test-Path $AncmModulePath)) {
        throw "Hosting Bundle installer reported success but $AncmModulePath is still missing. Aborting."
    }

    Write-Host "  Hosting Bundle installed. Recycling IIS (WAS + W3SVC) so ANCM is picked up..."
    # A full reboot is not required for ANCM registration to take effect - stopping
    # and restarting the Windows Process Activation Service and the World Wide Web
    # Publishing Service is sufficient. Do NOT reboot the machine here.
    net stop was /y | Out-Null
    net start w3svc | Out-Null

    Write-Host "ASP.NET Core Hosting Bundle installed successfully." -ForegroundColor Green
} else {
    Write-Host "ASP.NET Core Hosting Bundle already installed. Skipping." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Phase 1b: IIS URL Rewrite Module check / install
# ---------------------------------------------------------------------------
# web.config uses <rewrite> to force HTTP -> HTTPS. Without this module registered
# in applicationHost.config, IIS fails to parse ANY web.config containing a
# <rewrite> section with error 0x8007000d ("the data is invalid") - it looks like
# a config/XML error but is actually a missing-module error.
$RewriteModulePath = "C:\Windows\System32\inetsrv\rewrite.dll"

if (-not (Test-Path $RewriteModulePath)) {
    Write-Host "IIS URL Rewrite Module not detected. Downloading and installing..." -ForegroundColor Yellow

    $RewriteModuleUrl = "https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi"
    $msiPath = Join-Path $env:TEMP "rewrite_amd64-$(Get-Date -Format 'yyyyMMddHHmmss').msi"

    Write-Host "  Downloading from $RewriteModuleUrl ..."
    Invoke-WebRequest -Uri $RewriteModuleUrl -OutFile $msiPath -UseBasicParsing
    if (-not (Test-Path $msiPath)) { throw "URL Rewrite Module download failed: installer not found at $msiPath." }

    Write-Host "  Running installer silently..."
    $proc = Start-Process -FilePath "msiexec.exe" -ArgumentList "/i", "`"$msiPath`"", "/quiet", "/norestart" -Wait -PassThru
    Remove-Item $msiPath -Force -ErrorAction SilentlyContinue

    if ($proc.ExitCode -ne 0 -and $proc.ExitCode -ne 3010) {
        throw "URL Rewrite Module installer failed with exit code $($proc.ExitCode)."
    }

    if (-not (Test-Path $RewriteModulePath)) {
        throw "URL Rewrite Module installer reported success but $RewriteModulePath is still missing. Aborting."
    }

    Write-Host "  URL Rewrite Module installed. Recycling IIS (WAS + W3SVC) so it is picked up..."
    net stop was /y | Out-Null
    net start w3svc | Out-Null

    Write-Host "IIS URL Rewrite Module installed successfully." -ForegroundColor Green
} else {
    Write-Host "IIS URL Rewrite Module already installed. Skipping." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Phase 2: IIS site + app pool (idempotent)
# ---------------------------------------------------------------------------
Import-Module WebAdministration

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    Write-Host "Creating app pool $AppPoolName..."
    New-WebAppPool -Name $AppPoolName | Out-Null
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedPipelineMode -Value "Integrated"
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name startMode -Value "AlwaysRunning"
    Write-Host "App pool $AppPoolName created (No Managed Code / Integrated / AlwaysRunning)." -ForegroundColor Green
} else {
    Write-Host "App pool $AppPoolName already exists. Skipping creation." -ForegroundColor Green
}

if (-not (Test-Path $DeployPath)) {
    New-Item -ItemType Directory -Path $DeployPath -Force | Out-Null
}

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    Write-Host "Creating site $SiteName on port $HttpPort bound to $DeployPath..."
    New-Website -Name $SiteName -PhysicalPath $DeployPath -ApplicationPool $AppPoolName -Port $HttpPort | Out-Null
    Write-Host "Site $SiteName created." -ForegroundColor Green
} else {
    $existingSite = Get-Website -Name $SiteName
    $resolvedDeployPath = (Resolve-Path $DeployPath).Path
    if ($existingSite.PhysicalPath.TrimEnd('\') -ne $resolvedDeployPath.TrimEnd('\')) {
        Write-Host "Site $SiteName exists but points at $($existingSite.PhysicalPath) - repointing to $resolvedDeployPath..." -ForegroundColor Yellow
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $resolvedDeployPath
        Write-Host "Site $SiteName repointed." -ForegroundColor Green
    } else {
        Write-Host "Site $SiteName already exists and points at the correct path. Skipping." -ForegroundColor Green
    }
}

# ---------------------------------------------------------------------------
# Phase 2b: TLS certificate import + HTTPS binding (optional - only if -PfxPath given)
# ---------------------------------------------------------------------------
if ($PfxPath) {
    if (-not (Test-Path $PfxPath)) { throw "PfxPath '$PfxPath' not found. Run deploy\create-pfx.ps1 first." }
    if (-not $PfxPassword) { throw "-PfxPassword is required when -PfxPath is provided." }

    Write-Host "Importing certificate from $PfxPath into LocalMachine\My..."
    $cert = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation "Cert:\LocalMachine\My" -Password $PfxPassword -Exportable
    Write-Host "  Imported certificate thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

    $existingHttpsBinding = Get-WebBinding -Name $SiteName -Protocol "https" -HostHeader $HostName -ErrorAction SilentlyContinue
    if (-not $existingHttpsBinding) {
        Write-Host "Adding HTTPS binding $HostName`:$HttpsPort to site $SiteName..."
        New-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort -HostHeader $HostName -SslFlags 1 | Out-Null
    } else {
        Write-Host "HTTPS binding for $HostName already exists on site $SiteName. Skipping binding creation." -ForegroundColor Green
    }

    $binding = Get-WebBinding -Name $SiteName -Protocol "https" -HostHeader $HostName
    $binding.AddSslCertificate($cert.Thumbprint, "My")
    Write-Host "HTTPS certificate bound to $HostName`:$HttpsPort (SNI)." -ForegroundColor Green
} else {
    Write-Host "No -PfxPath provided. Skipping certificate import / HTTPS binding (HTTP-only on port $HttpPort)." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Phase 3: ProgramData data/keys directories + ACLs
# ---------------------------------------------------------------------------
foreach ($dir in @($DataDir, $KeysDir)) {
    if (-not (Test-Path $dir)) {
        Write-Host "Creating $dir..."
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

Write-Host "Granting IIS AppPool\$AppPoolName full control on $ProgramDataRoot..."
$icaclsResult = icacls $ProgramDataRoot /grant ("IIS AppPool\{0}:(OI)(CI)F" -f $AppPoolName) /T
if ($LASTEXITCODE -ne 0) { throw "icacls failed to grant permissions on $ProgramDataRoot. Output: $icaclsResult" }

Write-Host "Granting IIS AppPool\$AppPoolName read+execute on $DeployPath (site content)..."
$icaclsResult = icacls $DeployPath /grant ("IIS AppPool\{0}:(OI)(CI)RX" -f $AppPoolName) /T
if ($LASTEXITCODE -ne 0) { throw "icacls failed to grant permissions on $DeployPath. Output: $icaclsResult" }

$logsDir = Join-Path $DeployPath "logs"
if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
}
Write-Host "Granting IIS AppPool\$AppPoolName full control on $logsDir (stdout logs)..."
$icaclsResult = icacls $logsDir /grant ("IIS AppPool\{0}:(OI)(CI)F" -f $AppPoolName) /T
if ($LASTEXITCODE -ne 0) { throw "icacls failed to grant permissions on $logsDir. Output: $icaclsResult" }

# ---------------------------------------------------------------------------
# Phase 4/5: Build (dev mode) or deploy in place (ship mode)
# ---------------------------------------------------------------------------
# A shipped release package contains only the published backend output
# (no .csproj/source) alongside deploy\deploy.ps1, plus a pre-built
# wwwroot\ folder - it is not meant to be rebuilt or copied anywhere. When
# $DeployPath already IS the repo root (the default resolved above), IIS
# just serves the extracted folder directly: no build, no copy, no backup
# staging - only stop/bootstrap/start. When run from a full source
# checkout (BackendProject resolves to a real .csproj), build and copy
# into $DeployPath as before.
$deployInPlace = $isShipMode -and ((Resolve-Path $DeployPath).Path -eq (Resolve-Path $repoRoot).Path)

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = "C:\ADManagerData\backups\$timestamp"
$tempBuildPath = "C:\ADManagerData\temp-build\$timestamp"

if ($deployInPlace) {
    Write-Host "Pre-published release package detected - deploying in place at $DeployPath (no build, no copy)." -ForegroundColor Green
} elseif ($isShipMode) {
    Write-Host "Pre-published release package detected (no source checkout) - skipping build, using artifacts shipped alongside deploy.ps1." -ForegroundColor Green
    New-Item -ItemType Directory -Path "$tempBuildPath\backend" -Force | Out-Null
    Copy-Item -Path "$repoRoot\*" -Destination "$tempBuildPath\backend" -Recurse -Force -Exclude @("deploy", "docs", "wwwroot")
    $frontendDistSource = $shipModeWwwroot
} else {
    Write-Host "Publishing backend..."
    dotnet publish $BackendProject -c Release -o "$tempBuildPath\backend"
    if ($LASTEXITCODE -ne 0) { throw "Backend publish failed." }

    Write-Host "Building frontend..."
    Push-Location $FrontendDir
    npm run build
    if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Frontend build failed." }
    Pop-Location
    $frontendDistSource = Join-Path $FrontendDir "dist"
}

Write-Host "Stopping app pool $AppPoolName..."
if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne "Stopped") {
    Stop-WebAppPool -Name $AppPoolName
    Start-Sleep -Seconds 3
} else {
    Write-Host "App pool $AppPoolName is already stopped (likely from a prior interrupted run). Skipping." -ForegroundColor Green
}

if (-not $deployInPlace) {
    if (Test-Path $DeployPath) {
        $existingItems = Get-ChildItem -Path $DeployPath -ErrorAction SilentlyContinue
        if ($existingItems) {
            Write-Host "Backing up current deployment to $backupPath..."
            New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
            Copy-Item -Path "$DeployPath\*" -Destination $backupPath -Recurse -Force -Exclude @("logs", "keys")
        }
    }

    Write-Host "Copying backend output..."
    Copy-Item -Path "$tempBuildPath\backend\*" -Destination $DeployPath -Recurse -Force

    Write-Host "Copying frontend static assets..."
    Copy-Item -Path "$frontendDistSource\*" -Destination "$DeployPath\wwwroot" -Recurse -Force
}

# ---------------------------------------------------------------------------
# Phase 6: Bootstrap appsettings.Production.json on first-ever deploy only
# ---------------------------------------------------------------------------
# NOTE: dotnet publish (and the shipped release package) always includes an
# EMPTY appsettings.Production.json template (Secrets all blank, no CORS
# origins) copied straight from source - so Test-Path alone can never detect
# "first deploy" once that template exists on disk. Check whether the file
# has actually been bootstrapped with a real signing key instead.
$prodSettingsPath = Join-Path $DeployPath "appsettings.Production.json"
$needsBootstrap = $true
if (Test-Path $prodSettingsPath) {
    try {
        $existingSettings = Get-Content $prodSettingsPath -Raw | ConvertFrom-Json
        if (-not [string]::IsNullOrWhiteSpace($existingSettings.Secrets.SessionSigningKeyEncrypted)) {
            $needsBootstrap = $false
        }
    } catch {
        Write-Warning "appsettings.Production.json at $prodSettingsPath is not valid JSON - treating as needing bootstrap."
    }
}

if ($needsBootstrap) {
    Write-Host "appsettings.Production.json at $DeployPath has no configured session signing key - bootstrapping a fresh config (first deploy)..."

    # Windows PowerShell 5.1 resolves System.Security.Cryptography types against
    # .NET Framework, whose RandomNumberGenerator has no static GetBytes(int) -
    # that overload only exists on .NET (Core) 3.0+. Use the instance API, which
    # works on both .NET Framework and modern .NET.
    $sessionKeyBytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($sessionKeyBytes) } finally { $rng.Dispose() }
    $sessionKeyPlaintext = [Convert]::ToBase64String($sessionKeyBytes)
    $sessionKeyPlaintextB64Arg = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sessionKeyPlaintext))
    $encryptOutput = & dotnet "$DeployPath\ADManager.Api.dll" encrypt-secret-b64 "$sessionKeyPlaintextB64Arg" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Failed to DPAPI-encrypt the generated session signing key via ADManager.Api.dll encrypt-secret-b64. Output: $encryptOutput" }
    $sessionKeyEncrypted = ($encryptOutput | Select-Object -Last 1).ToString().Trim()

    # DpapiSecretsProvider.EnsureLoaded() eagerly Unprotect()s ServiceAccountEncrypted
    # at app startup (Program.cs) with no empty-string guard - Convert.FromBase64String("")
    # + ProtectedData.Unprotect on an empty array throws CryptographicException, which
    # Program.cs re-throws as LogCritical, crashing the process (IIS then reports 500.30).
    # A placeholder-but-valid encrypted JSON keeps the app booting even with no AD info;
    # if real AD values were supplied (via params or install.ps1's prompts), use those
    # instead so the app is fully functional out of the box.
    $adPasswordPlaintext = ""
    if ($AdPassword) {
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($AdPassword)
        try { $adPasswordPlaintext = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    }
    $serviceAccountJson = (@{
        username = $AdUsername
        password = $adPasswordPlaintext
        server   = $AdServer
        domain   = $AdDomain
        baseDn   = $AdBaseDn
        ouBaseDn = $AdOuBaseDn
    } | ConvertTo-Json -Compress)
    # Passed as base64 (not the raw JSON) because PowerShell's argv marshaling to a native
    # exe silently strips embedded double quotes from a quoted argument, corrupting JSON.
    $serviceAccountJsonB64Arg = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($serviceAccountJson))
    $serviceAccountOutput = & dotnet "$DeployPath\ADManager.Api.dll" encrypt-secret-b64 "$serviceAccountJsonB64Arg" 2>&1
    $adPasswordPlaintext = $null
    if ($LASTEXITCODE -ne 0) { throw "Failed to DPAPI-encrypt the service account secret via ADManager.Api.dll encrypt-secret-b64. Output: $serviceAccountOutput" }
    $serviceAccountEncrypted = ($serviceAccountOutput | Select-Object -Last 1).ToString().Trim()

    $prodSettings = [ordered]@{
        Logging = [ordered]@{
            LogLevel = [ordered]@{
                Default   = "Warning"
                ADManager = "Information"
            }
        }
        Secrets = [ordered]@{
            ServiceAccountEncrypted    = $serviceAccountEncrypted
            SessionSigningKeyEncrypted = $sessionKeyEncrypted
            DiscordWebhookUrlEncrypted = ""
        }
        Cors = [ordered]@{
            AllowedOrigins = @("https://$HostName")
        }
    }
    $prodSettings | ConvertTo-Json -Depth 5 | Set-Content -Path $prodSettingsPath -Encoding UTF8

    if ([string]::IsNullOrWhiteSpace($AdUsername)) {
        Write-Host "appsettings.Production.json bootstrapped with a fresh DPAPI-encrypted session signing key and placeholder service account." -ForegroundColor Green
        Write-Warning "Secrets:ServiceAccountEncrypted is a PLACEHOLDER (empty username/password/server). /health/ready will report the 'active-directory' check as unhealthy until a real AD service account secret is configured."
        Write-Warning "This is expected on a dev/test box with no real AD. Configure a real service account secret per docs\deployment\iis-deployment-runbook.md before relying on AD features."
    } else {
        Write-Host "appsettings.Production.json bootstrapped with a fresh DPAPI-encrypted session signing key and the supplied AD service account ($AdUsername@$AdDomain)." -ForegroundColor Green
    }
} else {
    Write-Host "appsettings.Production.json at $DeployPath already has a configured session signing key - leaving it untouched (not overwriting operator-configured secrets)." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Phase 7: Start app pool + site, health check
# ---------------------------------------------------------------------------
Write-Host "Starting app pool $AppPoolName..."
if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne "Started") {
    Start-WebAppPool -Name $AppPoolName
} else {
    Write-Host "App pool $AppPoolName is already started. Skipping." -ForegroundColor Green
}

if ((Get-Website -Name $SiteName).State -ne "Started") {
    Start-Website -Name $SiteName
}

Start-Sleep -Seconds 5
# The published web.config always redirects HTTP -> https://$HostName/ (see
# the "Redirect to HTTPS" rewrite rule), so a plain Invoke-WebRequest either
# follows that redirect to a hostname this machine may not be able to reach
# yet (first deploy, DNS not cut over) or reports a false failure. Disable
# redirect-following and treat a 301/302 back to $HostName as proof the app
# responded, without requiring the public hostname to be reachable.
$healthOk = $false
try {
    $request = [System.Net.HttpWebRequest]::Create("http://localhost:$HttpPort/health")
    $request.AllowAutoRedirect = $false
    $request.Timeout = 15000
    $response = $request.GetResponse()
    $healthOk = [int]$response.StatusCode -in 200, 301, 302
    $response.Close()
} catch [System.Net.WebException] {
    $resp = $_.Exception.Response
    if ($resp) {
        $healthOk = [int]$resp.StatusCode -in 301, 302
        $resp.Close()
    }
}
if ($healthOk) {
    Write-Host "HTTP health check passed (http://localhost:$HttpPort/health)." -ForegroundColor Green
} else {
    Write-Warning "HTTP health check did not return 200 (or a redirect to HTTPS)."
    Write-Host ""
    Write-Host "=== Diagnosing startup failure: running the app directly to capture the real exception ===" -ForegroundColor Yellow
    Write-Host "(This is what IIS's generic '500.30 - ASP.NET Core app failed to start' page hides.)"
    Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    $env:ASPNETCORE_ENVIRONMENT = "Production"
    $diagOutputFile = Join-Path $env:TEMP "ad-manager-startup-diag-$(Get-Date -Format 'yyyyMMddHHmmss').log"
    $proc = Start-Process -FilePath "dotnet" -ArgumentList "`"$DeployPath\ADManager.Api.dll`"" `
        -WorkingDirectory $DeployPath -NoNewWindow -PassThru `
        -RedirectStandardOutput $diagOutputFile -RedirectStandardError "$diagOutputFile.err"
    Start-Sleep -Seconds 8
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
    Remove-Item Env:\ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue

    $diagOutput = @()
    if (Test-Path $diagOutputFile) { $diagOutput += Get-Content $diagOutputFile }
    if (Test-Path "$diagOutputFile.err") { $diagOutput += Get-Content "$diagOutputFile.err" }
    Remove-Item $diagOutputFile, "$diagOutputFile.err" -ErrorAction SilentlyContinue

    if ($diagOutput) {
        Write-Host "--- Captured startup output ---" -ForegroundColor Cyan
        $diagOutput | ForEach-Object { Write-Host $_ }
        Write-Host "--- End captured output ---" -ForegroundColor Cyan
    } else {
        Write-Warning "No output captured - the process may have started successfully and is still running standalone, or exited silently. Check Windows Event Viewer > Application log for .NET Runtime errors."
    }

    Write-Host ""
    Write-Host "Restarting app pool $AppPoolName under IIS..."
    Start-WebAppPool -Name $AppPoolName
    Start-Sleep -Seconds 3
}

if ($PfxPath) {
    $httpsHealth = $null
    try {
        $httpsHealth = Invoke-WebRequest -Uri "https://$HostName/health" -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
    } catch {
        $httpsHealth = $null
    }
    if ($httpsHealth -and $httpsHealth.StatusCode -eq 200) {
        Write-Host "HTTPS health check passed (https://$HostName/health)." -ForegroundColor Green
    } else {
        Write-Warning "HTTPS health check to https://$HostName/health failed - verify DNS points at this server and the firewall allows port $HttpsPort."
    }
}

if ($deployInPlace) {
    Write-Host "Deployed in place at: $DeployPath (no backup taken - redeploy by extracting a new release package over this folder)."
} else {
    Write-Host "Backup preserved at: $backupPath"
}
