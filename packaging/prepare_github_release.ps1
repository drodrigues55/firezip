# Prepares the complete GitHub Releases asset bundle for Firezip
Param(
    [string]$Version = "1.0.0",
    [switch]$Development,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       FIREZIP GITHUB RELEASE PACKAGING SUITE (v$Version)  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$distDir = Join-Path $projectRoot "dist"
$releaseDir = Join-Path $distDir "release"

if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# 1. Build installer
$installerSource = Join-Path $distDir "FirezipSetup-x64-v$Version.exe"
if (-not $SkipBuild -or -not (Test-Path $installerSource)) {
    Write-Host "[1/4] Building official Inno Setup installer..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "build_installer.ps1") -Version $Version
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build installer!"
    }
} else {
    Write-Host "[1/4] Using existing installer: $installerSource" -ForegroundColor Green
}

$installerTarget = Join-Path $releaseDir "FirezipSetup-x64-v$Version.exe"
Copy-Item $installerSource $installerTarget -Force

# 2. Package Portable ZIP
Write-Host "[2/4] Packaging portable distribution ZIP..." -ForegroundColor Yellow
$tempPortableDir = Join-Path $env:TEMP ("Firezip_Portable_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempPortableDir -Force | Out-Null

try {
    Copy-Item (Join-Path $projectRoot "Firezip.UI.exe") (Join-Path $tempPortableDir "Firezip.UI.exe") -Force
    Copy-Item (Join-Path $projectRoot "FirezipUpdater.exe") (Join-Path $tempPortableDir "FirezipUpdater.exe") -Force
    Copy-Item (Join-Path $projectRoot "README.md") (Join-Path $tempPortableDir "README.md") -Force
    Copy-Item (Join-Path $projectRoot "THIRD-PARTY-NOTICES.md") (Join-Path $tempPortableDir "THIRD-PARTY-NOTICES.md") -Force

    $portableZipTarget = Join-Path $releaseDir "Firezip-v$Version-windows-x64-portable.zip"
    if (Test-Path $portableZipTarget) { Remove-Item $portableZipTarget -Force }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempPortableDir, $portableZipTarget, [System.IO.Compression.CompressionLevel]::Optimal, $false)
}
finally {
    if (Test-Path $tempPortableDir) {
        Remove-Item $tempPortableDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 3. Generate and Sign manifest.json
Write-Host "[3/4] Generating and signing update manifest..." -ForegroundColor Yellow
$releaseScript = Join-Path $PSScriptRoot "release_update_package.ps1"
if ($Development) {
    & $releaseScript -Version $Version -PackagePath $installerTarget -OutputDir $releaseDir -Development
} else {
    & $releaseScript -Version $Version -PackagePath $installerTarget -OutputDir $releaseDir
}

# 4. Generate SHA-256 Checksums and Release Table
Write-Host "[4/4] Calculating SHA-256 checksums..." -ForegroundColor Yellow
$summaryLines = @()
$summaryLines += "| File | Size | SHA-256 Checksum |"
$summaryLines += "| :--- | :--- | :--- |"

Get-ChildItem -Path $releaseDir -File | Where-Object { $_.Extension -ne ".sha256" -and $_.Name -ne "manifest.json" } | ForEach-Object {
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
    $hashFile = $_.FullName + ".sha256"
    "$hash  $($_.Name)" | Set-Content -Path $hashFile -Encoding utf8

    $sizeMb = [math]::Round($_.Length / 1MB, 2)
    $name = $_.Name
    $summaryLines += "| `$name` | $sizeMb MB | `$hash` |"
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "       RELEASE BUNDLE READY IN dist/release/              " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
$summaryLines | ForEach-Object { Write-Host $_ -ForegroundColor Cyan }
Write-Host "==========================================================" -ForegroundColor Green
