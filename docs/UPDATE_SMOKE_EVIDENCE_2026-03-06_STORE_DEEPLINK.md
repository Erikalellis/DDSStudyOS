# Evidência de Smoke - Loja + Deep Link (2026-03-06)

## Escopo

- Validar baseline técnico após inclusão da aba `Loja`.
- Confirmar roteamento interno para `store`.
- Confirmar suporte ao protocolo externo `ddsstudyos://`.
- Confirmar estabilidade geral do build/test em `Release x64`.

## Gate local executado

- `dotnet build DDSStudyOS.sln -c Release -p:Platform=x64` -> OK
- `dotnet test tests/DDSStudyOS.App.Tests/DDSStudyOS.App.Tests.csproj -c Release -p:Platform=x64 --no-build --filter "FullyQualifiedName~DeepLinkServiceTests"` -> OK (`4/4`)
- `dotnet test DDSStudyOS.sln -c Release -p:Platform=x64 --no-build` -> OK (`44/44`)

## Smoke estático (código e instalador)

- `src/DDSStudyOS.App/MainWindow.xaml`
  - `ListViewItem x:Name="NavItemStore" Tag="store"` presente.
  - label `Loja` presente.
- `src/DDSStudyOS.App/MainWindow.xaml.cs`
  - rota `store => typeof(StorePage)` presente.
- `src/DDSStudyOS.App/App.xaml.cs`
  - grava `AppState.PendingNavigationTag = targetTag` no fluxo de ativação por protocolo.
- `src/DDSStudyOS.App/Services/AppState.cs`
  - propriedade `PendingNavigationTag` presente.
- `src/DDSStudyOS.App/Pages/BrowserPage.xaml.cs`
  - alias `dds://loja` presente.
  - normalização `ddsstudyos://` -> `dds://` presente.
- `src/DDSStudyOS.App/Services/SettingsService.cs`
  - chave `StoreCatalogUrl` presente.
- `installer/inno/DDSStudyOS.iss`
  - registro de protocolo `ddsstudyos` presente.
- `installer/inno/DDSStudyOS-Personalizado.iss`
  - registro de protocolo `ddsstudyos` presente.

## Resultado

Smoke técnico de `Loja + deep-link` aprovado. Build e testes completos passando em `Release x64`, com rota interna e protocolo externo corretamente conectados para continuidade do ciclo.
