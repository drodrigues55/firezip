# Firezip 🔥🗜️

<p align="center">
  <strong>The ultra-fast, modern archive manager crafted for Windows 11.</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Platform-Windows%2011%20%7C%2010-0078D6?style=flat-square&logo=windows&logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/Architecture-x64-blue?style=flat-square" alt="x64" />
  <img src="https://img.shields.io/badge/UI-WinUI%203%20%2F%20Windows%20App%20SDK-0078D4?style=flat-square" alt="WinUI 3" />
  <img src="https://img.shields.io/badge/Tests-121%20Passed%20(100%25)-success?style=flat-square" alt="Tests" />
  <img src="https://img.shields.io/badge/License-MIT-green?style=flat-square" alt="License MIT" />
  <img src="https://img.shields.io/badge/Release-v1.0.0%20(GA)-success?style=flat-square" alt="Version 1.0.0" />
</p>

---

## ⚡ Overview

**Firezip** is an open-source, modern archive manager engineered in C# on **.NET 10** with **WinUI 3** and the **Windows App SDK**. Built to eliminate the clunky, decades-old interfaces of legacy archivers, Firezip delivers a fluid **Mica backdrop**, native dark/light themes, multi-language support (**English & Português do Brasil**), seamless Windows Explorer shell integration with official app icons, dedicated micro-task progress windows, complete offline network isolation, and enterprise-grade security hardening against decompression vulnerabilities.

---

## ✨ Key Features

### 🎨 Next-Generation Windows 11 UI
- **Mica Material & Fluent Design:** Modern translucent backdrop matching Windows 11 visual aesthetics.
- **Adaptive Dark / Light Themes:** Automatic detection of system theme with instant runtime switching.
- **Keyboard-Centric Navigation:** Full shortcut coverage (`Ctrl+O` Open, `Ctrl+N` New, `Ctrl+E` Extract, `Delete`, `F5` Refresh, `Enter` Open Folder, `Backspace` Up Level).
- **In-Memory O(1) Folder Navigation:** Virtualized file listing with `ObservableRangeCollection` batch updates supporting archives with 10,000+ files smoothly.

### 📦 Comprehensive Multi-Format Engine
- **Read & Write:**
  - **ZIP:** Standard, Deflate64, BZip2, LZMA compression, and AES-256 encryption.
  - **7-Zip (.7z):** High-ratio LZMA & LZMA2 compression, header encryption, AES-256.
  - **TAR (.tar):** Standard POSIX/ustar archive creation and extraction.
  - **GZip (.tar.gz, .tgz):** Tar streams compressed with GZip.
  - **BZip2 (.tar.bz2, .tbz2):** Tar streams compressed with BZip2.
- **Read & Extract:**
  - **RAR:** Full extraction support for RAR4 and RAR5 formats.
  - **XZ (.tar.xz, .txz, .xz):** Full decompression support.
  - **ISO & CAB:** Archive reading and extraction.

### 🪟 Dedicated Micro Task Windows
- **Lightweight Progress Dialogs:** Context menu operations launch a dedicated ~500×240 px task window with real-time throughput (MB/s), elapsed time, ETA countdown, and item counts.
- **Interactive In-Place Dialogs:**
  - **File Conflict Resolution:** Overwrite, Skip, Rename, or Auto-rename with "Apply to all" toggles.
  - **Password Prompts:** In-place secure password entry with show/hide password toggle.
- **Configurable Auto-Close:** Automatically closes upon operation completion or displays detailed error logs if issues occur.

### 📁 Native Windows Shell Integration
- **Clean Context Menu:** Direct integration into the Windows File Explorer right-click menu with native application branding icons (`{exe},0`).
- **Cascading Submenu Option:** Group all commands under a single `Firezip >` menu or show individual top-level verbs.
- **Smart Task Verbs:**
  - `Extract Here` (`--extract-here`)
  - `Extract to Folder` (`--extract-to-folder`)
  - `Extract to...` (`--extract-to`)
  - `Compress to .zip` (`--compress-zip`)
  - `Compress to .7z` (`--compress-7z`)

### 🛡️ Enterprise-Grade Security Hardening
- **Path Traversal / Zip Slip Protection:** Strict path validation rejecting directory traversal tokens (`../`, `..\`), absolute drive letters (`C:\`), and network UNC paths (`\\share\`).
- **Alternate Data Streams (ADS) Neutralization:** Blocks NTFS stream exploits (`file.txt:hidden`).
- **DOS Reserved Names Filter:** Blocks legacy Windows device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1-9`, `LPT1-9`).
- **Decompression Bomb (Zip Bomb) Defense:** Real-time streaming budget counter aborts decompression immediately if an entry exceeds safe expansion ratios (100:1) or specified caps.
- **Malformed Archive Resiliency:** Graceful exception recovery without process hangs or memory leaks.

