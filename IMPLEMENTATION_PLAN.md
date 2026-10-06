# Implementation Plan — Free Windows Archive Manager

## 1. Product Definition

Build a native Windows desktop archive manager inspired by the usability and workflow of applications such as Bandizip and 7-Zip.

The application must be:

* 100% free
* No subscriptions
* No Premium tier
* No advertisements
* No feature gating
* No artificial file-size or archive-size limitations
* No account required
* Functional offline
* Native-feeling on Windows
* Fast startup
* Low memory usage
* Integrated with Windows Explorer

The product should provide a polished, modern UI while maintaining the familiar workflow expected from traditional archive managers.

Do not copy Bandizip's branding, logos, icons, proprietary assets, text, or exact visual design. Use it only as a functional and UX reference.

---

# 2. Technology Stack

Target platform:

* Windows 10+
* Windows 11
* x64 as the primary architecture
* ARM64 support should be considered for a later release

Application:

* C#
* .NET 10 or the current supported LTS .NET version at implementation time
* WinUI 3
* Windows App SDK

Architecture:

* MVVM
* Dependency Injection
* Async/await for long-running operations
* CancellationToken for cancellable operations

Do not use Electron.

Do not use Python for the production application.

Do not implement archive algorithms from scratch.

Use mature archive libraries/engines and verify their redistribution licenses before finalizing dependencies.

---

# 3. High-Level Architecture

Use a layered architecture:

```
ArchiveApp
│
├── ArchiveApp.UI
│
├── ArchiveApp.Core
│
├── ArchiveApp.Formats
│
├── ArchiveApp.Windows
│
├── ArchiveApp.Infrastructure
│
├── ArchiveApp.Tests
│
└── ArchiveApp.Installer
```

### ArchiveApp.UI

Responsible only for:

* Windows
* Pages
* Dialogs
* ViewModels
* Commands
* User interaction
* Progress visualization

The UI must not directly implement archive extraction/compression logic.

### ArchiveApp.Core

Contains application-independent domain logic:

* Archive models
* ArchiveEntry
* Archive metadata
* Extraction requests
* Compression requests
* Conflict policies
* Progress models
* Operation results
* Error models

### ArchiveApp.Formats

Contains adapters for archive engines.

Example:

```
IArchiveProvider
    ├── ZipProvider
    ├── SevenZipProvider
    ├── TarProvider
    ├── GZipProvider
    └── RarProvider
```

The rest of the application must interact through interfaces rather than directly depending on a specific compression library.

### ArchiveApp.Windows

Contains Windows-specific functionality:

* Explorer integration
* File associations
* Shell integration
* Windows notifications
* Windows file pickers
* Windows filesystem APIs

### ArchiveApp.Infrastructure

Contains:

* Configuration
* Logging
* Update system
* Temporary file handling
* Dependency injection setup

### ArchiveApp.Tests

Contains:

* Unit tests
* Integration tests
* Archive compatibility tests
* Security tests
* Performance benchmarks

---

# 4. Core Domain Models

Create an Archive model:

```
Archive
├── FilePath
├── Format
├── TotalEntries
├── TotalUncompressedSize
├── TotalCompressedSize
├── IsEncrypted
└── Entries
```

Create ArchiveEntry:

```
ArchiveEntry
├── FullPath
├── Name
├── Size
├── CompressedSize
├── ModifiedDate
├── IsDirectory
├── IsEncrypted
└── Attributes
```

Create operation models:

```
ExtractionRequest
CompressionRequest
TestArchiveRequest
```

Create conflict policies:

```
Overwrite
Skip
Rename
AskUser
```

All long-running operations must expose progress.

---

# 5. Archive Provider Interface

Define a common abstraction similar to:

```
IArchiveProvider
```

Responsibilities:

* Detect format
* Open archive
* Enumerate entries
* Extract entries
* Create archives
* Add entries
* Delete entries when supported
* Test archive integrity when supported

The interface must support:

* async operations
* cancellation
* progress reporting
* password callbacks
* conflict callbacks
* error reporting

The UI must never know which underlying archive engine is being used.

---

# 6. Initial Format Support

Priority 1:

* ZIP
* 7Z

Priority 2:

* TAR
* GZIP
* BZIP2
* XZ

Priority 3:

* RAR extraction
* ISO
* CAB
* additional formats supported reliably by the selected engine

