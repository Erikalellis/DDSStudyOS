#!/bin/bash
set -Eeuo pipefail

# ==============================
# DDS BACKUP SYSTEM
# ==============================
# By Deep Darkness Studios
# Backup automatico dos projetos, databases e persistencia Docker

BACKUP_VERSION="1.2.0"
CONFIG_FILE="${DDS_BACKUP_CONFIG:-/etc/dds/dds-backup.conf}"

# Defaults (podem ser sobrescritos via env ou CONFIG_FILE)
BACKUP_DIR="${BACKUP_DIR:-/mnt/dds-backup}"
DAILY_DIR="${DAILY_DIR:-}"
WEEKLY_DIR="${WEEKLY_DIR:-}"
DB_DIR="${DB_DIR:-}"
VOLUMES_DIR="${VOLUMES_DIR:-}"
RESTORE_DIR="${RESTORE_DIR:-}"
DDS_DIR="${DDS_DIR:-$HOME/dds-projetos}"
LOG_FILE="${LOG_FILE:-}"
MOUNT_DEVICE="${MOUNT_DEVICE:-/dev/sdb1}"
MIN_SPACE_KB="${MIN_SPACE_KB:-5242880}"  # 5GB

DAILY_KEEP="${DAILY_KEEP:-14}"      # 14 dias de backups diarios
WEEKLY_KEEP="${WEEKLY_KEEP:-8}"     # 8 semanas de backups semanais
VOLUMES_KEEP="${VOLUMES_KEEP:-8}"   # 8 snapshots de persistencia Docker
DB_KEEP_DAYS="${DB_KEEP_DAYS:-14}"  # 14 dias para dumps/bases
MANIFEST_EXT=".manifest"

# Cores
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Timestamp
NOW="$(date +%Y%m%d_%H%M%S)"

load_config() {
    if [ -f "$CONFIG_FILE" ]; then
        # shellcheck disable=SC1090
        source "$CONFIG_FILE"
    fi
}

hydrate_paths() {
    DAILY_DIR="${DAILY_DIR:-$BACKUP_DIR/backups-diarios}"
    WEEKLY_DIR="${WEEKLY_DIR:-$BACKUP_DIR/backups-semanais}"
    DB_DIR="${DB_DIR:-$BACKUP_DIR/databases}"
    VOLUMES_DIR="${VOLUMES_DIR:-$BACKUP_DIR/volumes}"
    RESTORE_DIR="${RESTORE_DIR:-$BACKUP_DIR/restore-test}"
    LOG_FILE="${LOG_FILE:-$BACKUP_DIR/backup.log}"
}

ensure_dirs() {
    mkdir -p "$DAILY_DIR" "$WEEKLY_DIR" "$DB_DIR" "$VOLUMES_DIR" "$(dirname "$LOG_FILE")"
}

log() {
    local msg="[$(date '+%Y-%m-%d %H:%M:%S')] $1"
    mkdir -p "$(dirname "$LOG_FILE")" 2>/dev/null || true
    echo "$msg" >> "$LOG_FILE"
    echo -e "${GREEN}$msg${NC}"
}

warn() {
    local msg="[$(date '+%Y-%m-%d %H:%M:%S')] AVISO: $1"
    mkdir -p "$(dirname "$LOG_FILE")" 2>/dev/null || true
    echo "$msg" >> "$LOG_FILE"
    echo -e "${YELLOW}$msg${NC}"
}

error() {
    local msg="[$(date '+%Y-%m-%d %H:%M:%S')] ERRO: $1"
    mkdir -p "$(dirname "$LOG_FILE")" 2>/dev/null || true
    echo "$msg" >> "$LOG_FILE"
    echo -e "${RED}$msg${NC}" >&2
}

die() {
    error "$1"
    exit 1
}

on_error() {
    local line="$1"
    error "Falha inesperada na linha $line."
}

