#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Qualify the independent valid-program generator and preservation oracle."""
import base64
import importlib.util
from pathlib import Path
import struct
import unittest

spec = importlib.util.spec_from_file_location('properties', Path(__file__).with_name('test-binary-roundtrip-properties.py'))
properties = importlib.util.module_from_spec(spec)
spec.loader.exec_module(properties)


class PropertyControls(unittest.TestCase):
    def test_reproducible_generation_and_optional_section_coverage(self):
        rows = list(properties.programs(properties.SEED, 192))
        self.assertEqual(rows, list(properties.programs(properties.SEED, 192)))
        self.assertNotEqual(rows, list(properties.programs(properties.SEED + 1, 192)))
        combinations, registers, opaque_counts = set(), set(), set()
        for metadata, encoded in rows:
            data = base64.b64decode(metadata['canonical'])
            self.assertLessEqual(len(encoded), properties.fuzz.MAX_BYTES)
            self.assertEqual(len(data), struct.unpack_from('<I', data, 12)[0])
            offsets = list(properties.fuzz.sections(data))
            types = [struct.unpack_from('<H', data, offset)[0] for offset in offsets]
            combinations.add(tuple(t for t in types if t in (0x20, 0x21, 0x22, 0x30)))
            opaque_counts.add(sum(t >= 0x8000 for t in types))
            registers.add(struct.unpack_from('<H', data, offsets[0] + 14)[0])
            self.assertEqual(types[:5], [1, 2, 3, 4, 0x10])
        self.assertEqual(len(combinations), 16)
        self.assertEqual(registers, set(range(1, 9)))
        self.assertEqual(opaque_counts, {0, 1, 2})
        self.assertTrue(any(data != base64.b64decode(meta['canonical']) for meta, data in rows))

    def test_oracle_rejects_data_loss_and_other_valid_program(self):
        rows = list(properties.programs(properties.SEED, 48))
        correct = ['accept:' + meta['canonical'] for meta, _ in rows]
        properties.verify_expected(rows, correct)
        for changed in ('reject:InvalidMagic', correct[1], 'accept:' + base64.b64encode(b'GESB').decode()):
            actual = list(correct)
            actual[0] = changed
            with self.assertRaises(AssertionError):
                properties.verify_expected(rows, actual)
        with self.assertRaises(AssertionError):
            properties.verify_expected(rows, correct[:-1])


if __name__ == '__main__':
    unittest.main()