RAR creation is not required.

All format support must respect the licenses and redistribution rights of the selected libraries.

If a format cannot legally be redistributed with the application, do not ship it simply to match a competitor's feature list.

---

# 7. Main User Interface

The primary window should resemble a conventional Windows archive manager.

Layout:

```
┌─────────────────────────────────────────────┐
│ ArchiveApp                              ×  │
├─────────────────────────────────────────────┤
│ File Edit Actions Tools Settings            │
├─────────────────────────────────────────────┤
│ Open  Extract  Add  Delete  Test            │
├─────────────────────────────────────────────┤
│ Search                                      │
├─────────────────────────────────────────────┤
│ Name             Size          Modified      │
│                                             │
│ Documents/                                  │
│ Images/                                     │
│ readme.txt        12 KB                    │
│ setup.exe         42 MB                    │
│                                             │
├─────────────────────────────────────────────┤
│ 127 files                         842 MB    │
└─────────────────────────────────────────────┘
```

Required actions:

* Open
* Extract
* Add
* Delete
* Test
* Refresh
* Search

Double-clicking a directory opens it.

Double-clicking a supported file should provide an option to extract/open it depending on the file type.

---

# 8. Archive Opening

When the user opens an archive:

1. Detect format.
2. Open through the appropriate provider.
3. Enumerate entries.
4. Display directory tree/list.
5. Display archive statistics.
6. Handle encrypted archives through a password prompt.
7. Handle corrupted archives gracefully.

Do not extract the entire archive merely to display its contents.

---

# 9. Extraction

Implement:

* Extract selected
* Extract all
* Extract here
* Extract to archive-name folder
* Extract to custom directory

Example:

```
archive.zip
    ↓
Extract to "archive\"
```

Provide:

* Progress
* Current file
* Files processed
* Total files
* Bytes processed
* Estimated speed
* Estimated remaining time
* Cancel button

Cancellation must be cooperative and safe.

---

# 10. Compression

Implement creation of:

* ZIP
* 7Z

Options:

* Compression level
* Store
* Fast
* Normal
* Maximum
* Ultra when supported
* Password
* Encryption
* Split archive
* Preserve timestamps
* Include/exclude files

Default settings should prioritize a balance between speed and compression ratio.

---

# 11. Conflict Handling

When extraction encounters an existing file:

```
File already exists

[Replace]
[Skip]
[Rename]
[Cancel]
```

Include:

```
Apply to all
```

Do not interrupt the operation for every file when the user chooses a global policy.

---

# 12. Drag & Drop

Support:

### Explorer → Application

Dragging archives onto the application should open them.

### Application → Explorer

Dragging archive entries out of the application should extract them to the target location.

### Explorer → Application for compression

Dragging normal files/folders into the application should provide:

```
Add to current archive
Create new archive
```

where appropriate.

---

# 13. Windows Explorer Integration

Implement context menu integration.

### Context Menu Visuals & Icon Branding

* **No Redundant Text Suffix:** Do NOT append `(Firezip)` in parentheses to the menu item text labels.
* **Native Explorer Icon Integration:** Assign the application icon directly to each context menu verb via the registry `Icon` property (e.g., `Icon="{app}\Firezip.UI.exe,0"`):
  - On Windows 10 and Windows 11, Windows Explorer renders the Firezip brand icon directly beside each context menu entry.
  - Menu labels remain clean and uncluttered:
    - `Open with Firezip`
    - `Extract Here`
    - `Extract to "<ArchiveName>\"`
    - `Extract to...`
    - `Compress to ZIP`
    - `Compress to 7Z`
* **Cascading Menu Option:** Provide an optional setting to group all Firezip verbs into a single cascading submenu (`Firezip >` with icon) to prevent polluting the top-level Windows Explorer context menu.
* **Configurable Verbs:** Allow users to enable/disable individual context menu entries via Settings.
* **Modern Windows 11 Support:** Prefer modern Windows 11 context menu integration (sparse package / `IExplorerCommand`) where technically appropriate, while maintaining robust classic registry shell verbs.

### Dedicated Task Windows (No Full UI on Context Menu Clicks)

When a user clicks a context menu action in Windows Explorer, the application **must NOT open the full application UI (`MainWindow`)**:

