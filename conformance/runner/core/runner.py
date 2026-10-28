# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Independent assertions against implementation observations, without GES dependencies."""
import hashlib
import json

from core.corpus import InvalidDocument, closed, logical_lines
from checks.compiler_checks import SUPPORTED as COMPILER_KINDS, expectation, compare
from checks import binary_checks
from checks import script_checks
from checks import api_checks
from checks.numeric_checks import validate_authored

SUPPORTED = COMPILER_KINDS | {'programBinary', 'scriptApi'} | api_checks.KINDS


def unsupported_reason(case):
    if case.kind not in SUPPORTED:
        return 'Test kind is not implemented yet'
    if case.kind == 'scriptApi' or case.kind in api_checks.KINDS:
        try:
            (script_checks.prepare if case.kind == 'scriptApi' else api_checks.prepare)(case)
        except (ValueError, TypeError, KeyError) as error:
            return str(error)
    return None


def supported(case):
    return unsupported_reason(case) is None

CORE = {'compiler', 'program-binary', 'host', 'vm', 'message-api', 'value-api', 'document-api',
        'external-types', 'native-handlers', 'publish-sink', 'observer'}
OPTIONAL = {'performance', 'bytecode-snapshot'}


def encode(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':'), allow_nan=False).encode('utf-8')


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise InvalidDocument('Duplicate JSON key: ' + key)
        result[key] = value
    return result


def read_json(path, limit=32 * 1024 * 1024):
    with open(path, 'rb') as stream:
        data = stream.read(limit + 1)
    if len(data) > limit:
        raise InvalidDocument('JSON input size limit exceeded')
    def invalid_constant(value):
        raise InvalidDocument('Invalid JSON constant: ' + value)
    value = json.loads(data.decode('utf-8'), object_pairs_hook=unique_object, parse_constant=invalid_constant)
    encode(value)  # Reject unpaired surrogates and non-finite values, including overflow.
    return value


def case_request(case, fixtures):
    validate_authored(case.metadata)
    validate_authored(case.expected)
    if case.kind not in SUPPORTED:
        raise InvalidDocument('Unsupported execution kind: ' + case.kind)
    api = api_checks.prepare(case) if case.kind in api_checks.KINDS else None
    binary = binary_checks.prepare(case, fixtures) if case.kind == 'programBinary' else None
    runtime = script_checks.prepare(case) if case.kind == 'scriptApi' else None
    if binary is None and runtime is None and api is None:
        expectation(case)
    if binary is None and runtime is None and api is None and (not case.sources or case.steps):
        raise InvalidDocument('Invalid compiler source/expectation shape: ' + case.id)
    if runtime is None and case.sources and len({source['program'] for source in case.sources}) != 1:
        raise InvalidDocument('Compiler case requires one Program: ' + case.id)
    fields = {'gesBlock', 'id', 'kind', 'level', 'categories', 'tags', 'requires', 'sources', 'compile'}
    if runtime is not None:
        fields.update({'runtimeLimits', 'comparison', 'random', 'hostCount', 'nativeHandlers', 'stepActions', 'deferredPrograms', 'publishSink'})
    if case.kind == 'loadError':
        fields.update({'runtimeLimits', 'externalTypeRegistry'})
    elif case.kind == 'messageApi':
        fields.update({'messageApi', 'comparison'})
    elif case.kind == 'valueApi':
        fields.update({'valueApi', 'comparison'})
    elif case.kind == 'documentApi':
        fields.update({'documentApi', 'comparison'})
    elif case.kind == 'externalTypeApi':
        fields.add('externalTypeApi')
    closed(case.metadata, fields | ({'binaryFixture', 'runtimeLimits'} if binary is not None else set()), 'case metadata')
    compile_options = {'debugInfo': ['debugSymbols', 'sourceMap', 'sourceArchive'], 'binaryRoundTrip': False}
    overrides = case.metadata.get('compile', {})
    closed(overrides, set(compile_options), 'compile options')
    compile_options.update(overrides)
    debug = compile_options['debugInfo']
    if (not isinstance(debug, list) or any(not isinstance(x, str) for x in debug)
            or len(set(debug)) != len(debug) or set(debug) - {'debugSymbols', 'sourceMap', 'sourceArchive'}
            or type(compile_options['binaryRoundTrip']) is not bool):
        raise InvalidDocument('Invalid compile options')
    if binary is not None and compile_options['binaryRoundTrip']:
        raise InvalidDocument('Binary fixture cannot request a second roundtrip')
    requires = case.metadata.get('requires', {})
    closed(requires, {'core', 'optional'}, 'requirements')
    requirements = {}
    core = ['message-api'] if case.kind == 'messageApi' else ['value-api'] if case.kind == 'valueApi' else ['document-api'] if case.kind == 'documentApi' else ['external-types'] if case.kind == 'externalTypeApi' else ['program-binary'] if binary is not None else ['compiler']
    if case.kind == 'loadError':
        core.extend(['host', 'vm'])
    if runtime is not None or (binary is not None and case.steps):
        core.extend(['host', 'vm', 'observer', 'publish-sink'])
    if binary is not None and binary['compareCompiledRuntime']:
        core.append('compiler')
    for kind, allowed, implicit in [('core', CORE, core), ('optional', OPTIONAL, ['bytecode-snapshot'] if case.assembler is not None else [])]:
        values = requires.get(kind, [])
        if not isinstance(values, list) or any(not isinstance(v, str) or v not in allowed for v in values):
            raise InvalidDocument('Invalid capabilities')
        requirements[kind] = list(dict.fromkeys(implicit + values))
    if compile_options['binaryRoundTrip'] and 'program-binary' not in requirements['core']:
        requirements['core'].append('program-binary')
    result = {'id': case.id, 'kind': case.kind, 'sources': case.sources if binary is None or binary['compareCompiledRuntime'] else [],
              'compile': compile_options, 'requires': requirements}
    if api is not None:
        result['api'] = api
    if runtime is not None:
        result['runtime'] = runtime
    if binary is not None:
        result['binary'] = binary
    return result


