<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Shared Markdown V1 parser fixtures

These bootstrap fixtures are consumed unchanged by every language port. They
test the Markdown/YAML boundary before a port can execute the semantic corpus.

`manifest.tsv` is the machine-readable V1 manifest. It is UTF-8 without BOM,
uses `LF`, has one header row, and contains tab-separated fields without quoting
or escapes. Paths are relative to this directory and use `/`. SHA-256 values are
uppercase hexadecimal hashes of the exact fixture bytes. `-` denotes a field
that does not apply to the fixture outcome.

A `valid` row must parse to exactly one case with the stated suite and local
case IDs. An `invalid` row must fail and its first diagnostic must have the
stated stable code. Ports must verify the fixture hash before parsing so that
they never compare different bootstrap inputs accidentally.
