#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Qualify archive boundaries and complete-release selection without publication."""

import importlib.util
import io
import json
import os
import shlex
import subprocess
from pathlib import Path
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.dont_write_bytecode = True
from cli_distribution import TARGETS, archive_name, extract, identity, sha256, verified_set, version

spec = importlib.util.spec_from_file_location("collect", Path(__file__).with_name("collect-cli-release.py"))
collect = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collect)


class DistributionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.release = "0.2.0-rc.1"
        self.revision = "a" * 40

    def populate(self):
        for language, targets in TARGETS.items():
            for target in targets:
                name = archive_name(language, self.release, target)
                path = self.root / name
                path.write_bytes(b"synthetic distribution control")
                receipt = {"archive": name, "version": self.release, "revision": self.revision, "implementation": language,
                           "target": target, "sha256": sha256(path), "verified": True, "sourceClean": True}
                (self.root / (name + ".json")).write_text(json.dumps(receipt))

    def test_container_git_trust_is_limited_to_checkout(self):
        workflow = Path(__file__).resolve().parent.parent / ".github/workflows/cli-distribution.yml"
        linux_job = workflow.read_text().split("  swift-linux:\n", 1)[1]
        container_script = linux_job.split("bash -euc '\n", 1)[1].split("\n            '", 1)[0]
        preparation = container_script.split("swift sdk install", 1)[0]
        commands = [shlex.split(line) for line in preparation.splitlines()
                    if line.strip() and not line.strip().startswith("#")]
        self.assertEqual([["git", "config", "--global", "--add", "safe.directory", "/repo"]], commands)

        # Exercise real Git with an isolated config and simulated foreign ownership,
        # without requiring root privileges or changing the developer's Git settings.
        home = self.root / "home"
        home.mkdir()
        environment = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
        environment.update(HOME=str(home), XDG_CONFIG_HOME=str(home), GIT_CONFIG_NOSYSTEM="1",
                           GIT_CONFIG_GLOBAL=str(home / ".gitconfig"), GIT_TEST_ASSUME_DIFFERENT_OWNER="1")
        checkout = self.root / "checkout"
        unrelated = self.root / "unrelated"
        for repository in (checkout, unrelated):
            subprocess.run(["git", "init", "--quiet", str(repository)], env=environment, check=True)

        def inspect(repository):
            return subprocess.run(["git", "-C", str(repository), "rev-parse", "--show-toplevel"],
                                  env=environment, capture_output=True, text=True)

        self.assertNotEqual(0, inspect(checkout).returncode)
        command = commands[0][:-1] + [str(checkout)]
        subprocess.run(command, env=environment, check=True)
        result = inspect(checkout)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(checkout.resolve(), Path(result.stdout.strip()).resolve())
        self.assertNotEqual(0, inspect(unrelated).returncode)

    def test_version_and_target_validation(self):
        self.assertEqual("0.2.0", version("0.2.0"))
        for value in ["v0.2.0", "../0.2.0", "0.2", "0.2.0\n", "01.2.0", "0.2.0-01", "0.2.0;false"]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                version(value)
        with self.assertRaises(ValueError):
            identity("swift", self.release, "win-x64")

    def test_full_set_requires_every_target(self):
        self.populate()
        self.assertEqual(10, len(verified_set(self.root, self.release, self.revision)))
        next(self.root.glob("*.zip")).unlink()
        with self.assertRaises(ValueError):
            verified_set(self.root, self.release, self.revision)

    def test_extra_or_changed_archive_is_rejected(self):
        self.populate()
        extra = self.root / "unexpected.zip"
        extra.write_bytes(b"extra")
        with self.assertRaises(ValueError):
            verified_set(self.root, self.release, self.revision)
        extra.unlink()
        next(self.root.glob("*.zip")).write_bytes(b"changed after verification")
        with self.assertRaises(ValueError):
            verified_set(self.root, self.release, self.revision)

    def test_receipt_rejects_failed_dirty_or_other_revision_build(self):
        self.populate()
        receipt = next(self.root.glob("*.json"))
        original = json.loads(receipt.read_text())
        for field, value in [("verified", False), ("sourceClean", False), ("revision", "b" * 40), ("version", "0.1.0"), ("target", "other")]:
            receipt.write_text(json.dumps({**original, field: value}))
            with self.subTest(field=field), self.assertRaises(ValueError):
                verified_set(self.root, self.release, self.revision)

    def test_tar_preserves_files_and_executable_mode_after_relocation(self):
        archive = self.root / "valid.tar.gz"
        with tarfile.open(archive, "w:gz") as package:
            info = tarfile.TarInfo("release/bin/ges")
            info.mode, info.size = 0o755, 3
            package.addfile(info, io.BytesIO(b"ges"))
        root = extract(archive, self.root / "another path", "release")
        self.assertEqual(b"ges", (root / "bin/ges").read_bytes())
        if os.name != "nt":
            self.assertEqual(0o755, (root / "bin/ges").stat().st_mode & 0o777)
        with self.assertRaises(ValueError):
            extract(archive, self.root / "another path", "release")

    def test_archive_rejects_traversal_and_links(self):
        for index, name in enumerate(["../escape", "release/../../escape", "/release/file", "other/file", "release/C:/file", "release\\..\\escape"]):
            archive = self.root / f"bad-{index}.zip"
            with zipfile.ZipFile(archive, "w") as package:
                package.writestr(name, b"bad")
            with self.subTest(name=name), self.assertRaises(ValueError):
                extract(archive, self.root / f"out-{index}", "release")
        archive = self.root / "link.tar.gz"
        with tarfile.open(archive, "w:gz") as package:
            info = tarfile.TarInfo("release/link")
            info.type, info.linkname = tarfile.SYMTYPE, "../outside"
            package.addfile(info)
        with self.assertRaises(ValueError):
            extract(archive, self.root / "links", "release")

    def arguments(self, publish=False):
        return ["collect-cli-release.py", self.release, "--revision", self.revision, "--directory", str(self.root), *(["--publish"] if publish else [])]

    def test_dry_run_never_calls_github(self):
        self.populate()
        with patch.object(sys, "argv", self.arguments()), patch.object(collect.subprocess, "run") as run, patch.object(collect.subprocess, "check_output") as output:
            collect.main()
            run.assert_not_called()
            output.assert_not_called()
        self.assertEqual(10, len(next(self.root.glob("*SHA256SUMS.txt")).read_text().splitlines()))

    def test_publication_rejects_branch_and_existing_assets(self):
        self.populate()
        with patch.object(sys, "argv", self.arguments(True)), patch.dict(os.environ, {"GITHUB_REF": "refs/heads/main"}), self.assertRaises(ValueError):
            collect.main()
        release = {"tagName": self.release, "assets": [{"name": archive_name("csharp", self.release, "win-x64")}]}
        with patch.object(sys, "argv", self.arguments(True)), patch.dict(os.environ, {"GITHUB_REF": f"refs/tags/{self.release}", "GITHUB_REPOSITORY": "example/fixture"}), \
                patch.object(collect.subprocess, "check_output", side_effect=[self.revision, self.revision, json.dumps(release)]), \
                patch.object(collect.subprocess, "run") as run, self.assertRaises(ValueError):
            collect.main()
        run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
