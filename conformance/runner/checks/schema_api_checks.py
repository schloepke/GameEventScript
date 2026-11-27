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
CONTENT_HASH = re.compile(r'[0-9a-f]{64}')
SCHEMA_CODE = re.compile(r'schema\.[a-zA-Z]+')
WIRE_CODE = re.compile(r'wire\.[a-zA-Z]+')
DOCUMENT_CODE = re.compile(r'(document|schema)\.[a-zA-Z]+')
READ_DOCUMENT_CODE = re.compile(r'(document|wire)\.[a-zA-Z]+')
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
    closed(raw, {'sources', 'root', 'parameters', 'bind', 'encode', 'encodeExternal', 'baselineExternal', 'externalFields', 'failingField', 'baselineFailingField', 'failingCode', 'encodeBuffer', 'baseline', 'decode', 'decodeLimit', 'key', 'complete', 'dump', 'dumpBaseline', 'deltaEncode', 'deltaDecode', 'describe', 'document', 'readDocument', 'network', 'handle', 'contentHash', 'catalog'}, 'schema API input')
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
    if ('encode' in raw or 'decode' in raw or 'key' in raw or 'complete' in raw or 'dump' in raw or 'deltaEncode' in raw or 'deltaDecode' in raw) and 'root' not in raw:
        raise InvalidDocument('Encoding requires a root')
    external_inputs(raw)
    if 'encodeBuffer' in raw and 'baseline' in raw:
        raise InvalidDocument('A buffer size belongs to a full encoding, because the Swift delta API takes none')
    if 'baseline' in raw and 'encode' not in raw and 'decode' not in raw:
        raise InvalidDocument('A baseline requires encoding or decoding')
    if 'dumpBaseline' in raw and 'dump' not in raw:
        raise InvalidDocument('A dump baseline requires a dump')
    if 'contentHash' in raw and 'handle' not in raw:
        raise InvalidDocument('A content hash requires a handle type')
    request = {
        'sources': sources,
        'root': text(raw['root'], 'root') if 'root' in raw else None,
        'parameters': [{'name': k, 'literal': parameters[k]} if isinstance(parameters[k], str) else {'name': k, 'value': input_value(parameters[k])} for k in sorted(parameters)],
        'bind': raw.get('bind', 'document' not in raw and 'readDocument' not in raw and 'root' in raw),
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
        'complete': input_value(raw['complete']) if 'complete' in raw else None,
        'dump': hexadecimal(raw['dump']) if 'dump' in raw else None,
        'dumpBaseline': hexadecimal(raw['dumpBaseline']) if 'dumpBaseline' in raw else None,
        'deltaEncode': delta_input(raw['deltaEncode'], 'record') if 'deltaEncode' in raw else None,
        'deltaDecode': delta_input(raw['deltaDecode'], 'delta') if 'deltaDecode' in raw else None,
        'describe': raw.get('describe', False),
        'document': text(raw['document'], 'document') if 'document' in raw else None,
        'readDocument': read_document(raw['readDocument']) if 'readDocument' in raw else None,
        'network': text(raw['network'], 'network') if 'network' in raw else None,
        'handle': text(raw['handle'], 'handle') if 'handle' in raw else None,
        'contentHash': input_value(raw['contentHash']) if 'contentHash' in raw else None,
        'catalog': catalog_input(raw['catalog']) if 'catalog' in raw else None,
    }
    closed(case.expected, {'gesBlock', 'schema'}, 'schema expectation')
    expected = case.expected.get('schema')
    closed(expected, {'error', 'canonical', 'hash', 'bindingHash', 'maxByteCount', 'encoded', 'decoded', 'deltaEncoded', 'deltaDecoded', 'key', 'completed', 'dump', 'description', 'document', 'readDocument', 'network', 'handle', 'contentHash', 'catalog'}, 'schema result')
    if not expected:
        raise InvalidDocument('Schema expectation requires a result')
    if 'error' in expected:
        if set(expected) != {'error'}:
            raise InvalidDocument('A schema error stands alone')
        positioned(expected['error'], SCHEMA_CODE)
    if 'canonical' in expected:
        text(expected['canonical'], 'canonical')
    if 'network' in expected:
        if request['network'] is None:
            raise InvalidDocument('A network expectation requires a network')
        closed(expected['network'], {'canonical', 'hash'}, 'network result')
        if set(expected['network']) != {'canonical', 'hash'}:
            raise InvalidDocument('A network result has a canonical form and a hash')
        text(expected['network']['canonical'], 'network canonical')
        if not isinstance(expected['network']['hash'], str) or not HASH.fullmatch(expected['network']['hash']):
            raise InvalidDocument('Invalid hash')
    if 'handle' in expected:
        if request['handle'] is None:
            raise InvalidDocument('A handle expectation requires a handle type')
        if not isinstance(expected['handle'], dict) or set(expected['handle']) != {'canonical', 'hash'}:
            raise InvalidDocument('A handle result has a canonical form and a hash')
        text(expected['handle']['canonical'], 'handle canonical')
        if not isinstance(expected['handle']['hash'], str) or not HASH.fullmatch(expected['handle']['hash']):
            raise InvalidDocument('Invalid hash')
    if 'contentHash' in expected:
        if request['contentHash'] is None:
            raise InvalidDocument('A content hash expectation requires a state')
        wanted = expected['contentHash']
        if isinstance(wanted, dict):
            closed(wanted, {'error'}, 'content hash result')
            wire_error(wanted['error'])
        elif not isinstance(wanted, str) or not CONTENT_HASH.fullmatch(wanted):
            raise InvalidDocument('A content hash is 64 lowercase hexadecimal digits or an error')
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
    if 'completed' in expected:
        if request['complete'] is None:
            raise InvalidDocument('A completion expectation requires a message to complete')
        closed(expected['completed'], {'value', 'bytes', 'error'}, 'completion result')
        if 'error' in expected['completed']:
            if len(expected['completed']) != 1:
                raise InvalidDocument('A completion error stands alone')
            wire_error(expected['completed']['error'])
        elif set(expected['completed']) != {'value', 'bytes'}:
            raise InvalidDocument('A completion has the completed message and its strict encoding')
        else:
            value(expected['completed']['value'])
            hexadecimal(expected['completed']['bytes'])
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
    if 'readDocument' in expected:
        if request['readDocument'] is None:
            raise InvalidDocument('A read document expectation requires a document to read')
        wanted = expected['readDocument']
        if 'error' in wanted:
            closed(wanted, {'error'}, 'read document result')
            closed(wanted['error'], {'code', 'line', 'column', 'path'}, 'read document error')
            positioned({key: v for key, v in wanted['error'].items() if key != 'path'}, READ_DOCUMENT_CODE)
            if not isinstance(wanted['error'].get('path', ''), str):
                raise InvalidDocument('A read document error path is text')
        else:
            closed(wanted, {'schema', 'value'}, 'read document result')
            text(wanted.get('schema'), 'read document schema')
            value(wanted['value'])
    if 'catalog' in expected:
        if request['catalog'] is None:
            raise InvalidDocument('A catalog expectation requires a catalog')
        catalog_result(expected['catalog'])
        if set(expected['catalog'].get('indexes', {})) != set(request['catalog']['index']) and 'entries' in expected['catalog']:
            raise InvalidDocument('Catalog indexes expect exactly the requested projections')
    return request


