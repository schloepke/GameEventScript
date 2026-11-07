#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Controls for verify-swift-parser-tokens.py: the gate passes on the checkout and detects drift."""
import importlib.util
import unittest
from pathlib import Path

SPEC = importlib.util.spec_from_file_location("parser_tokens", Path(__file__).with_name("verify-swift-parser-tokens.py"))
TOKENS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(TOKENS)

CSHARP = """
    private static bool IsArgumentLabelKind(GesTokenKind kind)
        => kind is Identifier or To or IntrinsicAbs;

    private static bool IsExtensionFunctionNameKind(GesTokenKind kind)
        => kind is Identifier or IntrinsicAbs;
"""
SWIFT = """
    static let intrinsicNames: Set<GesTokenKind> = [
        .intrinsicAbs,
    ]
    static let labelKeywords: Set<GesTokenKind> = intrinsicNames.union([.to])
"""


class ParserTokenTests(unittest.TestCase):
    def test_checkout_matches(self):
        self.assertEqual([], TOKENS.differences(TOKENS.CSHARP.read_text(encoding="utf-8"), TOKENS.SWIFT.read_text(encoding="utf-8")))

    def test_matching_sets_pass(self):
        self.assertEqual([], TOKENS.differences(CSHARP, SWIFT))

    def test_missing_label_keyword_fails(self):
        found = TOKENS.differences(CSHARP.replace("Identifier or To or", "Identifier or To or Nothing or"), SWIFT)
        self.assertEqual(1, len(found))
        self.assertIn("argument labels", found[0])
        self.assertIn("nothing", found[0])

    def test_extra_swift_intrinsic_fails_both_sets(self):
        found = TOKENS.differences(CSHARP, SWIFT.replace(".intrinsicAbs,", ".intrinsicAbs, .intrinsicLn,"))
        self.assertEqual(2, len(found))
        self.assertIn("intrinsicLn", found[0])

    def test_missing_declarations_fail(self):
        with self.assertRaises(RuntimeError):
            TOKENS.differences("", SWIFT)
        with self.assertRaises(RuntimeError):
            TOKENS.differences(CSHARP, "")


if __name__ == "__main__":
    unittest.main()
