# Evidencia de Atualizacao - 2026-02-27 (v3.1.3)

## Setup validado

- Arquivo (stable): `artifacts/installer-output/DDSStudyOS-Setup.exe`
- SHA256 (stable): `3bb9b168f209590568e8a98a2adc313a576a9396eb8f9a963b74bea69bfd26d6`
- Arquivo (beta): `artifacts/installer-output/DDSStudyOS-Beta-Setup.exe`
- SHA256 (beta): `311b8522a84c16878aa4dd88d5cfcdfc6d7be2eca3a238a6e41874278a13e679`
- Arquivo (portable): `artifacts/installer-output/DDSStudyOS-Portable.zip`
- SHA256 (portable): `47a592150dae0117c5dcaedfd97a42e305261218d9f455ac70e30437f2eb5b9e`
- Versao alvo do pacote: `3.1.3`
- Thumbprint configurado no update-info: `6780CE530A33615B591727F5334B3DD075B76422`

## Smoke test (maquina limpa)

- Relatorio: `artifacts/installer-logs/clean-machine-smoke-20260227-090002.txt`
- Setup: `OK`
- Registro de desinstalacao no Windows: `OK`
- Executavel principal encontrado: `OK`
- Abertura do app por 12s: `OK`
- Uninstall silencioso: `OK`

## Logs detalhados

- Setup log: `artifacts/installer-logs/clean-machine-setup-20260227-090002-inno.log`
- Uninstall log: `artifacts/installer-logs/clean-machine-uninstall-20260227-090002.log`
- Gate one-click: `artifacts/release-gate/release-gate-20260227-084607.md`

## Smoke de primeiro uso (build 3.1.3)

- Relatorio: `artifacts/installer-logs/first-use-smoke-20260227-085924.txt`
- Marcadores obrigatorios: `OK` (`SMOKE_FIRST_USE:*`)
