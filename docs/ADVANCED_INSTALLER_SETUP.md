# Advanced Installer Setup - DDS StudyOS

Este guia prepara o projeto para gerar o instalador `.exe` com os links oficiais.

## 1) Preparar arquivos de entrada do setup
Execute:

```powershell
.\scripts\prepare-installer-input.ps1 -SignExecutable
```

Observacao: por padrao o publish agora roda em `SelfContained=true` e `WindowsAppSDKSelfContained=true` para evitar erro de app nao abrir em maquina sem runtime.
Observacao: por padrao a plataforma usada no publish e `x64`.

Saida esperada:
- `artifacts/installer-input/app` (arquivos da aplicacao)
- `artifacts/installer-input/scripts` (certificado e scripts)
- `artifacts/installer-input/docs` (support, changelog, update info)
- `artifacts/installer-input/legal` (EULA e readme do instalador)
- `artifacts/installer-input/installer-manifest.json`

## 2) Criar projeto `.aip` automaticamente
Se o Advanced Installer ja estiver instalado:

```powershell
.\scripts\create-advanced-installer-project.ps1 -PrepareInput -SignExecutable -Force
```

Se o Advanced Installer estiver fora dos caminhos padrao, use:

```powershell
.\scripts\create-advanced-installer-project.ps1 -PrepareInput -SignExecutable -Force -AdvancedInstallerPath "F:\Program Files (x86)\Caphyon\Advanced Installer 23.4\bin\x86\AdvancedInstaller.com"
```

Saida esperada:
- `installer/advanced-installer/DDSStudyOS.aip`
- `artifacts/installer-output/` (pasta de destino do setup)

Observacao: o script cria projeto do tipo `enterprise` com idioma principal `pt_BR`.
Observacao: o script tambem aplica automaticamente o EULA PT-BR (`/SetEula`) no dialogo de licenca.
Observacao: o script cria automaticamente atalhos no `Menu Iniciar` e na `Area de Trabalho`.
Observacao: o script aplica automaticamente o icone de `src/DDSStudyOS.App/Assets/DDSStudyOS.ico` no produto e no `Setup.exe`.
Observacao: o script adiciona automaticamente o pre-requisito `.NET Desktop Runtime 8 (x64)` para baixar/instalar quando estiver faltando.
Observacao: para desabilitar esse comportamento em um caso especifico, use `-EnableDotNetDesktopPrerequisite 0`.

## 3) Abrir projeto no Advanced Installer e Build
1. Abrir `installer/advanced-installer/DDSStudyOS.aip`
2. Conferir Product Details, Files and Folders e Digital Signature
3. Clicar em `Build`

## 4) Dados do produto (preenchidos pelo script)
- Product Name: `DDS StudyOS`
- Company Name: `Deep Darkness Studios`
- Version: lida de `src/DDSStudyOS.App/DDSStudyOS.App.csproj`
- Publisher: `Deep Darkness Studios`
- Idioma principal do instalador: `Português (Brasil)` (`ProductLanguage=1046`, `pt_BR`)

## 5) Arquivos da aplicacao
- Fonte importada automaticamente: `artifacts/installer-input/app`
- Executavel principal: `DDSStudyOS.App.exe`

## 6) Links oficiais (Installer UI / Product Details)
- Homepage: `https://177.71.165.60/`
- Support: `https://github.com/Erikalellis/DDSStudyOS/blob/main/SUPPORT.md`
- Release Notes: `https://github.com/Erikalellis/DDSStudyOS/blob/main/CHANGELOG.md`
- Update Info: `https://github.com/Erikalellis/DDSStudyOS/blob/main/docs/UPDATE_INFO.md`

## 7) Arquivos legais no instalador
- License Agreement (EULA PT-BR): `installer/legal/EULA.pt-BR.rtf`
- License Agreement (EULA ES): `installer/legal/EULA.es.rtf`
- Readme da instalacao: `installer/legal/README_INSTALLER.pt-BR.rtf`

Se estiver usando o pacote pronto (`artifacts/installer-input`), use:
- `artifacts/installer-input/legal/EULA.pt-BR.rtf`
- `artifacts/installer-input/legal/EULA.es.rtf`
- `artifacts/installer-input/legal/README_INSTALLER.pt-BR.rtf`

## 8) Certificado interno (opcional, homologacao)
Se desejar instalar certificado no fim do setup:
- Adicionar:
  - `artifacts/installer-input/scripts/install-internal-cert.ps1`
  - `artifacts/installer-input/scripts/DDS_Studios_Final.cer`
- Custom Action (PowerShell):
  - `-NoProfile -ExecutionPolicy Bypass -File "install-internal-cert.ps1" -CerPath "DDS_Studios_Final.cer" -ExpectedThumbprint "6780CE530A33615B591727F5334B3DD075B76422" -StoreScope LocalMachine -InstallTrustedPublisher $true -InstallRoot $true`

## 9) Assinatura do instalador
- Assinar `DDSStudyOS.App.exe` antes de empacotar.
- Assinar tambem o `Setup.exe` final no Advanced Installer.

## 10) Teste minimo antes de publicar
1. Instalar em maquina limpa.
2. Abrir app e validar navegacao.
3. Testar backup export/import.
4. Testar lembrete e notificacao.
5. Validar links de suporte e changelog.

## 11) Personalizar icone (PNG -> ICO)
1. Gerar o `.ico`:
   - `.\scripts\convert-png-to-ico.ps1 -SourceImage "C:\Users\robso\Downloads\dds icone.png" -OutputIco ".\src\DDSStudyOS.App\Assets\DDSStudyOS.ico"`
2. Recriar o `.aip`:
   - `.\scripts\create-advanced-installer-project.ps1 -Force -AdvancedInstallerPath "F:\Program Files (x86)\Caphyon\Advanced Installer 23.4\bin\x86\AdvancedInstaller.com"`

## 12) Troubleshooting rapido (licenca vazia)
1. Se a janela de licenca abrir sem texto, recrie o `.aip`:
   - `.\scripts\create-advanced-installer-project.ps1 -Force -AdvancedInstallerPath "F:\Program Files (x86)\Caphyon\Advanced Installer 23.4\bin\x86\AdvancedInstaller.com"`
2. Confirme no `.aip`:
   - `LicenseAgreementDlg` presente em `FragmentComponent`
   - `AgreementText` apontando para `..\legal\EULA.pt-BR.rtf`

## 13) Troubleshooting rapido (popup pedindo .NET Runtime)
1. Gere novamente o pacote de entrada e o instalador:
   - `.\scripts\prepare-installer-input.ps1`
   - `.\scripts\create-advanced-installer-project.ps1 -Force -AdvancedInstallerPath "F:\Program Files (x86)\Caphyon\Advanced Installer 23.4\bin\x86\AdvancedInstaller.com"`
2. Confirme no `.aip` que existem:
   - `PreReqComponent` com `.NET Desktop Runtime 8 (x64)`
   - `PreReqSearchComponent` para `HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App`
3. Compile e distribua somente o setup novo em `artifacts/installer-output/DDSStudyOS-Setup.exe`.
