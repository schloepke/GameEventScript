#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Check performance scenarios' behavior without timing or advertising a measurement pass."""
import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'conformance/runner'))
from core.corpus import read_corpus
from core.execution import run
from measurements.performance import correctness_cases
from reporting.reports import canonical


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', required=True, help='Select workload configuration, without measuring it')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--adapter', nargs=argparse.REMAINDER, required=True)
    args = parser.parse_args()
    cases = [case for case in read_corpus(sorted((ROOT / 'conformance/suites/performance').glob('*.md')))
             if case.kind == 'performance']
    cases = correctness_cases(cases, args.profile)
    result = canonical(cases, run(cases, args.adapter, ROOT / 'conformance/fixtures'))
    result['verification'] = 'performance-scenario-correctness-only'
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print('Performance scenario correctness (no measurements):', result['summary'])
    return 0 if result['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
