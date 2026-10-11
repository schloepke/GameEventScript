# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Structural corpus reader. Full execution schema validation is kind-specific."""
from dataclasses import dataclass, fields
from copy import deepcopy
from core.immutable import freeze
from pathlib import Path
import re

from core.yaml_subset import InvalidDocument, parse
from core.source_document import SourceDocument

MAX_DOCUMENT_BYTES = 16 * 1024 * 1024
ID = re.compile(r'[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*\Z')
DEFAULTS = {'kind', 'level', 'categories', 'tags', 'requires', 'compile', 'runtimeLimits', 'comparison'}
FRONT = DEFAULTS | {'formatVersion', 'suiteId', 'title'}
CASE = DEFAULTS | {'gesBlock', 'id', 'sources', 'random', 'publishSink', 'externalTypeRegistry',
                   'hostCount', 'deferredPrograms', 'nativeHandlers', 'stepActions', 'messageApi',
                   'valueApi', 'externalTypeApi', 'binaryFixture', 'performance'}
KINDS = {'scriptApi', 'compileError', 'loadError', 'messageApi', 'valueApi', 'externalTypeApi',
         'compileMetadata', 'bytecode', 'bytecodeSnapshot', 'programBinary', 'performance'}


def logical_lines(text):
    return text.replace('\r\n', '\n').replace('\r', '\n')


def closed(value, allowed, label):
    if not isinstance(value, dict) or set(value) - allowed:
        raise InvalidDocument('Invalid or unknown fields in ' + label, 'conformance.schema.unknownField')


@dataclass
class Case:
    id: str
    kind: str
    path: str
    title: str
    metadata: dict
    sources: list
    expected: dict | None
    assembler: str | None
    steps: list
    document: SourceDocument | None = None
    ranges: dict | None = None
    _sealed: bool = False

    def __setattr__(self, name, value):
        if getattr(self, '_sealed', False):
            raise TypeError('Normalized case is immutable; copy it before editing')
        object.__setattr__(self, name, value)

    def __deepcopy__(self, memo):
        return Case(**{field.name: deepcopy(getattr(self, field.name), memo)
                       for field in fields(self) if field.name != '_sealed'})

    def seal(self):
        for name in ('metadata', 'sources', 'expected', 'steps', 'ranges'):
            setattr(self, name, freeze(getattr(self, name)))
        self._sealed = True
        return self


def read_document(path):
    path = Path(path)
    with path.open('rb') as stream:
        data = stream.read(MAX_DOCUMENT_BYTES + 1)
    if len(data) > MAX_DOCUMENT_BYTES:
        raise InvalidDocument('Document size limit exceeded')
    return parse_document(data, str(path.resolve()))


def parse_document(data, name='<document>'):
    """Parse an immutable input buffer; only read_document performs file I/O."""
    document = SourceDocument(name, data)
    try:
        return _parse_document(document)
    except InvalidDocument as error:
        error.document = document
        raise


