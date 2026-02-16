# DDS StudyOS - WinUI 3 (.NET 8)

Projeto desktop em WinUI 3 para gestão de cursos, materiais, lembretes, navegação e backup local com criptografia opcional.

## Pré-requisitos
- Visual Studio 2022 ou superior
- Workload `NET desktop development`
- Workload `Windows application development`
- .NET 8 SDK

## Abrir e rodar
1. Abra `DDSStudyOS.sln`
2. Aguarde o restore do NuGet
3. Rode em `Debug` (F5) ou `Release`

## Dependências principais
- `Microsoft.WindowsAppSDK` (WinUI 3 + WebView2 control)
- `Microsoft.Data.Sqlite`
- `System.Security.Cryptography.ProtectedData`

## WebView2 Runtime
O app valida o runtime na inicialização. Se faltar, ele exibe diálogo com link direto para o instalador Evergreen oficial.

## Melhorias de pré-release já aplicadas
- Versionamento de assembly e metadata de produto
- Diagnóstico técnico em Configurações (checagens de banco, escrita, WebView2, logs e criptografia)
- Exportação de diagnóstico em `.zip` (relatório + tail de logs)
- Logger com rotação automática de arquivo
- Criptografia de backup reforçada com compatibilidade para backups legados
- Validação de backup sem importar dados
- Materiais com suporte a caminho local, URL e cópia gerenciada interna
- Import de backup preservando vínculo de cursos em materiais e lembretes
- Lembretes com estado de notificação persistido para recuperar pendências ao reabrir

## Empacotamento
Consulte `docs/MSIX_CICD.md` para estratégia de MSIX/CI e checklist de assinatura.

## Documentação (GitHub)
- Guia de release e empacotamento: `docs/MSIX_CICD.md`
- Informações de atualização (feeds/links): `docs/UPDATE_INFO.md`
- Changelog técnico: `CHANGELOG.md`
- Suporte: `SUPPORT.md`

## Links de publicação
- `Update Info URL`: publicar `docs/UPDATE_INFO.md` no GitHub e usar o link no Advanced Installer.
- `Support URL`: usar `SUPPORT.md` publicado no GitHub.
- `Release Notes URL`: usar `CHANGELOG.md` publicado no GitHub.

## Scripts úteis
- `scripts/build-release.ps1`: build + publish usando MSBuild do Visual Studio (via `vswhere`)
- `scripts/sign-release.ps1`: assinatura de artefatos com `.pfx` ou certificado do store por thumbprint
- `scripts/install-internal-cert.ps1`: instalação robusta do certificado interno
- `scripts/Instalar_DDS.bat`: launcher simples para instalação em ambiente interno
- `scripts/publish-github.ps1`: cria/publica repositório no GitHub e atualiza links de suporte/update

## Observação de release
Após cada `publish`, assine novamente o executável com `scripts/sign-release.ps1`, pois o arquivo é recriado durante o publish.
