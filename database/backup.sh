#!/usr/bin/env bash
# SQL Server backup script — full COPY_ONLY backup of PFA_DB and PFA_DW to
# timestamped .bak files, plus retention-based pruning.
#
# Run by the db-backup sidecar (docker-compose) on a loop. Also runnable
# manually inside any container that has sqlcmd:
#   docker compose exec db-backup /backup.sh
#
# Env vars (passed by compose):
#   SA_PASSWORD            — sa login password (required)
#   DB_HOST                — SQL Server host, e.g. "db" (required)
#   BACKUP_DIR             — where to write .bak files (required)
#   BACKUP_RETENTION_DAYS  — delete .bak older than N days (default 7)
set -euo pipefail

: "${SA_PASSWORD:?SA_PASSWORD is required}"
: "${DB_HOST:?DB_HOST is required}"
: "${BACKUP_DIR:?BACKUP_DIR is required}"

RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-7}"
mkdir -p "$BACKUP_DIR"

TS="$(date +%Y%m%d_%H%M%S)"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd

backup_one() {
  local db="$1"
  local out="$BACKUP_DIR/${db}_${TS}.bak"
  echo "==> Backing up [$db] -> $out"
  SQLCMDPASSWORD="$SA_PASSWORD" "$SQLCMD" \
    -S "$DB_HOST" -U sa -C -b \
    -Q "BACKUP DATABASE [$db] TO DISK='$out' WITH FORMAT, INIT, SKIP, COMPRESSION, STATS=10"
  echo "    done ($(du -h "$out" 2>/dev/null | cut -f1 || echo '?'))"
}

for db in PFA_DB PFA_DW; do
  backup_one "$db"
done

# Prune backups older than the retention window.
echo "==> Pruning .bak files older than ${RETENTION_DAYS} days..."
find "$BACKUP_DIR" -maxdepth 1 -name '*.bak' -type f -mtime "+${RETENTION_DAYS}" -print -delete \
  | sed 's/^/    deleted: /' || true

echo "==> Backup complete at $(date -u +%Y-%m-%dT%H:%M:%SZ)"
