from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image


@dataclass
class NameEntry:
    index: int
    file_stem: str
    icon_rel_path: str | None = None
    raw: dict[str, Any] | None = None


PRESETS = {
    "armor5": {
        "cols": 3,
        "rows": 2,
        "slots": [
            ("helmet", "Шлем"),
            ("chest", "Броня"),
            ("gloves", "Перчатки"),
            ("legs", "Поножи"),
            ("boots", "Сапоги"),
            None,
        ],
    },
    "armor6": {
        "cols": 3,
        "rows": 2,
        "slots": [
            ("helmet", "Шлем"),
            ("shoulders", "Наплечники"),
            ("chest", "Броня"),
            ("gloves", "Перчатки"),
            ("legs", "Поножи"),
            ("boots", "Сапоги"),
        ],
    },
    "armor8": {
        "cols": 4,
        "rows": 2,
        "slots": [
            ("helmet", "Шлем"),
            ("shoulders", "Наплечники"),
            ("chest", "Броня"),
            ("bracers", "Наручи"),
            ("gloves", "Перчатки"),
            ("belt", "Пояс"),
            ("legs", "Поножи"),
            ("boots", "Сапоги"),
        ],
    },
}


def parse_args():
    parser = argparse.ArgumentParser(
        description="Slice Elyndor icon sheets directly to PNG or WebP."
    )

    parser.add_argument("image", help="Path to source sheet")
    parser.add_argument(
        "--preset",
        choices=sorted(PRESETS.keys()),
        help="Built-in armor layout preset",
    )
    parser.add_argument("--cols", type=int, help="Grid columns")
    parser.add_argument("--rows", type=int, help="Grid rows")
    parser.add_argument(
        "--count",
        type=int,
        help="Only save the first N cells. Useful when trailing cells are blank.",
    )
    parser.add_argument("--out", required=True, help="Output directory")
    parser.add_argument("--prefix", default="icon", help="Output filename prefix")
    parser.add_argument("--names", help="Optional .txt/.json custom names file")
    parser.add_argument("--size", type=int, default=256, help="Final icon size")
    parser.add_argument(
        "--format",
        choices=["webp", "png"],
        default="webp",
        help="Output format (default: webp)",
    )
    parser.add_argument(
        "--quality",
        type=int,
        default=90,
        help="WebP quality 1-100 (default: 90)",
    )
    parser.add_argument(
        "--lossless",
        action="store_true",
        help="Use lossless WebP",
    )
    parser.add_argument("--trim-transparent", action="store_true")
    parser.add_argument("--skip-empty", action="store_true")
    parser.add_argument("--empty-alpha-threshold", type=int, default=8)
    parser.add_argument("--manifest", help="Optional JSON manifest path")
    parser.add_argument("--pad-left", type=int, default=0)
    parser.add_argument("--pad-right", type=int, default=0)
    parser.add_argument("--pad-top", type=int, default=0)
    parser.add_argument("--pad-bottom", type=int, default=0)
    parser.add_argument("--gap-x", type=int, default=0)
    parser.add_argument("--gap-y", type=int, default=0)

    return parser.parse_args()


def slugify(value: str) -> str:
    value = value.strip().lower()
    value = value.replace(" ", "_").replace("-", "_")
    value = re.sub(r"[^\w]+", "_", value, flags=re.UNICODE)
    value = re.sub(r"_+", "_", value).strip("_")
    return value or "icon"


def load_names(path: str | None) -> list[NameEntry]:
    if not path:
        return []

    p = Path(path)
    if not p.exists():
        raise FileNotFoundError(f"Names file not found: {p}")

    if p.suffix.lower() == ".txt":
        lines = [
            line.strip()
            for line in p.read_text(encoding="utf-8").splitlines()
            if line.strip()
        ]
        return [NameEntry(i, slugify(line)) for i, line in enumerate(lines)]

    if p.suffix.lower() == ".json":
        data = json.loads(p.read_text(encoding="utf-8"))
        if not isinstance(data, list):
            raise ValueError("Names JSON must be a list")

        result: list[NameEntry] = []
        for i, item in enumerate(data):
            if isinstance(item, str):
                result.append(NameEntry(i, slugify(item), raw={"value": item}))
                continue

            if isinstance(item, dict):
                file_stem = (
                    item.get("id")
                    or item.get("file")
                    or item.get("filename")
                    or item.get("slug")
                    or item.get("name")
                )
                if not file_stem:
                    raise ValueError(f"JSON entry #{i + 1} has no usable name")

                icon_rel_path = item.get("iconPath")
                result.append(
                    NameEntry(
                        i,
                        slugify(str(file_stem)),
                        str(icon_rel_path) if icon_rel_path else None,
                        item,
                    )
                )
                continue

            raise ValueError(f"Unsupported JSON entry at index {i}")

        return result

    raise ValueError("Names file must be .txt or .json")


def alpha_bbox(img: Image.Image, threshold: int = 1):
    alpha = img.convert("RGBA").getchannel("A")
    mask = alpha.point(lambda a: 255 if a >= threshold else 0)
    return mask.getbbox()


def is_empty_cell(img: Image.Image, threshold: int = 8) -> bool:
    return alpha_bbox(img, threshold) is None


def trim_transparent(img: Image.Image) -> Image.Image:
    bbox = alpha_bbox(img, 1)
    return img.crop(bbox) if bbox else img


