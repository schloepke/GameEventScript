# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Synthetic oracle controls, explicitly not runtime performance evidence."""
import copy
import math
import unittest
from pathlib import Path
from core.corpus import InvalidDocument, read_corpus
from checks.performance_checks import prepare, profiles, compare, upper_bound, workload


class PerformanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [c for c in read_corpus(sorted((Path(__file__).resolve().parents[2] / 'suites/performance').glob('*.md'))) if c.kind == 'performance']

    def sample(self, metric=None):
        case = copy.deepcopy(self.cases[0])
        case.expected['performance'] = {'profiles': {'test-profile': {'metrics': {'run.elapsed': metric or {'reference': 10, 'maximum': 12, 'unit': 'ms'}}}}}
        return case

    def observation(self, value, unit='ms'):
        return {'profile': 'test-profile', 'metrics': [{'id': 'run.elapsed', 'unit': unit, 'value': value}]}

    def test_all_36_workloads_and_every_authored_profile_validate(self):
        self.assertEqual(36, len(self.cases))
        for case in self.cases:
            for profile, data in profiles(case).items():
                with self.subTest(case=case.id, profile=profile):
                    request = prepare(case, profile)
                    self.assertEqual(len(data['metrics']), len(request['metrics']))
                    self.assertTrue(all(set(m) == {'id', 'unit'} for m in request['metrics']))

    def test_strictest_bound_and_improvements(self):
        metric = {'reference': 10, 'maximum': 20, 'toleranceRelative': .2, 'toleranceAbsolute': 1, 'unit': 'ms'}
        self.assertEqual(11, upper_bound(metric))
        case = self.sample(metric)
        for value in ('0', '10', '11'):
            self.assertEqual('passed', compare(case, 'test-profile', self.observation(value))[0])
        self.assertEqual('failed', compare(case, 'test-profile', self.observation(str(math.nextafter(11, math.inf))))[0])

    def test_zero_allocation_never_rounds_positive_measurement(self):
        case = self.sample({'reference': 0, 'maximum': 0, 'unit': 'B'})
        self.assertEqual('passed', compare(case, 'test-profile', self.observation('0', 'B'))[0])
        self.assertEqual('failed', compare(case, 'test-profile', self.observation('5e-324', 'B'))[0])

    def test_baselines_are_not_measurement_inputs(self):
        case = self.sample()
        before = prepare(case, 'test-profile')
        case.expected['performance']['profiles']['test-profile']['metrics']['run.elapsed'].update(reference=100, maximum=200)
        self.assertEqual(before, prepare(case, 'test-profile'))

    def test_invalid_workload_counts_and_observer(self):
        for field in ('iterations', 'warmupIterations', 'compileWarmupIterations'):
            for invalid in (True, -1, 2**32, 1.5, '1'):
                case = self.sample()
                case.metadata['performance'][field] = invalid
                with self.assertRaises(InvalidDocument):
                    workload(case)
        for invalid in (0, 'false', None):
            case = self.sample()
            case.metadata['performance']['observeRuntime'] = invalid
            with self.assertRaises(InvalidDocument):
                workload(case)
        case.metadata['performance'] = {'iterations': 0}
        with self.assertRaises(InvalidDocument):
            workload(case)

    def test_invalid_measurements_are_errors_not_regressions(self):
        case = self.sample()
        for value in ('NaN', 'Infinity', '-1', '1e999', True, 1):
            with self.assertRaises(InvalidDocument):
                compare(case, 'test-profile', self.observation(value))
        for unit in ('s', 'kB', None):
            with self.assertRaises(InvalidDocument):
                compare(case, 'test-profile', self.observation('10', unit))
        for metrics in ([], self.observation('10')['metrics'] * 2, [{'id': 'other', 'unit': 'ms', 'value': '1'}]):
            with self.assertRaises(InvalidDocument):
                compare(case, 'test-profile', {'profile': 'test-profile', 'metrics': metrics})

    def test_profiles_and_metric_schema_are_strict(self):
        case = self.sample()
        with self.assertRaisesRegex(InvalidDocument, 'missingPerformanceProfile'):
            prepare(case, 'unknown')
        for metric in ({'reference': 1, 'unit': 'ms'}, {'reference': -1, 'maximum': 1, 'unit': 'ms'},
                       {'reference': True, 'maximum': 1, 'unit': 'ms'}, {'reference': 1, 'maximum': 1, 'unit': 'bytes'},
                       {'reference': 1, 'maximum': float('inf'), 'unit': 'ms'}):
            with self.assertRaises(InvalidDocument):
                prepare(self.sample(metric), 'test-profile')
        with self.assertRaises(InvalidDocument):
            compare(case, 'test-profile', {'profile': 'other', 'metrics': []})
