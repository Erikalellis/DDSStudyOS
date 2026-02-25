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