def fit_to_square(img: Image.Image, size: int) -> Image.Image:
    img = img.convert("RGBA")
    w, h = img.size

    if w <= 0 or h <= 0:
        return Image.new("RGBA", (size, size), (0, 0, 0, 0))

    scale = min(size / w, size / h)
    new_w = max(1, round(w * scale))
    new_h = max(1, round(h * scale))

    resized = img.resize((new_w, new_h), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    x = (size - new_w) // 2
    y = (size - new_h) // 2
    canvas.paste(resized, (x, y), resized)
    return canvas


def compute_grid_boxes(
    img_w: int,
    img_h: int,
    cols: int,
    rows: int,
    pad_left: int,
    pad_right: int,
    pad_top: int,
    pad_bottom: int,
    gap_x: int,
    gap_y: int,
):
    usable_w = img_w - pad_left - pad_right - gap_x * (cols - 1)
    usable_h = img_h - pad_top - pad_bottom - gap_y * (rows - 1)

    if usable_w <= 0 or usable_h <= 0:
        raise ValueError("Invalid grid geometry")

    cell_w = usable_w / cols
    cell_h = usable_h / rows

    boxes = []
    for row in range(rows):
        for col in range(cols):
            left = round(pad_left + col * (cell_w + gap_x))
            top = round(pad_top + row * (cell_h + gap_y))
            right = round(left + cell_w)
            bottom = round(top + cell_h)
            boxes.append((left, top, right, bottom))

    return boxes, cell_w, cell_h


def save_image(img: Image.Image, path: Path, fmt: str, quality: int, lossless: bool):
    path.parent.mkdir(parents=True, exist_ok=True)

    if fmt == "webp":
        img.save(
            path,
            "WEBP",
            quality=quality,
            method=6,
            lossless=lossless,
        )
    else:
        img.save(path, "PNG")


def main():
    args = parse_args()

    image_path = Path(args.image)
    out_root = Path(args.out)

    if not image_path.exists():
        raise FileNotFoundError(f"Source image not found: {image_path}")

    if args.size <= 0:
        raise ValueError("--size must be > 0")

    if not 1 <= args.quality <= 100:
        raise ValueError("--quality must be 1..100")

    preset = PRESETS.get(args.preset) if args.preset else None

    if preset:
        cols = preset["cols"]
        rows = preset["rows"]
        preset_slots = preset["slots"]
    else:
        if not args.cols or not args.rows:
            raise ValueError("Use --preset or provide --cols and --rows")
        cols = args.cols
        rows = args.rows
        preset_slots = None

    sheet = Image.open(image_path).convert("RGBA")

    boxes, cell_w, cell_h = compute_grid_boxes(
        sheet.width,
        sheet.height,
        cols,
        rows,
        args.pad_left,
        args.pad_right,
        args.pad_top,
        args.pad_bottom,
        args.gap_x,
        args.gap_y,
    )

    total_cells = len(boxes)
    save_limit = args.count if args.count is not None else total_cells

    if save_limit < 0 or save_limit > total_cells:
        raise ValueError(f"--count must be between 0 and {total_cells}")

    names = load_names(args.names)
    ext = ".webp" if args.format == "webp" else ".png"

    print(f"[SOURCE] {image_path}")
    print(f"[SIZE]   {sheet.width}x{sheet.height}")
    print(f"[GRID]   {cols}x{rows}")
    print(f"[FORMAT] {args.format.upper()}")
    print(f"[COUNT]  {save_limit}/{total_cells}")

    if abs((cell_w / cell_h) - 1.0) > 0.10:
        print(f"[WARNING] Cells are not square: {cell_w:.1f}x{cell_h:.1f}")

    out_root.mkdir(parents=True, exist_ok=True)

    generated = []
    saved = 0
    skipped = 0

    for idx, box in enumerate(boxes):
        if idx >= save_limit:
            skipped += 1
            continue

        if preset_slots is not None and preset_slots[idx] is None:
            skipped += 1
            print(f"[SKIP] Cell {idx + 1}: preset empty")
            continue

        cell = sheet.crop(box)

        if args.skip_empty and is_empty_cell(cell, args.empty_alpha_threshold):
            skipped += 1
            print(f"[SKIP] Cell {idx + 1}: transparent/empty")
            continue

        if args.trim_transparent:
            cell = trim_transparent(cell)

        final_img = fit_to_square(cell, args.size)

        display_label = None

        if preset_slots is not None:
            slot_key, display_label = preset_slots[idx]
            stem = f"{slugify(args.prefix)}_{slot_key}"
        elif idx < len(names):
            stem = names[idx].file_stem
        else:
            stem = f"{slugify(args.prefix)}_{idx + 1:02d}"

        output_path = out_root / f"{stem}{ext}"
        save_image(final_img, output_path, args.format, args.quality, args.lossless)

        if display_label:
            print(f"[OK] {display_label}: {output_path}")
        else:
            print(f"[OK] {output_path}")

        generated.append(
            {
                "index": idx + 1,
                "output": str(output_path.as_posix()),
                "sourceBox": {
                    "left": box[0],
                    "top": box[1],
                    "right": box[2],
                    "bottom": box[3],
                },
            }
        )
        saved += 1

    print()
    print(f"Done. Saved: {saved}, Skipped: {skipped}, Total cells: {total_cells}")

    if args.manifest:
        manifest_path = Path(args.manifest)
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(
            json.dumps(
                {
                    "sourceImage": str(image_path.as_posix()),
                    "preset": args.preset,
                    "cols": cols,
                    "rows": rows,
                    "count": save_limit,
                    "size": args.size,
                    "format": args.format,
                    "generated": generated,
                },
                ensure_ascii=False,
                indent=2,
            ),
            encoding="utf-8",
        )
        print(f"[MANIFEST] {manifest_path}")


if __name__ == "__main__":
    main()
