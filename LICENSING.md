<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Licensing policy

Game Event Script is licensed under the Apache License, Version 2.0. The
unaltered official license text is stored in [LICENSE](LICENSE).

## Copyright identity and year

The canonical notice is:

```text
Copyright 2026 Stephan Schlöpke
```

The personal name identifies the individual author independently of a current
or future business designation. UTF-8 capable formats use the canonical spelling
with `ö`; an ASCII transliteration is not an alternative identity.

`2026` is the fixed first-publication year. It is not calculated from the clock
and does not become a rolling range during builds or routine edits. A future
copyright holder or contributor keeps existing notices intact and adds a
separate notice only when legally appropriate.

## Short file headers

Handwritten C# uses:

```csharp
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
```

Handwritten Markdown uses:

```html
<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->
```

XML-based project and editor files use the same two XML comment lines.
Line-oriented configuration uses `#` comments. The full license is included once
per distribution instead of being duplicated in every file.

The header applies to handwritten production and test C#, product documentation,
specifications, guides, README files, C# project files, the scoped EditorConfig,
the root Git ignore file, and XML-based TextMate bundle files.

## Intentional header exclusions

A missing inline header does not change the distribution license when the file
format or its role cannot safely carry one. The following files remain covered
by the top-level license but intentionally have no inserted header:

- `LICENSE`, because it must remain the unaltered official license text;
- strict Conformance Markdown suites and valid/invalid parser fixtures, because
  their first-byte grammar and integrity hashes are executable test input;
- generated Conformance reports, result JSON, received approval candidates,
  API snapshots, test results, build outputs, and crash dumps;
- `.gesb` binary fixtures and other binary assets;
- strict JSON, TSV, plist-independent JSON TextMate grammars, and other formats
  that do not admit comments without changing their schema;
- standalone `.ges` test inputs whose exact source is part of the test;
- operating-system metadata such as `.DS_Store`.

The nearest bundle README carries the license header for headerless JSON editor
assets. New handwritten file formats must either receive an equivalent legal
comment or be added here with a concrete technical reason.

## Third-party material and NOTICE

The current source inventory contains no identified vendored third-party source
or asset requiring an attribution notice. NuGet dependencies are referenced as
external packages rather than copied into this source distribution. Therefore a
`NOTICE` file is intentionally not created.

When third-party material is added, its existing copyright and license notices
must remain unchanged. Its redistribution terms must be reviewed, the material
must not receive the Game Event Script header as if it were original work, and
`LICENSE`, `NOTICE`, or a third-party-notices document must be updated when its
terms require that.
