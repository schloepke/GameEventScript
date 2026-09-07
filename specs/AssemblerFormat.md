<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# `.gesa` assembler format specification

This document defines the current human-readable dump produced from a validated `GameEventScriptProgram`. It is normative for bytecode snapshot text, syntax highlighting, and diagnostic presentation.

`.gesa` is not an executable transport format and has no reader in the portable API. Program transport uses [`.gesb`](BinaryFormat.md); instruction meaning and operand roles are defined by the [bytecode specification](Bytecode.md).

## Text model

- Output is Unicode text.
- The C# dumper uses the platform line ending. Conformance comparison normalizes `CRLF` and `CR` to `LF`; checked-in snapshots therefore use `LF`.
- The dump ends with a line terminator.
- Indentation and alignment tabs are presentation details emitted canonically by a specific dumper implementation. Semantic tokens, ordering, labels, operands, and comments are portable.
- A dump is deterministic for the same validated Program, dump options, and dumper version.
- Changes to presentation may require bytecode-snapshot approval updates but never change `.gesb` compatibility or runtime behavior.

## Document order

A complete dump has this order:

1. descriptive header comments;
2. `.gesb`, `.module`, and `.program-version` directives;
3. zero or more embedded Source segments in SourceArchive order;
4. the Text segment;
5. the Lists segment;
6. the Bindings segment;
7. the Code segment.

The required top-level directives are:

```gesa
.gesb 1
.module "Example"
.program-version 0
```

`.gesb` is the Program format version, `.module` is the escaped module name, and `.program-version` is the unsigned application-defined Program version. These directives describe the Program; they do not declare a separate assembler binary format.

## Comments, regions, and escaping

`//` begins an explanatory comment outside embedded Source content. The standard region separator is:

```gesa
// -------------------------------------------------------------------------------
```

Every generated presentation region is balanced and separated by blank lines:

```gesa
// -------------------------------------------------------------------------------
.region "Text"

...

.region-end "Text"
// -------------------------------------------------------------------------------
```

Region names are identical at both boundaries. Regions are editor-folding and readability metadata; they do not alter Program meaning.

Quoted dumper strings escape `\` as `\\` and `"` as `\"`. No other escape sequence is introduced by the dumper.

## Source segments

For each SourceArchive entry the dumper emits:

```gesa
.region "Source: example.ges"

.segment source "example.ges"

module example

on Start() {
  emit Done()
}

.region-end "Source: example.ges"
```

The content after the blank line following `.segment source` is the exact logical compiler input, except that `CRLF` and `CR` are normalized to `LF` and a missing final line terminator is added. Source content is presentation data and is not parsed as assembler directives; consequently directive-like Source lines have no structural meaning.

Source segments are absent when the Program has no SourceArchive. SourceMap and DebugSymbols do not require SourceArchive and remain useful independently.

## Text segment

The Text segment lists every StringConstant entry in Program order:

```gesa
.segment text

T_value:            .text "value"
T_value_2:          .text "value"
T_2:                .text ""
```

Labels are diagnostic aliases, not serialized Program data. A label begins with `T_`, derives from an ASCII-safe form of the text when possible, and receives a numeric suffix when needed for uniqueness. Duplicate strings remain separate entries and therefore receive separate labels.

## Lists segment

Every UInt16IndexList entry appears in Program order. Its observed operand role selects one of three views:

```gesa
.segment lists

U16_0:              .u16 [0, 3]
Names_1:            .texts [T_left, T_right] // "left", "right"
Args_2:             .registers [r0, r1]
```

- `.u16` is the raw unsigned list view.
- `.texts` resolves entries as StringConstant labels.
- `.registers` renders entries as physical register identifiers.

Role-specific prefixes include `Shape`, `Names`, `Keys`, `Args`, `Items`, `Values`, `Captures`, and `Tags`. If the same list is observed in more than one role, the first non-raw role discovered from code determines its presentation.

## Bindings segment

Every Binding entry appears in Program order:

