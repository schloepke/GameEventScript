# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""GES document reading and printing: documents read from text or built from a value, then printed and read back."""
from core.corpus import InvalidDocument, closed
from checks.runtime_checks import value
from checks.value_api_checks import input_value, literal_text
from checks import numeric_checks

CODES = {'invalidKey', 'missingColon', 'duplicateKey', 'unquotedText', 'unknownUnit', 'invalidLiteral', 'unknownType',
         'unexpectedToken', 'unclosedBracket', 'unclosedText', 'invalidSchemaHeader', 'misplacedSchemaHeader',
         'limitExceeded'}
LIMITS = {'MaxLiteralInputScalars', 'MaxLiteralDepth', 'MaxLiteralItems', 'MaxRangeItems'}
FORMAT = {'indentation', 'lineWidth', 'prefixedUnits'}


def schema(raw):
    if raw is None:
        return None
    closed(raw, {'kind', 'name'}, 'document schema')
    if raw.keys() != {'kind', 'name'} or raw['kind'] not in ('type', 'file') or not isinstance(raw['name'], str) or not raw['name']:
        raise InvalidDocument('Invalid document schema')
    return {'kind': raw['kind'], 'name': raw['name']}


def error(raw):
    closed(raw, {'code', 'line', 'column', 'limitName', 'limit'}, 'document error')
    if not {'code', 'line', 'column'} <= raw.keys() or raw['code'] not in CODES:
        raise InvalidDocument('Invalid document error')
    if any(type(raw[k]) is not int or raw[k] < 1 for k in ('line', 'column')):
        raise InvalidDocument('Invalid document error position')
    limited = raw['code'] == 'limitExceeded'
    if limited != ('limitName' in raw) or limited != ('limit' in raw):
        raise InvalidDocument('Only limit errors carry a limit')
    if limited and (raw['limitName'] not in LIMITS or type(raw['limit']) is not int or not 0 < raw['limit'] < 2**31):
        raise InvalidDocument('Invalid document limit')
    return dict(raw)


def print_format(raw):
    if raw is True:
        return {}
    closed(raw, FORMAT, 'document print format')
    if any(type(raw[k]) is not int for k in ('indentation', 'lineWidth') if k in raw) or ('prefixedUnits' in raw and type(raw['prefixedUnits']) is not bool):
        raise InvalidDocument('Invalid document print format')
    if raw.get('indentation', 0) < 0 or raw.get('lineWidth', 1) < 1:
        raise InvalidDocument('Invalid document print format')
    return dict(raw)


def prepare(case):
    if case.sources or case.steps or case.assembler is not None:
        raise InvalidDocument('Document API requires no source, Steps or assembler')
    numeric_checks.options(case)
    raw = case.metadata.get('documentApi')
    closed(raw, {'text', 'value', 'schema', 'print'}, 'document API input')
    if ('text' in raw) == ('value' in raw):
        raise InvalidDocument('Document API requires either text or value')
    if 'schema' in raw and 'text' in raw:
        raise InvalidDocument('A document read from text takes its schema from the header')
    request = {'text': literal_text(raw['text'])} if 'text' in raw else {'value': input_value(raw['value']), 'schema': schema(raw.get('schema'))}
    request['print'] = print_format(raw['print']) if 'print' in raw else None
    if 'value' in raw and request['print'] is None:
        raise InvalidDocument('A document built from a value is checked by printing it')
    closed(case.expected, {'gesBlock', 'document'}, 'document expectation')
    expected = case.expected.get('document')
    closed(expected, {'value', 'schema', 'error', 'rejected'}, 'document result')
    if 'error' in expected:
        if expected.keys() != {'error'} or 'value' in raw or request['print'] is not None:
            raise InvalidDocument('A document error stands alone and is read from text without printing')
        error(expected['error'])
    elif 'rejected' in expected:
        if expected != {'rejected': True} or 'text' in raw:
            raise InvalidDocument('Only a document built from a value can be rejected')
    else:
        if 'value' not in expected:
            raise InvalidDocument('Document expectation requires a value, an error or a rejection')
        value(expected['value'])
        schema(expected.get('schema'))
    return request


def observed_document(raw, label):
    closed(raw, {'value', 'schema', 'error'}, label)
    if 'error' in raw:
        if raw.keys() != {'error'}:
            raise InvalidDocument('Invalid ' + label)
        return {'error': error(raw['error'])}
    if raw.keys() != {'value', 'schema'}:
        raise InvalidDocument('Invalid ' + label)
    return {'value': input_value(raw['value']), 'schema': schema(raw['schema'])}


def compare(case, observed):
    closed(observed, {'status', 'detail', 'document'}, 'document API observation')
    if observed.get('status') == 'error':
        if set(observed) - {'status', 'detail'} or not isinstance(observed.get('detail', ''), str):
            raise InvalidDocument('Invalid document API error')
        return 'error', []
    if set(observed) != {'status', 'document'} or observed['status'] != 'ok':
        raise InvalidDocument('Invalid document API outcome')
    actual = observed['document']
    closed(actual, {'read', 'rejected', 'printed', 'reread'}, 'document observation')
    raw = case.metadata['documentApi']
    expected = case.expected['document']
    printing = 'print' in raw
    if actual.get('rejected') is True:
        if actual.keys() != {'rejected'} or 'value' not in raw:
            raise InvalidDocument('Invalid document rejection')
        ok = expected.get('rejected') is True
        return ('passed', []) if ok else ('failed', [{'path': '/document', 'expected': expected, 'actual': {'rejected': True}}])
    keys = ({'read'} if 'text' in raw else set()) | ({'printed', 'reread'} if printing else set())
    if actual.keys() != keys or ('printed' in actual and not isinstance(actual['printed'], str)):
        raise InvalidDocument('Incomplete document observation')
    differences = []
    results = []
    if 'read' in actual:
        results.append(('/document/read', observed_document(actual['read'], 'read document')))
    if printing:
        reread = observed_document(actual['reread'], 'reread document')
        if 'error' in reread:
            return 'failed', [{'path': '/document/reread', 'expected': 'printed text reads back', 'actual': reread, 'printed': actual['printed']}]
        results.append(('/document/reread', reread))
    for path, result in results:
        if 'error' in expected or 'error' in result:
            if result != {'error': expected.get('error')}:
                differences.append({'path': path, 'expected': expected, 'actual': result})
            continue
        if 'rejected' in expected:
            differences.append({'path': path, 'expected': expected, 'actual': result})
            continue
        if result['schema'] != schema(expected.get('schema')):
            differences.append({'path': path + '/schema', 'expected': expected.get('schema'), 'actual': result['schema']})
        if not numeric_checks.equal(value(expected['value']), result['value'], numeric_checks.options(case)):
            differences.append({'path': path + '/value', 'expected': value(expected['value']), 'actual': result['value']})
    return ('passed', []) if not differences else ('failed', differences)
