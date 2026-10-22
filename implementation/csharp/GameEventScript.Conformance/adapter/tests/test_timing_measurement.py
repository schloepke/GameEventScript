# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import support
import copy
import os
from pathlib import Path
import unittest
from unittest.mock import patch
from core.corpus import InvalidDocument, read_corpus
from instrumentation.timing_measurement import PROFILE, measurement_request, evidence, run
from instrumentation.allocation_measurement import PROFILE as ALLOCATION_PROFILE
from core.transport import execute

ROOT = support.ROOT
ADAPTER = os.environ.get('GES_CSHARP_ADAPTER')


class TimingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [c for c in read_corpus(sorted((ROOT / 'conformance/suites/performance').glob('*.md'))) if c.kind == 'performance' and ALLOCATION_PROFILE not in c.expected['performance']['profiles']]

    def test_ten_remaining_workloads_and_no_references_in_request(self):
        self.assertEqual(10, len(self.cases))
        cases = copy.deepcopy(self.cases)
        before = measurement_request(cases)
        for case in cases:
            for metric in case.expected['performance']['profiles'][PROFILE]['metrics'].values():
                metric['reference'] = 12345
        self.assertEqual(before, measurement_request(cases))

    def test_correctness_failure_prevents_timing(self):
        report = {'summary': {'passed': 0, 'failed': 1, 'error': 0, 'skipped': 0}}
        with patch('instrumentation.timing_measurement.execute') as process, patch('instrumentation.timing_measurement.verify', return_value=report):
            result = run(self.cases[:1], ['adapter'])
        self.assertFalse(result['measured'])
        self.assertEqual(1, process.call_count)

    @unittest.skipUnless(ADAPTER and os.environ.get('GES_MEASURE_TIMING'), 'Opt in to macOS ARM64 timing instrumentation')
    def test_real_phases_and_negative_evidence_controls(self):
        command = ['dotnet', ADAPTER]
        result = run(self.cases, command)
        # Threshold regressions remain visible, but do not invalidate the instrument tests.
        self.assertEqual(0, result['summary']['error'])
        self.assertEqual(10, result['summary']['passed'] + result['summary']['failed'])
        case = self.cases[0]
        observed = result['observations']['observations'][case.id]
        for mutate in (lambda e: e.update(ast=e['ast'][:2]),
                       lambda e: e['load'][0].update(Bytes=-1),
                       lambda e: e['binary'][0].update(ElapsedMilliseconds=float('nan')),
                       lambda e: e['run']['evidence'].update(architecture='X64')):
            invalid = copy.deepcopy(observed)
            mutate(invalid['evidence'])
            with self.assertRaises(InvalidDocument):
                evidence(case, invalid)
        invalid = copy.deepcopy(observed)
        invalid['measurement']['metrics'][0]['value'] = '123456'
        with self.assertRaises(InvalidDocument):
            evidence(case, invalid)
        for mutate in (lambda m: m.update(profile='other'),
                       lambda m: m['metrics'][0].update(unit='s'),
                       lambda m: m['metrics'][0].update(id='unknown.elapsed')):
            request = measurement_request([case])
            mutate(request['cases'][0]['measurement'])
            with self.assertRaises(InvalidDocument):
                execute([*command, '--measure'], request)
