#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Negative controls for hash-bound foreign Program inputs, without a Swift build."""
import hashlib
import importlib.util
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest

spec = importlib.util.spec_from_file_location('swift_runtime', Path(__file__).with_name('test-swift-runtime.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class RuntimeInputTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name)
        self.binary = b'independent binary payload'
        self.digest = hashlib.sha256(self.binary).hexdigest().upper()
        self.document = SimpleNamespace(digest='a' * 64)
        self.case = SimpleNamespace(id='suite/case', sources=[{'program': 'main'}], document=self.document)
        self.row = '\t'.join([self.case.id, 'main', self.digest + '.gesb', self.digest, 'A' * 64])
        (self.path / (self.digest + '.gesb')).write_bytes(self.binary)
        self.manifest(self.row)

    def manifest(self, *rows):
        (self.path / 'manifest.tsv').write_text('GES-RUNTIME-FIXTURES-V1\n' + '\n'.join(rows) + '\n')

    def test_valid_input(self):
        self.assertIn('main', module.foreign_inputs([self.case], self.path)[self.case.id])

    def test_stale_document_or_binary_is_rejected(self):
        self.document.digest = 'b' * 64
        with self.assertRaisesRegex(ValueError, 'Stale source'):
            module.foreign_inputs([self.case], self.path)
        self.document.digest = 'a' * 64
        (self.path / (self.digest + '.gesb')).write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError, 'binary hash'):
            module.foreign_inputs([self.case], self.path)

    def test_missing_duplicate_and_reordered_groups(self):
        self.manifest()
        with self.assertRaises(ValueError):
            module.foreign_inputs([self.case], self.path)
        self.manifest(self.row, self.row)
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            module.foreign_inputs([self.case], self.path)
        self.case.sources = [{'program': 'first'}, {'program': 'main'}]
        self.manifest(self.row, self.row.replace('\tmain\t', '\tfirst\t'))
        with self.assertRaisesRegex(ValueError, 'reordered'):
            module.foreign_inputs([self.case], self.path)

    def test_file_path_cannot_escape_fixture_directory(self):
        self.manifest(self.row.replace(self.digest + '.gesb', '../foreign.gesb'))
        with self.assertRaisesRegex(ValueError, 'content-addressed'):
            module.foreign_inputs([self.case], self.path)

    def test_sourceless_runtime_uses_explicit_empty_group(self):
        self.case.sources = []
        self.manifest('\t'.join([self.case.id, '', '', '', 'A' * 64]))
        self.assertEqual(module.foreign_inputs([self.case], self.path), {self.case.id: {}})


if __name__ == '__main__':
    unittest.main()
