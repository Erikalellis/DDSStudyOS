# DDS StudyOS - Manual de Canais e Atualizacoes (Iniciante -> Senior)

Este manual explica:

- como funcionam os canais `stable` e `beta`;
- onde cada arquivo deve ficar;
- o que publicar em cada release;
- como operar do fluxo mais simples ao mais avancado.

---

## 1) Visao geral da arquitetura

O app usa dois fluxos de atualizacao:

1. Atualizacao do app base (Setup Inno)
- Servico: `AppUpdateService`
- Feed remoto:
  - `installer/update/stable/update-info.json`
  - `installer/update/beta/update-info.json`
- Resultado: baixa `Setup.exe`, valida hash/assinatura e executa instalador.

2. Atualizacao incremental de modulos (DLC)
- Servico: `DlcUpdateService`
- Manifesto remoto:
  - `installer/update/stable/dlc-manifest.json`
  - `installer/update/beta/dlc-manifest.json`
- Resultado: baixa `.zip` por modulo, valida hash SHA256 e aplica no modulo local.

Links usados pelo app em runtime sao `raw.githubusercontent.com` (arquivo bruto), nao a pagina `blob`.

---

## 2) Canais: quando usar cada um

## `stable`
- Para usuarios finais.
- Somente versoes validadas por smoke + gate.
- Sempre apontar para release publica mais recente.

## `beta`
- Para testers e homologacao.
- Pode receber correcao antes do stable.
- Ideal para validar regressao rapida antes de promover para stable.

Regra pratica:
- quebrou fluxo critico no beta -> nao promove.
- beta estavel em regressao -> promove para stable.

---

## 3) Onde cada arquivo deve ficar

## Codigo e scripts
- App: `src/DDSStudyOS.App/`
- Scripts release: `scripts/`

## Feeds de update (versionados no git)
- Stable app feed: `installer/update/stable/update-info.json`
- Beta app feed: `installer/update/beta/update-info.json`
- Stable DLC feed: `installer/update/stable/dlc-manifest.json`
- Beta DLC feed: `installer/update/beta/dlc-manifest.json`

## Artefatos gerados localmente
- Setup/portable/sha: `artifacts/installer-output/`
- DLC zip: `artifacts/dlc-output/`
- Logs de smoke: `artifacts/installer-logs/`
- Gate report: `artifacts/release-gate/`

## Evidencias documentais
- Checklist regressao: `docs/BETA_REGRESSION_CHECKLIST.md`
- Evidencia de update: `docs/UPDATE_SMOKE_EVIDENCE_*.md`
- Changelog: `CHANGELOG.md`
- Rollout da bridge `3.2.1`: `docs/BRIDGE_3_2_1_ROLLOUT.md`
- Corte final para repo privado: `docs/PRIVATE_REPO_CUTOVER_CHECKLIST.md`

---

## 4) O que enviar em cada release

No GitHub Release (tag `vX.Y.Z`) publicar junto:

1. `DDSStudyOS-Setup.exe`
2. `DDSStudyOS-Beta-Setup.exe`
3. `DDSStudyOS-Portable.zip`
4. `DDSStudyOS-SHA256.txt`
5. `DDSStudyOS-DLC-*.zip` (modulos DLC)

E commitar no repositrio:

1. `CHANGELOG.md`
2. `installer/update/*/update-info.json`
3. `installer/update/*/dlc-manifest.json`
4. evidencias em `docs/` e `artifacts/release-gate/`

---

## 5) Fluxo Iniciante (seguro e direto)

Use este fluxo para nao errar.

1. Gerar tudo com 1 comando:

```powershell
.\scripts\release-one-click.ps1 -Owner Erikalellis -Repo DDSStudyOS -ReleaseTag v3.1.3
```

2. Confirmar resultado `GO` no gate:
- `artifacts/release-gate/release-gate-*.md`

3. Commitar:

