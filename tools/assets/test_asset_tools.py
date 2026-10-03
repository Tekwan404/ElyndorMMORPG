import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from PIL import Image


TOOLS = Path(__file__).resolve().parent


class AssetToolTests(unittest.TestCase):
    def test_grid_slicer_supports_png_and_webp_without_modifying_source(self):
        for output_format in ("png", "webp"):
            with self.subTest(format=output_format), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                source = root / "sheet.png"
                Image.new("RGBA", (32, 32), (120, 40, 90, 128)).save(source)
                original = source.read_bytes()
                manifest = root / "manifest.json"
                result = subprocess.run([sys.executable, str(TOOLS / "slice-icons.py"), str(source),
                    "--cols", "2", "--rows", "2", "--count", "3", "--size", "16",
                    "--format", output_format, "--out", str(root / "out"),
                    "--manifest", str(manifest)], capture_output=True, text=True)
                self.assertEqual(result.returncode, 0, result.stderr)
                images = list((root / "out").glob(f"*.{output_format}"))
                self.assertEqual(len(images), 3)
                with Image.open(images[0]) as icon:
                    self.assertEqual(icon.size, (16, 16))
                    self.assertEqual(icon.getchannel("A").getextrema(), (128, 128))
                self.assertEqual(source.read_bytes(), original)
                self.assertEqual(len(json.loads(manifest.read_text())["generated"]), 3)

    def test_batch_preparation_preserves_existing_output_files(self):
        spec = importlib.util.spec_from_file_location("batch_assets", TOOLS / "slice-all-webp-assets.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source = root / "sources"
            source.mkdir()
            Image.new("RGBA", (16, 16), (10, 30, 50, 255)).save(source / "sheet.png")
            destination = root / "out" / "test" / "sheet"
            destination.mkdir(parents=True)
            sentinel = destination / "original.png"
            sentinel.write_bytes(b"preserve-this-source")
            module.process_sheet(root, root / "out", 16, {
                "source_folder": "sources", "tokens": ["sheet"], "group": "test",
                "prefix": "sheet", "cols": 1, "rows": 1, "names": ["icon"],
            })
            self.assertTrue(sentinel.exists(), "Batch preparation deleted an unrelated file")
            self.assertEqual(sentinel.read_bytes(), b"preserve-this-source")


if __name__ == "__main__":
    unittest.main()
