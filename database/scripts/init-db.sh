#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# 建立資料庫與初始資料
#
#   ./database/scripts/init-db.sh
#
# 正式環境建議交由應用程式啟動時的 EF Migration 執行（Database__MigrateOnStartup），
# 本腳本僅用於手動初始化或災難復原。
# ---------------------------------------------------------------------------
set -euo pipefail

: "${POSTGRES_DB:=queue}"
: "${POSTGRES_USER:=postgres}"
: "${PGHOST:=db}"
: "${PGPORT:=5432}"
: "${PGPASSWORD:?請設定 PGPASSWORD}"

export PGDATABASE="$POSTGRES_DB"

echo "==> 建立角色與權限"
psql -h "$PGHOST" -p "$PGPORT" -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 <<'SQL'
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'queue_app') THEN
        CREATE ROLE queue_app LOGIN PASSWORD 'CHANGE_ME_in_production';
    END IF;
END
$$;
SQL

echo "==> 建立資料庫 ${POSTGRES_DB}"
psql -h "$PGHOST" -p "$PGPORT" -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -c \
    "CREATE DATABASE \"${POSTGRES_DB}\" OWNER \"${POSTGRES_USER}\" ENCODING 'UTF8';"

echo "==> 套用 Migration（種子資料由 HasData 一併寫入）"
cd "$(dirname "$0")/../.."
dotnet ef database update \
  --project backend/Queue.Infrastructure \
  --startup-project backend/Queue.Api \
  --context QueueDbContext

echo "==> 完成。請確認 /health/ready 回傳 Healthy。"