```powershell
git add .
git commit -m "release: fechar ciclo X.Y.Z"
git push origin main
```

4. Tag + release:

```powershell
git tag -a v3.1.3 -m "Release 3.1.3"
git push origin v3.1.3
gh release create v3.1.3 `
  artifacts/installer-output/DDSStudyOS-Setup.exe `
  artifacts/installer-output/DDSStudyOS-Beta-Setup.exe `
  artifacts/installer-output/DDSStudyOS-Portable.zip `
  artifacts/installer-output/DDSStudyOS-SHA256.txt `
  artifacts/dlc-output/DDSStudyOS-DLC-web-content.zip `
  --repo Erikalellis/DDSStudyOS `
  --title "DDS StudyOS v3.1.3"
```

---

## 6) Fluxo Pleno (controle fino)

Quando voce quer separar etapas:

1. Build release:
- `scripts/build-release-package.ps1`
2. Build DLC:
- `scripts/build-dlc-package.ps1 -Channel stable`
- `scripts/build-dlc-package.ps1 -Channel beta`
3. Smoke:
- `scripts/validate-first-use-smoke.ps1`
- `scripts/validate-clean-machine-smoke.ps1 -RunSetup`
4. Atualizar docs/checklist manualmente.
5. Publicar release.

Use este modo para depurar falha especifica de setup, hash, feed ou smoke.

### Bridge `3.2.1` (codigo -> canal publico)

Quando houver migracao de feed entre repositorios:

1. publicar assets nos dois repositorios
2. manter manifestos legados apontando para o repositorio antigo
3. sincronizar o repo publico novo com os manifestos dele
4. validar com:

```powershell
.\scripts\verify-bridge-release.ps1
```

5. somente depois da janela de adocao tornar o repositorio de codigo privado

---

## 7) Fluxo Senior (operacao e escala)

Para time, CI/CD e operacao continua:

1. Padronizar "gate blocking":
- release so sai com `Gate automatico: GO`.
2. Criar rollback rapido:
- manter tag anterior pronta (ex.: `v3.1.2`) para reverter feed.
3. Separar promocao de canal:
- publicar primeiro `beta`;
- promover para `stable` apos janela de validacao.
4. Garantir rastreabilidade:
- toda release com:
  - changelog;
  - checklist;
  - evidencia smoke;
  - hash SHA256.
5. Automatizar em Actions:
- build + smoke + attach assets;
- travar release se check falhar.

---

## 8) Checklist rapido antes de publicar

1. Versao no `.csproj` atualizada.
2. `CHANGELOG.md` atualizado.
3. `update-info` stable/beta com versao e hash corretos.
4. `dlc-manifest` stable/beta com release tag correta.
5. `Setup/Beta/Portable/SHA256` gerados.
6. Smoke first-use e clean-machine com `OK`.
7. Gate report com `GO`.
8. Tag + release publicados no GitHub.

---

## 9) Erros comuns e correcao rapida

## Erro 404 no beta update
- Causa comum: `downloadUrl` apontando tag errada.
- Ajuste em `installer/update/beta/update-info.json`.

## App diz "sem assinatura valida"
- Causa: arquivo nao assinado com certificado esperado.
- Revisar thumbprint + `scripts/sign-release.ps1`.

## Update baixa mas nao instala
- Verifique log em `%LOCALAPPDATA%/DDSStudyOS/logs`.
- Valide caminho e permissao do instalador.

## DLC nao aplica
- Conferir `sha256` e `downloadUrl` no `dlc-manifest`.
- Conferir se `minimumAppVersion` <= versao atual do app.

---

## 10) Referencias oficiais internas

- `docs/RELEASE_ONE_CLICK_PLAYBOOK.md`
- `docs/UPDATE_INFO.md`
- `docs/BETA_REGRESSION_CHECKLIST.md`
- `docs/ROADMAP_3_1.md`
- `CHANGELOG.md`
