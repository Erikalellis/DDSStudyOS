# Evidencia de Atualizacao - 2026-02-26 (3.1.1)

## Artefatos validados
- Setup stable: `artifacts/installer-output/DDSStudyOS-Setup.exe`
- SHA256 stable: `6525887f9781e1d0e1c74bd051864540860e0897642557ed1561528509b2d29e`
- Setup beta: `artifacts/installer-output/DDSStudyOS-Beta-Setup.exe`
- SHA256 beta: `9c4af0d4e7dcc999e1d4748aec84088b0e55c2570d0f03760c84f29beccbf6d6`
- Portable: `artifacts/installer-output/DDSStudyOS-Portable.zip`
- SHA256 portable: `f2f743139e91f150bac8153a721972940ab45a368c9a4266e61980f47ffd30f1`
- Checksums: `artifacts/installer-output/DDSStudyOS-SHA256.txt`

## Canais online (updater)
- Stable update-info: `installer/update/stable/update-info.json`
- Beta update-info: `installer/update/beta/update-info.json`
- Hash do `update-info` conferido contra arquivo final: `OK` (stable e beta)
- Download URLs publicados no release `v3.1.1`: `HTTP 200` (stable e beta)

## DLC incremental
- Manifesto stable: `installer/update/stable/dlc-manifest.json`
- Manifesto beta: `installer/update/beta/dlc-manifest.json`
- Pacote DLC: `artifacts/dlc-output/DDSStudyOS-DLC-web-content.zip`
- SHA256 DLC: `a693d56f1b4cb84f40916eeef89e2a1e5bc113e48198c59c33ac7a1cec7f1d94`

## Smoke automatizado executado hoje
- First-use smoke: `artifacts/installer-logs/first-use-smoke-20260226-200043.txt`
- Clean-machine smoke (install/open/uninstall): `artifacts/installer-logs/clean-machine-smoke-20260226-200127.txt`
- Setup log: `artifacts/installer-logs/clean-machine-setup-20260226-200127-inno.log`
- Uninstall log: `artifacts/installer-logs/clean-machine-uninstall-20260226-200127.log`

## Resultado
- Fluxo de atualizacao 3.1.1 validado para stable/beta.
- Instalacao, abertura e desinstalacao validadas em smoke automatizado.
