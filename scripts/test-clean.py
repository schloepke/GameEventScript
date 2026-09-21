#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Safety and scope checks for the repository cleanup script, using disposable trees."""

from pathlib import Path
import tempfile
import sys
import shutil
import unittest

sys.dont_write_bytecode = True
from clean import clean


class CleanTests(unittest.TestCase):
    def setUp(self):
        self.workspace = tempfile.TemporaryDirectory()
        self.addCleanup(self.workspace.cleanup)
        self.root = Path(self.workspace.name) / "repository"
        self.root.mkdir()
        self.write("AGENTS.md")
        self.write("implementation/csharp/GameEventScript.Runtime/src/Runtime.cs")
        self.write("implementation/swift/Runtime/Sources/Runtime.swift")
        self.output = []

    def write(self, path, content="data"):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content)
        return target

    def run_clean(self, **options):
        return clean(self.root, tracked=options.pop("tracked", set()), report=self.output.append, **options)

    def test_removes_only_build_outputs_and_is_idempotent(self):
        outputs = [
            "artifacts/release/package.nupkg", "bin/local.dll", "obj/project.assets.json",
            "TestResults/results.trx", "implementation/csharp/GameEventScript.Runtime/src/bin/Release/a.dll",
            "implementation/csharp/GameEventScript.Runtime/tests/obj/cache", "implementation/csharp/GameEventScript.Runtime/tests/TestResults/log",
            "implementation/swift/Runtime/.build/release/library", ".build/cache",
        ]
        kept = [
            "conformance/fixtures/canonical.gesb", "implementation/swift/Runtime/.swiftpm/configuration",
            "docs/example.ges", ".git/config", "implementation/csharp/GameEventScript.Runtime/src/Runtime.cs",
        ]
        for path in outputs + kept:
            self.write(path)
        self.assertEqual(len(self.run_clean()), 9)
        for path in outputs:
            self.assertFalse((self.root / path).exists(), path)
        for path in kept:
            self.assertTrue((self.root / path).exists(), path)
        self.assertEqual(self.run_clean(), [])

    def test_dry_run_does_not_delete(self):
        item = self.write("artifacts/cache")
        self.assertEqual(self.run_clean(dry_run=True), [self.root / "artifacts"])
        self.assertTrue(item.exists())
        self.assertTrue(any("Would remove:" in line for line in self.output))

    def test_artifacts_only_leaves_other_outputs(self):
        self.write("artifacts/cache")
        item = self.write("implementation/csharp/bin/cache")
        self.run_clean(artifacts_only=True)
        self.assertTrue(item.exists())
        self.assertFalse((self.root / "artifacts").exists())

    def test_symlinks_never_delete_external_content(self):
        external = Path(self.workspace.name) / "external"
        external.mkdir()
        sentinel = external / "keep"
        sentinel.write_text("keep")
        (self.root / "artifacts").symlink_to(external, target_is_directory=True)
        (self.root / "implementation/csharp/bin").symlink_to(external, target_is_directory=True)
        (self.root / "implementation/swift/Linked").symlink_to(external, target_is_directory=True)
        self.run_clean()
        self.assertTrue(sentinel.exists())
        self.assertTrue((self.root / "artifacts").is_symlink())
        (self.root / "artifacts").unlink()
        self.write("artifacts/cache")
        (self.root / "artifacts/link").symlink_to(external, target_is_directory=True)
        (self.root / "artifacts/file").symlink_to(sentinel)
        self.run_clean()
        self.assertTrue(sentinel.exists())
        self.assertFalse((self.root / "artifacts").exists())

    def test_symlinked_language_directory_is_not_walked(self):
        external = Path(self.workspace.name) / "external"
        (external / "bin").mkdir(parents=True)
        sentinel = external / "bin/keep"
        sentinel.write_text("keep")
        shutil.rmtree(self.root / "implementation/csharp")
        (self.root / "implementation/csharp").symlink_to(external, target_is_directory=True)
        self.run_clean()
        self.assertTrue(sentinel.exists())

    def test_tracked_content_prevents_all_deletion(self):
        artifact = self.write("artifacts/cache")
        tracked = self.write("implementation/csharp/bin/source.cs")
        with self.assertRaisesRegex(RuntimeError, "Refusing to delete tracked content"):
            self.run_clean(tracked={"implementation/csharp/bin/source.cs"})
        self.assertTrue(artifact.exists())
        self.assertTrue(tracked.exists())


if __name__ == "__main__":
    unittest.main()
