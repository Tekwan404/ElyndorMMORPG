#!/usr/bin/env python3
"""Bind a production file tree to its tested revision and reject changed/extra files."""
import hashlib
import json
import pathlib
import re
import sys

MANIFEST = "RELEASE-MANIFEST.json"
REQUIRED = {"REVISION", "Elyndor.Server.dll", "frontend/index.html",
            "frontend-admin/index.html", "content/package.json"}


def inventory(root):
    files = {}
    for path in sorted(root.rglob("*")):
        if path.is_symlink():
            raise ValueError(f"Symlink prohibited: {path.relative_to(root)}")
        if path.is_dir():
            continue
        if not path.is_file():
            raise ValueError("Non-regular release file")
        name = path.relative_to(root).as_posix()
        if name != MANIFEST:
            with path.open("rb") as stream:
                files[name] = hashlib.file_digest(stream, "sha256").hexdigest()
    if not REQUIRED.issubset(files):
        raise ValueError("Incomplete production release")
    return files


def main():
    action, directory, revision = sys.argv[1:]
    if action not in {"create", "verify"} or not re.fullmatch(r"[0-9a-f]{40}", revision):
        raise ValueError("Invalid action/revision")
    root = pathlib.Path(directory)
    if root.is_symlink() or not root.is_dir():
        raise ValueError("Release must be a real directory")
    files = inventory(root)
    if (root / "REVISION").read_text(encoding="utf-8").strip() != revision:
        raise ValueError("Release revision mismatch")
    expected = {"revision": revision, "files": files}
    manifest = root / MANIFEST
    if action == "create":
        manifest.write_text(json.dumps(expected, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    elif json.loads(manifest.read_text(encoding="utf-8")) != expected:
        raise ValueError("Release checksum/file inventory mismatch")
    print(f"Release {revision}: {len(files)} files {action} OK")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
