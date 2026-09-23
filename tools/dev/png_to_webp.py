from pathlib import Path
from PIL import Image

ROOT = Path(r"C:\Users\tekwan\Downloads\ELYNDOR\pic\test")

for png in ROOT.rglob("*.png"):
    webp = png.with_suffix(".webp")

    try:
        with Image.open(png) as img:
            img = img.convert("RGBA")
            img.save(
                webp,
                "WEBP",
                quality=90,
                method=6,
            )

        if webp.exists() and webp.stat().st_size > 0:
            png.unlink()
            print(f"[OK] {png.name} -> {webp.name} | PNG deleted")
        else:
            print(f"[ERROR] WebP not created: {png}")

    except Exception as e:
        print(f"[ERROR] {png}: {e}")