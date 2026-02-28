# Evidencia de Atualizacao - 2026-02-28 (v3.1.4)

## Setup validado

- Arquivo (stable): `artifacts/installer-output/DDSStudyOS-Setup.exe`
- SHA256 (stable): `458576937752072a87f10e64f71346934c26ce5c7bac3c1ce3bcd8a91f925570`
- Arquivo (beta): `artifacts/installer-output/DDSStudyOS-Beta-Setup.exe`
- SHA256 (beta): `7e439f16ad853977d14723950843a307ea889a586352eb72c268036c7a88b946`
- Arquivo (portable): `artifacts/installer-output/DDSStudyOS-Portable.zip`
- SHA256 (portable): `4194f4709be82d51b8ecf576ba65e87840b9b04cdc250586f891c393675ea1fa`
- Versao alvo do pacote: `3.1.4`
- Thumbprint configurado no update-info: `6780CE530A33615B591727F5334B3DD075B76422`

## Smoke test (maquina limpa)

- Relatorio: `artifacts/installer-logs/clean-machine-smoke-20260228-100341.txt`
- Setup: `OK`
- Registro de desinstalacao no Windows: `OK`
- Executavel principal encontrado: `OK`
- Abertura do app por 12s: `OK`
- Uninstall silencioso: `OK`

## Logs detalhados

- Setup log: `artifacts/installer-logs/clean-machine-setup-20260228-100341-inno.log`
- Uninstall log: `artifacts/installer-logs/clean-machine-uninstall-20260228-100341.log`

## Smoke de primeiro uso (build 3.1.4)

- Relatorio: `artifacts/installer-logs/first-use-smoke-20260228-091627.txt`
- Marcadores obrigatorios: `OK` (`SMOKE_FIRST_USE:*`)
- Auto-DLC em modo smoke: `OK` (desabilitado explicitamente durante a validacao)

## Cobertura adicional

- Teste automatizado de agenda recorrente: `tests/DDSStudyOS.App.Tests/ReminderRepositoryRecurringTests.cs`
- Cenarios cobertos: criar lembrete recorrente, aplicar `Adiar`, aplicar `Concluir / proxima`, reiniciar repositorio e confirmar persistencia.
