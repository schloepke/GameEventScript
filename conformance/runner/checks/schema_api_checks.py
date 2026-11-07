# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""GES schemas: source and binding errors, canonical form and hash, exact bytes, decoding and document checks."""
import re
from core.corpus import InvalidDocument, closed
from checks.runtime_checks import value
from checks.value_api_checks import input_value
from checks import numeric_checks

HEX = re.compile(r'(?:[0-9a-f]{2})*')
HASH = re.compile(r'[0-9a-f]{16}')
SCHEMA_CODE = re.compile(r'schema\.[a-zA-Z]+')
WIRE_CODE = re.compile(r'wire\.[a-zA-Z]+')
DOCUMENT_CODE = re.compile(r'(document|schema)\.[a-zA-Z]+')
NUMERIC_PARAMETERS = {':Number.int64', ':Number.binary64', ':Quantity.int64', ':Quantity.binary64'}


def text(raw, label):
    if not isinstance(raw, str):
        raise InvalidDocument('Expected text: ' + label)
    return raw


def hexadecimal(raw):
    if not isinstance(raw, str) or not HEX.fullmatch(raw):
        raise InvalidDocument('Expected lowercase hexadecimal bytes')
    return raw


def delta_input(raw, name):
    closed(raw, {name, 'baseline'}, 'delta input')
    if set(raw) != {name, 'baseline'}:
        raise InvalidDocument('A delta input requires its bytes and a baseline')
    return {name: hexadecimal(raw[name]), 'baseline': hexadecimal(raw['baseline'])}


def positive(raw):
    if type(raw) is not int or raw < 0 or raw >= 2**31:
        raise InvalidDocument('Expected a small nonnegative integer')
    return raw


def positioned(raw, pattern):
    closed(raw, {'code', 'line', 'column'}, 'schema error')
    if not isinstance(raw.get('code'), str) or not pattern.fullmatch(raw['code']):
        raise InvalidDocument('Invalid schema error code')
    for key in ('line', 'column'):
        if key in raw and (type(raw[key]) is not int or raw[key] < 0):
            raise InvalidDocument('Invalid schema error position')
    return raw


def wire_error(raw):
    closed(raw, {'code', 'path'}, 'wire error')
    if not isinstance(raw.get('code'), str) or not WIRE_CODE.fullmatch(raw['code']):
        raise InvalidDocument('Invalid wire error code')
    if 'path' in raw and not isinstance(raw['path'], str):
        raise InvalidDocument('Invalid wire error path')
    return raw


def is_record(raw):
    return isinstance(raw, dict) and isinstance(raw.get('type'), str) and raw['type'].startswith(':') and raw['type'] != ':Map' and isinstance(raw.get('entries'), list) \
        and all(isinstance(entry, dict) and isinstance(entry.get('key'), str) and 'value' in entry for entry in raw['entries'])


def external_inputs(raw):
    """Only Records can be exposed as adapter-owned external values, because they carry the type name and the fields."""
    if 'encodeExternal' in raw and (raw['encodeExternal'] is not True or not is_record(raw.get('encode'))):
        raise InvalidDocument('An external encoding wraps a Record')
    if 'baselineExternal' in raw and (raw['baselineExternal'] is not True or not is_record(raw.get('baseline'))):
        raise InvalidDocument('An external baseline wraps a Record')
    if 'externalFields' in raw or 'failingField' in raw:
        if not is_record(raw.get('encode')):
            raise InvalidDocument('External fields belong to a Record')
        entries = {entry['key']: entry['value'] for entry in raw['encode']['entries']}
    if 'externalFields' in raw:
        names = raw['externalFields']
        if not isinstance(names, list) or not names or not all(isinstance(name, str) and is_record(entries.get(name)) for name in names):
            raise InvalidDocument('External fields name Record fields')
    if 'failingField' in raw and ('encodeExternal' not in raw or not isinstance(raw['failingField'], str) or raw['failingField'] not in entries):
        raise InvalidDocument('A failing field belongs to an external encoding')
    if 'baselineFailingField' in raw and ('baselineExternal' not in raw or raw['baselineFailingField'] not in [entry['key'] for entry in raw['baseline']['entries']]):
        raise InvalidDocument('A failing baseline field belongs to an external baseline')
    if 'failingCode' in raw and ('failingField' not in raw and 'baselineFailingField' not in raw or not isinstance(raw['failingCode'], str) or not raw['failingCode'].strip() or raw['failingCode'].startswith('runtime.')):
        raise InvalidDocument('A failing code is the code of a deliberate fault on a failing field')


