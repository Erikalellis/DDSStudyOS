#!/bin/bash
set -Eeuo pipefail
# DDS BACKUP RESTORE SMOKE TEST

BACKUP_DIR="${BACKUP_DIR:-/mnt/dds-backup}"
DAILY_DIR="${DAILY_DIR:-$BACKUP_DIR/backups-diarios}"
WEEKLY_DIR="${WEEKLY_DIR:-$BACKUP_DIR/backups-semanais}"
DB_DIR="${DB_DIR:-$BACKUP_DIR/databases}"
VOLUMES_DIR="${VOLUMES_DIR:-$BACKUP_DIR/volumes}"
LOG_FILE="${LOG_FILE:-$BACKUP_DIR/restore-test.log}"
BACKUP_SCRIPT="${BACKUP_SCRIPT:-/home/kika/dds-projetos/dds-backup.sh}"

log() {
  echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

verify_tar_or_script() {
  local archive="$1"
  if [ -x "$BACKUP_SCRIPT" ]; then
    "$BACKUP_SCRIPT" verify "$archive" >/dev/null
  else
    tar tzf "$archive" >/dev/null
  fi
}

fail=0
log "==== RESTORE SMOKE TEST START ===="

latest_daily="$(ls -1t "$DAILY_DIR"/dds_diario_*.tar.gz 2>/dev/null | head -1 || true)"
if [ -n "$latest_daily" ] && verify_tar_or_script "$latest_daily"; then
  log "OK: daily valido -> $(basename "$latest_daily")"
else
  log "FAIL: daily ausente ou invalido"
  fail=1
fi

latest_weekly="$(ls -1t "$WEEKLY_DIR"/dds_semanal_*.tar.gz 2>/dev/null | head -1 || true)"
if [ -n "$latest_weekly" ] && verify_tar_or_script "$latest_weekly"; then
  log "OK: weekly valido -> $(basename "$latest_weekly")"
else
  log "FAIL: weekly ausente ou invalido"
  fail=1
fi

latest_sqlite="$(ls -1t "$DB_DIR"/academiavirtual_*.db 2>/dev/null | head -1 || true)"
if [ -n "$latest_sqlite" ] && command -v sqlite3 >/dev/null 2>&1; then
  check="$(sqlite3 "$latest_sqlite" 'PRAGMA integrity_check;' 2>/dev/null | head -1)"
  if [ "$check" = "ok" ]; then
    log "OK: sqlite integridade -> $(basename "$latest_sqlite")"
  else
    log "FAIL: sqlite integridade falhou -> $(basename "$latest_sqlite")"
    fail=1
  fi
else
  log "FAIL: sqlite backup ausente ou sqlite3 indisponivel"
  fail=1
fi

latest_pg="$(ls -1t "$DB_DIR"/taskingai_*.sql 2>/dev/null | head -1 || true)"
if [ -n "$latest_pg" ] && grep -q 'PostgreSQL database cluster dump' "$latest_pg"; then
  log "OK: dump postgres com cabecalho esperado -> $(basename "$latest_pg")"
else
  log "FAIL: dump postgres ausente ou invalido"
  fail=1
fi

latest_vol="$(ls -1t "$VOLUMES_DIR"/docker_persistencia_*.tar.gz 2>/dev/null | head -1 || true)"
if [ -n "$latest_vol" ]; then
  if verify_tar_or_script "$latest_vol"; then
    log "OK: backup de persistencia docker valido -> $(basename "$latest_vol")"
  else
    log "FAIL: backup de persistencia docker invalido -> $(basename "$latest_vol")"
    fail=1
  fi
else
  log "WARN: nenhum backup de persistencia docker encontrado ainda"
fi

if [ "$fail" -eq 0 ]; then
  log "RESTORE SMOKE RESULT: SUCCESS"
  log "==== RESTORE SMOKE TEST END ===="
  exit 0
else
  log "RESTORE SMOKE RESULT: FAIL"
  log "==== RESTORE SMOKE TEST END ===="
  exit 1
fi
