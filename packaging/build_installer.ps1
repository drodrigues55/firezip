Param(
    [string]$Version = "1.0.0",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "             BUILDING FIREZIP WINDOWS INSTALLER           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Locate ISCC.exe
$isccCandidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles (x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)

$isccPath = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $isccPath) {
    Write-Error "Inno Setup Compiler (ISCC.exe) was not found. Please install it using: winget install JRSoftware.InnoSetup --silent"
}

Write-Host "[*] Found Inno Setup Compiler at: $isccPath" -ForegroundColor Green

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$rootExe = Join-Path $projectRoot "Firezip.UI.exe"

# 2. Build and Package application if not skipping
if (-not $SkipBuild) {
    Write-Host "[*] Publishing Firezip.UI (Release)..." -ForegroundColor Yellow
    dotnet publish (Join-Path $projectRoot "src\Firezip.UI\Firezip.UI.csproj") -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish Firezip.UI"
    }

    $uiPublishDir = Join-Path $projectRoot "src\Firezip.UI\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish"
    $launcherObjDir = Join-Path $projectRoot "src\Firezip.Launcher\obj"
    if (-not (Test-Path $launcherObjDir)) {
        New-Item -ItemType Directory -Path $launcherObjDir -Force | Out-Null
    }

    $payloadZip = Join-Path $launcherObjDir "FirezipPayload.zip"
    if (Test-Path $payloadZip) {
        Remove-Item $payloadZip -Force
    }

    Write-Host "[*] Creating FirezipPayload.zip from published assets..." -ForegroundColor Yellow
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($uiPublishDir, $payloadZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

    Write-Host "[*] Publishing Firezip.Launcher single-file executable..." -ForegroundColor Yellow
    dotnet publish (Join-Path $projectRoot "src\Firezip.Launcher\Firezip.Launcher.csproj") -c Release -r win-x64
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish Firezip.Launcher"
    }

    $launcherExe = Join-Path $projectRoot "src\Firezip.Launcher\bin\Release\net10.0-windows\win-x64\publish\Firezip.UI.exe"
    Copy-Item $launcherExe $rootExe -Force

    Write-Host "[*] Publishing Firezip.Updater (Release)..." -ForegroundColor Yellow
    dotnet publish (Join-Path $projectRoot "src\Firezip.Updater\Firezip.Updater.csproj") -c Release -r win-x64 --self-contained false
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish Firezip.Updater"
    }

    $updaterPublishExe = Join-Path $projectRoot "src\Firezip.Updater\bin\Release\net10.0-windows\win-x64\publish\FirezipUpdater.exe"
    $rootUpdaterExe = Join-Path $projectRoot "FirezipUpdater.exe"
    Copy-Item $updaterPublishExe $rootUpdaterExe -Force

    # Aliases for MyUnzip / Firezip interchangeable naming
    Copy-Item $rootExe (Join-Path $projectRoot "MyUnzip.exe") -Force
    Copy-Item $rootUpdaterExe (Join-Path $projectRoot "MyUnzipUpdater.exe") -Force
}

if (-not (Test-Path $rootExe)) {
    Write-Error "Firezip.UI.exe not found at project root: $rootExe"
}
$rootUpdaterExe = Join-Path $projectRoot "FirezipUpdater.exe"
if (-not (Test-Path $rootUpdaterExe)) {
    Write-Error "FirezipUpdater.exe not found at project root: $rootUpdaterExe"
}
Write-Host "[*] Found application executable: $rootExe" -ForegroundColor Green
Write-Host "[*] Found updater executable:     $rootUpdaterExe" -ForegroundColor Green

# 3. Create dist output directory
$distDir = Join-Path $PSScriptRoot "..\dist"
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

# 4. Compile Inno Setup Script
$issScript = Join-Path $PSScriptRoot "installer\firezip_setup.iss"
Write-Host "[*] Compiling installer using $issScript ..." -ForegroundColor Yellow

& $isccPath "/DMyAppVersion=$Version" $issScript

if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed with exit code $LASTEXITCODE"
}

$installerOutput = Join-Path $distDir "FirezipSetup-x64-v$Version.exe"
if (-not (Test-Path $installerOutput)) {
    Write-Error "Expected installer output not found: $installerOutput"
}

# 5. Compute SHA-256
$hash = (Get-FileHash -Path $installerOutput -Algorithm SHA256).Hash
$hashFile = Join-Path $distDir "FirezipSetup-x64-v$Version.exe.sha256"
"$hash  FirezipSetup-x64-v$Version.exe" | Out-File -FilePath $hashFile -Encoding utf8

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "[+] Installer successfully created!" -ForegroundColor Green
Write-Host "    Path:   $installerOutput" -ForegroundColor White
Write-Host "    SHA256: $hash" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
