# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Language-neutral result serialization and Received input identity."""
from checks import performance_checks
from core.corpus import InvalidDocument
from checks.numeric_checks import canonical_text as numeric_text


def canonical(cases, report):
    """Project raw comparison/evidence results into the normative V1 report shape."""
    items = {item['id']: item for item in report['results']}
    if len(items) != len(cases) or set(items) != {case.id for case in cases}:
        raise InvalidDocument('Report case selection mismatch')
    output = []
    for case in cases:
        raw = items[case.id]
        performance = None
        if 'measurement' in raw:
            actual = raw['measurement']
            profile = actual['profile']
            performance_checks.compare(case, profile, actual)
            expected = performance_checks.profiles(case)[profile]['metrics']
            performance = {'profile': profile, 'metrics': {}}
            for metric in actual['metrics']:
                name = metric['id']
                bound = performance_checks.upper_bound(expected[name])
                performance['metrics'][name] = {'measured': numeric_text(metric['value']),
                    'reference': numeric_text(expected[name]['reference']), 'allowed': numeric_text(bound),
                    'unit': metric['unit'], 'passed': float(metric['value']) <= bound}
        mismatches = raw.get('mismatches', raw.get('differences', []))
        if 'mismatch' in raw:
            mismatches = [*mismatches, raw['mismatch']]
        suite, local = case.id.split('/')
        output.append({'id': case.id, 'suiteId': suite, 'caseId': local, 'title': case.title,
            'kind': case.kind, 'level': case.metadata['level'], 'categories': case.metadata.get('categories', []),
            'tags': case.metadata.get('tags', []), 'status': raw['status'], 'code': raw['code'],
            'missingCapabilities': sorted(raw.get('missing', [])),
            'mismatches': [dict(item, code=item.get('code', 'conformance.assertion.mismatch')) for item in mismatches],
            'diagnostics': raw.get('diagnostics', []), 'runtimeLimits': raw.get('runtimeLimits', []),
            'actualAssembler': raw.get('actualAssembler'), 'performance': performance})
        if isinstance(raw.get('detail'), str):
            output[-1]['technicalDetails'] = raw['detail']
    summary = {s: sum(item['status'] == s for item in output) for s in ('passed', 'failed', 'skipped', 'error')}
    status = next((s for s in ('error', 'failed', 'skipped') if summary[s]), 'passed')
    profiles = list(dict.fromkeys(item['performance']['profile'] for item in output if item['performance']))
    result = {'schemaVersion': 1, 'runner': {'id': 'steph.ges.conformance.python', 'version': '1.0.0'},
              'implementation': report['implementation'], 'capabilities': sorted(report.get('capabilities', [])),
              'performanceProfile': profiles[0] if len(profiles) == 1 else None, 'status': status,
              'summary': {'total': len(output), **summary}, 'cases': output}
    # Bind update reports to exact original bytes, independently of request digests.
    result['sourceDocuments'] = {case.document.name: case.document.digest for case in cases if case.document}
    if report.get('measurementEvidence'):
        result['measurementEvidence'] = report['measurementEvidence']
    return result
