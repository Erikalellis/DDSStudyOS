# Changelog

Todas as mudanças importantes neste projeto serão documentadas neste arquivo.

O formato é baseado em **Keep a Changelog** e o projeto segue **SemVer**.

## [Unreleased]
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