```gesa
.segment bind

Handler_Start:      .bind MessageHandler id=0 name=T_Start args=[T_value] entry=Start // "Start(value)"
Outbound_Done:      .bind OutboundMessage id=0 name=T_Done args=[T_result] // "Done(result)"
```

The syntax is:

```text
label: .bind Kind id=(unsigned|none) name=TextLabel args=[TextLabel, ...]
       [entry=CodeLabel] [requiredTags=[TextLabel, ...]] [excludedTags=[TextLabel, ...]]
```

The `entry` field is present only when the bind has an entry address. Tag fields are emitted only when non-empty. The trailing comment resolves the logical signature and tag constraints.

Bind-label prefixes are `Handler`, `Function`, `Predicate`, `Extension`, `Outbound`, `Record`, and `ExternalType`. Unrecognized kinds use `Bind`. Labels derive from the binding name where possible and are made unique by numeric suffixes.

## Code segment

The Code segment contains one linear global instruction address space:

```gesa
.segment code

.source-line "example.ges" 3 | on Start(value) {
Start:               // handler Start(value)
                    RegisterLocals #2
                    Add r1(total), r0(value), r2
                    EmitMessage Outbound_Done, Args_2 // "Done(result)"
                    ReturnVoid
```

Instructions are emitted in ascending address order. Named entry labels derive from handler, function, predicate, or record binds. Other referenced targets use an owner-scoped label where possible and otherwise `L_<address>`. Labels are presentation aliases and never replace the numeric addresses in the Program.

When `includeInstructionAddresses` is enabled, every instruction line begins with an address such as `@0000`. Addresses are zero-based instruction indices formatted with at least four decimal digits. The default dump omits these prefixes but retains labels for all referenced targets.

## Source-line annotations

When a SourceMap covers an instruction, the first instruction associated with a different source line is preceded by a blank line and:

```gesa
.source-line "example.ges" 7 |   let total be left + right
```

The line number is one-based. If SourceArchive is present, the text after `|` is the corresponding normalized Source line. Without SourceArchive, the directive ends after `|`. Consecutive instructions mapped to the same source line do not repeat the directive.

An unmapped run is preceded once by:

```gesa
// compiler-generated
```

Source-line annotations are derived metadata and do not affect instruction execution.

## Instruction and operand syntax

An instruction consists of the opcode name followed by its operands in the order defined by [the canonical opcode field map](Bytecode.md#canonical-opcode-field-map). Operands are separated by `, `.

| Operand kind | Form | Example |
| --- | --- | --- |
| Register | `r` followed by an unsigned ID | `r12` |
| Symbolic register | physical register plus active DebugSymbol | `r12(total)` |
| Signed or floating immediate | `#` plus invariant numeric text | `#-1`, `#0.5` |
| Code target | generated Code label | `Start_12` |
| Text, list, or bind reference | generated label | `T_name`, `Args_2`, `Outbound_Done` |
| Unit | `unit:` plus the portable unit type name | `unit:meter` |
| Enum operand | portable enum member name | `Fibonacci` |

Binary64 immediates use invariant round-trip formatting. The register form always retains the physical register ID. When DebugSymbols contain more than one live symbol for a register and address, the matching symbol with the shortest code range wins.

Non-zero instruction flags follow all normal operands:

```gesa
Call r2(result), function_score flags=NormalizeResultAsPredicate
```

Reference-resolving comments may follow an instruction. They show string contents, message signatures, record or external references, and named callable entries without changing the operand list.

## Canonicality and scope

- Text, list, binding, and instruction order always follows Program order.
- Source order follows SourceArchive order.
- Generated labels are unique within their label domain and deterministic.
- The same Program dumped with and without instruction addresses differs only by address prefixes and the layout needed to retain target labels.
- DebugSymbols, SourceMap, and SourceArchive enrich a dump but are never required to execute the Program.
- VM state is not part of `.gesa`; a VM-state diagnostic may embed a Program dump alongside separate execution-state information.
- `.gesa` does not promise parse/write roundtripping, binary identity, or a stable wire protocol.
