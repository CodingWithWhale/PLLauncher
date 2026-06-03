; Inno Setup script for PLLauncher
; Requires Inno Setup 6+ (https://jrsoftware.org/isdl.php)

#define MyAppName "PLLauncher"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "PLLauncher"
#define MyAppURL "https://github.com/YOUR_USERNAME/PLLauncher"
#define MyAppExeName "PLLauncher.exe"
#define MyIcon "PLLauncher\icon.ico"

[Setup]
AppId={{B8A3C8E0-4C1A-4F3A-9E2D-7A1B2C3D4E5F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=dist
OutputBaseFilename=PLLauncher_Setup_{#MyAppVersion}
SetupIconFile={#MyIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
AlwaysShowDirOnReadyPage=yes
DisableProgramGroupPage=yes
CreateUninstallRegistryKey=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: checkedonce

[Files]
Source: "dist\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--uninstall"; Flags: runhidden

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    AppDataDir := ExpandConstant('{localappdata}\PLLauncher');
    if DirExists(AppDataDir) then
      if MsgBox('Do you want to remove all PLLauncher data (settings, logs, usage history)?', mbConfirmation, MB_YESNO) = IDYES then
        DelTree(AppDataDir, True, True, True);
  end;
end;
