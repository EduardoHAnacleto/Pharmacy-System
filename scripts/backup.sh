#!/usr/bin/env bash
#
# Backs up everything that cannot be rebuilt from this repository: the database
# and the uploaded promotion images.
#
# The images matter as much as the database. They live in a Docker volume, not
# in git, and every promotion row points at one — a database restored without
# them comes back with a catalogue of broken pictures.
#
# Usage:
#   scripts/backup.sh [destination-directory]
#
# Cron example, daily at 03:15:
#   15 3 * * * cd /srv/pharmacy-system && scripts/backup.sh >> /var/log/pharmacy-backup.log 2>&1
#
# Restore:
#   gunzip -c backups/<stamp>/database.sql.gz \
#     | docker compose exec -T db mysql -uroot -p"$MYSQL_ROOT_PASSWORD" "$MYSQL_DATABASE"
#
#   docker run --rm -v pharmacy-system_promotion_images:/target -v "$PWD/backups/<stamp>":/backup \
#     alpine sh -c 'rm -rf /target/* && tar xzf /backup/promotion_images.tar.gz -C /target'

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

DEST_ROOT="${1:-$REPO_ROOT/backups}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"

# Volume names are prefixed with the compose project name.
PROJECT_NAME="${COMPOSE_PROJECT_NAME:-pharmacy-system}"
IMAGES_VOLUME="${PROJECT_NAME}_promotion_images"

if [[ ! -f .env ]]; then
  echo "error: .env not found in $REPO_ROOT" >&2
  exit 1
fi

# shellcheck disable=SC1091
set -a; source .env; set +a

: "${MYSQL_ROOT_PASSWORD:?must be set in .env}"
: "${MYSQL_DATABASE:?must be set in .env}"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DEST="$DEST_ROOT/$STAMP"
mkdir -p "$DEST"

echo "==> Backing up to $DEST"

# ---------------------------------------------------------------------------
# Database
# ---------------------------------------------------------------------------
# --single-transaction keeps the dump consistent without locking the tables, so
# the storefront stays up during the backup.
echo "--> Dumping database $MYSQL_DATABASE"
docker compose exec -T db \
  mysqldump \
    -uroot -p"$MYSQL_ROOT_PASSWORD" \
    --single-transaction \
    --routines \
    --triggers \
    "$MYSQL_DATABASE" \
  | gzip > "$DEST/database.sql.gz"

# ---------------------------------------------------------------------------
# Uploaded images
# ---------------------------------------------------------------------------
echo "--> Archiving volume $IMAGES_VOLUME"
docker run --rm \
  -v "$IMAGES_VOLUME":/source:ro \
  -v "$DEST":/backup \
  alpine tar czf /backup/promotion_images.tar.gz -C /source .

# ---------------------------------------------------------------------------
# Verify, then rotate
# ---------------------------------------------------------------------------
# Rotation happens only after both artefacts exist and are non-empty. A failed
# backup must never be the reason old, good backups get deleted.
for artefact in database.sql.gz promotion_images.tar.gz; do
  if [[ ! -s "$DEST/$artefact" ]]; then
    echo "error: $artefact is missing or empty; keeping all previous backups" >&2
    exit 1
  fi
done

gzip -t "$DEST/database.sql.gz"
tar tzf "$DEST/promotion_images.tar.gz" >/dev/null

echo "--> Removing backups older than $RETENTION_DAYS days"
find "$DEST_ROOT" -mindepth 1 -maxdepth 1 -type d -mtime "+$RETENTION_DAYS" \
  -exec rm -rf {} +

echo "==> Done: $(du -sh "$DEST" | cut -f1) in $DEST"
