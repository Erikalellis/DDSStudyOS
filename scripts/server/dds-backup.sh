#!/bin/bash
# ==============================
# DDS BACKUP SYSTEM
# ==============================
# By Deep Darkness Studios
# Backup automatico dos projetos, databases e persistencia Docker

BACKUP_VERSION="1.1.1"
BACKUP_DIR="/mnt/dds-backup"
DAILY_DIR="$BACKUP_DIR/backups-diarios"
WEEKLY_DIR="$BACKUP_DIR/backups-semanais"
DB_DIR="$BACKUP_DIR/databases"
VOLUMES_DIR="$BACKUP_DIR/volumes"
DDS_DIR="$HOME/dds-projetos"
LOG_FILE="$BACKUP_DIR/backup.log"

# Cores
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Quantos backups manter
DAILY_KEEP=14    # 14 dias de backups diarios
WEEKLY_KEEP=8    # 8 semanas de backups semanais
VOLUMES_KEEP=8   # 8 snapshots de persistencia Docker

# Timestamp
NOW=$(date +%Y%m%d_%H%M%S)

log() {
    local msg="[$(date '+%Y-%m-%d %H:%M:%S')] $1"
    echo -e "$msg" >> "$LOG_FILE"
    echo -e "${GREEN}$msg${NC}"
}

warn() {
    local msg="[$(date '+%Y-%m-%d %H:%M:%S')] AVISO: $1"
    echo -e "$msg" >> "$LOG_FILE"
    echo -e "${YELLOW}$msg${NC}"
}

error() {
    local msg="[$(date '+%Y-%m-%d %H:%M:%S')] ERRO: $1"
    echo -e "$msg" >> "$LOG_FILE"
    echo -e "${RED}$msg${NC}"
}

# Verificar se o HD de backup esta montado
check_backup_drive() {
    if ! mountpoint -q "$BACKUP_DIR" 2>/dev/null; then
        sudo mount /dev/sdb1 "$BACKUP_DIR" 2>/dev/null
        if ! mountpoint -q "$BACKUP_DIR" 2>/dev/null; then
            error "HD de backup nao esta montado em $BACKUP_DIR. Backup cancelado."
            exit 1
        fi
    fi
    log "HD de backup montado em $BACKUP_DIR"

    mkdir -p "$DAILY_DIR" "$WEEKLY_DIR" "$DB_DIR" "$VOLUMES_DIR"
}

# Verificar espaco disponivel (minimo 5GB)
check_disk_space() {
    local available
    available=$(df --output=avail "$BACKUP_DIR" | tail -1)
    local min_space=$((5 * 1024 * 1024))  # 5GB em KB
    if [ "$available" -lt "$min_space" ]; then
        error "Espaco insuficiente no HD de backup. Disponivel: $(df -h --output=avail "$BACKUP_DIR" | tail -1)"
        exit 1
    fi
    log "Espaco disponivel: $(df -h --output=avail "$BACKUP_DIR" | tail -1 | xargs)"
}

# Backup das databases
backup_databases() {
    log "Iniciando backup das databases..."

    # SQLite - Academia Digital
    local ad_db="$DDS_DIR/ad-app/backend/academiavirtual.db"
    if [ -f "$ad_db" ]; then
        cp "$ad_db" "$DB_DIR/academiavirtual_$NOW.db"
        log "  Database academiavirtual.db copiada"
    fi

    # SQLite via Docker (copia de dentro do container)
    if docker ps --format '{{.Names}}' | grep -q "ad-app-backend"; then
        docker cp ad-app-backend:/app/academiavirtual.db "$DB_DIR/academiavirtual_docker_$NOW.db" 2>/dev/null
        log "  Database do container Docker copiada"
    fi

    # PostgreSQL (taskingai)
    if docker ps --format '{{.Names}}' | grep -q "taskingai-db"; then
        docker exec taskingai-db-1 pg_dumpall -U postgres > "$DB_DIR/taskingai_$NOW.sql" 2>/dev/null
        log "  Database TaskingAI exportada"
    fi

    # Manter apenas ultimos 14 backups de DB
    find "$DB_DIR" -name "*.db" -mtime +14 -delete 2>/dev/null
    find "$DB_DIR" -name "*.sql" -mtime +14 -delete 2>/dev/null

    log "Backup das databases concluido"
}