CATALOG_CODE = re.compile(r'(catalog|document|schema|wire)\.[a-zA-Z]+')
CATALOG_NAME = re.compile(r'[A-Z][A-Za-z0-9]*(?:_[A-Z][A-Za-z0-9]*)*')


def catalog_root(raw, label):
    closed(raw, {'name', 'path'}, label)
    return {'name': text(raw.get('name'), label + ' name'), 'path': text(raw.get('path'), label + ' path')}


def revision(raw, label):
    if isinstance(raw, bool) or not isinstance(raw, int) or raw < 0:
        raise InvalidDocument(label + ' is a revision, an integer from 0')
    return raw


# Catalogs read in order from one file table; each depends on every catalog before it. A catalog reads named roots or root
# handles; for handles, the table's root is the root handle's Text id, `refs` turns refs into revisions, a file answers with
# its own `version` or else the root's, and `latest` is the revision of an answer for a root that carries none.
def catalog_input(raw):
    closed(raw, {'files', 'catalogs', 'refs', 'latest', 'index'}, 'catalog input')
    index = raw.get('index', [])
    if not isinstance(index, list) or any(not isinstance(name, str) or not name for name in index) or len(set(index)) != len(index):
        raise InvalidDocument('A catalog index names distinct projections')
    refs = raw.get('refs', {})
    if not isinstance(refs, dict) or any(not isinstance(name, str) for name in refs):
        raise InvalidDocument('Catalog refs map names to revisions')
    refs = {name: revision(number, 'A catalog ref') for name, number in refs.items()}
    latest = revision(raw['latest'], 'The latest catalog revision') if 'latest' in raw else None
    files = raw.get('files')
    if not isinstance(files, list):
        raise InvalidDocument('A catalog input has a file table')
    table = []
    for entry in files:
        closed(entry, {'root', 'path', 'text', 'version'}, 'catalog file')
        if not all(isinstance(entry.get(key), str) for key in ('root', 'path', 'text')):
            raise InvalidDocument('A catalog file has a root, a path and a text')
        if any(seen['root'] == entry['root'] and seen['path'] == entry['path'] for seen in table):
            raise InvalidDocument('A catalog file appears once')
        version = revision(entry['version'], 'A catalog file version') if 'version' in entry else None
        table.append({'root': entry['root'], 'path': entry['path'], 'text': entry['text'], 'version': version})
    catalogs = raw.get('catalogs')
    if not isinstance(catalogs, list) or not catalogs:
        raise InvalidDocument('A catalog input reads at least one catalog')
    read = []
    for catalog in catalogs:
        closed(catalog, {'type', 'roots', 'handles'}, 'catalog')
        name = catalog.get('type')
        if not isinstance(name, str) or not CATALOG_NAME.fullmatch(name) or any(seen['type'] == name for seen in read):
            raise InvalidDocument('Each catalog reads a distinct handle type')
        if ('roots' in catalog) == ('handles' in catalog):
            raise InvalidDocument('A catalog reads either named roots or root handles')
        if 'handles' in catalog:
            handles = catalog['handles']
            if not isinstance(handles, list) or any(not isinstance(handle, str) or not handle.startswith('@') for handle in handles):
                raise InvalidDocument('A catalog has a list of root handles')
            read.append({'type': name, 'roots': None, 'handles': list(handles)})
            continue
        roots = catalog.get('roots')
        if not isinstance(roots, list):
            raise InvalidDocument('A catalog has a list of roots')
        read.append({'type': name, 'roots': [catalog_root(root, 'catalog root') for root in roots], 'handles': None})
    return {'files': table, 'catalogs': read, 'refs': refs, 'latest': latest, 'index': index}


