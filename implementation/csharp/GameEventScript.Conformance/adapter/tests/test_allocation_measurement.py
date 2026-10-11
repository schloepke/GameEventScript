# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import support
import copy
import os
from pathlib import Path
import unittest
from unittest.mock import patch
from core.corpus import InvalidDocument, read_corpus
from instrumentation.allocation_measurement import PROFILE, correctness_cases, measurement_request, evidence, run
from core.runner import requests
from core.transport import execute

ROOT = support.ROOT
ADAPTER = os.environ.get('GES_CSHARP_ADAPTER')


class AllocationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [c for c in read_corpus(sorted((ROOT / 'conformance/suites/performance').glob('*.md'))) if c.kind == 'performance' and PROFILE in c.expected['performance']['profiles']]

    def test_requests_do_not_include_baselines(self):
        cases = copy.deepcopy(self.cases)
        before = measurement_request(cases)
        for case in cases:
            case.expected['performance']['profiles'][PROFILE]['metrics']['run.allocated']['maximum'] = 1000
        self.assertEqual(before, measurement_request(cases))
        self.assertEqual(26, len(before['cases']))
        self.assertTrue(all('performance' not in c.metadata and 'performance' not in c.expected for c in correctness_cases(cases)))
        self.assertTrue(all('performance' in c.metadata for c in self.cases))

    def test_failed_correctness_never_starts_measurement(self):
        report = {'summary': {'passed': 0, 'failed': 1, 'error': 0, 'skipped': 0}}
        with patch('instrumentation.allocation_measurement.execute') as process, patch('instrumentation.allocation_measurement.verify', return_value=report):
            result = run(self.cases[:1], ['adapter'])
        self.assertFalse(result['measured'])
        self.assertEqual(1, process.call_count)
        self.assertEqual(1, result['summary']['failed'])

    @unittest.skipUnless(ADAPTER, 'Set GES_CSHARP_ADAPTER to test allocation instrumentation')
    def test_real_counter_and_all_26_workloads(self):
        command = ['dotnet', ADAPTER]
        result = run(self.cases, command)
        self.assertEqual({'passed': 26, 'failed': 0, 'error': 0}, result['summary'])
        case = self.cases[0]
        observed = result['observations']['observations'][case.id]
        for mutate in (lambda e: e['controls'].update(reuse=1),
                       lambda e: e['controls'].update(escaping=0),
                       lambda e: e.update(samples=e['samples'][:2]),
                       lambda e: e.update(instrumentation='RSS'),
                       lambda e: e['samples'][0]['Work'].update(Opcodes=0),
                       lambda e: e['samples'][0].update(Bytes=1)):
            invalid = copy.deepcopy(observed)
            mutate(invalid['evidence'])
            with self.assertRaises(InvalidDocument):
                evidence(case, invalid)
        # A real positive sample, consistently aggregated, is a regression, not an input error.
        regression = copy.deepcopy(observed)
        regression['evidence']['samples'][0]['Bytes'] = 1
        regression['measurement']['metrics'][0]['value'] = '1'
        self.assertEqual('failed', evidence(case, regression)[0])
        for mutate in (lambda entry: entry['measurement'].update(profile='unknown'),
                       lambda entry: entry['measurement'].update(iterations=0),
                       lambda entry: entry['measurement'].update(expectedMaximum=0),
                       lambda entry: entry['runtime']['steps'][0].update(pump='enqueue')):
            request = measurement_request([case])
            mutate(request['cases'][0])
            with self.assertRaises(InvalidDocument):
                execute([*command, '--measure'], request)
        # Measurement fields must not silently activate measurement in ordinary runs.
        with self.assertRaises(InvalidDocument):
            execute(command, measurement_request([case]))
