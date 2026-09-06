<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script documentation

This directory is the canonical entry point for Game Event Script documentation. It separates normative, language-neutral contracts from non-normative learning material.

The project is distributed under the [Apache License 2.0](../LICENSE). The
repository header, attribution, and file-exclusion rules are defined by the
[licensing policy](../LICENSING.md); that policy is operational rather than part
of the language specification.

## Normative specification

Every contract area has exactly one owning document. Other documents link to that owner instead of restating its rules.

| Contract area | Owning document | Responsibility | Status |
| --- | --- | --- | --- |
| Language | [Language](../specs/Language.md) | Lexical grammar, syntax, static semantics, and language constructs | Normative |
| Public API | [Public API](../specs/PublicApi.md) | Language-neutral API responsibilities and per-language mappings | Normative |
| Host runtime | [Host runtime](../specs/HostRuntime.md) | Host lifecycle, dispatch, execution, context, and outbound behavior | Normative |
| Program model | [Program model](../specs/ProgramModel.md) | Immutable transport data, ownership, validation, and linking boundary | Normative |
| Bytecode | [Bytecode](../specs/Bytecode.md) | Instruction model, opcode semantics, operands, and control flow | Normative |
| Binary format | [Binary format](../specs/BinaryFormat.md) | `.gesb` container, segments, encoding, retention, and decoding | Normative |
| Assembler format | [Assembler format](../specs/AssemblerFormat.md) | Human-readable `.gesa` dump syntax and presentation metadata | Normative |
| Diagnostics | [Diagnostics](../specs/Diagnostics.md) | Stable phases, codes, locations, and propagation rules | Normative |
| Text semantics | [Text](../specs/Semantics/Text.md) | Unicode, source text, names, ordering, and scalar operations | Normative |
| Number semantics | [Numbers](../specs/Semantics/Numbers.md) | Integer, Binary64, units, arithmetic, conversion, and equality | Normative |
| Determinism | [Determinism](../specs/Semantics/Determinism.md) | Randomness, iteration, stable ordering, and deterministic dispatch | Normative |
| Conformance Markdown | [Markdown format](../specs/Conformance/MarkdownFormat.md) | Portable suite authoring syntax and normalized test model | Normative |
| Conformance runner | [Runner](../specs/Conformance/Runner.md) | Capabilities, execution, results, reports, and received updates | Normative |
| Conformance environment | [Environment](../specs/Conformance/Environment.md) | Fixed extensions, external types, sinks, observers, and resources | Normative |
| Conformance coverage | [Coverage](../specs/Conformance/Coverage.md) | Stable mapping from portable behavior to executable case IDs | Normative |
| Cross-language acceptance | [Cross-language acceptance](../specs/Conformance/CrossLanguageAcceptance.md) | Corpus identity and acceptance criteria for language ports | Normative |

All specification documents in the table are normative.

## Non-normative guides

[Guide](guide/README.md) contains tutorials, explanations, and examples intended for learning. A guide may simplify presentation but must link to the owning specification for exact behavior.

## Documentation rules

- Normative documents describe only the current contract. They contain no migration history, superseded architecture, or implementation diary.
- A rule is defined once. Cross-cutting documents use explicit links to the owning document.
- C#, Swift, Kotlin, C++, and Unity integrations may be idiomatic, but their observable behavior must satisfy the same language-neutral contracts.
- Work plans, backlogs, test inventories, generated reports, editor bundles, and historical memory files remain outside this directory.
- Public API names shown in a language mapping are not automatically language-neutral requirements unless the public API specification says so explicitly.
