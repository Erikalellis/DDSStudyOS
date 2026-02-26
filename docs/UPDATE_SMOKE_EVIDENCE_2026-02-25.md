# Evidencia de Atualizacao - 2026-02-25

## Setup validado
- Arquivo (stable): `artifacts/installer-output/DDSStudyOS-Setup.exe`
- SHA256 (stable): `994edee7e9c5fea6ed452bc049dae60431552ac794d463b77aa25ee9fd5059a6`
- Arquivo (beta): `artifacts/installer-output/DDSStudyOS-Beta-Setup.exe`
- SHA256 (beta): `994edee7e9c5fea6ed452bc049dae60431552ac794d463b77aa25ee9fd5059a6`
- Arquivo (portable): `artifacts/installer-output/DDSStudyOS-Portable.zip`
- SHA256 (portable): `0ebba8ada77e40bbaeedff0a6e8d730e148054edadb9e7020fa91803c6a31455`
- Versao alvo do pacote: `3.1.0`
- Thumbprint configurado no update-info: `6780CE530A33615B591727F5334B3DD075B76422`

## Smoke test (maquina limpa)
- Relatorio: `artifacts/installer-logs/clean-machine-smoke-20260225-195734.txt`
- Setup: `OK`
- Registro de desinstalacao no Windows: `OK`
- Executavel principal encontrado: `OK`
- Abertura do app por 12s: `OK`
- Uninstall silencioso: `OK`

## Logs detalhados
- Setup log: `artifacts/installer-logs/clean-machine-setup-20260225-195734-inno.log`
- Uninstall log: `artifacts/installer-logs/clean-machine-uninstall-20260225-195734.log`

## Smoke de primeiro uso (build 3.1.0)
- Relatorio: `artifacts/installer-logs/first-use-smoke-20260225-212706.txt`
- Marcadores obrigatorios: `OK` (`SMOKE_FIRST_USE:*`)
