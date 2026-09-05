#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this script as root." >&2
  exit 1
fi

archive="${1:?Usage: install-release.sh /path/to/elyndor-release.tar.gz}"
if [[ ! -f "$archive" ]]; then
  echo "Release archive not found: $archive" >&2
  exit 1
fi

archive_listing="$(tar -tzf "$archive")"
if grep -Eq '(^/|(^|/)\.\.(/|$))' <<<"$archive_listing"; then
  echo "Release archive contains unsafe paths." >&2
  exit 1
fi

root_dir="/opt/elyndor"
releases_dir="$root_dir/releases"
release_id="$(date -u +%Y%m%dT%H%M%SZ)"
release_dir="$releases_dir/$release_id"
current_link="$root_dir/current"
previous_release=""

mkdir -p "$releases_dir"
if [[ -L "$current_link" ]]; then
  previous_release="$(readlink -f "$current_link" || true)"
fi

mkdir "$release_dir"
tar -xzf "$archive" -C "$release_dir"

if [[ ! -f "$release_dir/Elyndor.Server.dll" \
    || ! -f "$release_dir/frontend/index.html" \
    || ! -f "$release_dir/frontend-admin/index.html" ]]; then
  rm -rf "$release_dir"
  echo "Release is incomplete: server, game frontend, or admin frontend is missing." >&2
  exit 1
fi

chown -R root:elyndor "$release_dir"
chmod -R u=rwX,g=rX,o= "$release_dir"

new_link="$root_dir/.current.new"
rm -f "$new_link"
ln -s "$release_dir" "$new_link"
mv -Tf "$new_link" "$current_link"

systemctl daemon-reload
systemctl restart elyndor

for _ in $(seq 1 60); do
  if curl --fail --silent --show-error --max-time 3 http://127.0.0.1:5080/api/v1/status | grep -q '"status":"ready"'; then
    echo "Elyndor release is healthy: $release_id"
    mapfile -t old_releases < <(find "$releases_dir" -mindepth 1 -maxdepth 1 -type d -printf '%T@ %p\n' | sort -nr | tail -n +4 | cut -d' ' -f2-)
    if [[ "${#old_releases[@]}" -gt 0 ]]; then
      rm -rf -- "${old_releases[@]}"
    fi
    exit 0
  fi
  sleep 2
done

echo "Health check failed; rolling back." >&2
if [[ -n "$previous_release" && -d "$previous_release" ]]; then
  rm -f "$new_link"
  ln -s "$previous_release" "$new_link"
  mv -Tf "$new_link" "$current_link"
  systemctl restart elyndor
else
  rm -f "$current_link"
  systemctl stop elyndor || true
fi

exit 1
