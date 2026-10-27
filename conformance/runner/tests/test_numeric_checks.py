# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import math
from types import SimpleNamespace
import unittest

from core.corpus import InvalidDocument
from checks.numeric_checks import equal, floating_equal, number, options
from checks.runtime_checks import value


class NumericChecksTests(unittest.TestCase):
    def test_exact_bits_and_ulp_edges(self):
        exact = {'mode': 'exact'}
        zero = {'mode': 'ulp', 'maxUlps': 0}
        one = {'mode': 'ulp', 'maxUlps': 1}
        self.assertFalse(floating_equal(0.0, -0.0, exact))
        self.assertTrue(floating_equal(0.0, -0.0, zero))
        for start, direction in [(1.0, math.inf), (-1.0, -math.inf), (0.0, math.inf), (-0.0, -math.inf)]:
            adjacent = math.nextafter(start, direction)
            farther = math.nextafter(adjacent, direction)
            self.assertFalse(floating_equal(start, adjacent, exact))
            self.assertFalse(floating_equal(start, adjacent, zero))
            self.assertTrue(floating_equal(start, adjacent, one))
            self.assertFalse(floating_equal(start, farther, one))

    def test_infinities_and_nan_contract(self):
        permissive = {'mode': 'ulp', 'maxUlps': 2**64 - 1}
        self.assertTrue(floating_equal(math.inf, math.inf, permissive))
        self.assertFalse(floating_equal(math.inf, -math.inf, permissive))
        self.assertFalse(floating_equal(math.inf, 1.0, permissive))
        for kind in (':Number.binary64', ':Percentage', ':Quantity.binary64'):
            expected = {'type': kind, 'value': 'NaN'}
            if kind.startswith(':Quantity'):
                expected['unit'] = ':meter'
            self.assertTrue(equal(expected, {'type': ':Nothing'}, permissive))
            self.assertFalse(equal(expected, {'type': ':Number.int64', 'value': '0'}, permissive))

    def test_kind_unit_and_integer_stay_exact(self):
        ulp = {'mode': 'ulp', 'maxUlps': 2**64 - 1}
        integer = {'type': ':Number.int64', 'value': '9007199254740993'}
        self.assertFalse(equal(integer, {**integer, 'value': '9007199254740992'}, ulp))
        self.assertFalse(equal({'type': ':Number.binary64', 'value': '1'}, {'type': ':Number.int64', 'value': '1'}, ulp))
        quantity = {'type': ':Quantity.binary64', 'unit': ':meter', 'value': '1'}
        self.assertFalse(equal(quantity, {**quantity, 'unit': ':second'}, ulp))
        self.assertEqual(value(quantity), value({**quantity, 'unit': 'm'}))
        self.assertFalse(equal({'type': ':Percentage', 'value': '1'}, {'type': ':Number.binary64', 'value': '1'}, ulp))

    def test_nested_values_use_numeric_comparison_only_at_numeric_leaves(self):
        left = {'type': ':List', 'items': [{'type': ':Number.binary64', 'value': '1.0'}, {'type': ':Text', 'value': '1.0'}]}
        right = {'type': ':List', 'items': [{'type': ':Number.binary64', 'value': '1.0000000000000002'}, {'type': ':Text', 'value': '1.0'}]}
        self.assertTrue(equal(left, right, {'mode': 'ulp', 'maxUlps': 1}))
        self.assertFalse(equal(left, right, {'mode': 'exact'}))
        right['items'][1]['value'] = '1'
        self.assertFalse(equal(left, right, {'mode': 'ulp', 'maxUlps': 1}))

    def test_bad_transport_and_options(self):
        for text in [1, True, '', 'nan', 'inf', '1e999', ' 1', '1 ', '0x1', '1_000']:
            with self.assertRaises(InvalidDocument):
                number(text)
        self.assertEqual(5e-324, number('5e-324'))
        for config in [{'mode': 'exact', 'maxUlps': 0}, {'mode': 'ulp'}, {'mode': 'ulp', 'maxUlps': True},
                       {'mode': 'ulp', 'maxUlps': -1}, {'mode': 'ulp', 'maxUlps': 2**64}, {'mode': 'approximate'}]:
            with self.assertRaises(InvalidDocument):
                options(SimpleNamespace(metadata={'comparison': {'binary64': config}}))
        for item in [{'type': ':Quantity.int64', 'value': '1'}, {'type': ':Quantity.binary64', 'value': '1', 'unit': 'lb'},
                     {'type': ':Number.binary64', 'value': 1.0}]:
            with self.assertRaises(InvalidDocument):
                value(item)


if __name__ == '__main__':
    unittest.main()
