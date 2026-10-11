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
        duration('none', allow_none=True)
        for value in ('none', '-1', '+1', '01', '1.0', str(2**63), 1, True):
            with self.assertRaises(InvalidDocument):
                duration(value)

    def test_state_assertions_are_optional_but_missing_handles_fail(self):
        observed = {'hostReady': False, 'programStarts': {'main': 'runtimeError'}, 'idleDurationMicroseconds': 'none'}
        mismatches = []
        def check(path, expected, actual):
            if expected != actual:
                mismatches.append(path)
        compare(self.case, {}, observed, check)
        self.assertEqual([], mismatches)
        compare(self.case, {'hostReady': True, 'programStarts': {'late': 'ready'}, 'idleDurationMicroseconds': '0'}, observed, check)
        self.assertEqual(['/hostReady', '/idleDurationMicroseconds', '/programStarts/late'], mismatches)
        for invalid in ({'hostReady': 1}, {'programStarts': {'unknown': 'ready'}},
                        {'programStarts': {'main': 'missing'}}, {'idleDurationMicroseconds': '-1'}):
            with self.assertRaises(InvalidDocument):
                validate(self.case, invalid)
        with self.assertRaises(InvalidDocument):
            validate(self.case, {}, observation=True)
