; Inno Setup script for PLLauncher
; Requires Inno Setup 6+ (https://jrsoftware.org/isdl.php)

#define MyAppName "PLLauncher"
#define MyAppVersion "2.2.2"
#define MyAppPublisher "PLLauncher"
#define MyAppURL "https://github.com/CodingWithWhale/PLLauncher"
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
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "starticon"; Description: "Pin to &Start Menu"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
Source: "dist\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: starticon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\PLLauncher"; ValueType: string; ValueName: "DataDir"; ValueData: "{code:GetDataDir}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DataDirPage: TInputDirWizardPage;

function GetDataDir(Param: string): string;
begin
  Result := DataDirPage.Values[0];
end;

procedure InitializeWizard;
begin
  DataDirPage := CreateInputDirPage(
    wpSelectDir,
    'Data Location',
    'Where should PLLauncher store its data?',
    'PLLauncher saves settings, tasks, schedules, and usage history to this folder.'#13#10#13#10 +
    'This folder will be deleted when you uninstall PLLauncher.',
    False,
    'PLLauncherData');
  DataDirPage.Add('');
  DataDirPage.Values[0] := ExpandConstant('{localappdata}\PLLauncher');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    try
      RegQueryStringValue(HKCU, 'Software\PLLauncher', 'DataDir', AppDataDir);
    except
      AppDataDir := '';
    end;
    if (AppDataDir = '') or not DirExists(AppDataDir) then
      AppDataDir := ExpandConstant('{localappdata}\PLLauncher');
    if DirExists(AppDataDir) then
      DelTree(AppDataDir, True, True, True);
  end;
end;
