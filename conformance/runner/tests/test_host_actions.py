# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import unittest
from types import SimpleNamespace
from core.corpus import InvalidDocument
from runtime.host_actions import actions, compare, prepare


class HostActionTests(unittest.TestCase):
    def test_expectations_never_cross_transport(self):
        raw = [{'loadProgram': 'late', 'expectError': {'phase': 'link', 'code': 'link.test'}},
               {'detachProgram': 'main', 'expectResult': False}]
        self.assertEqual([{'operation': 'loadProgram', 'target': 'late'},
                          {'operation': 'detachProgram', 'target': 'main'}], actions(raw, {'main', 'late'}, ['late'], set()))

    def test_invalid_actions_and_references(self):
        for raw in ({}, {'loadProgram': 'main'}, {'detachProgram': 'missing'},
                    {'subscribeHandler': 'missing'}, {'detachProgram': 'main', 'expectResult': 1},
                    {'loadProgram': 'late', 'expectResult': True, 'expectError': {'phase': 'link', 'code': 'link.test'}},
                    {'detachProgram': 'main', 'expectError': {'phase': 'link', 'code': 'link.test'}},
                    {'loadProgram': 'late', 'expectError': {'phase': 'runtime', 'code': 'test.fault'}}):
            with self.subTest(raw=raw), self.assertRaises(InvalidDocument):
                actions([raw], {'main', 'late'}, ['late'], {'native'})
        case = SimpleNamespace(sources=[{'program': 'main'}], metadata={'stepActions': {'missing': []}}, steps=[])
        with self.assertRaises(InvalidDocument):
            prepare(case, [])

    def test_expect_errors_lists_every_diagnostic_in_order(self):
        first = {'phase': 'link', 'code': 'link.test', 'line': 1}
        second = {'phase': 'link', 'code': 'link.test', 'line': 2}
        raw = {'loadProgram': 'late', 'expectErrors': [first, second]}
        self.assertEqual([{'operation': 'loadProgram', 'target': 'late'}], actions([raw], {'late'}, ['late'], set()))
        absent = {'loadProgram': 'late', 'expectErrors': [{'phase': 'link', 'code': 'link.test', 'sourceName': None, 'column': None}]}
        self.assertEqual(1, len(actions([absent], {'late'}, ['late'], set())))
        for broken in ({'loadProgram': 'late', 'expectErrors': []}, {'loadProgram': 'late', 'expectErrors': first},
                       {'loadProgram': 'late', 'expectError': first, 'expectErrors': [first]}):
            with self.subTest(broken=broken), self.assertRaises(InvalidDocument):
                actions([broken], {'late'}, ['late'], set())
        case = SimpleNamespace(metadata={'stepActions': {'run': [raw]}})
        mismatches = []
        def check(path, expected, actual):
            if expected != actual:
                mismatches.append(path)
        def run(*errors):
            mismatches.clear()
            compare(case, [{'origin': 'step/run', 'results': [{'result': None, 'errors': list(errors)}]}], 'run', check)
            return list(mismatches)
        self.assertEqual([], run(first, second))
        self.assertEqual(['/actions/0/0/errors/count'], run(first))
        self.assertEqual(['/actions/0/0/errors/0/line', '/actions/0/0/errors/1/line'], run(second, first))

    def test_missing_wrong_and_duplicate_action_results(self):
        case = SimpleNamespace(metadata={'stepActions': {'run': [{'detachProgram': 'main', 'expectResult': False}]}})
        mismatches = []
        def check(path, expected, actual):
            if expected != actual:
                mismatches.append(path)
        compare(case, [], 'run', check)
        self.assertTrue(mismatches)
        invocation = {'origin': 'step/run', 'results': [{'result': True, 'errors': []}]}
        mismatches.clear()
        compare(case, [invocation], 'run', check)
        self.assertEqual(['/actions/0/0/result'], mismatches)
        for observed in ([invocation, invocation], [{'origin': 'native/missing', 'results': []}],
                         [{'origin': 'step/run', 'results': []}]):
            with self.assertRaises(InvalidDocument):
                compare(case, observed, 'run', check)
