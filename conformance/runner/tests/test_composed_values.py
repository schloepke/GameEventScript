# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
import unittest

from core.corpus import InvalidDocument
from checks.numeric_checks import equal
from checks.runtime_checks import value


class ComposedValueTests(unittest.TestCase):
    def test_spatial_kind_units_and_tolerance(self):
        vector = {'type': ':Vector', 'x': '1', 'y': '2', 'z': '3', 'unit': ':meter'}
        ulp = {'mode': 'ulp', 'maxUlps': 1}
        self.assertEqual(value(vector), value({**vector, 'unit': 'm'}))
        self.assertTrue(equal(vector, {**vector, 'x': '1.0000000000000002'}, ulp))
        for replacement in [{'type': ':Point'}, {'unit': ':second'}, {'x': '4'}, {'y': '3', 'z': '2'}]:
            self.assertFalse(equal(vector, {**vector, **replacement}, ulp))
        self.assertTrue(equal({**vector, 'y': 'NaN'}, {'type': ':Nothing'}, ulp))

    def test_range_storage_and_components_remain_exact(self):
        integer = {'type': ':Range.int64', 'from': '9007199254740993', 'to': '9007199254740995', 'step': '1'}
        self.assertEqual(integer, value(integer))
        loose = {'mode': 'ulp', 'maxUlps': 4096}
        self.assertFalse(equal(integer, {**integer, 'from': '9007199254740992'}, loose))
        self.assertFalse(equal(integer, {**integer, 'type': ':Range.binary64'}, loose))
        floating = {'type': ':Range.binary64', 'from': '1.5', 'to': '3.5', 'step': '0.5'}
        self.assertTrue(equal(floating, {**floating, 'from': '1.5000000000000002'}, loose))
        self.assertFalse(equal(floating, {**floating, 'step': '-0.5'}, loose))
        self.assertTrue(equal({**floating, 'step': 'NaN'}, {'type': ':Nothing'}, loose))

    def test_records_last_entry_wins_and_unicode_keys(self):
        one, two = {'type': ':Number.int64', 'value': '1'}, {'type': ':Number.int64', 'value': '2'}
        raw = {'type': ':Box', 'entries': [{'key': 'é', 'value': one}, {'key': 'a', 'value': one},
                                         {'key': 'e\u0301', 'value': one}, {'key': 'a', 'value': two}]}
        normalized = value(raw)
        self.assertEqual(['a', 'e\u0301', 'é'], [entry['key'] for entry in normalized['entries']])
        self.assertEqual(two, normalized['entries'][0]['value'])
        exact = {'mode': 'exact'}
        self.assertFalse(equal(normalized, {**normalized, 'type': ':OtherBox'}, exact))
        self.assertFalse(equal(normalized, {**normalized, 'type': ':Map'}, exact))

    def test_nested_messages_preserve_signature_order_and_tags(self):
        record = {'type': ':Box', 'entries': [{'key': 'v', 'value': {'type': ':Number.int64', 'value': '1'}}]}
        raw = {'type': ':Message', 'message': {'name': 'Send', 'tags': ['one'], 'args': [
            {'name': 'a', 'value': record}, {'name': 'b', 'value': {'type': ':List', 'items': [record]}}]}}
        expected = value(raw)
        for mutate in [lambda m: m['args'].reverse(), lambda m: m.update(tags=['two']), lambda m: m.update(name='Other')]:
            observed = copy.deepcopy(expected)
            mutate(observed['message'])
            self.assertFalse(equal(expected, observed, {'mode': 'exact'}))

    def test_invalid_composite_fields_rejected(self):
        for raw in [{'type': ':Vector', 'x': '1', 'y': '2'},
                    {'type': ':Point', 'x': '1', 'y': '2', 'z': '3', 'unit': 'kg'},
                    {'type': ':Range.int64', 'from': '1.5', 'to': '2', 'step': '1'},
                    {'type': ':Range.binary64', 'from': '1', 'to': '2', 'step': 1},
                    {'type': ':Box', 'entries': [], 'extra': True},
                    {'type': ':Message', 'message': {'name': 'Send', 'args': [{}]}}]:
            with self.assertRaises(InvalidDocument):
                value(raw)


if __name__ == '__main__':
    unittest.main()
