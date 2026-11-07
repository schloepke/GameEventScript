#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Controls for verify-swift-portable-imports.py: the gate passes on the checkout and rejects platform frameworks."""
import importlib.util
import tempfile
import unittest
from pathlib import Path

SPEC = importlib.util.spec_from_file_location("portable_imports", Path(__file__).with_name("verify-swift-portable-imports.py"))
IMPORTS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(IMPORTS)

MANIFEST = 'let package = Package(name: "{name}", dependencies: [{dependencies}])\n'
SOURCE = """// import Foundation in a comment is no import.
#if canImport(Darwin)
    import Darwin
#elseif canImport(WASILibc)
    import WASILibc
#endif
@_spi(Syntax) import GameEventScriptSyntax
"""


class PortableImportTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)
        for name in IMPORTS.PACKAGES:
            package = self.root / "implementation/swift" / name
            (package / "Sources" / name).mkdir(parents=True)
            dependencies = "" if name == "GameEventScriptSyntax" else '.package(path: "../GameEventScriptSyntax")'
            (package / "Package.swift").write_text(MANIFEST.format(name=name, dependencies=dependencies), encoding="utf-8")
            (package / "Sources" / name / "Source.swift").write_text(SOURCE, encoding="utf-8")

    def tearDown(self):
        self.directory.cleanup()

    def source(self, package):
        return self.root / "implementation/swift" / package / "Sources" / package / "Source.swift"

    def test_checkout_passes(self):
        self.assertEqual([], IMPORTS.check(IMPORTS.ROOT)[0])

    def test_portable_imports_pass(self):
        self.assertEqual(([], 3), IMPORTS.check(self.root))

    def test_foundation_fails(self):
        path = self.source("GameEventScriptSyntax")
        path.write_text(SOURCE + "import Foundation\n", encoding="utf-8")
        errors = IMPORTS.check(self.root)[0]
        self.assertEqual(1, len(errors))
        self.assertIn("Source.swift:8: imports Foundation", errors[0])

    def test_attributed_and_scoped_imports_fail(self):
        path = self.source("GameEventScriptRuntime")
        path.write_text(SOURCE + "@preconcurrency import Dispatch\nimport struct FoundationEssentials.Data\n", encoding="utf-8")
        errors = IMPORTS.check(self.root)[0]
        self.assertEqual(["Dispatch", "FoundationEssentials"], [error.rsplit(" ", 1)[1] for error in errors])

    def test_remote_package_fails(self):
        manifest = self.root / "implementation/swift/GameEventScriptCompiler/Package.swift"
        manifest.write_text(MANIFEST.format(name="GameEventScriptCompiler", dependencies='.package(url: "https://example.com/x.git", from: "1.0.0")'), encoding="utf-8")
        errors = IMPORTS.check(self.root)[0]
        self.assertEqual(1, len(errors))
        self.assertIn("is not a portable GES package", errors[0])

    def test_missing_sources_fail(self):
        for name in IMPORTS.PACKAGES:
            self.source(name).unlink()
        self.assertIn("no portable Swift sources found", IMPORTS.check(self.root)[0])


if __name__ == "__main__":
    unittest.main()