def requests(cases, fixtures=binary_checks.FIXTURES):
    if not cases or len({case.id for case in cases}) != len(cases):
        raise InvalidDocument('Empty selection or duplicate case IDs')
    entries = [case_request(case, fixtures) for case in cases]
    return {'formatVersion': 1, 'requestDigest': hashlib.sha256(encode(entries)).hexdigest(), 'cases': entries}


def snapshot_difference(expected, actual):
    left, right = (logical_lines(text).encode('utf-8', errors='strict') for text in (expected, actual))
    if left == right:
        return None
    offset = next((i for i, pair in enumerate(zip(left, right)) if pair[0] != pair[1]), min(len(left), len(right)))
    return {'path': '/assembler', 'firstDifferentUtf8Byte': offset, 'line': left[:offset].count(b'\n') + 1,
            'expected': logical_lines(expected), 'actual': logical_lines(actual)}


def verify(cases, observed, fixtures=binary_checks.FIXTURES):
    request = requests(cases, fixtures)
    closed(observed, {'formatVersion', 'requestDigest', 'implementation', 'capabilities', 'observations'}, 'observation envelope')
    if type(observed.get('formatVersion')) is not int or observed['formatVersion'] != 1 or observed.get('requestDigest') != request['requestDigest']:
        raise InvalidDocument('Observation version or request digest mismatch')
    implementation = observed.get('implementation')
    closed(implementation, {'id', 'version'}, 'implementation')
    if any(not isinstance(implementation.get(key), str) or not implementation[key] for key in ('id', 'version')):
        raise InvalidDocument('Missing implementation identity')
    capabilities = observed.get('capabilities')
    if not isinstance(capabilities, list) or any(not isinstance(x, str) or x not in CORE | OPTIONAL for x in capabilities):
        raise InvalidDocument('Invalid observation capabilities')
    if len(set(capabilities)) != len(capabilities):
        raise InvalidDocument('Duplicate observation capability')
    observations = observed.get('observations')
    if not isinstance(observations, dict) or set(observations) - {case.id for case in cases}:
        raise InvalidDocument('Unexpected observation IDs or invalid observation mapping')
    results = []
    for case, input_case in zip(cases, request['cases']):
        result = {'id': case.id, 'status': 'passed', 'code': 'conformance.passed'}
        missing_core = set(input_case['requires']['core']) - set(capabilities)
        missing_optional = set(input_case['requires']['optional']) - set(capabilities)
        observation = observations.get(case.id)
        if missing_core:
            result.update(status='error', code='conformance.runner.missingCoreCapability', missing=sorted(missing_core))
        elif missing_optional:
            result.update(status='skipped', code='conformance.runner.missingOptionalCapability', missing=sorted(missing_optional))
        elif observation is None:
            result.update(status='error', code='python.observation.missing')
        else:
            status, differences = (binary_checks.compare(case, observation, input_case['binary'])
                                   if case.kind == 'programBinary' else script_checks.compare(case, observation)
                                   if case.kind == 'scriptApi' else api_checks.compare(case, observation)
                                   if case.kind in api_checks.KINDS else compare(case, observation))
            if status == 'error':
                result.update(status='error', code='python.observation.executionError', detail=observation.get('detail'))
            elif status == 'failed':
                result.update(status='failed', code='conformance.assertion.mismatch', mismatches=differences)
            elif case.assembler is not None:
                mismatch = snapshot_difference(case.assembler, observation['dump'])
                if mismatch:
                    result.update(status='failed', code='conformance.assertion.mismatch', mismatch=mismatch)
            if case.assembler is not None and isinstance(observation.get('dump'), str):
                result['actualAssembler'] = logical_lines(observation['dump'])
            if observation.get('diagnostics'):
                result['diagnostics'] = observation['diagnostics']
        results.append(result)
    return {'formatVersion': 1, 'runner': 'ges-python-conformance/0.3', 'implementation': implementation,
            'requestDigest': request['requestDigest'], 'capabilities': capabilities, 'results': results,
            'summary': {status: sum(result['status'] == status for result in results)
                        for status in ('passed', 'failed', 'error', 'skipped')}}
