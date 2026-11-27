# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""The per-case handle catalog and scripted resolver answers (specs/Conformance/MarkdownFormat.md#handle-catalogs)."""
import re

from core.corpus import InvalidDocument, closed
from checks import runtime_checks

KINDS = {'scriptApi', 'loadError', 'compileError', 'compileMetadata', 'bytecode', 'bytecodeSnapshot'}
ANSWERS = ('document', 'record', 'missing', 'notVisible', 'fail')
REF_ANSWERS = ('version', 'missing', 'fail')
TYPE_NAME = re.compile(r'[A-Z][A-Za-z0-9]*(?:_[A-Z][A-Za-z0-9]*)*\Z')
HANDLE = re.compile(r'@[A-Z][A-Za-z0-9]*(?:_[A-Z][A-Za-z0-9]*)*:.+', re.S)
# A ref follows `~` as a tag or a quoted Text (specs/Documents.md#handles).
REF = re.compile(r"[^~]*~(?:#|')", re.S)


def texts(raw, label):
    if not isinstance(raw, list) or not raw or any(not isinstance(text, str) for text in raw):
        raise InvalidDocument('Expected a non-empty list of schema texts: ' + label)
    return list(raw)


def answer(raw):
    closed(raw, {'handle', 'version', *ANSWERS}, 'handle answer')
    if not isinstance(raw.get('handle'), str) or not HANDLE.fullmatch(raw['handle']):
        raise InvalidDocument('A handle answer names a handle in its text form')
    kinds = [key for key in ANSWERS if key in raw]
    if len(kinds) != 1:
        raise InvalidDocument('A handle answer has exactly one of document, record, missing, notVisible and fail')
    kind = kinds[0]
    if kind in ('missing', 'notVisible', 'fail'):
        if raw[kind] is not True:
            raise InvalidDocument('missing, notVisible and fail are written as true')
        if kind != 'missing' and 'version' in raw:
            raise InvalidDocument('A failing or not visible answer has no version')
        text = None
    elif not isinstance(raw[kind], str):
        raise InvalidDocument('A document or record answer is text')
    else:
        text = raw[kind]
    return {'handle': raw['handle'], 'answer': kind, 'text': text, 'version': version(raw['version']) if 'version' in raw else None}


def version(raw):
    result = runtime_checks.value(raw)
    if result['type'] not in (':Number.int64', ':Bytes'):
        raise InvalidDocument('A passed version is an integer Number or Bytes')
    return result


def ref_answer(raw):
    closed(raw, {'ref', *REF_ANSWERS}, 'ref answer')
    if not isinstance(raw.get('ref'), str) or not HANDLE.fullmatch(raw['ref']) or not REF.match(raw['ref']):
        raise InvalidDocument('A ref answer names a handle with a ref in its text form')
    kinds = [key for key in REF_ANSWERS if key in raw]
    if len(kinds) != 1:
        raise InvalidDocument('A ref answer has exactly one of version, missing and fail')
    if kinds[0] != 'version' and raw[kinds[0]] is not True:
        raise InvalidDocument('missing and fail are written as true')
    return {'ref': raw['ref'], 'answer': kinds[0], 'version': version(raw['version']) if kinds[0] == 'version' else None}


def prepare(case):
    """The normalized catalog of a case, or None. Every field is present in the request."""
    raw = case.metadata.get('handles')
    if raw is None:
        return None
    if case.kind not in KINDS:
        raise InvalidDocument('A handle catalog belongs to compiler, loading and script cases')
    closed(raw, {'schemas', 'hostSchemas', 'resolvers', 'answers', 'resolveRefs'}, 'handle catalog')
    schemas = texts(raw.get('schemas'), 'schemas')
    host = texts(raw['hostSchemas'], 'hostSchemas') if 'hostSchemas' in raw else schemas
    resolvers = raw.get('resolvers')
    if resolvers is not None and (not isinstance(resolvers, list) or any(not isinstance(name, str) or not TYPE_NAME.fullmatch(name) for name in resolvers)
                                  or len(set(resolvers)) != len(resolvers)):
        raise InvalidDocument('Resolvers name distinct handle types without @')
    answers = raw.get('answers', [])
    if not isinstance(answers, list):
        raise InvalidDocument('Handle answers form a list')
    refs = raw.get('resolveRefs', [])
    if not isinstance(refs, list) or any(not isinstance(text, str) or not HANDLE.fullmatch(text) for text in refs):
        raise InvalidDocument('resolveRefs lists handles in their text form')
    if (case.kind != 'scriptApi') and (answers or refs):
        raise InvalidDocument('Only script cases answer state reads and resolve refs')
    expected = (case.expected or {}).get('resolvedRefs')
    if refs:
        resolved_refs(expected, refs)
    elif expected is not None:
        raise InvalidDocument('resolvedRefs needs handles.resolveRefs')
    return {'schemas': schemas, 'hostSchemas': host, 'resolvers': resolvers,
            'answers': [answer(item) for item in answers if 'ref' not in item],
            'refAnswers': [ref_answer(item) for item in answers if 'ref' in item], 'resolveRefs': list(refs)}


def outcome(raw, refs, index, expectation):
    """One resolved ref: the returned handle or Nothing, or the failure."""
    closed(raw, {'ref', 'value', 'error'}, 'resolved ref')
    if raw.get('ref') != refs[index]:
        raise InvalidDocument('resolvedRefs follow handles.resolveRefs in order')
    if ('value' in raw) == ('error' in raw):
        raise InvalidDocument('A resolved ref has exactly one of value and error')
    if 'value' in raw:
        result = runtime_checks.value(raw['value'])
        if result['type'] not in (':Handle', ':Nothing'):
            raise InvalidDocument('A resolved ref is a handle or Nothing')
        return {'ref': raw['ref'], 'value': result}
    error = raw['error']
    closed(error, {'phase', 'code', 'symbol'}, 'ref error')
    if error.get('phase') != 'runtime' or not isinstance(error.get('code'), str) or not error['code'].startswith('runtime.'):
        raise InvalidDocument('A ref error has the runtime phase and a runtime code')
    if ('symbol' in error or not expectation) and not isinstance(error.get('symbol'), str):
        raise InvalidDocument('A ref error symbol is text')
    return {'ref': raw['ref'], 'error': dict(error)}


def resolved_refs(raw, refs, expectation=True):
    if not isinstance(raw, list) or len(raw) != len(refs):
        raise InvalidDocument('resolvedRefs has one entry per handles.resolveRefs entry')
    return [outcome(item, refs, index, expectation) for index, item in enumerate(raw)]


def compare(case, hosts, check):
    """Compares each host's resolved refs with the expectation; a script case without refs observes none."""
    refs = case.metadata.get('handles', {}).get('resolveRefs', [])
    expected = resolved_refs(case.expected['resolvedRefs'], refs) if refs else []
    for host_index, host in enumerate(hosts):
        observed = resolved_refs(host.get('resolvedRefs'), refs, expectation=False)
        for index, (exp, obs) in enumerate(zip(expected, observed)):
            path = '/hosts/' + str(host_index) + '/resolvedRefs/' + str(index)
            if 'value' in exp:
                check(path + '/value', exp['value'], obs.get('value'))
            else:
                for key, item in exp['error'].items():
                    check(path + '/error/' + key, item, obs.get('error', {}).get(key))
