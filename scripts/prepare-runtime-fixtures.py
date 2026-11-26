#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Prepare source-only runtime fixture inputs with the independent corpus parser."""
import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'conformance/runner'))
from checks import handle_catalog
from core.corpus import read_corpus


def handle_schemas(case):
    # The handle types the compiler knows; resolvers and answers stay with the executing adapter.
    catalog = handle_catalog.prepare(case)
    return catalog['schemas'] if catalog is not None else []


def compiled_sources(case):
    # Binary fixtures compile their sources only to compare runtime behaviour, as the runner sends them.
    if case.kind == 'programBinary' and not case.metadata.get('binaryFixture', {}).get('compareCompiledRuntime', False):
        return []
    return [dict(source) for source in case.sources]


def prepare(corpus):
    cases = read_corpus(sorted(Path(corpus).rglob('*.md')))
    return [dict(id=case.id, sources=compiled_sources(case),
                 debugInfo=list(case.metadata.get('compile', {}).get('debugInfo',
                                ['debugSymbols', 'sourceMap', 'sourceArchive'])),
                 programVersion=case.metadata.get('binaryFixture', {}).get('programVersion', 0),
                 handleSchemas=handle_schemas(case),
                 documentSha256=case.document.digest.upper())
            for case in cases if case.kind in
            {'scriptApi', 'loadError', 'performance', 'bytecodeSnapshot', 'programBinary'}]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('corpus', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    if 'artifacts' not in args.output.resolve().parts:
        parser.error('Generated requests must be written below artifacts')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(prepare(args.corpus)), encoding='utf-8')
