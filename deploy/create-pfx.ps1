<#
.SYNOPSIS
    Packages the wildcard *.example.com PEM certificate (.crt/.key/.ca-bundle)
    into a single .pfx for import into the Windows certificate store / IIS binding.

.DESCRIPTION
    Pure .NET/PowerShell implementation - no OpenSSL, Git, winget, Chocolatey, or
    internet access required. Works on any bare Windows Server with PowerShell 5.1+
    (.NET Framework 4.7.2+, which ships with Windows Server 2016 and later).

    .NET Framework's RSA classes do not expose ImportPkcs8PrivateKey /
    ImportRSAPrivateKey (those are .NET Core/.NET 5+ only), so this script
    parses the PKCS#1 / PKCS#8 DER structure by hand into RSAParameters and
    uses the legacy ImportParameters API, which IS available on Framework.

    Generates a random PFX password, prints it once to the console, and
    never writes it to disk.

.EXAMPLE
    .\create-pfx.ps1 -CertDir "C:\path\to\cert-download-folder" -OutFile "C:\certs\example-wildcard.pfx"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$CertDir,

    [string]$OutFile = "C:\certs\example-wildcard.pfx",

    [string]$FriendlyName = "wildcard.example.com"
)

$ErrorActionPreference = "Stop"

$crt = Join-Path $CertDir "star_example.com.crt"
$key = Join-Path $CertDir "star_example.com.key"
$bundle = Join-Path $CertDir "star_example.com.ca-bundle"

foreach ($f in @($crt, $key, $bundle)) {
    if (-not (Test-Path $f)) { throw "Required certificate file not found: $f" }
}

function ConvertFrom-PemBlocks {
    param([string]$PemText)
    $pattern = "-----BEGIN ([A-Z0-9 ]+)-----([^-]+)-----END \1-----"
    $matches = [regex]::Matches($PemText, $pattern)
    $blocks = @()
    foreach ($m in $matches) {
        $b64 = $m.Groups[2].Value -replace "\s", ""
        $blocks += [PSCustomObject]@{
            Label = $m.Groups[1].Value
            Bytes = [Convert]::FromBase64String($b64)
        }
    }
    return $blocks
}

# --- Minimal ASN.1 DER reader/writer (only what PKCS#1/PKCS#8 RSA keys need) ---

function Read-DerLength {
    param([byte[]]$Bytes, [ref]$Pos)
    $b = $Bytes[$Pos.Value]; $Pos.Value++
    if ($b -lt 0x80) { return [int]$b }
    $numBytes = $b -band 0x7F
    $len = 0
    for ($i = 0; $i -lt $numBytes; $i++) {
        $len = ($len -shl 8) -bor $Bytes[$Pos.Value]
        $Pos.Value++
    }
    return $len
}

function Read-DerInteger {
    param([byte[]]$Bytes, [ref]$Pos)
    if ($Bytes[$Pos.Value] -ne 0x02) { throw "Expected DER INTEGER tag at offset $($Pos.Value), got 0x$($Bytes[$Pos.Value].ToString('X2'))." }
    $Pos.Value++
    $len = Read-DerLength -Bytes $Bytes -Pos $Pos
    $val = $Bytes[$Pos.Value..($Pos.Value + $len - 1)]
    $Pos.Value += $len
    if ($val.Length -gt 1 -and $val[0] -eq 0) { $val = $val[1..($val.Length - 1)] }
    return ,$val
}

function Get-PaddedBytes {
    param([byte[]]$Value, [int]$Length)
    if ($Value.Length -eq $Length) { return $Value }
    if ($Value.Length -gt $Length) { throw "DER integer longer ($($Value.Length) bytes) than expected field size ($Length bytes)." }
    $padded = New-Object byte[] $Length
    [Array]::Copy($Value, 0, $padded, $Length - $Value.Length, $Value.Length)
    return $padded
}

function ConvertFrom-Pkcs1RsaPrivateKey {
    param([byte[]]$Der)
    $pos = 0
    if ($Der[$pos] -ne 0x30) { throw "Expected DER SEQUENCE at start of RSA private key." }
    $pos++
    Read-DerLength -Bytes $Der -Pos ([ref]$pos) | Out-Null
    Read-DerInteger -Bytes $Der -Pos ([ref]$pos) | Out-Null   # version
    $n = Read-DerInteger -Bytes $Der -Pos ([ref]$pos)
    $e = Read-DerInteger -Bytes $Der -Pos ([ref]$pos)
    $d = Read-DerInteger -Bytes $Der -Pos ([ref]$pos)
    $p = Read-DerInteger -Bytes $Der -Pos ([ref]$pos)
    $q = Read-DerInteger -Bytes $Der -Pos ([ref]$pos)
    $dp = Read-DerInteger -Bytes $Der -Pos ([ref]$pos)
    $dq = Read-DerInteger -Bytes $Der -Pos ([ref]$pos)
    $qi = Read-DerInteger -Bytes $Der -Pos ([ref]$pos)

    $modLen = $n.Length
    $halfLen = [Math]::Ceiling($modLen / 2)

    $rsaParams = New-Object Security.Cryptography.RSAParameters
    $rsaParams.Modulus = $n
    $rsaParams.Exponent = $e
    $rsaParams.D = Get-PaddedBytes $d $modLen
    $rsaParams.P = Get-PaddedBytes $p $halfLen
    $rsaParams.Q = Get-PaddedBytes $q $halfLen
    $rsaParams.DP = Get-PaddedBytes $dp $halfLen
    $rsaParams.DQ = Get-PaddedBytes $dq $halfLen
    $rsaParams.InverseQ = Get-PaddedBytes $qi $halfLen
    return $rsaParams
}

