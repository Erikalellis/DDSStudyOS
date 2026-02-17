#define MyAppName "DDS StudyOS"
#define MyAppPublisher "Deep Darkness Studios"
#define MyAppURL "https://github.com/Erikalellis/DDSStudyOS"

#ifndef MyAppVersion
  #define MyAppVersion "2.1.0"
#endif
#ifndef MySourceDir
  #define MySourceDir "..\..\artifacts\installer-input\app"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "..\..\artifacts\installer-output"
#endif
#ifndef MySetupBaseName
  #define MySetupBaseName "DDSStudyOS-Setup-Inno"
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

[Setup]
AppId={{A5F4F364-3F77-470A-BD5C-641AA103D8AA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
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
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\app\{#MyAppExeName}"; WorkingDir: "{app}\app"; IconFilename: "{#MyAppIcon}"; Tasks: desktopicon

[Run]
Filename: "{app}\app\{#MyAppExeName}"; Description: "Abrir {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\app"

[Code]
function GetPowerShellExePath(): string;
begin
  if IsWin64 then
    Result := ExpandConstant('{sysnative}\WindowsPowerShell\v1.0\powershell.exe')
  else
    Result := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
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
  LogPath := ExpandConstant('{tmp}\{#MyPrereqLogName}');
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
