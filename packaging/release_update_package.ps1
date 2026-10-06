Param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [string]$PackagePath,

    [Parameter(Mandatory=$false)]
    [string]$DownloadUrl,

    [Parameter(Mandatory=$false)]
    [string]$PrivateKeyFile,

    [Parameter(Mandatory=$false)]
    [string]$PrivateKeyXml,

    [Parameter(Mandatory=$false)]
    [string]$OutputDir,

    [Parameter(Mandatory=$false)]
    [string]$MinimumSupportedVersion = "1.0.0",

    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = "Firezip Update",

    [Parameter(Mandatory=$false)]
    [switch]$Mandatory,

    [Parameter(Mandatory=$false)]
    [switch]$Development
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         FIREZIP UPDATE RELEASE ENGINEERING SCRIPT        " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$scriptDir = $PSScriptRoot
$projectRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path

if (-not $OutputDir) {
    $OutputDir = Join-Path $projectRoot "dist"
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Resolve Private Key:
# Priority 1: $PrivateKeyXml argument
# Priority 2: $env:FIREZIP_RELEASE_PRIVATE_KEY or $env:MYUNZIP_RELEASE_PRIVATE_KEY
# Priority 3: $PrivateKeyFile argument
# Priority 4: Development fallback (only if -Development specified)
$finalPrivateKeyXml = $null

if ($PrivateKeyXml) {
    $finalPrivateKeyXml = $PrivateKeyXml
} elseif ($env:FIREZIP_RELEASE_PRIVATE_KEY) {
    $finalPrivateKeyXml = $env:FIREZIP_RELEASE_PRIVATE_KEY
} elseif ($env:MYUNZIP_RELEASE_PRIVATE_KEY) {
    $finalPrivateKeyXml = $env:MYUNZIP_RELEASE_PRIVATE_KEY
} elseif ($PrivateKeyFile -and (Test-Path $PrivateKeyFile)) {
    $finalPrivateKeyXml = Get-Content -Path $PrivateKeyFile -Raw
} elseif ($Development) {
    $devKeyFile = Join-Path $scriptDir "keys\firezip_private_key.xml"
    if (Test-Path $devKeyFile) {
        Write-Warning "DEVELOPMENT MODE: Using local development/testing private key. DO NOT USE FOR PUBLIC PRODUCTION RELEASES!"
        $finalPrivateKeyXml = Get-Content -Path $devKeyFile -Raw
    }
}

if (-not $finalPrivateKeyXml) {
    Write-Error "Production release private key not provided! Set env:FIREZIP_RELEASE_PRIVATE_KEY or supply -PrivateKeyXml/-PrivateKeyFile, or pass -Development for local testing."
}

# 1. Package validation & SHA-256 calculation
if (-not $PackagePath) {
    # Default to installer in dist
    $PackagePath = Join-Path $OutputDir "FirezipSetup-x64-v$Version.exe"
    if (-not (Test-Path $PackagePath)) {
        # Check root Firezip.UI.exe
        $PackagePath = Join-Path $projectRoot "Firezip.UI.exe"
    }
}

if (-not (Test-Path $PackagePath)) {
    Write-Error "Package file not found at: $PackagePath"
}

Write-Host "[*] Calculating SHA-256 for: $PackagePath" -ForegroundColor Yellow
$hashBytes = (Get-FileHash -Path $PackagePath -Algorithm SHA256)
$sha256 = $hashBytes.Hash.ToUpperInvariant()
Write-Host "    SHA-256: $sha256" -ForegroundColor Green

if (-not $DownloadUrl) {
    $fileName = [System.IO.Path]::GetFileName($PackagePath)
    $DownloadUrl = "https://github.com/drodrigues55/firezip/releases/download/v$Version/$fileName"
}

Write-Host "[*] Target Download URL: $DownloadUrl" -ForegroundColor White

# 2. Canonical Payload Creation (version|sha256|url|min_ver|mandatory)
$cleanVer = $Version.TrimStart('v', 'V').Trim()
$cleanMinVer = $MinimumSupportedVersion.TrimStart('v', 'V').Trim()
$isMandatory = if ($Mandatory.IsPresent) { "true" } else { "false" }
$canonicalPayload = "$cleanVer|$sha256|$DownloadUrl|$cleanMinVer|$isMandatory"
Write-Host "[*] Canonical Payload: $canonicalPayload" -ForegroundColor White

# 3. Cryptographic RSA-SHA256 Signing
Write-Host "[*] Signing payload using RSA 2048-bit Private Key..." -ForegroundColor Yellow
$rsa = [System.Security.Cryptography.RSA]::Create()
$rsa.FromXmlString($finalPrivateKeyXml)
$payloadBytes = [System.Text.Encoding]::UTF8.GetBytes($canonicalPayload)
$signatureBytes = $rsa.SignData($payloadBytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
$base64Signature = [Convert]::ToBase64String($signatureBytes)
$rsa.Dispose()

Write-Host "    RSA Signature: $base64Signature" -ForegroundColor Green

# 4. Generate JSON Manifest
$manifestObj = [ordered]@{
    version = $cleanVer
    minimum_supported_version = $MinimumSupportedVersion
    download_url = $DownloadUrl
    sha256 = $sha256
    signature = $base64Signature
    mandatory = [bool]$Mandatory.IsPresent
    release_notes = $ReleaseNotes
}

$manifestJson = $manifestObj | ConvertTo-Json -Depth 5
$manifestPath = Join-Path $OutputDir "manifest.json"
[System.IO.File]::WriteAllText($manifestPath, $manifestJson, [System.Text.Encoding]::UTF8)

Write-Host "[*] Written update manifest to: $manifestPath" -ForegroundColor Green

# 5. Verify the generated manifest using the Public Key
$publicKeyFile = Join-Path $scriptDir "keys\firezip_public_key.xml"
if (Test-Path $publicKeyFile) {
    $publicKeyXml = Get-Content -Path $publicKeyFile -Raw
    $verifyRsa = [System.Security.Cryptography.RSA]::Create()
    $verifyRsa.FromXmlString($publicKeyXml)
    $isValid = $verifyRsa.VerifyData($payloadBytes, $signatureBytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $verifyRsa.Dispose()

    if ($isValid) {
        Write-Host "[+] Self-test verification passed: Manifest signature is 100% VALID!" -ForegroundColor Green
    } else {
        Write-Error "Self-test verification FAILED! Signature does not match public key."
    }
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "[+] Release package generated and signed successfully!" -ForegroundColor Green
Write-Host "    Manifest: $manifestPath" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
