; Firezip Inno Setup Script
; Generates silent-install capable, Winget-ready native Windows installer

#define MyAppName "Firezip"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Firezip Contributors"
#define MyAppURL "https://github.com/drodrigues55/firezip"
#define MyAppExeName "Firezip.UI.exe"

[Setup]
AppId={{8B036367-AE8C-4D88-B1F3-E18F2E23A534}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\..\dist
OutputBaseFilename=FirezipSetup-x64-v{#MyAppVersion}
SetupIconFile=..\..\src\Firezip.UI\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
ChangesAssociations=yes
PrivilegesRequiredOverridesAllowed=commandline dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autoupdate"; Description: "Verificar atualizações automaticamente"; GroupDescription: "Atualizações / Updates:"; Flags: checkedonce
Name: "assoc_zip"; Description: "Associate with .zip files"; GroupDescription: "File Associations:"
Name: "assoc_7z"; Description: "Associate with .7z files"; GroupDescription: "File Associations:"
Name: "assoc_rar"; Description: "Associate with .rar files"; GroupDescription: "File Associations:"
Name: "assoc_tar"; Description: "Associate with .tar, .gz, .bz2 files"; GroupDescription: "File Associations:"

[Files]
Source: "..\..\Firezip.UI.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\FirezipUpdater.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\MyUnzip.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\MyUnzipUpdater.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; File type registration
Root: HKA; Subkey: "Software\Classes\Firezip.Archive"; ValueType: string; ValueData: "Firezip Archive"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\open"; ValueType: string; ValueData: "Open with Firezip"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\open"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

; Dedicated task verbs for archive files
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\ExtractHere"; ValueType: string; ValueData: "Extract Here"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\ExtractHere"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\ExtractHere\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""; Flags: uninsdeletekey

Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\ExtractToFolder"; ValueType: string; ValueData: "Extract to Folder"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\ExtractToFolder"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\ExtractToFolder\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-to-folder ""%1"""; Flags: uninsdeletekey

Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\ExtractTo"; ValueType: string; ValueData: "Extract to..."; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\ExtractTo"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Firezip.Archive\shell\ExtractTo\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-to ""%1"""; Flags: uninsdeletekey

; File associations
Root: HKA; Subkey: "Software\Classes\.zip"; ValueType: string; ValueData: "Firezip.Archive"; Tasks: assoc_zip; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.7z"; ValueType: string; ValueData: "Firezip.Archive"; Tasks: assoc_7z; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.rar"; ValueType: string; ValueData: "Firezip.Archive"; Tasks: assoc_rar; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.tar"; ValueType: string; ValueData: "Firezip.Archive"; Tasks: assoc_tar; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.gz"; ValueType: string; ValueData: "Firezip.Archive"; Tasks: assoc_tar; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.bz2"; ValueType: string; ValueData: "Firezip.Archive"; Tasks: assoc_tar; Flags: uninsdeletevalue

[Run]
Filename: "{sys}\schtasks.exe"; Parameters: "/Create /TN ""Firezip\FirezipUpdateTask"" /TR """"{app}\FirezipUpdater.exe"""" --auto --silent"" /SC DAILY /F"; Flags: runhidden; Tasks: autoupdate
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""Firezip\FirezipUpdateTask"" /F"; Flags: runhidden
