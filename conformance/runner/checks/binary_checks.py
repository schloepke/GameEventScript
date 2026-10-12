# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Fixture I/O and independent binary assertions; no GES binary decoder."""
import base64
import hashlib
from pathlib import Path
import re

from core.corpus import ID, InvalidDocument, closed
from checks.compiler_checks import exact, natural
from checks import runtime_checks

FIXTURES = Path(__file__).resolve().parents[2] / 'fixtures'
MAX_RESOURCE_BYTES = 64 * 1024 * 1024
SUCCESS = {'outcome', 'rewriteByteExact', 'rewriteSha256', 'moduleName',
           'requiredRegisterCount', 'requiredCallStackDepth', 'opaqueSectionCount'}
ERROR = {'outcome', 'errorCode', 'byteOffset', 'sectionType', 'entryIndex'}
# Stable format error names, in numeric order (specs/BinaryFormat.md).
ERRORS = '''InvalidMagic UnsupportedFormatVersion InvalidHeaderFlags InvalidHeaderSize
FileSizeMismatch FileTooLarge TooManySections TruncatedSectionHeader TruncatedSectionPayload
SectionTooLarge InvalidSectionType InvalidSectionFlags InvalidSectionReserved UnsupportedSectionVersion
UnsupportedCompression MissingRequiredSection DuplicateSection UnknownRequiredSection InvalidPayloadLength
InvalidUtf8 TooManyEntries InvalidStringIndex InvalidListIndex InvalidBindingKind DuplicateBindingId
InvalidEntryAddress InvalidOpcode InvalidOperand InvalidJumpAddress InvalidCallAddress CyclicCallGraph
InvalidResourceMetadata InvalidDebugSymbol InvalidSourceMap InvalidSourceArchive SourceMetadataMismatch
ReaderLimitExceeded InvalidProgram'''.split()


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def digest(value):
    if not isinstance(value, str) or not re.fullmatch('[0-9A-F]{64}', value):
        raise InvalidDocument('Expected canonical SHA-256')


def bytes_from(value):
    if not isinstance(value, str) or len(value) > (MAX_RESOURCE_BYTES + 2) // 3 * 4:
        raise InvalidDocument('Invalid binary payload')
    try:
        result = base64.b64decode(value, validate=True)
    except ValueError as error:
        raise InvalidDocument('Invalid Base64 payload') from error
    if len(result) > MAX_RESOURCE_BYTES or base64.b64encode(result).decode('ascii') != value:
        raise InvalidDocument('Invalid or oversized binary payload')
    return result


def fixture_bytes(fixture, root):
    relative = fixture['relativePath']
    if (not isinstance(relative, str) or '\\' in relative or '\0' in relative or ':' in relative
            or any(part in ('', '.', '..') for part in relative.split('/'))):
        raise InvalidDocument('Invalid relative fixture path')
    root = Path(root).resolve()
    path = (root / relative).resolve()
    if not path.is_relative_to(root):
        raise InvalidDocument('Fixture path escapes resource root')
    with path.open('rb') as stream:
        data = stream.read(MAX_RESOURCE_BYTES + 1)
    if len(data) > MAX_RESOURCE_BYTES:
        raise InvalidDocument('Fixture exceeds resource byte limit')
    if sha(data) != fixture['sha256']:
        raise InvalidDocument('Fixture SHA-256 mismatch: ' + relative)
    return data


def prepare(case, root=FIXTURES):
    closed(case.metadata, {'gesBlock', 'id', 'kind', 'level', 'categories', 'tags', 'requires',
                           'sources', 'compile', 'binaryFixture', 'runtimeLimits'}, 'binary case metadata')
    fixture = case.metadata.get('binaryFixture')
    closed(fixture, {'id', 'resourceId', 'relativePath', 'sha256', 'compilerId', 'compilerVersion',
                     'programVersion', 'compareCompiledRuntime', 'derivation'}, 'binary fixture')
    for key in ('id', 'resourceId', 'compilerId'):
        if not isinstance(fixture.get(key), str) or not ID.fullmatch(fixture[key]):
            raise InvalidDocument('Invalid fixture identifier')
    if not isinstance(fixture.get('compilerVersion'), str) or not fixture['compilerVersion']:
        raise InvalidDocument('Missing fixture compiler version')
    if 'derivation' in fixture and not isinstance(fixture['derivation'], str):
        raise InvalidDocument('Invalid fixture derivation')
    natural(fixture.get('programVersion'))
    digest(fixture.get('sha256'))
    compare_runtime = fixture.get('compareCompiledRuntime', False)
    if type(compare_runtime) is not bool or (compare_runtime and not case.sources):
        raise InvalidDocument('Invalid compiled runtime comparison')
    closed(case.expected, {'gesBlock', 'binary', 'steps'}, 'binary expectation')
    expected = case.expected.get('binary')
    closed(expected, SUCCESS | ERROR, 'binary constraints')
    outcome = expected.get('outcome')
    if case.steps and outcome != 'valid':
        raise InvalidDocument('Only valid binaries can execute Steps')
    runtime = runtime_checks.prepare(case)
    if not case.steps and (case.expected.get('steps') or case.metadata.get('runtimeLimits')):
        raise InvalidDocument('Runtime configuration requires Steps')
    if outcome == 'valid':
        closed(expected, SUCCESS, 'valid binary constraints')
        for key, value in expected.items():
            if key == 'rewriteByteExact':
                if type(value) is not bool:
                    raise InvalidDocument('Expected Boolean byte equality')
            elif key == 'rewriteSha256':
                digest(value)
            elif key == 'moduleName':
                if not isinstance(value, str):
                    raise InvalidDocument('Expected module name')
            elif key != 'outcome':
                natural(value)
    elif outcome in ('readError', 'validationError'):
        closed(expected, ERROR, 'invalid binary constraints')
        code = expected.get('errorCode')
        if code not in ERRORS or (ERRORS.index(code) < 21) != (outcome == 'readError'):
            raise InvalidDocument('Invalid binary error code/outcome')
        for key in ('byteOffset', 'sectionType', 'entryIndex'):
            if key in expected:
                natural(expected[key])
        if case.assembler is not None:
            raise InvalidDocument('Invalid binary cannot have a dump expectation')
    else:
        raise InvalidDocument('Invalid binary outcome')
    data = fixture_bytes(fixture, root)
    return {'data': base64.b64encode(data).decode('ascii'), 'includeDump': case.assembler is not None,
            'compareCompiledRuntime': compare_runtime, 'programVersion': fixture['programVersion'],
            'runtime': runtime if case.steps else None}