def _parse_document(document):
    if len(document.data) > MAX_DOCUMENT_BYTES:
        raise InvalidDocument('Document size limit exceeded', 'conformance.yaml.limitExceeded')
    lines, offsets = document.lines()
    if b'\0' in document.content:
        raise InvalidDocument('NUL in document', 'conformance.yaml.invalidScalar', document.content.index(b'\0'))

    def yaml(start, end):
        payload = '\n'.join(lines[start:end])
        spans = {}
        try:
            result = parse(payload, spans)
        except InvalidDocument as error:
            local = error.offset or 0
            row = payload[:local].count('\n')
            column = len(payload[:local].rsplit('\n', 1)[-1])
            error.offset = offsets[start + row] + len(lines[start + row][:column].encode('utf-8'))
            raise
        def byte_offset(char):
            row = payload[:char].count('\n')
            column = len(payload[:char].rsplit('\n', 1)[-1])
            return offsets[start + row] + len(lines[start + row][:column].encode('utf-8'))
        return result, {path: (byte_offset(a), byte_offset(b)) for path, (a, b) in spans.items()}
    if lines[0] != '---':
        raise InvalidDocument('Missing frontmatter', 'conformance.markdown.missingFrontmatter')
    try:
        end = lines.index('---', 1)
    except ValueError as error:
        raise InvalidDocument('Unclosed frontmatter', 'conformance.markdown.unterminatedFrontmatter') from error
    front, front_ranges = yaml(1, end)
    closed(front, FRONT, 'frontmatter')
    if type(front.get('formatVersion')) is not int or front['formatVersion'] != 1:
        raise InvalidDocument('Unsupported format version', 'conformance.schema.unsupportedVersion')
    if 'title' in front and not isinstance(front['title'], str):
        raise InvalidDocument('Invalid suite title')
    suite = front.get('suiteId')
    if not isinstance(suite, str) or not ID.fullmatch(suite):
        raise InvalidDocument('Invalid suite ID')
    cases, current = [], None
    fixture_seen = False
    index = end + 1
    while index < len(lines):
        line = lines[index]
        if line.startswith('## Test: '):
            title = line[9:].strip(' \t')
            if not title:
                raise InvalidDocument('Empty test title', 'conformance.markdown.invalidTestHeading')
            if current is not None:
                current['ranges'][('test',)] = (current['offset'], offsets[index])
            current = {'title': title, 'ges': [], 'gesa': [], 'yaml': [], 'steps': [],
                       'ranges': {('frontmatter',): (0, offsets[end] + 3)}, 'offset': offsets[index]}
            cases.append(current)
        elif line.startswith('## Test:'):
            raise InvalidDocument('Invalid test heading', 'conformance.markdown.invalidTestHeading', offsets[index])
        elif line == '## Fixtures':
            if fixture_seen or cases:
                raise InvalidDocument('Misplaced fixtures section')
            fixture_seen = True
        elif re.match(r'^(`{3,}|~{3,})', line):
            opening = re.match(r'^(`{3,}|~{3,})(.*)$', line)
            fence, info = opening.groups()
            semantic = current is not None and fence == '```' and info in ('ges', 'gesa', 'yaml')
            if current is not None and not semantic and (info.startswith('ges') or info.startswith('yaml')):
                raise InvalidDocument('Unknown semantic fence: ' + info, 'conformance.markdown.unknownSemanticFence', offsets[index])
            start = index + 1
            index = start
            while index < len(lines):
                if (lines[index] == fence if semantic else re.fullmatch(re.escape(fence[0]) + '{' + str(len(fence)) + r',}[ \t]*', lines[index])):
                    break
                index += 1
            if index == len(lines):
                raise InvalidDocument('Unclosed fence', 'conformance.markdown.unterminatedFence')
            if semantic:
                current['ranges'][('block', info, len(current[info]))] = (offsets[start - 1], offsets[index] + len(fence))
                payload = '\n'.join(lines[start:index])
                if info == 'yaml':
                    node, spans = yaml(start, index)
                    current[info].append(node)
                    if isinstance(node, dict) and node.get('gesBlock') == 'expect':
                        current['ranges'].update(spans)
                else:
                    current[info].append(payload)
                    if info == 'gesa':
                        current['ranges'][('assembler',)] = (offsets[start], offsets[index])
        elif line == '### Steps' and current is not None:
            if current['steps']:
                raise InvalidDocument('Duplicate steps table')
            index += 1
            while index < len(lines) and not lines[index].strip():
                index += 1
            if lines[index:index + 2] != ['| step | receive | pump | budget |', '| --- | --- | --- | --- |']:
                raise InvalidDocument('Invalid steps table header', 'conformance.markdown.invalidStepsTable')
            index += 2
            while index < len(lines) and lines[index].startswith('|'):
                cells = [cell.strip(' \t') for cell in lines[index].split('|')]
                if len(cells) != 6 or cells[0] or cells[-1]:
                    raise InvalidDocument('Invalid steps row', 'conformance.markdown.invalidStepsTable')
                step, receive, pump, budget = cells[1:-1]
                if not ID.fullmatch(step) or pump not in ('completion', 'frames', 'frame', 'enqueue'):
                    raise InvalidDocument('Invalid step or pump')
                if any(row['step'] == step for row in current['steps']):
                    raise InvalidDocument('Duplicate step')
                if pump in ('frames', 'frame'):
                    if not budget.isascii() or not budget.isdigit() or not 0 < int(budget) <= 4294967295:
                        raise InvalidDocument('Invalid frame budget')
                elif budget:
                    raise InvalidDocument('Unexpected frame budget')
                current['steps'].append(dict(zip(('step', 'receive', 'pump', 'budget'), cells[1:-1])))
                index += 1
            index -= 1
        index += 1
    if current is not None:
        current['ranges'][('test',)] = (current['offset'], len(document.content))
    if not cases:
        raise InvalidDocument('Suite has no cases')
    result, seen = [], set()
    for raw in cases:
        blocks = {}
        for block in raw['yaml']:
            if not isinstance(block, dict):
                raise InvalidDocument('Expected YAML mapping')
            discriminator = block.get('gesBlock')
            if discriminator not in ('case', 'expect') or discriminator in blocks:
                raise InvalidDocument('Missing or duplicate YAML discriminator')
            blocks[discriminator] = block
        if 'case' not in blocks:
            raise InvalidDocument('Missing case block', 'conformance.schema.missingField')
        meta = blocks['case']
        closed(meta, CASE, 'case')
        local = meta.get('id')
        if not isinstance(local, str) or not ID.fullmatch(local) or local in seen:
            raise InvalidDocument('Invalid or duplicate case ID', 'conformance.schema.duplicateId')
        seen.add(local)
        merged = {key: value for key, value in front.items() if key in DEFAULTS}
        merged.update(meta)
        for key in ('compile', 'runtimeLimits', 'comparison', 'requires'):
            if key in front or key in meta:
                if not isinstance(front.get(key, {}), dict) or not isinstance(meta.get(key, {}), dict):
                    raise InvalidDocument('Expected mapping: ' + key)
                merged[key] = {**front.get(key, {}), **meta.get(key, {})}
        for key in ('categories', 'tags'):
            if any(not isinstance(values.get(key, []), list) for values in (front, meta)):
                raise InvalidDocument('Expected text list: ' + key)
            items = front.get(key, []) + meta.get(key, [])
            if not isinstance(items, list) or any(not isinstance(item, str) for item in items):
                raise InvalidDocument('Expected text list: ' + key)
            if key == 'categories' and any(not ID.fullmatch(item) for item in items):
                raise InvalidDocument('Invalid category identifier')
            merged[key] = list(dict.fromkeys(items))
        for key in ('core', 'optional'):
            if any(not isinstance(values.get('requires', {}).get(key, []), list) or any(not isinstance(item, str) for item in values.get('requires', {}).get(key, [])) for values in (front, meta)):
                raise InvalidDocument('Expected capability list: ' + key)
            items = front.get('requires', {}).get(key, []) + meta.get('requires', {}).get(key, [])
            merged.setdefault('requires', {})[key] = list(dict.fromkeys(items))
        kind = merged.get('kind')
        if kind not in KINDS or merged.get('level') not in ('atomic', 'scenario'):
            raise InvalidDocument('Missing/invalid kind or level', 'conformance.schema.unknownKind')
        descriptors = merged.get('sources', [{'name': suite + '.' + local + '.ges'}] if raw['ges'] else [])
        if not isinstance(descriptors, list) or len(descriptors) != len(raw['ges']):
            raise InvalidDocument('Source descriptor count mismatch', 'conformance.schema.invalidCardinality')
        sources = []
        for descriptor, source in zip(descriptors, raw['ges']):
            closed(descriptor, {'name', 'program'}, 'source descriptor')
            name, program = descriptor.get('name'), descriptor.get('program', 'main')
            if not isinstance(name, str) or not name or not isinstance(program, str) or not ID.fullmatch(program):
                raise InvalidDocument('Invalid source descriptor')
            sources.append({'name': name, 'program': program, 'text': source})
        if len(raw['gesa']) > 1:
            raise InvalidDocument('Multiple assembler blocks')
        expected = blocks.get('expect')
        if expected and 'steps' in expected:
            if set(expected['steps']) - {step['step'] for step in raw['steps']}:
                raise InvalidDocument('Unknown step reference', 'conformance.schema.unknownReference')
        result.append(Case(suite + '/' + local, kind, document.name, raw['title'], merged, sources,
                           expected, raw['gesa'][0] if raw['gesa'] else None, raw['steps'], document, raw['ranges']).seal())
    return suite, result


def read_corpus(paths):
    suites, cases = set(), []
    for path in paths:
        try:
            suite, document = read_document(path)
        except InvalidDocument:
            raise
        except (ValueError, TypeError, KeyError, RecursionError) as error:
            raise InvalidDocument(f'{path}: {error}') from error
        if suite in suites:
            raise InvalidDocument('Duplicate suite: ' + suite)
        suites.add(suite)
        cases.extend(document)
    return cases
