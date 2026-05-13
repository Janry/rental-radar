#!/usr/bin/env bash
# Backup SQLite файлу через built-in `.backup` (atomic, live-friendly — не блокує писалок).
# Викликати з cron на хост-машині (Oracle Cloud VM):
#   0 3 * * * /opt/rental-radar/docker/backup-sqlite.sh
#
# Параметри через env:
#   RR_DATA_DIR   — де лежить rental_radar.db (default: ./data на VM)
#   RR_BACKUP_DIR — куди класти бекапи (default: ./backups)
#   RR_KEEP_DAYS  — скільки днів тримати (default: 14)

set -euo pipefail

DATA_DIR="${RR_DATA_DIR:-/opt/rental-radar/data}"
BACKUP_DIR="${RR_BACKUP_DIR:-/opt/rental-radar/backups}"
KEEP_DAYS="${RR_KEEP_DAYS:-14}"

DB="$DATA_DIR/rental_radar.db"
TS=$(date +%Y%m%d_%H%M%S)
OUT="$BACKUP_DIR/rental_radar_${TS}.db"

mkdir -p "$BACKUP_DIR"

if [ ! -f "$DB" ]; then
    echo "[backup] No DB at $DB — nothing to back up." >&2
    exit 0
fi

# Виконуємо backup через контейнер scraper-а (там є sqlite3 у Playwright base image).
# Альтернатива — встановити sqlite3 на хост.
docker compose -f "$(dirname "$0")/docker-compose.yml" exec -T scraper \
    sqlite3 /app/data/rental_radar.db ".backup '/app/data/_backup_tmp.db'"
mv "$DATA_DIR/_backup_tmp.db" "$OUT"
gzip "$OUT"

echo "[backup] Saved $OUT.gz"

# Cleanup старіших бекапів.
find "$BACKUP_DIR" -name "rental_radar_*.db.gz" -mtime "+$KEEP_DAYS" -delete
echo "[backup] Pruned files older than $KEEP_DAYS days."
