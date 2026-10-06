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

## Milestone 10 — Finalização da Base Atual & Homologação RC

> **Status:** Concluído (Complete) ✅  
> **Resultado:** 123 testes aprovados (100%), 0 erros, 0 warnings (.NET 10).

Consolidação completa da base técnica do produto antes da etapa de publicação:
* **Compatibilidade Multi-Formato & Corpus:** Suporte completo de leitura e escrita para ZIP, 7Z, TAR, GZ, BZ2 e extração de RAR, testado contra caminhos longos, caracteres Unicode/acentuação/emojis e árvores com mais de 15 níveis.
* **Segurança & Hardening:** Proteção ativa contra Zip Slip, path traversal, streams ADS, dispositivos reservados DOS e bombas de descompressão (*Zip Bombs*) com monitoramento de streaming.
* **Internacionalização (i18n):** Suporte nativo e alternância em tempo de execução para Português do Brasil (`pt-BR`) e Inglês (`en-US`), com catálogo de recursos e formatação localizada.
* **Experiência de Extração com Duplo Clique (Estilo Bandizip):** Ação configurável via preferências para duplo clique em arquivos compactados (`Abrir na Interface`, `Extrair Aqui`, `Extrair em Subpasta`, `Extrair em Pasta Pré-definida`, `Perguntar Destino`), executando diretamente sem carregar a interface pesada.
* **Interface de Extração Avançada (`TaskProgressWindow`):** Dupla barra de progresso (barra do item atual `ItemProgressBar` + barra global `TaskProgressBar`), painel expansível (*expander*) com estatísticas analíticas de taxa de compressão (ratio %, tamanhos compactado/descompactado, espaço economizado, velocidade MB/s, tempo e ETA), caixas de seleção (*tickers*) para abrir pasta e manter janela aberta, e botões pós-extração (*Abrir Pasta*, *Excluir arquivo de origem .zip*, *Fechar*).
* **Isolamento de Configurações:** Preferências do usuário armazenadas de forma desacoplada em `%APPDATA%\Firezip\settings.json` (Roaming), imunes a desinstalações limpas, reinstalações ou atualizações do sistema.
* **Integração com Shell do Windows 10/11:** Registro formal de `Capabilities`, `RegisteredApplications`, chaves de `OpenWithProgids`, `Applications\Firezip.UI.exe` e disparo de notificação nativa `SHChangeNotify(SHCNE_ASSOCCHANGED)`.
* **Diálogo Nativo "Sobre o Firezip":** Diálogo WinUI 3 Fluent exibindo versão do assembly, runtime, licença MIT e links oficiais.

---

## Milestone 11 — Release Engineering & Packaging

> **Status:** Implementado (Aguardando validação formal de máquina limpa) 🟡  
> **Objetivo:** Definir, gerar e auditar o artefato final de distribuição x64 para o ecossistema Windows e WinGet.

### 1. Especificação do Artefato
* **Plataforma & Arquitetura:** Windows 10 / Windows 11 (x64 nativo).
* **Versão Unificada:** Sincronizada em `Directory.Build.props`, `firezip_setup.iss`, scripts de build e manifestos.
* **Nomenclatura do Instalador:** `FirezipSetup-x64-v<Version>.exe` (padronizado e sem caracteres especiais).
* **Tipo Real do Instalador:** **`inno`** (Inno Setup Compiler 6.7.3).  
  *Critério Mandatório:* O WinGet deve identificar explicitamente `InstallerType: inno`. Nunca assumir `InstallerType: exe` genérico.
* **Método de Empacotamento:** Automação via script PowerShell `packaging/build_installer.ps1`, que compila os binários em Release, cria o payload comprimido e executa `ISCC.exe`.

### 2. Validação do Comportamento do Instalador
* `[Implementado]` **Instalação Silenciosa:** Suporte nativo aos switches Inno Setup:
  ```powershell
  FirezipSetup-x64-v<Version>.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-
  ```
* `[Implementado]` **Instalação com Progresso (SilentWithProgress):**
  ```powershell
  FirezipSetup-x64-v<Version>.exe /SILENT /SUPPRESSMSGBOXES /NORESTART /SP-
  ```
