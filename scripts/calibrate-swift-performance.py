#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Collect Swift profile observations after correctness checks, without approving budgets."""
import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'conformance/runner'))
from core.corpus import read_corpus
from core.runner import requests, verify
from core.transport import execute
from measurements.performance import correctness_cases, measurement_request
from checks.performance_checks import compare

PROFILE = 'swift-6.4-release-macos27-arm64-m3max'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--adapter', type=Path, default=ROOT / 'artifacts/swift/conformance/release/ges-conformance-adapter')
    args = parser.parse_args()
    cases = [c for c in read_corpus(sorted((ROOT / 'conformance/suites/performance').glob('*.md'))) if c.kind == 'performance']
    fixtures = ROOT / 'conformance/fixtures'
    command = [str(args.adapter)]
    semantic = correctness_cases(cases, PROFILE)
    result = verify(semantic, execute(command, requests(semantic, fixtures), 120), fixtures)
    if result['summary']['passed'] != len(cases):
        raise ValueError('Correctness failed; calibration refused')
    request = measurement_request(cases, PROFILE, fixtures)
    observed = execute([*command, '--measure'], request, 120)
    if (observed.get('requestDigest') != request['requestDigest'] or observed.get('implementation') != result['implementation']
            or set(observed.get('observations', {})) != {c.id for c in cases}):
        raise ValueError('Invalid measurement identity')
    for case in cases:
        value = observed['observations'][case.id]
        if value.get('status') != 'ok':
            raise ValueError('Measurement failed: ' + case.id)
        compare(case, PROFILE, value['measurement'])  # Validate shape; budgets do not gate explicit calibration.
    args.output.mkdir(parents=True, exist_ok=True)
    evidence = [observed['observations'][case.id]['evidence'] for case in cases]
    provenance = evidence[0]['provenance']
    if any(item['provenance'] != provenance for item in evidence):
        raise ValueError('Inconsistent measurement process')
    documents = {case.id.split('/')[0]: case.document.digest.upper() for case in cases}
    report = {'provenance': provenance,
              'documents': [{'suiteId': suite, 'sha256': digest} for suite, digest in documents.items()],
              'cases': [item['samples'] for item in evidence]}
    (args.output / 'SwiftPerformance.measurement.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(f'{len(cases)} measured workloads retained; no reference was modified')


if __name__ == '__main__':
    main()
