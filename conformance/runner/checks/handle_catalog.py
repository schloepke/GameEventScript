# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""The per-case handle catalog and scripted resolver answers (specs/Conformance/MarkdownFormat.md#handle-catalogs)."""
import re

from core.corpus import InvalidDocument, closed
from checks import runtime_checks

KINDS = {'scriptApi', 'loadError', 'compileError', 'compileMetadata', 'bytecode', 'bytecodeSnapshot'}
ANSWERS = ('document', 'record', 'missing', 'fail')
TYPE_NAME = re.compile(r'[A-Z][A-Za-z0-9]*(?:_[A-Z][A-Za-z0-9]*)*\Z')
HANDLE = re.compile(r'@[A-Z][A-Za-z0-9]*(?:_[A-Z][A-Za-z0-9]*)*:.+', re.S)


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
        raise InvalidDocument('A handle answer has exactly one of document, record, missing and fail')
    kind = kinds[0]
    if kind in ('missing', 'fail'):
        if raw[kind] is not True:
            raise InvalidDocument('missing and fail are written as true')
        if kind == 'fail' and 'version' in raw:
            raise InvalidDocument('A failing answer has no version')
        text = None
    elif not isinstance(raw[kind], str):
        raise InvalidDocument('A document or record answer is text')
    else:
        text = raw[kind]
    version = None
    if 'version' in raw:
        version = runtime_checks.value(raw['version'])
        if version['type'] not in (':Number.int64', ':Bytes'):
            raise InvalidDocument('A passed version is an integer Number or Bytes')
    return {'handle': raw['handle'], 'answer': kind, 'text': text, 'version': version}


def prepare(case):
    """The normalized catalog of a case, or None. Every field is present in the request."""
    raw = case.metadata.get('handles')
    if raw is None:
        return None
    if case.kind not in KINDS:
        raise InvalidDocument('A handle catalog belongs to compiler, loading and script cases')
    closed(raw, {'schemas', 'hostSchemas', 'resolvers', 'answers'}, 'handle catalog')
    schemas = texts(raw.get('schemas'), 'schemas')
    host = texts(raw['hostSchemas'], 'hostSchemas') if 'hostSchemas' in raw else schemas
    resolvers = raw.get('resolvers')
    if resolvers is not None and (not isinstance(resolvers, list) or any(not isinstance(name, str) or not TYPE_NAME.fullmatch(name) for name in resolvers)
                                  or len(set(resolvers)) != len(resolvers)):
        raise InvalidDocument('Resolvers name distinct handle types without @')
    answers = raw.get('answers', [])
    if not isinstance(answers, list):
        raise InvalidDocument('Handle answers form a list')
    if (case.kind != 'scriptApi') and answers:
        raise InvalidDocument('Only script cases answer state reads')
    return {'schemas': schemas, 'hostSchemas': host, 'resolvers': resolvers, 'answers': [answer(item) for item in answers]}
