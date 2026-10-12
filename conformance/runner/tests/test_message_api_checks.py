# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
import unittest
from pathlib import Path
from core.corpus import InvalidDocument, read_corpus
from checks.message_api_checks import prepare, compare


class MessageApiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [c for c in read_corpus([Path(__file__).resolve().parents[2] / 'suites/api/messages.md']) if c.kind == 'messageApi']

    def test_construction_inputs_preserve_labels_and_mapping(self):
        duplicate = next(c for c in self.cases if c.id.endswith('case-0005'))
        self.assertEqual(['score', ' score '], [a['name'] for a in prepare(duplicate)['message']['args']])
        mapping = next(c for c in self.cases if c.id.endswith('case-0006'))
        self.assertIsInstance(prepare(mapping)['message']['args'], dict)
        invalid = copy.deepcopy(mapping)
        invalid.expected['message'] = {'argumentCount': 1}
        with self.assertRaises(InvalidDocument):
            prepare(invalid)

    def test_missing_comparands_and_wrong_expectation_types(self):
        for expectation in ({'messageEquals': True}, {'createdMessageSignatureId': 'Start()'}, {'argumentCount': True}, {'matches': 1}, {'error': 'invalidMessage', 'name': 'Start'}):
            case = copy.deepcopy(self.cases[0])
            case.expected['message'] = expectation
            with self.assertRaises(InvalidDocument):
                prepare(case)

    def test_incomplete_and_mistyped_observations_are_rejected(self):
        case = self.cases[0]
        actual = {'normalized': prepare(case)['message'], 'name': 'Start', 'signatureId': 'Start(a,b)', 'messageSignatureId': 'Start(b,a)', 'matches': False, 'argumentCount': 2}
        self.assertEqual('passed', compare(case, {'status': 'ok', 'message': actual})[0])
        for invalid in ({}, {**actual, 'matches': 0}, {**actual, 'error': 'invalidMessage'}):
            with self.assertRaises(InvalidDocument):
                compare(case, {'status': 'ok', 'message': invalid})
        self.assertEqual('failed', compare(case, {'status': 'ok', 'message': {'error': 'invalidMessage'}})[0])

    def test_conformance_comparison_pair_is_required_and_stays_in_python(self):
        case = copy.deepcopy(self.cases[0])
        expected_message = prepare(case)['message']
        case.metadata['messageApi']['compareConformanceMessage'] = expected_message
        with self.assertRaises(InvalidDocument):
            prepare(case)
        case.expected['message']['conformanceEquals'] = True
        self.assertNotIn('compareConformanceMessage', prepare(case))
        del case.metadata['messageApi']['compareConformanceMessage']
        with self.assertRaises(InvalidDocument):
            prepare(case)

    def test_conformance_comparison_cannot_be_claimed_by_adapter(self):
        case = copy.deepcopy(self.cases[0])
        case.metadata['messageApi']['compareConformanceMessage'] = prepare(case)['message']
        case.expected['message'] = {'conformanceEquals': True}
        prepare(case)
        with self.assertRaises(InvalidDocument):
            compare(case, {'status': 'ok', 'message': {'conformanceEquals': True}})
        with self.assertRaises(InvalidDocument):
            compare(case, {'status': 'ok', 'message': {'name': 'Start', 'signatureId': 'Start(a,b)', 'messageSignatureId': 'Start(b,a)', 'matches': False, 'argumentCount': 2}})

    def test_product_json_input_is_opaque_and_roundtrip_is_strict(self):
        case = copy.deepcopy(self.cases[0])
        invalid_json = '{"version":1,"version":1}'
        case.metadata['messageApi']['json'] = invalid_json
        case.expected['message'] = {'error': 'message.invalidJson'}
        self.assertEqual(invalid_json, prepare(case)['json'])
        for bad in (1, 'true', None):
            case.metadata['messageApi']['roundTripJson'] = bad
            with self.assertRaises(InvalidDocument):
                prepare(case)
        case.metadata['messageApi']['roundTripJson'] = False
        self.assertFalse(prepare(case)['roundTripJson'])

    def test_json_observations_require_exact_text(self):
        case = copy.deepcopy(self.cases[0])
        case.metadata['messageApi']['roundTripJson'] = False
        case.expected['message'] = {'json': '{"version":1}'}
        prepare(case)
        actual = {'name': 'Start', 'signatureId': 'Start(a,b)', 'messageSignatureId': 'Start(b,a)', 'matches': False, 'argumentCount': 2, 'json': '{"version":1}'}
        self.assertEqual('passed', compare(case, {'status': 'ok', 'message': actual})[0])
        actual['json'] += ' '
        self.assertEqual('failed', compare(case, {'status': 'ok', 'message': actual})[0])
        actual['json'] = {}
        with self.assertRaises(InvalidDocument):
            compare(case, {'status': 'ok', 'message': actual})
