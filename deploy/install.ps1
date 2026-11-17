<#
.SYNOPSIS
    One-click installer: packages the wildcard PEM certificate into a PFX
    (if not already done) and runs deploy.ps1 with HTTPS enabled for
    ad.example.com, all in a single command.

.DESCRIPTION
    Run this from an elevated (Administrator) PowerShell prompt, from the
    root of the extracted release package. It expects the certificate
    files (star_example.com.crt / .key / .ca-bundle) to already be
    present under -CertDir (defaults to .\certs next to this script).

    Safe to re-run: certificate import, IIS site/pool creation, and the
    HTTPS binding are all idempotent (see deploy.ps1).

.EXAMPLE
    .\deploy\install.ps1
    (uses .\certs as CertDir, ad.example.com as hostname)

.EXAMPLE
    .\deploy\install.ps1 -CertDir "C:\path\to\cert-download-folder"
#>
param(
    [string]$CertDir = (Join-Path $PSScriptRoot "certs"),
    [string]$HostName = "ad.example.com",
    [string]$PfxOutFile = "C:\certs\example-wildcard.pfx",
    [string]$DeployPath = "",

    # Example AD topology defaults (override with parameters for your domain; stays the same between installs/service
    # accounts) - only the service account username/password are prompted for below.
    [string]$AdServer = "10.0.0.10",
    [string]$AdDomain = "corp.example.com",
    [string]$AdBaseDn = "DC=corp,DC=example,DC=com",
    [string]$AdOuBaseDn = "OU=Users,OU=Company,DC=corp,DC=example,DC=com"
)

$ErrorActionPreference = "Stop"

$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)) {
    throw "install.ps1 must be run as Administrator (right-click PowerShell -> Run as Administrator)."
}

$repoRoot = Split-Path $PSScriptRoot -Parent

Write-Host "=== ad-manager one-click install ===" -ForegroundColor Cyan
Write-Host "Release root : $repoRoot"
Write-Host "Cert dir     : $CertDir"
Write-Host "Host name    : $HostName"
Write-Host "Deploy path  : $(if ($DeployPath) { $DeployPath } else { '(auto: extracted release folder)' })"
Write-Host ""

# ---------------------------------------------------------------------------
# Step 1: Package PFX (skip if it already exists - re-runs are idempotent)
# ---------------------------------------------------------------------------
if (Test-Path $PfxOutFile) {
    Write-Host "PFX already exists at $PfxOutFile - skipping packaging step." -ForegroundColor Green
    Write-Host "(Delete it first if you need to re-package from updated cert files.)"
} else {
    if (-not (Test-Path $CertDir)) {
        throw "Certificate directory '$CertDir' not found. Pass -CertDir pointing at the folder containing star_example.com.crt/.key/.ca-bundle."
    }
    Write-Host "Packaging certificate into PFX..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "create-pfx.ps1") -CertDir $CertDir -OutFile $PfxOutFile
    if ($LASTEXITCODE -ne 0 -and -not (Test-Path $PfxOutFile)) {
        throw "create-pfx.ps1 did not produce $PfxOutFile."
    }
}

$deployArgs = @{
    DeployPath = $DeployPath
    HostName   = $HostName
    AdServer   = $AdServer
    AdDomain   = $AdDomain
    AdBaseDn   = $AdBaseDn
    AdOuBaseDn = $AdOuBaseDn
}

# ---------------------------------------------------------------------------
# Step 1a: PFX password - only asked when the HTTPS binding for this host
# isn't already correctly configured on the site. On a routine update
# (re-running install.ps1 after unzipping a new release over the same
# folder) the certificate and binding don't change, so re-entering the PFX
# password every time is pure friction. Re-run deploy\create-pfx.ps1 and
# delete the HTTPS binding first if the certificate itself needs rotating.
# ---------------------------------------------------------------------------
$httpsAlreadyBound = $false
$siteNameForCheck = "ad-manager"
try {
    Import-Module WebAdministration -ErrorAction Stop
    $existingBinding = Get-WebBinding -Name $siteNameForCheck -Protocol "https" -HostHeader $HostName -ErrorAction SilentlyContinue
    if ($existingBinding -and $existingBinding.certificateHash) {
        $httpsAlreadyBound = $true
    }
} catch {
    $httpsAlreadyBound = $false
}

