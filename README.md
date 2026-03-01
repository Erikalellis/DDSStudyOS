# DDS StudyOS - WinUI 3 (.NET 8)

Projeto desktop em WinUI 3 para gestão de cursos, materiais, lembretes, navegação e backup local criptografado.

## Status do produto
- Build estável validada localmente: `3.2.0`
- Próximo ciclo incremental (DLC) em preparação: `3.2.1`
- Última tag enviada ao repositório: `v3.2.0`
- Último release GitHub consolidado: `3.2.0`
- Página de releases: `https://github.com/Erikalellis/DDSStudyOS/releases`

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
Fluxo oficial de instalador: `docs/INNO_SETUP.md` (Inno Setup).

Guia legado/backup: `docs/ADVANCED_INSTALLER_SETUP.md`.

## Documentação (GitHub)
- Manual completo de canais e atualizacoes (iniciante -> senior): `docs/MANUAL_CANAIS_E_ATUALIZACOES.md`
- Guia rápido de uso: `docs/USER_GUIDE.md`
- Guia oficial do instalador (Inno Setup): `docs/INNO_SETUP.md`
- Guia legado (Advanced Installer): `docs/ADVANCED_INSTALLER_SETUP.md`
- Projeto legado do instalador (Advanced Installer): `installer/advanced-installer/README.md`
- Checklist oficial de regressão beta: `docs/BETA_REGRESSION_CHECKLIST.md`
- Gate de promoção para versão 3.0: `docs/RELEASE_3_0_GATE.md`
- Guia de release e empacotamento: `docs/MSIX_CICD.md`
- Playbook one-click de release + gate automático: `docs/RELEASE_ONE_CLICK_PLAYBOOK.md`
- Arquivos legais do instalador: `installer/legal/`
- Informações de atualização (feeds/links): `docs/UPDATE_INFO.md`
- Changelog técnico: `CHANGELOG.md`
- Suporte: `SUPPORT.md`

## Links de publicação
- `Update Info URL`: publicar `docs/UPDATE_INFO.md` no GitHub e usar no release/instalador oficial.
- `Support URL`: usar `SUPPORT.md` publicado no GitHub.
- `Release Notes URL`: usar `CHANGELOG.md` publicado no GitHub.

## Scripts úteis
- `scripts/build-inno-installer.ps1`: fluxo oficial de build do setup (`DDSStudyOS-Setup.exe`)
- `scripts/build-release-package.ps1`: gera pacote completo de release (setup estavel + setup beta + portatil + SHA256 + sync de `update-info.json`)
- `scripts/release-one-click.ps1`: executa release completa (setup + dlc + evidências + gate automático GO/FAIL)
- `scripts/run-setup-with-log.ps1`: executa setup com log de instalação para diagnóstico
- `scripts/build-release.ps1`: build + publish usando MSBuild do Visual Studio (via `vswhere`)
- `scripts/sign-release.ps1`: assinatura de artefatos com `.pfx` ou certificado do store por thumbprint
- `scripts/install-internal-cert.ps1`: instalação robusta do certificado interno
- `scripts/Instalar_DDS.bat`: launcher simples para instalação em ambiente interno
- `scripts/publish-github.ps1`: cria/publica repositório no GitHub e atualiza links de suporte/update
- `scripts/prepare-installer-input.ps1`: gera pasta de entrada do instalador
- `scripts/build-installer.ps1`: fluxo legado (Advanced Installer)
- `scripts/create-advanced-installer-project.ps1`: fluxo legado para projeto `.aip`

## CI
- Workflow de integração contínua: `.github/workflows/ci.yml`
- Valida restore, build Debug/Release e publish self-contained (`win-x64`) a cada push/PR em `main`.

## Observação de release
Após cada `publish`, assine novamente o executável com `scripts/sign-release.ps1`, pois o arquivo é recriado durante o publish.
