# DDS StudyOS - Checklist Beta 3.2.2

## Objetivo

Validar o ciclo `Power-Up` em beta sem tocar no canal `stable`.

## Fluxos para testar

1. Abrir o app e confirmar a versao exibida:
   - canal beta
   - `v3.2.2`
2. Ir em `Desenvolvimento` e clicar `Verificar agora`.
   - esperado: sem erro de assinatura no update beta
3. Clicar `Atualizar agora` no canal beta.
   - esperado: download completo e abertura do instalador
4. Abrir o `Dashboard`.
   - esperado: card `Templates do Power-Up` visivel
   - esperado: lista com 3 sugestoes
5. Confirmar que o `stable` nao foi afetado.
   - feed `stable` permanece em `3.2.1`

## Critérios de aceite

- update beta conclui sem erro de assinatura
- instalador beta abre
- card de templates aparece sem quebrar layout do dashboard
- canal `stable` continua intacto
