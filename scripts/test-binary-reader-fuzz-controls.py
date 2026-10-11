#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Qualify deterministic mutation generation and fail-closed process handling."""
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('fuzz', Path(__file__).with_name('test-binary-reader-fuzz.py'))
fuzz = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fuzz)


class FuzzControls(unittest.TestCase):
    def test_reproducible_bounded_mutations(self):
        first = list(fuzz.cases(fuzz.SEED, 128))
        self.assertEqual(first, list(fuzz.cases(fuzz.SEED, 128)))
        self.assertNotEqual(first, list(fuzz.cases(fuzz.SEED + 1, 128)))
        self.assertEqual(136, len(first))
        self.assertTrue(all(len(data) <= fuzz.MAX_BYTES + 1 for _, data in first))
        self.assertEqual(set(range(7)), {meta['operation'] for meta, _ in first[8:]})

    def test_reject_all_accept_all_and_missing_output_fail_controls(self):
        rows = [({'expected': 'accept'}, b'valid'), ({'expected': 'reject'}, b'bad')]
        for responses in ([], ['reject:invalid'] * 2, ['accept:YWJj'] * 2,
                          ['accept:?', 'reject:error'], ['unexpected', 'reject:error']):
            with self.assertRaises((AssertionError, ValueError)):
                fuzz.validate(rows, responses)
        fuzz.validate(rows, ['accept:YWJj', 'reject:invalid'])

    def test_cross_language_comparison_detects_disagreement(self):
        fuzz.compare(['reject:one', 'accept:YWJj'], ['reject:two', 'accept:YWJj'])
        for other in (['accept:YWJj'], ['accept:ZGVm']):
            with self.assertRaises(AssertionError):
                fuzz.compare(['reject:error'] if other == ['accept:YWJj'] else ['accept:YWJj'], other)
        with self.assertRaises(ValueError):
            fuzz.compare(['reject:error'], [])

    def test_crashes_and_timeouts_fail_and_keep_inputs(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(fuzz, 'OUTPUT', Path(directory)):
            rows = [({}, b'crashing input')]
            with self.assertRaises(subprocess.CalledProcessError):
                fuzz.run_adapter([sys.executable, '-c', 'raise SystemExit(7)'], 'crash', rows, 2)
            with self.assertRaises(subprocess.TimeoutExpired):
                fuzz.run_adapter([sys.executable, '-c', 'import time; time.sleep(10)'], 'timeout', rows, 0.1)
            self.assertTrue((Path(directory) / 'crash.input.txt').is_file())
            self.assertTrue((Path(directory) / 'timeout.input.txt').is_file())


if __name__ == '__main__':
    unittest.main()