* `[Implementado]` **Comportamento Não Interativo:** Configuração de `skipifsilent` em todas as entradas de inicialização pós-instalação (`[Run]`) do Inno Setup para garantir que nenhum diálogo, aplicativo ou navegador seja aberto durante a execução silenciosa em pipelines do WinGet.
* `[Implementado]` **Escopo e Localização:** `Scope: machine`, instalando por padrão no diretório de programas do Windows: `{autopf}\Firezip` (`C:\Program Files\Firezip`).
* `[Implementado]` **Controle de Elevação e UAC:** Instalação em máquina exige elevação de privilégios de administrador. `PrivilegesRequiredOverridesAllowed=commandline dialog` configurado para compatibilidade.
* `[Implementado]` **Entrada em Apps & Features (Registro de Desinstalação):**
  - Chave de desinstalação: `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8B036367-AE8C-4D88-B1F3-E18F2E23A534}_is1`
  - Campos registrados: `DisplayName`, `DisplayVersion`, `Publisher`, `UninstallString`, `QuietUninstallString`, `DisplayIcon`, `URLInfoAbout`, `InstallLocation`.
* `[Implementado]` **Desinstalação Limpa:** Binário `unins000.exe` gerado em `{app}`, suportando `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.

---

## Milestone 12 — Security & Installer Audit

> **Status:** Implementado (Aguardando auditoria em ambiente Windows Sandbox) 🟡  
> **Objetivo:** Executar auditoria profunda de segurança, integridade de componentes e ausência de comportamento persistente indevido.

### 1. Garantia da Arquitetura de Isolamento de Rede
Confirmar e auditar que a divisão arquitetural do Firezip permanece estritamente preservada:
```text
Firezip.UI.exe
    └── Aplicativo Principal & Motor de Extração
        └── 100% ISOLADO DA REDE (Zero permissões de rede, sem chamadas HTTP, sem sockets)

FirezipUpdater.exe
    └── Processo Separado de Atualização
        └── Conexões HTTPS restritas aos endpoints de release oficial
        └── Validação criptográfica de integridade de pacote com assinatura RSA-SHA256 (2048 bits)
