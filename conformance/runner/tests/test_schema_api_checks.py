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

    def store_case(self, name, change=None, expected=None):
        case = copy.deepcopy(self.cases[name])
        if change:
            change(case.metadata['schemaApi']['store'])
        if expected is not None:
            case.expected['schema']['store'] = expected
        return case

    def test_handle_roots_reach_the_adapter_with_refs_and_versions(self):
        request = prepare(self.cases['store-handle-roots'])['store']
        self.assertEqual({'#main': 7}, request['refs'])
        self.assertEqual(3, request['latest'])
        self.assertEqual(['@UnitFiles:content~#main/units.gesd', '@UnitFiles:mod/mod.gesd'], request['stores'][0]['handles'])
        self.assertIsNone(request['stores'][0]['roots'])
        self.assertIsNone(request['files'][0]['version'])
        self.assertEqual(4, prepare(self.cases['store-handle-version-mismatch'])['store']['files'][0]['version'])
        named = prepare(self.cases['store-entries'])['store']
        self.assertEqual(({}, None), (named['refs'], named['latest']))
        self.assertIsNone(named['stores'][0]['handles'])

    def test_malformed_handle_setups_are_document_errors(self):
        def both(store):
            store['stores'][0]['roots'] = []
        changes = [both, lambda c: c.update(refs={'#main': -1}), lambda c: c.update(refs=[]), lambda c: c.update(latest=True),
                   lambda c: c['files'][0].update(version='4'), lambda c: c['stores'][0].update(handles=['units.gesd']),
                   lambda c: c['stores'][0].update(handles='@UnitFiles:mod/mod.gesd'), lambda c: c['stores'][0].pop('handles')]
        for change in changes:
            with self.assertRaises(InvalidDocument):
                prepare(self.store_case('store-handle-roots', change))
        for expected in ({'failure': 'store.importMissing'}, {'failure': 'runtime.handleVersionMismatch', 'entries': []},
                         {'sources': []}, {'errors': [{'code': 'store.importMissing', 'location': {'root': 'a', 'path': 'b'}}], 'sources': []}):
            with self.assertRaises(InvalidDocument):
                prepare(self.store_case('store-handle-roots', expected=expected))

    def test_sources_and_failures_are_compared(self):
        case = self.cases['store-handle-roots']
        wanted = dict(case.expected['schema']['store'])
        seen = lambda store: {'status': 'ok', 'schema': {'store': store}}
        self.assertEqual('passed', compare(case, seen(copy.deepcopy(wanted)))[0])
        self.assertEqual('failed', compare(case, seen({**copy.deepcopy(wanted), 'sources': ['@UnitFiles:content~7/units.gesd']}))[0])
        mismatch = self.cases['store-handle-version-mismatch']
        self.assertEqual('passed', compare(mismatch, seen({'failure': 'runtime.handleVersionMismatch'}))[0])
        self.assertEqual('failed', compare(mismatch, seen({'failure': 'runtime.handleResolveFailed'}))[0])
        self.assertEqual('failed', compare(mismatch, seen({'entries': []}))[0])

    def test_projection_indexes_are_requested_and_compared(self):
        case = self.cases['store-projection-index']
        self.assertEqual(['UnitDisplay', 'UnitLabel'], prepare(case)['store']['index'])
        self.assertEqual([], prepare(self.cases['store-entries'])['store']['index'])
        for index in ('UnitDisplay', [''], ['A', 'A'], [7]):
            with self.assertRaises(InvalidDocument):
                prepare(self.store_case('store-projection-index', lambda c: c.update(index=index)))
        wanted = copy.deepcopy(case.expected['schema']['store'])
        for expected in ({**wanted, 'indexes': {'UnitDisplay': wanted['indexes']['UnitDisplay']}},
                         {**wanted, 'indexes': {'UnitDisplay': 'x', 'UnitLabel': []}},
                         {'indexes': wanted['indexes'], 'failure': 'store.importMissing'}):
            with self.assertRaises(InvalidDocument):
                prepare(self.store_case('store-projection-index', expected=expected))
        seen = lambda store: {'status': 'ok', 'schema': {'store': store}}
        self.assertEqual('passed', compare(case, seen(copy.deepcopy(wanted)))[0])
        swapped = copy.deepcopy(wanted)
        swapped['indexes']['UnitLabel'] = list(reversed(swapped['indexes']['UnitLabel']))
        self.assertEqual('failed', compare(case, seen(swapped))[0])


if __name__ == '__main__':
    unittest.main()
