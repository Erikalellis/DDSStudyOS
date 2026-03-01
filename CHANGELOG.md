# Changelog

Todas as mudanças importantes neste projeto serão documentadas neste arquivo.

O formato é baseado em **Keep a Changelog** e o projeto segue **SemVer**.

## [Unreleased]
### Planejado
- Proxima entrega: `3.2.2` em modelo DLC (`Power-Up`), com templates de estudo, presets extras de Pomodoro e publicacao do `help-center`.

## [3.2.1] - 2026-03-01
### Adicionado
- Migracao dos clientes `3.2.1+` para o novo canal publico de distribuicao `Erikalellis/DDSStudyOS-Updates`, mantendo o repositório de código pronto para ser privado apos a janela de transicao.
- Integracao de `web-content`, `onboarding-content` e `branding-assets` como base do pack `Checkpoint`, com suporte a conteudo carregado por modulos DLC.
- Scripts de publicacao separados para distribuir manifests e assets em um repositório publico dedicado, com suporte a bridge de transicao.

### Alterado
- `AppUpdateService` e `DlcUpdateService` agora usam `UpdateDistributionConfig` para centralizar endpoints publicos de update e DLC.
- Home interna do navegador, paginas `dds://` e pagina 404 passam a priorizar conteudo externo do modulo `web-content`, com fallback local.
- Roadmap e pagina `Desenvolvimento` atualizados para comunicar a linha oficial de DLCs (`Checkpoint`, `Power-Up`, `Signal Boost` e `Quest Hub`).

## [3.2.0] - 2026-02-28
### Adicionado
- Onboarding em 4 etapas com progresso visual, validacao por passo e resumo final para personalizar o perfil no primeiro uso.
- Shell principal atualizado para menu lateral proprio (`ListView`), eliminando o layout compacto inconsistente do `NavigationView`.
- Branding da janela reforcado com titulo fixo `Deep Darkness Studios : StudyOS` e aplicacao explicita do icone no `AppWindow`.

### Alterado
- Tour inicial automatico foi desabilitado temporariamente no fluxo normal; o onboarding 3.2 passa a guiar a primeira execucao.
- Cabecalho da janela nao exibe mais o timer do Pomodoro; o cronometro continua apenas no painel lateral e na taskbar.

## [3.1.4] - 2026-02-28
### Adicionado
- Meta semanal no Home e Dashboard com relatorio de consistencia (dias ativos + minutos da semana).
- Agenda com recorrencia (`diario`, `semanal`, `mensal`) e acao de snooze configuravel no cadastro.
- Atualizacao incremental (DLC) em segundo plano no startup, com auto-check e auto-apply silencioso fora do modo smoke.
- Manual tecnico consolidado do app (iniciante -> intermediario) em `docs/MANUAL_TECNICO_APP.md`.

### Alterado
- Registro de abertura de curso agora gera atividade diaria em `study_activity` para alimentar metricas semanais por perfil.
- Documentacao central atualizada (README raiz, `docs/README.md`, `docs/UPDATE_INFO.md`, playbook one-click e suporte) com links oficiais e fluxo completo de release.
- Exportacao/importacao de backup agora preserva recorrencia e tempo de snooze dos lembretes.

## [3.1.3] - 2026-02-27
### Adicionado
- Sincronização de favoritos por perfil na tela de Cursos com exportação/importação em `.json`.
- Ação de limpeza de histórico de estudo por perfil (baseada em `last_accessed`) sem afetar cursos/favoritos.
- Filtros avançados em Materiais por curso, tipo e intervalo de data de cadastro.
- Ação "Abrir pasta" em Materiais com fallback para pasta válida quando o arquivo não existe ou for URL.
- Playbook one-click de release com gate automático (`scripts/release-one-click.ps1`) e documentação em `docs/RELEASE_ONE_CLICK_PLAYBOOK.md`.

### Alterado
- Repositório de cursos atualizado para persistir histórico por perfil em `course_history` e usar esse escopo na ordenação/listagem.

## [3.1.2] - 2026-02-27
### Alterado
- Fluxo de onboarding/tour refinado para escalas 100% e 125%, com foco em texto, posicionamento e navegacao de retorno.
- Navegacao curso -> navegador interno reforcada para manter contexto e permitir retorno sem perda de estado.
- Formularios principais (onboarding, agenda, materiais e cursos) ajustados para melhor contraste e legibilidade.

### Corrigido
- Regressao de primeiro uso validada novamente com marcadores de onboarding/tour/navegador no modo `--smoke-first-use`.
- Regressao de maquina limpa validada novamente com setup, abertura do app, registro de desinstalacao e uninstall silencioso.
- Pipeline de release/instalador robustecido para caminhos com espacos (execucao direta dos scripts `build-release`, `prepare-installer-input` e `build-inno-installer`).
- `validate-clean-machine-smoke.ps1` ajustado para gerar logs de setup/uninstall com caminho completo (`/LOG=\"...\"`) sem falhar em pastas com espacos.
- Uninstall silencioso nao abre mais prompt de feedback no fim da remocao (`UninstallSilent` no Inno), evitando travamento em regressao automatizada.

