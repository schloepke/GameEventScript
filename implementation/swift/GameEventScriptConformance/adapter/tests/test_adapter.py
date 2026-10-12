#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Swift adapter protocol controls; portable behavior remains in Markdown."""
import argparse
import copy
import json
from pathlib import Path
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[5]
ADAPTER = ROOT / 'artifacts/swift/conformance/release/ges-conformance-adapter'


def request():
    return {'formatVersion': 1, 'requestDigest': 'a' * 64, 'cases': [{
        'id': 'adapter/control', 'kind': 'bytecodeSnapshot',
        'sources': [{'name': 'control.ges', 'program': 'main', 'text': 'on Main() { emit Done(1) }'}],
        'compile': {'debugInfo': [], 'binaryRoundTrip': False}, 'requires': {'core': ['compiler'], 'optional': []}}]}


class AdapterTests(unittest.TestCase):
    def run_adapter(self, raw, *args):
        return subprocess.run([str(ADAPTER), *args], input=raw, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=30)

    def test_native_compilation_echoes_identity_and_has_no_verdict(self):
        result = self.run_adapter(json.dumps(request()).encode())
        self.assertEqual(result.returncode, 0, result.stderr)
        body = json.loads(result.stdout)
        self.assertEqual(body['requestDigest'], 'a' * 64)
        self.assertEqual(body['implementation']['id'], 'swift')
        observation = body['observations']['adapter/control']
        self.assertEqual(set(observation), {'status', 'dump'})
        self.assertIn('EmitMessage', observation['dump'])

    def test_rejects_unknown_fields_kinds_options_duplicate_ids_and_sources(self):
        mutations = [lambda r: r.update(expect={}),
                     lambda r: r['cases'][0].update(expect={}),
                     lambda r: r['cases'][0].update(kind='unknown'),
                     lambda r: r['cases'][0]['compile'].update(debugInfo=['unknown']),
                     lambda r: r['cases'][0]['compile'].update(binaryRoundTrip='true'),
                     lambda r: r['cases'].append(copy.deepcopy(r['cases'][0])),
                     lambda r: r['cases'][0]['sources'][0].update(text=None),
                     lambda r: r.update(requestDigest='z' * 64)]
        for mutate in mutations:
            data = request()
            mutate(data)
            with self.subTest(data=data):
                result = self.run_adapter(json.dumps(data).encode())
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(result.stdout, b'')

    def test_rejects_duplicate_json_keys_including_escaped_spelling(self):
        text = json.dumps(request())
        for prefix in ('"formatVersion":1,', '"format\\u0056ersion":1,'):
            result = self.run_adapter(('{' + prefix + text[1:]).encode())
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(result.stdout, b'')

    def test_rejects_trailing_and_excessively_nested_json(self):
        for raw in (json.dumps(request()).encode() + b' {}', b'[' * 70 + b'0' + b']' * 70):
            result = self.run_adapter(raw)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(result.stdout, b'')

    def test_measurement_preflight_rejects_unsupported_work_before_instrumentation(self):
        data = request()
        case = data['cases'][0]
        case['kind'] = 'scriptApi'
        case['runtime'] = {'limits': {}, 'random': {'seed': '0'}, 'hostCount': 1,
                           'nativeHandlers': [], 'deferredPrograms': [], 'stepActions': {},
                           'initializationPump': 'completion', 'publishSink': 'accept',
                           'steps': [{'id': 'main', 'input': {'name': 'Main', 'args': [], 'tags': []},
                                      'pump': 'completion', 'budget': None}]}
        case['measurement'] = {'profile': 'swift-6.4-release-macos26-arm64-m3max', 'iterations': 1,
                               'warmupIterations': 1, 'compileWarmupIterations': 0, 'observeRuntime': False,
                               'metrics': [{'id': 'run.allocated', 'unit': 'B'}]}
        mutations = [lambda c: c['runtime']['steps'][0].update(pump='enqueue'),
                     lambda c: c['runtime'].update(hostCount=2),
                     lambda c: c['runtime'].update(publishSink='throw'),
                     lambda c: c['measurement'].update(iterations=2**32),
                     lambda c: c['measurement']['metrics'][0].update(id='unknown'),
                     lambda c: c['measurement']['metrics'][0].update(unit='ms')]
        for mutate in mutations:
            changed = copy.deepcopy(data)
            mutate(changed['cases'][0])
            result = self.run_adapter(json.dumps(changed).encode(), '--measure')
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(result.stdout, b'')
            self.assertNotIn(b'instrumentation', result.stderr.lower())

    def test_description_and_measurement_rejection(self):
        result = self.run_adapter(b'', '--describe')
        self.assertEqual(result.returncode, 0, result.stderr)
        description = json.loads(result.stdout)
        self.assertTrue(description['supportedBinarySteps'])
        self.assertEqual(set(description['supportedKinds']), {'bytecodeSnapshot', 'bytecode', 'compileError', 'compileMetadata', 'programBinary', 'scriptApi', 'loadError', 'externalTypeApi', 'valueApi', 'messageApi'})
        result = self.run_adapter(json.dumps(request()).encode(), '--measure')
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, b'')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--adapter', type=Path, default=ADAPTER)
    args, rest = parser.parse_known_args()
    ADAPTER = args.adapter.resolve()
    unittest.main(argv=[__file__, *rest])
