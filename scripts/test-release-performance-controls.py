#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Negative controls for release performance evidence; no benchmarks executed."""
import copy
from pathlib import Path
import runpy
import tempfile
import unittest

GATE = runpy.run_path(str(Path(__file__).with_name('test-release-performance.py')))
EXPECTED = {'suite/case': 'performance'}


def report():
    return {'status': 'passed', 'summary': {'total': 1, 'passed': 1, 'failed': 0, 'skipped': 0, 'error': 0},
            'cases': [{'id': 'suite/case', 'kind': 'performance', 'status': 'passed',
                       'performance': {'metrics': {'run.allocated': {'passed': True, 'measured': '0'}}}}]}


class EvidenceTests(unittest.TestCase):
    def test_accepts_measured_zero(self):
        self.assertEqual(1, GATE['validate_report'](report(), EXPECTED))

    def test_rejects_missing_failed_or_skipped_measurements(self):
        original = report()
        mutations = [
            lambda value: value.update(status='failed'),
            lambda value: value['summary'].update(skipped=1),
            lambda value: value['summary'].update(total=2),
            lambda value: value['cases'][0].update(status='skipped'),
            lambda value: value['cases'][0].update(performance=None),
            lambda value: value['cases'][0]['performance']['metrics'].clear(),
            lambda value: value['cases'][0]['performance']['metrics']['run.allocated'].update(measured=None),
            lambda value: value['cases'][0]['performance']['metrics']['run.allocated'].update(passed=False),
            lambda value: value['cases'][0].update(kind='bytecodeSnapshot'),
        ]
        for mutation in mutations:
            value = copy.deepcopy(original)
            mutation(value)
            with self.subTest(value=value), self.assertRaises(ValueError):
                GATE['validate_report'](value, EXPECTED)

    def test_rejects_duplicate_cases(self):
        value = report()
        value['cases'] *= 2
        value['summary'].update(total=2, passed=2)
        with self.assertRaises(ValueError):
            GATE['validate_report'](value, EXPECTED)

    def test_rejects_consistent_but_incomplete_or_substituted_inventory(self):
        for expected in ({}, {**EXPECTED, 'suite/missing': 'performance'},
                         {'suite/other': 'performance'}, {'suite/case': 'bytecodeSnapshot'}):
            with self.subTest(expected=expected), self.assertRaises(ValueError):
                GATE['validate_report'](report(), expected)

    def test_approved_inventory_includes_every_performance_suite(self):
        inventory = GATE['performance_inventory']()
        self.assertTrue(inventory)
        self.assertIn('performance', inventory.values())
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            suites = root / 'conformance/suites/performance'
            suites.mkdir(parents=True)
            (suites / 'missing.md').write_text('suiteId: performance.missing\n')
            reference = root / 'conformance/cross-language/CSharpReferenceResults.json'
            reference.parent.mkdir()
            reference.write_text('{"cases": []}')
            function = GATE['performance_inventory']
            original = function.__globals__['ROOT']
            try:
                function.__globals__['ROOT'] = root
                with self.assertRaises(ValueError):
                    function()
            finally:
                function.__globals__['ROOT'] = original

    def test_native_results_must_all_execute_and_pass(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'tests.trx'
            for total, passed, result in [(1, 1, 'Passed'), (1, 0, 'NotExecuted'), (1, 0, 'Failed'), (2, 2, 'Passed'), (0, 0, None)]:
                entry = f'<UnitTestResult outcome="{result}" />' if result else ''
                path.write_text(f'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>{entry}</Results>'
                                f'<ResultSummary><Counters total="{total}" passed="{passed}" /></ResultSummary></TestRun>')
                if total == passed == 1:
                    self.assertEqual(1, GATE['validate_trx']([path]))
                else:
                    with self.subTest(total=total, passed=passed, result=result), self.assertRaises(ValueError):
                        GATE['validate_trx']([path])
        with self.assertRaises(ValueError):
            GATE['validate_trx']([])


if __name__ == '__main__':
    unittest.main()
