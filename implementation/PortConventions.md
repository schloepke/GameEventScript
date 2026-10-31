<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# C# and Swift port conventions

These conventions keep the C# and Swift ports structurally aligned. `AGENTS.md`
summarizes them; `python3 scripts/verify-port-names.py` enforces the prefix,
abbreviation, parity and opcode-handler rules against
`scripts/port-names-baseline.txt`.

## Principle

Ports provide the same functionality, concepts and structure, so a developer who
knows one port finds their way in another. Each port nevertheless follows its
language's API design guidelines and should feel as native as possible, because
the usual case is one developer using one port in their own language. Idioms may
change spelling and mechanics (factories, errors, builders, literals), never the
primary concepts: every port works with the same edge types and roles.

## Prefixes

- `GameEventScript` is reserved for the three API edge types, the core nouns of
  the model (a Program runs in a Host and receives Messages):
  `GameEventScriptHost`, `GameEventScriptProgram` and `GameEventScriptMessage`.
- Edge types bundle their own input/output: Program reading/writing and Message
  JSON encoding/decoding are members of the edge type, not separate types.
- Every other library type, public or internal, uses `Ges`. Bridge types use
  `GesBridge`.
- Exempt: nested types, `private`/`fileprivate` types, executables (CLI,
  Conformance adapters) and tests, and technically required types such as
  C# `IsExternalInit`.
- Bridge attributes and macros keep their short names: `[GesType]`/`@GesType`,
  `[GesField]`/`@GesField`, `[GesConstruct]`/`@GesConstruct`, and so on. They
  mark user types and are not Bridge objects.
- Abbreviations are written as words: `Ges`, `Vm`, `Api`; never `GES`, `VM`, `API`.
- No type may share its name with a Swift module (for example
  `GameEventScriptCompiler`, `GameEventScriptSyntax`); such a type
  breaks module qualification.

## Identical names, idiomatic spelling

Corresponding types and members use identical names in both ports. C# files are
named after their main type or with a name that summarizes their content; partial
class files append an aspect (`GesBinaryBuilderOpcodes.cs`). Swift file names are free. Only these language-idiomatic mappings may differ:

| Concept | C# | Swift | Later Java/Kotlin |
|---|---|---|---|
| Error type | `GesXException` | `GesXError` | `GesXException` |
| Platform argument/state errors | BCL `ArgumentException`, `InvalidOperationException` | `GesApiError` | platform exceptions |
| Interface | `IGesX` | `GesX` (protocol) | `GesX` |
| Direct creation of data types | constructor or `Create(...)` | throwing or plain `init` | constructor |
| Value creation | `GesValue.GesInteger(5)`; with `using static`, `GesInteger(5)` | `GesValue.integer(5)`; in context, `.integer(5)` | `GesValue.integer(5)`; with static import, `integer(5)` |
| Builder creation | `new GesXBuilder()` | `GesXBuilder()` | constructor |
| Builder convenience on target type | `GameEventScriptHost.CreateBuilder()` | `GameEventScriptHost.makeBuilder()` | `newBuilder()` |
| Builder completion | `Build()` | `build()` | `build()` |
| Other factory methods | `CreateX(...)` | `makeX(...)` | `newX(...)` / `createX(...)` |
| Immutable list exposure | `GesReadOnlyArray<T>` / read-only spans | `[T]` | read-only list |
| Cross-package internals | `internal` + `InternalsVisibleTo` | `@_spi(Compiler)` / `@_spi(Bridge)` public | module-internal |
| Type-erased wrapper | object or interface | `GesAnyX` | interface |

An error type may be absent from a port that cannot produce that error.

C# value factories keep the `Ges` member prefix because C# methods and types share
PascalCase: after `using static`, `Integer(5)`, `List(...)` or `Range(...)` would
read like type names and collide with `List<T>` and `System.Range`. Swift and
Java/Kotlin factories are lower-case and resolve through the expected type or
the static import, so they omit the redundant prefix.

## Creation

