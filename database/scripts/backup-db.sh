#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# 每日備份（建議排入 cron）
#
#   0 2 * * *  /opt/queue/database/scripts/backup-db.sh >> /var/log/queue-backup.log 2>&1
#
# 產出：database/backups/queue_YYYYmmdd_HHMMSS.dump.gz
# ---------------------------------------------------------------------------
set -euo pipefail

: "${POSTGRES_DB:=queue}"
: "${POSTGRES_USER:=postgres}"
: "${PGHOST:=db}"
: "${PGPORT:=5432}"
: "${PGPASSWORD:?請設定 PGPASSWORD}"
: "${BACKUP_DIR:=$(cd "$(dirname "$0")/.." && pwd)/backups}"
: "${KEEP_DAYS:=14}"

mkdir -p "$BACKUP_DIR"

STAMP="$(date +%Y%m%d_%H%M%S)"
TARGET="${BACKUP_DIR}/${POSTGRES_DB}_${STAMP}.dump.gz"

echo "[$(date -Iseconds)] 開始備份 ${POSTGRES_DB} → ${TARGET}"
pg_dump -h "$PGHOST" -p "$PGPORT" -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  --format=custom --compress=9 --no-owner --no-privileges \
  | gzip -9 > "$TARGET"

echo "[$(date -Iseconds)] 備份完成，大小：$(du -h "$TARGET" | cut -f1)"

# 清理過期備份
if [ "$KEEP_DAYS" -gt 0 ]; then
  find "$BACKUP_DIR" -name "${POSTGRES_DB}_*.dump.gz" -type f -mtime "+${KEEP_DAYS}" -print -delete
fi

echo "[$(date -Iseconds)] 保留最近 ${KEEP_DAYS} 天"
