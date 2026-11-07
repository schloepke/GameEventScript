# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
import unittest
from pathlib import Path
from core.corpus import InvalidDocument, read_corpus
from checks.schema_api_checks import compare, prepare


class SchemaApiExternalInputTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cases = read_corpus(sorted((Path(__file__).resolve().parents[3] / 'conformance/suites').rglob('*.md')))
        cls.cases = {c.id.rsplit('/', 1)[-1]: c for c in cases if c.kind == 'schemaApi'}

    def case(self, name, **changes):
        case = copy.deepcopy(self.cases[name])
        case.metadata['schemaApi'].update(changes)
        return case

    def test_the_corpus_inputs_reach_the_adapter(self):
        request = prepare(self.cases['external-value-getter-failure'])
        self.assertTrue(request['encodeExternal'])
        self.assertEqual('a', request['failingField'])
        self.assertEqual(['pos'], prepare(self.cases['external-value-nested'])['externalFields'])
        self.assertTrue(prepare(self.cases['external-baseline-delta'])['baselineExternal'])

    def test_only_records_can_be_external(self):
        number = {'type': ':Number.int64', 'value': '7'}
        bare_map = {'type': ':Map', 'entries': []}
        for encode in (number, bare_map, {'type': ':List', 'items': []}, 'text', None):
            with self.assertRaises(InvalidDocument):
                prepare(self.case('external-value', encode=encode))
        with self.assertRaises(InvalidDocument):
            prepare(self.case('external-value', encodeExternal=False))
        with self.assertRaises(InvalidDocument):
            prepare(self.case('external-baseline-delta', baseline=number))

    def test_malformed_entries_are_document_errors(self):
        for entries in (['x'], [{'value': 1}], [{'key': [], 'value': 1}], [{'key': 'a'}], 'x'):
            with self.assertRaises(InvalidDocument):
                prepare(self.case('external-value', encode={'type': ':Hit', 'entries': entries}))
            with self.assertRaises(InvalidDocument):
                prepare(self.case('external-value-getter-failure', encode={'type': ':Hit', 'entries': entries}))

    def test_a_failing_code_is_a_deliberate_fault_on_a_failing_field(self):
        self.assertEqual('probe.fault', prepare(self.cases['external-value-getter-fault'])['failingCode'])
        for code in (1, '', ' ', 'runtime.fault'):
            with self.assertRaises(InvalidDocument):
                prepare(self.case('external-value-getter-fault', failingCode=code))
        with self.assertRaises(InvalidDocument):
            prepare(self.case('external-value', failingCode='probe.fault'))

    def test_a_failing_baseline_field_belongs_to_an_external_baseline(self):
        request = prepare(self.cases['external-baseline-getter-failure'])
        self.assertEqual('x', request['baselineFailingField'])
        with self.assertRaises(InvalidDocument):
            prepare(self.case('external-baseline-getter-failure', baselineFailingField='missing'))
        with self.assertRaises(InvalidDocument):
            prepare(self.case('external-value-delta', baselineFailingField='x'))
        case = self.case('external-baseline-delta')
        case.expected['schema']['decoded'] = {'failure': 'runtime.externalFieldAccessFailed'}
        with self.assertRaises(InvalidDocument):
            prepare(case)
        case = self.cases['external-baseline-getter-failure']
        failure = {'failure': 'runtime.externalFieldAccessFailed'}
        self.assertEqual('passed', compare(case, {'status': 'ok', 'schema': {'encoded': failure, 'decoded': failure}})[0])
        self.assertEqual('failed', compare(case, {'status': 'ok', 'schema': {'encoded': failure, 'decoded': {'failure': 'runtime.other'}}})[0])
        self.assertEqual('failed', compare(case, {'status': 'ok', 'schema': {'encoded': failure, 'decoded': {'value': {'type': ':Nothing'}, 'byteCount': 2}}})[0])

    def test_a_buffer_size_belongs_to_a_full_encoding(self):
        self.assertEqual(4, prepare(self.case('external-value', encodeBuffer=4))['encodeBuffer'])
        with self.assertRaises(InvalidDocument):
            prepare(self.case('external-value-delta', encodeBuffer=4))

    def test_external_fields_name_record_fields(self):
        for names in ([], ['missing'], 'pos', [1]):
            with self.assertRaises(InvalidDocument):
                prepare(self.case('external-value-nested', externalFields=names))
        with self.assertRaises(InvalidDocument):
            prepare(self.case('external-value', externalFields=['a']))

    def test_a_failing_field_belongs_to_an_external_encoding(self):
        with self.assertRaises(InvalidDocument):
            prepare(self.case('external-value-getter-failure', failingField='missing'))
        case = self.case('record-next-to-external', failingField='a')
        with self.assertRaises(InvalidDocument):
            prepare(case)
        case = self.case('external-value')
        case.expected['schema']['encoded'] = {'failure': 'runtime.externalFieldAccessFailed'}
        with self.assertRaises(InvalidDocument):
            prepare(case)

    def test_a_failure_is_compared_by_its_code(self):
        case = self.cases['external-value-getter-failure']
        seen = lambda encoded: {'status': 'ok', 'schema': {'encoded': encoded}}
        self.assertEqual('passed', compare(case, seen({'failure': 'runtime.externalFieldAccessFailed'}))[0])
        self.assertEqual('failed', compare(case, seen({'failure': 'runtime.other'}))[0])
        self.assertEqual('failed', compare(case, seen({'bytes': '07'}))[0])


if __name__ == '__main__':
    unittest.main()