---

## 📊 Performance Benchmarks

Measured on a standard Windows 11 x64 environment running .NET 10 Release build:

| Benchmark Operation | Target / Format | Time (ms) | Throughput / Metric |
| :--- | :---: | :---: | :---: |
| **Compress Dataset (500 files, ~12 MB)** | `ZIP` (Normal) | 345 ms | **34.60 MB/s** |
| **Open & Enumerate Archive** | `ZIP` | 47 ms | 501 entries |
| **Extract Full Dataset** | `ZIP` | 585 ms | **20.42 MB/s** |
| **Compress Dataset (500 files, ~12 MB)** | `7Z` (LZMA2) | 185 ms | **64.52 MB/s** |
| **Open & Enumerate Archive** | `7Z` | 33 ms | 501 entries |
| **Extract Full Dataset** | `7Z` | 1,198 ms | **9.97 MB/s** |
| **In-Memory Directory Indexing** | 10,000 entries | 1 ms | 10,000 items indexed |
| **O(1) Folder Navigation Queries** | 10,000 entries | 1 ms (100x) | **12.9 µs / query** |

---

## 🚀 Installation

### Option 1: Windows Package Manager (Winget)
```powershell
winget install Firezip.Firezip
```

### Option 2: Setup Installer (Inno Setup)
Download `FirezipSetup-x64-v1.0.0.exe` from the [Latest Release](https://github.com/drodrigues55/firezip/releases).

**Silent / Unattended Install:**
```powershell
FirezipSetup-x64-v1.0.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-
```

### Option 3: Portable Executable
Download the standalone single-file `Firezip.UI.exe` directly from the release page. Run anywhere without administrator rights.

---

## 💻 Command-Line Interface (CLI)

Firezip supports automated scripting and Explorer shell verbs through command-line arguments:

```powershell
# Extract archive directly into current directory
Firezip.UI.exe --extract-here "C:\path\to\archive.zip"

# Extract archive into a subfolder named after the archive
Firezip.UI.exe --extract-to-folder "C:\path\to\archive.7z"

# Open the task options dialog to choose extraction target
Firezip.UI.exe --extract-to "C:\path\to\archive.tar.gz"

# Quickly compress one or multiple files/folders into a ZIP
Firezip.UI.exe --compress-zip "C:\path\to\file1.txt" "C:\path\to\folder2"

# Quickly compress into a high-compression 7-Zip (.7z) archive
Firezip.UI.exe --compress-7z "C:\path\to\data_directory"
```

---

## 🛠️ Architecture & Solution Layout

Firezip follows clean architecture principles with distinct separation of concerns:

```
src/
├── Firezip.Core/            # Domain models, interfaces, and algorithms (PathSanitizer, RangeCollections)
├── Firezip.Formats/         # Format providers (ZIP, 7Z, TAR, GZ, BZ2, XZ, RAR), ArchiveEngine, FormatDetector
├── Firezip.Infrastructure/  # Settings service, logging, update checker (GitHub Releases API)
├── Firezip.Windows/         # Win32 P/Invoke, Shell context menu registration, File associations
├── Firezip.UI/              # WinUI 3 desktop application, MVVM ViewModels, Mica window, Dialogs
├── Firezip.Launcher/        # Single-file bundle bootstrapping and process management
├── Firezip.Benchmarks/      # Benchmark suite measuring throughput and query latencies
└── Firezip.Tests/           # xUnit test suite (Security, Compatibility, Options, Detection) - 93 tests
```

---

## 🔨 Building from Source

### Prerequisites
1. Windows 10 (Build 19041+) or Windows 11 (recommended).
2. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
3. [Inno Setup 6](https://jrsoftware.org/isinfo.php) (optional, required only for compiling the installer).

### Build Instructions
```powershell
# Clone repository
git clone https://github.com/drodrigues55/firezip.git
cd firezip

# Build entire solution
dotnet build Firezip.slnx -c Release

# Run complete test suite (93 tests)
dotnet test

# Run performance benchmarks
dotnet run --project src/Firezip.Benchmarks -c Release

# Build official Inno Setup installer & Winget manifests
powershell -ExecutionPolicy Bypass -File packaging/build_installer.ps1
powershell -ExecutionPolicy Bypass -File packaging/generate_winget_manifest.ps1
powershell -ExecutionPolicy Bypass -File packaging/verify_release_candidate.ps1
```

---

## 📄 License

Firezip is released under the **MIT License**. See [LICENSE](LICENSE) for details.
