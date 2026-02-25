# Evidencia de Smoke de Primeiro Uso - 2026-02-25

## Objetivo
Validar automaticamente o fluxo de primeiro uso do app (`onboarding + tour + navegador`) em execução real.

## Execucao
- Comando:
  `.\scripts\validate-first-use-smoke.ps1 -AppExePath src\DDSStudyOS.App\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\DDSStudyOS.App.exe`
- Relatorio:
  `artifacts/installer-logs/first-use-smoke-20260225-205532.txt`

## Marcadores validados no log
- `SMOKE_FIRST_USE:MODE_ENABLED`
- `SMOKE_FIRST_USE:ONBOARDING_OK`
- `SMOKE_FIRST_USE:TOUR_OK`
- `SMOKE_FIRST_USE:BROWSER_HOME_OK`
- `SMOKE_FIRST_USE:BROWSER_OK`
- `SMOKE_FIRST_USE:SUCCESS`

## Resultado
- Status final: `OK`
- Perfil criado automaticamente: `Smoke Tester`
- Flag de tour no perfil: `HasSeenTour=True`
