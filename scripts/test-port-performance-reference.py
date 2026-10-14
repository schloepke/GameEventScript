#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Controls for the port convergence performance reference comparison."""

import importlib.util
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location("reference", Path(__file__).with_name("port-performance-reference.py"))
reference = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reference)


def result(elapsed, allocated, zero=0):
    return {"cases": [{"id": "suite/case-1", "performance": {"metrics": {
        "run.elapsed": {"measured": str(elapsed), "unit": "ms"},
        "run.per-invoke-elapsed": {"measured": str(elapsed / 10), "unit": "ms/iteration"},
        "run.allocated": {"measured": str(allocated), "unit": "KiB"},
        "dispatch.allocations": {"measured": str(zero), "unit": "count"},
    }}}, {"id": "suite/case-2"}]}


class ReferenceTests(unittest.TestCase):
    def test_summary_uses_median_and_spread(self):
        summary = reference.summarize([result(10, 5), result(30, 5), result(11, 5)])
        self.assertEqual(11, summary["suite/case-1|run.elapsed"]["median"])
        self.assertEqual((10, 30, 3), tuple(summary["suite/case-1|run.elapsed"][k] for k in ("min", "max", "runs")))

    def test_unchanged_candidate_passes(self):
        base = reference.summarize([result(10, 5)])
        self.assertEqual((1.0, [], []), reference.compare(base, base))

    def test_elapsed_budget_uses_geometric_mean(self):
        base = reference.summarize([result(10, 5)])
        mean, regressions, missing = reference.compare(base, reference.summarize([result(10.5, 5)]))
        self.assertAlmostEqual(1.05, mean)
        self.assertGreater(mean, reference.ELAPSED_BUDGET)
        self.assertEqual(([], []), (regressions, missing))

    def test_allocation_growth_and_nonzero_dispatch_fail(self):
        base = reference.summarize([result(10, 5)])
        _, regressions, _ = reference.compare(base, reference.summarize([result(10, 6, zero=1)]))
        self.assertEqual({"suite/case-1|run.allocated", "suite/case-1|dispatch.allocations"}, {r[0] for r in regressions})

    def test_missing_metric_is_reported(self):
        base = reference.summarize([result(10, 5)])
        candidate = reference.summarize([{"cases": []}])
        self.assertEqual(sorted(base), reference.compare(base, candidate)[2])


if __name__ == "__main__":
    unittest.main()
