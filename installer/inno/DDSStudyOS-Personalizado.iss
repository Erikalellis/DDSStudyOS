; ============================================================
; DDS StudyOS - Instalador Personalizado (Inno Setup)
; Abra este arquivo no Inno Setup e ajuste os defines abaixo.
; ============================================================

#define MyAppName "DDS StudyOS"
#define MyAppVersion "2.1.0"
#define MyAppPublisher "Deep Darkness Studios"
#define MyAppURL "https://github.com/<OWNER>/<REPO>"
#define MyAppExeName "DDSStudyOS.App.exe"
#define MyAppId "{{A5F4F364-3F77-470A-BD5C-641AA103D8AA}"
#define MyUninstallFeedbackURL "https://docs.google.com/forms/d/e/1FAIpQLScN1a0_ISFNIbfOx3XMY6L8Na5Utf9lZCoO3S8efGn4934GCQ/viewform"

; Pastas de entrada/saida (relativas a este .iss)
#define MySourceDir "..\..\artifacts\installer-input\app"
#define MyOutputDir "..\..\artifacts\installer-output"
#define MySetupBaseName "DDSStudyOS-Setup-Personalizado"

; Arquivos visuais/legais
#define MySetupIcon "..\..\src\DDSStudyOS.App\Assets\DDSStudyOS.ico"
#define MyLicenseFile "..\legal\EULA.pt-BR.rtf"

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
SetupIconFile={#MySetupIcon}
LicenseFile={#MyLicenseFile}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\app\{#MyAppExeName}

; Se quiser imagem no assistente, descomente e ajuste os caminhos:
;WizardImageFile=branding\wizard.bmp
;WizardSmallImageFile=branding\wizard-small.bmp

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Area de Trabalho"; GroupDescription: "Atalhos:"; Flags: checkedonce
Name: "startmenuicon"; Description: "Criar atalho no Menu Iniciar"; GroupDescription: "Atalhos:"; Flags: checkedonce

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\app\{#MyAppExeName}"; WorkingDir: "{app}\app"; IconFilename: "{#MySetupIcon}"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\app\{#MyAppExeName}"; WorkingDir: "{app}\app"; IconFilename: "{#MySetupIcon}"; Tasks: desktopicon

[Run]
Filename: "{app}\app\{#MyAppExeName}"; Description: "Abrir {#MyAppName} agora"; Flags: nowait postinstall skipifsilent skipifdoesntexist

[UninstallDelete]
Type: filesandordirs; Name: "{app}\app"
Type: filesandordirs; Name: "{localappdata}\DDSStudyOS"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ErrorCode: Integer;
  OpenFeedbackAnswer: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    OpenFeedbackAnswer := MsgBox(
      'Desinstalacao concluida.' + #13#10 + #13#10 +
      'Quer nos contar o motivo da desinstalacao?' + #13#10 +
      'Seu feedback ajuda a melhorar o DDS StudyOS.',
      mbInformation, MB_YESNO);

    if OpenFeedbackAnswer = IDYES then
      ShellExec('open', '{#MyUninstallFeedbackURL}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;
