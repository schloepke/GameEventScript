# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Small, dependency-free reader for the Conformance YAML subset (not full YAML)."""
import json
import re
from decimal import Decimal


class InvalidDocument(ValueError):
    """Stable input diagnostic, never a conformance assertion failure."""

    def __init__(self, message, code='conformance.schema.invalidValue', offset=None):
        super().__init__(message)
        self.code, self.offset = code, offset
        self.document = None

    def diagnostic(self):
        result = {'phase': 'conformance', 'code': self.code, 'message': str(self)}
        if self.document is not None:
            result.update(self.document.location(self.offset or 0))
        return result


def colon(text):
    quote = None
    escaped = False
    for i, char in enumerate(text):
        if escaped:
            escaped = False
        elif quote == '"' and char == '\\':
            escaped = True
        elif quote:
            if char == quote:
                quote = None
        elif char in "\"'":
            quote = char
        elif char == ':' and (i + 1 == len(text) or text[i + 1] == ' '):
            return i
    return None


def strip_comment(text):
    quote = None
    escaped = False
    for i, char in enumerate(text):
        if escaped:
            escaped = False
        elif quote == '"' and char == '\\':
            escaped = True
        elif quote:
            if char == quote:
                quote = None
        elif char in "\"'":
            quote = char
        elif char == '#' and (i == 0 or text[i - 1] in ' \t'):
            return text[:i].rstrip()
    return text.rstrip()


class Flow:
    def __init__(self, text, ranges=None, base=0):
        self.text, self.i = text, 0
        self.ranges, self.base = ranges, base

    def spaces(self):
        while self.i < len(self.text) and self.text[self.i] == ' ':
            self.i += 1

    def quoted(self):
        start = self.i
        quote = self.text[self.i]
        self.i += 1
        if quote == '"':
            try:
                value, length = json.JSONDecoder().raw_decode(self.text[start:])
                value.encode('utf-8', errors='strict')
            except (ValueError, UnicodeError) as error:
                raise InvalidDocument('Invalid quoted YAML string', 'conformance.yaml.invalidScalar') from error
            self.i = start + length
            return value
        value = ''
        while self.i < len(self.text):
            char = self.text[self.i]
            self.i += 1
            if char == "'":
                if self.i < len(self.text) and self.text[self.i] == "'":
                    self.i += 1
                else:
                    return value
            value += char
        raise InvalidDocument('Unclosed YAML string', 'conformance.yaml.syntax')

    def value(self, depth=0, path=()):
        self.spaces()
        start = self.i
        try:
            result = self._value(depth, path)
        except InvalidDocument as error:
            if error.offset is None:
                error.offset = self.base + start
            raise
        if self.ranges is not None:
            self.ranges[path] = (self.base + start, self.base + len(self.text[:self.i].rstrip(' ')))
        return result

    def _value(self, depth=0, path=()):
        if depth > 64:
            raise InvalidDocument('YAML nesting limit exceeded', 'conformance.yaml.limitExceeded')
        self.spaces()
        if self.i == len(self.text):
            raise InvalidDocument('Missing flow value', 'conformance.yaml.syntax')
        char = self.text[self.i]
        if char in "\"'":
            return self.quoted()
        if char in '[{':
            mapping = char == '{'
            end = '}' if mapping else ']'
            result = {} if mapping else []
            self.i += 1
            self.spaces()
            if self.i < len(self.text) and self.text[self.i] == end:
                self.i += 1
                return result
            while True:
                if mapping:
                    self.spaces()
                    if self.i < len(self.text) and self.text[self.i] in "\"'":
                        key = self.quoted()
                    else:
                        match = re.match(r'[A-Za-z][A-Za-z0-9_.-]*', self.text[self.i:])
                        if not match:
                            raise InvalidDocument('Invalid flow key', 'conformance.yaml.syntax')
                        key = match[0]
                        self.i += len(key)
                    self.spaces()
                    if self.i == len(self.text) or self.text[self.i] != ':':
                        raise InvalidDocument('Missing flow mapping colon', 'conformance.yaml.syntax')
                    self.i += 1
                    if key in result:
                        raise InvalidDocument('Duplicate YAML key: ' + key, 'conformance.yaml.duplicateKey')
                    result[key] = self.value(depth + 1, path + (key,))
                else:
                    result.append(self.value(depth + 1, path + (len(result),)))
                self.spaces()
                if self.i == len(self.text):
                    raise InvalidDocument('Unclosed flow container', 'conformance.yaml.syntax')
                separator = self.text[self.i]
                self.i += 1
                if separator == end:
                    return result
                if separator != ',':
                    raise InvalidDocument('Expected flow separator', 'conformance.yaml.syntax')
        start = self.i
        while self.i < len(self.text) and self.text[self.i] not in ',]}':
            self.i += 1
        token = self.text[start:self.i].strip(' ')
        if not token or token[0] in '#&*!%@`|>?' or token in ('---', '...', '.nan', '.inf', '-.inf'):
            raise InvalidDocument('Unsupported YAML scalar: ' + token, 'conformance.yaml.unsupportedFeature')
        if token in ('null', 'true', 'false'):
            return {'null': None, 'true': True, 'false': False}[token]
        if re.fullmatch(r'-?(0|[1-9][0-9]*)', token):
            return int(token)
        if re.fullmatch(r'-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?', token):
            return Decimal(token)
        if re.fullmatch(r'[-+]?(?:0[xob][0-9A-Fa-f]+|[0-9]+:[0-9:]+|[0-9]{4}-[0-9]{2}-[0-9]{2}|0[0-9]+|\+[0-9]+)', token) or token.lower() in ('.nan', '.inf', '+.inf', '-.inf'):
            raise InvalidDocument('Unsupported numeric/date scalar: ' + token, 'conformance.yaml.invalidScalar')
        return token


