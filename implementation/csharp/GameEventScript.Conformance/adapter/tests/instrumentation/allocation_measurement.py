# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Opt-in managed allocation qualification, separate from normal acceptance."""
import copy
import hashlib
from core.corpus import InvalidDocument, closed
from core.runner import requests, verify, encode
from core.transport import execute
from checks import performance_checks

PROFILE = 'csharp-dotnet-release-managed'


def correctness_cases(cases, profile=PROFILE):
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


def measurement_request(cases):
    request = requests(correctness_cases(cases))
    for case, entry in zip(cases, request['cases']):
        entry['measurement'] = performance_checks.prepare(case, PROFILE)
    request['requestDigest'] = hashlib.sha256(encode(request['cases'])).hexdigest()
    return request


def validate_evidence(case, observation):
    closed(observation, {'status', 'measurement', 'evidence'}, 'allocation observation')
    if observation.get('status') != 'ok':
        raise InvalidDocument('Allocation measurement did not complete')
    data = observation.get('evidence')
    fields = {'runtime', 'runtimeVersion', 'buildSdkVersion', 'configuration', 'os', 'architecture', 'instrumentation',
              'scope', 'excluded', 'coverageGaps', 'aggregation', 'iterations', 'warmup', 'observe', 'controls', 'samples'}
    closed(data, fields, 'allocation evidence')
    if set(data) != fields:
        raise InvalidDocument('Incomplete measurement provenance')
    for key in fields - {'iterations', 'warmup', 'observe', 'controls', 'samples'}:
        if not isinstance(data[key], str) or not data[key]:
            raise InvalidDocument('Missing measurement provenance')
    if data['configuration'] != 'Release' or data['instrumentation'] != 'GC.GetAllocatedBytesForCurrentThread' or data['aggregation'] != 'Maximum of three fresh-host samples':
        raise InvalidDocument('Unqualified measurement mechanism')
    config = performance_checks.workload(case)
    if type(data['iterations']) is not int or type(data['warmup']) is not int or type(data['observe']) is not bool or (data['iterations'], data['warmup'], data['observe']) != (config['iterations'], config['warmupIterations'], config['observeRuntime']):
        raise InvalidDocument('Measured workload differs from requested workload')
    controls = data['controls']
    closed(controls, {'reuse', 'escaping', 'minimumEscapingBytes'}, 'counter controls')
    if set(controls) != {'reuse', 'escaping', 'minimumEscapingBytes'} or any(type(v) is not int for v in controls.values()) or controls['reuse'] != 0 or controls['minimumEscapingBytes'] != 32000 or controls['escaping'] < 32000:
        raise InvalidDocument('Allocation counter controls failed')
    samples = data['samples']
    if not isinstance(samples, list) or len(samples) != 3:
        raise InvalidDocument('Three allocation samples required')
    for sample in samples:
        closed(sample, {'Bytes', 'Work', 'Callbacks', 'ElapsedMilliseconds'}, 'allocation sample')
        performance_checks.nonnegative(sample.get('ElapsedMilliseconds'))
        if type(sample.get('Bytes')) is not int or sample['Bytes'] < 0:
            raise InvalidDocument('Invalid allocation sample')
        for key, names in [('Work', {'Opcodes', 'ProcessedMessages', 'EmittedMessages', 'PublishedMessages', 'Pauses'}),
                           ('Callbacks', {'Started', 'Completed', 'Emitted', 'Published', 'Limits', 'Errors'})]:
            counters = sample.get(key)
            closed(counters, names, 'work counters')
            if set(counters) != names or any(type(v) is not int or v < 0 for v in counters.values()):
                raise InvalidDocument('Invalid work counters')
        if sample['Work']['Opcodes'] == 0 or sample['Work']['ProcessedMessages'] < config['iterations'] * len(case.steps):
            raise InvalidDocument('No completed measured work')
        callbacks = sample['Callbacks']
        if config['observeRuntime']:
            if callbacks['Started'] == 0 or callbacks['Started'] != callbacks['Completed'] or callbacks['Errors'] or callbacks['Limits']:
                raise InvalidDocument('Observer did not count successful work')
        elif any(callbacks.values()):
            raise InvalidDocument('Observer was enabled against request')
    measurement = observation['measurement']
    if measurement.get('metrics') != [{'id': 'run.allocated', 'unit': 'B', 'value': str(max(s['Bytes'] for s in samples))}]:
        raise InvalidDocument('Metric must be the exact maximum sampled allocation')
    return measurement


def evidence(case, observation):
    return performance_checks.compare(case, PROFILE, validate_evidence(case, observation))


def run(cases, command, timeout=60):
    correctness = verify(correctness_cases(cases), execute(command, requests(correctness_cases(cases)), timeout))
    # A failing correctness execution never launches a measurement process.
    if correctness['summary']['passed'] != len(cases):
        return {'correctness': correctness, 'measured': False, 'summary': correctness['summary']}
    request = measurement_request(cases)
    measured = execute([*command, '--measure'], request, timeout)
    closed(measured, {'formatVersion', 'requestDigest', 'implementation', 'capabilities', 'observations'}, 'measurement envelope')
    if type(measured.get('formatVersion')) is not int or measured['formatVersion'] != 1 or measured.get('requestDigest') != request['requestDigest'] or measured.get('implementation') != correctness['implementation']:
        raise InvalidDocument('Measurement identity or digest mismatch')
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
