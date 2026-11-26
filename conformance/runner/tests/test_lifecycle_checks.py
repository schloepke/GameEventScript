# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import unittest
from types import SimpleNamespace
from core.corpus import InvalidDocument
from checks.lifecycle_checks import compare, validate, duration
from runtime.host_actions import actions


class LifecycleTests(unittest.TestCase):
    def setUp(self):
        self.case = SimpleNamespace(kind='scriptApi', sources=[{'program': 'main'}, {'program': 'late'}])

    def test_duration_bounds_and_time_action(self):
        for value in ('0', str(2**63-1)):
            duration(value)
            self.assertEqual([{'operation': 'advanceMicroseconds', 'target': value}],
                             actions([{'advanceMicroseconds': value}], set(), [], set()))
        for operation in ('resetHost', 'startHost'):
            self.assertEqual([{'operation': operation, 'target': 'host'}], actions([{operation: 'host', 'expectResult': False}], set(), [], set()))
            for invalid in ({operation: 'main'}, {operation: 'host', 'expectResult': 1}, {operation: 'host', 'expectError': {'code': 'x'}}):
                with self.assertRaises(InvalidDocument):
                    actions([invalid], {'main'}, [], set())
        duration('none', allow_none=True)
        for value in ('none', '-1', '+1', '01', '1.0', str(2**63), 1, True):
            with self.assertRaises(InvalidDocument):
                duration(value)

    def test_state_assertions_are_optional_but_missing_handles_fail(self):
        observed = {'hostReady': False, 'programStarts': {'main': 'runtimeError'}, 'idleDurationMicroseconds': 'none',
                    'pendingTimers': [{'name': 'beat', 'deadline': '1000000', 'period': '0', 'message': 'Beat', 'program': 'main', 'publish': False}]}
        mismatches = []
        def check(path, expected, actual):
            if expected != actual:
                mismatches.append(path)
        compare(self.case, {}, observed, check)
        self.assertEqual([], mismatches)
        compare(self.case, {'hostReady': True, 'programStarts': {'late': 'ready'}, 'idleDurationMicroseconds': '0', 'pendingTimers': []}, observed, check)
        self.assertEqual(['/hostReady', '/idleDurationMicroseconds', '/pendingTimers', '/programStarts/late'], sorted(mismatches))
        for invalid in ({'hostReady': 1}, {'programStarts': {'unknown': 'ready'}},
                        {'programStarts': {'main': 'missing'}}, {'idleDurationMicroseconds': '-1'},
                        {'pendingTimers': {}}, {'pendingTimers': [{'name': 'beat'}]},
                        {'pendingTimers': [{'name': 'beat', 'deadline': '-1', 'period': '0', 'message': 'Beat', 'program': 'main', 'publish': False}]},
                        {'pendingTimers': [{'name': 'beat', 'deadline': '1', 'period': '0', 'message': 'Beat', 'program': 'other', 'publish': False}]}):
            with self.assertRaises(InvalidDocument):
                validate(self.case, invalid)
        with self.assertRaises(InvalidDocument):
            validate(self.case, {}, observation=True)
