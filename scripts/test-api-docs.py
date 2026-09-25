#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Negative controls for native documentation staging; no toolchain or network."""

import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('stage_api_docs', ROOT / 'scripts/stage-api-docs.py')
stage = importlib.util.module_from_spec(spec)
spec.loader.exec_module(stage)


class StagingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.patch = patch.object(stage.build, 'ARTIFACTS', self.root)
        self.patch.start()
        self.addCleanup(self.patch.stop)
        self.expected = {'revision': 'a' * 40, 'sourceHash': 'b' * 64, 'label': 'Unreleased'}
        for language in ['csharp', 'swift']:
            output = self.root / 'api' / language
            output.mkdir(parents=True)
            (output / 'build-info.json').write_text(json.dumps(self.expected))
        for module in stage.build.SWIFT:
            path = self.root / f'api/swift/{module.lower()}/documentation/{module.lower()}/index.html'
            path.parent.mkdir(parents=True)
            path.write_text('<html>Generated module</html>')
        for relative in stage.SWIFT_EXTENSION_PAGES:
            path = self.root / 'api/swift' / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('<html>Generated public extension</html>')
        for name in ['index', 'api/GameEventScript.Api.GameEventScriptHost',
                     'api/GameEventScript.Api.GameEventScriptBuilder',
                     'api/GameEventScript.CSharpBridge.GameEventScriptCSharpHostRunner',
                     'api/GameEventScript.SyntaxHighlighter.GameEventScriptSyntaxHighlighter']:
            path = self.root / f'api/csharp/reference/{name}.html'
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('<html>Generated API</html>')

    def test_matching_builds_are_staged(self):
        with patch.object(stage.build, 'identity', return_value=self.expected):
            stage.main()
        self.assertTrue((self.root / 'public/api/csharp/reference/index.html').is_file())
        self.assertTrue((self.root / 'public/api/swift/build-info.json').is_file())

    def test_missing_manifest_is_rejected(self):
        (self.root / 'api/swift/build-info.json').unlink()
        with self.assertRaisesRegex(RuntimeError, 'Missing or stale'):
            stage.validate('swift', self.expected)

    def test_missing_bridge_extension_is_rejected(self):
        for relative in stage.SWIFT_EXTENSION_PAGES:
            path = self.root / 'api/swift' / relative
            original = path.read_bytes()
            path.unlink()
            with self.subTest(page=relative), self.assertRaisesRegex(RuntimeError, 'Incomplete'):
                stage.validate('swift', self.expected)
            path.write_bytes(original)

    def test_api_metadata_is_not_staged(self):
        for relative in ['.DS_Store', 'reference/.DS_Store', 'reference/._index.html']:
            (self.root / 'api/csharp' / relative).write_text('local metadata')
        with patch.object(stage.build, 'identity', return_value=self.expected):
            stage.main()
        self.assertEqual([], list((self.root / 'public').rglob('.DS_Store')))
        self.assertEqual([], list((self.root / 'public').rglob('._index.html')))

    def test_another_revision_or_modified_source_is_rejected(self):
        for key in ['revision', 'sourceHash']:
            with self.subTest(key=key), self.assertRaisesRegex(RuntimeError, 'stale'):
                stage.validate('csharp', self.expected | {key: 'different'})

    def test_missing_module_is_rejected(self):
        for language, path in [
            ('swift', 'gameeventscriptsyntaxhighlighter/documentation/gameeventscriptsyntaxhighlighter/index.html'),
            ('csharp', 'reference/api/GameEventScript.Api.GameEventScriptBuilder.html')]:
            (self.root / 'api' / language / path).unlink()
            with self.subTest(language=language), self.assertRaisesRegex(RuntimeError, 'Incomplete'):
                stage.validate(language, self.expected)

    def test_invalid_input_does_not_replace_previous_staged_site(self):
        previous = self.root / 'public/api/previous.txt'
        previous.parent.mkdir(parents=True)
        previous.write_text('previous')
        (self.root / 'api/swift/build-info.json').unlink()
        with patch.object(stage.build, 'identity', return_value=self.expected), self.assertRaises(RuntimeError):
            stage.main()
        self.assertEqual(previous.read_text(), 'previous')


if __name__ == '__main__':
    unittest.main()
