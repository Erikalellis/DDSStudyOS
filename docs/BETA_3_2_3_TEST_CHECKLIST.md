# DDS StudyOS - Checklist Beta 3.2.3

## Objetivo

Validar o corte tecnico `3.2.3-beta.1` no canal `beta`, destravando o update in-app para quem ja esta em `3.2.2` sem tocar no canal `stable`.

## Fluxos para testar

1. Abrir um cliente em `3.2.2` no canal `beta`.
   - esperado: versao exibida `v3.2.2`
2. Ir em `Desenvolvimento` e clicar `Verificar agora`.
   - esperado: o app detectar uma versao mais nova no canal `beta`
3. Clicar `Atualizar agora`.
   - esperado: download completo e abertura do instalador `3.2.3-beta.1`
4. Concluir a instalacao e reabrir o app.
   - esperado: versao exibida `v3.2.3`
5. Abrir o `Dashboard`.
   - esperado: card `Templates do Power-Up` visivel
   - esperado: lista com 3 sugestoes
6. Confirmar que o `stable` nao foi afetado.
   - esperado: feed `stable` permanece em `3.2.1`

## Critérios de aceite

- update beta e detectado em clientes `3.2.2`
- instalador beta abre e conclui sem erro de assinatura
- apos atualizar, o app mostra `v3.2.3`
- card de templates aparece sem quebrar o layout do dashboard
- canal `stable` continua intacto
