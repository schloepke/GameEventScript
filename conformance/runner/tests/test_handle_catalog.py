# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Controls for the per-case handle catalog (specs/Conformance/MarkdownFormat.md#handle-catalogs)."""
import copy
from pathlib import Path
import unittest

from checks import handle_catalog
from core.corpus import InvalidDocument, read_corpus

ROOT = Path(__file__).resolve().parents[3]


class HandleCatalogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cases = read_corpus([ROOT / 'conformance/suites/runtime/handles.md', ROOT / 'conformance/suites/compile/handles.md'])
        cls.script = next(case for case in cases if case.kind == 'scriptApi' and case.metadata['handles'].get('answers'))
        cls.compile = next(case for case in cases if case.kind == 'compileError' and 'handles' in case.metadata)

    def with_catalog(self, case, **changes):
        case = copy.deepcopy(case)
        case.metadata['handles'] = {**case.metadata['handles'], **changes}
        return case

    def test_corpus_catalogs_normalize_with_defaults(self):
        catalog = handle_catalog.prepare(self.script)
        self.assertEqual({'schemas', 'hostSchemas', 'resolvers', 'answers'}, set(catalog))
        self.assertTrue(catalog['answers'])
        for answer in catalog['answers']:
            self.assertEqual({'handle', 'answer', 'text', 'version'}, set(answer))
        defaulted = handle_catalog.prepare(self.with_catalog(self.compile, resolvers=None))
        self.assertEqual(defaulted['schemas'], defaulted['hostSchemas'])
        self.assertIsNone(defaulted['resolvers'])
        self.assertEqual([], defaulted['answers'])

    def test_invalid_catalogs_are_rejected(self):
        answer = {'handle': '@Drone:7', 'document': '{ altitude: 1 }'}
        invalid = [
            (self.script, {'schemas': []}),
            (self.script, {'schemas': [1]}),
            (self.script, {'resolvers': ['@Drone']}),
            (self.script, {'resolvers': ['Drone', 'Drone']}),
            (self.script, {'unknown': True}),
            (self.script, {'answers': [{'handle': 'Drone:7', 'missing': True}]}),
            (self.script, {'answers': [{'handle': '@Drone:7', 'missing': True, 'fail': True}]}),
            (self.script, {'answers': [{'handle': '@Drone:7', 'missing': False}]}),
            (self.script, {'answers': [{'handle': '@Drone:7', 'fail': True, 'version': {'type': ':Number.int64', 'value': '1'}}]}),
            (self.script, {'answers': [{**answer, 'version': {'type': ':Text', 'value': '1'}}]}),
            (self.compile, {'answers': [answer]}),
        ]
        for case, changes in invalid:
            with self.subTest(changes=changes), self.assertRaises(InvalidDocument):
                handle_catalog.prepare(self.with_catalog(case, **changes))

    def test_catalog_belongs_to_compiler_loading_and_script_cases(self):
        case = copy.deepcopy(self.compile)
        case.kind = 'valueApi'
        with self.assertRaises(InvalidDocument):
            handle_catalog.prepare(case)


if __name__ == '__main__':
    unittest.main()
