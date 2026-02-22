# DDS StudyOS - Checklist Oficial de Regressão (Beta)

Use esta lista antes de cada pre-release e release.

## 1) Build e Empacotamento
- [ ] `dotnet build DDSStudyOS.sln -c Debug -p:Platform=x64`
- [ ] `dotnet build DDSStudyOS.sln -c Release -p:Platform=x64`
- [ ] `.\scripts\build-release-package.ps1`
- [ ] Setup gerado: `artifacts/installer-output/DDSStudyOS-Setup.exe`
- [ ] Setup beta gerado: `artifacts/installer-output/DDSStudyOS-Beta-Setup.exe`
- [ ] Portátil gerado: `artifacts/installer-output/DDSStudyOS-Portable.zip`
- [ ] Checksums gerados: `artifacts/installer-output/DDSStudyOS-SHA256.txt`

## 2) Fluxos Críticos do App
- [ ] Splash abre e encerra sem travar.
- [ ] Onboarding abre no primeiro uso com campos legíveis.
- [ ] Tour guiado mostra título e descrição em todos os passos.
- [ ] Botão `Config. Pomodoro` abre `Configurações`.
- [ ] Dashboard, Cursos, Materiais, Agenda, Navegador e Desenvolvimento navegam corretamente.

## 3) Navegador Interno
- [ ] Busca por texto usa mecanismo padrão configurado.
- [ ] URL direta abre corretamente.
- [ ] `dds://inicio` renderiza página interna.
- [ ] Favoritos e notas persistem entre sessões.
- [ ] Abrir curso leva ao navegador sem fechar o app.

## 4) Cofre DDS e Backup
- [ ] Importação CSV do cofre funciona.
- [ ] Credencial selecionada abre no navegador com autofill.
- [ ] Exportar backup criptografado funciona (senha mestra obrigatória).
- [ ] Restaurar backup funciona sem perda de dados.
- [ ] Validação de backup retorna status correto.

## 5) Instalação e Desinstalação
- [ ] Instalação com log: `.\scripts\run-setup-with-log.ps1`
- [ ] App abre ao final do setup.
- [ ] Atalho do Menu Iniciar criado.
- [ ] Atalho desktop criado apenas quando selecionado.
- [ ] Desinstalação remove app e `%LOCALAPPDATA%\DDSStudyOS`.
- [ ] Desinstalação não remove runtimes globais do Windows.

## 6) Publicação
- [ ] Release notes atualizadas (`CHANGELOG.md`).
- [ ] `installer/update/stable/update-info.json` e `installer/update/beta/update-info.json` sincronizados.
- [ ] Assets publicados no GitHub Release.
- [ ] SHA256 dos assets conferido.
