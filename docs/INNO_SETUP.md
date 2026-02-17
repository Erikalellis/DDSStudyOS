# Inno Setup - Fluxo Oficial (DDS StudyOS)

Este e o fluxo oficial para gerar e distribuir o instalador do DDS StudyOS.

## 1) Build oficial do instalador
No raiz do repositorio:

```powershell
.\scripts\build-inno-installer.ps1 -InstallWebView2 1 -InstallDotNetDesktopRuntime 0
```

Saida esperada:
- `artifacts/installer-output/DDSStudyOS-Setup-Inno.exe`

Padrao oficial:
- `InstallWebView2=1`
- `InstallDotNetDesktopRuntime=0`
- publish com `SelfContained=true` e `WindowsAppSDKSelfContained=true`
- branding visual do wizard gerado automaticamente (`installer/inno/branding`)

## 2) Executar setup com log
Opcao recomendada:

```powershell
.\scripts\run-setup-with-log.ps1
```

Opcao manual:

```powershell
.\artifacts\installer-output\DDSStudyOS-Setup-Inno.exe /LOG="C:\Temp\DDSStudyOS-Inno.log"
```

Logs gerados:
- Log do setup Inno: caminho informado no `/LOG`
- Log de pre-requisitos: `%TEMP%\DDSStudyOS-Prereqs.log`

## 3) Parametros uteis
Informar caminho manual do compilador Inno:

```powershell
.\scripts\build-inno-installer.ps1 -InnoCompilerPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

Build sem refazer a pasta de entrada:

```powershell
.\scripts\build-inno-installer.ps1 -PrepareInput 0
```

Gerar setup sem recriar branding (quando quiser manter imagens atuais):

```powershell
.\scripts\build-inno-installer.ps1 -GenerateBranding 0
```

Habilitar tambem .NET Desktop Runtime (somente se necessario):

```powershell
.\scripts\build-inno-installer.ps1 -InstallWebView2 1 -InstallDotNetDesktopRuntime 1 -DotNetDesktopRuntimeMajor 8
```

## 4) Validacao minima antes de release
1. Instalar em maquina de teste.
2. Confirmar tela de licenca em pt-BR.
3. Confirmar abertura do app ao fim do setup.
4. Confirmar atalho no Menu Iniciar e atalho Desktop conforme task.
5. Confirmar desinstalacao removendo somente arquivos do app.

## 5) Arquivos do fluxo Inno
- `installer/inno/DDSStudyOS.iss`
- `installer/inno/branding/wizard-side.bmp`
- `installer/inno/branding/wizard-small.bmp`
- `installer/inno/prereqs/install-prereqs.ps1`
- `scripts/build-inno-installer.ps1`
- `scripts/generate-inno-branding.ps1`
- `scripts/run-setup-with-log.ps1`

## 6) Legado
Advanced Installer permanece apenas como backup:
- `docs/ADVANCED_INSTALLER_SETUP.md`
