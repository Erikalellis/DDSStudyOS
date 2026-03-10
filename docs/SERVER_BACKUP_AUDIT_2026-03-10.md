# Auditoria e Hardening do Autobackup do Servidor (2026-03-10)

## Escopo

- Validar o estado do backup geral do servidor.
- Aplicar correcoes de agenda/logging/cobertura.
- Executar validacao operacional com restore smoke test.

## Resultado executivo

- `autobackup`: **ativo e corrigido**
- `destino`: **/mnt/dds-backup** montado e com espaco livre (~402G)
- `script`: **atualizado para v1.1.1**
- `status geral`: **saudavel para operacao**, com proximos passos definidos para ambiente multi-projeto

## Mudancas aplicadas nesta rodada

### 1) Agenda de backup corrigida (remove sobreposicao de DB)

Foi aplicada a **Opcao B**:

- `daily` agora nao executa DB.
- `databases` segue em `*/6h`.

Crontab ativo de `kika`:

```cron
0 */12 * * * flock -n /tmp/dds-backup-daily.lock /home/kika/dds-projetos/dds-backup.sh daily >/dev/null 2>&1
0 3 * * 0 flock -n /tmp/dds-backup-weekly.lock /home/kika/dds-projetos/dds-backup.sh weekly >/dev/null 2>&1
0 */6 * * * flock -n /tmp/dds-backup-db.lock /home/kika/dds-projetos/dds-backup.sh databases >/dev/null 2>&1
30 4 1 * * flock -n /tmp/dds-restore-smoke.lock /home/kika/dds-projetos/dds-backup-restore-smoke.sh >/dev/null 2>&1
```

### 2) Logging padronizado

- Estrategia aplicada: **script registra no arquivo** (`/mnt/dds-backup/backup.log`).
- `cron` executa com `>/dev/null 2>&1` para evitar duplicidade/poluicao de log.

### 3) Cobertura de persistencia Docker expandida

`backup_volumes` reforcado para cobrir dados criticos e executar `tar` com `sudo` (necessario para `/var/lib/docker/volumes/*`).

Cobertura atual (conforme mounts em execucao):

- `open-webui` (`/var/lib/docker/volumes/open-webui/_data`)
- `taskingai` (`postgres` e `redis`)
- `filebrowser` (`filebrowser.db`, `settings.json`)
- `ddsstudyos-portal` (`data-protection`)
- `ad-app-backend` bind relevante sob `dds-projetos`

Evidencia do novo snapshot de persistencia:

- arquivo: `/mnt/dds-backup/volumes/docker_persistencia_20260310_095551.tar.gz`
- tamanho: **6.8M**
- entradas: **1361**

### 4) Restore test automatizado

Script criado e instalado:

- `/home/kika/dds-projetos/dds-backup-restore-smoke.sh`

Valida:

- integridade `tar` do diario e semanal
- integridade SQLite (`PRAGMA integrity_check`)
- cabecalho de dump PostgreSQL
- legibilidade do ultimo snapshot de persistencia Docker

Status da ultima execucao manual:

- `RESTORE SMOKE RESULT: SUCCESS` em `2026-03-10 09:56:58`

## Evidencias operacionais

### Script em uso

- Arquivo: `/home/kika/dds-projetos/dds-backup.sh`
- Versao: `1.1.1`
- Copia versionada no repositorio:
  - `scripts/server/dds-backup.sh`
  - `scripts/server/dds-backup-restore-smoke.sh`
- Retencao:
  - diarios: `14`
  - semanais: `8`
  - persistencia docker: `8`

### Artefatos recentes

- Diario:
  - `/mnt/dds-backup/backups-diarios/dds_diario_20260310_000001.tar.gz`
- Semanal:
  - `/mnt/dds-backup/backups-semanais/dds_semanal_20260308_030001.tar.gz`
- DB:
  - `/mnt/dds-backup/databases/taskingai_20260310_060001.sql`
  - `/mnt/dds-backup/databases/academiavirtual_20260310_060001.db`
- Volumes:
  - `/mnt/dds-backup/volumes/docker_persistencia_20260310_095551.tar.gz`

### Logs

- backup: `/mnt/dds-backup/backup.log`
- restore smoke: `/mnt/dds-backup/restore-test.log`

## Recomendacoes extras (ambiente com varios projetos)

1. Tier de criticidade por projeto

- Definir `Tier-0` (dados de negocio), `Tier-1` (servicos de apoio), `Tier-2` (cache/derivados).
- Aplicar RPO/RTO diferentes por tier para evitar overbackup.

2. Catalogo de backup por servico

- Manter um inventario versionado com:
  - servico
  - caminho persistente
  - frequencia
  - ultimo restore validado

3. Copia offsite

- Replicar `/mnt/dds-backup` para destino externo (S3/Backblaze/segundo host) com criptografia.
- Sem offsite, risco permanece alto para perda fisica do host.

4. Restore drill trimestral completo

- Alem do smoke mensal, executar restore real de amostra de cada Tier em ambiente isolado.

5. Alertas operacionais

- Adicionar alerta (Telegram/Discord/email) para:
  - falha de cron
  - backup sem novo arquivo dentro da janela esperada
  - restore smoke com erro

## Conclusao

A rodada de hardening foi concluida com sucesso e removeu os riscos principais apontados na auditoria inicial:

- sobreposicao de agenda
- log duplicado
- cobertura fraca de persistencia Docker
- ausencia de rotina de restore periodico

O sistema de backup agora esta mais consistente para operacao diaria e pronto para evolucao multi-projeto com tierizacao e offsite.
