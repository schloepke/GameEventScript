# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import unittest
from core.corpus import InvalidDocument
from runtime.random_config import prepare


class RandomConfigurationTests(unittest.TestCase):
    def test_seed_is_exact_int64_text_and_default_is_deterministic(self):
        self.assertEqual({'seed': '0'}, prepare({}))
        for seed in (0, -(2**63), 2**63 - 1):
            self.assertEqual({'seed': str(seed)}, prepare({'random': {'seed': seed}}))
        for seed in (True, '0', 1.5, -(2**63)-1, 2**63):
            with self.assertRaises(InvalidDocument):
                prepare({'random': {'seed': seed}})

    def test_sequence_and_entropy_are_preserved(self):
        for config in ({'sequence': ['4', '0.25', '1.5', '1e-5', '0.0001', '10000000000000000', '1e17']},
                       {'entropy': '0001FF'}):
            self.assertEqual(config, prepare({'random': config}))

    def test_invalid_configurations_are_rejected(self):
        for config in ({}, {'seed': 0, 'sequence': ['1']}, {'unknown': 0},
                       {'sequence': []}, {'sequence': [1]}, {'sequence': ['NaN']},
                       {'sequence': ['Infinity']}, {'sequence': ['1e400']},
                       {'sequence': ['1.0']}, {'sequence': ['-0']}, {'sequence': ['1E+17']},
                       {'entropy': ''}, {'entropy': 'aB'}, {'entropy': '0'},
                       {'entropy': '0xFF'}, {'entropy': '00 FF'}, {'entropy': 123}):
            with self.subTest(config=config), self.assertRaises(InvalidDocument):
                prepare({'random': config})
