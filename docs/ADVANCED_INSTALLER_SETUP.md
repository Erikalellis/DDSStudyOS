# Advanced Installer - Legado/Backup

## Status
Este fluxo nao e mais o padrao do projeto.

Fluxo oficial atual:
- `docs/INNO_SETUP.md`

Use este guia apenas como contingencia.

## Build legado (quando realmente necessario)
No raiz do repositorio:

```powershell
.\scripts\build-installer.ps1
```

Saida esperada:
- `artifacts/installer-output/DDSStudyOS-Setup.exe`

## Observacoes
- O app continua sendo publicado em modo self-contained.
- O projeto `.aip` permanece versionado em `installer/advanced-installer/DDSStudyOS.aip`.
- Ajustes novos devem ser feitos primeiro no fluxo Inno; este fluxo recebe apenas manutencao minima.
