# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from core.corpus import InvalidDocument, read_corpus, read_document
from core.runner import read_json, requests, snapshot_difference, verify
from core.yaml_subset import parse

ROOT = Path(__file__).resolve().parents[3]


class RunnerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = read_corpus(sorted((ROOT / 'conformance/suites').rglob('*.md')))
        cls.snapshots = [case for case in cls.cases if case.kind == 'bytecodeSnapshot']

    def observations(self, cases=None):
        cases = cases or self.snapshots
        return {'formatVersion': 1, 'requestDigest': requests(cases)['requestDigest'],
                'implementation': {'id': 'test-fixture', 'version': '1'},
                'capabilities': ['compiler', 'program-binary', 'bytecode-snapshot'],
                'observations': {case.id: {'status': 'ok', 'dump': case.assembler} for case in cases}}

    def test_inventory_and_request_isolation(self):
        self.assertGreater(len(self.cases), 1900)
        self.assertEqual(14, len(self.snapshots))
        exported = requests(self.snapshots)
        self.assertNotIn('assembler', exported['cases'][0])
        self.assertNotIn('expected', exported['cases'][0])
        self.assertEqual('compile.program-dumps.compact-bindings.ges', exported['cases'][0]['sources'][0]['name'])
        changed = copy.deepcopy(self.snapshots)
        changed[0].sources[0]['text'] += '\n'
        self.assertNotEqual(exported['requestDigest'], requests(changed)['requestDigest'])

    def test_exact_snapshots_pass_without_runtime(self):
        result = verify(self.snapshots, self.observations())
        self.assertEqual({'passed': 14, 'failed': 0, 'error': 0, 'skipped': 0}, result['summary'])

    def test_negative_snapshot_control_and_unicode(self):
        observed = self.observations()
        observed['observations'][self.snapshots[0].id]['dump'] += ' '
        self.assertEqual(1, verify(self.snapshots, observed)['summary']['failed'])
        self.assertIsNone(snapshot_difference('a\nb\n', 'a\r\nb\r'))
        self.assertIsNotNone(snapshot_difference('é', 'e\u0301'))
        self.assertEqual(2, snapshot_difference('éx', 'éy')['firstDifferentUtf8Byte'])
        self.assertIsNotNone(snapshot_difference('a\n', 'a'))
        self.assertIsNotNone(snapshot_difference('LoadInteger r0, #1', 'LoadInteger r1, #1'))

    def test_missing_core_is_error_optional_is_skip(self):
        for capability, status in [('compiler', 'error'), ('bytecode-snapshot', 'skipped')]:
            observed = self.observations()
            observed['capabilities'].remove(capability)
            self.assertEqual(14, verify(self.snapshots, observed)['summary'][status])
        observed = self.observations()
        observed['capabilities'].remove('program-binary')
        expected = sum(case.metadata.get('compile', {}).get('binaryRoundTrip', False) for case in self.snapshots)
        self.assertEqual(expected, verify(self.snapshots, observed)['summary']['error'])

    def test_missing_observation_and_execution_error_never_pass(self):
        observed = self.observations()
        del observed['observations'][self.snapshots[0].id]
        observed['observations'][self.snapshots[1].id] = {'status': 'error', 'detail': 'compiler crashed'}
        self.assertEqual(2, verify(self.snapshots, observed)['summary']['error'])

    def test_wrong_digest_unknown_cases_or_adapter_pass_fail_rejected(self):
        for mutate in [lambda x: x.update(requestDigest='stale'),
                       lambda x: x['observations'].update(unknown={'status': 'ok', 'dump': ''}),
                       lambda x: x['observations'][self.snapshots[0].id].update(passed=True),
                       lambda x: x.update(formatVersion=True)]:
            observed = self.observations()
            mutate(observed)
            with self.assertRaises(InvalidDocument):
                verify(self.snapshots, observed)

    def test_unsupported_kinds_and_options_rejected(self):
        with self.assertRaises(InvalidDocument):
            requests([next(case for case in self.cases if case.kind == 'performance')])
        for options in [{'debugInfo': ['unknown']}, {'binaryRoundTrip': 1}, {'unknown': True}]:
            cases = copy.deepcopy(self.snapshots[:1])
            cases[0].metadata['compile'] = options
            with self.assertRaises(InvalidDocument):
                requests(cases)

    def test_duplicate_corpus_rejected(self):
        path = ROOT / 'conformance/suites/compile/program-dumps.md'
        with self.assertRaises(InvalidDocument):
            read_corpus([path, path])

    def test_bootstrap_fixtures(self):
        root = ROOT / 'conformance/fixtures/MarkdownV1'
        for path in (root / 'Valid').glob('*.md'):
            self.assertTrue(read_document(path)[1])
        for path in (root / 'Invalid').glob('*.md'):
            with self.assertRaises(InvalidDocument, msg=str(path)):
                read_document(path)

    def test_yaml_rejects_nonportable_or_duplicate_forms(self):
        for text in ['a: 1\na: 2', 'a: {b: 1, b: 2}', 'a: &ref hello', 'a: *ref',
                     'a: !tag value', 'a: |', 'a: >', 'a: .nan', 'a: 0x12', 'a: 2026-10-11',
                     'a:\n   b: 1', 'a:\n\tb: 1', 'a: "\\ud800"', 'a: [1,]', 'a: {x: 1,}',
                     'a: [1', 'a: "x" trailing', 'a: 01']:
            with self.assertRaises(InvalidDocument, msg=text):
                parse(text)

    def test_yaml_keeps_exact_numbers_and_booleans(self):
        value = parse('a: 9223372036854775807\nb: true\nc: 0.100000000000000000001\nd: "1"\ne: [1, {key: \'it\'\'s\'}]')
        self.assertIs(type(value['a']), int)
        self.assertIs(type(value['b']), bool)
        self.assertEqual('0.100000000000000000001', str(value['c']))
        self.assertEqual('1', value['d'])
        self.assertEqual("it's", value['e'][1]['key'])

    def test_strict_json(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'input.json'
            for text in ['{"a":1,"a":2}', '{"a":NaN}', '{"a":"\\ud800"}', '{"a":1e999}']:
                path.write_text(text)
                with self.assertRaises((ValueError, UnicodeError)):
                    read_json(path)

    def test_cli_pass_failure_and_error_exit_codes(self):
        with tempfile.TemporaryDirectory() as directory:
            observations = Path(directory) / 'observations.json'
            output = Path(directory) / 'report.json'
            data = self.observations()
            command = [sys.executable, str(Path(__file__).resolve().parents[1] / 'main.py'), 'verify', '--kind', 'bytecodeSnapshot',
                       '--observations', str(observations), '--output', str(output)]
            for expected in [0, 1, 2, 3]:
                if expected == 1:
                    data['observations'][self.snapshots[0].id]['dump'] += 'bad'
                if expected == 2:
                    del data['observations'][self.snapshots[1].id]
                if expected == 3:
                    data = self.observations()
                    data['capabilities'].remove('bytecode-snapshot')
                observations.write_text(json.dumps(data))
                result = subprocess.run(command, capture_output=True, text=True)
                self.assertEqual(expected, result.returncode, result.stderr)
                self.assertEqual(14, json.loads(output.read_text())['summary']['total'])


if __name__ == '__main__':
    unittest.main()