- Root objects (`GameEventScriptHost`, `GameEventScriptProgram`) are created
  through builders. Their own constructors/initializers are internal.
- `GameEventScriptMessage` uses the idiomatic constructor/initializer and JSON
  members (C# `GameEventScriptMessage.FromJson(...)`/`message.ToJson()`, Swift
  `GameEventScriptMessage(json:)`/`message.json()`).
- Value literal parsing follows the same pattern: C# `GesValue.ParseLiteral(...)`
  and `GesValue.ParseLiteralStrict(...)`, Swift `GesValue(literal:)` and the
  failable `GesValue(strictLiteral:)`.
- GES documents read and print the same way: C# `GesDocument.Parse(...)`,
  `document.Print(...)` and `value.Format(...)`, Swift `GesDocument(parsing:)`,
  `document.text(format:)` and `value.formatted(_:)`. Swift additionally offers
  the `GesDocumentSchema.type(_:)`/`.file(_:)` factories for the C# constructor
  `new GesDocumentSchema(kind, name)`.
- Builders are directly creatable. A builder accessor on the target type is only
  a convenience.
- `GameEventScriptProgram.CreateBuilder()` is a C# 14 static extension member in
  the Compiler package. The Compiler project may use C# 14 while still targeting
  netstandard2.1; the compiler embeds the required marker attribute. C# 9
  consumers such as Unity cannot use the extension syntax and use
  `new GesProgramBuilder()`. Swift provides
  `GameEventScriptProgram.makeBuilder()` as a Compiler-module extension.
- Values are created through static factories on `GesValue`; its constructors are
  internal where the language allows it (the C# struct default value remains
  Nothing).
- Message arguments, signatures, options and limits use the idiomatic
  constructor/initializer.
- No central value factory and no `Host.createMessage`: factories stay on their
  type, and platform conveniences (C# tuples, Swift literals) belong in the
  Bridge or in idiomatic Swift literal conformances.

## Layers

- Core (Runtime, Compiler) is normative, portable and structurally identical in
  both ports: same types, same layers (`Api`, `Program`, `Runtime/Vm`,
  `Runtime/Values`, compiler phases).
- Bridge adapts the Core to the platform (threads, locks, dispatchers,
  Reflection versus KeyPath/macros, delegates versus closures). The same role
  has the same name; platform-only types are allowed.
- Syntax is a separate module with the entry type
  `GesSyntaxHighlighter`.

## Tools and adapters

The CLIs and Conformance adapters are executables and exempt from the type-name
gate, but corresponding concepts keep the C# names in both ports: `RunSession`,
`RunWorkspace` (with `Draft`), `RunInventory` (with `LoadedProgram`),
`RunObserver`, `RunProfiler` (with `Measurement`), `RunPrompt`, `RunKeyReader`,
`RunHighlighting` and `ToolTextDisplay` in the CLIs; `HostScenario`,
`RuntimeObservations`, `RuntimeValues` and `VirtualClock` in the adapters.
Members that write console output use a `Write` prefix (`WritePrograms` /
`writePrograms`). Platform-only types, such as the Swift POSIX terminal keys or
the C# PrettyPrompt history, need no counterpart.

## Performance and size

- Optimize for performance first, then for library/binary size. Hot-path
  duplication is acceptable when it is measurably faster.
- Size reductions are welcome at any time if they do not noticeably reduce
  performance on the affected workloads, compared before and after the change;
  zero-allocation paths stay at zero.
- Inlining attributes (`AggressiveInlining`, `NoInlining`, `@inline(__always)`,
  `@inline(never)`) are added only with a measured benefit and a short rationale
  comment.
- Opcode handlers are `Execute<OpCode>` members of `GesVirtualMachine`, named after
  the `GesBytecodeOpCode` case and callable only from dispatch: C# `private`
  members of the partial class, Swift `internal` members whose calls the port gate
  restricts to the dispatch file and `Opcodes`. Trivial opcodes may stay in
  dispatch; the set need not be identical across ports.
