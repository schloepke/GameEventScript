# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Language-neutral measurement requests and metric comparisons."""
import copy
import hashlib
from core.corpus import InvalidDocument, closed
from core.runner import requests, verify, encode, CORE, OPTIONAL
from core.transport import execute
from checks import performance_checks


def correctness_cases(cases, profile):
    result = copy.deepcopy(cases)
    for case in result:
        if case.kind != 'performance':
            raise InvalidDocument('Expected performance workload')
        performance_checks.prepare(case, profile)
        case.kind = case.metadata['kind'] = 'scriptApi'
        del case.metadata['performance']
        del case.expected['performance']
        if 'requires' in case.metadata:
            case.metadata['requires']['optional'] = [c for c in case.metadata['requires'].get('optional', []) if c != 'performance']
    return result


def measurement_request(cases, profile, fixtures):
    request = requests(correctness_cases(cases, profile), fixtures)
    for case, entry in zip(cases, request['cases']):
        entry['measurement'] = performance_checks.prepare(case, profile)
    request['requestDigest'] = hashlib.sha256(encode(request['cases'])).hexdigest()
    return request


def run(cases, command, profile, fixtures, timeout=60):
    semantic = correctness_cases(cases, profile)
    correctness = verify(semantic, execute(command, requests(semantic, fixtures), timeout), fixtures)
    if correctness['summary']['passed'] != len(cases):
        return {'correctness': correctness, 'measured': False, 'summary': correctness['summary']}
    request = measurement_request(cases, profile, fixtures)
    measured = execute([*command, '--measure'], request, timeout)
    closed(measured, {'formatVersion', 'requestDigest', 'implementation', 'capabilities', 'observations'}, 'measurement envelope')
    if (type(measured.get('formatVersion')) is not int or measured['formatVersion'] != 1
            or measured.get('requestDigest') != request['requestDigest']
            or measured.get('implementation') != correctness['implementation']):
        raise InvalidDocument('Measurement identity or digest mismatch')
    capabilities = measured.get('capabilities')
    if (not isinstance(capabilities, list) or any(not isinstance(c, str) or c not in CORE | OPTIONAL for c in capabilities)
            or len(set(capabilities)) != len(capabilities)):
        raise InvalidDocument('Invalid measurement capabilities')
    if not isinstance(measured.get('observations'), dict) or set(measured['observations']) != {c.id for c in cases}:
        raise InvalidDocument('Missing or unexpected measurements')
    results = []
    for case in cases:
        observation = measured['observations'][case.id]
        if not isinstance(observation, dict):
            raise InvalidDocument('Invalid measurement observation')
        if observation.get('status') == 'error':
            closed(observation, {'status', 'detail'}, 'measurement error')
            if not isinstance(observation.get('detail', ''), str):
                raise InvalidDocument('Invalid measurement error detail')
            results.append({'id': case.id, 'status': 'error', 'detail': observation.get('detail')})
            continue
        closed(observation, {'status', 'measurement', 'evidence'}, 'measurement observation')
        if observation.get('status') != 'ok':
            raise InvalidDocument('Invalid measurement outcome')
        # Evidence is retained for diagnostics, never interpreted as a portable assertion.
        status, differences = performance_checks.compare(case, profile, observation.get('measurement'))
        results.append({'id': case.id, 'status': status, 'differences': differences})
    return {'correctness': correctness, 'measured': True, 'results': results, 'observations': measured,
            'summary': {s: sum(r['status'] == s for r in results) for s in ('passed', 'failed', 'error')}}
