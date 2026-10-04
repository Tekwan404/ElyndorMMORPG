import os
import pathlib
import shutil
import subprocess
import sys
import tempfile
import unittest

REPO = pathlib.Path(__file__).resolve().parents[2]
ROOT = pathlib.Path("/opt/elyndor")
REVISION = "a" * 40


@unittest.skipUnless(sys.platform == "linux" and os.geteuid() == 0, "Run in an isolated root Linux container")
class InstallReleaseTests(unittest.TestCase):
    def setUp(self):
        # Never delete a pre-existing deployment, even on a developer's machine.
        if ROOT.exists():
            self.skipTest("/opt/elyndor already exists")
        self.addCleanup(shutil.rmtree, ROOT, True)
        self.temp = tempfile.TemporaryDirectory(prefix=f"elyndor-{REVISION}.")
        self.addCleanup(self.temp.cleanup)
        self.tree = pathlib.Path(self.temp.name)
        self.bin = self.tree.parent / (self.tree.name + "-commands")
        self.bin.mkdir()
        self.addCleanup(shutil.rmtree, self.bin)
        subprocess.run(["groupadd", "-f", "elyndor"], check=True)
        verifier = pathlib.Path("/usr/local/lib/elyndor/release-manifest.py")
        verifier.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(REPO / "deploy/release-manifest.py", verifier)
        for name, code in {"systemctl": 'exit "${RESTART_FAIL:-0}"',
                           "curl": '[[ "${HEALTH_FAIL:-0}" == 0 ]] && echo \'{"status":"ready"}\' || exit 1',
                           "sleep": "exit 0"}.items():
            command = self.bin / name
            command.write_text("#!/bin/bash\n" + code + "\n")
            command.chmod(0o755)
        self.env = {**os.environ, "PATH": str(self.bin) + ":" + os.environ["PATH"]}
        self.old = ROOT / "releases/old"
        self.old.mkdir(parents=True)
        (self.old / "sentinel").write_text("original")
        (ROOT / "current").symlink_to(self.old)
        for name in ["Elyndor.Server.dll", "frontend/index.html", "frontend-admin/index.html", "content/package.json"]:
            target = self.tree / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text("fixture")
        (self.tree / "REVISION").write_text(REVISION + "\n")
        subprocess.run([sys.executable, str(REPO / "deploy/release-manifest.py"), "create", str(self.tree), REVISION], check=True)

    def install(self, **variables):
        return subprocess.run(["bash", str(REPO / "deploy/install-release.sh"), str(self.tree), REVISION],
                              env={**self.env, **variables}, capture_output=True, text=True)

    def test_success_switches_atomically_without_shared_inodes(self):
        result = self.install()
        self.assertEqual(0, result.returncode, result.stderr)
        current = (ROOT / "current").resolve()
        self.assertNotEqual(self.old, current)
        self.assertEqual(REVISION, (current / "REVISION").read_text().strip())
        (self.tree / "Elyndor.Server.dll").write_text("later mutation")
        self.assertEqual("fixture", (current / "Elyndor.Server.dll").read_text())
        self.assertEqual("original", (self.old / "sentinel").read_text())

    def test_corruption_never_switches_current(self):
        (self.tree / "frontend/index.html").write_text("corrupt")
        self.assertNotEqual(0, self.install().returncode)
        self.assertEqual(self.old, (ROOT / "current").resolve())
        self.assertEqual([self.old], list((ROOT / "releases").iterdir()))

    def test_failed_health_check_rolls_back_and_keeps_previous(self):
        self.assertNotEqual(0, self.install(HEALTH_FAIL="1").returncode)
        self.assertEqual(self.old, (ROOT / "current").resolve())
        self.assertEqual("original", (self.old / "sentinel").read_text())

    def test_failed_restart_also_rolls_back(self):
        self.assertNotEqual(0, self.install(RESTART_FAIL="1").returncode)
        self.assertEqual(self.old, (ROOT / "current").resolve())

    def test_first_deployment_and_first_deployment_failure(self):
        (ROOT / "current").unlink()
        self.assertNotEqual(0, self.install(HEALTH_FAIL="1").returncode)
        self.assertFalse((ROOT / "current").exists())
        result = self.install()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(REVISION, (ROOT / "current/REVISION").read_text().strip())

    def test_revision_mismatch_keeps_current(self):
        (self.tree / "REVISION").write_text("b" * 40)
        self.assertNotEqual(0, self.install().returncode)
        self.assertEqual(self.old, (ROOT / "current").resolve())

    def test_rsync_transfers_changed_files_only_and_removes_obsolete_files(self):
        old = self.tree.parent / (self.tree.name + "-basis")
        stage = self.tree.parent / (self.tree.name + "-stage")
        shutil.copytree(self.tree, old)
        stage.mkdir()
        self.addCleanup(shutil.rmtree, old)
        self.addCleanup(shutil.rmtree, stage)
        (old / "obsolete").write_text("old")
        (self.tree / "frontend/index.html").write_text("new web")
        result = subprocess.run(["rsync", "-rlt", "--checksum", "--delete", "--stats", f"--copy-dest={old}",
                                 str(self.tree) + "/", str(stage) + "/"], capture_output=True, text=True, check=True)
        self.assertIn("Number of regular files transferred: 1", result.stdout)
        self.assertFalse((stage / "obsolete").exists())
        self.assertEqual("new web", (stage / "frontend/index.html").read_text())
        self.assertEqual("fixture", (old / "frontend/index.html").read_text())


if __name__ == "__main__":
    unittest.main()
