# Auditoria do Autobackup do Servidor (2026-03-10)

## Escopo

- Verificar se o autobackup do servidor geral esta ativo.
- Validar agenda (cron/systemd), destino dos backups, status recente e riscos.
- Sem alterar a configuracao de backup nesta rodada.

## Resultado resumido

- `autobackup`: **ativo e executando**
- `destino`: **/mnt/dds-backup** montado e com espaco
- `ultima execucao DB`: **2026-03-10 06:00**
- `ultimo backup diario`: **2026-03-10 00:01**
- `ultimo backup semanal`: **2026-03-08 03:01**
- `status geral`: **funcionando, com ajustes recomendados**

## Evidencias coletadas

### 1) Agendamento ativo (crontab de `kika`)

```cron
0 */12 * * * /home/kika/dds-projetos/dds-backup.sh daily >> /mnt/dds-backup/backup.log 2>&1
0 3 * * 0 /home/kika/dds-projetos/dds-backup.sh weekly >> /mnt/dds-backup/backup.log 2>&1
0 */6 * * * /home/kika/dds-projetos/dds-backup.sh databases >> /mnt/dds-backup/backup.log 2>&1
```

### 2) Script de backup em uso

- Arquivo: `/home/kika/dds-projetos/dds-backup.sh`
- Versao no script: `1.0.0`
- Retencao configurada:
  - diarios: `14`
  - semanais: `8`

### 3) Destino e uso de disco

- Pasta: `/mnt/dds-backup`
- Espaco livre reportado: **~402G**
- Tamanho atual:
  - `backups-diarios`: **7.8G**
  - `backups-semanais`: **1.7G**
  - `databases`: **5.9M**

### 4) Ultimos artefatos

- Diario mais recente:
  - `/mnt/dds-backup/backups-diarios/dds_diario_20260310_000001.tar.gz`
- Semanal mais recente:
  - `/mnt/dds-backup/backups-semanais/dds_semanal_20260308_030001.tar.gz`
- DB mais recente:
  - `/mnt/dds-backup/databases/taskingai_20260310_060001.sql`
  - `/mnt/dds-backup/databases/academiavirtual_20260310_060001.db`

### 5) Logs

- `backup.log` confirma execucoes regulares.
- Erros encontrados no historico recente: apenas tentativas iniciais antigas em `2026-03-01`.

## Pontos de atencao

1. Sobreposicao de agenda

- `daily` roda em `00:00` e `12:00`.
- `databases` roda de 6 em 6 horas, incluindo `00:00` e `12:00`.
- Isso gera execucoes duplicadas de backup de DB nesses horarios.

2. Log duplicado/poluido

- O script grava no `backup.log` internamente e o cron tambem redireciona para o mesmo arquivo.
- Resultado: entradas duplicadas e sequencias ANSI no log.

3. Cobertura de backup incompleta para Docker

- O script cobre SQLite do `ad-app-backend` e dump do Postgres `taskingai-db`.
- Nao ha rotina dedicada para backup de volumes de outros servicos (ex.: `open-webui`, volumes `mailcow-*`, etc.).

4. Sem rotina formal de teste de restauracao

- Existe backup e retencao, mas nao ha evidência de restore test automatizado/periodico.

## Recomendacoes (proxima rodada)

1. Remover sobreposicao de DB

- Opcao A: manter `daily` com DB e mudar `databases` para `6,18`.
- Opacao B: manter `databases` 6/6h e remover DB de `daily`.

2. Padronizar logging

- Escolher uma unica estrategia:
  - script loga no arquivo e cron sem redirecionamento, ou
  - cron redireciona e script nao grava no mesmo arquivo.

3. Cobertura de volumes

- Definir backup para volumes criticos de containers fora do fluxo atual.

4. Restore test

- Agendar teste mensal de restauracao (amostra) e registrar evidência.

## Observacao operacional

Nesta auditoria nao foi aplicada nenhuma mudanca em cron/script de backup.
Foram apenas analise e registro de estado para acao controlada na proxima janela.
