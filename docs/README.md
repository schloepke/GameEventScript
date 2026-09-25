<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script documentation

This directory is the canonical entry point for Game Event Script documentation. It separates normative, language-neutral contracts from non-normative learning material.

The project is distributed under the [Apache License 2.0](../LICENSE). The
repository header, attribution, and file-exclusion rules are defined by the
[licensing policy](../LICENSING.md); that policy is operational rather than part
of the language specification.

## Normative specification

Version notes such as **Since: 0.1.0** and **Since: Unreleased** are informative
availability hints, not additional language rules or a compatibility matrix.
A note below a document title identifies its baseline; a more specific feature
note takes precedence. A qualified note applies only to the feature it names.
The version refers to the behavior currently described, not necessarily the
first appearance of a similarly named feature. When behavior changes, update
the note to the new release rather than retaining old behavior here.
**Unreleased** requires a matching development build. Release history and
migration details belong in the [changelog](../CHANGELOG.md).

Every contract area has exactly one owning document. Other documents link to that owner instead of restating its rules.

| Contract area | Owning document | Responsibility | Status |
| --- | --- | --- | --- |
| Language | [Language](../specs/Language.md) | Lexical grammar, syntax, static semantics, and language constructs | Normative |
| Syntax highlighting | [Highlighting](../specs/SyntaxHighlighting.md) | Optional editor/terminal ranges, TextMate scopes and incremental states | Normative |
| Public API | [Public API](../specs/PublicApi.md) | Language-neutral API responsibilities and per-language mappings | Normative |
| Product JSON | [Message format](../specs/MessageFormat.md) | Optional versioned message/value transport and external snapshots | Normative |
| Host runtime | [Host runtime](../specs/HostRuntime.md) | Host lifecycle, dispatch, execution, context, and outbound behavior | Normative |
| Program model | [Program model](../specs/ProgramModel.md) | Immutable transport data, ownership, validation, and linking boundary | Normative |
| Bytecode | [Bytecode](../specs/Bytecode.md) | Instruction model, opcode semantics, operands, and control flow | Normative |
| Binary format | [Binary format](../specs/BinaryFormat.md) | `.gesb` container, segments, encoding, retention, and decoding | Normative |
| Assembler format | [Assembler format](../specs/AssemblerFormat.md) | Human-readable `.gesa` dump syntax and presentation metadata | Normative |
| Diagnostics | [Diagnostics](../specs/Diagnostics.md) | Stable phases, codes, locations, and propagation rules | Normative |
| Text semantics | [Text](../specs/Semantics/Text.md) | Unicode, source text, names, ordering, scalar operations, data formatting, and literal recognition | Normative |
| Number semantics | [Numbers](../specs/Semantics/Numbers.md) | Integer, Binary64, units, arithmetic, conversion, and equality | Normative |
| Determinism | [Determinism](../specs/Semantics/Determinism.md) | Randomness, iteration, stable ordering, and deterministic dispatch | Normative |
| Conformance Markdown | [Markdown format](../specs/Conformance/MarkdownFormat.md) | Portable suite authoring syntax and normalized test model | Normative |
| Conformance runner | [Runner](../specs/Conformance/Runner.md) | Capabilities, execution, results, reports, and received updates | Normative |
| Conformance environment | [Environment](../specs/Conformance/Environment.md) | Fixed extensions, external types, sinks, observers, and resources | Normative |
| Conformance coverage | [Coverage](../specs/Conformance/Coverage.md) | Stable mapping from portable behavior to executable case IDs | Normative |
| Cross-language acceptance | [Cross-language acceptance](../specs/Conformance/CrossLanguageAcceptance.md) | Corpus identity and acceptance criteria for language ports | Normative |

All specification documents in the table are normative.

## Generated API references

- [C# API reference](guide/api/CSharp.md): public assemblies and XML documentation.
- [Swift API reference](guide/api/Swift.md): public symbol graphs and DocC documentation.

These describe the development checkout and show their source revision. They
complement the portable specification with concrete language APIs.

## Non-normative guides

The [TextMate editor bundles](../tools/editors/README.md) provide downloadable
JSON and classic XML grammars for `.ges` and `.gesa`, with installation guidance.

Start with [Learn Game Event Script](guide/Language.md), then choose the
[C# embedding guide](guide/CSharp.md) or [Swift embedding guide](guide/Swift.md).

[Guide](guide/README.md) contains tutorials, explanations, and examples intended for learning. A guide may simplify presentation but must link to the owning specification for exact behavior.

The [C# implementation guide](../implementation/csharp/README.md) describes the
library boundaries, solution and toolchain, build/test commands, CLI lifecycle
and Compiler/Runtime embedding. The
[C# bridge guide](../implementation/csharp/GameEventScript.CSharpBridge/README.md)
covers attribute-based external types, extension functions and native callbacks. The
[C# CLI guide](../implementation/csharp/GameEventScript.Tool/README.md)
documents `dotnet ges` and the interactive event console; the
[C# distribution guide](guide/distribution/CSharp.md) covers local packages,
DLL sets and release verification.
The [package release guide](guide/distribution/Packages.md) describes the shared
NuGet/SwiftPM release scope, Xcode consumption and publisher setup.
The [changelog](../CHANGELOG.md) collects user-visible changes and migration
guidance for the next release.

The [Swift implementation guide](../implementation/swift/README.md) describes
the package boundaries, current port coverage, and local verification commands.
It also documents the [Xcode workspace](../implementation/swift/README.md#xcode-workspace)
for building and testing all Swift packages together.
The [Swift CLI guide](../implementation/swift/GameEventScriptTool/README.md)
describes `ges`, installation/update, the event console and native CLI verification.
The [SwiftBridge guide](../implementation/swift/GameEventScriptSwiftBridge/README.md)
describes native closures, value conversions, typed external bindings and the
optional synchronized Host runner. It depends only on Runtime.
The [Swift performance guide](../implementation/swift/Performance.md) documents
the measured profile, instrumentation scope and baseline review workflow.
The generated performance baseline matrix (`artifacts/conformance/PerformanceMatrix.md`)
lists every performance case with C# and Swift time/allocation references side by
side. Create or refresh both tables with
`ruby --disable-gems scripts/write-performance-matrix.rb`; the report stays under
`artifacts`, and the shared Markdown remains the source of truth.

The [numeric text roundtrip gate](../conformance/cross-language/NumberTextRoundtrip.md)
checks exact Number, Quantity and Percentage text exchange between C# and Swift.
The [product JSON roundtrip gate](../conformance/cross-language/MessageJsonRoundtrip.md)
checks actual ordered message/value interchange against independent Markdown oracles.

## Documentation rules

- Normative documents describe only the current contract.
- A rule is defined once. Cross-cutting documents use explicit links to the owning document.
- C#, Swift, Kotlin, Go, Rust, C++, and Unity integrations may be idiomatic, but their observable behavior must satisfy the same language-neutral contracts.
- Work plans, backlogs, test inventories, generated reports, and editor bundles remain outside the normative specification tree.
- Public API names shown in a language mapping are not automatically language-neutral requirements unless the public API specification says so explicitly.
