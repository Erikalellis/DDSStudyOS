#define MyAppName "DDS StudyOS"
#define MyAppPublisher "Deep Darkness Studios"
#define MyAppId "{{A5F4F364-3F77-470A-BD5C-641AA103D8AA}"
#define MyUninstallRegKey "Software\Microsoft\Windows\CurrentVersion\Uninstall\{A5F4F364-3F77-470A-BD5C-641AA103D8AA}_is1"

#ifndef MyAppURL
  #define MyAppURL "https://github.com/<OWNER>/<REPO>"
#endif
#ifndef MyAppVersion
  #define MyAppVersion "3.1.1"
#endif
#ifndef MySourceDir
  #define MySourceDir "..\..\artifacts\installer-input\app"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "..\..\artifacts\installer-output"
#endif
#ifndef MySetupBaseName
  #define MySetupBaseName "DDSStudyOS-Setup"
#endif
#ifndef MyAppExeName
  #define MyAppExeName "DDSStudyOS.App.exe"
#endif
#ifndef MyAppIcon
  #define MyAppIcon "..\..\src\DDSStudyOS.App\Assets\DDSStudyOS.ico"
#endif
#ifndef MyWizardImageFile
  #define MyWizardImageFile "branding\wizard-side.bmp"
#endif
#ifndef MyWizardSmallImageFile
  #define MyWizardSmallImageFile "branding\wizard-small.bmp"
#endif
#ifndef MyLicenseFile
  #define MyLicenseFile "..\legal\EULA.pt-BR.rtf"
#endif
#ifndef MyInstallWebView2
  #define MyInstallWebView2 "1"
#endif
#ifndef MyInstallDotNetDesktopRuntime
  #define MyInstallDotNetDesktopRuntime "0"
#endif
#ifndef MyDotNetDesktopRuntimeMajor
  #define MyDotNetDesktopRuntimeMajor "8"
#endif
#ifndef MyPrereqLogName
  #define MyPrereqLogName "DDSStudyOS-Prereqs.log"
#endif
#ifndef MyUninstallFeedbackURL
  #define MyUninstallFeedbackURL "https://docs.google.com/forms/d/e/1FAIpQLScN1a0_ISFNIbfOx3XMY6L8Na5Utf9lZCoO3S8efGn4934GCQ/viewform"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#MyOutputDir}
OutputBaseFilename={#MySetupBaseName}
SetupIconFile={#MyAppIcon}
WizardImageFile={#MyWizardImageFile}
WizardSmallImageFile={#MyWizardSmallImageFile}
LicenseFile={#MyLicenseFile}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
Uninstallable=yes
CreateUninstallRegKey=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\app\{#MyAppExeName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "prereqs\install-prereqs.ps1"; DestDir: "{tmp}"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\app\{#MyAppExeName}"; WorkingDir: "{app}\app"; IconFilename: "{#MyAppIcon}"
Name: "{autoprograms}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\app\{#MyAppExeName}"; WorkingDir: "{app}\app"; IconFilename: "{#MyAppIcon}"; Tasks: desktopicon

[Run]
Filename: "{app}\app\{#MyAppExeName}"; Description: "Abrir {#MyAppName}"; Flags: nowait postinstall skipifsilent skipifdoesntexist

[UninstallDelete]
Type: filesandordirs; Name: "{app}\app"
Type: filesandordirs; Name: "{localappdata}\DDSStudyOS"

[Code]
function GetPowerShellExePath(): string;
begin
  if IsWin64 then
    Result := ExpandConstant('{sysnative}\WindowsPowerShell\v1.0\powershell.exe')
  else
    Result := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
end;

procedure EnsureVisibleUninstallEntry();
var
  KeyPath: string;
begin
  KeyPath := '{#MyUninstallRegKey}';

  RegDeleteValue(HKLM, KeyPath, 'SystemComponent');
  RegDeleteValue(HKLM, KeyPath, 'NoRemove');
  RegDeleteValue(HKLM, KeyPath, 'NoModify');
  RegDeleteValue(HKLM, KeyPath, 'NoRepair');

  RegWriteStringValue(HKLM, KeyPath, 'DisplayName', '{#MyAppName}');
  RegWriteStringValue(HKLM, KeyPath, 'Publisher', '{#MyAppPublisher}');
  RegWriteStringValue(HKLM, KeyPath, 'InstallLocation', ExpandConstant('{app}'));
  RegWriteStringValue(HKLM, KeyPath, 'DisplayIcon', ExpandConstant('{app}\app\{#MyAppExeName}'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): string;
var
  PowerShellExe: string;
  ScriptPath: string;
  LogPath: string;
  Params: string;
  ResultCode: Integer;
begin
  Result := '';

  WizardForm.StatusLabel.Caption := 'Verificando pre-requisitos do sistema...';
  WizardForm.Update();

  ExtractTemporaryFile('install-prereqs.ps1');
  ScriptPath := ExpandConstant('{tmp}\install-prereqs.ps1');
  LogPath := ExpandConstant('{localappdata}\Temp\{#MyPrereqLogName}');
  PowerShellExe := GetPowerShellExePath();

  Params :=
    '-NoProfile -ExecutionPolicy Bypass ' +
    '-File "' + ScriptPath + '" ' +
    '-LogPath "' + LogPath + '" ' +
    '-DotNetDesktopMajor {#MyDotNetDesktopRuntimeMajor} ' +
    '-Quiet ';

  if '{#MyInstallWebView2}' = '1' then
    Params := Params + '-InstallWebView2 ';

  if '{#MyInstallDotNetDesktopRuntime}' = '1' then
    Params := Params + '-InstallDotNetDesktopRuntime ';

  if not Exec(PowerShellExe, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Nao foi possivel iniciar a verificacao de pre-requisitos.';
    Exit;
  end;

  if ResultCode <> 0 then
  begin
    Result :=
      'Falha ao validar/instalar pre-requisitos (codigo ' + IntToStr(ResultCode) + ').' + #13#10 +
      'Log: ' + LogPath;
    Exit;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    EnsureVisibleUninstallEntry();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ErrorCode: Integer;
  OpenFeedbackAnswer: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if UninstallSilent then
      Exit;

    OpenFeedbackAnswer := MsgBox(
      'Desinstalacao concluida.' + #13#10 + #13#10 +
      'Quer nos contar o motivo da desinstalacao?' + #13#10 +
      'Seu feedback ajuda a melhorar o DDS StudyOS.',
      mbInformation, MB_YESNO);

    if OpenFeedbackAnswer = IDYES then
      ShellExec('open', '{#MyUninstallFeedbackURL}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;
