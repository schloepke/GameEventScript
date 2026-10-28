# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
from pathlib import Path
import unittest

from core.corpus import InvalidDocument, read_corpus
from core.runner import requests, supported, unsupported_reason
from checks.runtime_checks import value
from checks.script_checks import compare

ROOT = Path(__file__).resolve().parents[3]


class ScriptChecksTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [c for c in read_corpus(sorted((ROOT / 'conformance/suites').rglob('*.md'))) if c.kind == 'scriptApi']

    def test_request_separates_expectations_and_explains_pending_cases(self):
        selected = [case for case in self.cases if supported(case)]
        self.assertEqual(1390, len(selected))
        for request in requests(selected)['cases']:
            self.assertEqual({'id', 'kind', 'sources', 'compile', 'requires', 'runtime'}, set(request))
            self.assertEqual({'limits', 'steps', 'random', 'hostCount', 'nativeHandlers', 'stepActions', 'deferredPrograms', 'initializationPump', 'publishSink'}, set(request['runtime']))
        self.assertEqual(len(self.cases), len(selected))
        rejected = copy.deepcopy(self.cases[0])
        rejected.metadata['unknownConfiguration'] = True
        self.assertTrue(unsupported_reason(rejected))
        with self.assertRaises(InvalidDocument):
            requests([rejected])

    def test_unexpected_fields_never_produce_partial_acceptance(self):
        template = next(case for case in self.cases if supported(case))
        for key, data in [('random', {'seed': True}), ('hostCount', 0), ('nativeHandlers', [{'message': 'X', 'actions': [{'detachProgram': 'unknown'}]}]),
                          ('comparison', {'binary64': {'mode': 'unknown'}})]:
            case = copy.deepcopy(template)
            case.metadata[key] = data
            self.assertFalse(supported(case))
            with self.assertRaises(InvalidDocument):
                requests([case])
        with self.assertRaises(InvalidDocument):
            compare(template, {'status': 'ok'})
        with self.assertRaises(InvalidDocument):
            compare(template, {'status': 'error', 'runtime': {}})
        self.assertEqual(('error', []), compare(template, {'status': 'error', 'detail': 'compile failed'}))

    def test_tag_and_dice_preserve_value_identity(self):
        self.assertNotEqual(value({'type': ':Tag', 'value': 'red'}), value({'type': ':Text', 'value': 'red'}))
        self.assertNotEqual(value({'type': ':Dice', 'rolls': [6, 1]}), value({'type': ':Dice', 'rolls': [5, 2]}))
        self.assertNotEqual(value({'type': ':Dice', 'rolls': [6, 1]}), value({'type': ':Dice', 'rolls': [1, 6]}))
        for rolls in [[True], [0], [-1], [2**31], ['1'], {}]:
            with self.assertRaises(InvalidDocument):
                value({'type': ':Dice', 'rolls': rolls})


if __name__ == '__main__':
    unittest.main()
