#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Regression checks for the Swift symbol-graph documentation gate."""

import importlib.util
from pathlib import Path
import sys
import unittest


sys.dont_write_bytecode = True
SPEC = importlib.util.spec_from_file_location("swift_api", Path(__file__).with_name("verify-swift-api.py"))
API = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(API)
BYTECODE_SPEC = importlib.util.spec_from_file_location("swift_bytecode", Path(__file__).with_name("verify-swift-bytecode.py"))
BYTECODE = importlib.util.module_from_spec(BYTECODE_SPEC)
BYTECODE_SPEC.loader.exec_module(BYTECODE)


def symbol(name, *, kind="swift.method", comment=None, access="public", authored=True):
    value = {"pathComponents": ["Example", name], "accessLevel": access, "kind": {"identifier": kind}}
    if authored:
        value["location"] = {"uri": "file:///repo/Example.swift", "position": {"line": 4, "character": 0}}
    if comment is not None:
        value["docComment"] = comment
    return value


class SwiftDocumentationTests(unittest.TestCase):
    def issues(self, *symbols):
        return API.documentation_issues("ExampleModule", [{"symbols": list(symbols)}])

    def test_undocumented_cases_and_protocol_requirements_are_checked(self):
        issues = self.issues(symbol("ready", kind="swift.enum.case"), symbol("handle()"))
        self.assertEqual(2, len(issues))
        self.assertTrue(all("Example.swift:5:" in issue for issue in issues))
        self.assertTrue(any("Example.ready" in issue for issue in issues))

    def test_empty_or_inherited_documentation_is_not_a_local_contract(self):
        self.assertEqual(1, len(self.issues(symbol("empty", comment={"lines": [{"text": "  "}]}))))
        inherited = {"module": "Swift", "lines": [{"text": "A textual representation of this instance."}]}
        self.assertEqual(1, len(self.issues(symbol("description", comment=inherited))))

    def test_local_documentation_passes_with_or_without_explicit_module(self):
        for module in (None, "ExampleModule"):
            comment = {"lines": [{"text": "Canonical GES text, with unquoted root Text."}]}
            if module:
                comment["module"] = module
            self.assertEqual([], self.issues(symbol("description", comment=comment)))

    def test_synthesized_and_nonpublic_members_are_excluded(self):
        self.assertEqual([], self.issues(symbol("synthesized", authored=False), symbol("private", access="internal")))

    def test_open_members_and_duplicate_graph_entries(self):
        member = symbol("overridable()", access="open")
        self.assertEqual(1, len(self.issues(member, member)))

    def test_bytecode_documentation_does_not_change_numeric_registry(self):
        text = """
        public enum Example: UInt8 {
            /// Produces { left, right }; example: case fake = 0xff
            case zip = 0xb7
            /// The next instruction after the example.
            case keys = 0xb8
        }
        """
        self.assertEqual({"zip": 0xb7, "keys": 0xb8}, BYTECODE.enum_values(text, "Example", True))


if __name__ == "__main__":
    unittest.main()
