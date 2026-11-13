#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Controls for the release verification check, without contacting GitHub."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("release", Path(__file__).with_name("require-verified-release.py"))
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


def run(name, conclusion="success", status="completed", branch="main"):
    return {"name": name, "conclusion": conclusion, "status": status, "head_branch": branch}


class ReleaseVerificationTests(unittest.TestCase):
    def test_both_workflows_succeeded_on_main(self):
        self.assertEqual([], release.missing_workflows([run("C# CI"), run("Swift CI"), run("Website")]))

    def test_a_later_success_outweighs_an_earlier_failure(self):
        self.assertEqual([], release.missing_workflows([run("C# CI", "failure"), run("C# CI"), run("Swift CI")]))

    def test_every_missing_or_unsuccessful_workflow_blocks(self):
        for runs in ([run("C# CI")],
                     [run("C# CI"), run("Swift CI", "failure")],
                     [run("C# CI"), run("Swift CI", "cancelled")],
                     [run("C# CI"), run("Swift CI", None, "in_progress")],
                     [run("C# CI"), run("Swift CI", branch="feature")],
                     []):
            self.assertNotEqual([], release.missing_workflows(runs), runs)


if __name__ == "__main__":
    unittest.main()
