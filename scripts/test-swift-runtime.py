#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Verify Swift runtime behavior using hash-bound C#-compiled inputs and Python assertions."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'conformance/runner'))
from core.corpus import read_corpus
from core.execution import run
from measurements.performance import correctness_cases
from reporting.reports import canonical


def foreign_inputs(cases, directory):
    rows = (directory / 'manifest.tsv').read_text().splitlines()
    if not rows or rows[0] != 'GES-RUNTIME-FIXTURES-V1':
        raise ValueError('Invalid runtime manifest')
    manifest = {}
    for row in rows[1:]:
        cells = row.split('\t')
        if len(cells) != 5:
            raise ValueError('Invalid runtime manifest row')
        identity, group, name, digest, document = cells
        groups = manifest.setdefault(identity, {})
        if group in groups:
            raise ValueError('Duplicate runtime Program group')
        groups[group] = (name, digest, document)
    inputs = {}
    for case in cases:
        groups = manifest.get(case.id)
        wanted = list(dict.fromkeys(s['program'] for s in case.sources)) or ['']
        if groups is None or list(groups) != wanted:
            raise ValueError('Missing or reordered runtime inputs: ' + case.id)
        inputs[case.id] = {}
        for group, (name, digest, document) in groups.items():
            if document != case.document.digest.upper():
                raise ValueError('Stale source document: ' + case.id)
            if not group:
                if name or digest:
                    raise ValueError('Unexpected sourceless input')
                continue
            if name != digest + '.gesb' or len(digest) != 64 or any(c not in '0123456789ABCDEF' for c in digest):
                raise ValueError('Invalid content-addressed input')
            with (directory / name).open('rb') as stream:
                data = stream.read(64 * 1024 * 1024 + 1)
            if len(data) > 64 * 1024 * 1024 or hashlib.sha256(data).hexdigest().upper() != digest:
                raise ValueError('Invalid runtime binary hash or size')
            inputs[case.id][group] = base64.b64encode(data).decode('ascii')
    return inputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--programs', type=Path, default=ROOT / 'artifacts/swift/runtime-fixtures')
    parser.add_argument('--adapter', type=Path, default=ROOT / 'artifacts/swift/conformance/release/ges-conformance-adapter')
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/swift/conformance-results/RuntimeResults.json')
    args = parser.parse_args()
    cases = [c for c in read_corpus(sorted((ROOT / 'conformance/suites').rglob('*.md')))
             if c.kind in {'scriptApi', 'loadError', 'bytecodeSnapshot', 'programBinary', 'performance'}]
    inputs = foreign_inputs(cases, args.programs)
    # Performance scenarios verify behavior here; no Swift compiler/performance claim.
    converted = correctness_cases([c for c in cases if c.kind == 'performance'], 'swift-6.4-release-macos26-arm64-m3max')
    replacements = {c.id: c for c in converted}
    cases = [replacements.get(c.id, c) for c in cases]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    path = args.output.parent / 'foreign-programs.json'
    path.write_text(json.dumps(inputs), encoding='utf-8')
    result = canonical(cases, run(cases, [str(args.adapter), '--programs', str(path)], ROOT / 'conformance/fixtures'))
    result['inputCompiler'] = 'csharp'
    args.output.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print('Swift runtime (C# inputs):', result['summary'])
    return 0 if result['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
