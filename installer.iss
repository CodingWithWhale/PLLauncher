; Inno Setup script for PLLauncher
; Requires Inno Setup 6+ (https://jrsoftware.org/isdl.php)

#define MyAppName "PLLauncher"
#define MyAppVersion "2.7.1"
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
CloseApplications=force
DisableProgramGroupPage=yes
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "starticon"; Description: "Pin to &Start Menu"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
Source: "terms.txt"; Flags: dontcopy
Source: "dist\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
; Force-delete the app directory and any runtime-created files
Type: filesandordirs; Name: "{app}"
; Force-delete the data directory as a fallback if [Code] step fails
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: starticon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\PLLauncher"; ValueType: string; ValueName: "DataDir"; ValueData: "{code:GetDataDir}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall

[Code]
var
  DataDirPage: TInputDirWizardPage;
  SavedDataDir: String;
  TermsPage: TWizardPage;
  TermsMemo: TNewMemo;
  AcceptCheck: TNewCheckBox;
  TermsButton: TNewButton;

function GetDataDir(Param: string): string;
begin
  Result := DataDirPage.Values[0];
end;

procedure TermsButtonClick(Sender: TObject);
var
  TermsPath: String;
  ResultCode: Integer;
begin
  ExtractTemporaryFile('terms.txt');
  TermsPath := ExpandConstant('{tmp}\terms.txt');
  ShellExec('open', TermsPath, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;

procedure InitializeWizard;
begin
  // Terms & Conditions page (shown right after Welcome)
  TermsPage := CreateCustomPage(wpWelcome, 'Terms & Conditions',
    'Please read the Terms & Conditions before installing PLLauncher.');

  TermsMemo := TNewMemo.Create(TermsPage);
  TermsMemo.Parent := TermsPage.Surface;
  TermsMemo.Left := ScaleX(8);
  TermsMemo.Top := ScaleY(8);
  TermsMemo.Width := TermsPage.SurfaceWidth - ScaleX(16);
  TermsMemo.Height := TermsPage.SurfaceHeight - ScaleY(60);
  TermsMemo.ReadOnly := True;
  TermsMemo.ScrollBars := ssVertical;
  TermsMemo.TabOrder := 0;
  TermsMemo.Anchors := [akLeft, akTop, akRight, akBottom];
  ExtractTemporaryFile('terms.txt');
  TermsMemo.Lines.LoadFromFile(ExpandConstant('{tmp}\terms.txt'));

  AcceptCheck := TNewCheckBox.Create(TermsPage);
  AcceptCheck.Parent := TermsPage.Surface;
  AcceptCheck.Caption := 'I accept the Terms & Conditions';
  AcceptCheck.Checked := False;
  AcceptCheck.Left := TermsMemo.Left;
  AcceptCheck.Top := TermsMemo.Top + TermsMemo.Height + ScaleY(10);
  AcceptCheck.Width := ScaleX(260);
  AcceptCheck.Height := ScaleY(17);
  AcceptCheck.TabOrder := 1;
  AcceptCheck.Anchors := [akLeft, akBottom];

  TermsButton := TNewButton.Create(TermsPage);
  TermsButton.Parent := TermsPage.Surface;
  TermsButton.Caption := 'See Terms & Conditions';
  TermsButton.Left := TermsPage.SurfaceWidth - ScaleX(150);
  TermsButton.Top := AcceptCheck.Top - ScaleY(3);
  TermsButton.Width := ScaleX(142);
  TermsButton.Height := ScaleY(23);
  TermsButton.TabOrder := 2;
  TermsButton.Anchors := [akRight, akBottom];
  TermsButton.OnClick := @TermsButtonClick;

  // Data Location page
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

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = TermsPage.ID then
  begin
    if WizardSilent then
    begin
      // Silent install (auto-update): accept terms automatically so the
      // update is not blocked by a hidden checkbox that can't be ticked.
      AcceptCheck.Checked := True;
    end
    else if not AcceptCheck.Checked then
    begin
      MsgBox('Please read and accept the Terms & Conditions to continue.', mbInformation, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataDir: String;
  ResultCode: Integer;
begin
  // Save data dir BEFORE registry is deleted by uninsdeletekey
  if CurUninstallStep = usUninstall then
  begin
    if RegQueryStringValue(HKCU, 'Software\PLLauncher', 'DataDir', AppDataDir) then
      SavedDataDir := AppDataDir;
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    AppDataDir := SavedDataDir;
    if (AppDataDir = '') or not DirExists(AppDataDir) then
      AppDataDir := ExpandConstant('{localappdata}\PLLauncher');
    if DirExists(AppDataDir) then
    begin
      if not DelTree(AppDataDir, True, True, True) then
      begin
        // DelTree failed (files in use) — schedule deletion on next reboot
        Exec('cmd.exe', '/c rmdir /s /q "' + AppDataDir + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
        if DirExists(AppDataDir) then
          Exec('cmd.exe', '/c ping -n 5 127.0.0.1 >nul & rmdir /s /q "' + AppDataDir + '"', '', SW_HIDE, ewNoWait, ResultCode);
      end;
    end;
  end;
end;
