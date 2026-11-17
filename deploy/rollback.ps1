param(
    [Parameter(Mandatory = $true)][string]$BackupPath,
    [string]$DeployPath = "C:\inetpub\wwwroot\ad-manager",
    [string]$AppPoolName = "ADManagerPool"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupPath)) {
    throw "Backup path '$BackupPath' does not exist."
}

Import-Module WebAdministration
Write-Host "Stopping app pool $AppPoolName..."
Stop-WebAppPool -Name $AppPoolName
Start-Sleep -Seconds 3

Write-Host "Restoring from $BackupPath..."
Copy-Item -Path "$BackupPath\*" -Destination $DeployPath -Recurse -Force -Exclude @("logs", "keys")

Write-Host "Starting app pool $AppPoolName..."
Start-WebAppPool -Name $AppPoolName

Write-Host "Rollback complete." -ForegroundColor Green
