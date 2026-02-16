# Update Info - DDS StudyOS

Este documento centraliza as URLs e parâmetros usados no mecanismo de atualização (Advanced Installer / updater interno).

## Canais de atualização
- `stable`: produção
- `beta`: homologação interna

## URLs de publicação
- `Update Feed URL (stable)`: `PREENCHER_AQUI`
- `Update Feed URL (beta)`: `PREENCHER_AQUI`
- `Release Notes URL`: `PREENCHER_AQUI`
- `Support URL`: `PREENCHER_AQUI`

## Política de versionamento
- Formato: `MAJOR.MINOR.PATCH` (ex.: `2.1.0`)
- Build de arquivo: `MAJOR.MINOR.PATCH.REVISION` (ex.: `2.1.0.0`)
- Mudança de canal deve preservar compatibilidade de upgrade.

## Assinatura e confiança
- Certificado atual (homologação interna): `CN=Deep Darkness Studios`
- Thumbprint esperado: `6780CE530A33615B591727F5334B3DD075B76422`
- Script de instalação do certificado: `scripts/install-internal-cert.ps1`
- Script de assinatura de release: `scripts/sign-release.ps1`

## Checklist de release
1. Build/publish: `scripts/build-release.ps1`
2. Assinar artefatos (`.exe`/`.msix`) após publish.
3. Atualizar feed e release notes.
4. Validar upgrade em ambiente limpo.