trap 'on_error $LINENO' ERR

need_cmd() {
    command -v "$1" >/dev/null 2>&1 || die "Comando obrigatorio nao encontrado: $1"
}

ensure_dependencies() {
    need_cmd tar
    need_cmd sha256sum
    need_cmd find
    need_cmd df
}

# Verificar se o HD de backup esta montado
check_backup_drive() {
    if ! mountpoint -q "$BACKUP_DIR" 2>/dev/null; then
        warn "Ponto de backup nao montado em $BACKUP_DIR. Tentando montar $MOUNT_DEVICE..."
        sudo mount "$MOUNT_DEVICE" "$BACKUP_DIR" 2>/dev/null || true
        mountpoint -q "$BACKUP_DIR" 2>/dev/null || die "HD de backup nao esta montado em $BACKUP_DIR."
    fi
    log "HD de backup montado em $BACKUP_DIR"

    mkdir -p "$DAILY_DIR" "$WEEKLY_DIR" "$DB_DIR" "$VOLUMES_DIR"
}

# Verificar espaco disponivel (minimo 5GB)
check_disk_space() {
    local available
    available="$(df --output=avail "$BACKUP_DIR" | tail -1 | xargs)"
    [[ "$available" =~ ^[0-9]+$ ]] || die "Nao foi possivel verificar espaco em disco."
    if [ "$available" -lt "$MIN_SPACE_KB" ]; then
        die "Espaco insuficiente no HD de backup. Disponivel: $(df -h --output=avail "$BACKUP_DIR" | tail -1 | xargs)"
    fi
    log "Espaco disponivel: $(df -h --output=avail "$BACKUP_DIR" | tail -1 | xargs)"
}

write_manifest() {
    local archive="$1"
    local manifest="${archive}${MANIFEST_EXT}"
    {
        echo "version=$BACKUP_VERSION"
        echo "created_at=$(date --iso-8601=seconds)"
        echo "archive=$(basename "$archive")"
        echo "size_bytes=$(stat -c %s "$archive")"
        echo "sha256=$(sha256sum "$archive" | awk '{print $1}')"
    } > "$manifest"
}

verify_archive() {
    local archive="${1:-}"
    [ -n "$archive" ] || die "Uso: verify <arquivo>"
    [ -f "$archive" ] || die "Arquivo nao encontrado: $archive"

    case "$archive" in
        *.tar.gz)
            tar -tzf "$archive" >/dev/null
            ;;
        *.tar.zst)
            command -v zstd >/dev/null 2>&1 || die "zstd nao instalado para validar $archive"
            tar -I zstd -tf "$archive" >/dev/null
            ;;
        *)
            die "Formato nao suportado para validacao: $archive"
            ;;
    esac

    local manifest="${archive}${MANIFEST_EXT}"
    if [ -f "$manifest" ]; then
        local expected actual
        expected="$(awk -F= '/^sha256=/{print $2}' "$manifest" | head -1)"
        actual="$(sha256sum "$archive" | awk '{print $1}')"
        [ -n "$expected" ] || die "Manifest sem SHA256 valido: $manifest"
        [ "$expected" = "$actual" ] || die "SHA256 divergente para $(basename "$archive")"
    else
        warn "Manifest nao encontrado para $(basename "$archive")"
    fi

    log "Verificacao OK: $archive"
}

restore_archive() {
    local archive="${1:-}"
    local target="${2:-$RESTORE_DIR}"
    [ -n "$archive" ] || die "Uso: restore <arquivo> [destino]"
    [ -f "$archive" ] || die "Arquivo nao encontrado: $archive"
    if ! mkdir -p "$target" 2>/dev/null; then
        sudo mkdir -p "$target" 2>/dev/null || die "Nao foi possivel criar destino de restore: $target"
        sudo chown "$(id -un):$(id -gn)" "$target" 2>/dev/null || true
    fi

    verify_archive "$archive"

    case "$archive" in
        *.tar.gz)
            tar -xzf "$archive" -C "$target"
            ;;
        *.tar.zst)
            command -v zstd >/dev/null 2>&1 || die "zstd nao instalado para restore de $archive"
            tar -I zstd -xf "$archive" -C "$target"
            ;;
        *)
            die "Formato nao suportado para restore: $archive"
            ;;
    esac

    log "Restore concluido em: $target"
}

