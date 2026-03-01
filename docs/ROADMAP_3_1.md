# DDS StudyOS - Roadmap Pós 3.0 (3.1.x)

Este roadmap organiza as próximas melhorias após o fechamento da versão `3.0.0`.

Status atual: `3.2.0` fechado localmente em 2026-02-28 (novo onboarding, shell lateral proprio, branding fixo de janela e base pronta para update incremental).
Status tecnico 3.2.0 (fechado local): onboarding em 4 etapas, menu lateral em `ListView`, titulo fixo `Deep Darkness Studios : StudyOS`, Pomodoro sem sobrescrever cabecalho e build/testes validos.
Ultimo release publico consolidado: `3.2.0` (novo onboarding em 4 etapas, shell lateral proprio, branding fixo de janela e base pronta para DLC 3.2.1).
Evidencias atuais do fechamento local: build/testes de 2026-02-28, `artifacts/installer-logs/first-use-smoke-20260228-091627.txt`, `artifacts/installer-logs/clean-machine-smoke-20260228-100341.txt` e `artifacts/installer-logs/clean-machine-setup-20260228-100341-inno.log`.

## Patch fechado (3.1.2)

- [x] Ajustar UX de onboarding/tour (texto/posicionamento/voltar) em escala 100% e 125%.
- [x] Refinar navegacao de curso -> navegador interno e retorno sem perda de estado.
- [x] Melhorar contraste e legibilidade dos formularios principais.
- [x] Validar mais 1 ciclo de regressao automatizado e anexar evidencias no gate.

## Patch fechado (3.1.3)

- [x] Sincronizar favoritos por perfil com export/import e limpar historico por perfil.
- [x] Adicionar filtros avancados em Materiais (tipo/curso/data) e acao de abrir pasta com fallback.
- [x] Consolidar playbook de release one-click (setup + dlc + evidencias) com gate automatico.

## Patch fechado local (3.1.4)

- [x] Metas semanais (nao so diarias) com relatorio de consistencia.
- [x] Pomodoro por perfil com presets (foco profundo, revisao e pratica).
- [x] Agenda com lembretes recorrentes e snooze configuravel.
- [x] Atualizacao incremental (DLC) em segundo plano no startup, mantendo setup completo como fallback.

## Patch fechado local (3.2.0)

- [x] Substituir o tour inicial por onboarding em 4 etapas com validacao por passo e resumo final.
- [x] Trocar o `NavigationView` por shell lateral proprio para eliminar o layout compacto inconsistente.
- [x] Fixar branding da janela (titulo + icone) e remover o relogio do Pomodoro do cabecalho.

## Proxima entrega incremental (3.2.1 - DLC)

- [ ] Atualizar o modulo `web-content` com novo pacote e hash sincronizado.
- [ ] Publicar manifestos DLC `stable` e `beta` em 3.2.1 com rollback validado.
- [ ] Polir onboarding/branding final antes do proximo setup completo.

## Objetivo do ciclo

- Consolidar estabilidade do 3.0 em produção.
- Evoluir recursos de estudo (agenda, navegador e materiais).
- Melhorar distribuição, atualização e observabilidade.

## Prioridades (30 dias)

1. Qualidade e confiabilidade

- Criar suíte de smoke automatizada para primeiro uso (onboarding + tour + navegador).
  Status atual: versão inicial entregue com `scripts/validate-first-use-smoke.ps1` e modo `--smoke-first-use`.
- Adicionar teste automatizado para visibilidade do desinstalador no Windows.
- Expandir logs de instalação/desinstalação para diagnóstico em campo.

2. Navegador interno

- Sincronizar favoritos por perfil com export/import.
- Histórico de navegação por perfil com limpeza rápida.
- Melhorar modo leitura para aulas longas (UI mais limpa, foco).

3. Materiais e certificados

- Detecção automática de arquivo temporário e limpeza em segundo plano.
- Tags e filtro avançado por tipo/curso/data.
- Ação de "abrir pasta do material" com fallback quando caminho estiver indisponível.

## Prioridades (60 dias)

1. Atualização e release

- Canal beta com rollout progressivo e rollback rápido.
- Verificação de update assinada (hash + assinatura do manifesto).
- Script único de release "one click" com validação de gate.

2. Produtividade de estudo

- Metas semanais (não só diárias) e relatório de consistência.
- Pomodoro por perfil com presets (foco profundo, revisão, prática).
- Agenda com lembretes recorrentes e snooze configurável.

3. Segurança e dados

- Auditoria de backups (data, tamanho, origem, validade).
- Verificação de integridade do banco antes de restore.
- Hardening de criptografia com rotação opcional da senha mestra.

## Prioridades (90 dias)

1. Expansão de produto

- Dashboard com insights de progresso por área de estudo.
- Templates de trilhas (frontend, backend, dados, segurança).
- Integração opcional com calendário externo (Google/Microsoft).

2. Operação

- Telemetria opt-in anonimizada para crash e performance.
- Painel interno de saúde da aplicação (runtime, DB, update channel).
- Playbook de suporte com respostas padrão por incidente.

## Critérios de saída do ciclo 3.2

- Zero bug crítico aberto por 2 ciclos de regressão consecutivos.
- Build/release 100% reproduzível com artefatos assinados.
- Cobertura mínima dos fluxos críticos (onboarding, navegador, backup, instalação, desinstalação).
