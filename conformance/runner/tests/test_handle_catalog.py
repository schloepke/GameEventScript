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
        cls.refs = next(case for case in cases if case.metadata.get('handles', {}).get('resolveRefs'))

    def with_catalog(self, case, **changes):
        case = copy.deepcopy(case)
        case.metadata['handles'] = {**case.metadata['handles'], **changes}
        return case

    def test_corpus_catalogs_normalize_with_defaults(self):
        catalog = handle_catalog.prepare(self.script)
        self.assertEqual({'schemas', 'hostSchemas', 'resolvers', 'answers', 'refAnswers', 'resolveRefs'}, set(catalog))
        self.assertTrue(catalog['answers'])
        for answer in catalog['answers']:
            self.assertEqual({'handle', 'answer', 'text', 'version'}, set(answer))
        defaulted = handle_catalog.prepare(self.with_catalog(self.compile, resolvers=None))
        self.assertEqual(defaulted['schemas'], defaulted['hostSchemas'])
        self.assertIsNone(defaulted['resolvers'])
        self.assertEqual([], defaulted['answers'])
        self.assertEqual([], defaulted['refAnswers'])
        self.assertEqual([], defaulted['resolveRefs'])

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
            (self.script, {'answers': [{'handle': '@Drone:7', 'notVisible': False}]}),
            (self.script, {'answers': [{'handle': '@Drone:7', 'notVisible': True, 'missing': True}]}),
            (self.script, {'answers': [{'handle': '@Drone:7', 'notVisible': True, 'version': {'type': ':Number.int64', 'value': '1'}}]}),
            (self.script, {'answers': [{'handle': '@Drone:7', 'fail': True, 'version': {'type': ':Number.int64', 'value': '1'}}]}),
            (self.script, {'answers': [{**answer, 'version': {'type': ':Text', 'value': '1'}}]}),
            (self.compile, {'answers': [answer]}),
            (self.compile, {'resolveRefs': ['@Drone:7~#live']}),
            (self.script, {'answers': [{'ref': '@Drone:7', 'missing': True}]}),
            (self.script, {'answers': [{'ref': '@Drone:7~#live', 'missing': True, 'fail': True}]}),
            (self.script, {'answers': [{'ref': '@Drone:7~#live', 'fail': False}]}),
            (self.script, {'answers': [{'ref': '@Drone:7~#live'}]}),
            (self.script, {'answers': [{'ref': '@Drone:7~#live', 'version': {'type': ':Text', 'value': '1'}}]}),
            (self.script, {'answers': [{'ref': '@Drone:7~#live', 'document': 'a: 1'}]}),
            (self.script, {'resolveRefs': '@Drone:7~#live'}),
            (self.script, {'resolveRefs': ['Drone:7']}),
        ]
        for case, changes in invalid:
            with self.subTest(changes=changes), self.assertRaises(InvalidDocument):
                handle_catalog.prepare(self.with_catalog(case, **changes))

    def test_not_visible_answer(self):
        case = self.with_catalog(self.script, answers=[{'handle': '@Drone:7', 'notVisible': True}])
        answer = handle_catalog.prepare(case)['answers'][0]
        self.assertEqual({'handle': '@Drone:7', 'answer': 'notVisible', 'text': None, 'version': None}, answer)

    def test_ref_answers_and_resolved_refs(self):
        catalog = handle_catalog.prepare(self.refs)
        self.assertTrue(catalog['refAnswers'])
        self.assertEqual({'missing', 'fail', 'version'}, {item['answer'] for item in catalog['refAnswers']})
        for item in catalog['refAnswers']:
            self.assertEqual({'ref', 'answer', 'version'}, set(item))
        self.assertEqual(self.refs.metadata['handles']['resolveRefs'], catalog['resolveRefs'])
        refs = catalog['resolveRefs']
        invalid = [
            None,
            [],
            [{'ref': 'other', 'value': {'type': ':Nothing'}}] + [{'ref': ref, 'value': {'type': ':Nothing'}} for ref in refs[1:]],
            [{'ref': ref} for ref in refs],
            [{'ref': ref, 'value': {'type': ':Nothing'}, 'error': {'phase': 'runtime', 'code': 'runtime.x'}} for ref in refs],
            [{'ref': ref, 'value': {'type': ':Text', 'value': 'x'}} for ref in refs],
            [{'ref': ref, 'error': {'phase': 'wire', 'code': 'runtime.x'}} for ref in refs],
            [{'ref': ref, 'error': {'phase': 'runtime', 'code': 'wire.x'}} for ref in refs],
        ]
        for expected in invalid:
            case = copy.deepcopy(self.refs)
            case.expected['resolvedRefs'] = expected
            with self.subTest(expected=expected), self.assertRaises(InvalidDocument):
                handle_catalog.prepare(case)
        without = copy.deepcopy(self.script)
        without.expected = {**(without.expected or {}), 'resolvedRefs': []}
        with self.assertRaises(InvalidDocument):
            handle_catalog.prepare(without)

    def test_resolved_refs_compare_per_host(self):
        expected = self.refs.expected['resolvedRefs']
        observed = []
        for item in expected:
            if 'value' in item:
                observed.append(copy.deepcopy(item))
            else:
                observed.append({'ref': item['ref'], 'error': {'phase': 'runtime', 'code': item['error']['code'], 'symbol': item['ref']}})
        mismatches = []
        handle_catalog.compare(self.refs, [{'resolvedRefs': observed}], lambda path, exp, obs: mismatches.append(path) if exp != obs else None)
        self.assertEqual([], mismatches)
        value = next(index for index, item in enumerate(observed) if 'value' in item)
        error = next(index for index, item in enumerate(observed) if 'error' in item)
        changed = copy.deepcopy(observed)
        changed[value]['value'] = {'type': ':Nothing'}
        changed[error]['error']['code'] = 'runtime.other'
        handle_catalog.compare(self.refs, [{'resolvedRefs': observed}, {'resolvedRefs': changed}],
                               lambda path, exp, obs: mismatches.append(path) if exp != obs else None)
        self.assertEqual(['/hosts/1/resolvedRefs/%d/value' % value, '/hosts/1/resolvedRefs/%d/error/code' % error], mismatches)
        missing_symbol = copy.deepcopy(observed)
        del missing_symbol[error]['error']['symbol']
        with self.assertRaises(InvalidDocument):
            handle_catalog.compare(self.refs, [{'resolvedRefs': missing_symbol}], lambda *_: None)
        with self.assertRaises(InvalidDocument):
            handle_catalog.compare(self.refs, [{'resolvedRefs': observed[:-1]}], lambda *_: None)

    def test_catalog_belongs_to_compiler_loading_and_script_cases(self):
        case = copy.deepcopy(self.compile)
        case.kind = 'valueApi'
        with self.assertRaises(InvalidDocument):
            handle_catalog.prepare(case)


if __name__ == '__main__':
    unittest.main()
