# DDS StudyOS - Rollout da Bridge 3.2.1

Este documento define a operacao da ponte `3.2.1` entre:

- repositorio de codigo: `Erikalellis/DDSStudyOS`
- canal publico de distribuicao: `Erikalellis/DDSStudyOS-Updates`

## Objetivo

Permitir que clientes `3.2.0` ainda encontrem a atualizacao via canal legado, enquanto os binarios `3.2.1+` passam a consultar o canal publico novo.

## Estado esperado da bridge

## Canal legado (`DDSStudyOS`)
- `installer/update/stable/update-info.json` deve apontar para `DDSStudyOS`
- `installer/update/beta/update-info.json` deve apontar para `DDSStudyOS`
- `installer/update/*/dlc-manifest.json` deve apontar para `DDSStudyOS`
- release `v3.2.1` deve conter:
  - `DDSStudyOS-Setup.exe`
  - `DDSStudyOS-Beta-Setup.exe`
  - `DDSStudyOS-Portable.zip`
  - `DDSStudyOS-SHA256.txt`
  - `DDSStudyOS-DLC-*.zip`

## Canal publico (`DDSStudyOS-Updates`)
- `installer/update/stable/update-info.json` deve apontar para `DDSStudyOS-Updates`
- `installer/update/beta/update-info.json` deve apontar para `DDSStudyOS-Updates`
- `installer/update/*/dlc-manifest.json` deve apontar para `DDSStudyOS-Updates`
- release `v3.2.1` deve conter o mesmo conjunto de assets

## Validacao automatizada

Use:

```powershell
.\scripts\verify-bridge-release.ps1
```

O script valida:

1. versao `stable` e `beta` nos dois repositorios
2. URLs de download do app
3. `appVersion` e `releaseTag` dos manifestos DLC
4. URLs dos modulos DLC
5. resposta HTTP dos principais assets

Se houver qualquer falha, o script sai com erro.

## Janela de adocao

Regra operacional:

1. manter `Erikalellis/DDSStudyOS` publico por `7 a 14 dias`
2. monitorar logs e reports dos testers
3. confirmar que usuarios `3.2.0` conseguem migrar para `3.2.1`
4. somente depois disso tornar `Erikalellis/DDSStudyOS` privado

## Corte definitivo

Depois da janela de adocao:

1. tornar `Erikalellis/DDSStudyOS` privado
2. manter releases e manifests apenas em `Erikalellis/DDSStudyOS-Updates`
3. publicar `3.2.2+` somente no canal publico

## Regra de seguranca

Nao privatizar `Erikalellis/DDSStudyOS` antes da validacao da bridge em campo.

Se isso for feito antes da adocao, clientes `3.2.0` perdem acesso ao feed legado.
