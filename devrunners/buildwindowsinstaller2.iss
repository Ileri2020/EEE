#ifndef PublishDir
  #error PublishDir must point to the self-contained Windows publish directory.
#endif

#define AppName "EE Engine"
#define AppVersion "1.0"
#define AppExeName "EEE.exe"

[Setup]
AppId={{0C4671A5-06E8-456B-947C-159FA377135E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=EEE
DefaultDirName={localappdata}\Programs\EE Engine
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=.
OutputBaseFilename=EEE-Setup
SetupIconFile={#PublishDir}\appicon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: postinstall nowait skipifsilent
