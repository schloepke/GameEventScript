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
