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
            None,  # шестая ячейка пустая
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
        description="Slice icon sheets into individual PNG icons for Elyndor."
    )

    parser.add_argument("image", help="Path to source sheet PNG")

    parser.add_argument(
        "--preset",
        choices=sorted(PRESETS.keys()),
        help="Built-in layout preset. armor5 = 5 items + empty; armor6 = 6 items; armor8 = 8 items",
    )

    parser.add_argument("--cols", type=int, help="Grid columns")
    parser.add_argument("--rows", type=int, help="Grid rows")

    parser.add_argument(
        "--out",
        required=True,
        help="Output directory",
    )

    parser.add_argument(
        "--prefix",
        default="icon",
        help="Filename prefix, e.g. guardian_depths",
    )

    parser.add_argument(
        "--names",
        help="Optional .txt or .json file with custom names",
    )

    parser.add_argument(
        "--size",
        type=int,
        default=256,
        help="Final square icon size in px (default: 256)",
    )

    parser.add_argument(
        "--trim-transparent",
        action="store_true",
        help="Trim transparent borders before resize",
    )

    parser.add_argument(
        "--skip-empty",
        action="store_true",
        help="Skip fully transparent cells",
    )

    parser.add_argument(
        "--empty-alpha-threshold",
        type=int,
        default=8,
        help="Alpha threshold for empty-cell detection (default: 8)",
    )

    parser.add_argument(
        "--manifest",
        help="Optional JSON manifest output path",
    )

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

        return [
            NameEntry(index=i, file_stem=slugify(line))
            for i, line in enumerate(lines)
        ]

    if p.suffix.lower() == ".json":
        data = json.loads(p.read_text(encoding="utf-8"))

        if not isinstance(data, list):
            raise ValueError("Names JSON must be a list")

        result: list[NameEntry] = []

        for i, item in enumerate(data):
            if isinstance(item, str):
                result.append(
                    NameEntry(
                        index=i,
                        file_stem=slugify(item),
                        raw={"value": item},
                    )
                )
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
                    raise ValueError(
                        f"JSON entry #{i + 1} must contain one of: "
                        "id, file, filename, slug, name"
                    )

                icon_rel_path = item.get("iconPath")

                result.append(
                    NameEntry(
                        index=i,
                        file_stem=slugify(str(file_stem)),
                        icon_rel_path=str(icon_rel_path) if icon_rel_path else None,
                        raw=item,
                    )
                )
                continue

            raise ValueError(
                f"Unsupported JSON entry at index {i}: {item!r}"
            )

        return result

    raise ValueError("Names file must be .txt or .json")


def alpha_bbox(img: Image.Image, threshold: int = 1):
    rgba = img.convert("RGBA")
    alpha = rgba.getchannel("A")
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


def ensure_parent(path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)


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
        raise ValueError("Invalid grid geometry: usable area <= 0")

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


def build_output_path(
    out_root: Path,
    entry: NameEntry | None,
    prefix: str,
    idx: int,
) -> Path:
    if entry and entry.icon_rel_path:
        rel = Path(entry.icon_rel_path)
        filename = rel.name

        if not filename.lower().endswith(".png"):
            filename += ".png"

        return out_root / rel.parent / filename

    if entry:
        return out_root / f"{entry.file_stem}.png"

    return out_root / f"{slugify(prefix)}_{idx + 1:02d}.png"