1. **Window Dispatching Architecture:**
   - On application startup (`App.OnLaunched`), inspect command-line arguments prior to creating any windows.
   - If invoked with a context menu task flag (`--extract-here`, `--extract-to-folder`, `--extract-to`, `--compress-zip`, `--compress-7z`), route directly to a dedicated lightweight task window.
   - Never instantiate or display the full file browser `MainWindow` (with navigation bars, file lists, menus, and search) for background or quick tasks.

2. **Dedicated Task Windows:**
   - **`TaskProgressWindow` (Quick Extraction / Quick Compression):**
     - Invoked by `--extract-here`, `--extract-to-folder`, `--compress-zip`, and `--compress-7z`.
     - Compact, focused window (e.g., ~460×200 px) showing only:
       - Operation title (e.g., "Extracting archive.zip...", "Compressing files...")
       - Current file being processed
       - Progress bar and percentage
       - Speed and estimated time remaining
       - [Cancel] button
     - **Auto-Close on Completion:** Automatically closes upon successful completion (with optional Windows notification if configured in Settings).
     - **In-Place Dialogs:** Prompts for file overwrite conflicts or archive passwords directly in-place without opening the full UI.
   - **`TaskOptionsWindow` (Extract to...):**
     - Invoked by `--extract-to`.
     - Focused modal dialog for destination folder selection and extraction options.
     - Transitions directly to `TaskProgressWindow` upon confirmation, closing immediately once done.
   - **Full `MainWindow` Execution:**
     - Opened **only** when the user explicitly clicks `Open with Firezip`, double-clicks an associated archive file, or launches the app from the Start Menu / Desktop.

---

# 14. File Associations

Register supported extensions:

* .zip
* .7z
* .tar
* .gz
* .bz2
* .xz
* others supported by the installed build

Do not forcibly take ownership of file associations during installation.

Provide an explicit setting:

```
Set ArchiveApp as default archive manager
```

---

# 15. Security Requirements

Security is a first-class requirement.

Protect against:

### Zip Slip / Path Traversal

Never allow:

```
../../file
..\..\file
C:\Windows\...
\\server\share\...
```

to escape the extraction directory.

Every output path must be canonicalized and verified before extraction.

### Symlinks / Reparse Points

Handle symbolic links and Windows reparse points safely.

Do not allow archive contents to redirect extraction outside the intended destination.

### Decompression Bombs

Detect suspicious ratios and/or extreme expansion where practical.

Allow users to cancel extraction.

### Corrupted Archives

Never crash the application because an archive is malformed.

### Password Protected Archives

Never log passwords.

Do not store archive passwords by default.

### Temporary Files

Use secure temporary directories.

Clean temporary files after successful operations and after recoverable failures.

---

# 16. Performance Requirements

Performance is a core product requirement.

Target:

* Startup: as close to instantaneous as reasonably possible
* Archive listing: responsive even with large archives
* Low idle memory usage
* Streaming extraction
* Streaming compression
* No unnecessary full-archive buffering

Never load an entire multi-gigabyte archive into RAM.

Use buffered streams.

Use asynchronous I/O where appropriate.

---

# 17. Performance Benchmarks

Create a benchmark suite using:

1. Small ZIP
2. Large ZIP
3. 1 GB archive
4. 10 GB archive
5. 100,000 small files
6. 1,000,000 small files
7. Highly compressible files
8. Already-compressed media
9. Large single files

Measure:

* Startup
* Archive opening
* Directory enumeration
* Extraction speed
* Compression speed
* Memory consumption
* CPU utilization

Compare against established tools during development.

The objective is not necessarily to beat every competitor in every benchmark, but to avoid obvious performance regressions.

---

# 18. Settings

Settings should include:

### General

* Default extraction folder
* Open extracted folder automatically
* Confirm before overwriting
* Confirm before deleting
* Default archive format

### Appearance

* Light
* Dark
* System

### Explorer

* Enable context menu with native brand icons
* Group into cascading context menu (`Firezip >`) or show individual verbs
* Configure enabled/disabled context menu entries
* Auto-close task progress window on successful completion
* Show notification upon task completion
* File associations

### Compression

* Default format
* Default compression level
* Default encryption behavior

### Advanced

* Temporary directory
* Thread count
* Logging

---

# 19. Error Handling

Errors should be human-readable.