```
*Critério:* Não alterar essa divisão de isolamento. O executável principal de interface nunca deve realizar requisições de rede.

### 2. Matriz de Auditoria do Instalador
* `[Implementado]` **Integridade do Instalador:** Compressão de alta taxa `lzma2/ultra64` em modo sólido com processo isolado (`LZMAUseSeparateProcess=yes`).
* `[Implementado]` **Ausência de Arquivos Inesperados:** Apenas `Firezip.UI.exe`, `FirezipUpdater.exe` e os aliases legados são instalados em `{app}`.
* `[Implementado]` **Autonomia de Runtimes & Dependências:** Executável publicado como single-file com assemblies do .NET 10 e Windows App SDK empacotados, sem dependências externas ausentes ou instaladores secundários em cadeia.
* `[Implementado]` **Componentes Persistentes & Tarefas Agendadas:**
  - O instalador pode criar opcionalmente a tarefa agendada `Firezip\FirezipUpdateTask` (`schtasks /Create`) para checagem diária silenciosa de atualizações via `FirezipUpdater.exe --auto --silent`.
  - A desinstalação garante a exclusão estrita da tarefa (`schtasks /Delete /TN "Firezip\FirezipUpdateTask" /F`).
  - Nenhum serviço Windows de inicialização em segundo plano é criado.
* `[Implementado]` **Auditoria de Registro:** Gravação apenas de chaves oficiais de shell (`Firezip.Archive`), associações (`OpenWithProgids`), capacidades do Windows 10/11 (`Capabilities`, `RegisteredApplications`) e desinstalação.
* `[Implementado]` **Desinstalação Completa:** Remoção total de todos os arquivos de `{app}` e das chaves de registro.
* `[Implementado]` **Preservação de Dados do Usuário:** A desinstalação não apaga o diretório `%APPDATA%\Firezip`, garantindo que reinstalações e atualizações não causem perda de preferências do usuário.

---

## Milestone 13 — WinGet Preflight

> **Status:** Em Andamento ⏳  
> **Objetivo:** Gate obrigatório com verificação formal de todos os requisitos do WinGet antes da submissão do Pull Request.

### Checklist Obrigatório Pré-Submissão (26 Itens)

```text
[x] 01. Instalador final x64 gerado (FirezipSetup-x64-v1.0.0.exe / v1.0.1.exe)
[x] 02. InstallerType identificado corretamente como 'inno'
[x] 03. Instalação silenciosa testada localmente (/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-)
[x] 04. SilentWithProgress testado localmente (/SILENT /SUPPRESSMSGBOXES /NORESTART /SP-)
[ ] 05. Instalação feita a partir de terminal não elevado (UAC prompt e fallback)
[ ] 06. Instalação em Windows Sandbox limpo testada
[x] 07. Aplicativo inicia após instalação
[x] 08. Executável principal pode ser localizado em C:\Program Files\Firezip\Firezip.UI.exe
[x] 09. Aplicativo executa corretamente e realiza operações de arquivo após instalação
[x] 10. Desinstalação silenciosa testada (unins000.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART)
[x] 11. Desinstalação limpa confirmada (arquivos e atalhos removidos)
[x] 12. Dependências verificadas (zero DLLs faltantes)
[x] 13. Runtime necessário verificado (pacote auto-contido .NET 10 + WinAppSDK)
[x] 14. SHA256 calculado via algoritmo oficial SHA-256
[x] 15. SHA256 confirmado e confrontado com o binário distribuído
[x] 16. URL final usa HTTPS (https://github.com/drodrigues55/firezip/releases/...)
[x] 17. URL aponta diretamente para o instalador (.exe direto)
[x] 18. URL não depende de redirect dinâmico ou página intermediária
[x] 19. URL é estável e imutável
[x] 20. URL é específica para a versão (/download/v1.0.0/ ou /v1.0.1/)
[x] 21. URL pertence a uma fonte controlada pelo publisher (github.com/drodrigues55/firezip)
[x] 22. Página oficial do projeto aponta para o instalador (README.md e GitHub Releases)
[x] 23. Manifesto WinGet multi-file criado no schema 1.9.0
[x] 24. 'winget validate --manifest' passa com êxito (100% de conformidade de schema)
[ ] 25. 'winget install --manifest' testado localmente em máquina de homologação
[ ] 26. Desinstalação após instalação via manifest testada com sucesso
```

### Matriz de Pendências do Preflight

| Item Pendente | Motivo | Impacto | Ação Necessária | Responsável | Condição de Conclusão |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **05. Terminal não elevado** | Teste executado em console de build elevado | Validar elevação UAC adequada | Rodar instalador em PowerShell não admin e verificar elevação UAC | Equipe Release | Instalação solicita UAC e completa com sucesso |
| **06. Windows Sandbox** | Ambiente de desenvolvimento possui ferramentas já instaladas | Garantir ausência de dependências ocultas | Executar script em Windows Sandbox puro (sem .NET pré-instalado) | Equipe QA | App abre e extrai sem erros no Sandbox |
| **25. winget install local** | Requer pacote e manifesto em máquina isolada | Simular execução exata do pipeline do WinGet | Executar `winget install --manifest <caminho>` | Equipe Release | Comando conclui com código 0 e registra app |
| **26. Desinstalação pós-winget** | Validar desinstalação disparada pelo WinGet CLI | Garantir desinstalação limpa via gerenciador | Executar `winget uninstall Firezip.Firezip` | Equipe Release | WinGet remove pacote sem erros |

---

## Milestone 14 — WinGet Manifest

> **Status:** Implementado & Validado Localmente ✅  
> **Objetivo:** Estruturar o conjunto oficial de manifestos no formato multi-file aceito pelo WinGet.

### 1. Estrutura de Diretórios e Arquivos (Schema v1.9.0)
Caminho oficial: `packaging/winget/manifests/f/Firezip/Firezip/<Version>/`

1. **`Firezip.Firezip.yaml` (Version Manifest):**
   - `PackageIdentifier`: `Firezip.Firezip`
   - `PackageVersion`: `<Version>` (ex: `1.0.0` ou `1.0.1`)
   - `DefaultLocale`: `en-US`
   - `ManifestType`: `version`
   - `ManifestVersion`: `1.9.0`
2. **`Firezip.Firezip.installer.yaml` (Installer Manifest):**
   - `InstallerType`: `inno`
   - `Scope`: `machine`
   - `InstallModes`: `[ interactive, silent, silentWithProgress ]`
   - `InstallerSwitches`:
     - `Silent`: `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`
     - `SilentWithProgress`: `/SILENT /SUPPRESSMSGBOXES /NORESTART /SP-`
   - `UpgradeBehavior`: `install`
   - `ProductCode`: `'{8B036367-AE8C-4D88-B1F3-E18F2E23A534}_is1'`
   - `FileExtensions`: `[ zip, 7z, rar, tar, gz, bz2 ]`
   - `Architecture`: `x64`
   - `InstallerUrl`: URL HTTPS direta e versionada no GitHub Releases
   - `InstallerSha256`: Hash SHA-256 maiúsculo de 64 caracteres gerado do arquivo final
3. **`Firezip.Firezip.locale.en-US.yaml` (Default Locale Manifest):**
   - Metadados completos em inglês: `Publisher`, `PackageName`, `License` (`MIT`), `LicenseUrl` (apontando para URL pública com retorno 200 OK), `ShortDescription`, `Description`, `Moniker` (`firezip`), `Tags` e `ReleaseNotesUrl`.
4. **`Firezip.Firezip.locale.pt-BR.yaml` (Locale Manifest pt-BR):**
   - Metadados completos localizados em Português do Brasil.

### 2. Validação Local de Schema
* Executado via comando oficial:
  ```powershell
  winget validate --manifest packaging/winget/manifests/f/Firezip/Firezip/<Version>/
  ```
* *Resultado:* **Êxito na validação do manifesto (100% de conformidade com os schemas do WinGet).**

---

## Milestone 15 — WinGet Submission

> **Status:** Em Andamento (PR submetido, aguardando validação do pipeline) ⏳  
> **Objetivo:** Submeter e manter o Pull Request oficial no repositório `microsoft/winget-pkgs`.

### 1. Regras de Estrutura do Pull Request
* **Isolamento Estrito:** O PR contém **exclusivamente** os 4 arquivos YAML do manifesto no caminho:
  `manifests/f/Firezip/Firezip/<Version>/`
* **Zero Arquivos Espúrios:** Nenhuma alteração em código C#, scripts, README ou outros diretórios é incluída no mesmo PR.
* **Casing e Nomenclatura:**
  - Diretórios: `manifests/f/Firezip/Firezip/<Version>/`
  - Arquivos: `Firezip.Firezip.*.yaml` (idêntico ao `PackageIdentifier`).
* **Uma Única Versão:** Um único pacote e uma única versão por PR de submissão.

### 2. Comandos de Homologação Pré-PR
Executar obrigatoriamente antes do push do PR:
```powershell
# 1. Validação estática de schema
winget validate --manifest packaging/winget/manifests/f/Firezip/Firezip/<Version>/

# 2. Teste de instalação local direta via manifesto
winget install --manifest packaging/winget/manifests/f/Firezip/Firezip/<Version>/
```

---

## Milestone 16 — Validation / Fixes (Tratamento de Falhas do Pipeline)

> **Status:** Em Execução Ativa 🔄  
> **Objetivo:** Monitorar o pipeline de CI do Azure Pipelines no `microsoft/winget-pkgs`, tratar erros e manter conformidade estrita.

### Matriz de Resposta a Falhas de Validação do WinGet

| Categoria de Falha | Código de Erro / Verificação | Causa Raiz Possível | O que corrigir no projeto / código | O que corrigir no manifesto | Exige novo instalador? | Depende de infra externa? |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Manifesto** | `Manifest-Validation-Error` / `Manifest-Installer-Validation-Error` | Campo obrigatório ausente, tipo incorreto ou erro de indentação YAML | Nenhuma alteração de código necessária | Corrigir sintaxe YAML ou valores dos campos | Não | Não |
| **Manifesto** | `Manifest-Version-Deprecated` | Schema version desatualizada (ex: < 1.6.0) | Nenhuma alteração | Atualizar `$schema` e `ManifestVersion` para versão aceita (ex: `1.9.0`) | Não | Não |
| **Manifesto** | `Manifest-Path-Error` | Arquivos fora do caminho `manifests/f/Firezip/Firezip/<version>/` | Nenhuma alteração | Mover arquivos para a árvore correta | Não | Não |
| **Instalador** | `Error-Hash-Mismatch` | Checksum SHA-256 no YAML difere do binário baixado pela URL | Nenhuma alteração no código | Recalcular `Get-FileHash` e atualizar `InstallerSha256` | Não | Sim (se o arquivo na release mudou) |
| **Instalador** | `Error-Installer-Availability` | Instalador inacessível no momento do teste do bot | Nenhuma alteração | Verificar URL em `InstallerUrl` | Não | Sim (estabilidade do GitHub Releases) |
| **Instalador** | `Binary-Validation-Error` | Executável corrompido ou arquitetura incompatível | Verificar arquitetura x64 no build do Inno Setup | Confirmar `Architecture: x64` | Sim | Não |
| **URL** | `URL-Validation-Error` / `Validation-HTTP-Error` | URL retorna 404, 403, 500 ou quebra de link (ex: `LICENSE` inexistente) | Criar o arquivo no repositório (ex: `LICENSE` na branch `main`) | Atualizar URL se o link estiver apontando para caminho errado | Não | Sim (commit e push para o repositório público) |
| **URL** | `Validation-Domain` / `Validation-Unapproved-URL` | URL de download fora do domínio do publisher | Hospedar instalador apenas no repositório oficial (`github.com/drodrigues55/firezip`) | Atualizar `InstallerUrl` | Não | Sim |
| **URL** | `Validation-Indirect-URL` | URL passa por encurtador ou landing page intermediária | Nenhuma alteração | Garantir link direto `.exe` para o asset da release | Não | Sim |
| **Instalação** | `Validation-Unattended-Failed` | Instalador abre janela, trava ou exige clique durante `/VERYSILENT` | Inserir `skipifsilent` nas ações pós-instalação do Inno Setup | Confirmar `InstallerSwitches.Silent` | Sim | Não |
| **Instalação** | `Validation-Executable-Error` | Executável não inicia após a instalação no sandbox | Verificar dependências nativas e runtime empacotado | Confirmar `ProductCode` e caminhos de atalhos | Sim | Não |
| **Instalação** | `Validation-Uninstall-Error` | Desinstalador falha ou deixa arquivos bloqueados | Ajustar seção `[UninstallRun]` e flags do Inno Setup | Verificar `AppsAndFeaturesEntries` | Sim | Não |
| **Instalação** | `Validation-Defender-Error` | Falso positivo no Windows Defender / SmartScreen | Submeter binário para análise de falso positivo no portal da Microsoft | Nenhuma alteração no manifesto | Não | Sim (Microsoft Defender Portal) |
| **Dependências**| `Validation-MSIX-Dependency` / `Validation-VCRuntime-Dependency` | Falta runtime VC++ ou pacote MSIX | Empacotar dependências estaticamente ou via single-file | Adicionar dependência se estritamente necessária | Sim | Não |
| **Políticas** | `Policy-Test-*` / Assinatura CLA | Contribuidor não assinou o Microsoft CLA | Nenhuma alteração de código | Nenhuma alteração no manifesto | Não | Sim (Assinar CLA no portal Microsoft Open Source) |

*Regra de Ouro:* **Nunca mascarar ou forçar metadados falsos** apenas para contornar uma regra de validação. Se um teste falhar, investigar a causa real (código, instalador, infraestrutura ou manifesto) e aplicar a correção definitiva.

---

## Milestone 17 — GA Release Audit

> **Status:** Pendente de Validação Microsoft & Publicação ⏸️  
> **Objetivo:** Auditoria final completa e fechamento de ciclo após aprovação do pacote no catálogo oficial do WinGet.

### Checklist de Homologação Pós-Publicação
* `[Pendente de validação Microsoft]` **Disponibilidade no Feed Oficial:** Verificar indexação do pacote no repositório central (`winget search Firezip`).
* `[Pendente]` **Instalação Limpa via WinGet:**
  ```powershell
  winget install Firezip.Firezip
  ```
  Verificar que o download, verificação de hash SHA-256 e instalação silenciosa ocorrem sem intervenção e retornam código 0.
* `[Pendente]` **Inicialização e Funcionamento:** Executar o aplicativo após instalação via WinGet e testar:
  - Criação e extração de arquivos ZIP e 7Z.
  - Ação de duplo clique configurada (Bandizip-style).
  - Preservação da janela de progresso com estatísticas de compressão.
* `[Pendente]` **Auditoria de Isolamento de Rede Pós-Instalação:** Confirmar via monitoramento de conexões que `Firezip.UI.exe` não realiza chamadas de rede.
* `[Pendente]` **Auditoria de Desinstalação via WinGet:**
  ```powershell
  winget uninstall Firezip.Firezip
  ```
  Verificar que o pacote é removido de forma silenciosa, atalhos são excluídos e configurações em `%APPDATA%\Firezip` permanecem seguras.
* `[Pendente]` **Consistência Ponta a Ponta:** Garantir correspondência 100% idêntica entre:
  - Versão do assembly compilado (`Firezip.UI.exe`).
  - Versão exibida no diálogo nativo *Sobre* e na UI.
  - Versão da GitHub Release e arquivos anexados.
  - Versão publicada no catálogo oficial do WinGet.

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