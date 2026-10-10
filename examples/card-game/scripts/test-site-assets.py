#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Negative controls for the example's deployment provenance checks."""
import json
from pathlib import Path
import tempfile
import unittest
from site_assets import contents, validate, version_browser_assets


class SiteAssetsTests(unittest.TestCase):
    def test_browser_graph_uses_one_build_version(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            files = {
                'index.html': '<script src="app.mjs"></script><link href="style.css">',
                'app.mjs': "import './client.mjs';",
                'client.mjs': "new Worker('./worker.mjs', {type:'module'});",
                'worker.mjs': "import './engine.mjs';",
                'engine.mjs': "import './vendor/index.js'; fetch('./card-game.wasm');",
                'vendor/index.js': "export * from './dependency.js';",
                'vendor/dependency.js': "const x = 'https://example.org/remote.js';",
                'style.css': 'body {}',
                'card-game.wasm': 'binary fixture',
            }
            for name, text in files.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text)
            version_browser_assets(root, 'abc123')
            for name in ['index.html', 'app.mjs', 'client.mjs', 'worker.mjs', 'engine.mjs', 'vendor/index.js']:
                self.assertIn('?v=abc123', (root / name).read_text())
            self.assertIn('card-game.wasm?v=abc123', (root / 'engine.mjs').read_text())
            self.assertEqual((root / 'vendor/dependency.js').read_text(), files['vendor/dependency.js'])
            first = contents(root)
            version_browser_assets(root, 'abc123')
            self.assertEqual(contents(root), first)
            version_browser_assets(root, 'def456')
            self.assertIn('worker.mjs?v=def456', (root / 'client.mjs').read_text())
            self.assertNotIn('abc123', (root / 'engine.mjs').read_text())

    def test_stale_modified_incomplete_and_metadata(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in ['index.html', 'card-game.wasm', 'worker.mjs', 'app.mjs', 'examples/mau-mau.ges', 'examples/high-card.ges', 'examples/skat.ges', 'examples/blackjack.ges', 'examples.json', 'vendor/index.js', 'licenses/LICENSE']:
                target = root / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text('fixture')
            manifest = root / 'build-info.json'
            manifest.write_text(json.dumps({'source': 'expected', 'files': contents(root)}))
            validate(root, 'expected')
            with self.assertRaisesRegex(RuntimeError, 'Stale'):
                validate(root, 'other-source')
            (root / 'app.mjs').write_text('modified')
            with self.assertRaisesRegex(RuntimeError, 'Stale'):
                validate(root, 'expected')
            (root / 'app.mjs').unlink()
            manifest.write_text(json.dumps({'source': 'expected', 'files': contents(root)}))
            with self.assertRaisesRegex(RuntimeError, 'Incomplete'):
                validate(root, 'expected')
            (root / '.DS_Store').write_text('metadata')
            with self.assertRaisesRegex(RuntimeError, 'Not a static'):
                validate(root, 'expected')


if __name__ == '__main__':
    unittest.main()