def prepare(case):
    if case.sources or case.steps or case.assembler is not None:
        raise InvalidDocument('Schema API requires no GES source, Steps or assembler')
    numeric_checks.options(case)
    raw = case.metadata.get('schemaApi')
    closed(raw, {'sources', 'root', 'parameters', 'bind', 'encode', 'encodeExternal', 'baselineExternal', 'externalFields', 'failingField', 'baselineFailingField', 'failingCode', 'encodeBuffer', 'baseline', 'decode', 'decodeLimit', 'key', 'dump', 'dumpBaseline', 'deltaEncode', 'deltaDecode', 'describe', 'document'}, 'schema API input')
    sources = raw.get('sources')
    if not isinstance(sources, list) or not sources or any(not isinstance(s, str) for s in sources):
        raise InvalidDocument('Schema API requires source texts')
    parameters = raw.get('parameters', {})
    closed(parameters, set(parameters), 'schema parameters')
    if any(not isinstance(v, str) and (not isinstance(v, dict) or v.get('type') not in NUMERIC_PARAMETERS) for v in parameters.values()):
        raise InvalidDocument('Schema parameters are literals or numeric values')
    if 'bind' in raw and type(raw['bind']) is not bool:
        raise InvalidDocument('Invalid bind flag')
    if 'describe' in raw and (raw['describe'] is not True or 'root' not in raw):
        raise InvalidDocument('A description requires a root')
    if ('encode' in raw or 'decode' in raw or 'key' in raw or 'dump' in raw or 'deltaEncode' in raw or 'deltaDecode' in raw) and 'root' not in raw:
        raise InvalidDocument('Encoding requires a root')
    external_inputs(raw)
    if 'encodeBuffer' in raw and 'baseline' in raw:
        raise InvalidDocument('A buffer size belongs to a full encoding, because the Swift delta API takes none')
    if 'baseline' in raw and 'encode' not in raw and 'decode' not in raw:
        raise InvalidDocument('A baseline requires encoding or decoding')
    if 'dumpBaseline' in raw and 'dump' not in raw:
        raise InvalidDocument('A dump baseline requires a dump')
    request = {
        'sources': sources,
        'root': text(raw['root'], 'root') if 'root' in raw else None,
        'parameters': [{'name': k, 'literal': parameters[k]} if isinstance(parameters[k], str) else {'name': k, 'value': input_value(parameters[k])} for k in sorted(parameters)],
        'bind': raw.get('bind', 'document' not in raw and 'root' in raw),
        'encode': input_value(raw['encode']) if 'encode' in raw else None,
        'encodeExternal': raw.get('encodeExternal', False),
        'baselineExternal': raw.get('baselineExternal', False),
        'externalFields': raw.get('externalFields', []),
        'failingField': raw.get('failingField'),
        'baselineFailingField': raw.get('baselineFailingField'),
        'failingCode': raw.get('failingCode'),
        'encodeBuffer': positive(raw['encodeBuffer']) if 'encodeBuffer' in raw else None,
        'baseline': input_value(raw['baseline']) if 'baseline' in raw else None,
        'decode': hexadecimal(raw['decode']) if 'decode' in raw else None,
        'decodeLimit': positive(raw['decodeLimit']) if 'decodeLimit' in raw else None,
        'key': hexadecimal(raw['key']) if 'key' in raw else None,
        'dump': hexadecimal(raw['dump']) if 'dump' in raw else None,
        'dumpBaseline': hexadecimal(raw['dumpBaseline']) if 'dumpBaseline' in raw else None,
        'deltaEncode': delta_input(raw['deltaEncode'], 'record') if 'deltaEncode' in raw else None,
        'deltaDecode': delta_input(raw['deltaDecode'], 'delta') if 'deltaDecode' in raw else None,
        'describe': raw.get('describe', False),
        'document': text(raw['document'], 'document') if 'document' in raw else None,
    }
    closed(case.expected, {'gesBlock', 'schema'}, 'schema expectation')
    expected = case.expected.get('schema')
    closed(expected, {'error', 'canonical', 'hash', 'bindingHash', 'maxByteCount', 'encoded', 'decoded', 'deltaEncoded', 'deltaDecoded', 'key', 'dump', 'description', 'document'}, 'schema result')
    if not expected:
        raise InvalidDocument('Schema expectation requires a result')
    if 'error' in expected:
        if set(expected) != {'error'}:
            raise InvalidDocument('A schema error stands alone')
        positioned(expected['error'], SCHEMA_CODE)
    if 'canonical' in expected:
        text(expected['canonical'], 'canonical')
    for key in ('hash', 'bindingHash'):
        if key in expected and (not isinstance(expected[key], str) or not HASH.fullmatch(expected[key])):
            raise InvalidDocument('Invalid hash')
    if 'maxByteCount' in expected:
        positive(expected['maxByteCount'])
    if 'encoded' in expected:
        if request['encode'] is None:
            raise InvalidDocument('An encoding expectation requires encode')
        closed(expected['encoded'], {'bytes', 'error', 'failure'}, 'encoding result')
        if len(expected['encoded']) != 1:
            raise InvalidDocument('An encoding has bytes, an error or a failure')
        if 'bytes' in expected['encoded']:
            hexadecimal(expected['encoded']['bytes'])
        elif 'failure' in expected['encoded']:
            if not isinstance(expected['encoded']['failure'], str) or 'failingField' not in raw and 'baselineFailingField' not in raw:
                raise InvalidDocument('A getter failure requires a failing field')
        else:
            wire_error(expected['encoded']['error'])
    if 'deltaEncoded' in expected:
        if request['deltaEncode'] is None:
            raise InvalidDocument('A delta expectation requires deltaEncode')
        closed(expected['deltaEncoded'], {'bytes', 'error'}, 'delta result')
        if len(expected['deltaEncoded']) != 1:
            raise InvalidDocument('A delta has bytes or an error')
        if 'bytes' in expected['deltaEncoded']:
            hexadecimal(expected['deltaEncoded']['bytes'])
        else:
            wire_error(expected['deltaEncoded']['error'])
    if 'deltaDecoded' in expected:
        if request['deltaDecode'] is None:
            raise InvalidDocument('A delta expectation requires deltaDecode')
        closed(expected['deltaDecoded'], {'record', 'deltaByteCount', 'error'}, 'delta result')
        if 'error' in expected['deltaDecoded']:
            if len(expected['deltaDecoded']) != 1:
                raise InvalidDocument('A delta result has a record or an error')
            wire_error(expected['deltaDecoded']['error'])
        elif set(expected['deltaDecoded']) != {'record', 'deltaByteCount'}:
            raise InvalidDocument('A delta result has a record and its size')
        else:
            hexadecimal(expected['deltaDecoded']['record'])
    if 'decoded' in expected:
        if request['decode'] is None:
            raise InvalidDocument('A decoding expectation requires decode')
        closed(expected['decoded'], {'value', 'byteCount', 'error', 'failure'}, 'decoding result')
        if 'failure' in expected['decoded']:
            if len(expected['decoded']) != 1 or not isinstance(expected['decoded']['failure'], str) or 'baselineFailingField' not in raw:
                raise InvalidDocument('A decoding failure stands alone and requires a failing baseline field')
        elif 'error' in expected['decoded']:
            if len(expected['decoded']) != 1:
                raise InvalidDocument('A decoding error stands alone')
            wire_error(expected['decoded']['error'])
        else:
            value(expected['decoded']['value'])
            positive(expected['decoded']['byteCount'])
    if 'key' in expected and (type(expected['key']) is not int or request['key'] is None):
        raise InvalidDocument('A key expectation requires key data')
    if 'description' in expected and not request['describe']:
        raise InvalidDocument('A description expectation requires describe')
    if 'dump' in expected:
        if request['dump'] is None:
            raise InvalidDocument('A dump expectation requires dump data')
        closed(expected['dump'], {'text', 'valid'}, 'dump result')
        if set(expected['dump']) != {'text', 'valid'} or not isinstance(expected['dump']['text'], str) or type(expected['dump']['valid']) is not bool:
            raise InvalidDocument('A dump has its text and validity')
    if 'document' in expected:
        if request['document'] is None:
            raise InvalidDocument('A document expectation requires a document')
        closed(expected['document'], {'ok', 'error'}, 'document check result')
        if 'error' in expected['document']:
            positioned(expected['document']['error'], DOCUMENT_CODE)
        elif expected['document'].get('ok') is not True:
            raise InvalidDocument('A document check is ok or has an error')
    return request


