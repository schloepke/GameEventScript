# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Public value API inputs preserve collection construction order and duplicates; literal inputs use the data
literal parsing API."""
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


LITERAL_MODES = {'lenient', 'strict'}
LITERAL_LIMITS = {'MaxLiteralInputScalars', 'MaxLiteralDepth', 'MaxLiteralItems', 'MaxRangeItems'}


def literal_limit(raw):
    closed(raw, {'name', 'limit'}, 'literal limit')
    if raw.keys() != {'name', 'limit'} or raw['name'] not in LITERAL_LIMITS or type(raw['limit']) is not int or not 0 < raw['limit'] < 2**31:
        raise InvalidDocument('Invalid literal limit')
    return raw


# Expanded repeated text stays bounded at twice MaxLiteralInputScalars.
MAX_REPEATED_SCALARS = 2 * 1048576


def literal_text(raw):
    """Literal text, or `prefix` + `count` copies of `repeat` + `suffix` for limit-sized inputs."""
    if isinstance(raw, str):
        return raw
    closed(raw, {'prefix', 'repeat', 'count', 'suffix'}, 'repeated literal text')
    prefix, suffix, repeat, count = raw.get('prefix', ''), raw.get('suffix', ''), raw.get('repeat'), raw.get('count')
    if not all(isinstance(part, str) for part in (prefix, suffix, repeat)) or not repeat or type(count) is not int or count < 1:
        raise InvalidDocument('Repeated literal text requires repeat and a positive count')
    if len(prefix) + len(repeat) * count + len(suffix) > MAX_REPEATED_SCALARS:
        raise InvalidDocument('Repeated literal text is too long')
    return prefix + repeat * count + suffix


def prepare_literal(case, raw):
    closed(raw, {'literal'}, 'value API input')
    literal = raw['literal']
    closed(literal, {'text', 'mode'}, 'literal input')
    if 'text' not in literal or literal.get('mode') not in LITERAL_MODES:
        raise InvalidDocument('Literal input requires text and mode')
    text = literal_text(literal['text'])
    closed(case.expected, {'gesBlock', 'literal'}, 'literal expectation')
    expected = case.expected.get('literal')
    closed(expected, {'value', 'unrecognized', 'limit'}, 'literal result')
    if len(expected) != 1:
        raise InvalidDocument('Literal expectation requires exactly one outcome')
    if 'value' in expected:
        value(expected['value'])
    elif 'limit' in expected:
        literal_limit(expected['limit'])
    elif expected['unrecognized'] is not True or literal['mode'] != 'strict':
        raise InvalidDocument('Only strict literals can be unrecognized')
    return {'literal': {'text': text, 'mode': literal['mode']}}


def prepare(case):
    if case.sources or case.steps or case.assembler is not None:
        raise InvalidDocument('Value API requires no source, Steps or assembler')
    numeric_checks.options(case)
    raw = case.metadata.get('valueApi')
    if isinstance(raw, dict) and 'literal' in raw:
        return prepare_literal(case, raw)
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


def compare_literal(case, observed):
    if set(observed) != {'status', 'literal'} or observed['status'] != 'ok':
        raise InvalidDocument('Invalid literal outcome')
    actual = observed['literal']
    closed(actual, {'value', 'limit'}, 'literal observation')
    if actual.keys() != {'value', 'limit'} or (actual['value'] is not None and actual['limit'] is not None):
        raise InvalidDocument('Invalid literal observation')
    if actual['value'] is not None:
        value(actual['value'])
    if actual['limit'] is not None:
        literal_limit(actual['limit'])
    expected = case.expected['literal']
    if 'value' in expected:
        observed_value = input_value(actual['value']) if actual['value'] is not None else None
        ok = observed_value is not None and numeric_checks.equal(value(expected['value']), observed_value, numeric_checks.options(case))
        difference = {'path': '/literal/value', 'expected': value(expected['value']), 'actual': observed_value if observed_value is not None else actual['limit']}
    elif 'limit' in expected:
        ok = actual['limit'] == expected['limit']
        difference = {'path': '/literal/limit', 'expected': expected['limit'], 'actual': actual['limit'] if actual['limit'] is not None else actual['value']}
    else:
        ok = actual['value'] is None and actual['limit'] is None
        difference = {'path': '/literal/unrecognized', 'expected': True, 'actual': actual['value'] if actual['value'] is not None else actual['limit']}
    return ('passed', []) if ok else ('failed', [difference])


def compare(case, observed):
    closed(observed, {'status', 'detail', 'value', 'literal'}, 'value API observation')
    if observed.get('status') == 'error':
        if set(observed) - {'status', 'detail'} or not isinstance(observed.get('detail', ''), str):
            raise InvalidDocument('Invalid value API error')
        return 'error', []
    if 'literal' in case.metadata['valueApi']:
        return compare_literal(case, observed)
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