def main():
    args = parse_args()

    image_path = Path(args.image)
    out_root = Path(args.out)

    if not image_path.exists():
        raise FileNotFoundError(f"Source image not found: {image_path}")

    if args.size <= 0:
        raise ValueError("--size must be greater than 0")

    preset = PRESETS.get(args.preset) if args.preset else None

    if preset:
        cols = preset["cols"]
        rows = preset["rows"]
        preset_slots = preset["slots"]

        print(f"[PRESET] {args.preset}: {cols}x{rows}")
    else:
        if not args.cols or not args.rows:
            raise ValueError(
                "Specify --cols and --rows, or use --preset armor5 / armor6 / armor8"
            )

        cols = args.cols
        rows = args.rows
        preset_slots = None

    if cols <= 0 or rows <= 0:
        raise ValueError("--cols and --rows must be greater than 0")

    sheet = Image.open(image_path).convert("RGBA")

    print(f"[SOURCE] {image_path}")
    print(f"[SIZE]   {sheet.width}x{sheet.height}")
    print(f"[GRID]   {cols}x{rows}")

    boxes, cell_w, cell_h = compute_grid_boxes(
        img_w=sheet.width,
        img_h=sheet.height,
        cols=cols,
        rows=rows,
        pad_left=args.pad_left,
        pad_right=args.pad_right,
        pad_top=args.pad_top,
        pad_bottom=args.pad_bottom,
        gap_x=args.gap_x,
        gap_y=args.gap_y,
    )

    ratio = cell_w / cell_h if cell_h else 0

    if ratio < 0.90 or ratio > 1.10:
        print(
            f"[WARNING] Cells are about {cell_w:.1f}x{cell_h:.1f}, "
            "so they are not square."
        )
        print(
            "[WARNING] Check --cols/--rows. "
            "For common armor sets use --preset armor5 / armor6 / armor8."
        )

    names = load_names(args.names)

    total_cells = cols * rows

    if names and len(names) > total_cells:
        raise ValueError(
            f"Names count ({len(names)}) is greater than cell count ({total_cells})"
        )

    out_root.mkdir(parents=True, exist_ok=True)

    generated = []
    saved_count = 0
    skipped_count = 0

    for idx, box in enumerate(boxes):
        if preset_slots is not None and preset_slots[idx] is None:
            skipped_count += 1
            print(f"[SKIP] Cell {idx + 1}: preset says empty")
            continue

        cell = sheet.crop(box)

        if args.skip_empty and is_empty_cell(
            cell,
            args.empty_alpha_threshold,
        ):
            skipped_count += 1
            print(f"[SKIP] Cell {idx + 1}: transparent/empty")
            continue

        if args.trim_transparent:
            cell = trim_transparent(cell)

        if args.skip_empty and is_empty_cell(
            cell,
            args.empty_alpha_threshold,
        ):
            skipped_count += 1
            print(f"[SKIP] Cell {idx + 1}: transparent/empty after trim")
            continue

        final_img = fit_to_square(cell, args.size)

        entry: NameEntry | None = None
        display_label = None

        if preset_slots is not None:
            slot_key, display_label = preset_slots[idx]

            entry = NameEntry(
                index=idx,
                file_stem=f"{slugify(args.prefix)}_{slot_key}",
                raw={
                    "slot": slot_key,
                    "labelRu": display_label,
                },
            )

        elif idx < len(names):
            entry = names[idx]

        output_path = build_output_path(
            out_root=out_root,
            entry=entry,
            prefix=args.prefix,
            idx=idx,
        )

        ensure_parent(output_path)
        final_img.save(output_path)

        if display_label:
            print(f"[OK] {display_label}: {output_path}")
        else:
            print(f"[OK] {output_path}")

        row = {
            "index": idx + 1,
            "sourceBox": {
                "left": box[0],
                "top": box[1],
                "right": box[2],
                "bottom": box[3],
            },
            "output": str(output_path.as_posix()),
        }

        if entry:
            row["fileStem"] = entry.file_stem

            if entry.icon_rel_path:
                row["iconPath"] = entry.icon_rel_path

            if entry.raw is not None:
                row["meta"] = entry.raw

        generated.append(row)
        saved_count += 1

    print()
    print(
        f"Done. Saved: {saved_count}, "
        f"Skipped: {skipped_count}, "
        f"Total cells: {total_cells}"
    )

    if args.manifest:
        manifest_path = Path(args.manifest)
        ensure_parent(manifest_path)

        manifest = {
            "sourceImage": str(image_path.as_posix()),
            "preset": args.preset,
            "cols": cols,
            "rows": rows,
            "size": args.size,
            "generated": generated,
        }

        manifest_path.write_text(
            json.dumps(
                manifest,
                ensure_ascii=False,
                indent=2,
            ),
            encoding="utf-8",
        )

        print(f"[MANIFEST] {manifest_path}")


if __name__ == "__main__":
    main()
