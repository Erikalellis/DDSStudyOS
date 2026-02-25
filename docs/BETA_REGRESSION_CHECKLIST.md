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
- [ ] Splash abre e encerra sem travar.
- [ ] Onboarding abre no primeiro uso com campos legíveis.
- [ ] Tour guiado mostra título e descrição em todos os passos.
- [x] Botão `Config. Pomodoro` abre `Configurações`.
- [ ] Dashboard, Cursos, Materiais, Agenda, Navegador e Desenvolvimento navegam corretamente.

## 3) Navegador Interno
- [ ] Busca por texto usa mecanismo padrão configurado.
- [ ] URL direta abre corretamente.
- [x] `dds://inicio` renderiza página interna.
- [x] Favoritos e notas persistem entre sessões.
- [ ] Abrir curso leva ao navegador sem fechar o app.

## 4) Cofre DDS e Backup
- [ ] Importação CSV do cofre funciona.
- [ ] Credencial selecionada abre no navegador com autofill.
- [ ] Exportar backup criptografado funciona (senha mestra obrigatória).
- [ ] Restaurar backup funciona sem perda de dados.
- [ ] Validação de backup retorna status correto.

## 5) Instalação e Desinstalação
- [x] Instalação com log: `.\scripts\run-setup-with-log.ps1`
- [x] App abre ao final do setup.
- [ ] Atalho do Menu Iniciar criado.
- [ ] Atalho desktop criado apenas quando selecionado.
- [x] Desinstalação remove app e `%LOCALAPPDATA%\DDSStudyOS`.
- [ ] Desinstalação não remove runtimes globais do Windows.

## 6) Publicação
- [x] Release notes atualizadas (`CHANGELOG.md`).
- [x] `installer/update/stable/update-info.json` e `installer/update/beta/update-info.json` sincronizados.
- [x] Assets publicados no GitHub Release.
- [x] SHA256 dos assets conferido.
