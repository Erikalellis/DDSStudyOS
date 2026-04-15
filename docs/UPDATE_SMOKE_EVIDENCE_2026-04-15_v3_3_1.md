# Evidencia de Promocao Stable - 3.3.1 (2026-04-15)

## Escopo

- Promocao do canal `stable` para `3.3.1`.
- Publicacao dos assets oficiais no repositorio publico `Erikalellis/DDSStudyOS-Updates`.
- Validacao do manifesto de update estavel com hash e thumbprint.

## Gate local antes da promocao

- `dotnet build src/DDSStudyOS.App/DDSStudyOS.App.csproj --configuration Release --no-restore` -> OK
- `scripts/build-release-package.ps1 -SkipBeta -SkipPortable` -> OK (gerou setup estavel e SHA)

## Publicacao

- Release publica criada:
  - `https://github.com/Erikalellis/DDSStudyOS-Updates/releases/tag/v3.3.1`
- Assets publicados:
  - `DDSStudyOS-Setup.exe`
  - `DDSStudyOS-SHA256.txt`

## Manifesto promovido

- `installer/update/stable/update-info.json`
  - `currentVersion = 3.3.1`
  - `downloadUrl = releases/latest/download/DDSStudyOS-Setup.exe`
  - `installerSha256` preenchido
  - `signerThumbprint = 6780CE530A33615B591727F5334B3DD075B76422`

## Resultado

Promocao `stable 3.3.1` concluida com sucesso para clientes do canal estavel.
O canal `beta` permaneceu em `3.3.0-beta.1` nesta rodada.
