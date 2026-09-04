# Game Event Script documentation

This directory is the canonical entry point for Game Event Script documentation. It separates normative, language-neutral contracts from non-normative learning material.

## Normative specification

Every contract area has exactly one owning document. Other documents link to that owner instead of restating its rules.

| Contract area | Owning document | Responsibility | Status |
| --- | --- | --- | --- |
| Language | [Language](Specification/Language.md) | Lexical grammar, syntax, static semantics, and language constructs | Normative |
| Public API | [Public API](Specification/PublicApi.md) | Language-neutral API responsibilities and per-language mappings | Normative |
| Host runtime | [Host runtime](Specification/HostRuntime.md) | Host lifecycle, dispatch, execution, context, and outbound behavior | Normative |
| Program model | [Program model](Specification/ProgramModel.md) | Immutable transport data, ownership, validation, and linking boundary | Normative |
| Bytecode | [Bytecode](Specification/Bytecode.md) | Instruction model, opcode semantics, operands, and control flow | Normative |
| Binary format | [Binary format](Specification/BinaryFormat.md) | `.gesb` container, segments, encoding, retention, and decoding | Normative |
| Assembler format | [Assembler format](Specification/AssemblerFormat.md) | Human-readable `.gesa` dump syntax and presentation metadata | Normative |
| Diagnostics | [Diagnostics](Specification/Diagnostics.md) | Stable phases, codes, locations, and propagation rules | Normative |
| Text semantics | [Text](Specification/Semantics/Text.md) | Unicode, source text, names, ordering, and scalar operations | Normative |
| Number semantics | [Numbers](Specification/Semantics/Numbers.md) | Integer, Binary64, units, arithmetic, conversion, and equality | Normative |
| Determinism | [Determinism](Specification/Semantics/Determinism.md) | Randomness, iteration, stable ordering, and deterministic dispatch | Normative |
| Conformance Markdown | [Markdown format](Specification/Conformance/MarkdownFormat.md) | Portable suite authoring syntax and normalized test model | Normative |
| Conformance runner | [Runner](Specification/Conformance/Runner.md) | Capabilities, execution, results, reports, and received updates | Normative |
| Conformance environment | [Environment](Specification/Conformance/Environment.md) | Fixed extensions, external types, sinks, observers, and resources | Normative |
| Cross-language acceptance | [Cross-language acceptance](Specification/Conformance/CrossLanguageAcceptance.md) | Corpus identity and acceptance criteria for language ports | Normative |

All specification documents in the table are normative.

## Non-normative guides

[Guide](Guide/README.md) contains tutorials, explanations, and examples intended for learning. A guide may simplify presentation but must link to the owning specification for exact behavior.

## Documentation rules

- Normative documents describe only the current contract. They contain no migration history, superseded architecture, or implementation diary.
- A rule is defined once. Cross-cutting documents use explicit links to the owning document.
- C#, Swift, Kotlin, C++, and Unity integrations may be idiomatic, but their observable behavior must satisfy the same language-neutral contracts.
- Work plans, backlogs, test inventories, generated reports, editor bundles, and historical memory files remain outside this directory.
- Public API names shown in a language mapping are not automatically language-neutral requirements unless the public API specification says so explicitly.
