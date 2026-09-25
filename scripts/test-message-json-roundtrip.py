#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Exchange actual product JSON between C# and Swift using independent Markdown oracles."""
import argparse
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'artifacts/swift/message-json-roundtrip'
PACKAGE = ROOT / 'implementation/swift/GameEventScriptConformance'
SCRATCH = ROOT / 'artifacts/swift/conformance'
PROJECT = ROOT / 'implementation/csharp/GameEventScript.Conformance/fixture-exporter'


def corpus_vectors():
    """Read the deliberately JSON-quoted JSON scalar from each successful Markdown case."""
    vectors = []
    corpus = ROOT / 'conformance/suites/api/product-json.md'
    for case in corpus.read_text(encoding='utf-8').split('## Test: ')[1:]:
        values = re.findall(r'^  json: (".*")$', case, re.MULTILINE)
        if len(values) == 2:
            vectors.append(tuple(json.loads(value) for value in values))
    if len(vectors) < 15:
        raise AssertionError('Product JSON corpus no longer exposes the expected independent oracles')
    return vectors


def verify(actual, expected):
    if actual != expected:
        raise AssertionError('Exchanged message JSON differs from the independent Markdown expectation')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--skip-build', action='store_true')
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    report = OUTPUT / 'summary.json'
    report.unlink(missing_ok=True)
    if not args.skip_build:
        subprocess.run(['dotnet', 'build', str(PROJECT), '--configuration', 'Release'], cwd=ROOT, check=True)
        subprocess.run(['swift', 'build', '--package-path', str(PACKAGE), '--scratch-path', str(SCRATCH),
                        '--build-system', 'native', '--disable-build-manifest-caching', '--configuration', 'release'], cwd=ROOT, check=True)
    adapters = {
        'csharp': ['dotnet', str(ROOT / 'artifacts/csharp/runtime-fixture-exporter/bin/release/GameEventScript.RuntimeFixtureExporter.dll')],
        'swift': [str(SCRATCH / 'release/ges-conformance')],
    }
    vectors = corpus_vectors()
    expected = [row[1] for row in vectors]
    # Qualify the comparator: dropping a value or changing ordered arguments must fail.
    controls = [expected[:-1], [expected[0].replace('Nothing', 'Text')] + expected[1:]]
    for control in controls:
        try:
            verify(control, expected)
        except AssertionError:
            continue
        raise AssertionError('Negative comparison control unexpectedly passed')
    written = {}
    def exchange(name, adapter, values):
        source, target = OUTPUT / (name + '.input.json'), OUTPUT / (name + '.output.json')
        source.write_text(json.dumps(values, ensure_ascii=False), encoding='utf-8')
        subprocess.run(adapter + ['--message-json', str(source), str(target)], cwd=ROOT, check=True)
        result = json.loads(target.read_text(encoding='utf-8'))
        verify(result, expected)
        return result
    for name, adapter in adapters.items():
        written[name] = exchange(name + '-write', adapter, [row[0] for row in vectors])
    for writer, reader in [('csharp', 'swift'), ('swift', 'csharp')]:
        exchange(writer + '-to-' + reader, adapters[reader], written[writer])
        print(f'{writer}-to-{reader}: {len(vectors)} product JSON roundtrips passed', flush=True)
    report.write_text(json.dumps({'status': 'passed', 'casesPerDirection': len(vectors), 'directions': 2,
                                  'negativeControls': len(controls)}, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
