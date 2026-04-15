# Update Info - DDS StudyOS

Este documento centraliza as URLs usadas no release do instalador oficial (Inno Setup) e no suporte do produto.

## Repositorio oficial

- GitHub: `https://github.com/Erikalellis/DDSStudyOS-Updates`

## Canais de update

- `stable`: producao
- `beta`: homologacao interna

## Estado atual dos canais (2026-04-15)

- `stable`: `3.3.1`
- `beta`: `3.3.0-beta.1`

## URLs publicas

- `Update Info URL (stable)`: `https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/stable/update-info.json`
- `Update Info URL (beta)`: `https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/beta/update-info.json`
- `DLC Manifest URL (stable)`: `https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/stable/dlc-manifest.json`
- `DLC Manifest URL (beta)`: `https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/beta/dlc-manifest.json`
- `Release Notes URL`: `https://github.com/Erikalellis/DDSStudyOS-Updates/blob/main/CHANGELOG_PUBLIC.md`
- `Support URL`: `https://github.com/Erikalellis/DDSStudyOS-Updates/blob/main/USER_GUIDE_PUBLIC.md`
- `Releases`: `https://github.com/Erikalellis/DDSStudyOS-Updates/releases`

## Observacao sobre auto-update

- O arquivo acima e informativo.
- O fluxo oficial de distribuicao usa Inno Setup com release no GitHub.
- Para updates incrementais, use o manifesto DLC por canal e pacotes `.zip` assinados por hash SHA256.
- Se for usar Advanced Installer em contingencia, publique tambem o feed especifico do updater dele.

## Versionamento

- Formato: `MAJOR.MINOR.PATCH` (exemplo: `3.3.1`)
- Build de arquivo: `MAJOR.MINOR.PATCH.REVISION` (exemplo: `3.3.1.0`)

## Campos opcionais no `update-info.json`

- `signerThumbprint`: thumbprint esperado do certificado Authenticode do setup.
- `installerSha256`: hash SHA256 esperado do instalador (quando quiser validação forte de integridade no app).

## Assinatura

- Certificado atual (homologacao interna): `CN=Deep Darkness Studios`
- Thumbprint esperado: `6780CE530A33615B591727F5334B3DD075B76422`
- Script de instalacao de certificado: `scripts/install-internal-cert.ps1`
- Script de assinatura: `scripts/sign-release.ps1`

## Checklist rapido

1. Gerar pacote completo de release: `scripts/build-release-package.ps1`
2. Gerar pacotes DLC por canal: `scripts/build-dlc-package.ps1 -Channel stable` e `scripts/build-dlc-package.ps1 -Channel beta`
3. Assinar artefatos apos publish.
4. Sincronizar `update-info` e `dlc-manifest` com versao/tag do release.
5. Atualizar release notes no GitHub (CHANGELOG).
6. Publicar juntos: setup estavel, setup beta, portatil, SHA256 e DLC.
7. Validar upgrade em maquina limpa.