def contains(expected, actual):
    return isinstance(actual, dict) and all(actual.get(k) == v for k, v in expected.items())


def compare(case, observed):
    closed(observed, {'status', 'detail', 'schema'}, 'schema API observation')
    if observed.get('status') == 'error':
        if set(observed) - {'status', 'detail'} or not isinstance(observed.get('detail', ''), str):
            raise InvalidDocument('Invalid schema API error')
        return 'error', []
    if set(observed) != {'status', 'schema'} or observed['status'] != 'ok':
        raise InvalidDocument('Invalid schema API outcome')
    actual = observed['schema']
    closed(actual, {'error', 'canonical', 'hash', 'bindingHash', 'maxByteCount', 'encoded', 'decoded', 'deltaEncoded', 'deltaDecoded', 'key', 'dump', 'description', 'document'}, 'schema observation')
    expected = case.expected['schema']
    differences = []
    def differ(path, wanted, seen):
        differences.append({'path': '/schema/' + path, 'expected': wanted, 'actual': seen})
    if 'error' in expected:
        if not contains(expected['error'], actual.get('error')):
            differ('error', expected['error'], actual)
        return ('passed', []) if not differences else ('failed', differences)
    if 'error' in actual:
        differ('error', None, actual['error'])
        return 'failed', differences
    for key in ('canonical', 'hash', 'bindingHash', 'maxByteCount', 'key', 'dump', 'description'):
        if key in expected and actual.get(key) != expected[key]:
            differ(key, expected[key], actual.get(key))
    for key in ('encoded', 'deltaEncoded', 'deltaDecoded'):
        if key not in expected:
            continue
        seen = actual.get(key)
        wanted = expected[key]
        if 'error' in wanted:
            if not isinstance(seen, dict) or not contains(wanted['error'], seen.get('error')):
                differ(key, wanted, seen)
        elif seen != wanted:
            differ(key, wanted, seen)
    if 'decoded' in expected:
        seen = actual.get('decoded')
        wanted = expected['decoded']
        if 'error' in wanted:
            if not isinstance(seen, dict) or not contains(wanted['error'], seen.get('error')):
                differ('decoded', wanted, seen)
        elif 'failure' in wanted:
            if seen != wanted:
                differ('decoded', wanted, seen)
        elif not isinstance(seen, dict) or 'value' not in seen or seen.get('byteCount') != wanted['byteCount'] \
                or not numeric_checks.equal(value(wanted['value']), input_value(seen['value']), numeric_checks.options(case)):
            differ('decoded', wanted, seen)
    if 'document' in expected:
        seen = actual.get('document')
        wanted = expected['document']
        if 'error' in wanted:
            if not isinstance(seen, dict) or not contains(wanted['error'], seen.get('error')):
                differ('document', wanted, seen)
        elif seen != {'ok': True}:
            differ('document', wanted, seen)
    return ('passed', []) if not differences else ('failed', differences)
