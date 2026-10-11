# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Message API construction inputs and independent result assertions."""
from core.corpus import InvalidDocument, closed
from checks.compiler_checks import exact
from checks.value_api_checks import input_value
from checks.runtime_checks import message as transport_message
from checks import numeric_checks

BOOLS = {'conformanceEquals', 'matches', 'signatureEquals', 'signatureHashEquals', 'messageEquals', 'messageHashEquals', 'handlerEquals', 'handlerHashEquals'}
TEXTS = {'name', 'signatureId', 'messageSignatureId', 'createdMessageSignatureId', 'json'}
ERRORS = {'invalidArgumentsShape', 'invalidMessage', 'duplicateArgumentName', 'invalidArgument', 'message.invalidJson', 'message.invalidValue', 'message.unsupportedVersion', 'message.unsupportedSeries', 'message.resourceLimit'}
PAIRS = {'compareSignature': {'signatureEquals', 'signatureHashEquals'}, 'compareMessage': {'messageEquals', 'messageHashEquals'}, 'compareHandler': {'handlerEquals', 'handlerHashEquals'}, 'createArguments': {'createdMessageSignatureId'}}


def signature(raw):
    closed(raw, {'name', 'parameters'}, 'signature')
    if not isinstance(raw.get('name'), str) or not isinstance(raw.get('parameters'), list) or any(not isinstance(p, str) for p in raw['parameters']):
        raise InvalidDocument('Invalid signature transport')
    return raw


def message(raw):
    closed(raw, {'name', 'args', 'tags'}, 'message API input')
    if not isinstance(raw.get('name'), str):
        raise InvalidDocument('Message name must be text')
    tags = raw.get('tags', [])
    if not isinstance(tags, list) or any(not isinstance(t, str) for t in tags):
        raise InvalidDocument('Tags must be text')
    args = raw.get('args', [])
    if isinstance(args, dict):
        args = {k: input_value(v) for k, v in args.items()}
    elif isinstance(args, list):
        for arg in args:
            closed(arg, {'name', 'value'}, 'argument')
            if not isinstance(arg.get('name'), str):
                raise InvalidDocument('Argument label must be text')
        args = [{'name': a['name'], 'value': input_value(a.get('value'))} for a in args]
    else:
        raise InvalidDocument('Invalid argument shape')
    return {'name': raw['name'], 'args': args, 'tags': tags}


def prepare(case):
    if case.sources or case.steps or case.assembler is not None:
        raise InvalidDocument('Message API requires no source, Steps or assembler')
    numeric_checks.options(case)
    raw = case.metadata.get('messageApi')
    closed(raw, {'signature', 'message', 'compareConformanceMessage', 'json', 'roundTripJson'} | PAIRS.keys(), 'message API input')
    result = {'signature': signature(raw.get('signature')), 'message': message(raw.get('message'))}
    if 'json' in raw:
        if not isinstance(raw['json'], str):
            raise InvalidDocument('Product JSON input must be text')
        result['json'] = raw['json']
    if 'roundTripJson' in raw:
        if type(raw['roundTripJson']) is not bool:
            raise InvalidDocument('JSON roundtrip flag must be Boolean')
        result['roundTripJson'] = raw['roundTripJson']
    json_mode = 'json' in raw or 'roundTripJson' in raw
    for key in PAIRS:
        if key in raw:
            if key == 'createArguments':
                if not isinstance(raw[key], list):
                    raise InvalidDocument('Expected creation arguments')
                result[key] = [input_value(v) for v in raw[key]]
            else:
                result[key] = message(raw[key]) if key == 'compareMessage' else signature(raw[key])
    closed(case.expected, {'gesBlock', 'message'}, 'message API expectation')
    expected = case.expected.get('message')
    closed(expected, BOOLS | TEXTS | {'argumentCount', 'error'}, 'message result')
    validate_result(expected)
    if 'json' in expected and not json_mode:
        raise InvalidDocument('JSON serialization requires an explicit JSON operation')
    if json_mode and 'compareConformanceMessage' in raw:
        result['includeNormalized'] = True
    comparison = raw.get('compareConformanceMessage')
    if ('compareConformanceMessage' in raw) != ('conformanceEquals' in expected):
        raise InvalidDocument('Conformance comparison requires both input and assertion')
    if 'compareConformanceMessage' in raw:
        transport_message(comparison)
        if 'error' in expected:
            raise InvalidDocument('Conformance comparison cannot expect construction failure')
    if 'error' in expected and set(expected) != {'error'}:
        raise InvalidDocument('Error cannot coexist with successful assertions')
    for key, assertions in PAIRS.items():
        if assertions & expected.keys() and key not in raw:
            raise InvalidDocument('Comparison requires its input')
    for key in ('message', 'compareMessage'):
        if key in result and isinstance(result[key]['args'], dict) and (key != 'message' or expected != {'error': 'invalidArgumentsShape'}):
            raise InvalidDocument('Argument mapping only allowed for negative shape case')
    return result


def validate_result(raw):
    if not raw:
        raise InvalidDocument('Empty message result')
    for k, v in raw.items():
        if k in BOOLS and type(v) is not bool or k in TEXTS and not isinstance(v, str):
            raise InvalidDocument('Invalid message result type')
        if k == 'argumentCount' and (type(v) is not int or not 0 <= v <= 65535):
            raise InvalidDocument('Invalid argument count')
        if k == 'error' and v not in ERRORS:
            raise InvalidDocument('Invalid message error')


def compare(case, observed):
    closed(observed, {'status', 'detail', 'message'}, 'message API observation')
    if observed.get('status') == 'error':
        if set(observed) - {'status', 'detail'} or not isinstance(observed.get('detail', ''), str):
            raise InvalidDocument('Invalid API error')
        return 'error', []
    if set(observed) != {'status', 'message'} or observed['status'] != 'ok':
        raise InvalidDocument('Invalid message outcome')
    actual = observed['message']
    json_mode = 'json' in case.metadata['messageApi'] or 'roundTripJson' in case.metadata['messageApi']
    fields = {'name', 'signatureId', 'messageSignatureId', 'matches', 'argumentCount', 'json' if json_mode else 'normalized'}
    if json_mode and 'compareConformanceMessage' in case.metadata['messageApi']:
        fields.add('normalized')
    for key, outputs in PAIRS.items():
        if key in case.metadata['messageApi']:
            fields |= outputs
    closed(actual, fields | {'error'}, 'message observation')
    if set(actual) not in (fields, {'error'}):
        raise InvalidDocument('Incomplete message observation')
    validate_result(actual)
    if 'normalized' in actual:
        transport_message(actual['normalized'])
    checked = dict(actual)
    if 'conformanceEquals' in case.expected['message'] and 'normalized' in actual:
        checked['conformanceEquals'] = numeric_checks.equal(
            transport_message(case.metadata['messageApi']['compareConformanceMessage']),
            transport_message(actual['normalized']), numeric_checks.options(case))
    differences = [{'path': '/message/' + k, 'expected': v, 'actual': checked.get(k)} for k, v in case.expected['message'].items() if not exact(v, checked.get(k))]
    return ('failed' if differences else 'passed'), differences
