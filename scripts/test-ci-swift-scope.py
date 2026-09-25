#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Exercise CI scope selection, including complete PR diffs and source removals."""

import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location("scope", Path(__file__).with_name("ci-swift-scope.py"))
scope = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scope)


class ScopeTests(unittest.TestCase):
    def test_only_known_documentation_can_skip(self):
        self.assertFalse(scope.requires_swift(["docs/guide/swift.md", "website/src/page.astro", "README.md", ".spi.yml"]))
        for path in ["Package.swift", "implementation/swift/Runtime.swift", "implementation/csharp/Value.cs",
                     "conformance/case.md", "specs/HostRuntime.md", "scripts/test-swift.sh",
                     ".github/workflows/swift-ci.yml", "tools/editors/grammar.json", "new-directory/input"]:
            with self.subTest(path=path):
                self.assertTrue(scope.requires_swift(["README.md", path]))

    def test_manual_and_initial_push_are_full(self):
        self.assertIsNone(scope.changed_paths("workflow_dispatch", {}))
        self.assertIsNone(scope.changed_paths("push", {"before": "0" * 40}))

    def test_complete_pr_and_renamed_source(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)

            def git(*args):
                return subprocess.check_output(["git", *args], cwd=root, stderr=subprocess.DEVNULL, text=True).strip()

            def commit():
                git("add", ".")
                git("-c", "user.name=Scope test", "-c", "user.email=test@example.invalid",
                    "-c", "commit.gpgsign=false", "commit", "-m", "fixture")
                return git("rev-parse", "HEAD")

            git("init")
            (root / "source.swift").write_text("original")
            base = commit()
            (root / "source.swift").write_text("changed")
            previous = commit()
            (root / "README.md").write_text("guide")
            head = commit()
            pr = {"pull_request": {"base": {"sha": base}, "head": {"sha": head}}}
            self.assertTrue(scope.requires_swift(scope.changed_paths("pull_request", pr, root)))
            push = {"before": previous, "after": head}
            self.assertFalse(scope.requires_swift(scope.changed_paths("push", push, root)))
            (root / "docs").mkdir()
            (root / "source.swift").rename(root / "docs/old.swift")
            moved = commit()
            paths = scope.changed_paths("push", {"before": head, "after": moved}, root)
            self.assertIn("source.swift", paths)
            self.assertTrue(scope.requires_swift(paths))
            with self.assertRaises(subprocess.CalledProcessError):
                scope.changed_paths("push", {"before": "missing-ref", "after": moved}, root)


if __name__ == "__main__":
    unittest.main()
