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
        self.assertTrue(result.accepted("success", "true", ["success"] * 3))
        self.assertTrue(result.accepted("success", "false", ["skipped"] * 3))

    def test_every_required_job_fails_closed(self):
        for position in range(3):
            for state in ("failure", "cancelled", "skipped", None):
                jobs = ["success"] * 3
                jobs[position] = state
                self.assertFalse(result.accepted("success", "true", jobs))

    def test_invalid_scope_and_incomplete_results_fail(self):
        for state in ("failure", "cancelled", "skipped", None):
            self.assertFalse(result.accepted(state, "false", ["skipped"] * 3))
        for scope in ("", None, "unknown"):
            self.assertFalse(result.accepted("success", scope, ["success"] * 3))
        self.assertFalse(result.accepted("success", "false", ["failure"] * 3))
        self.assertFalse(result.accepted("success", "true", []))


if __name__ == "__main__":
    unittest.main()
