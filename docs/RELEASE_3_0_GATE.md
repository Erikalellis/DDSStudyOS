# DDS StudyOS - Gate Oficial para Versão 3.0

Este documento define o critério de **Go / No-Go** para promover o produto de `2.x` para `3.0.0`.

## Regra de promoção
- Só vira `3.0.0` quando **todos os itens P0** estiverem concluídos.
- Além dos P0, é necessário concluir pelo menos **80% dos itens P1**.
- A regressão beta (`docs/BETA_REGRESSION_CHECKLIST.md`) deve passar em **2 ciclos consecutivos** sem bug crítico.

## P0 - Obrigatório (bloqueia release 3.0)
- [ ] Estabilidade: app não fecha ao abrir/sair/voltar do navegador interno.
- [ ] Navegação: clique em curso abre navegador interno sempre.
- [ ] Navegação: botões de Acesso Rápido executam a ação correta.
- [ ] Onboarding: tela inicial de cadastro abre e salva sem erro.
- [ ] Tour guiado: textos e alvos corretos em todos os passos (incluindo botão Voltar).
- [x] Segurança: export de backup exige senha mestra (mín. 8 chars) e gera `.ddsbackup`.
- [ ] Release: instalador estável abre o app em máquina limpa sem crash.

## P1 - Alta prioridade (fortalece qualidade de 3.0)
- [x] UX: contraste e legibilidade da tela de cadastro revisados.
- [x] Pomodoro: botão de configurações abaixo do card funciona e aplica preferências.
- [x] Navegador: `dds://inicio` estável e páginas internas personalizadas consistentes.
- [x] Favoritos: botão/lista de favoritos com cursos salvos por perfil.
- [x] Desenvolvimento: página beta com histórico de melhorias + link de feedback.
- [x] Desinstalação: limpeza de `%LOCALAPPDATA%\DDSStudyOS` validada com segurança.
- [x] Release pack: `Setup`, `Beta Setup`, `Portable` e `SHA256` publicados juntos.
- [x] Assinatura: artefatos principais assinados antes de release público.

## Status atual (25/02/2026)
- Estado geral: **2.2.0-beta candidato** (ainda não 3.0).
- P0 concluídos: **1/7**.
- P1 concluídos: **8/8**.
- Smoke técnico concluído nesta rodada: build Debug/Release, testes automatizados, publish e geração do pacote (`Setup`, `Beta Setup`, `Portable`, `SHA256`) com validação de instalação silenciosa por log.
- Pendências para gate 3.0: validação funcional completa dos fluxos de navegação/onboarding/tour e validação de instalador em **máquina limpa**.

## Fluxo de decisão
1. Executar checklist de regressão beta.
2. Validar P0 um por um em máquina de desenvolvimento.
3. Validar instalador em máquina limpa (smoke test).
4. Se P0=100% e P1>=80%: promover versão para `3.0.0`.
5. Caso contrário: manter em `2.2.x-beta` e abrir itens pendentes.

## Comandos recomendados (release candidate)
```powershell
dotnet build DDSStudyOS.sln -c Debug -p:Platform=x64
dotnet build DDSStudyOS.sln -c Release -p:Platform=x64
.\scripts\build-release-package.ps1
.\scripts\run-setup-with-log.ps1
```
