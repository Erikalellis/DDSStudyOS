# DDS StudyOS - Playbook One-Click de Release

Este playbook consolida em um unico comando:

- build do pacote de release (`Setup`, `Beta Setup`, `Portable`, `SHA256`);
- build dos manifests DLC (`stable` e `beta`);
- evidencias automatizadas (`first-use smoke` e `clean-machine smoke`);
- gate automatico com relatorio final (GO/FAIL).

## Comando padrao

```powershell
.\scripts\release-one-click.ps1
```

## Fluxo completo recomendado (fim-a-fim)

1. Gerar artefatos + DLC + smoke + gate:

```powershell
.\scripts\release-one-click.ps1 -Owner Erikalellis -Repo DDSStudyOS -ReleaseTag v3.1.3
```

2. Commitar atualizações de versionamento/feed/evidências:

```powershell
git add .
git commit -m "release: fechar ciclo 3.1.3"
git push origin main
```

3. Criar tag e publicar release:

```powershell
git tag -a v3.1.3 -m "Release 3.1.3"
git push origin v3.1.3
gh release create v3.1.3 `
  artifacts/installer-output/DDSStudyOS-Setup.exe `
  artifacts/installer-output/DDSStudyOS-Beta-Setup.exe `
  artifacts/installer-output/DDSStudyOS-Portable.zip `
  artifacts/installer-output/DDSStudyOS-SHA256.txt `
  artifacts/dlc-output/DDSStudyOS-DLC-web-content.zip `
  --repo Erikalellis/DDSStudyOS `
  --title "DDS StudyOS v3.1.3"
```

## Saidas esperadas

- `artifacts/installer-output/DDSStudyOS-Setup.exe`
- `artifacts/installer-output/DDSStudyOS-Beta-Setup.exe`
- `artifacts/installer-output/DDSStudyOS-Portable.zip`
- `artifacts/installer-output/DDSStudyOS-SHA256.txt`
- `installer/update/stable/dlc-manifest.json`
- `installer/update/beta/dlc-manifest.json`
- `artifacts/installer-logs/first-use-smoke-*.txt`
- `artifacts/installer-logs/clean-machine-smoke-*.txt`
- `artifacts/release-gate/release-gate-*.md`

## Criterio do gate automatico

O gate so retorna `GO` quando:

- os 4 artefatos principais existem;
- `update-info` stable/beta estao sincronizados com a versao do app;
- manifests DLC stable/beta foram gerados e validados;
- smoke tests first-use e clean-machine passaram sem marcador `[FAIL]`.

Se qualquer check falhar, o script encerra com `FAIL` e codigo de saida diferente de zero.

## Parametros uteis

```powershell
.\scripts\release-one-click.ps1 -Owner Erikalellis -Repo DDSStudyOS -ReleaseTag v3.1.3
```

```powershell
.\scripts\release-one-click.ps1 -KeepInstalled
```

```powershell
.\scripts\release-one-click.ps1 -SkipFirstUseSmoke -SkipCleanMachineSmoke -AllowSkippedChecks
```

Use o ultimo comando apenas para build tecnico rapido (nao substitui gate de release).
