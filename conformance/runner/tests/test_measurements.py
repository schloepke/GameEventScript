# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Protocol controls using synthetic observations, without a language adapter."""
import copy
from pathlib import Path
import unittest
from unittest.mock import patch
from core.corpus import read_corpus, InvalidDocument
from measurements import performance
from core import execution

ROOT = Path(__file__).resolve().parents[2]


class MeasurementTests(unittest.TestCase):
    def setUp(self):
        self.case = copy.deepcopy(next(c for c in read_corpus(sorted((ROOT / 'suites').rglob('*.md'))) if c.kind == 'performance'))
        self.case.expected['performance'] = {'profiles': {'unseen-platform': {'metrics': {
            'sample.elapsed': {'reference': 10, 'maximum': 12, 'unit': 'ms'}}}}}
        self.identity = {'id': 'synthetic', 'version': '1'}
        self.correctness = {'implementation': self.identity, 'summary': {'passed': 1}, 'results': []}

    def observation(self, request):
        return {'formatVersion': 1, 'requestDigest': request['requestDigest'], 'implementation': self.identity,
                'capabilities': ['compiler'], 'observations': {self.case.id: {'status': 'ok',
                'measurement': {'profile': 'unseen-platform', 'metrics': [{'id': 'sample.elapsed', 'unit': 'ms', 'value': '11'}]},
                'evidence': {'arbitrary-platform-counter': [42]}}}}

    def test_unseen_profile_uses_generic_measure_command_and_keeps_references_private(self):
        calls = []
        def adapter(command, request, timeout):
            calls.append((command, request))
            return self.observation(request)
        with patch('measurements.performance.execute', side_effect=adapter), patch('measurements.performance.verify', return_value=self.correctness):
            result = performance.run([self.case], ['adapter'], 'unseen-platform', ROOT / 'fixtures')
        self.assertEqual(1, result['summary']['passed'])
        self.assertEqual(['adapter', '--measure'], calls[1][0])
        directive = calls[1][1]['cases'][0]['measurement']
        self.assertEqual([{'id': 'sample.elapsed', 'unit': 'ms'}], directive['metrics'])
        before = performance.measurement_request([self.case], 'unseen-platform', ROOT / 'fixtures')
        self.case.expected['performance']['profiles']['unseen-platform']['metrics']['sample.elapsed']['reference'] = 999
        self.assertEqual(before, performance.measurement_request([self.case], 'unseen-platform', ROOT / 'fixtures'))

    def test_malformed_envelopes_and_metrics_fail(self):
        request = performance.measurement_request([self.case], 'unseen-platform', ROOT / 'fixtures')
        for mutate in (lambda r: r.update(requestDigest='stale'), lambda r: r.update(observations={}),
                       lambda r: r['implementation'].update(id='different'),
                       lambda r: r['observations'][self.case.id]['measurement']['metrics'][0].update(unit='B')):
            actual = self.observation(request)
            actual['implementation'] = dict(self.identity)
            mutate(actual)
            with patch('measurements.performance.execute', side_effect=[{}, actual]), patch('measurements.performance.verify', return_value=self.correctness):
                with self.assertRaises(InvalidDocument):
                    performance.run([self.case], ['adapter'], 'unseen-platform', ROOT / 'fixtures')

    def test_correctness_failure_prevents_measurement(self):
        with patch('measurements.performance.execute') as process, patch('measurements.performance.verify', return_value={'summary': {'passed': 0}}):
            result = performance.run([self.case], ['adapter'], 'unseen-platform', ROOT / 'fixtures')
        self.assertFalse(result['measured'])
        self.assertEqual(1, process.call_count)

    def test_execution_accepts_advertised_profile_without_registry(self):
        description = {'formatVersion': 1, 'implementation': self.identity, 'capabilities': ['compiler'], 'performanceProfiles': ['unseen-platform']}
        measurement = self.observation({'requestDigest': 'irrelevant'})
        result = {'correctness': self.correctness, 'measured': True, 'observations': measurement,
                  'results': [{'id': self.case.id, 'status': 'passed'}]}
        with patch('core.execution.execute', return_value=description), patch('measurements.performance.run', return_value=result):
            report = execution.run([self.case], ['adapter'], ROOT / 'fixtures', profiles=['unseen-platform'])
        self.assertEqual(1, report['summary']['passed'])