cleanup_old() {
    log "Iniciando limpeza de artefatos auxiliares..."
    find "$DB_DIR" -type f \( -name '*.db' -o -name '*.sql' \) -mtime +"$DB_KEEP_DAYS" -delete 2>/dev/null || true
    find "$WEEKLY_DIR" -type f -name "*.bak" -mtime +90 -delete 2>/dev/null || true
    find "$BACKUP_DIR" -type f -name "*.manifest" -mtime +180 -delete 2>/dev/null || true
    log "Limpeza concluida"
}

backup_databases() {
    log "Iniciando backup das databases..."

    # SQLite - Academia Digital
    local ad_db="$DDS_DIR/ad-app/backend/academiavirtual.db"
    if [ -f "$ad_db" ]; then
        cp "$ad_db" "$DB_DIR/academiavirtual_$NOW.db"
        log "  Database academiavirtual.db copiada"
    else
        warn "  Database local academiavirtual.db nao encontrada em $ad_db"
    fi

    if command -v docker >/dev/null 2>&1; then
        # SQLite via Docker (copia de dentro do container)
        if docker ps --format '{{.Names}}' | grep -q '^ad-app-backend$'; then
            if docker cp ad-app-backend:/app/academiavirtual.db "$DB_DIR/academiavirtual_docker_$NOW.db" 2>/dev/null; then
                log "  Database do container Docker copiada"
            else
                warn "  Falha ao copiar database do container ad-app-backend"
            fi
        fi

        # PostgreSQL (taskingai)
        if docker ps --format '{{.Names}}' | grep -q '^taskingai-db-1$'; then
            if docker exec taskingai-db-1 pg_dumpall -U postgres > "$DB_DIR/taskingai_$NOW.sql" 2>/dev/null; then
                log "  Database TaskingAI exportada"
            else
                warn "  Falha ao exportar Database TaskingAI"
            fi
        fi
    else
        warn "  Docker nao instalado/disponivel; pulando backups de containers"
    fi

    # Manter apenas ultimos N dias de backups de DB
    find "$DB_DIR" -name "*.db" -mtime +"$DB_KEEP_DAYS" -delete 2>/dev/null || true
    find "$DB_DIR" -name "*.sql" -mtime +"$DB_KEEP_DAYS" -delete 2>/dev/null || true

    log "Backup das databases concluido"
}

build_home_targets() {
    local include_bashrc="${1:-0}"
    local targets=("dds-projetos")
    [ -f "$HOME/start.sh" ] && targets+=("start.sh")
    if [ "$include_bashrc" = "1" ] && [ -f "$HOME/.bashrc" ]; then
        targets+=(".bashrc")
    fi
    printf '%s\n' "${targets[@]}"
}

