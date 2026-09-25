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

The personal name identifies the individual author independently of a business
designation. UTF-8 capable formats use the canonical spelling
with `ö`; an ASCII transliteration is not an alternative identity.

`2026` is the fixed first-publication year. It is not calculated from the clock
and does not become a rolling range during builds or routine edits. A copyright
holder or contributor keeps existing notices intact and adds a
separate notice only when legally appropriate.

## Short file headers

Handwritten C, C headers, C# and Swift use:

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

SwiftPM manifests place the required `swift-tools-version` directive first,
followed by the two license-comment lines. Python uses equivalent `#` comments.

The header applies to handwritten production and test C/C#/Swift, Python tooling, product documentation,
specifications, guides, README files, C# project files, the scoped EditorConfig,
the root Git ignore file, MSBuild configuration, GitHub Actions workflows,
solution and shell entry-point files, and XML-based TextMate bundle files.

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
- strict JSON (including `global.json`), TSV, plist-independent JSON TextMate grammars, and other formats
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
`NOTICE` file is intentionally not created. The CLI tool bundles external terminal
dependencies in its binary distribution; their source links, license texts, and
notices are included in its packaged
[THIRD-PARTY-NOTICES.md](implementation/csharp/GameEventScript.Tool/THIRD-PARTY-NOTICES.md).

The documentation website references npm packages rather than vendoring their
sources. Its generated static distribution includes Vite's
`third-party-licenses.md` and the separately copied `pagefind-licenses/` for
the search assets. Retain those files when uploading the website. Authored
JavaScript, TypeScript, CSS, Astro and SVG files use the equivalent source-comment
headers; strict JSON lockfiles and manifests retain the documented exclusion.

Generated API references additionally distribute DocFX and Swift-DocC-Render
presentation assets. Their unaltered upstream license and notice texts are stored
in `website/api/licenses` and copied to API output; these third-party texts must
not receive project copyright headers. DocFX dependency notices from the pinned
tool distribution accompany its reference too.

When third-party material is added, its existing copyright and license notices
must remain unchanged. Its redistribution terms must be reviewed, the material
must not receive the Game Event Script header as if it were original work, and
`LICENSE`, `NOTICE`, or a third-party-notices document must be updated when its
terms require that.

Standalone C# CLI archives also carry the exact bundled .NET runtime pack's
`LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`. Swift Linux archives retain the Static
SDK SBOM and the unmodified upstream license texts under
`tools/distribution/licenses`; the source inventory is documented there. These
third-party texts must not receive project copyright headers. Update that inventory
and verify applicable notices whenever the pinned SDK changes.