## [3.1.1] - 2026-02-26
### Adicionado
- Base do updater incremental por modulos (`DlcUpdateService`) com download, validacao de hash, aplicacao e rollback local.
- Manifestos DLC por canal em `installer/update/stable/dlc-manifest.json` e `installer/update/beta/dlc-manifest.json`.
- Script `scripts/build-dlc-package.ps1` para empacotar modulos em `.zip` e gerar manifesto com SHA256.
- Workflow `.github/workflows/dlc-pack.yml` para gerar artefatos DLC no GitHub Actions.
- Bloco de UI de DLC na aba Desenvolvimento (checar, aplicar, progresso e abertura da pasta de modulos).

## [3.1.0] - 2026-02-26
### Adicionado
- Roadmap pós-3.0 documentado em `docs/ROADMAP_3_1.md`
- Painel de atualização na aba Desenvolvimento com verificação por canal (`stable`/`beta`) e atalho para baixar update.
- Fluxo de atualização automática no app: download do instalador, validação de assinatura/hash (quando disponível), elevação UAC e início silencioso da instalação.
- Modo de smoke automatizado de primeiro uso (`--smoke-first-use`) com validação de onboarding, tour guiado e navegador interno.
- Script `scripts/validate-first-use-smoke.ps1` para executar e gerar evidência de regressão do fluxo inicial.

## [3.0.0] - 2026-02-25
### Adicionado
- Serviço de diagnóstico técnico com exportação de bundle `.zip`
- Validação de backup sem importação no painel de Configurações
- Metadata de release (produto, versão e companhia) no projeto
- Fluxo oficial de instalador com Inno Setup (`scripts/build-inno-installer.ps1`)
- Guia oficial de setup em `docs/INNO_SETUP.md`
- Checklist oficial de regressão beta em `docs/BETA_REGRESSION_CHECKLIST.md`
- Pipeline CI em `.github/workflows/ci.yml` (restore, build e publish self-contained)
- Script `scripts/build-release-package.ps1` para gerar release completo (setup estavel, setup beta, portatil e SHA256)

### Alterado
- Criptografia de backup reforçada (formato v2) com compatibilidade para backups legados
- Logger com rotação automática e leitura de tail
- Tela de Configurações com versão dinâmica da aplicação
- Documentação de release/empacotamento atualizada
- `update-info.json` dos canais stable/beta apontando para asset oficial (`DDSStudyOS-Setup.exe`)
- Documentação do Advanced Installer marcada como legado/backup
- Onboarding com layout mais responsivo para escalas de tela maiores
- Tour guiado com alvo resiliente para evitar passos sem texto/posição inválida
- Exportacao de backup agora exige senha mestra obrigatoria (arquivo `.ddsbackup` criptografado)
- Fluxo de release com sincronizacao automatica de `installer/update/stable/update-info.json` e `installer/update/beta/update-info.json`
- Scripts de release atualizados para assinatura automatica do `Setup.exe` (timestamp opcional)
- Favoritos de cursos migrados para escopo por perfil de usuario (`course_favorites`), preservando isolamento entre perfis
- Configuracoes do Pomodoro agora sao aplicadas imediatamente apos salvar na tela de Configuracoes
- Onboarding recebeu reforco de contraste nos campos para melhorar legibilidade no primeiro cadastro

### Corrigido
- Tratamento de exceções não observadas no ciclo de vida da aplicação
- Script `run-setup-with-log.ps1` ajustado para fluxo de log com setup Inno por padrão
- Tratamento de exceção global na UI para não mascarar falhas críticas
- Navegador interno ajustado para usar diretório de dados do WebView2 em caminho gravável (fora de `Program Files`)
- Navegação para IP/local sem certificado mantém `http://` e evita promoção automática para `https://`
- Lista de "Materiais & Certificados" passou a ocultar/limpar registros temporários `.tmp` gerados por captura provisória
- Instalador Inno protegido contra falha de pós-instalação com `skipifdoesntexist` ao abrir o app
- Preparação do input do instalador reforçada com retries para limpeza/cópia, reduzindo falhas por arquivos bloqueados
- Validacao de desinstalacao confirmada com remocao de `%LOCALAPPDATA%\\DDSStudyOS`
- Menu lateral no primeiro uso/tour com abertura resiliente em telas iniciais
- Registro de desinstalação do Windows reforçado para aparecer corretamente em Apps e Painel de Controle

## [2.1.0] - 2026-02-22
### Alterado
- Linha de release oficial padronizada para o ciclo 2.1.0 (setup estavel, setup beta, portatil e checksum).

## [0.1.0] - 2026-02-12
### Adicionado
- Projeto WinUI 3 (Windows App SDK) com WebView2
- SQLite com WAL
- CRUD Cursos, Materiais, Agenda
- Backup export/import (JSON) + opção criptografada com Master Password
- Toast Notifications (Action Center) para lembretes (com fallback)
- Organização automática de Downloads + auto-registro no banco
- Aba Desenvolvimento (créditos e links)
