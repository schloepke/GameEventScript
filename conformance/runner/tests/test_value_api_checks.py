# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
import unittest
from pathlib import Path
from core.corpus import InvalidDocument, read_corpus
from core.runner import requests
from checks.value_api_checks import input_value, prepare, compare


class ValueApiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [c for c in read_corpus(sorted((Path(__file__).resolve().parents[3] / 'conformance/suites').rglob('*.md'))) if c.kind == 'valueApi']

    def test_inputs_preserve_duplicate_keys_and_construction_order(self):
        case = next(c for c in self.cases if c.id.endswith('map-ordering-and-copy'))
        raw = case.metadata['valueApi']['value']
        request = prepare(case)
        self.assertEqual(raw, request['value'])
        self.assertEqual(4, len(request['value']['entries']))
        self.assertEqual(raw, input_value({'type': ':List', 'items': [raw]})['items'][0])
        self.assertTrue(request['mutateSourceAfterCreate'])
        self.assertNotIn('normalized', request)

    def test_required_inputs_and_closed_results(self):
        case = copy.deepcopy(self.cases[0])
        for invalid in ({'equalHash': True}, {'equal': True}, {'notEqual': True}):
            case.metadata['valueApi'] = {'value': {'type': ':Nothing'}}
            case.expected['value'] = {'normalized': {'type': ':Nothing'}, **invalid}
            with self.assertRaises(InvalidDocument):
                prepare(case)
        case = copy.deepcopy(self.cases[0])
        case.metadata['valueApi']['mutateSourceAfterCreate'] = 1
        with self.assertRaises(InvalidDocument):
            prepare(case)
        with self.assertRaises(InvalidDocument):
            compare(self.cases[0], {'status': 'ok', 'value': {}})

    def test_expectations_do_not_change_requests(self):
        case = copy.deepcopy(self.cases[0])
        before = requests([case])
        case.expected['value']['isNumeric'] = False
        self.assertEqual(before, requests([case]))

    def literal_case(self, suffix):
        return copy.deepcopy(next(c for c in self.cases if c.id.endswith(suffix)))

    def test_literal_requests_and_outcomes(self):
        case = self.literal_case('/literal-strict-bare-word')
        self.assertEqual({'literal': {'text': 'standard', 'mode': 'strict'}}, prepare(case))
        unrecognized = {'status': 'ok', 'literal': {'value': None, 'limit': None}}
        text = {'status': 'ok', 'literal': {'value': {'type': ':Text', 'value': 'standard'}, 'limit': None}}
        limit = {'status': 'ok', 'literal': {'value': None, 'limit': {'name': 'MaxLiteralDepth', 'limit': 64}}}
        self.assertEqual('passed', compare(case, unrecognized)[0])
        self.assertEqual('failed', compare(case, text)[0])
        self.assertEqual('failed', compare(case, limit)[0])
        recognized = self.literal_case('/literal-strict-quoted-text')
        self.assertEqual('passed', compare(recognized, text)[0])
        self.assertEqual('failed', compare(recognized, unrecognized)[0])
        bounded = self.literal_case('/literal-depth-limit')
        self.assertEqual('passed', compare(bounded, limit)[0])
        other = {'status': 'ok', 'literal': {'value': None, 'limit': {'name': 'MaxLiteralItems', 'limit': 65536}}}
        self.assertEqual('failed', compare(bounded, other)[0])

    def test_repeated_literal_text_expands_before_the_request(self):
        items = prepare(self.literal_case('/literal-items-limit'))['literal']['text']
        self.assertEqual(131075, len(items))
        self.assertTrue(items.startswith('[0,0,') and items.endswith(',0,0]'))
        self.assertEqual(' ' * 1048577, prepare(self.literal_case('/literal-strict-input-scalars-limit'))['literal']['text'])
        for text in ({'repeat': '', 'count': 1}, {'repeat': 'x', 'count': 0}, {'repeat': 'x', 'count': True},
                     {'repeat': 'x', 'count': 2, 'prefix': 1}, {'count': 2}, {'repeat': 'x', 'count': 2, 'times': 2},
                     {'repeat': 'xy', 'count': 1048577}):
            case = self.literal_case('/literal-strict-bare-word')
            case.metadata['valueApi'] = {'literal': {'text': text, 'mode': 'strict'}}
            with self.assertRaises(InvalidDocument):
                prepare(case)

    def test_literal_schema_is_closed(self):
        lenient = self.literal_case('/literal-lenient-bare-word')
        lenient.expected['literal'] = {'unrecognized': True}
        with self.assertRaises(InvalidDocument):
            prepare(lenient)
        for metadata in ({'literal': {'text': 'x'}}, {'literal': {'text': 'x', 'mode': 'loose'}},
                         {'literal': {'text': 1, 'mode': 'strict'}}, {'literal': {'text': 'x', 'mode': 'strict'}, 'value': {'type': ':Nothing'}}):
            case = self.literal_case('/literal-strict-bare-word')
            case.metadata['valueApi'] = metadata
            with self.assertRaises(InvalidDocument):
                prepare(case)
        for expected in ({'unrecognized': True, 'value': {'type': ':Nothing'}}, {'limit': {'name': 'MaxCallDepth', 'limit': 64}}, {}):
            case = self.literal_case('/literal-strict-bare-word')
            case.expected['literal'] = expected
            with self.assertRaises(InvalidDocument):
                prepare(case)
        case = self.literal_case('/literal-strict-bare-word')
        for observed in ({'status': 'ok', 'literal': {'value': None}},
                         {'status': 'ok', 'literal': {'value': {'type': ':Nothing'}, 'limit': {'name': 'MaxLiteralDepth', 'limit': 64}}},
                         {'status': 'ok', 'value': {}, 'literal': {'value': None, 'limit': None}}):
            with self.assertRaises(InvalidDocument):
                compare(case, observed)
