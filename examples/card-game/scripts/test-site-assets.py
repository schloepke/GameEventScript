#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Negative controls for the example's deployment provenance checks."""
import json
from pathlib import Path
import tempfile
import unittest
from site_assets import contents, validate


class SiteAssetsTests(unittest.TestCase):
    def test_stale_modified_incomplete_and_metadata(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in ['index.html', 'card-game.wasm', 'worker.mjs', 'app.mjs', 'examples/mau-mau.ges', 'examples/high-card.ges', 'examples/skat.ges', 'examples.json', 'vendor/index.js', 'licenses/LICENSE']:
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
