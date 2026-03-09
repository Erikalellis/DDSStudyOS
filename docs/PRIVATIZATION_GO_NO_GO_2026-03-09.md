# DDS StudyOS - Go/No-Go para Privatizacao do Repositorio Principal

Data: 2026-03-09

Objetivo: permitir que `Erikalellis/DDSStudyOS` vire privado sem quebrar distribuicao publica de app e DLC.

## Status atual

- `AppUpdateService` consulta feed por `UpdateDistributionConfig` apontando para `Erikalellis/DDSStudyOS-Updates`.
- `DlcUpdateService` consulta manifestos por `UpdateDistributionConfig` apontando para `Erikalellis/DDSStudyOS-Updates`.
- `installer/update/stable/update-info.json` aponta para `DDSStudyOS-Updates`.
- `installer/update/beta/update-info.json` aponta para `DDSStudyOS-Updates`.
- `installer/update/stable/dlc-manifest.json` aponta para assets do `DDSStudyOS-Updates`.
- `installer/update/beta/dlc-manifest.json` aponta para assets do `DDSStudyOS-Updates`.
- Portal publico (`/studyos`) revisado para usar somente links publicos.

## Checklist de bloqueio (obrigatorio)

1. Validar update `stable` em maquina limpa:
   - checar update
   - baixar instalador
   - executar instalacao
2. Validar update `beta` em maquina limpa:
   - checar update
   - baixar instalador
   - executar instalacao
3. Validar DLC `stable`:
   - checar manifesto
   - baixar modulo
   - aplicar modulo
4. Validar DLC `beta`:
   - checar manifesto
   - baixar modulo
   - aplicar modulo
5. Confirmar respostas HTTP 200:
   - `.../installer/update/stable/update-info.json`
   - `.../installer/update/beta/update-info.json`
   - `.../installer/update/stable/dlc-manifest.json`
   - `.../installer/update/beta/dlc-manifest.json`
6. Confirmar que links publicos do portal nao usam `Erikalellis/DDSStudyOS`.

## Decisao

- GO: todos os itens de bloqueio concluidos.
- NO-GO: qualquer falha em update/download/aplicacao de DLC.

## Acao apos GO

1. Tornar `Erikalellis/DDSStudyOS` privado.
2. Manter `Erikalellis/DDSStudyOS-Updates` publico.
3. Publicar nota curta no portal e no canal de updates:
   - codigo-fonte privado
   - distribuicao publica mantida
   - changelog e roadmap continuam publicos.