# Backup diario (projetos, sem node_modules/venvs/arquivos grandes)
backup_daily() {
    log "Iniciando backup DIARIO..."

    local backup_file="$DAILY_DIR/dds_diario_$NOW.tar.gz"
    local -a targets=()
    mapfile -t targets < <(build_home_targets 0)

    tar czf "$backup_file" \
        --ignore-failed-read \
        --warning=no-file-changed \
        --exclude='node_modules' \
        --exclude='venv' \
        --exclude='.venv' \
        --exclude='__pycache__' \
        --exclude='.git' \
        --exclude='*.pyc' \
        --exclude='*.db' \
        --exclude='*.sqlite3' \
        --exclude='*.tar.gz' \
        --exclude='*.tar.zst' \
        --exclude='*.zip' \
        --exclude='*.rar' \
        --exclude='*.pem' \
        --exclude='*.key' \
        --exclude='.env' \
        --exclude='rar' \
        --exclude='painel_backup_*' \
        --exclude='backup_*' \
        -C "$HOME" \
        "${targets[@]}" \
        2>/dev/null

    if [ -f "$backup_file" ] && [ -s "$backup_file" ]; then
        local size
        size="$(du -h "$backup_file" | cut -f1)"
        log "Backup diario criado: $backup_file ($size)"
        write_manifest "$backup_file"
        verify_archive "$backup_file"
    else
        die "Falha ao criar backup diario"
    fi

    # Rotacao: manter apenas os ultimos N backups diarios
    local count
    count="$(ls -1 "$DAILY_DIR"/dds_diario_*.tar.gz 2>/dev/null | wc -l)"
    if [ "$count" -gt "$DAILY_KEEP" ]; then
        ls -1t "$DAILY_DIR"/dds_diario_*.tar.gz | tail -n +$((DAILY_KEEP + 1)) | xargs -r rm -f
        log "Backups diarios antigos removidos (mantendo $DAILY_KEEP)"
    fi

    log "Backup diario concluido"
}

# Backup semanal (completo, incluindo configs do sistema)
backup_weekly() {
    log "Iniciando backup SEMANAL (completo)..."

    local backup_file="$WEEKLY_DIR/dds_semanal_$NOW.tar.gz"
    local -a targets=()
    mapfile -t targets < <(build_home_targets 1)

    tar czf "$backup_file" \
        --ignore-failed-read \
        --warning=no-file-changed \
        --exclude='node_modules' \
        --exclude='venv' \
        --exclude='.venv' \
        --exclude='__pycache__' \
        --exclude='*.pyc' \
        --exclude='rar' \
        --exclude='painel_backup_*' \
        --exclude='backup_2025*' \
        -C "$HOME" \
        "${targets[@]}" \
        2>/dev/null

    if [ -f "$backup_file" ] && [ -s "$backup_file" ]; then
        local size
        size="$(du -h "$backup_file" | cut -f1)"
        log "Backup semanal criado: $backup_file ($size)"
        write_manifest "$backup_file"
        verify_archive "$backup_file"
    else
        die "Falha ao criar backup semanal"
    fi

    # Guardar configs do sistema
    sudo cp /etc/fstab "$WEEKLY_DIR/fstab_$NOW.bak" 2>/dev/null || warn "Nao foi possivel copiar /etc/fstab"
    sudo cp /etc/nginx/nginx.conf "$WEEKLY_DIR/nginx_$NOW.bak" 2>/dev/null || warn "Nao foi possivel copiar /etc/nginx/nginx.conf"
    crontab -l > "$WEEKLY_DIR/crontab_$NOW.bak" 2>/dev/null || warn "Crontab vazio/indisponivel para exportar"

    # Rotacao semanal
    local count
    count="$(ls -1 "$WEEKLY_DIR"/dds_semanal_*.tar.gz 2>/dev/null | wc -l)"
    if [ "$count" -gt "$WEEKLY_KEEP" ]; then
        ls -1t "$WEEKLY_DIR"/dds_semanal_*.tar.gz | tail -n +$((WEEKLY_KEEP + 1)) | xargs -r rm -f
        log "Backups semanais antigos removidos (mantendo $WEEKLY_KEEP)"
    fi

    log "Backup semanal concluido"
}

