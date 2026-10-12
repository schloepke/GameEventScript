# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Opt-in profile-specific timing/compile measurements with raw sample verification."""
import hashlib
from core.corpus import InvalidDocument, closed
from core.runner import requests, verify, encode
from core.transport import execute
from instrumentation.allocation_measurement import correctness_cases, validate_evidence
from checks import performance_checks
from checks.numeric_checks import number

PROFILE = 'csharp-dotnet-release-macos-arm64'


def measurement_request(cases):
    request = requests(correctness_cases(cases, PROFILE))
    for case, entry in zip(cases, request['cases']):
        entry['measurement'] = performance_checks.prepare(case, PROFILE)
    request['requestDigest'] = hashlib.sha256(encode(request['cases'])).hexdigest()
    return request


def evidence(case, observed):
    closed(observed, {'status', 'measurement', 'evidence'}, 'timing observation')
    if observed.get('status') != 'ok':
        raise InvalidDocument('Timing measurement did not complete')
    data = observed.get('evidence')
    closed(data, {'run', 'ast', 'binary', 'load', 'aggregation', 'timer', 'timerFrequency', 'scope', 'excluded'}, 'timing evidence')
    if data.get('aggregation') != 'Minimum elapsed and maximum allocated independently; compile is the sum of its three phases.':
        raise InvalidDocument('Unknown timing aggregation')
    if data.get('timer') != 'Stopwatch.GetTimestamp' or type(data.get('timerFrequency')) is not int or data['timerFrequency'] <= 0 or any(not isinstance(data.get(k), str) or not data[k] for k in ('scope', 'excluded')):
        raise InvalidDocument('Missing timing provenance')
    validate_evidence(case, data.get('run'))
    provenance = data['run']['evidence']
    if provenance['architecture'] != 'Arm64' or not provenance['os'].startswith('macOS '):
        raise InvalidDocument('Timing evidence does not match macOS ARM64 profile')
    values = {}
    for key, prefix in (('ast', 'ast-build'), ('binary', 'binary-build'), ('load', 'program-load')):
        samples = data.get(key)
        if not isinstance(samples, list) or len(samples) != 3:
            raise InvalidDocument('Three phase samples required')
        for sample in samples:
            closed(sample, {'ElapsedMilliseconds', 'Bytes'}, 'phase sample')
            performance_checks.nonnegative(sample.get('ElapsedMilliseconds'))
            if type(sample.get('Bytes')) is not int or sample['Bytes'] < 0:
                raise InvalidDocument('Invalid allocated byte count')
        values[prefix + '.elapsed'] = min(s['ElapsedMilliseconds'] for s in samples)
        values[prefix + '.allocated'] = max(s['Bytes'] for s in samples) / 1024
    samples = provenance['samples']
    values['run.elapsed'] = min(s['ElapsedMilliseconds'] for s in samples)
    values['run.allocated'] = max(s['Bytes'] for s in samples) / 1024
    iterations = performance_checks.workload(case)['iterations']
    values['run.per-invoke-elapsed'] = values['run.elapsed'] / iterations
    values['run.per-invoke-allocated'] = max(s['Bytes'] for s in samples) / (1024 * iterations)
    for suffix in ('elapsed', 'allocated'):
        values['compile.' + suffix] = (values['ast-build.' + suffix] + values['binary-build.' + suffix]) + values['program-load.' + suffix]
    # Validate the closed metric schema before examining numeric sample consistency.
    outcome = performance_checks.compare(case, PROFILE, observed['measurement'])
    for metric in observed['measurement']['metrics']:
        if metric['id'] not in values or number(metric['value']) != values[metric['id']]:
            raise InvalidDocument('Metric differs from retained samples: ' + metric['id'] + ' observed=' + metric['value'] + ' calculated=' + str(values.get(metric['id'])))
    return outcome


def run(cases, command, timeout=60):
    semantic = correctness_cases(cases, PROFILE)
    correctness = verify(semantic, execute(command, requests(semantic), timeout))
    if correctness['summary']['passed'] != len(cases):
        return {'correctness': correctness, 'measured': False, 'summary': correctness['summary']}
    request = measurement_request(cases)
    measured = execute([*command, '--measure'], request, timeout)
    closed(measured, {'formatVersion', 'requestDigest', 'implementation', 'capabilities', 'observations'}, 'timing envelope')
    if type(measured.get('formatVersion')) is not int or measured['formatVersion'] != 1 or measured.get('requestDigest') != request['requestDigest'] or measured.get('implementation') != correctness['implementation']:
        raise InvalidDocument('Timing identity or digest mismatch')
    if not isinstance(measured.get('observations'), dict) or set(measured['observations']) != {c.id for c in cases}:
        raise InvalidDocument('Missing or unexpected measurements')
    results = []
    for case in cases:
        observed = measured['observations'][case.id]
        if observed.get('status') == 'error':
            results.append({'id': case.id, 'status': 'error', 'detail': observed.get('detail')})
        else:
            status, differences = evidence(case, observed)
            results.append({'id': case.id, 'status': status, 'differences': differences})
    return {'correctness': correctness, 'measured': True, 'results': results, 'observations': measured,
            'summary': {s: sum(r['status'] == s for r in results) for s in ('passed', 'failed', 'error')}}
