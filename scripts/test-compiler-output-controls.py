#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Controls for the C#/Swift compiler output comparison."""

import importlib.util
from pathlib import Path
import struct
import sys
import unittest

sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location("compare", Path(__file__).with_name("compare-compiler-output.py"))
compare = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compare)


def program(*sections):
    body = b"".join(struct.pack("<HHHHI", kind, 0, 1, 0, len(payload)) + payload for kind, payload in sections)
    return b"GESB" + struct.pack("<HHII", 1, 0, 16, 16 + len(body)) + body


class CompilerOutputTests(unittest.TestCase):
    def test_build_metadata_and_file_size_do_not_count(self):
        left = program((0x10, b"code"), (0x30, b"steph.ges.compiler.csharp"))
        right = program((0x10, b"code"), (0x30, b"steph.ges.compiler.swift"))
        self.assertNotEqual(left, right)
        self.assertEqual([], compare.differing_sections(left, right))

    def test_every_other_section_is_compared_byte_for_byte(self):
        left = program((0x02, b"a"), (0x10, b"code"), (0x21, b"map"))
        right = program((0x02, b"b"), (0x10, b"code"))
        self.assertEqual(["sourceMap", "strings"], compare.differing_sections(left, right))

    def test_section_order_is_compared(self):
        left = program((0x02, b"a"), (0x10, b"code"))
        right = program((0x10, b"code"), (0x02, b"a"))
        self.assertEqual(["sectionOrder"], compare.differing_sections(left, right))
        repeated = program((0x02, b"a"), (0x10, b"code"), (0x10, b"code"))
        self.assertEqual(["sectionOrder"], compare.differing_sections(left, repeated))

    def test_process_failures_never_agree_as_rejections(self):
        rejection = [("a.ges", "1", "3", "parse.syntax")]
        self.assertEqual(("rejected", rejection), compare.outcome(1, None, rejection))
        self.assertEqual("failed", compare.outcome(70, None, [])[0])
        self.assertEqual("failed", compare.outcome(1, None, [])[0])
        self.assertEqual("failed", compare.outcome(1, None, [("", None, None, "cli.io")])[0])
        self.assertEqual("failed", compare.outcome(0, None, [])[0])
        self.assertEqual("both-failed", compare.run_verdict("failed", "exit 70", "failed", "exit 70"))
        self.assertEqual("swift-failed", compare.run_verdict("rejected", rejection, "failed", "timeout"))
        self.assertEqual("both-rejected", compare.run_verdict("rejected", rejection, "rejected", list(rejection)))
        self.assertEqual("csharp-only", compare.run_verdict("compiled", b"x", "rejected", rejection))

    def test_ratchet_reports_new_and_resolved_differences(self):
        result = {"case": "suite/case", "program": "main", "debug": True, "verdict": "different:code"}
        line = compare.baseline_line(result)
        self.assertEqual(([], []), compare.ratchet([result], {line}))
        self.assertEqual(([line], []), compare.ratchet([result], set()))
        self.assertEqual(([], [line]), compare.ratchet([dict(result, verdict="identical")], {line}))
        worse = dict(result, verdict="different:code,strings")
        self.assertEqual(([compare.baseline_line(worse)], [line]), compare.ratchet([worse], {line}))

    def test_regressions_separate_improvements_from_new_differences(self):
        old = "suite/case\tmain\tdebug\tdifferent:code,sourceMap"
        improved = "suite/case\tmain\tdebug\tdifferent:sourceMap"
        worse = "suite/case\tmain\tdebug\tdifferent:code,sourceMap,strings"
        fresh = "suite/other\tmain\tdebug\tdifferent:code"
        self.assertEqual([worse, fresh], compare.regressions([improved, worse, fresh], [old]))

    def test_diagnostics_compare_position_and_code_but_not_message(self):
        csharp = "/w/a.ges(4,3): error parse.syntax: Unexpected token 'let' (line 4, col 3)\n/w/a.ges: error validate.duplicateType: Type 'T'\n"
        swift = "a.ges(4,3): error parse.syntax: Unexpected token 'let'. [program=m]\na.ges: error validate.duplicateType: other text\n"
        self.assertEqual([("a.ges", "4", "3", "parse.syntax"), ("a.ges", None, None, "validate.duplicateType")], compare.diagnostics(csharp))
        self.assertEqual(compare.diagnostics(csharp), compare.diagnostics(swift))
        self.assertNotEqual(compare.diagnostics(csharp), compare.diagnostics("a.ges(4,3): error parse.syntax: x\n"))

    def test_rejections_must_agree_in_codes_and_positions(self):
        first = [("a.ges", "4", "3", "parse.syntax"), ("a.ges", "6", "1", "parse.syntax")]
        self.assertEqual("both-rejected", compare.rejection_verdict(first, list(first)))
        self.assertEqual("positions", compare.rejection_verdict(first, [first[0], ("a.ges", "6", "2", "parse.syntax")]))
        self.assertEqual("diagnostics", compare.rejection_verdict(first, first[:1]))
        self.assertEqual("diagnostics", compare.rejection_verdict(first, [first[0], ("a.ges", "6", "1", "validate.duplicateType")]))
        result = {"case": "suite/case", "program": "main", "debug": True, "verdict": "positions"}
        self.assertEqual(([compare.baseline_line(result)], []), compare.ratchet([result], set()))

    def test_rejection_by_one_compiler_is_a_difference(self):
        result = {"case": "suite/case", "program": "main", "debug": False, "verdict": "swift-only"}
        self.assertEqual(([compare.baseline_line(result)], []), compare.ratchet([result], set()))


if __name__ == "__main__":
    unittest.main()
