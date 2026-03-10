# Scripts de Backup do Servidor

Este diretorio guarda as versoes canônicas dos scripts usados no servidor geral.

## Arquivos

- `dds-backup.sh`
  - Backup diario/semanal/databases/persistencia docker.
  - Versao atual: `1.2.0`.
  - Comandos extras: `verify`, `restore`, `cleanup`.
  - Gera manifest SHA256 (`.manifest`) para cada arquivo `.tar.gz`.
- `dds-backup-restore-smoke.sh`
  - Teste de restore rapido (integridade de artefatos e leitura de amostras).
  - Se `dds-backup.sh` estiver no host, usa `verify` (inclui checagem de manifest).
- `dds-backup.conf.example`
  - Exemplo de configuracao para `/etc/dds/dds-backup.conf`.

## Deploy no servidor

Exemplo (ajuste host/usuario):

```bash
scp scripts/server/dds-backup.sh kika@192.168.1.10:/home/kika/dds-projetos/dds-backup.sh
scp scripts/server/dds-backup-restore-smoke.sh kika@192.168.1.10:/home/kika/dds-projetos/dds-backup-restore-smoke.sh
ssh kika@192.168.1.10 'sed -i "s/\r$//" /home/kika/dds-projetos/dds-backup.sh /home/kika/dds-projetos/dds-backup-restore-smoke.sh && chmod +x /home/kika/dds-projetos/dds-backup.sh /home/kika/dds-projetos/dds-backup-restore-smoke.sh'
```

## Crontab recomendado

```cron
0 */12 * * * flock -n /tmp/dds-backup-daily.lock /home/kika/dds-projetos/dds-backup.sh daily >/dev/null 2>&1
0 3 * * 0 flock -n /tmp/dds-backup-weekly.lock /home/kika/dds-projetos/dds-backup.sh weekly >/dev/null 2>&1
0 */6 * * * flock -n /tmp/dds-backup-db.lock /home/kika/dds-projetos/dds-backup.sh databases >/dev/null 2>&1
30 4 1 * * flock -n /tmp/dds-restore-smoke.lock /home/kika/dds-projetos/dds-backup-restore-smoke.sh >/dev/null 2>&1
```

## Comandos uteis

```bash
/home/kika/dds-projetos/dds-backup.sh status
/home/kika/dds-projetos/dds-backup.sh verify /mnt/dds-backup/backups-diarios/arquivo.tar.gz
/home/kika/dds-projetos/dds-backup.sh restore /mnt/dds-backup/backups-diarios/arquivo.tar.gz /mnt/dds-backup/restore-test/manual
/home/kika/dds-projetos/dds-backup.sh cleanup
```
