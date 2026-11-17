# src/backend/build-image.ps1
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$frontendDir = Join-Path $repoRoot "src\frontend\ad-manager-ui"
$backendDir = $PSScriptRoot
$stagingDir = Join-Path $backendDir "frontend-dist"

Write-Host "Building frontend..."
Push-Location $frontendDir
try {
    npm ci
    npm run build
} finally {
    Pop-Location
}

Write-Host "Staging frontend build output..."
if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
Copy-Item -Recurse (Join-Path $frontendDir "dist") $stagingDir

Write-Host "Building Docker image..."
Push-Location $backendDir
try {
    docker build -t ad-manager-api:local .
} finally {
    Pop-Location
}

Write-Host "Cleaning up staging directory..."
Remove-Item -Recurse -Force $stagingDir

Write-Host "Done. Image tagged ad-manager-api:local"
