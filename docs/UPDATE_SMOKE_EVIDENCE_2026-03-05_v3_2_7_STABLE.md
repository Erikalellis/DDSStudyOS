# Evidência de Promoção Stable - 3.2.7 (2026-03-05)

## Escopo

- Promoção do canal `stable` para `3.2.7`.
- Publicação dos manifests e assets no repositório público `Erikalellis/DDSStudyOS-Updates`.
- Validação de integridade de update e DLC em `stable` e `beta`.

## Gate local antes da promoção

- `dotnet build DDSStudyOS.sln -c Release -p:Platform=x64` -> OK
- `dotnet test DDSStudyOS.sln -c Release -p:Platform=x64 --no-build` -> OK (`40/40`)
- `scripts/validate-first-use-smoke.ps1` -> OK
  - log: `artifacts/installer-logs/first-use-smoke-20260305-111441.txt`
- `scripts/validate-clean-machine-smoke.ps1 -RunSetup -KeepInstalled` -> OK (segunda execução após encerrar processo aberto)
  - logs:
    - `artifacts/installer-logs/clean-machine-smoke-20260305-111852.txt`
    - `artifacts/installer-logs/clean-machine-setup-20260305-111852-inno.log`

## Publicação

- Release pública criada:
  - `https://github.com/Erikalellis/DDSStudyOS-Updates/releases/tag/v3.2.7`
- Assets publicados:
  - `DDSStudyOS-Setup.exe`
  - `DDSStudyOS-Portable.zip`
  - `DDSStudyOS-SHA256.txt`
  - pacote DLC estável completo (`9` módulos)

## Manifests promovidos

- `installer/update/stable/update-info.json`
  - `currentVersion = 3.2.7`
  - `downloadUrl = releases/latest/download/DDSStudyOS-Setup.exe` (repo público de updates)
- `installer/update/stable/dlc-manifest.json`
  - `channel = stable`
  - `appVersion = 3.2.7`
  - `releaseTag = v3.2.7`
  - `modules = 9`

## Validação remota pós-publicação

- Feeds:
  - stable update feed -> `3.2.7` (OK)
  - stable dlc feed -> `3.2.7` / `9` módulos (OK)
  - beta update feed -> `3.2.7` (OK)
  - beta dlc feed -> `3.2.7` / `9` módulos (OK)
- HEAD HTTP:
  - `.../releases/latest/download/DDSStudyOS-Setup.exe` -> `200`
  - `.../releases/download/v3.2.7/DDSStudyOS-Setup.exe` -> `200`
  - módulos DLC stable (`v3.2.7`) -> `200`
  - setup e módulos beta (`v3.2.7-beta.1`) -> `200`

## Resultado

Promoção `stable 3.2.7` concluída com sucesso, canal público operacional para update e DLC, mantendo compatibilidade com clientes beta existentes.