# Backup diario (projetos, sem node_modules/venvs/arquivos grandes)
backup_daily() {
    log "Iniciando backup DIARIO..."

    local backup_file="$DAILY_DIR/dds_diario_$NOW.tar.gz"

    tar czf "$backup_file" \
        --exclude='node_modules' \
        --exclude='venv' \
        --exclude='.venv' \
        --exclude='__pycache__' \
        --exclude='.git' \
        --exclude='*.pyc' \
        --exclude='*.db' \
        --exclude='*.sqlite3' \
        --exclude='*.tar.gz' \
        --exclude='*.zip' \
        --exclude='*.rar' \
        --exclude='*.pem' \
        --exclude='*.key' \
        --exclude='.env' \
        --exclude='rar' \
        --exclude='painel_backup_*' \
        --exclude='backup_*' \
        -C "$HOME" \
        dds-projetos \
        start.sh \
        2>/dev/null

    if [ -f "$backup_file" ] && [ -s "$backup_file" ]; then
        local size
        size=$(du -h "$backup_file" | cut -f1)
        log "Backup diario criado: $backup_file ($size)"
    else
        error "Falha ao criar backup diario"
    fi

    # Rotacao: manter apenas os ultimos N backups diarios
    local count
    count=$(ls -1 "$DAILY_DIR"/dds_diario_*.tar.gz 2>/dev/null | wc -l)
    if [ "$count" -gt "$DAILY_KEEP" ]; then
        ls -1t "$DAILY_DIR"/dds_diario_*.tar.gz | tail -n +$((DAILY_KEEP + 1)) | xargs rm -f
        log "Backups diarios antigos removidos (mantendo $DAILY_KEEP)"
    fi

    log "Backup diario concluido"
}

# Backup semanal (completo, incluindo configs do sistema)
backup_weekly() {
    log "Iniciando backup SEMANAL (completo)..."

    local backup_file="$WEEKLY_DIR/dds_semanal_$NOW.tar.gz"

    tar czf "$backup_file" \
        --exclude='node_modules' \
        --exclude='venv' \
        --exclude='.venv' \
        --exclude='__pycache__' \
        --exclude='*.pyc' \
        --exclude='rar' \
        --exclude='painel_backup_*' \
        --exclude='backup_2025*' \
        -C "$HOME" \
        dds-projetos \
        start.sh \
        .bashrc \
        2>/dev/null

    if [ -f "$backup_file" ] && [ -s "$backup_file" ]; then
        local size
        size=$(du -h "$backup_file" | cut -f1)
        log "Backup semanal criado: $backup_file ($size)"
    else
        error "Falha ao criar backup semanal"
    fi

    # Guardar configs do sistema
    sudo cp /etc/fstab "$WEEKLY_DIR/fstab_$NOW.bak" 2>/dev/null
    sudo cp /etc/nginx/nginx.conf "$WEEKLY_DIR/nginx_$NOW.bak" 2>/dev/null
    crontab -l > "$WEEKLY_DIR/crontab_$NOW.bak" 2>/dev/null

    # Rotacao semanal
    local count
    count=$(ls -1 "$WEEKLY_DIR"/dds_semanal_*.tar.gz 2>/dev/null | wc -l)
    if [ "$count" -gt "$WEEKLY_KEEP" ]; then
        ls -1t "$WEEKLY_DIR"/dds_semanal_*.tar.gz | tail -n +$((WEEKLY_KEEP + 1)) | xargs rm -f
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
    tmp_list=$(mktemp)
    selected="${tmp_list}.selected"
    filtered="${tmp_list}.filtered"

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
        size=$(du -h "$backup_file" | cut -f1)
        entry_count=$(tar -tzf "$backup_file" 2>/dev/null | wc -l)
        log "Backup de persistencia Docker criado: $backup_file ($size, $entry_count entradas)"
        if [ "${entry_count:-0}" -lt 5 ]; then
            warn "Backup de persistencia com poucas entradas; revisar selecao de mounts."
        fi
    else
        error "Falha ao criar backup de persistencia Docker"
    fi

    local count
    count=$(ls -1 "$VOLUMES_DIR"/docker_persistencia_*.tar.gz 2>/dev/null | wc -l)
    if [ "$count" -gt "$VOLUMES_KEEP" ]; then
        ls -1t "$VOLUMES_DIR"/docker_persistencia_*.tar.gz | tail -n +$((VOLUMES_KEEP + 1)) | xargs rm -f
        log "Backups de persistencia antigos removidos (mantendo $VOLUMES_KEEP)"
    fi

    rm -f "$tmp_list" "$selected" "$filtered"
    log "Backup de persistencia Docker concluido"
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
    daily_count=$(ls -1 "$DAILY_DIR"/dds_diario_*.tar.gz 2>/dev/null | wc -l)
    echo "  Total: $daily_count backups"

    echo ""
    echo "Backups semanais:"
    ls -1t "$WEEKLY_DIR"/dds_semanal_*.tar.gz 2>/dev/null | head -5 | while read -r f; do
        echo "  $(basename "$f") ($(du -h "$f" | cut -f1))"
    done
    local weekly_count
    weekly_count=$(ls -1 "$WEEKLY_DIR"/dds_semanal_*.tar.gz 2>/dev/null | wc -l)
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
}

# Menu principal
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
    *)
        echo "DDS Backup System v$BACKUP_VERSION"
        echo ""
        echo "Uso: $0 {daily|weekly|full|databases|volumes|status}"
        echo ""
        echo "  daily     - Backup diario (codigo + configs)"
        echo "  weekly    - Backup semanal + DB + persistencia Docker"
        echo "  full      - Backup diario + semanal + DB + persistencia"
        echo "  databases - Backup apenas das databases"
        echo "  volumes   - Backup apenas da persistencia Docker"
        echo "  status    - Mostrar status dos backups"
        ;;
esac
