# DDS StudyOS - Checklist de Corte para Repositorio Privado

Este checklist define o corte final para tornar `Erikalellis/DDSStudyOS` privado sem quebrar usuarios em campo.

Use este documento somente **depois** da ponte `3.2.1` estar em producao durante a janela de adocao.

---

## Objetivo

Encerrar o uso publico do repositrio de codigo:

- `Erikalellis/DDSStudyOS`

e manter o fluxo de updates e DLCs apenas no canal publico:

- `Erikalellis/DDSStudyOS-Updates`

---

## Pre-condicoes obrigatorias

Antes de privatizar `Erikalellis/DDSStudyOS`, tudo abaixo precisa estar verdadeiro:

1. A ponte `3.2.1` ficou em campo por pelo menos `7 dias` (ideal: `14 dias`).
2. `.\scripts\verify-bridge-release.ps1` passa sem falhas.
3. Os feeds do repositrio publico novo estao respondendo `200`.
4. Os assets de `3.2.1` existem no repo `DDSStudyOS-Updates`.
5. Os testers confirmaram que:
   - clientes `3.2.0` conseguiram migrar para `3.2.1`
   - clientes `3.2.1` continuam recebendo update e DLC normalmente
6. Nao existe incidente aberto de update, DLC, hash, 404 ou rollback.

Se qualquer item acima falhar, **nao** privatizar.

---

## Validacao tecnica imediata

Rode no repo de codigo:

```powershell
.\scripts\verify-bridge-release.ps1
```

Esperado:

- validacao `Legacy*`: OK
- validacao `Public*`: OK
- assets principais com `HTTP 200`
- modulos DLC nos dois canais com URL correta

Depois disso, valide manualmente:

1. instalar `3.2.0`
2. detectar update para `3.2.1`
3. atualizar
4. abrir `3.2.1`
5. confirmar que o app passa a consultar o canal novo

---

## Passo a passo de corte

## Fase 1 - Congelar o legado

1. Nao publicar novas mudancas em `Erikalellis/DDSStudyOS` como canal de distribuicao.
2. Garantir que todo material novo (`3.2.2+`) sera publicado apenas em `Erikalellis/DDSStudyOS-Updates`.
3. Manter a tag `v3.2.1` integra nos dois repositorios.

## Fase 2 - Verificacao final

1. Confirmar novamente:

```powershell
.\scripts\verify-bridge-release.ps1
```

2. Verificar manualmente no navegador:
- `https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/stable/update-info.json`
- `https://raw.githubusercontent.com/Erikalellis/DDSStudyOS-Updates/main/installer/update/beta/update-info.json`

3. Confirmar que os links de download do repo publico respondem:
- `releases/latest/download/DDSStudyOS-Setup.exe`
- `releases/download/v3.2.1/DDSStudyOS-Beta-Setup.exe`

## Fase 3 - Tornar o repo de codigo privado

Fazer via GitHub:

1. Abrir:
   - `https://github.com/Erikalellis/DDSStudyOS/settings`
2. Ir em:
   - `General`
   - `Danger Zone`
3. Alterar visibilidade:
   - `Change repository visibility`
   - `Make private`

Nao fazer isso antes de confirmar a Fase 2.

## Fase 4 - Pos-corte imediato

Depois de privatizar:

1. Rodar novamente:

```powershell
.\scripts\verify-bridge-release.ps1
```

2. Confirmar que:
- o canal publico `DDSStudyOS-Updates` continua OK
- o app `3.2.1+` continua baixando manifests do repo publico

3. Registrar no roadmap/manual que o corte foi concluido.

---

## O que muda depois do corte

Depois que `Erikalellis/DDSStudyOS` ficar privado:

1. o repo de codigo deixa de ser usado como canal de update
2. `DDSStudyOS-Updates` vira o unico canal publico oficial
3. releases `3.2.2+` passam a ser publicadas apenas em:
   - `Erikalellis/DDSStudyOS-Updates`

---

## Regra operacional para o proximo ciclo

Para `3.2.2` e seguintes:

1. buildar no repo privado `DDSStudyOS`
2. publicar assets e manifests apenas em `DDSStudyOS-Updates`
3. validar com `verify-bridge-release.ps1` ou um verificador equivalente do novo ciclo

---

## Nao fazer

Nao execute nenhum destes passos antes da validacao:

1. tornar `DDSStudyOS` privado imediatamente
2. apagar releases antigas do repo legado
3. remover manifests legados antes da janela de adocao terminar
4. mudar schema de `update-info.json` ou `dlc-manifest.json` durante o corte

Esses erros quebram clientes em campo.
