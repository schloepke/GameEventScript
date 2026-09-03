# Game Event Script documentation

This directory is the canonical entry point for Game Event Script documentation. It separates normative, language-neutral contracts from non-normative learning material.

> [!IMPORTANT]
> Documents marked **Structural draft** define their ownership boundary but are not yet complete normative specifications. A document becomes normative only after its contract has been populated, reviewed, and marked **Normative**.

## Normative specification

Every contract area has exactly one owning document. Other documents link to that owner instead of restating its rules.

| Contract area | Owning document | Responsibility | Status |
| --- | --- | --- | --- |
| Language | [Language](Specification/Language.md) | Lexical grammar, syntax, static semantics, and language constructs | Structural draft |
| Public API | [Public API](Specification/PublicApi.md) | Language-neutral API responsibilities and per-language mappings | Structural draft |
| Host runtime | [Host runtime](Specification/HostRuntime.md) | Host lifecycle, dispatch, execution, context, and outbound behavior | Structural draft |
| Program model | [Program model](Specification/ProgramModel.md) | Immutable transport data, ownership, validation, and linking boundary | Structural draft |
| Bytecode | [Bytecode](Specification/Bytecode.md) | Instruction model, opcode semantics, operands, and control flow | Structural draft |
| Binary format | [Binary format](Specification/BinaryFormat.md) | `.gesb` container, segments, encoding, retention, and decoding | Structural draft |
| Assembler format | [Assembler format](Specification/AssemblerFormat.md) | Human-readable `.gesa` dump syntax and presentation metadata | Structural draft |
| Diagnostics | [Diagnostics](Specification/Diagnostics.md) | Stable phases, codes, locations, and propagation rules | Structural draft |
| Text semantics | [Text](Specification/Semantics/Text.md) | Unicode, source text, names, ordering, and scalar operations | Structural draft |
| Number semantics | [Numbers](Specification/Semantics/Numbers.md) | Integer, Binary64, units, arithmetic, conversion, and equality | Structural draft |
| Determinism | [Determinism](Specification/Semantics/Determinism.md) | Randomness, iteration, stable ordering, and deterministic dispatch | Structural draft |
| Conformance Markdown | [Markdown format](Specification/Conformance/MarkdownFormat.md) | Portable suite authoring syntax and normalized test model | Structural draft |
| Conformance runner | [Runner](Specification/Conformance/Runner.md) | Capabilities, execution, results, reports, and received updates | Structural draft |
| Conformance environment | [Environment](Specification/Conformance/Environment.md) | Fixed extensions, external types, sinks, observers, and resources | Structural draft |
| Cross-language acceptance | [Cross-language acceptance](Specification/Conformance/CrossLanguageAcceptance.md) | Corpus identity and acceptance criteria for language ports | Structural draft |

## Current normative sources

Until a target specification above is complete and marked **Normative**, the following existing contracts remain authoritative:

| Contract area | Current source |
| --- | --- |
| Host runtime | [Host architecture](../HostArchitecture.md) |
| Program model | [Portable program model](../PortableProgramModel.md) |
| Bytecode | [Bytecode specification](../BytecodeSpec.md) and [opcode instruction shape](../BytecodeOpcodeShape.md) |
| Binary format | [`.gesb` V1](../GesbFormatV1.md) |
| Diagnostics | [Portable diagnostics](../PortableDiagnostics.md) |
| Text semantics | [Portable text semantics](../PortableTextSemantics.md) |
| Number semantics | [Portable number semantics](../PortableNumberSemantics.md) |
| Determinism | [Portable determinism semantics](../PortableDeterminismSemantics.md) |
| Conformance Markdown | [Conformance Markdown V1](../ConformanceMarkdownV1.md) |
| Conformance runner | [Conformance Runner V1](../ConformanceRunnerV1.md) |
| Conformance environment | [Conformance Environment V1](../ConformanceEnvironmentV1.md) |
| Cross-language acceptance | [Cross-Language Conformance V1](../CrossLanguageConformanceV1.md) |

The language does not yet have a complete normative specification. Existing language-oriented material is source material for the structural draft, not a substitute for its eventual complete contract.

## Non-normative guides

[Guide](Guide/README.md) contains tutorials, explanations, and examples intended for learning. A guide may simplify presentation but must link to the owning specification for exact behavior.

## Documentation rules

- Normative documents describe only the current contract. They contain no migration history, superseded architecture, or implementation diary.
- A rule is defined once. Cross-cutting documents use explicit links to the owning document.
- C#, Swift, Kotlin, C++, and Unity integrations may be idiomatic, but their observable behavior must satisfy the same language-neutral contracts.
- Work plans, backlogs, test inventories, generated reports, editor bundles, and historical memory files remain outside this directory.
- Public API names shown in a language mapping are not automatically language-neutral requirements unless the public API specification says so explicitly.