if ($httpsAlreadyBound) {
    Write-Host ""
    Write-Host "HTTPS binding for $HostName already configured on this server - skipping certificate re-import (no PFX password needed)." -ForegroundColor Green
} else {
    Write-Host ""
    $pfxPassword = Read-Host -AsSecureString "Enter the PFX password (printed above when the PFX was created)"
    $deployArgs.PfxPath = $PfxOutFile
    $deployArgs.PfxPassword = $pfxPassword
}

# ---------------------------------------------------------------------------
# Step 1b: AD service account - only asked the first time (skipped once
# appsettings.Production.json is already bootstrapped; see deploy.ps1
# Phase 6, which ignores these values entirely once bootstrapped anyway).
# Leave any field blank to bootstrap a placeholder instead (app still starts;
# /health/ready reports the AD check unhealthy until configured later).
# ---------------------------------------------------------------------------
$configAlreadyBootstrapped = $false
# Mirror deploy.ps1's own DeployPath default (ship-mode: pre-published release
# package deployed in place at $repoRoot; otherwise C:\inetpub\wwwroot\ad-manager).
$resolvedDeployPath = $DeployPath
if (-not $resolvedDeployPath) {
    $shipModeBackendDll = Join-Path $repoRoot "ADManager.Api.dll"
    $shipModeWwwroot = Join-Path $repoRoot "wwwroot"
    $backendProjectPath = Join-Path $repoRoot "src\backend\ADManager.Api\ADManager.Api.csproj"
    $isShipMode = (-not (Test-Path $backendProjectPath)) -and (Test-Path $shipModeBackendDll) -and (Test-Path $shipModeWwwroot)
    $resolvedDeployPath = if ($isShipMode) { $repoRoot } else { "C:\inetpub\wwwroot\ad-manager" }
}
$prodSettingsCheckPath = Join-Path $resolvedDeployPath "appsettings.Production.json"
if (Test-Path $prodSettingsCheckPath) {
    try {
        $existingSettings = Get-Content $prodSettingsCheckPath -Raw | ConvertFrom-Json
        if (-not [string]::IsNullOrWhiteSpace($existingSettings.Secrets.SessionSigningKeyEncrypted)) {
            $configAlreadyBootstrapped = $true
        }
    } catch { }
}

if ($configAlreadyBootstrapped) {
    Write-Host ""
    Write-Host "appsettings.Production.json is already configured on this server." -ForegroundColor Green
    $updateSecrets = Read-Host "  Update the AD service account credentials? [y/N]"
    if ($updateSecrets -notmatch '^(?i)y(es)?$') {
        Write-Host "  Keeping existing secrets - skipping AD service account prompt." -ForegroundColor Green
        $configAlreadyBootstrapped = $true
    } else {
        $configAlreadyBootstrapped = $false
    }
}

if (-not $configAlreadyBootstrapped) {
    Write-Host ""
    Write-Host "AD service account (used to bind to Active Directory) - press Enter on Username to skip and use a placeholder:" -ForegroundColor Cyan
    Write-Host "  (Server/domain come from the script parameters: $AdServer / $AdDomain)"
    $adUsername = Read-Host "  AD service account username"
    if (-not [string]::IsNullOrWhiteSpace($adUsername)) {
        $deployArgs.AdUsername = $adUsername
        $deployArgs.AdPassword = Read-Host "  AD service account password" -AsSecureString
    }
}

# ---------------------------------------------------------------------------
# Step 2: Deploy (build + IIS site/pool + HTTPS binding + first-run config)
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Running deploy.ps1..." -ForegroundColor Cyan
Push-Location $repoRoot
try {
    & (Join-Path $PSScriptRoot "deploy.ps1") @deployArgs
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "=== Install complete ===" -ForegroundColor Green
Write-Host "Site should now be reachable at https://$HostName/ (and http://localhost:8090/ locally)."
