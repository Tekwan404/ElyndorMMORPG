import pathlib
import subprocess
import sys
import tempfile
import unittest


SCRIPT = pathlib.Path(__file__).resolve().parents[2] / "deploy" / "release-manifest.py"
REVISION = "a" * 40


class ReleaseManifestTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = pathlib.Path(self.temp.name)
        for name in ["Elyndor.Server.dll", "frontend/index.html", "frontend-admin/index.html", "content/package.json"]:
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text("fixture", encoding="utf-8")
        (self.root / "REVISION").write_text(REVISION + "\n", encoding="utf-8")

    def run_manifest(self, action, revision=REVISION):
        return subprocess.run([sys.executable, str(SCRIPT), action, str(self.root), revision],
                              capture_output=True, text=True)

    def create(self):
        result = self.run_manifest("create")
        self.assertEqual(0, result.returncode, result.stderr)

    def test_valid_tree_roundtrip_and_revision_binding(self):
        self.create()
        self.assertEqual(0, self.run_manifest("verify").returncode)
        self.assertNotEqual(0, self.run_manifest("verify", "b" * 40).returncode)

    def test_changed_missing_and_extra_files_are_rejected(self):
        self.create()
        target = self.root / "frontend/index.html"
        target.write_text("tampered", encoding="utf-8")
        self.assertNotEqual(0, self.run_manifest("verify").returncode)
        target.unlink()
        self.assertNotEqual(0, self.run_manifest("verify").returncode)
        target.write_text("fixture", encoding="utf-8")
        (self.root / "unexpected").write_text("extra", encoding="utf-8")
        self.assertNotEqual(0, self.run_manifest("verify").returncode)

    def test_invalid_revision_and_incomplete_tree_rejected(self):
        self.assertNotEqual(0, self.run_manifest("create", "../unsafe").returncode)
        (self.root / "frontend-admin/index.html").unlink()
        self.assertNotEqual(0, self.run_manifest("create").returncode)

    @unittest.skipUnless(sys.platform == "linux", "Linux symlink behavior")
    def test_symlink_rejected_even_if_pointing_to_a_valid_file(self):
        self.create()
        (self.root / "linked").symlink_to(self.root / "frontend/index.html")
        self.assertNotEqual(0, self.run_manifest("verify").returncode)


if __name__ == "__main__":
    unittest.main()
