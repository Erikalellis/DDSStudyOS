# Evidencia de Atualizacao - 2026-02-27

## Setup validado

- Arquivo (stable): `artifacts/installer-output/DDSStudyOS-Setup.exe`
- SHA256 (stable): `a17a3a42ef261239d81fc08e1b74d88119b1e68d3ae625484c05f9b278a72d65`
- Arquivo (beta): `artifacts/installer-output/DDSStudyOS-Beta-Setup.exe`
- SHA256 (beta): `bd1e58043c1e0e3906948aee1844a4c26ea5b0593d46a29371a579aba78db2e9`
- Arquivo (portable): `artifacts/installer-output/DDSStudyOS-Portable.zip`
- SHA256 (portable): `599aa202e14aef1374cb4a89076d01c12b966167e6a20758557f61648600399d`
- Versao alvo do pacote: `3.1.2`
- Thumbprint configurado no update-info: `6780CE530A33615B591727F5334B3DD075B76422`

## Smoke test (maquina limpa)

- Relatorio: `artifacts/installer-logs/clean-machine-smoke-20260227-031935.txt`
- Setup: `OK`
- Registro de desinstalacao no Windows: `OK`
- Executavel principal encontrado: `OK`
- Abertura do app por 12s: `OK`
- Uninstall silencioso: `OK`

## Logs detalhados

- Setup log: `artifacts/installer-logs/clean-machine-setup-20260227-031935-inno.log`
- Uninstall log: `artifacts/installer-logs/clean-machine-uninstall-20260227-031935.log`

## Smoke de primeiro uso (build 3.1.2)

- Relatorio: `artifacts/installer-logs/first-use-smoke-20260227-032127.txt`
- Marcadores obrigatorios: `OK` (`SMOKE_FIRST_USE:*`)
