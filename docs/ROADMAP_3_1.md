# DDS StudyOS - Roadmap Pós 3.0 (3.1.x)

Este roadmap organiza as próximas melhorias após o fechamento da versão `3.0.0`.

Status atual: `3.2.7` promovido no canal `stable` em 2026-03-05 com o ciclo `Signal Boost` consolidado.
Status beta atual: `3.2.7-beta.1` validado e usado como base da promocao do stable.
Status tecnico 3.2.7 (base fechada): onboarding em 4 etapas, menu lateral em `ListView`, titulo fixo `Deep Darkness Studios : StudyOS`, Pomodoro sem sobrescrever cabecalho, links publicos alinhados com `DDSStudyOS-Updates`, `study-templates`, `browser-presets`, `notification-pack`, `community-feed` e limpeza de `Materiais`.
Ultimo release publico consolidado: `3.2.7` (estavel para rollout geral no canal publico `DDSStudyOS-Updates`).
Evidencias atuais do fechamento local: build/testes de 2026-03-05 e artefatos em `artifacts/installer-output/` (`DDSStudyOS-Setup.exe`, `DDSStudyOS-Beta-Setup.exe`, `DDSStudyOS-Portable.zip`, `DDSStudyOS-SHA256.txt`).

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

## Linha oficial de DLCs (ciclos incrementais)

### 3.2.1 - Checkpoint (DLC)

- [x] Atualizar o modulo web-content com nova home interna, paginas dds:// refinadas e 404 personalizada.
- [x] Publicar os modulos onboarding-content e branding-assets como base de polimento visual e fluxo inicial.
- [x] Validar manifestos DLC stable e beta com rollback funcional antes do proximo setup completo.

Status atual: infraestrutura de distribuicao publica preparada em `Erikalellis/DDSStudyOS-Updates`, com manifests `stable/beta` validados e bridge publica absorvida pelo stable `3.2.7`.

### 3.2.2 - Power-Up (DLC)

- [x] Fechar hotfix de links publicos e consolidar a troca para `DDSStudyOS-Updates` no app antes da entrega funcional do pack.
- [x] Entregar study-templates com modelos de rotina e trilhas de estudo.
- [x] Entregar pomodoro-presets com presets extras por perfil (foco, revisao, pratica e prova).
- [x] Publicar help-center com guias e resumo de changelog dentro do app.

Status atual: ciclo `Power-Up` fechado no codigo e empacotado em `3.2.5-beta.1` para validacao incremental no canal beta.

### 3.2.3 - Signal Boost (DLC)

- [x] Publicar browser-presets com atalhos e favoritos iniciais.
- [x] Publicar notification-pack com mensagens, presets de lembrete e snooze.
- [x] Publicar community-feed para mostrar proximas metas, novidades e comunicados dentro do app.

Status atual: ciclo `Signal Boost` fechado no codigo (`browser-presets`, `notification-pack` e `community-feed`) e promovido para o `stable 3.2.7`.

### 3.3.0 - Quest Hub (codename: Phoebe)

- [ ] Consolidar nova leva de recursos que exigem mudancas no core.
- [ ] Avaliar a aba de exploracao/catalogo beta de cursos como expansao de produto.
- [ ] Fechar um novo setup completo apenas apos estabilizar a linha 3.2.x de DLCs.

Status atual: fase de definicao da linha `3.3.x (Phoebe)` iniciada apos o fechamento estavel de `3.2.7`.
Progresso tecnico inicial: aba `Loja` integrada ao shell, rota `store` ativa e protocolo externo `ddsstudyos://` habilitado no app e no instalador.
Progresso foundation atual: catalogo remoto com fallback local/interno implementado na `Loja`, snapshot de diagnostico adicionado, portal ASP.NET Core dedicado criado com home publica + `/api/catalog` + `/healthz` e baseline pronto para ligacao futura com servidor proprio isolado do portal ja existente.

## Definicao de cada secao (escopo operacional)

### 3.2.1 - Checkpoint (DLC)

- Funcao da secao: estabelecer base tecnica e visual para a linha de DLCs sem alterar o core.
- Entregaveis principais: `web-content`, `onboarding-content`, `branding-assets` e validacao de manifestos com rollback.
- Criterio de pronto: feeds `stable/beta` validos, downloads de modulos funcionais e fallback de rollback testado.

### 3.2.2 - Power-Up (DLC)

- Funcao da secao: adicionar ganho de produtividade sem reinstalacao completa.
- Entregaveis principais: `study-templates`, `pomodoro-presets` e `help-center`.
- Criterio de pronto: modulos carregando no app com fallback local, update incremental funcionando no canal beta e sem regressao critica.

### 3.2.3 - Signal Boost (DLC)

- Funcao da secao: ampliar descoberta e comunicacao dentro do app.
- Entregaveis principais: `browser-presets`, `notification-pack` e `community-feed`.
- Criterio de pronto: conteudo dinamico exibido em runtime, links validos e promocao para `stable` apos validacao beta.

