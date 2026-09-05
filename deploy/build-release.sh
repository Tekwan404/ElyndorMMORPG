#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_path="${1:-$repo_root/artifacts/elyndor-linux-x64.tar.gz}"
work_dir="$(mktemp -d)"
publish_dir="$work_dir/publish"

cleanup() {
  rm -rf "$work_dir"
}
trap cleanup EXIT

mkdir -p "$(dirname "$output_path")"

npm ci --prefix "$repo_root/web/elyndor-web"
npm run build --prefix "$repo_root/web/elyndor-web"

dotnet restore "$repo_root/Elyndor.slnx"
dotnet publish "$repo_root/src/Elyndor.Server/Elyndor.Server.csproj" \
  --configuration Release \
  --no-restore \
  --self-contained false \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  --output "$publish_dir"

mkdir -p "$publish_dir/frontend"
cp -a "$repo_root/web/elyndor-web/dist/." "$publish_dir/frontend/"

revision="$(git -C "$repo_root" rev-parse HEAD 2>/dev/null || printf 'unknown')"
printf '%s\n' "$revision" > "$publish_dir/REVISION"

test -f "$publish_dir/Elyndor.Server.dll"
test -f "$publish_dir/frontend/index.html"
test -f "$publish_dir/content/package.json"

tar -C "$publish_dir" -czf "$output_path" .

printf 'Production release created: %s\n' "$output_path"
