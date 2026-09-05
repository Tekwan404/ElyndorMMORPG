#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this script as root." >&2
  exit 1
fi

database="${ELYNDOR_DB_NAME:-game}"
backup_dir="${ELYNDOR_BACKUP_DIR:-/var/backups/elyndor}"
retention_days="${ELYNDOR_BACKUP_RETENTION_DAYS:-7}"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_path="$backup_dir/${database}-$timestamp.dump"

umask 077
mkdir -p "$backup_dir"

runuser -u postgres -- pg_dump --format=custom --no-owner "$database" > "$backup_path"
find "$backup_dir" -type f -name "${database}-*.dump" -mtime "+$retention_days" -delete

echo "Database backup created: $backup_path"
