from pathlib import Path
from tempfile import TemporaryDirectory
from unittest import TestCase

from PIL import Image

from generate_skin_thumbnails import MAX_HEIGHT, MAX_WIDTH, generate


class SkinThumbnailTests(TestCase):
    def test_generates_small_transparent_webp_without_touching_full_asset(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory)
            originals = root / "personal"
            previews = originals / "thumbnails"
            originals.mkdir()
            source = originals / "mage-female-undead-starter.webp"
            with Image.new("RGBA", (1200, 1800), (0, 0, 0, 0)) as image:
                image.paste((160, 25, 30, 255), (50, 50, 1150, 1700))
                image.save(source, "WEBP", lossless=True)

            count, original_size, preview_size = generate(originals, previews)

            self.assertEqual(1, count)
            self.assertGreater(original_size, 0)
            self.assertGreater(preview_size, 0)
            self.assertTrue(source.is_file())
            with Image.open(previews / source.name) as preview:
                self.assertLessEqual(preview.width, MAX_WIDTH)
                self.assertLessEqual(preview.height, MAX_HEIGHT)
                self.assertEqual("RGBA", preview.mode)
                self.assertEqual(0, preview.getpixel((0, 0))[3])

            # Subsequent builds reuse a fresh preview rather than recompress it.
            first_mtime = (previews / source.name).stat().st_mtime_ns
            generate(originals, previews)
            self.assertEqual(first_mtime, (previews / source.name).stat().st_mtime_ns)
