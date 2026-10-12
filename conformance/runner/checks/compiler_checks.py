# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Independent compiler observation validation and assertions (no GES types)."""
from core.corpus import InvalidDocument, closed
from checks.opcode_names import OPCODES

SUPPORTED = {'bytecodeSnapshot', 'compileError', 'bytecode', 'compileMetadata'}
DIAGNOSTIC = {'phase', 'code', 'symbol', 'symbolKind', 'sourceName', 'line', 'column',
              'endLine', 'endColumn', 'programName', 'handlerName'}
POSITIONS = {'line', 'column', 'endLine', 'endColumn'}
RESOURCES = {'requiredRegisterCount', 'requiredCallStackDepth'}


def exact(left, right):
    if isinstance(left, list):
        return isinstance(right, list) and len(left) == len(right) and all(exact(a, b) for a, b in zip(left, right))
    if isinstance(left, dict):
        return isinstance(right, dict) and left.keys() == right.keys() and all(exact(left[k], right[k]) for k in left)
    return type(left) is type(right) and left == right


def natural(value):
    if type(value) is not int or not 0 <= value <= 18446744073709551615:
        raise InvalidDocument('Expected nonnegative bounded integer')


def diagnostic(value, expectation=False, phases=('parse', 'validate', 'compile')):
    closed(value, DIAGNOSTIC, 'diagnostic')
    for key in ('phase', 'code'):
        if not isinstance(value.get(key), str) or not value[key]:
            raise InvalidDocument('Required diagnostic phase/code')
    if value['phase'] not in phases:
        raise InvalidDocument('Invalid compiler diagnostic phase')
    for key, item in value.items():
        if item is None:
            if expectation and key not in ('programName', 'handlerName'):
                raise InvalidDocument('Unexpected null diagnostic constraint')
        elif key in POSITIONS:
            natural(item)
            if item == 0 or item > 4294967295:
                raise InvalidDocument('Invalid diagnostic position')
        elif not isinstance(item, str):
            raise InvalidDocument('Expected diagnostic text')


def metadata(value, observation=False):
    closed(value, {'messageDefinitions', 'programResources', 'handlerResources'}, 'metadata')
    if observation and set(value) != {'messageDefinitions', 'programResources', 'handlerResources'}:
        raise InvalidDocument('Missing metadata observation')
    if not value:
        raise InvalidDocument('Empty metadata expectation')
    if 'programResources' in value:
        closed(value['programResources'], RESOURCES, 'program resources')
        for item in value['programResources'].values():
            natural(item)
        if observation and set(value['programResources']) != RESOURCES:
            raise InvalidDocument('Missing resource observations')
    for group in ('messageDefinitions', 'handlerResources'):
        if group not in value:
            continue
        if not isinstance(value[group], list):
            raise InvalidDocument('Expected metadata sequence')
        seen = set()
        for entry in value[group]:
            allowed = {'name', 'count', 'signatureIds'} if group == 'messageDefinitions' else {'name', 'signatureId'} | RESOURCES
            closed(entry, allowed, group)
            if not isinstance(entry.get('name'), str) or not entry['name']:
                raise InvalidDocument('Missing metadata name')
            if 'signatureId' in entry and not isinstance(entry['signatureId'], str):
                raise InvalidDocument('Invalid signature ID')
            identity = (entry['name'], entry.get('signatureId'))
            if identity in seen:
                raise InvalidDocument('Duplicate metadata entry')
            seen.add(identity)
            if group == 'messageDefinitions':
                if observation and set(entry) != allowed:
                    raise InvalidDocument('Incomplete message definition')
                if 'count' in entry:
                    natural(entry['count'])
                if 'signatureIds' in entry and (not isinstance(entry['signatureIds'], list) or any(not isinstance(x, str) for x in entry['signatureIds'])):
                    raise InvalidDocument('Invalid signature sequence')
            else:
                if 'signatureId' in entry and not isinstance(entry['signatureId'], str):
                    raise InvalidDocument('Invalid signature ID')
                for key in RESOURCES & entry.keys():
                    natural(entry[key])
                if observation and set(entry) != allowed:
                    raise InvalidDocument('Incomplete handler resources')