Bad:

```
HRESULT 0x80004005
```

Good:

```
Could not extract "video.mp4".
The archive appears to be corrupted or incomplete.
```

Provide:

* Human-readable error
* Technical details expandable by the user
* Copy error details button

Never show stack traces in the normal UI.

---

# 20. Logging

Implement structured logging.

Levels:

* Error
* Warning
* Information
* Debug

Default:

* Error
* Warning
* important operational information

Do not log:

* passwords
* private archive contents
* unnecessary personal file paths when avoidable

Provide:

```
Settings → Advanced → Open log folder
```

---

# 21. Installer

Create a professional Windows installer.

Requirements:

* Install/uninstall
* Start Menu shortcut
* Optional Desktop shortcut
* File association registration
* Explorer integration
* Upgrade without losing settings
* Clean uninstall

Do not install unrelated software.

Do not bundle advertising software.

Do not modify browser settings.

Do not use deceptive installer checkboxes.

---

# 22. Auto Update

Implement an optional update mechanism.

Flow:

```
Check update
    ↓
Download
    ↓
Verify signature/hash
    ↓
Install
    ↓
Restart
```

Users should be able to disable automatic update checks.

The application must remain fully functional when offline.

---

# 23. Accessibility

Support:

* Keyboard navigation
* Standard Windows shortcuts
* Screen readers where practical
* Proper focus handling
* High contrast
* Scalable UI
* Accessible names for controls

Important keyboard shortcuts:

```
Ctrl+O    Open
Ctrl+F    Search
Ctrl+A    Select all
Ctrl+C    Copy
Ctrl+V    Paste
Delete    Delete entry
F5        Refresh
Esc       Cancel operation
```

---

# 24. Testing Strategy

Create unit tests for:

* Path validation
* Conflict resolution
* Format detection
* Archive models
* Settings
* Extraction rules

Integration tests for:

* ZIP extraction
* ZIP creation
* 7Z extraction
* 7Z creation
* encrypted archives
* corrupted archives
* Unicode filenames
* long filenames
* nested directories
* empty directories
* large files

Security tests:

* Zip Slip
* absolute paths
* Windows paths
* UNC paths
* symbolic links
* malformed archives
* decompression bombs

---

# 25. Compatibility Test Corpus

Maintain a test archive corpus:

```
/TestArchives
    /zip
    /7z
    /tar
    /gzip
    /rar
    /corrupted
    /encrypted
    /unicode
    /large
    /security
```

Every release candidate must pass the compatibility suite.

---

# 26. Development Milestones

## Milestone 0 — Project Setup

Deliver:

* Repository
* Solution
* Projects
* CI
* Formatting
* Static analysis
* Unit test framework
* Logging
* Dependency injection

No UI polish yet.

---

## Milestone 1 — Archive Core

Deliver:

* Archive model
* Archive provider abstraction
* Format detection
* ZIP provider
* 7Z provider
* Enumeration
* Basic extraction

Success criteria:

A ZIP and 7Z archive can be opened and extracted through automated tests.

---

## Milestone 2 — Compression

Deliver:

* ZIP creation
* 7Z creation
* Compression levels
* Password protection
* Split archives

Success criteria:

Archives created by the application open correctly in 7-Zip and other major archive managers.

---

## Milestone 3 — Main UI

Deliver:

* Main window
* Archive browser
* Toolbar
* Menu
* File list
* Directory navigation
* Search
* Properties/statistics

---

## Milestone 4 — Extraction UX

Deliver:

* Destination picker
* Progress dialog
* Cancellation
* Conflict handling
* Error handling
* Open destination folder

---

## Milestone 5 — Windows Integration

Deliver:

* File associations for supported archive formats (.zip, .7z, .rar, .tar, .gz, .bz2)
* Explorer context menu with native application icon integration (clean labels without `(Firezip)` text suffix)
* Dedicated lightweight task windows:
  - `TaskProgressWindow`: compact modal progress window for quick extraction/compression verbs, auto-closing upon completion
  - `TaskOptionsWindow`: standalone destination/options dialog for "Extract to...", transitioning to progress without launching full UI
  - Early command-line routing in `App.OnLaunched` bypassing `MainWindow` for context menu verbs
* Drag & drop (Explorer to App, App to Explorer)
* Shell operations and file conflict management
* Windows notifications for completed background tasks

