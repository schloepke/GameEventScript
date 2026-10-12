#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Controls for the source-only handoff to the C# fixture exporter."""
import hashlib
from pathlib import Path
import runpy
import tempfile
import unittest

PREPARE = runpy.run_path(str(Path(__file__).with_name('prepare-runtime-fixtures.py')))['prepare']


class FixtureRequests(unittest.TestCase):
    def test_preserves_sources_options_versions_and_original_document_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'example.md'
            data = b'''---
formatVersion: 1
suiteId: fixture.export
kind: programBinary
level: scenario
compile:
  debugInfo: [sourceMap]
---
# Export
## Test: example
```yaml
gesBlock: case
id: example
sources:
  - name: one.ges
    program: first
  - name: two.ges
    program: second
binaryFixture:
  programVersion: 42
```
```ges
on One() {}
```
```ges
on Two() {}
```
```yaml
gesBlock: expect
binary:
  outcome: success
```
'''.replace(b'\n', b'\r\n')
            path.write_bytes(data)
            result = PREPARE(directory)
            self.assertEqual(len(result), 1)
            item = result[0]
            self.assertEqual(item['id'], 'fixture.export/example')
            self.assertEqual(item['documentSha256'], hashlib.sha256(data).hexdigest().upper())
            self.assertEqual(item['debugInfo'], ['sourceMap'])
            self.assertEqual(item['programVersion'], 42)
            self.assertEqual([s['program'] for s in item['sources']], ['first', 'second'])
            self.assertEqual([s['name'] for s in item['sources']], ['one.ges', 'two.ges'])
            self.assertEqual(set(item), {'id', 'sources', 'debugInfo', 'programVersion', 'documentSha256'})

    def test_real_corpus_has_sourceless_cases_and_no_assertion_payloads(self):
        root = Path(__file__).resolve().parents[1]
        requests = PREPARE(root / 'conformance/suites')
        self.assertTrue(any(not item['sources'] for item in requests))
        self.assertTrue(any(item['debugInfo'] == [] for item in requests))
        self.assertEqual(len(requests), len({item['id'] for item in requests}))
        for item in requests:
            self.assertNotIn('expect', item)
            for source in item['sources']:
                self.assertEqual(set(source), {'name', 'program', 'text'})


if __name__ == '__main__':
    unittest.main()
