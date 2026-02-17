# Advanced Installer Project

Esta pasta guarda o projeto `.aip` do instalador do DDS StudyOS.

## Fluxo recomendado (automatico)
No repositorio raiz, use um comando unico:

```powershell
.\scripts\build-installer.ps1
```

Se necessario, informe manualmente o caminho do CLI:

```powershell
.\scripts\build-installer.ps1 -AdvancedInstallerPath "F:\Program Files (x86)\Caphyon\Advanced Installer 23.4\bin\x86\AdvancedInstaller.com"
```

Esse fluxo:
- gera publish self-contained
- recria `installer-input`
- recria `DDSStudyOS.aip` com pre-requisito automatico de `.NET Desktop Runtime 8 (x64)`
- compila o setup em `artifacts/installer-output`

## Gerar somente o projeto `.aip`
No repositorio raiz:

```powershell
.\scripts\create-advanced-installer-project.ps1 -PrepareInput -SignExecutable -Force
```

Se necessario, informe manualmente o caminho do CLI:

```powershell
.\scripts\create-advanced-installer-project.ps1 -PrepareInput -SignExecutable -Force -AdvancedInstallerPath "F:\Program Files (x86)\Caphyon\Advanced Installer 23.4\bin\x86\AdvancedInstaller.com"
```

Saida principal:
- `installer/advanced-installer/DDSStudyOS.aip`
- `artifacts/installer-output/` (destino do setup)

## Arquivos legais versionados
- `installer/legal/EULA.pt-BR.rtf`
- `installer/legal/EULA.es.rtf`
- `installer/legal/README_INSTALLER.pt-BR.rtf`

## Compilar setup no Advanced Installer
1. Abrir `installer/advanced-installer/DDSStudyOS.aip`
2. Validar Product Details, Files and Folders e Digital Signature
3. Confirmar idioma principal: `Português (Brasil)` (`pt_BR`)
4. Pressionar `Build`

## Observacao
Se o `AdvancedInstaller.com` nao estiver no PATH, o script tenta encontrar a instalacao em:
- `C:\Program Files (x86)\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com`
- `C:\Program Files\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com`
