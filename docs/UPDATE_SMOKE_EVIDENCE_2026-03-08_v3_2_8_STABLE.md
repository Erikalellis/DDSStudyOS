# Evidência de Promoção Stable - 3.2.8 (2026-03-08)

## Objetivo

- Promover o canal `stable` para `3.2.8`.
- Publicar a revisao visual/portal do `DDS Study Pass`.
- Manter `beta` ativo em `3.2.8-beta.1` sem quebrar feeds ou DLC.

## Alterações consolidadas no corte

- `DDS Study Pass` aplicado como nome publico da experiencia de loja/catalogo.
- Atalhos de `DDS Study Pass` promovidos no `Dashboard` e na `Home`.
- Portal publico `http://177.71.165.60/studyos/` alinhado como origem oficial do catalogo remoto.
- Links do app ajustados para a rota publica `/studyos/`.

## Validação local

- `dotnet build DDSStudyOS.sln -c Release -p:Platform=x64 --no-restore`
  - resultado: `OK`
- `dotnet test tests/DDSStudyOS.App.Tests/DDSStudyOS.App.Tests.csproj -c Release -p:Platform=x64 --no-build --filter "StoreCatalogServiceTests|DeepLinkServiceTests|UpdateDistributionConfigTests"`
  - resultado: `10/10`
- `scripts/build-release-package.ps1`
  - artefatos gerados:
    - `artifacts/installer-output/DDSStudyOS-Setup.exe`
    - `artifacts/installer-output/DDSStudyOS-Beta-Setup.exe`
    - `artifacts/installer-output/DDSStudyOS-Portable.zip`
    - `artifacts/installer-output/DDSStudyOS-SHA256.txt`
- `scripts/build-dlc-package.ps1 -Channel stable -ReleaseTag v3.2.8`
  - resultado: `OK`
- `scripts/build-dlc-package.ps1 -Channel beta -ReleaseTag v3.2.8`
  - resultado: `OK`

## Estado dos feeds públicos

- `stable update-info`
  - URL: `https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/stable/update-info.json`
  - `currentVersion = 3.2.8`
  - `HTTP 200`
- `beta update-info`
  - URL: `https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/beta/update-info.json`
  - `currentVersion = 3.2.8-beta.1`
  - `HTTP 200`
- `stable dlc-manifest`
  - `releaseTag = v3.2.8`
  - `modules = 9`
  - `HTTP 200`
- `beta dlc-manifest`
  - `releaseTag = v3.2.8`
  - `modules = 9`
  - `HTTP 200`

## Estado dos assets públicos

- `https://github.com/Erikalellis/DDSStudyOS-Updates/releases/download/v3.2.8/DDSStudyOS-Setup.exe`
  - `HTTP 200`
- `https://github.com/Erikalellis/DDSStudyOS-Updates/releases/download/v3.2.8/DDSStudyOS-Beta-Setup.exe`
  - `HTTP 200`
- `https://github.com/Erikalellis/DDSStudyOS-Updates/releases/download/v3.2.8/DDSStudyOS-Portable.zip`
  - `HTTP 200`
- `https://github.com/Erikalellis/DDSStudyOS-Updates/releases/download/v3.2.8/DDSStudyOS-DLC-study-templates.zip`
  - `HTTP 200`

## Publicação executada

- release pública atualizada em `Erikalellis/DDSStudyOS-Updates`
  - tag: `v3.2.8`
- manifests públicos sincronizados no branch `main` do repositório `DDSStudyOS-Updates`
- código fonte local preparado para commit/push com versão `3.2.8`

## Observação operacional

- O smoke em computadores externos continua manual.
- Este corte já está pronto para ser instalado e validado em outras máquinas via:
  - instalador estável `DDSStudyOS-Setup.exe`
  - instalador beta `DDSStudyOS-Beta-Setup.exe`
  - pacote portátil `DDSStudyOS-Portable.zip`