---

## Milestone 6 — Advanced Features

Deliver:

* Test archive
* Add files
* Delete files
* Batch operations
* Archive comments where supported
* Advanced compression settings
* Encryption settings
* Volume splitting

---

## Milestone 7 — Security & Hardening

Deliver:

* Path traversal protection
* Reparse point handling
* Malformed archive handling
* Temporary file security
* Resource limits
* Fuzz testing where practical

---

## Milestone 8 — Performance

Deliver:

* Benchmark suite
* Startup optimization
* Large archive optimization
* Memory optimization
* I/O optimization
* UI virtualization where needed

---

## Milestone 9 — Installer, Winget & Update

Deliver:

* Standalone Windows Installer (Inno Setup / WiX)
* Windows Package Manager (Winget) official manifest suite (`Firezip.Firezip`)
* Clean Uninstaller (`unins000.exe`) with complete registry and association cleanup
* Update mechanism via GitHub Releases API (`UpdateChecker.cs`)
* Version migration & settings preservation
* Dedicated sub-plan: [MILESTONE_9_PLAN.md](file:///c:/Users/DRODRIGUES/Documents/firezip/MILESTONE_9_PLAN.md)

---

## Milestone 10 — Release Candidate

Perform:

* Full regression testing
* Compatibility testing
* Security testing
* Performance testing
* Clean-install testing
* Upgrade testing
* Uninstall testing
* Windows 10 testing
* Windows 11 testing

---

## Milestone 11 — General Availability (GA) & Internationalization (i18n)

Deliver:

* Production-ready versioning unified at v1.0.0 (`Directory.Build.props`)
* Full assembly and package metadata (Company, Copyright 2026, MIT license, repository links)
* Multi-language / Internationalization framework (`ILocalizationService`, `LocalizationService`)
* Complete catalog in English (`en-US`) and Brazilian Portuguese (`pt-BR`)
* Runtime and settings language switcher (`LanguageComboBox`)
* Winget v1.0.0 manifests including dedicated `pt-BR` locale manifest
* Dedicated sub-plan: [MILESTONE_11_PLAN.md](file:///c:/Users/DRODRIGUES/Documents/firezip/MILESTONE_11_PLAN.md)

---

# 27. Definition of Done

A feature is complete only when:

1. Implementation is complete.
2. UI is implemented.
3. Error handling exists.
4. Cancellation is supported where applicable.
5. Unit tests exist.
6. Integration tests exist where applicable.
7. Security implications have been reviewed.
8. Logging exists where appropriate.
9. Documentation is updated.
10. The feature does not introduce unnecessary dependencies.
11. The feature does not block offline usage.
12. The feature does not introduce advertisements or monetization.
13. The feature does not degrade startup or Explorer performance significantly.

---

# 28. Development Principles

Prioritize in this order:

1. Correctness
2. Security
3. Reliability
4. Performance
5. Windows integration
6. Usability
7. Visual polish

Do not sacrifice archive correctness for UI polish.

Do not sacrifice security for performance.

Do not implement features merely because another archive manager has them if the implementation is unreliable or legally problematic.

Prefer simple, maintainable code over premature abstraction.

Keep archive engine dependencies isolated behind interfaces.

The application should remain usable even if one archive format fails to load.

---

# 29. Initial MVP Definition

The first public alpha should support:

* ZIP
* 7Z
* Open
* Browse
* Search
* Extract
* Create ZIP
* Create 7Z
* Drag & drop
* Progress
* Cancellation
* Conflict handling
* Windows Explorer integration
* Dark/light/system theme
* Basic settings

The alpha does NOT need:

* Every possible archive format
* SFX
* Advanced batch processing
* Repair tools
* Cloud integration
* Account system
* Telemetry
* Monetization

The goal of the MVP is to establish a fast, reliable foundation before expanding format support.

---

# 30. Final Product Principle

The application should feel like a tool that belongs on Windows rather than a web application packaged as an EXE.

A user should be able to install it and immediately understand:

```
Right click → Extract Here
```

or:

```
Right click files → Compress to ZIP
```

and advanced users should be able to open the application and perform detailed archive operations without encountering artificial restrictions.

The final product should be:

```
Free
Fast
Native
Reliable
Offline
Privacy-friendly
Full-featured
Simple
```