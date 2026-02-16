# Changelog

Todas as mudanças importantes neste projeto serão documentadas neste arquivo.

O formato é baseado em **Keep a Changelog** e o projeto segue **SemVer**.

## [Unreleased]
### Adicionado
- Serviço de diagnóstico técnico com exportação de bundle `.zip`
- Validação de backup sem importação no painel de Configurações
- Metadata de release (produto, versão e companhia) no projeto

### Alterado
- Criptografia de backup reforçada (formato v2) com compatibilidade para backups legados
- Logger com rotação automática e leitura de tail
- Tela de Configurações com versão dinâmica da aplicação
- Documentação de release/empacotamento atualizada

### Corrigido
- Tratamento de exceções não observadas no ciclo de vida da aplicação

## [0.1.0] - 2026-02-12
### Adicionado
- Projeto WinUI 3 (Windows App SDK) com WebView2
- SQLite com WAL
- CRUD Cursos, Materiais, Agenda
- Backup export/import (JSON) + opção criptografada com Master Password
- Toast Notifications (Action Center) para lembretes (com fallback)
- Organização automática de Downloads + auto-registro no banco
- Aba Desenvolvimento (créditos e links)
