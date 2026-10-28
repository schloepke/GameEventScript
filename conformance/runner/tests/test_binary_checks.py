# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import base64
import copy
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch

from checks import binary_checks as binary
from core.corpus import InvalidDocument, read_corpus
from core.runner import requests, supported

ROOT = Path(__file__).resolve().parents[3]


class BinaryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [c for c in read_corpus(sorted((ROOT / 'conformance/suites').rglob('*.md')))
                     if c.kind == 'programBinary']

    def test_inventory_and_requests(self):
        cases = [c for c in self.cases if supported(c)]
        self.assertEqual(162, len(cases))
        for entry in requests(cases)['cases']:
            self.assertEqual({'data', 'includeDump', 'compareCompiledRuntime', 'programVersion', 'runtime'}, set(entry['binary']))
            self.assertNotIn('binaryFixture', entry)
            self.assertNotIn('expected', entry)
            if not entry['binary']['compareCompiledRuntime']:
                self.assertEqual([], entry['sources'])
        self.assertEqual(37, sum(bool(c.steps) for c in cases))

    def test_error_catalog_matches_spec(self):
        text = (ROOT / 'specs/BinaryFormat.md').read_text().split('The portable numeric error codes are fixed as follows:', 1)[1].split('The writer exposes', 1)[0]
        pairs = re.findall(r'\| (\d+) \| (\w+) ', text)
        self.assertEqual({i + 1: name for i, name in enumerate(binary.ERRORS)},
                         {int(i): name for i, name in pairs})

    def test_resource_integrity_paths_and_bounds(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'fixture.gesb').write_bytes(b'fixture')
            fixture = {'relativePath': 'fixture.gesb', 'sha256': binary.sha(b'fixture')}
            self.assertEqual(b'fixture', binary.fixture_bytes(fixture, root))
            for path in ['../fixture.gesb', '/fixture.gesb', 'x//fixture.gesb', './fixture.gesb', 'C:/fixture.gesb', 'x\\fixture.gesb']:
                with self.assertRaises(InvalidDocument):
                    binary.fixture_bytes({**fixture, 'relativePath': path}, root)
            with tempfile.TemporaryDirectory() as outside:
                (Path(outside) / 'escape').write_bytes(b'fixture')
                try:
                    (root / 'link').symlink_to(Path(outside) / 'escape')
                except (OSError, NotImplementedError):
                    pass  # Windows may not grant symlink creation to the test account.
                else:
                    with self.assertRaises(InvalidDocument):
                        binary.fixture_bytes({**fixture, 'relativePath': 'link'}, root)
            with self.assertRaises(InvalidDocument):
                binary.fixture_bytes({**fixture, 'sha256': '0' * 64}, root)
            with patch('checks.binary_checks.MAX_RESOURCE_BYTES', 3):
                with self.assertRaises(InvalidDocument):
                    binary.fixture_bytes(fixture, root)
            for value in ['!', 'YQ', 'YR==', 3]:
                with self.assertRaises(InvalidDocument):
                    binary.bytes_from(value)

    def test_error_context_mismatch_and_execution_failure(self):
        case = copy.deepcopy(next(c for c in self.cases if c.expected['binary']['outcome'] == 'readError'))
        case.expected['binary'] = {'outcome': 'readError', 'errorCode': 'InvalidMagic', 'byteOffset': 0, 'sectionType': 2, 'entryIndex': 3}
        observed = {'status': 'ok', 'binary': dict(case.expected['binary'])}
        self.assertEqual('passed', binary.compare(case, observed, {})[0])
        for key, value in [('errorCode', 'InvalidHeaderFlags'), ('byteOffset', 1), ('sectionType', 3), ('entryIndex', 4)]:
            wrong = copy.deepcopy(observed)
            wrong['binary'][key] = value
            self.assertEqual('failed', binary.compare(case, wrong, {})[0])
        self.assertEqual('error', binary.compare(case, {'status': 'error'}, {})[0])
        observed['binary']['byteOffset'] = True
        with self.assertRaises(InvalidDocument):
            binary.compare(case, observed, {})

    def test_valid_bytes_metadata_and_runtime_comparison(self):
        case = copy.deepcopy(next(c for c in self.cases if not c.steps and c.expected['binary']['outcome'] == 'valid'))
        case.expected['binary'] = {'outcome': 'valid', 'rewriteByteExact': True, 'rewriteSha256': binary.sha(b'input')}
        encode = lambda value: base64.b64encode(value).decode('ascii')
        request = {'data': encode(b'input'), 'includeDump': False, 'compareCompiledRuntime': True}
        actual = {'outcome': 'valid', 'rewritten': encode(b'input'), 'moduleName': 'test',
                  'requiredRegisterCount': 1, 'requiredCallStackDepth': 0, 'opaqueSectionCount': 0,
                  'programVersion': case.metadata['binaryFixture']['programVersion'], 'buildMetadata': None,
                  'runtimeSections': encode(b'runtime'), 'compiledRuntimeSections': encode(b'runtime')}
        observed = {'status': 'ok', 'binary': actual}
        self.assertEqual('passed', binary.compare(case, observed, request)[0])
        for key, value in [('rewritten', encode(b'other')), ('compiledRuntimeSections', encode(b'other')),
                           ('programVersion', actual['programVersion'] + 1),
                           ('buildMetadata', {'compilerId': 'wrong', 'compilerVersion': 'wrong'})]:
            wrong = copy.deepcopy(observed)
            wrong['binary'][key] = value
            self.assertEqual('failed', binary.compare(case, wrong, request)[0])
        actual['requiredRegisterCount'] = False
        with self.assertRaises(InvalidDocument):
            binary.compare(case, observed, request)

    def test_incomplete_and_conflicting_payload(self):
        case = next(c for c in self.cases if supported(c))
        with self.assertRaises(InvalidDocument):
            binary.compare(case, {'status': 'error', 'binary': {}}, {})
        with self.assertRaises(InvalidDocument):
            binary.compare(case, {'status': 'ok', 'binary': {'outcome': 'valid'}}, {})


if __name__ == '__main__':
    unittest.main()
