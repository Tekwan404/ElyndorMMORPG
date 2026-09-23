"""Convert the approved PersonalArt sources into transparent game WebP assets.

Usage: py tools/prepare_personal_art.py SOURCE_DIRECTORY OUTPUT_DIRECTORY
The two scenic male backgrounds are intentionally not game character cutouts.
"""

from pathlib import Path
from PIL import Image, ImageChops
import sys


NAMES = {
    "archer female skin/Accurate shooting skin.png": "archer-female-accurate",
    "archer female skin/archer admin skin.png": "archer-female-admin",
    "archer female skin/bikini skin.png": "archer-female-bikini",
    "archer female skin/default skin.png": "archer-female-default",
    "archer female skin/endgame skin.png": "archer-female-endgame",
    "archer female skin/midgame skin.png": "archer-female-midgame",
    "archer female skin/the beast master skin.png": "archer-female-beast-master",
    "archer male skin/123.png": "archer-male-default",
    "mage female skin/arcane donate skin.png": "mage-female-arcane-donate",
    "mage female skin/arcane skin.png": "mage-female-arcane",
    "mage female skin/bikini skin.png": "mage-female-bikini",
    "mage female skin/default skin.png": "mage-female-default",
    "mage female skin/Fire skin.png": "mage-female-fire",
    "mage female skin/frozen skin.png": "mage-female-frozen",
    "mage female skin/krio donate skin.png": "mage-female-krio-donate",
    "mage female skin/mage admin skin.png": "mage-female-admin",
    "mage female skin/middlegame skin.png": "mage-female-midgame",
    "mage female skin/pyro donate skin.png": "mage-female-pyro-donate",
    "mage male skin/ChatGPT Image 8 сент. 2026 г., 19_02_04 (1).png": "mage-male-default",
    "paladin female skin/default skin.png": "paladin-female-default",
    "paladin female skin/paladin admin skin.png": "paladin-female-admin",
    "paladin male skin/default skin.png": "paladin-male-default",
    "warrior female skin/bikini skin.png": "warrior-female-bikini",
    "warrior female skin/commander skin.png": "warrior-female-commander",
    "warrior female skin/default skin.png": "warrior-female-default",
    "warrior female skin/endgame skin.png": "warrior-female-endgame",
    "warrior female skin/fury skin.png": "warrior-female-fury",
    "warrior female skin/guardian skin.png": "warrior-female-guardian",
    "warrior female skin/midgame skin.png": "warrior-female-midgame",
    "warrior female skin/warrior admin.png": "warrior-female-admin",
    "warrior male skin/male.png": "warrior-male-default",
}


def remove_black_background(image: Image.Image) -> Image.Image:
    # The supplied admin portraits use an actual RGB(0,0,0) backdrop, including
    # enclosed spaces between hair, staff, bowstring and cloth. Remove that
    # color throughout while keeping the non-black source pixels untouched.
    rgba = image.convert("RGBA")
    red, green, blue, _ = rgba.split()
    peak = ImageChops.lighter(red, ImageChops.lighter(green, blue))
    rgba.putalpha(peak.point(lambda value: min(255, value * 255 // 14)))
    return rgba


def main() -> None:
    source, destination = (Path(arg) for arg in sys.argv[1:3])
    found = {path.relative_to(source).as_posix() for path in source.rglob("*.png")}
    excluded = {
        "mage male skin/ChatGPT Image 8 сент. 2026 г., 19_02_04 (2).png",
        "warrior male skin/male fon.png",
    }
    if found != set(NAMES) | excluded:
        raise SystemExit(f"Unexpected input inventory: missing={set(NAMES) - found}; extra={found - set(NAMES) - excluded}")
    destination.mkdir(parents=True, exist_ok=True)
    only = set(sys.argv[3:])
    for relative, image_id in NAMES.items():
        if only and image_id not in only:
            continue
        with Image.open(source / relative) as raw:
            if raw.mode != "RGBA":
                image = remove_black_background(raw)
            else:
                image = raw.convert("RGBA")
            if image.getchannel("A").getextrema()[0] != 0:
                raise SystemExit(f"Transparency missing: {relative}")
            image.save(destination / f"{image_id}.webp", "WEBP", quality=84, method=6, exact=True)
            print(f"{relative} -> {image_id}.webp")


if __name__ == "__main__":
    main()
