# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Independent positive and negative controls for compiler observations."""
import ast
import copy
from pathlib import Path
import re
import unittest

from checks.compiler_checks import SUPPORTED, compare
from core.corpus import InvalidDocument, read_corpus
from checks.opcode_names import OPCODES
from core.runner import requests

ROOT = Path(__file__).resolve().parents[3]


class CompilerChecksTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [case for case in read_corpus(sorted((ROOT / 'conformance/suites').rglob('*.md')))
                     if case.kind in SUPPORTED]

    def case(self, kind, expected):
        case = copy.deepcopy(next(case for case in self.cases if case.kind == kind))
        case.expected = {'gesBlock': 'expect', **expected}
        return case

    def test_all_compiler_requests_exclude_expectations(self):
        request = requests(self.cases)
        self.assertEqual(342, len(request['cases']))
        for case in request['cases']:
            self.assertEqual({'id', 'kind', 'sources', 'compile', 'requires'}, set(case))

    def test_opcode_catalog_matches_normative_spec(self):
        names = re.findall(r'^\| 0x[0-9A-Fa-f]{2} \| `([^`]+)` \|',
                           (ROOT / 'specs/Bytecode.md').read_text(), re.M)
        self.assertEqual(OPCODES, set(names))

    def test_python_310_syntax(self):
        for path in Path(__file__).resolve().parents[1].rglob('*.py'):
            ast.parse(path.read_text(), filename=str(path), feature_version=(3, 10))

    def test_diagnostic_constraints_and_error_isolation(self):
        expected = {'phase': 'validate', 'code': 'validate.missingCallable', 'line': 3,
                    'symbol': 'missing', 'programName': None}
        case = self.case('compileError', {'error': expected})
        observed = {'status': 'compileError', 'diagnostics': [dict(expected, column=9)]}
        self.assertEqual('passed', compare(case, observed)[0])
        del observed['diagnostics'][0]['programName']
        self.assertEqual('passed', compare(case, observed)[0])
        for field, value in [('phase', 'parse'), ('code', 'parse.syntax'), ('line', 4),
                             ('symbol', 'other'), ('programName', 'Unexpected')]:
            wrong = copy.deepcopy(observed)
            wrong['diagnostics'][0][field] = value
            self.assertEqual('failed', compare(case, wrong)[0])
        self.assertEqual('failed', compare(case, {'status': 'ok'})[0])
        observed['status'] = 'error'
        self.assertEqual('error', compare(case, observed)[0])
        for diagnostics in [[], [{'phase': 'validate', 'code': 'x', 'line': True}]]:
            with self.assertRaises(InvalidDocument):
                compare(case, {'status': 'compileError', 'diagnostics': diagnostics})

    def test_ordered_diagnostic_list_requires_every_diagnostic_in_order(self):
        first = {'phase': 'parse', 'code': 'parse.syntax', 'line': 2}
        second = {'phase': 'parse', 'code': 'parse.syntax', 'line': 5}
        case = self.case('compileError', {'errors': [first, second]})
        observed = {'status': 'compileError', 'diagnostics': [dict(first, column=3), dict(second, column=1)]}
        self.assertEqual('passed', compare(case, observed)[0])
        for diagnostics in [[first], [second, first], [first, second, second], [first, dict(second, code='parse.sourceNestingExceeded')]]:
            self.assertEqual('failed', compare(case, {'status': 'compileError', 'diagnostics': diagnostics})[0])
        self.assertEqual('failed', compare(case, {'status': 'ok'})[0])
        for expected in [{'errors': []}, {'errors': first}, {'errors': [first], 'error': first}, {'errors': [{'code': 'parse.syntax'}]}]:
            with self.assertRaises(InvalidDocument):
                from checks.compiler_checks import expectation
                expectation(self.case('compileError', expected))

    def test_opcode_constraints_and_negative_controls(self):
        case = self.case('bytecode', {'opcodes': {'contains': ['Add'], 'excludes': ['Move'],
                                                'counts': {'Add': 2}, 'minimumCounts': {'ReturnVoid': 1}}})
        observed = {'status': 'ok', 'opcodes': {'Add': 2, 'ReturnVoid': 1}}
        self.assertEqual('passed', compare(case, observed)[0])
        for name, value in [('Add', 0), ('Add', 1), ('Move', 1), ('ReturnVoid', 0)]:
            wrong = copy.deepcopy(observed)
            wrong['opcodes'][name] = value
            self.assertEqual('failed', compare(case, wrong)[0])
        for counts in [{'Add': True}, {'Add': -1}, {'Add': 2**64}, {'Typo': 0}]:
            with self.assertRaises(InvalidDocument):
                compare(case, {'status': 'ok', 'opcodes': counts})
        case.expected['opcodes'] = {'excludes': ['Typo']}
        with self.assertRaises(InvalidDocument):
            requests([case])

    def test_metadata_resources_signatures_and_order(self):
        expected = {'programResources': {'requiredRegisterCount': 7},
                    'messageDefinitions': [{'name': 'Start', 'count': 2, 'signatureIds': ['Start(a)', 'Start(b)']}],
                    'handlerResources': [{'name': 'Start', 'signatureId': 'Start(a)', 'requiredCallStackDepth': 2}]}
        case = self.case('compileMetadata', {'metadata': expected})
        actual = copy.deepcopy(expected)
        actual['programResources']['requiredCallStackDepth'] = 2
        actual['handlerResources'][0]['requiredRegisterCount'] = 7
        observed = {'status': 'ok', 'metadata': actual}
        self.assertEqual('passed', compare(case, observed)[0])
        for mutate in [lambda x: x['programResources'].update(requiredRegisterCount=6),
                       lambda x: x['handlerResources'][0].update(requiredCallStackDepth=1),
                       lambda x: x['messageDefinitions'][0]['signatureIds'].reverse()]:
            wrong = copy.deepcopy(observed)
            mutate(wrong['metadata'])
            self.assertEqual('failed', compare(case, wrong)[0])
        actual['programResources']['requiredRegisterCount'] = True
        with self.assertRaises(InvalidDocument):
            compare(case, observed)

    def test_contradictory_observations_rejected(self):
        case = self.case('compileError', {'error': {'phase': 'parse', 'code': 'parse.syntax'}})
        for observed in [{'status': 'ok', 'diagnostics': [{'phase': 'parse', 'code': 'parse.syntax'}]},
                         {'status': 'ok', 'opcodes': {}}, {'status': 'error', 'dump': 'invalid'},
                         {'status': 'error', 'detail': 12}]:
            with self.assertRaises(InvalidDocument):
                compare(case, observed)


if __name__ == '__main__':
    unittest.main()
