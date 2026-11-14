#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Controls for the required aggregate Swift CI check."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("result", Path(__file__).with_name("ci-swift-result.py"))
result = importlib.util.module_from_spec(spec)
spec.loader.exec_module(result)


class ResultTests(unittest.TestCase):
    def test_selected_and_documentation_only_success(self):
        self.assertTrue(result.accepted("success", "true", ["success"] * len(result.JOBS)))
        self.assertTrue(result.accepted("success", "false", ["skipped"] * len(result.JOBS)))

    def test_every_required_job_fails_closed(self):
        for position in range(len(result.JOBS)):
            for state in ("failure", "cancelled", "skipped", None):
                jobs = ["success"] * len(result.JOBS)
                jobs[position] = state
                self.assertFalse(result.accepted("success", "true", jobs))

    def test_invalid_scope_and_incomplete_results_fail(self):
        for state in ("failure", "cancelled", "skipped", None):
            self.assertFalse(result.accepted(state, "false", ["skipped"] * len(result.JOBS)))
        for scope in ("", None, "unknown"):
            self.assertFalse(result.accepted("success", scope, ["success"] * len(result.JOBS)))
        self.assertFalse(result.accepted("success", "false", ["failure"] * len(result.JOBS)))
        self.assertFalse(result.accepted("success", "true", []))


if __name__ == "__main__":
    unittest.main()
