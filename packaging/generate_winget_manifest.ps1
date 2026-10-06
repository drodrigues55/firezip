# Firezip Winget Manifest Generator Script
Param(
    [string]$Version = "1.0.11",
    [string]$InstallerPath = "",
    [string]$ReleaseBaseUrl = "https://github.com/drodrigues55/firezip/releases/download/v"
)

$ErrorActionPreference = "Stop"

if (-not $InstallerPath) {
    $InstallerPath = Join-Path $PSScriptRoot "..\dist\FirezipSetup-x64-v$Version.exe"
}

if (-not (Test-Path $InstallerPath)) {
    Write-Error "Installer file not found at: $InstallerPath. Run build_installer.ps1 first."
}

Write-Host "[*] Calculating SHA-256 for: $InstallerPath" -ForegroundColor Cyan
$sha256 = (Get-FileHash -Path $InstallerPath -Algorithm SHA256).Hash.ToUpperInvariant()
Write-Host "[+] SHA-256: $sha256" -ForegroundColor Green

$manifestDir = Join-Path $PSScriptRoot "winget\manifests\f\Firezip\Firezip\$Version"
if (-not (Test-Path $manifestDir)) {
    New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
}

$installerUrl = "$ReleaseBaseUrl$Version/FirezipSetup-x64-v$Version.exe"

# 1. Version Manifest
$versionYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.9.0.schema.json

PackageIdentifier: Firezip.Firezip
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.9.0
"@

# 2. Installer Manifest
$installerYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.9.0.schema.json

PackageIdentifier: Firezip.Firezip
PackageVersion: $Version
InstallerType: inno
Scope: machine
InstallModes:
  - interactive
  - silent
  - silentWithProgress
InstallerSwitches:
  Silent: /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-
  SilentWithProgress: /SILENT /SUPPRESSMSGBOXES /NORESTART /SP-
UpgradeBehavior: install
FileExtensions:
  - zip
  - 7z
  - rar
  - tar
  - gz
  - bz2
ProductCode: '{8B036367-AE8C-4D88-B1F3-E18F2E23A534}_is1'
Installers:
  - Architecture: x64
    InstallerUrl: $installerUrl
    InstallerSha256: $sha256
ManifestType: installer
ManifestVersion: 1.9.0
"@

# 3. Locale Manifest
$localeYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.9.0.schema.json

PackageIdentifier: Firezip.Firezip
PackageVersion: $Version
PackageLocale: en-US
Publisher: Firezip Contributors
PublisherUrl: https://github.com/drodrigues55/firezip
PublisherSupportUrl: https://github.com/drodrigues55/firezip/issues
PackageName: Firezip
PackageUrl: https://github.com/drodrigues55/firezip
License: MIT
LicenseUrl: https://github.com/drodrigues55/firezip/blob/main/LICENSE
Copyright: Copyright (c) 2026 Firezip Contributors
ShortDescription: 100% free, fast, and secure native Windows archive manager built with .NET 10 and WinUI 3.
Description: |-
  Firezip is a modern, high-performance Windows desktop archive manager inspired by the workflows of Bandizip and 7-Zip.
  Features:
  - 100% free, zero ads, zero telemetry, no subscriptions.
  - Native Windows 10/11 WinUI 3 interface with dark/light theme support.
  - Streaming ZIP, 7Z, TAR, GZ, and RAR archive reading and creation.
  - Built-in Zip Slip, directory traversal, and decompression bomb protection.
  - Explorer drag-and-drop integration.
Moniker: firezip
Tags:
  - archive
  - archiver
  - compression
  - decompression
  - zip
  - 7zip
  - rar
  - extractor
ReleaseNotesUrl: https://github.com/drodrigues55/firezip/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: 1.9.0
"@

# Write files with UTF-8 without BOM or standard UTF-8
[System.IO.File]::WriteAllText((Join-Path $manifestDir "Firezip.Firezip.yaml"), $versionYaml, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText((Join-Path $manifestDir "Firezip.Firezip.installer.yaml"), $installerYaml, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText((Join-Path $manifestDir "Firezip.Firezip.locale.en-US.yaml"), $localeYaml, [System.Text.Encoding]::UTF8)

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "[+] Winget manifests created successfully in:" -ForegroundColor Green
Write-Host "    $manifestDir" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