def catalog_location(raw, label, positioned_required):
    closed(raw, {'root', 'path', 'line', 'column'}, label)
    text(raw.get('root'), label + ' root')
    if not isinstance(raw.get('path'), str):
        raise InvalidDocument('A catalog location has a path')
    for key in ('line', 'column'):
        if key in raw:
            positive(raw[key])
        elif positioned_required:
            raise InvalidDocument('A catalog location has a line and a column')


# Entries of the last catalog, sorted by id, with the sources it was read from, or the errors of the first catalog that could
# not be read, in report order, or the code of a resolver failure.
def catalog_result(raw):
    closed(raw, {'entries', 'sources', 'indexes', 'errors', 'failure'}, 'catalog result')
    if len(set(raw) - {'sources', 'indexes'}) != 1 or ('sources' in raw or 'indexes' in raw) and 'entries' not in raw:
        raise InvalidDocument('A catalog result has entries, errors or a failure')
    if 'indexes' in raw and (not isinstance(raw['indexes'], dict) or any(
            not isinstance(handles, list) or any(not isinstance(handle, str) for handle in handles) for handles in raw['indexes'].values())):
        raise InvalidDocument('Catalog indexes map projections to lists of handles')
    if 'failure' in raw:
        if raw['failure'] not in ('runtime.handleResolveFailed', 'runtime.handleVersionMismatch'):
            raise InvalidDocument('A catalog failure is a handle resolution code')
        return
    if 'sources' in raw and (not isinstance(raw['sources'], list) or any(not isinstance(source, str) for source in raw['sources'])):
        raise InvalidDocument('Catalog sources are a list of handles')
    if 'entries' in raw:
        if not isinstance(raw['entries'], list):
            raise InvalidDocument('Catalog entries are a list')
        for entry in raw['entries']:
            closed(entry, {'id', 'handle', 'origin', 'value'}, 'catalog entry')
            text(entry.get('id'), 'catalog id')
            text(entry.get('handle'), 'catalog handle')
            catalog_location(entry.get('origin'), 'catalog origin', True)
            value(entry['value'])
        ids = [entry['id'] for entry in raw['entries']]
        if ids != sorted(ids):
            raise InvalidDocument('Catalog entries are sorted by id')
        return
    errors = raw['errors']
    if not isinstance(errors, list) or not errors:
        raise InvalidDocument('Catalog errors are a nonempty list')
    for error in errors:
        closed(error, {'code', 'location', 'fieldPath', 'other', 'suggestion'}, 'catalog error')
        if not isinstance(error.get('code'), str) or not CATALOG_CODE.fullmatch(error['code']):
            raise InvalidDocument('Invalid catalog error code')
        catalog_location(error.get('location'), 'catalog error location', False)
        if 'other' in error:
            catalog_location(error['other'], 'catalog error other', False)
        for key in ('fieldPath', 'suggestion'):
            if key in error and not isinstance(error[key], str):
                raise InvalidDocument('A catalog error ' + key + ' is text')


