#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Regression controls for syntax-aware Swift declaration spacing."""

import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
SPEC = importlib.util.spec_from_file_location("spacing", Path(__file__).with_name("format-swift-spacing.py"))
SPACING = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SPACING)


class DeclarationSpacingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.helper = SPACING.helper()

    def check(self, source, expected):
        with tempfile.TemporaryDirectory(prefix="ges-swift-spacing-") as directory:
            path = Path(directory) / "Example.swift"
            path.write_text(source, encoding="utf-8")
            lint = subprocess.run([str(self.helper), str(path)], capture_output=True)
            self.assertEqual(lint.returncode, int(source != expected), lint.stderr)
            subprocess.run([str(self.helper), "--fix", str(path)], check=True)
            self.assertEqual(path.read_text(encoding="utf-8"), expected)
            # Fix is idempotent, and check mode accepts exactly the fixed source.
            subprocess.run([str(self.helper), "--fix", str(path)], check=True)
            self.assertEqual(path.read_text(encoding="utf-8"), expected)
            subprocess.run([str(self.helper), str(path)], check=True)

    def test_documentation_attributes_and_properties(self):
        source = ('struct Box {\n    var x = 0\n    /// Documentation.\n'
                  '    @available(*, deprecated)\n    func first() {}\n'
                  '    var y = 1\n    var z = 2\n}\nextension Box {}\n')
        expected = source.replace('0\n    ///', '0\n\n    ///').replace('{}\n    var', '{}\n\n    var').replace('}\nextension', '}\n\nextension')
        self.check(source, expected)

    def test_comments_stay_attached(self):
        source = 'func first() {} // trailing comment\n// Next function.\n/* More docs */\nfunc second() {}\n'
        self.check(source, source.replace('comment\n//', 'comment\n\n//'))

    def test_multiple_blank_lines_become_one(self):
        self.check('func a() {}\n\n\n\nfunc b() {}\n', 'func a() {}\n\nfunc b() {}\n')

    def test_nested_types_callables_and_conditionals(self):
        source = ('class Box {\n    init() {}\n    deinit {}\n'
                  '    subscript(x: Int) -> Int { x }\n    struct Nested {}\n'
                  '    enum Kind {}\n}\nprotocol P {\n    func a()\n    func b()\n}\n'
                  '#if DEBUG\nfunc debugA() {}\nfunc debugB() {}\n#endif\n'
                  'actor Worker {}\n')
        expected = source.replace('\n    deinit', '\n\n    deinit').replace('\n    subscript', '\n\n    subscript')
        expected = expected.replace('\n    struct', '\n\n    struct').replace('\n    enum', '\n\n    enum')
        expected = expected.replace('}\nprotocol', '}\n\nprotocol').replace('\n    func b', '\n\n    func b')
        expected = expected.replace('}\n#if', '}\n\n#if').replace('}\nfunc debugB', '}\n\nfunc debugB').replace('#endif\nactor', '#endif\n\nactor')
        self.check(source, expected)

    def test_raw_multiline_strings_and_closures_are_untouched(self):
        source = ('let text = #"""\nfunc fake() {}\nstruct Fake {}\n"""#\n'
                  'let closure = {\n    let a = 1\n    let b = 2\n    return a + b\n}\n')
        self.check(source, source)

    def test_local_functions(self):
        source = 'func outer() {\n    let value = 1\n    func inner() -> Int { value }\n    _ = inner()\n}\n'
        expected = source.replace('1\n    func', '1\n\n    func').replace('value }\n    _', 'value }\n\n    _')
        self.check(source, expected)

    def test_invalid_syntax_is_not_modified(self):
        with tempfile.TemporaryDirectory(prefix="ges-swift-spacing-") as directory:
            path = Path(directory) / "Invalid.swift"
            source = 'struct Broken {\nfunc f(\n'
            path.write_text(source, encoding="utf-8")
            result = subprocess.run([str(self.helper), "--fix", str(path)], capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(path.read_text(encoding="utf-8"), source)


if __name__ == "__main__":
    unittest.main()