def inline(text, ranges=None, base=0, path=()):
    parser = Flow(text, ranges, base)
    value = parser.value(path=path)
    parser.spaces()
    if parser.i != len(text):
        raise InvalidDocument('Trailing YAML data', 'conformance.yaml.syntax')
    return value


def parse(text, ranges=None):
    """Parse YAML; optional ranges map paths to half-open Unicode offsets."""
    lines = []
    offset = 0
    for number, line in enumerate(text.split('\n'), 1):
        line_offset = offset
        offset += len(line) + 1
        stripped = strip_comment(line)
        if not stripped.strip():
            continue
        prefix = stripped[:len(stripped) - len(stripped.lstrip(' \t'))]
        if '\t' in prefix or len(prefix) % 2:
            raise InvalidDocument(f'Invalid YAML indentation at line {number}', 'conformance.yaml.syntax', line_offset)
        lines.append((len(prefix), stripped[len(prefix):], line_offset + len(prefix)))
    index = 0

    def block(indent, depth=0, path=()):
        try:
            return block_value(indent, depth, path)
        except InvalidDocument as error:
            if error.offset is None:
                error.offset = lines[min(index, len(lines) - 1)][2] if lines else 0
            raise

    def block_value(indent, depth=0, path=()):
        nonlocal index
        if depth > 64 or index >= len(lines) or lines[index][0] != indent:
            raise InvalidDocument('Invalid YAML block nesting', 'conformance.yaml.syntax')
        sequence = lines[index][1] == '-' or lines[index][1].startswith('- ')
        result = [] if sequence else {}
        while index < len(lines) and lines[index][0] >= indent:
            actual, content, origin = lines[index]
            if actual != indent:
                raise InvalidDocument('Unexpected YAML indentation', 'conformance.yaml.syntax')
            if sequence:
                if content != '-' and not content.startswith('- '):
                    raise InvalidDocument('Mixed YAML mapping and sequence', 'conformance.yaml.syntax')
                item = content[2:] if content.startswith('- ') else ''
                if item and colon(item) is not None and item[0] not in '[{':
                    # Reinterpret the compact first mapping property at its logical indentation.
                    lines[index] = (indent + 2, item, origin + 2)
                    result.append(block(indent + 2, depth + 1, path + (len(result),)))
                else:
                    index += 1
                    result.append(inline(item, ranges, origin + 2, path + (len(result),)) if item else block(indent + 2, depth + 1, path + (len(result),)))
            else:
                split = colon(content)
                if split is None:
                    raise InvalidDocument('Expected key: value', 'conformance.yaml.syntax')
                key = content[:split].rstrip()
                if key.startswith(('"', "'")):
                    key = inline(key)
                elif not re.fullmatch(r'[A-Za-z][A-Za-z0-9_.-]*', key):
                    raise InvalidDocument('Invalid YAML key', 'conformance.yaml.syntax')
                if not isinstance(key, str) or key in result:
                    raise InvalidDocument('Duplicate or invalid YAML key: ' + str(key), 'conformance.yaml.duplicateKey')
                item = content[split + 1:].lstrip(' ')
                index += 1
                if item:
                    result[key] = inline(item, ranges, origin + len(content) - len(item), path + (key,))
                elif index < len(lines) and lines[index][0] > indent:
                    result[key] = block(indent + 2, depth + 1, path + (key,))
                else:
                    result[key] = None
        return result

    if not lines:
        raise InvalidDocument('Expected YAML root mapping', 'conformance.yaml.syntax')
    result = block(0)
    if not isinstance(result, dict) or index != len(lines):
        raise InvalidDocument('Expected one YAML root mapping', 'conformance.yaml.syntax')
    return result
