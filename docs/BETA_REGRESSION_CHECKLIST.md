# DDS StudyOS - Checklist Oficial de Regressão (Beta)

Use esta lista antes de cada pre-release e release.

## 1) Build e Empacotamento
- [x] `dotnet build DDSStudyOS.sln -c Debug -p:Platform=x64`
- [x] `dotnet build DDSStudyOS.sln -c Release -p:Platform=x64`
- [x] `.\scripts\build-release-package.ps1`
- [x] Setup gerado: `artifacts/installer-output/DDSStudyOS-Setup.exe`
- [x] Setup beta gerado: `artifacts/installer-output/DDSStudyOS-Beta-Setup.exe`
- [x] Portátil gerado: `artifacts/installer-output/DDSStudyOS-Portable.zip`
- [x] Checksums gerados: `artifacts/installer-output/DDSStudyOS-SHA256.txt`

## 2) Fluxos Críticos do App
- [x] Splash abre e encerra sem travar.
- [x] Onboarding abre no primeiro uso com campos legíveis.
- [x] Tour guiado mostra título e descrição em todos os passos.
- [x] Smoke automatizado de primeiro uso (`--smoke-first-use`) passa com marcadores de onboarding/tour/navegador.
- [x] Botão `Config. Pomodoro` abre `Configurações`.
- [x] Dashboard, Cursos, Materiais, Agenda, Navegador e Desenvolvimento navegam corretamente.

## 3) Navegador Interno
- [x] Busca por texto usa mecanismo padrão configurado.
- [x] URL direta abre corretamente.
- [x] `dds://inicio` renderiza página interna.
- [x] Favoritos e notas persistem entre sessões.
- [x] Abrir curso leva ao navegador sem fechar o app.

## 4) Cofre DDS e Backup
- [x] Importação CSV do cofre funciona.
- [x] Credencial selecionada abre no navegador com autofill.
- [x] Exportar backup criptografado funciona (senha mestra obrigatória).
- [x] Restaurar backup funciona sem perda de dados.
- [x] Validação de backup retorna status correto.

## 5) Instalação e Desinstalação
- [x] Instalação com log: `.\scripts\run-setup-with-log.ps1`
- [x] App abre ao final do setup.
- [x] Atalho do Menu Iniciar criado.
- [x] Atalho desktop criado apenas quando selecionado.
- [x] Entrada de desinstalação aparece em Apps/Painel de Controle do Windows.
- [x] Desinstalação remove app e `%LOCALAPPDATA%\DDSStudyOS`.
- [x] Desinstalação não remove runtimes globais do Windows.

## 6) Publicação
- [x] Release notes atualizadas (`CHANGELOG.md`).
- [x] `installer/update/stable/update-info.json` e `installer/update/beta/update-info.json` sincronizados.
- [x] Assets publicados no GitHub Release.
- [x] SHA256 dos assets conferido.

## 7) Evidências Formais (25/02/2026)
- [x] Ciclo 1 executado sem bug crítico.
- [x] Ciclo 2 consecutivo executado sem bug crítico.
- [x] Log consolidado do ciclo 1: `artifacts/installer-logs/regression-install-check-20260225-110835.txt`.
- [x] Log consolidado do ciclo 2: `artifacts/installer-logs/regression-install-check-20260225-111448.txt`.
- [x] Setup padrão do ciclo 1: `artifacts/installer-logs/regression-default-20260225-110835-inno.log`.
- [x] Uninstall padrão do ciclo 1: `artifacts/installer-logs/regression-after-default-20260225-110835-uninstall.log`.
- [x] Smoke machine-clean com uninstall visível: `artifacts/installer-logs/clean-machine-smoke-20260225-162512.txt`.
- [x] Smoke automatizado de primeiro uso: `artifacts/installer-logs/first-use-smoke-20260225-205532.txt`.

## 8) Fechamento 3.1.1 (26/02/2026)
- [x] Smoke automatizado de primeiro uso executado: `artifacts/installer-logs/first-use-smoke-20260226-200043.txt`.
- [x] Smoke clean-machine (setup + abrir + uninstall) executado: `artifacts/installer-logs/clean-machine-smoke-20260226-200127.txt`.
- [x] Setup log do ciclo: `artifacts/installer-logs/clean-machine-setup-20260226-200127-inno.log`.
- [x] Uninstall log do ciclo: `artifacts/installer-logs/clean-machine-uninstall-20260226-200127.log`.
- [x] `update-info` stable/beta sincronizado com SHA256 real dos artefatos finais.
- [x] Assets do release `v3.1.1` atualizados com `--clobber` (stable/beta/portable/SHA256).

## 9) Fechamento 3.1.2 (27/02/2026)
- [x] Smoke automatizado de primeiro uso executado: `artifacts/installer-logs/first-use-smoke-20260227-032127.txt`.
- [x] Smoke clean-machine (setup + abrir + uninstall) executado: `artifacts/installer-logs/clean-machine-smoke-20260227-031935.txt`.
- [x] Setup log do ciclo: `artifacts/installer-logs/clean-machine-setup-20260227-031935-inno.log`.
- [x] Uninstall log do ciclo: `artifacts/installer-logs/clean-machine-uninstall-20260227-031935.log`.
- [x] `update-info` stable/beta sincronizado com SHA256 real dos artefatos finais (`installer/update/stable/update-info.json`, `installer/update/beta/update-info.json`).
- [x] Uninstall silencioso validado sem prompt final (correcao de `CurUninstallStepChanged` para respeitar `UninstallSilent`).

## 10) Fechamento 3.1.3 (27/02/2026)
- [x] Pipeline one-click executado com sucesso: `scripts/release-one-click.ps1`.
- [x] Smoke automatizado de primeiro uso executado: `artifacts/installer-logs/first-use-smoke-20260227-085924.txt`.
- [x] Smoke clean-machine (setup + abrir + uninstall) executado: `artifacts/installer-logs/clean-machine-smoke-20260227-090002.txt`.
- [x] Setup log do ciclo: `artifacts/installer-logs/clean-machine-setup-20260227-090002-inno.log`.
- [x] Uninstall log do ciclo: `artifacts/installer-logs/clean-machine-uninstall-20260227-090002.log`.
- [x] Gate automatico da release em `GO`: `artifacts/release-gate/release-gate-20260227-084607.md`.
- [x] `update-info` e `dlc-manifest` stable/beta sincronizados com a versao `3.1.3`.