### 3.3.0 - Quest Hub (codename: Phoebe)

- Funcao da secao: consolidar evolucoes que exigem mudancas no core e preparar a proxima fase de expansao do produto.
- Entregaveis principais: arquitetura da aba de exploracao/catalogo, integracoes de modulo por dominio e novo setup completo.
- Criterio de pronto: estabilidade mantida nos fluxos criticos, rollout reproduzivel e pacote oficial `3.3.0` publicado.

## Plano de execucao - 3.3.x (Phoebe)

### 3.3.0 - Foundation (core + loja beta)

- [ ] Fechar modelo de dados do catalogo (`CourseCatalogItem`, categoria, nivel, tipo, preco e origem).
- [x] Integrar `Loja` com feed remoto configuravel (fallback local quando offline).
- [x] Implementar abertura por deep link para contexto de catalogo (`ddsstudyos://store/...`).
- [x] Adicionar rastreio de falhas de catalogo no diagnostico local.
- [ ] Publicar build beta com smoke dedicado de loja/catalogo.

Critério de aceite:

- App abre sem regressao nos fluxos atuais.
- Loja renderiza feed remoto e fallback local.
- Deep link abre rota correta sem crash.

### 3.3.1 - Modules (dominios de estudo)

- [ ] Entregar modulo `Tecnologia` (links, trilhas base e conteudos recomendados).
- [ ] Entregar modulo `Musica` (links, trilhas base e conteudos recomendados).
- [ ] Implementar mecanismo de pacotes de modulo no formato DLC para evitar inflar o setup base.
- [ ] Adicionar controle de versao por modulo no manifest.

Critério de aceite:

- Instalacao base continua leve.
- Modulos podem ser baixados/aplicados sem reinstalar o app.
- Rollback de modulo validado no canal beta.

### 3.3.2 - Commerce Bridge (preparacao servidor)

- [ ] Definir contrato HTTP minimo entre app e servidor (catalogo, detalhe, disponibilidade e link de aquisicao).
- [ ] Implementar fluxo de ida para web externa e retorno para app por protocolo.
- [ ] Adicionar validacao de origem para links externos antes da abertura.
- [ ] Publicar guia tecnico de integracao para o servidor.

Critério de aceite:

- Fluxo web -> app funciona em ambiente real.
- Sem redirecionamentos nao autorizados.
- Logs suficientes para suporte em campo.

### 3.3.3 - Release Candidate (estabilidade)

- [ ] Rodar regressao completa (build, testes, smoke first-use, smoke clean-machine, update stable/beta, DLC).
- [ ] Congelar schema de manifest para `3.3.x`.
- [ ] Gerar setup completo e pacote portable da linha `Phoebe`.
- [ ] Publicar evidencias e checklist final de promocao.

Critério de aceite:

- Zero bug critico aberto.
- Suite de testes/smoke 100% verde.
- Artefatos assinados e publicados nos canais corretos.

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

1. Navegador interno

- Sincronizar favoritos por perfil com export/import.
- Histórico de navegação por perfil com limpeza rápida.
- Melhorar modo leitura para aulas longas (UI mais limpa, foco).

1. Materiais e certificados

- Detecção automática de arquivo temporário e limpeza em segundo plano.
- Tags e filtro avançado por tipo/curso/data.
- Ação de "abrir pasta do material" com fallback quando caminho estiver indisponível.

## Prioridades (60 dias)

1. Atualização e release

- Canal beta com rollout progressivo e rollback rápido.
- Verificação de update assinada (hash + assinatura do manifesto).
- Script único de release "one click" com validação de gate.

1. Produtividade de estudo

- Metas semanais (não só diárias) e relatório de consistência.
- Pomodoro por perfil com presets (foco profundo, revisão, prática).
- Agenda com lembretes recorrentes e snooze configurável.

1. Segurança e dados

- Auditoria de backups (data, tamanho, origem, validade).
- Verificação de integridade do banco antes de restore.
- Hardening de criptografia com rotação opcional da senha mestra.

## Prioridades (90 dias)

1. Expansão de produto

- Dashboard com insights de progresso por área de estudo.
- Templates de trilhas (frontend, backend, dados, segurança).
- Integração opcional com calendário externo (Google/Microsoft).

1. Operação

- Telemetria opt-in anonimizada para crash e performance.
- Painel interno de saúde da aplicação (runtime, DB, update channel).
- Playbook de suporte com respostas padrão por incidente.

## Critérios de saída do ciclo 3.2

- Zero bug crítico aberto por 2 ciclos de regressão consecutivos.
- Build/release 100% reproduzível com artefatos assinados.
- Cobertura mínima dos fluxos críticos (onboarding, navegador, backup, instalação, desinstalação).