def expectation(case):
    if case.kind == 'bytecodeSnapshot':
        if case.expected is not None or case.assembler is None:
            raise InvalidDocument('Invalid snapshot expectation')
        return
    key = {'compileError': 'error', 'bytecode': 'opcodes', 'compileMetadata': 'metadata'}[case.kind]
    closed(case.expected, {'gesBlock', key}, 'compiler expectation')
    if key not in case.expected or case.assembler is not None:
        raise InvalidDocument('Missing or conflicting compiler expectation')
    value = case.expected[key]
    if key == 'error':
        diagnostic(value, expectation=True)
    elif key == 'metadata':
        metadata(value)
    else:
        closed(value, {'contains', 'excludes', 'counts', 'minimumCounts'}, 'opcode constraints')
        if not value or not any(value.values()):
            raise InvalidDocument('Empty opcode constraints')
        for group, items in value.items():
            if group in ('contains', 'excludes'):
                if not isinstance(items, list) or any(not isinstance(x, str) or x not in OPCODES for x in items):
                    raise InvalidDocument('Invalid opcode sequence')
            else:
                if not isinstance(items, dict) or any(not isinstance(x, str) or x not in OPCODES for x in items):
                    raise InvalidDocument('Invalid opcode counts')
                for count in items.values():
                    natural(count)


def compare(case, observed):
    """Return (status, mismatches); expected compiler failures are observations, not execution errors."""
    closed(observed, {'status', 'detail', 'diagnostics', 'dump', 'opcodes', 'metadata'}, 'compiler observation')
    status = observed.get('status')
    if status not in ('ok', 'error', 'compileError'):
        raise InvalidDocument('Invalid observation status')
    if 'detail' in observed and not isinstance(observed['detail'], str):
        raise InvalidDocument('Invalid observation detail')
    payload = {'bytecodeSnapshot': 'dump', 'bytecode': 'opcodes', 'compileMetadata': 'metadata'}.get(case.kind)
    allowed = {'status', 'detail', 'diagnostics'} | ({payload} if status == 'ok' and payload else set())
    if set(observed) - allowed or (status == 'ok' and observed.get('diagnostics')):
        raise InvalidDocument('Conflicting compiler observations')
    diagnostics = observed.get('diagnostics', [])
    if not isinstance(diagnostics, list):
        raise InvalidDocument('Invalid diagnostic sequence')
    for item in diagnostics:
        diagnostic(item)
    if status == 'compileError' and not diagnostics:
        raise InvalidDocument('Compiler failure without diagnostics')
    if status == 'error':
        return 'error', []
    if case.kind == 'compileError':
        expected = case.expected['error']
        if status == 'compileError' and any(all(exact(v, item.get(k)) if v is None else k in item and exact(v, item[k]) for k, v in expected.items()) for item in diagnostics):
            return 'passed', []
        return 'failed', [{'path': '/error', 'expected': expected, 'actual': diagnostics}]
    if status == 'compileError':
        return 'error', []
    if case.kind == 'bytecodeSnapshot':
        if not isinstance(observed.get('dump'), str):
            raise InvalidDocument('Missing assembler observation')
        return 'passed', []  # Exact UTF-8 comparison remains in runner.py.
    differences = []

    def check(path, expected, actual):
        if not exact(expected, actual):
            differences.append({'path': path, 'expected': expected, 'actual': actual})

    if case.kind == 'bytecode':
        counts = observed.get('opcodes')
        if not isinstance(counts, dict) or any(not isinstance(k, str) or k not in OPCODES for k in counts):
            raise InvalidDocument('Invalid opcode observations')
        for value in counts.values():
            natural(value)
        for group, values in case.expected['opcodes'].items():
            for name in values:
                actual = counts.get(name, 0)
                path = '/opcodes/' + group + '/' + name
                if group == 'contains':
                    check(path, True, actual > 0)
                elif group == 'excludes':
                    check(path, 0, actual)
                elif group == 'counts':
                    check(path, values[name], actual)
                elif actual < values[name]:
                    differences.append({'path': path, 'expectedMinimum': values[name], 'actual': actual})
    else:
        actual = observed.get('metadata')
        metadata(actual, observation=True)
        for group, expected in case.expected['metadata'].items():
            if group == 'programResources':
                for key, value in expected.items():
                    check('/metadata/' + group + '/' + key, value, actual[group].get(key))
            else:
                for entry in expected:
                    matches = [item for item in actual[group] if item['name'] == entry['name'] and
                               ('signatureId' not in entry or item.get('signatureId') == entry['signatureId'])]
                    item = matches[0] if matches else ({'name': entry['name'], 'count': 0, 'signatureIds': []} if group == 'messageDefinitions' else {})
                    for key, value in entry.items():
                        check('/metadata/' + group + '/' + entry['name'] + '/' + key, value, item.get(key))
    return ('failed' if differences else 'passed'), differences
