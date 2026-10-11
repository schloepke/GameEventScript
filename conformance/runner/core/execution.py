# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Complete suite execution with explicit optional measurement providers."""
from core.corpus import InvalidDocument
from core.runner import case_request, requests, verify, CORE, OPTIONAL
from core.transport import execute
from measurements import performance
from checks import performance_checks



def validate(cases, fixtures):
    """Validate every selected schema before starting any implementation process."""
    for case in cases:
        try:
            if case.kind == 'performance':
                profiles = performance_checks.profiles(case)
                performance_checks.workload(case)
                case_request(performance.correctness_cases([case], next(iter(profiles)))[0], fixtures)
            else:
                case_request(case, fixtures)
        except InvalidDocument as error:
            if error.document is None:
                error.document = case.document
                error.offset = (case.ranges or {}).get(('test',), (0, 0))[0]
            raise


def run(cases, command, fixtures, timeout=60, profiles=()):
    """Run all cases in source order; measurements are opt-in and never reference echoes."""
    if not command:
        raise InvalidDocument('Adapter command required')
    validate(cases, fixtures)
    if len(set(profiles)) != len(profiles):
        raise InvalidDocument('Duplicate measurement profile', 'conformance.runner.missingPerformanceProfile')
    description = None
    if profiles:
        description = execute([*command, '--describe'], {}, timeout)
        if description.get('formatVersion') != 1 or not isinstance(description.get('performanceProfiles'), list):
            raise InvalidDocument('Adapter does not describe its measurement profiles')
        if set(profiles) - set(description['performanceProfiles']):
            raise InvalidDocument('Requested measurement profile unavailable on this adapter', 'conformance.runner.missingPerformanceProfile')
    regular = [case for case in cases if case.kind != 'performance']
    if regular:
        base = verify(regular, execute(command, requests(regular, fixtures), timeout), fixtures)
    else:
        description = description or execute([*command, '--describe'], {}, timeout)
        identity = description.get('implementation')
        capabilities = description.get('capabilities')
        if (description.get('formatVersion') != 1 or not isinstance(identity, dict)
                or set(identity) != {'id', 'version'} or any(not isinstance(v, str) or not v for v in identity.values())
                or not isinstance(capabilities, list) or any(not isinstance(c, str) or c not in CORE | OPTIONAL for c in capabilities)):
            raise InvalidDocument('Invalid adapter description')
        base = {'implementation': identity, 'capabilities': capabilities, 'results': []}
    if description is not None and description.get('implementation') != base['implementation']:
        raise InvalidDocument('Adapter identity changed after profile negotiation')
    results = {result['id']: result for result in base['results']} if regular else {}
    evidence = {}
    batches = {profile: [] for profile in profiles}
    for case in cases:
        if case.kind != 'performance':
            continue
        selected = next((p for p in profiles if p in performance_checks.profiles(case)), None)
        if selected is None:
            results[case.id] = {'id': case.id, 'status': 'skipped',
                                'code': 'conformance.runner.missingOptionalCapability', 'missing': ['performance']}
            if profiles:
                results[case.id].update(status='error', code='conformance.runner.missingPerformanceProfile', missing=[])
        else:
            batches[selected].append(case)
    for profile, batch in batches.items():
        if not batch:
            continue
        measured = performance.run(batch, command, profile, fixtures, timeout)
        correctness = measured['correctness']
        if correctness['implementation'] != base['implementation']:
            raise InvalidDocument('Implementation identity changed between execution and measurement')
        evidence[profile] = measured
        for item in measured.get('results', correctness['results']):
            result = dict(item)
            if not measured.get('measured') and item['status'] == 'passed':
                result.update(status='error', code='conformance.runner.measurementNotPerformed')
            result.setdefault('code', {'passed': 'conformance.passed', 'failed': 'conformance.assertion.mismatch', 'error': 'conformance.runner.executionError', 'skipped': 'conformance.runner.missingOptionalCapability'}[item['status']])
            if measured.get('measured') and item['status'] in ('passed', 'failed'):
                observation = measured['observations']['observations'][item['id']]
                result['measurement'] = observation['measurement']
            results[item['id']] = result
    base['results'] = [results[case.id] for case in cases]
    base['summary'] = {s: sum(r['status'] == s for r in base['results']) for s in ('passed', 'failed', 'error', 'skipped')}
    if evidence:
        base['capabilities'] = sorted(set(base['capabilities']) | {'performance'})
    base['measurementEvidence'] = evidence
    return base
