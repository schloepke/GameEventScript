#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Controls for the release verification check, without contacting GitHub."""
import importlib.util
from pathlib import Path
import re
import unittest

spec = importlib.util.spec_from_file_location("release", Path(__file__).with_name("require-verified-release.py"))
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


def run(name, conclusion="success", status="completed", branch="main", jobs="success"):
    return {"name": name, "conclusion": conclusion, "status": status, "head_branch": branch, "jobs": jobs}


def jobs(run):
    return [{"name": name, "conclusion": run["jobs"]} for name in ("changes", "native-packages", "native-conformance", "distribution", "linux", "linux-minimum", "verify")]


class ReleaseVerificationTests(unittest.TestCase):
    def test_both_workflows_succeeded_on_main(self):
        self.assertEqual([], release.missing_workflows([run("C# CI"), run("Swift CI"), run("Website")], jobs=jobs))

    def test_a_later_success_outweighs_an_earlier_failure(self):
        self.assertEqual([], release.missing_workflows([run("C# CI", "failure"), run("C# CI"), run("Swift CI")], jobs=jobs))

    def test_a_swift_run_that_skipped_its_native_jobs_does_not_qualify(self):
        self.assertEqual(["Swift CI"], release.missing_workflows([run("C# CI"), run("Swift CI", jobs="skipped")], jobs=jobs))
        self.assertEqual([], release.missing_workflows([run("C# CI"), run("Swift CI", jobs="skipped"), run("Swift CI")], jobs=jobs))

    def test_every_missing_or_unsuccessful_workflow_blocks(self):
        for runs in ([run("C# CI")],
                     [run("C# CI"), run("Swift CI", "failure")],
                     [run("C# CI"), run("Swift CI", "cancelled")],
                     [run("C# CI"), run("Swift CI", None, "in_progress")],
                     [run("C# CI"), run("Swift CI", branch="feature")],
                     []):
            self.assertNotEqual([], release.missing_workflows(runs, jobs=jobs), runs)

    def test_one_missing_required_job_blocks(self):
        for name in release.REQUIRED_JOBS["Swift CI"]:
            partial = lambda run, name=name: [job for job in jobs(run) if job["name"] != name]
            self.assertEqual(["Swift CI"], release.missing_workflows([run("C# CI"), run("Swift CI")], jobs=partial), name)

    def test_required_jobs_exist_in_the_swift_workflow(self):
        # A renamed job would otherwise block every publication only once a release is tagged.
        workflow = (Path(__file__).parents[1] / ".github/workflows/swift-ci.yml").read_text()
        defined = set(re.findall(r"^  ([A-Za-z0-9_-]+):$", workflow.split("\njobs:\n", 1)[1], re.MULTILINE))
        self.assertLessEqual(set(release.REQUIRED_JOBS["Swift CI"]), defined)


if __name__ == "__main__":
    unittest.main()