# Backup da persistencia Docker critica (volumes + binds selecionados)
backup_volumes() {
    log "Iniciando backup de persistencia Docker..."

    local backup_file="$VOLUMES_DIR/docker_persistencia_$NOW.tar.gz"
    local tmp_list
    local selected
    local filtered
    tmp_list="$(mktemp)"
    selected="${tmp_list}.selected"
    filtered="${tmp_list}.filtered"

    if ! command -v docker >/dev/null 2>&1; then
        warn "Docker nao disponivel; backup de persistencia nao executado."
        rm -f "$tmp_list" "$selected" "$filtered"
        return 0
    fi

    for c in ddsstudyos-portal open-webui dds-filebrowser taskingai-db-1 taskingai-cache-1 ad-app-backend; do
        docker inspect "$c" --format '{{range .Mounts}}{{println .Source}}{{end}}' 2>/dev/null >> "$tmp_list" || true
    done

    sort -u "$tmp_list" | while read -r src; do
        [ -z "$src" ] && continue
        [ ! -e "$src" ] && continue

        if [[ "$src" == /var/lib/docker/volumes/* ]]; then
            echo "$src"
            continue
        fi

        if [[ "$src" == /home/kika/dds-projetos/taskingai/docker/data/* ]]; then
            echo "$src"
            continue
        fi

        if [[ "$src" == /home/kika/dds-projetos/ddsstudyos-portal/stack/data-protection* ]]; then
            echo "$src"
            continue
        fi

        if [[ "$src" == /home/kika/dds-projetos/filebrowser/* ]]; then
            echo "$src"
            continue
        fi
    done > "$selected"

    if [ ! -s "$selected" ]; then
        warn "Nenhum caminho de persistencia selecionado para backup de volumes."
        rm -f "$tmp_list" "$selected" "$filtered"
        return 0
    fi

    # Converte para caminhos relativos mantendo a estrutura original.
    # Exemplo: /var/lib/docker/volumes/open-webui/_data -> var/lib/docker/volumes/open-webui/_data
    sed 's#^/##' "$selected" > "$filtered"

    sudo tar czf "$backup_file" \
        --ignore-failed-read \
        --warning=no-file-changed \
        -C / \
        -T "$filtered" \
        2>/dev/null
    sudo chown "$(id -un):$(id -gn)" "$backup_file" 2>/dev/null || true

    if [ -f "$backup_file" ] && [ -s "$backup_file" ]; then
        local size
        local entry_count
        size="$(du -h "$backup_file" | cut -f1)"
        entry_count="$(tar -tzf "$backup_file" 2>/dev/null | wc -l)"
        log "Backup de persistencia Docker criado: $backup_file ($size, $entry_count entradas)"
        if [ "${entry_count:-0}" -lt 5 ]; then
            warn "Backup de persistencia com poucas entradas; revisar selecao de mounts."
        fi
        write_manifest "$backup_file"
        verify_archive "$backup_file"
    else
        die "Falha ao criar backup de persistencia Docker"
    fi

    local count
    count="$(ls -1 "$VOLUMES_DIR"/docker_persistencia_*.tar.gz 2>/dev/null | wc -l)"
    if [ "$count" -gt "$VOLUMES_KEEP" ]; then
        ls -1t "$VOLUMES_DIR"/docker_persistencia_*.tar.gz | tail -n +$((VOLUMES_KEEP + 1)) | xargs -r rm -f
        log "Backups de persistencia antigos removidos (mantendo $VOLUMES_KEEP)"
    fi

    rm -f "$tmp_list" "$selected" "$filtered"
    log "Backup de persistencia Docker concluido"
}

latest_backup() {
    local dir="$1"
    find "$dir" -maxdepth 1 -name '*.tar.gz' -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -1 | cut -d' ' -f2-
}

# Mostrar status dos backups
show_status() {
    echo -e "${GREEN}==============================${NC}"
    echo -e "${GREEN}  DDS BACKUP STATUS v$BACKUP_VERSION${NC}"
    echo -e "${GREEN}==============================${NC}"
    echo ""

    if mountpoint -q "$BACKUP_DIR" 2>/dev/null; then
        echo -e "HD Backup: ${GREEN}MONTADO${NC}"
        echo -e "Espaco:    $(df -h "$BACKUP_DIR" --output=used,avail,pcent | tail -1)"
    else
        echo -e "HD Backup: ${RED}NAO MONTADO${NC}"
    fi

    echo ""
    echo "Backups diarios:"
    ls -1t "$DAILY_DIR"/dds_diario_*.tar.gz 2>/dev/null | head -5 | while read -r f; do
        echo "  $(basename "$f") ($(du -h "$f" | cut -f1))"
    done
    local daily_count
    daily_count="$(ls -1 "$DAILY_DIR"/dds_diario_*.tar.gz 2>/dev/null | wc -l)"
    echo "  Total: $daily_count backups"

    echo ""
    echo "Backups semanais:"
    ls -1t "$WEEKLY_DIR"/dds_semanal_*.tar.gz 2>/dev/null | head -5 | while read -r f; do
        echo "  $(basename "$f") ($(du -h "$f" | cut -f1))"
    done
    local weekly_count
    weekly_count="$(ls -1 "$WEEKLY_DIR"/dds_semanal_*.tar.gz 2>/dev/null | wc -l)"
    echo "  Total: $weekly_count backups"

    echo ""
    echo "Persistencia Docker:"
    ls -1t "$VOLUMES_DIR"/docker_persistencia_*.tar.gz 2>/dev/null | head -5 | while read -r f; do
        echo "  $(basename "$f") ($(du -h "$f" | cut -f1))"
    done

    echo ""
    echo "Databases:"
    ls -1t "$DB_DIR"/*.{db,sql} 2>/dev/null | head -5 | while read -r f; do
        echo "  $(basename "$f") ($(du -h "$f" | cut -f1))"
    done

    echo ""
    local last_daily
    local last_weekly
    last_daily="$(latest_backup "$DAILY_DIR" || true)"
    last_weekly="$(latest_backup "$WEEKLY_DIR" || true)"
    echo "Ultimo daily:  $(basename "${last_daily:-nenhum}")"
    echo "Ultimo weekly: $(basename "${last_weekly:-nenhum}")"
}

usage() {
    echo "DDS Backup System v$BACKUP_VERSION"
    echo ""
    echo "Uso: $0 {daily|weekly|full|databases|volumes|status|verify|restore|cleanup}"
    echo ""
    echo "  daily                 - Backup diario (codigo + configs)"
    echo "  weekly                - Backup semanal + DB + persistencia Docker"
    echo "  full                  - Backup diario + semanal + DB + persistencia"
    echo "  databases             - Backup apenas das databases"
    echo "  volumes               - Backup apenas da persistencia Docker"
    echo "  status                - Mostrar status dos backups"
    echo "  verify <arquivo>      - Validar integridade e (se houver) manifest SHA256"
    echo "  restore <arq> [dest]  - Restaurar backup em destino (padrao: $RESTORE_DIR)"
    echo "  cleanup               - Limpeza de artefatos auxiliares antigos"
}

main() {
    load_config
    hydrate_paths
    ensure_dirs
    ensure_dependencies

    case "${1:-}" in
        daily)
            check_backup_drive
            check_disk_space
            backup_daily
            ;;
        weekly)
            check_backup_drive
            check_disk_space
            backup_databases
            backup_weekly
            backup_volumes
            ;;
        full)
            check_backup_drive
            check_disk_space
            backup_databases
            backup_daily
            backup_weekly
            backup_volumes
            ;;
        databases)
            check_backup_drive
            backup_databases
            ;;
        volumes)
            check_backup_drive
            check_disk_space
            backup_volumes
            ;;
        status)
            show_status
            ;;
        verify)
            verify_archive "${2:-}"
            ;;
        restore)
            restore_archive "${2:-}" "${3:-$RESTORE_DIR}"
            ;;
        cleanup)
            cleanup_old
            ;;
        *)
            usage
            ;;
    esac
}

main "$@"
