#!/usr/bin/env python3
"""Generate lightweight, transparent WebP cards from full-size character art.

Requires Pillow from tools/assets/requirements.txt. Output is intentionally
ignored by git and rebuilt by CI before Vite tests/build.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "web/elyndor-web/src/assets/characters/personal"
DESTINATION = SOURCE / "thumbnails"
MAX_WIDTH = 320
MAX_HEIGHT = 480


def generate(source: Path = SOURCE, destination: Path = DESTINATION) -> tuple[int, int, int]:
    destination.mkdir(parents=True, exist_ok=True)
    artwork = sorted(source.glob("*.webp"))
    valid = {item.name for item in artwork}
    for orphan in destination.glob("*.webp"):
        if orphan.name not in valid:
            orphan.unlink()

    total_source_bytes = 0
    total_preview_bytes = 0
    for item in artwork:
        output = destination / item.name
        total_source_bytes += item.stat().st_size
        if not output.exists() or output.stat().st_mtime_ns < item.stat().st_mtime_ns:
            with Image.open(item) as original:
                preview = ImageOps.exif_transpose(original).convert("RGBA")
                preview.thumbnail((MAX_WIDTH, MAX_HEIGHT), Image.Resampling.LANCZOS)
                preview.save(output, "WEBP", quality=68, method=6, exact=True)
        total_preview_bytes += output.stat().st_size
    return len(artwork), total_source_bytes, total_preview_bytes


if __name__ == "__main__":
    count, source_bytes, preview_bytes = generate()
    print(f"Shop thumbnails: {count} images, original {source_bytes / 1048576:.1f} MiB, "
          f"previews {preview_bytes / 1048576:.1f} MiB")
    if count == 0:
        raise SystemExit("No character WebP assets found.")
