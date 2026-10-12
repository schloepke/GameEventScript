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

    def test_missing_wrong_and_duplicate_action_results(self):
        case = SimpleNamespace(metadata={'stepActions': {'run': [{'detachProgram': 'main', 'expectResult': False}]}})
        mismatches = []
        def check(path, expected, actual):
            if expected != actual:
                mismatches.append(path)
        compare(case, [], 'run', check)
        self.assertTrue(mismatches)
        invocation = {'origin': 'step/run', 'results': [{'result': True, 'error': None}]}
        mismatches.clear()
        compare(case, [invocation], 'run', check)
        self.assertEqual(['/actions/0/0/result'], mismatches)
        for observed in ([invocation, invocation], [{'origin': 'native/missing', 'results': []}],
                         [{'origin': 'step/run', 'results': []}]):
            with self.assertRaises(InvalidDocument):
                compare(case, observed, 'run', check)
