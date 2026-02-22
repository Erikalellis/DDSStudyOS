# Update Info - DDS StudyOS

Este documento centraliza as URLs usadas no release do instalador oficial (Inno Setup) e no suporte do produto.

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

## Observacao sobre auto-update
- O arquivo acima e informativo.
- O fluxo oficial de distribuicao usa Inno Setup com release no GitHub.
- Se for usar Advanced Installer em contingencia, publique tambem o feed especifico do updater dele.

## Versionamento
- Formato: `MAJOR.MINOR.PATCH` (exemplo: `2.1.0`)
- Build de arquivo: `MAJOR.MINOR.PATCH.REVISION` (exemplo: `2.1.0.0`)

## Assinatura
- Certificado atual (homologacao interna): `CN=Deep Darkness Studios`
- Thumbprint esperado: `6780CE530A33615B591727F5334B3DD075B76422`
- Script de instalacao de certificado: `scripts/install-internal-cert.ps1`
- Script de assinatura: `scripts/sign-release.ps1`

## Checklist rapido
1. Gerar pacote completo de release: `scripts/build-release-package.ps1`
2. Assinar artefatos apos publish.
3. Atualizar release notes no GitHub (CHANGELOG).
4. Publicar juntos: setup estavel, setup beta, portatil e SHA256.
5. Validar upgrade em maquina limpa.