def compare_catalog(wanted, seen, options):
    if not isinstance(seen, dict):
        return False
    if 'failure' in wanted:
        return seen.get('failure') == wanted['failure']
    if 'sources' in wanted and seen.get('sources') != wanted['sources']:
        return False
    if 'indexes' in wanted and seen.get('indexes') != wanted['indexes']:
        return False
    if 'errors' in wanted:
        errors = seen.get('errors')
        return isinstance(errors, list) and len(errors) == len(wanted['errors']) \
            and all(contains(expected, actual) for expected, actual in zip(wanted['errors'], errors))
    entries = seen.get('entries')
    if not isinstance(entries, list) or len(entries) != len(wanted['entries']):
        return False
    for expected, actual in zip(wanted['entries'], entries):
        if not isinstance(actual, dict) or any(actual.get(key) != expected[key] for key in ('id', 'handle', 'origin')) or 'value' not in actual \
                or not numeric_checks.equal(value(expected['value']), input_value(actual['value']), options):
            return False
    return True


def read_document(raw):
    closed(raw, {'text', 'expected'}, 'read document input')
    names = raw.get('expected')
    if not isinstance(names, list) or not names or any(not isinstance(name, str) for name in names):
        raise InvalidDocument('A read document names the expected schemas')
    return {'text': text(raw.get('text'), 'read document text'), 'expected': names}


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
    closed(actual, {'error', 'canonical', 'hash', 'bindingHash', 'maxByteCount', 'encoded', 'decoded', 'deltaEncoded', 'deltaDecoded', 'key', 'completed', 'dump', 'description', 'document', 'readDocument', 'network', 'handle', 'contentHash', 'catalog'}, 'schema observation')
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
    for key in ('canonical', 'hash', 'bindingHash', 'maxByteCount', 'key', 'dump', 'description', 'network', 'handle'):
        if key in expected and actual.get(key) != expected[key]:
            differ(key, expected[key], actual.get(key))
    if 'contentHash' in expected:
        seen = actual.get('contentHash')
        wanted = expected['contentHash']
        if isinstance(wanted, dict):
            if not isinstance(seen, dict) or not contains(wanted['error'], seen.get('error')):
                differ('contentHash', wanted, seen)
        elif seen != wanted:
            differ('contentHash', wanted, seen)
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
    if 'completed' in expected:
        seen = actual.get('completed')
        wanted = expected['completed']
        if 'error' in wanted:
            if not isinstance(seen, dict) or not contains(wanted['error'], seen.get('error')):
                differ('completed', wanted, seen)
        elif not isinstance(seen, dict) or 'value' not in seen or seen.get('bytes') != wanted['bytes'] \
                or not numeric_checks.equal(value(wanted['value']), input_value(seen['value']), numeric_checks.options(case)):
            differ('completed', wanted, seen)
    if 'document' in expected:
        seen = actual.get('document')
        wanted = expected['document']
        if 'error' in wanted:
            if not isinstance(seen, dict) or not contains(wanted['error'], seen.get('error')):
                differ('document', wanted, seen)
        elif seen != {'ok': True}:
            differ('document', wanted, seen)
    if 'readDocument' in expected:
        seen = actual.get('readDocument')
        wanted = expected['readDocument']
        if 'error' in wanted:
            if not isinstance(seen, dict) or not contains({'path': '', **wanted['error']}, seen.get('error')):
                differ('readDocument', wanted, seen)
        elif not isinstance(seen, dict) or seen.get('schema') != wanted['schema'] or 'value' not in seen \
                or not numeric_checks.equal(value(wanted['value']), input_value(seen['value']), numeric_checks.options(case)):
            differ('readDocument', wanted, seen)
    if 'catalog' in expected and not compare_catalog(expected['catalog'], actual.get('catalog'), numeric_checks.options(case)):
        differ('catalog', expected['catalog'], actual.get('catalog'))
    return ('passed', []) if not differences else ('failed', differences)
