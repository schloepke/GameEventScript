# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import unittest
from core.corpus import InvalidDocument
from runtime.native_handlers import prepare
from checks.runtime_checks import message


class NativeHandlerTests(unittest.TestCase):
    def test_defaults_and_forwarding(self):
        result = prepare({'nativeHandlers': [{'message': 'Ping', 'emit': [{'name': 'Pong', 'forwardArguments': True}]}]}, message)[0]
        self.assertEqual('native-0001', result['id'])
        self.assertEqual([], result['parameters'])
        self.assertEqual(0, result['priority'])
        self.assertTrue(result['initiallySubscribed'])
        self.assertTrue(result['emit'][0]['forwardArguments'])
        self.assertEqual({'name': 'Pong', 'args': [], 'tags': []}, result['emit'][0]['message'])
        self.assertIsNone(result['faultCode'])

    def test_invalid_native_definitions(self):
        for change in ({'message': ''}, {'messageName': True, 'parameters': ['x']}, {'priority': True},
                       {'priority': 2**31}, {'throw': 1}, {'faultCode': 'runtime.invalid'},
                       {'faultCode': ' '}, {'faultCode': 'test.fault', 'throw': True},
                       {'faultContext': {}}, {'faultCode': 'test.fault', 'faultContext': {'programName': 1}},
                       {'parameters': 'x'},
                       {'emit': [{'name': 'X'}]}, {'emit': [{'name': 'X', 'args': [], 'forwardArguments': True}]},
                       {'emit': [{'name': 'X', 'forwardArguments': False}]}):
            with self.subTest(change=change), self.assertRaises(InvalidDocument):
                prepare({'nativeHandlers': [{'message': 'Ping', **change}]}, message)
        with self.assertRaises(InvalidDocument):
            prepare({'nativeHandlers': [{'id': 'same', 'message': 'A'}, {'id': 'same', 'message': 'B'}]}, message)