def compare(case, observed, request):
    closed(observed, {'status', 'detail', 'binary', 'dump', 'runtime'}, 'binary observation')
    if observed.get('status') == 'error':
        if set(observed) - {'status', 'detail'} or not isinstance(observed.get('detail', ''), str):
            raise InvalidDocument('Invalid execution error')
        return 'error', []
    if observed.get('status') != 'ok':
        raise InvalidDocument('Invalid binary observation status')
    actual = observed.get('binary')
    closed(actual, ERROR | {'moduleName', 'requiredRegisterCount', 'requiredCallStackDepth',
                           'opaqueSectionCount', 'programVersion', 'buildMetadata', 'rewritten',
                           'runtimeSections', 'compiledRuntimeSections'}, 'binary result')
    differences = []

    def check(path, expected, value):
        if not exact(expected, value):
            differences.append({'path': path, 'expected': expected, 'actual': value})

    outcome = actual.get('outcome')
    expected = case.expected['binary']
    check('/binary/outcome', expected['outcome'], outcome)
    if outcome in ('readError', 'validationError'):
        if set(actual) != ERROR or 'dump' in observed or 'runtime' in observed:
            raise InvalidDocument('Incomplete or conflicting binary error')
        code = actual['errorCode']
        if code not in ERRORS or (ERRORS.index(code) < 21) != (outcome == 'readError'):
            raise InvalidDocument('Invalid binary error observation')
        for key in ('byteOffset', 'sectionType', 'entryIndex'):
            if actual[key] is not None:
                natural(actual[key])
        for key, value in expected.items():
            if key in ERROR and key != 'outcome':
                check('/binary/' + key, value, actual[key])
    elif outcome == 'valid':
        required = {'outcome', 'moduleName', 'requiredRegisterCount', 'requiredCallStackDepth',
                    'opaqueSectionCount', 'programVersion', 'buildMetadata', 'rewritten',
                    'runtimeSections', 'compiledRuntimeSections'}
        if set(actual) != required or not isinstance(actual['moduleName'], str):
            raise InvalidDocument('Incomplete valid binary observation')
        for key in ('requiredRegisterCount', 'requiredCallStackDepth', 'opaqueSectionCount', 'programVersion'):
            natural(actual[key])
        fixture = case.metadata['binaryFixture']
        check('/binaryFixture/programVersion', fixture['programVersion'], actual['programVersion'])
        if actual['buildMetadata'] is not None:
            closed(actual['buildMetadata'], {'compilerId', 'compilerVersion'}, 'build metadata')
            for key in ('compilerId', 'compilerVersion'):
                if not isinstance(actual['buildMetadata'].get(key), str):
                    raise InvalidDocument('Invalid build metadata')
                check('/binaryFixture/' + key, fixture[key], actual['buildMetadata'][key])
        rewritten = bytes_from(actual['rewritten'])
        values = {**actual, 'rewriteByteExact': bytes_from(request['data']) == rewritten, 'rewriteSha256': sha(rewritten)}
        for key, value in expected.items():
            if key in SUCCESS and key != 'outcome':
                check('/binary/' + key, value, values[key])
        if request['compareCompiledRuntime']:
            left, right = bytes_from(actual['runtimeSections']), bytes_from(actual['compiledRuntimeSections'])
            if left != right:
                differences.append({'path': '/binary/compiledRuntimeSections', 'expected': sha(left), 'actual': sha(right)})
        elif actual['runtimeSections'] is not None or actual['compiledRuntimeSections'] is not None:
            raise InvalidDocument('Unexpected compiled runtime observation')
        if case.steps:
            differences.extend(runtime_checks.compare(case, observed.get('runtime')))
        elif 'runtime' in observed:
            raise InvalidDocument('Unexpected runtime observations')
        if request['includeDump']:
            if not isinstance(observed.get('dump'), str):
                raise InvalidDocument('Missing binary dump')
        elif 'dump' in observed:
            raise InvalidDocument('Unexpected binary dump')
    else:
        raise InvalidDocument('Invalid binary outcome observation')
    return ('failed' if differences else 'passed'), differences
