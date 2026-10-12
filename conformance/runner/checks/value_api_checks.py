# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Public value API inputs preserve collection construction order and duplicates."""
from core.corpus import InvalidDocument, closed
from checks.runtime_checks import value
from checks import numeric_checks

FLAGS = {'isNumeric', 'hasValue', 'isNothing', 'hasUnit', 'asBoolean', 'equal', 'equalHash', 'notEqual'}


def input_value(raw):
    normalized = value(raw)
    if 'entries' in raw:
        normalized['entries'] = [{'key': e['key'], 'value': input_value(e['value'])} for e in raw['entries']]
    if raw['type'] == ':List':
        normalized['items'] = [input_value(item) for item in raw['items']]
    if raw['type'] == ':Message':
        normalized['message']['args'] = [{'name': a.get('name', ''), 'value': input_value(a['value'])} for a in raw['message'].get('args', [])]
    return normalized


def prepare(case):
    if case.sources or case.steps or case.assembler is not None:
        raise InvalidDocument('Value API requires no source, Steps or assembler')
    numeric_checks.options(case)
    raw = case.metadata.get('valueApi')
    closed(raw, {'value', 'equalTo', 'notEqualTo', 'mutateSourceAfterCreate'}, 'value API input')
    result = {'value': input_value(raw.get('value')), 'mutateSourceAfterCreate': raw.get('mutateSourceAfterCreate', False)}
    if type(result['mutateSourceAfterCreate']) is not bool:
        raise InvalidDocument('Mutation flag must be Boolean')
    for key in ('equalTo', 'notEqualTo'):
        result[key] = input_value(raw[key]) if key in raw else None
    closed(case.expected, {'gesBlock', 'value'}, 'value API expectation')
    expected = case.expected.get('value')
    closed(expected, FLAGS | {'normalized', 'length', 'customTypeName'}, 'value API result')
    value(expected.get('normalized'))
    for key in FLAGS & expected.keys():
        if type(expected[key]) is not bool:
            raise InvalidDocument('Expected Boolean value flag')
    if 'length' in expected and (type(expected['length']) is not int or not 0 <= expected['length'] < 2**32):
        raise InvalidDocument('Invalid value length')
    if 'customTypeName' in expected and not isinstance(expected['customTypeName'], str):
        raise InvalidDocument('Invalid custom type name')
    if ({'equal', 'equalHash'} & expected.keys() and result['equalTo'] is None) or ('notEqual' in expected and result['notEqualTo'] is None):
        raise InvalidDocument('Equality assertion requires its comparison input')
    return result


def compare(case, observed):
    closed(observed, {'status', 'detail', 'value'}, 'value API observation')
    if observed.get('status') == 'error':
        if set(observed) - {'status', 'detail'} or not isinstance(observed.get('detail', ''), str):
            raise InvalidDocument('Invalid value API error')
        return 'error', []
    if set(observed) != {'status', 'value'} or observed['status'] != 'ok':
        raise InvalidDocument('Invalid value API outcome')
    actual = observed['value']
    fields = FLAGS | {'normalized', 'length', 'customTypeName'}
    closed(actual, fields, 'value API result')
    if actual.keys() != fields:
        raise InvalidDocument('Incomplete value API result')
    value(actual['normalized'])
    for key in FLAGS:
        optional = (key in ('equal', 'equalHash') and 'equalTo' not in case.metadata['valueApi']) or (key == 'notEqual' and 'notEqualTo' not in case.metadata['valueApi'])
        if not (actual[key] is None if optional else type(actual[key]) is bool):
            raise InvalidDocument('Invalid observed value flag')
    if type(actual['length']) is not int or not 0 <= actual['length'] < 2**32 or (actual['customTypeName'] is not None and not isinstance(actual['customTypeName'], str)):
        raise InvalidDocument('Invalid observed value metadata')
    differences = []
    for key, expected in case.expected['value'].items():
        observed_value = actual[key]
        if key == 'normalized':
            expected, observed_value = value(expected), input_value(observed_value)
        if not numeric_checks.equal(expected, observed_value, numeric_checks.options(case)):
            differences.append({'path': '/value/' + key, 'expected': expected, 'actual': observed_value})
    return ('failed' if differences else 'passed'), differences
