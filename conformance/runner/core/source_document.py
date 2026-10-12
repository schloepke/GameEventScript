# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Immutable UTF-8 input and ranges, independent of filesystem access."""
from dataclasses import dataclass
import hashlib
import re
from core.yaml_subset import InvalidDocument


@dataclass(frozen=True)
class SourceDocument:
    name: str
    data: bytes

    def __post_init__(self):
        if not isinstance(self.data, bytes):
            raise TypeError('Document data must be immutable bytes')
        try:
            self.content.decode('utf-8', errors='strict')
        except UnicodeDecodeError as cause:
            error = InvalidDocument('Invalid UTF-8 document', 'conformance.markdown.invalidUtf8', cause.start)
            error.document = self
            raise error from cause

    @property
    def content(self):
        return self.data[3:] if self.data.startswith(b'\xef\xbb\xbf') else self.data

    @property
    def digest(self):
        return hashlib.sha256(self.data).hexdigest()

    @property
    def text(self):
        return self.content.decode('utf-8')

    @property
    def line_ending(self):
        endings = set(re.findall(b'\r\n|\r|\n', self.content))
        return next(iter(endings)) if len(endings) == 1 else b'\n'

    def location(self, offset):
        prefix = self.content[:offset].decode('utf-8', errors='replace')
        lines = re.split(r'\r\n|\r|\n', prefix)
        return {'sourceName': self.name, 'byteOffset': offset, 'byteLength': 0, 'line': len(lines), 'column': len(lines[-1]) + 1}

    def lines(self):
        """Return logical text lines and original byte offsets, excluding BOM."""
        lines, offsets, position = [], [], 0
        for match in re.finditer(r'([^\r\n]*)(\r\n|\r|\n|$)', self.text):
            lines.append(match[1])
            offsets.append(position)
            position += len(match[0].encode('utf-8'))
        return lines, offsets
