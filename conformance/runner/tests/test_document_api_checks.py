# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
import unittest
from pathlib import Path
from core.corpus import InvalidDocument, read_corpus
from checks.document_api_checks import prepare, compare


class DocumentApiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [c for c in read_corpus(sorted((Path(__file__).resolve().parents[3] / 'conformance/suites').rglob('*.md'))) if c.kind == 'documentApi']

    def case(self, suffix):
        return copy.deepcopy(next(c for c in self.cases if c.id.endswith('/' + suffix)))

    def test_text_and_value_requests(self):
        text = prepare(self.case('empty-record-root'))
        self.assertEqual({'text': ':Schema(:Config)\n', 'print': {}}, text)
        built = prepare(self.case('built-map-file-schema'))
        self.assertEqual({'kind': 'file', 'name': 'schemas/unit.gess'}, built['schema'])
        self.assertEqual({}, built['print'])
        self.assertEqual(1048577, len(prepare(self.case('input-limit'))['text']))
        self.assertIsNone(prepare(self.case('unquoted-text'))['print'])

    def test_read_and_reread_must_both_match(self):
        case = self.case('empty-record-root')
        good = {'value': {'type': ':Config', 'entries': []}, 'schema': {'kind': 'type', 'name': 'Config'}}
        observed = {'status': 'ok', 'document': {'read': good, 'printed': ':Schema(:Config)\n', 'reread': good}}
        self.assertEqual('passed', compare(case, observed)[0])
        for wrong in ({'value': {'type': ':Map', 'entries': []}, 'schema': None},
                      {'value': {'type': ':Config', 'entries': []}, 'schema': {'kind': 'file', 'name': 'Config'}},
                      {'error': {'code': 'invalidKey', 'line': 1, 'column': 1}}):
            for path in ('read', 'reread'):
                broken = copy.deepcopy(observed)
                broken['document'][path] = wrong
                self.assertEqual('failed', compare(case, broken)[0], (path, wrong))

    def test_errors_compare_code_and_position(self):
        case = self.case('duplicate-root-key')
        exact = {'status': 'ok', 'document': {'read': {'error': {'code': 'duplicateKey', 'line': 3, 'column': 1}}}}
        self.assertEqual('passed', compare(case, exact)[0])
        for error in ({'code': 'duplicateKey', 'line': 3, 'column': 2}, {'code': 'invalidKey', 'line': 3, 'column': 1}):
            self.assertEqual('failed', compare(case, {'status': 'ok', 'document': {'read': {'error': error}}})[0])
        self.assertEqual('failed', compare(case, {'status': 'ok', 'document': {'read': {'value': {'type': ':Map', 'entries': []}, 'schema': None}}})[0])
        limited = self.case('depth-limit')
        bound = {'code': 'limitExceeded', 'line': 1, 'column': 68, 'limitName': 'MaxLiteralDepth', 'limit': 64}
        self.assertEqual('passed', compare(limited, {'status': 'ok', 'document': {'read': {'error': bound}}})[0])
        self.assertEqual('failed', compare(limited, {'status': 'ok', 'document': {'read': {'error': dict(bound, limit=65)}}})[0])

    def test_rejections_and_unreadable_prints(self):
        rejected = self.case('built-rejects-list-root')
        self.assertEqual('passed', compare(rejected, {'status': 'ok', 'document': {'rejected': True}})[0])
        accepted = {'status': 'ok', 'document': {'printed': 'a: 1\n', 'reread': {'value': {'type': ':Map', 'entries': []}, 'schema': None}}}
        self.assertEqual('failed', compare(rejected, accepted)[0])
        built = self.case('built-map-file-schema')
        unreadable = {'status': 'ok', 'document': {'printed': 'x', 'reread': {'error': {'code': 'missingColon', 'line': 1, 'column': 2}}}}
        self.assertEqual('failed', compare(built, unreadable)[0])
        with self.assertRaises(InvalidDocument):
            compare(built, {'status': 'ok', 'document': {'rejected': True, 'printed': ''}})

    def test_schema_is_closed(self):
        for change in (lambda c: c.metadata['documentApi'].pop('text'),
                       lambda c: c.metadata['documentApi'].update(value={'type': ':Map', 'entries': []}),
                       lambda c: c.metadata['documentApi'].update(schema={'kind': 'type', 'name': 'A'}),
                       lambda c: c.metadata['documentApi'].update(print={'width': 3}),
                       lambda c: c.expected.update(document={'value': {'type': ':Map', 'entries': []}, 'error': {'code': 'invalidKey', 'line': 1, 'column': 1}}),
                       lambda c: c.expected.update(document={'error': {'code': 'unknownCode', 'line': 1, 'column': 1}}),
                       lambda c: c.expected.update(document={'error': {'code': 'invalidKey', 'line': 0, 'column': 1}}),
                       lambda c: c.expected.update(document={'error': {'code': 'invalidKey', 'line': 1, 'column': 1, 'limit': 3}}),
                       lambda c: c.expected.update(document={'rejected': True})):
            case = self.case('unquoted-text')
            change(case)
            with self.assertRaises(InvalidDocument):
                prepare(case)
        printed_error = self.case('unquoted-text')
        printed_error.metadata['documentApi']['print'] = True
        with self.assertRaises(InvalidDocument):
            prepare(printed_error)
        unprinted = self.case('built-map')
        del unprinted.metadata['documentApi']['print']
        with self.assertRaises(InvalidDocument):
            prepare(unprinted)


if __name__ == '__main__':
    unittest.main()
