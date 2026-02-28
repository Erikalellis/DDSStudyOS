# DDS StudyOS - Manual Tecnico do App

Este manual resume a arquitetura atual do DDS StudyOS para quem precisa entender, manter ou evoluir o projeto.

Publico-alvo:
- Iniciante tecnico: quer entender a estrutura e onde cada parte mora.
- Intermediario: quer alterar features, dados, update ou release sem quebrar o produto.

## 1. Visao geral

O DDS StudyOS e um app desktop em:
- `WinUI 3`
- `.NET 8`
- `WebView2`
- `SQLite`

Objetivo do produto:
- centralizar cursos, materiais, lembretes, navegador interno, cofre e backup local;
- manter uma base estavel;
- evoluir com updates completos (setup) e incrementais (DLC).

## 2. Estrutura principal

Raiz do app:
- `src/DDSStudyOS.App/`

Pastas mais importantes:
- `Pages/`: telas do produto (`Home`, `Dashboard`, `Cursos`, `Materiais`, `Agenda`, `Browser`, `Development`, `Settings`)
- `Services/`: regra de negocio, dados, release, backup, runtime, update
- `Models/`: modelos de dominio e exportacao
- `Data/schema.sql`: schema base do banco SQLite
- `Assets/`: icones, imagens e recursos visuais

## 3. Ciclo de inicializacao

Entrada principal:
- `src/DDSStudyOS.App/App.xaml.cs`

Fluxo:
1. Configura pasta de dados do WebView2 em area gravavel.
2. Registra handlers globais de excecao.
3. Cria `MainWindow`.
4. Ativa a janela principal.
5. Dispara verificacao de DLC em segundo plano (quando aplicavel).

Observacao:
- o auto-DLC nao roda em modo smoke (`--smoke*`) nem quando `DDS_DISABLE_AUTO_DLC=1`.

## 4. Persistencia e banco local

Servico central:
- `src/DDSStudyOS.App/Services/DatabaseService.cs`

Banco:
- local em `%LOCALAPPDATA%\\DDSStudyOS\\studyos.db`

Pontos importantes:
- `EnsureCreatedAsync()` executa `schema.sql`
- migracoes simples sao feitas via `ALTER TABLE` tolerante
- modo `WAL` habilitado para estabilidade/performance

Tabelas chave:
- `courses`
- `materials`
- `reminders`
- `course_favorites`
- `course_history`
- `study_activity`
- `user_stats`

## 5. Cursos, materiais e agenda

Repositorios:
- `CourseRepository.cs`
- `MaterialRepository.cs`
- `ReminderRepository.cs`

Estado atual:
- cursos suportam favoritos por perfil e historico por perfil;
- materiais suportam filtros e fallback para abrir pasta;
- agenda agora suporta:
  - recorrencia (`none`, `daily`, `weekly`, `monthly`)
  - snooze configuravel
  - conclusao simples ou reagendamento da proxima ocorrencia

## 6. Pomodoro e metas

Configuracoes:
- `SettingsService.cs`
- `SettingsPage.xaml(.cs)`

Pomodoro:
- persistido por perfil
- presets:
  - `foco_profundo`
  - `revisao`
  - `pratica`
  - `custom`

Metas semanais:
- `WeeklyGoalService.cs`
- usa `study_activity` para calcular:
  - dias ativos
  - minutos acumulados
  - score de consistencia

## 7. Cofre e backup

Servicos:
- `CredentialVaultService.cs`
- `BackupService.cs`
- `DpapiProtector.cs`
- `MasterPasswordCrypto.cs`

Regras:
- backup sem senha esta desabilitado
- exportacao exige senha mestra
- restauracao preserva cursos, materiais e lembretes
- lembretes agora preservam:
  - recorrencia
  - snooze

## 8. Navegador interno

Tela:
- `Pages/BrowserPage.xaml(.cs)`

Base:
- `WebView2`

Responsabilidades:
- abrir URL direta
- usar busca por provedor configurado
- renderizar `dds://inicio`
- persistir notas e favoritos do navegador

## 9. Updates e canais

Existem dois fluxos complementares:

### 9.1 Update completo (setup)

Servico:
- `AppUpdateService.cs`

Fonte:
- `installer/update/stable/update-info.json`
- `installer/update/beta/update-info.json`

Fluxo:
1. verifica feed
2. compara versao
3. baixa `Setup.exe`
4. valida `SHA256` + assinatura
5. inicia instalador silencioso

### 9.2 Update incremental (DLC)

Servico:
- `DlcUpdateService.cs`

Fonte:
- `installer/update/stable/dlc-manifest.json`
- `installer/update/beta/dlc-manifest.json`

Fluxo:
1. baixa manifesto
2. compara hash/versao dos modulos
3. baixa `.zip` de modulo alterado
4. valida hash
5. extrai em staging
6. aplica em `%LOCALAPPDATA%\\DDSStudyOS\\modules`
7. faz rollback local se falhar

Estado atual:
- pode ser executado manualmente na aba `Desenvolvimento`
- tambem roda automaticamente no startup, em segundo plano

## 10. Release e distribuicao

Scripts principais:
- `scripts/build-release.ps1`
- `scripts/build-inno-installer.ps1`
- `scripts/build-release-package.ps1`
- `scripts/build-dlc-package.ps1`
- `scripts/release-one-click.ps1`

Artefatos esperados:
- `DDSStudyOS-Setup.exe`
- `DDSStudyOS-Beta-Setup.exe`
- `DDSStudyOS-Portable.zip`
- `DDSStudyOS-SHA256.txt`

Documentos de apoio:
- `docs/INNO_SETUP.md`
- `docs/RELEASE_ONE_CLICK_PLAYBOOK.md`
- `docs/BETA_REGRESSION_CHECKLIST.md`

## 11. Como evoluir sem quebrar

Regras praticas:
1. Mudar schema sempre com migracao tolerante em `DatabaseService`.
2. Se adicionar campo de modelo, refletir tambem em:
   - repositorio
   - backup/export
   - import/restore
3. Para features de update, manter `Setup.exe` como fallback.
4. Antes de release:
   - build
   - smoke de primeiro uso
   - smoke de maquina limpa
   - validar hashes

## 12. Proximos passos naturais

Itens tecnicos mais provaveis para o proximo ciclo:
- rollout progressivo por canal
- assinatura/verificacao mais forte do manifesto DLC
- historico de navegacao por perfil
- auditoria de backup
- telemetria opt-in

## 13. Leitura recomendada

Se voce vai mexer no projeto, leia nesta ordem:
1. `docs/README.md`
2. `docs/ROADMAP_3_1.md`
3. `CHANGELOG.md`
4. `docs/BETA_REGRESSION_CHECKLIST.md`
5. `docs/RELEASE_ONE_CLICK_PLAYBOOK.md`
