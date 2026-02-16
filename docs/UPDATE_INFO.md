# Update Info - DDS StudyOS

Este documento centraliza as URLs usadas no Advanced Installer e no suporte do produto.

## Repositorio oficial
- GitHub: `https://github.com/Erikalellis/DDSStudyOS`

## Canais de update
- `stable`: producao
- `beta`: homologacao interna

## URLs publicas
- `Update Info URL (stable)`: `https://github.com/Erikalellis/DDSStudyOS/blob/main/installer/update/stable/update-info.json`
- `Update Info URL (beta)`: `https://github.com/Erikalellis/DDSStudyOS/blob/main/installer/update/beta/update-info.json`
- `Release Notes URL`: `https://github.com/Erikalellis/DDSStudyOS/blob/main/CHANGELOG.md`
- `Support URL`: `https://github.com/Erikalellis/DDSStudyOS/blob/main/SUPPORT.md`

## Observacao sobre auto-update do Advanced Installer
- O arquivo acima e informativo.
- Se for usar o Updater nativo do Advanced Installer, publicar tambem o feed especifico gerado pelo proprio Advanced Installer e configurar a URL dele no projeto `.aip`.

## Versionamento
- Formato: `MAJOR.MINOR.PATCH` (exemplo: `2.1.0`)
- Build de arquivo: `MAJOR.MINOR.PATCH.REVISION` (exemplo: `2.1.0.0`)

## Assinatura
- Certificado atual (homologacao interna): `CN=Deep Darkness Studios`
- Thumbprint esperado: `6780CE530A33615B591727F5334B3DD075B76422`
- Script de instalacao de certificado: `scripts/install-internal-cert.ps1`
- Script de assinatura: `scripts/sign-release.ps1`

## Checklist rapido
1. Gerar build/publish: `scripts/build-release.ps1`
2. Assinar artefatos apos publish.
3. Atualizar release notes e update info no GitHub.
4. Validar upgrade em maquina limpa.
