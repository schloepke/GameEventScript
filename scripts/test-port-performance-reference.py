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
        self.assertEqual((True, 1.0, []), reference.evaluate(base, base, base))

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

    def test_empty_measurements_fail_instead_of_passing_neutrally(self):
        frozen = reference.summarize([result(10, 5)])
        empty = reference.summarize([{"cases": []}])
        passed, mean, problems = reference.evaluate(frozen, empty, empty)
        self.assertFalse(passed)
        self.assertIsNone(mean)
        self.assertIn("candidate produced no measurements", problems)
        self.assertIn("elapsed reference produced no measurements", problems)

    def test_interleaved_mode_still_checks_frozen_metric_coverage(self):
        frozen = reference.summarize([result(10, 5)])
        # Baseline and candidate agree with each other but both lack the frozen allocation metric.
        partial = {key: value for key, value in reference.summarize([result(10, 5)]).items() if not key.endswith("run.allocated")}
        passed, _, problems = reference.evaluate(frozen, partial, partial)
        self.assertFalse(passed)
        self.assertEqual(["missing metric suite/case-1|run.allocated"], problems)

    def test_metrics_missing_from_individual_runs_fail(self):
        frozen = reference.summarize([result(10, 5)])
        # One successful run and four runs whose workload errored without measurements.
        partial = reference.summarize([result(10, 5)] + [{"cases": [{"id": "suite/case-1", "status": "error"}]}] * 4)
        self.assertEqual((True, 1.0, []), reference.evaluate(frozen, frozen, partial))
        passed, _, problems = reference.evaluate(frozen, frozen, partial, runs=5)
        self.assertFalse(passed)
        self.assertIn("candidate metric missing in some runs: suite/case-1|run.elapsed", problems)
        passed, _, problems = reference.evaluate(frozen, partial, frozen, runs=1, reference_runs=5)
        self.assertFalse(passed)
        self.assertIn("baseline metric missing in some runs: suite/case-1|run.elapsed", problems)

    def test_workload_failing_in_every_baseline_run_fails(self):
        def two_cases(first, second):
            document = result(first, 5)
            document["cases"][1] = {"id": "suite/case-2", "performance": {"metrics": {"run.elapsed": {"measured": str(second), "unit": "ms"}}}}
            return document

        frozen = reference.summarize([two_cases(10, 10)])
        # case-2 errors in all five baseline runs, so it vanishes from the summary instead of looking incomplete.
        baseline = reference.summarize([result(10, 5)] * 5)
        candidate = reference.summarize([two_cases(10, 1000)] * 5)
        passed, mean, problems = reference.evaluate(frozen, baseline, candidate, runs=5, reference_runs=5)
        self.assertFalse(passed)
        self.assertEqual(1.0, mean)
        self.assertEqual(["baseline missing elapsed metric suite/case-2|run.elapsed"], problems)


    def test_baseline_adapter_uses_the_current_profile_name(self):
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / reference.SWIFT_MEASUREMENT
            path.parent.mkdir(parents=True)
            path.write_text('static let profile = "swift-6.4-release-macos26-arm64-m3max"\nmajorVersion == 26\n', encoding="utf-8")
            self.assertTrue(reference.align_profiles(directory))
            self.assertEqual('static let profile = "swift-6.4-release-macos27-arm64-m3max"\nmajorVersion == 27\n', path.read_text(encoding="utf-8"))
            self.assertFalse(reference.align_profiles(directory))


if __name__ == "__main__":
    unittest.main()
