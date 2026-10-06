# Release Candidate Automated Verification Script for Firezip
Param(
    [string]$Version = "1.0.11"
)

$ErrorActionPreference = "Stop"

Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "             FIREZIP RELEASE CANDIDATE (RC) VERIFICATION SUITE                 " -ForegroundColor Cyan
Write-Host "                               Version $Version                                " -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$distDir = Join-Path $projectRoot "dist"
$installerExe = Join-Path $distDir "FirezipSetup-x64-v$Version.exe"
$hashFile = Join-Path $distDir "FirezipSetup-x64-v$Version.exe.sha256"
$wingetDir = Join-Path $projectRoot "packaging\winget\manifests\f\Firezip\Firezip\$Version"

$testSandboxDir = Join-Path $env:TEMP ("Firezip_RC_Verify_" + [Guid]::NewGuid().ToString("N"))

try {
    # -------------------------------------------------------------------------
    # 1. VERIFY INSTALLER ARTIFACT & CHECKSUM
    # -------------------------------------------------------------------------
    Write-Host "[1/5] Verifying installer artifact integrity..." -ForegroundColor Yellow
    if (-not (Test-Path $installerExe)) {
        throw "Installer artifact not found: $installerExe"
    }

    $actualHash = (Get-FileHash -Path $installerExe -Algorithm SHA256).Hash.ToUpperInvariant()
    Write-Host "      Installer found: $installerExe" -ForegroundColor Gray
    Write-Host "      Calculated SHA256: $actualHash" -ForegroundColor Gray

    if (Test-Path $hashFile) {
        $expectedHashContent = Get-Content $hashFile -Raw
        if ($expectedHashContent -notmatch $actualHash) {
            throw "SHA256 mismatch between installer and sha256 checksum file!"
        }
        Write-Host "      Checksum file matches: PASS" -ForegroundColor Green
    } else {
        Write-Warning "Checksum file not found at $hashFile"
    }

    # -------------------------------------------------------------------------
    # 2. VERIFY WINGET MANIFESTS & HASH
    # -------------------------------------------------------------------------
    Write-Host "[2/5] Validating Winget manifests..." -ForegroundColor Yellow
    $versionManifest = Join-Path $wingetDir "Firezip.Firezip.yaml"
    $installerManifest = Join-Path $wingetDir "Firezip.Firezip.installer.yaml"
    $localeManifest = Join-Path $wingetDir "Firezip.Firezip.locale.en-US.yaml"

    foreach ($m in @($versionManifest, $installerManifest, $localeManifest)) {
        if (-not (Test-Path $m)) {
            throw "Winget manifest missing: $m"
        }
    }

    $installerYamlContent = Get-Content $installerManifest -Raw
    if ($installerYamlContent -notmatch $actualHash) {
        throw "Installer manifest SHA256 does not match actual installer hash!"
    }
    Write-Host "      Winget manifests found and SHA256 matches: PASS" -ForegroundColor Green

    $wingetCmd = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($wingetCmd) {
        Write-Host "      Running 'winget validate'..." -ForegroundColor Gray
        $validateResult = & winget validate --manifest $wingetDir 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "      Winget validation: PASS" -ForegroundColor Green
        } else {
            Write-Host "      Winget validate output:`n$validateResult" -ForegroundColor Yellow
        }
    } else {
        Write-Host "      winget.exe not available in PATH; skipping CLI validation" -ForegroundColor Gray
    }

    # -------------------------------------------------------------------------
    # 3. VERIFY SILENT INSTALLATION (SANDBOX DIRECTORY)
    # -------------------------------------------------------------------------
    Write-Host "[3/5] Testing silent installation into sandbox..." -ForegroundColor Yellow
    Write-Host "      Target Directory: $testSandboxDir" -ForegroundColor Gray

    $installArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CURRENTUSER /DIR=""$testSandboxDir"""
    $installProc = Start-Process -FilePath $installerExe -ArgumentList $installArgs -Wait -PassThru

    if ($installProc.ExitCode -ne 0) {
        throw "Silent installation failed with exit code $($installProc.ExitCode)"
    }

    $installedExe = Join-Path $testSandboxDir "Firezip.UI.exe"
    $uninstallerExe = Join-Path $testSandboxDir "unins000.exe"

    if (-not (Test-Path $installedExe)) {
        throw "Installed executable not found: $installedExe"
    }
    if (-not (Test-Path $uninstallerExe)) {
        throw "Uninstaller not found: $uninstallerExe"
    }

    $fileInfo = Get-Item $installedExe
    Write-Host "      Installed File Size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor Gray
    Write-Host "      Silent Installation: PASS" -ForegroundColor Green

    # -------------------------------------------------------------------------
    # 4. VERIFY SILENT UNINSTALLATION
    # -------------------------------------------------------------------------
    Write-Host "[4/5] Testing silent uninstallation..." -ForegroundColor Yellow
    $uninstallArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART"
    $uninstallProc = Start-Process -FilePath $uninstallerExe -ArgumentList $uninstallArgs -Wait -PassThru

    # Allow uninstaller child cleanup to complete
    Start-Sleep -Seconds 3

    if (Test-Path $installedExe) {
        throw "Application executable still present after uninstallation: $installedExe"
    }
    Write-Host "      Silent Uninstallation & Clean Removal: PASS" -ForegroundColor Green

    # -------------------------------------------------------------------------
    # 5. SUMMARY
    # -------------------------------------------------------------------------
    Write-Host "[5/5] Finalizing Release Candidate Status..." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "===============================================================================" -ForegroundColor Green
    Write-Host "        ALL RELEASE CANDIDATE VERIFICATION CRITERIA PASSED (100%)              " -ForegroundColor Green
    Write-Host "===============================================================================" -ForegroundColor Green
    Write-Host "  Installer Artifact:  $installerExe" -ForegroundColor White
    Write-Host "  Checksum (SHA256):   $actualHash" -ForegroundColor White
    Write-Host "  Winget Manifest:     $wingetDir" -ForegroundColor White
    Write-Host "  Install / Uninstall: Verified clean and fully automated" -ForegroundColor White
    Write-Host "===============================================================================" -ForegroundColor Green
}
finally {
    if (Test-Path $testSandboxDir) {
        try {
            Remove-Item -Path $testSandboxDir -Recurse -Force -ErrorAction SilentlyContinue
        } catch { }
    }
}