function ConvertFrom-Pkcs8PrivateKeyInfo {
    param([byte[]]$Der)
    # PKCS#8 PrivateKeyInfo ::= SEQUENCE { version INTEGER, algorithm AlgorithmIdentifier, privateKey OCTET STRING }
    # The OCTET STRING payload is itself the PKCS#1 RSAPrivateKey DER blob (for rsaEncryption keys).
    $pos = 0
    if ($Der[$pos] -ne 0x30) { throw "Expected DER SEQUENCE at start of PKCS#8 key." }
    $pos++
    Read-DerLength -Bytes $Der -Pos ([ref]$pos) | Out-Null
    Read-DerInteger -Bytes $Der -Pos ([ref]$pos) | Out-Null   # version

    if ($Der[$pos] -ne 0x30) { throw "Expected AlgorithmIdentifier SEQUENCE in PKCS#8 key." }
    $pos++
    $algLen = Read-DerLength -Bytes $Der -Pos ([ref]$pos)
    $pos += $algLen   # skip the algorithm OID/params, we assume RSA

    if ($Der[$pos] -ne 0x04) { throw "Expected OCTET STRING (private key payload) in PKCS#8 key." }
    $pos++
    $octetLen = Read-DerLength -Bytes $Der -Pos ([ref]$pos)
    $innerDer = $Der[$pos..($pos + $octetLen - 1)]

    return ConvertFrom-Pkcs1RsaPrivateKey -Der $innerDer
}

Write-Host "Loading certificate and private key (pure .NET, no external tools)..."

$leafBlocks = ConvertFrom-PemBlocks -PemText (Get-Content $crt -Raw)
if ($leafBlocks.Count -eq 0) { throw "No certificate found in $crt." }
$leafCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($leafBlocks[0].Bytes)

$keyBlock = (ConvertFrom-PemBlocks -PemText (Get-Content $key -Raw))[0]
if (-not $keyBlock) { throw "No private key found in $key." }
$rsaParams = switch ($keyBlock.Label) {
    "RSA PRIVATE KEY" { ConvertFrom-Pkcs1RsaPrivateKey -Der $keyBlock.Bytes }
    "PRIVATE KEY"     { ConvertFrom-Pkcs8PrivateKeyInfo -Der $keyBlock.Bytes }
    default           { throw "Unsupported private key format '$($keyBlock.Label)' in $key. Expected 'RSA PRIVATE KEY' or 'PRIVATE KEY'." }
}

$rsa = [System.Security.Cryptography.RSA]::Create()
$rsa.ImportParameters($rsaParams)
$leafWithKey = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($leafCert, $rsa)

Write-Host "Building certificate chain (leaf + intermediates)..."
$chainBlocks = ConvertFrom-PemBlocks -PemText (Get-Content $bundle -Raw)
if ($chainBlocks.Count -eq 0) { throw "No intermediate certificates found in $bundle." }

$collection = [System.Security.Cryptography.X509Certificates.X509Certificate2Collection]::new()
# Re-import leaf-with-key via a PFX round-trip so the collection carries the private key association on export.
$leafPfxBytes = $leafWithKey.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pkcs12)
$collection.Add([System.Security.Cryptography.X509Certificates.X509Certificate2]::new($leafPfxBytes, [string]$null, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)) | Out-Null
foreach ($b in $chainBlocks) {
    $collection.Add([System.Security.Cryptography.X509Certificates.X509Certificate2]::new($b.Bytes)) | Out-Null
}

$outDir = Split-Path $OutFile -Parent
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$pfxPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 24 | ForEach-Object { [char]$_ })

Write-Host "Packaging PFX at $OutFile ..."
$pfxBytes = $collection.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pkcs12, $pfxPassword)
[System.IO.File]::WriteAllBytes($OutFile, $pfxBytes)

$rsa.Dispose()
$leafCert.Dispose()
$leafWithKey.Dispose()

if (-not (Test-Path $OutFile)) {
    throw "PFX export failed - $OutFile was not created."
}

Write-Host ""
Write-Host "PFX created: $OutFile" -ForegroundColor Green
Write-Host "PFX password (save this now - it is not stored anywhere):" -ForegroundColor Yellow
Write-Host "  $pfxPassword" -ForegroundColor Yellow
Write-Host ""
Write-Host "Pass this password to deploy.ps1 via -PfxPassword (SecureString prompt) when importing." -ForegroundColor Cyan
