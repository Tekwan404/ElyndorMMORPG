#!/usr/bin/env bash
set -euo pipefail

[[ "$EUID" -eq 0 ]] || { echo 'Run this script as root.' >&2; exit 1; }
source_path="${1:?Usage: install-release.sh archive.tar.gz OR staging-directory revision}"
revision="${2:-}"
root_dir=/opt/elyndor
releases_dir="$root_dir/releases"
current_link="$root_dir/current"
verifier=/usr/local/lib/elyndor/release-manifest.py
mkdir -p "$releases_dir"
exec 9>"$root_dir/.deploy.lock"
flock -x 9
previous_release=""
if [[ -L "$current_link" ]]; then
  previous_release="$(readlink -f "$current_link" 2>/dev/null || true)"
elif [[ -e "$current_link" ]]; then
  echo 'Current must be a symlink, not a directory/file.' >&2
  exit 1
fi
if [[ -n "$previous_release" && "$previous_release" != "$releases_dir/"* ]]; then
  echo 'Current release is outside the managed releases directory.' >&2
  exit 1
fi
release_dir="$(mktemp -d "$releases_dir/$(date -u +%Y%m%dT%H%M%SZ).XXXXXXXX")"
new_link="$root_dir/.current.new"
switched=false
finished=false
# Invoked by the EXIT trap, including failures under set -e.
# shellcheck disable=SC2329
cleanup() {
  status=$?
  trap - EXIT
  if [[ "$finished" != true ]]; then
    if [[ "$switched" == true ]]; then
      echo 'Deployment failed; rolling back.' >&2
      rm -f "$new_link"
      if [[ -d "$previous_release" ]]; then
        ln -s "$previous_release" "$new_link"
        mv -Tf "$new_link" "$current_link"
        systemctl restart elyndor || echo 'Rollback restart failed; inspect systemd logs.' >&2
      else
        rm -f "$current_link"
        systemctl stop elyndor || true
      fi
    fi
    rm -rf -- "$release_dir"
  fi
  exit "$status"
}
trap cleanup EXIT
if [[ -d "$source_path" && ! -L "$source_path" ]]; then
  [[ "$revision" =~ ^[0-9a-f]{40}$ ]] || { echo 'Invalid revision.' >&2; exit 1; }
  source_path="$(realpath -e "$source_path")"
  [[ "$source_path" =~ ^/tmp/elyndor-${revision}\.[a-zA-Z0-9_-]+$ ]] || { echo 'Invalid staging path.' >&2; exit 1; }
  # Separate inodes/reflinks preserve rollback when future rsync updates run.
  cp -a --reflink=auto "$source_path/." "$release_dir/"
elif [[ -f "$source_path" ]]; then
  python3 - "$source_path" <<'PY'
import pathlib, sys, tarfile
with tarfile.open(sys.argv[1], "r:gz") as archive:
    for member in archive:
        name = pathlib.PurePosixPath(member.name)
        if name.is_absolute() or ".." in name.parts or not (member.isfile() or member.isdir()):
            raise SystemExit("Unsafe archive entry")
PY
  tar -xzf "$source_path" -C "$release_dir"
else
  echo 'Release source not found.' >&2
  exit 1
fi
if [[ -n "$revision" || -f "$release_dir/RELEASE-MANIFEST.json" ]]; then
  if [[ -z "$revision" ]]; then revision="$(cat "$release_dir/REVISION")"; fi
  python3 "$verifier" verify "$release_dir" "$revision"
fi
for file in Elyndor.Server.dll frontend/index.html frontend-admin/index.html content/package.json; do
  [[ -f "$release_dir/$file" ]] || { echo "Incomplete release: $file" >&2; exit 1; }
done
chown -R root:elyndor "$release_dir"
chmod -R u=rwX,g=rX,o= "$release_dir"
rm -f "$new_link"
ln -s "$release_dir" "$new_link"
mv -Tf "$new_link" "$current_link"
switched=true
systemctl daemon-reload
systemctl restart elyndor
for _ in $(seq 1 60); do
  if curl --fail --silent --show-error --max-time 3 http://127.0.0.1:5080/api/v1/status | grep -q '"status":"ready"'; then
    finished=true
    echo "Elyndor release is healthy: $(basename "$release_dir")"
    kept=0
    while IFS= read -r old_release; do
      if [[ "$old_release" == "$release_dir" || "$old_release" == "$previous_release" || "$kept" -lt 3 ]]; then
        kept=$((kept + 1))
      else
        rm -rf -- "$old_release"
      fi
    done < <(find "$releases_dir" -mindepth 1 -maxdepth 1 -type d -printf '%T@ %p\n' | sort -nr | cut -d' ' -f2-)
    exit 0
  fi
  sleep 2
done
echo 'Health check failed.' >&2
exit 1
