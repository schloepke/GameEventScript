# Native Test Retention

This inventory is the boundary between portable Markdown Conformance and native
C# verification. Portable public behavior belongs under
`StepH-GameEventScript-Tests/Conformance`; C# test code belongs under
`StepH-GameEventScript-Tests/Native`.

There are 91 C# test methods. The Markdown corpus contains 1,016 cases in 73
suites: 1,009 semantic cases and 7 bytecode snapshots. Of the C# methods, 36
bootstrap the Conformance infrastructure from the `Conformance` root and 55
test deliberately native implementation concerns below `Native`.

| Retention class | Methods | Native subtree | Why they remain native |
| --- | ---: | --- | --- |
| Conformance bootstrap | 36 | `Conformance` | The Markdown parser, schema binder, runner adapter, result writers and generated-report integration cannot use the corpus as their only oracle. They are colocated with the corpus so IDEs display them as Conformance tests. |
| `.gesb` and Program implementation | 24 | `Native/BinaryFormat` | Raw framing, corrupt in-memory graphs, retention buffers, defensive CLR arrays and concrete instruction layout remain direct implementation tests. Point 5.9 must replace applicable format validation with shared binary fixtures and re-audit this group. |
| Compiler and optimizer internals | 14 | `Native/Compiler`, `Native/Core/GesOptimizerTests.cs` | These inspect internal plans, rewrites, physical registers, source ranges and invalid graphs that valid source cannot construct. Observable compiler output and resource/cycle behavior is in Markdown. |
| Native Core/API bootstrap | 4 | `Native/Api`, `Native/Core/GameEventScriptNumberTests.cs` | These verify C# exception wrappers, the C# seed overload, the canonical formatter and the ULP comparison helper used by the C# runner. |
| C# bridge, runtime structure and allocation | 13 | `Native/CSharpBridge`, `Native/Runtime`, `Native/ApiSurface` | Reflection/attributes, dictionary adapters, C# API snapshots, auto-runner/threading, VM/context object identity and allocation measurement are deliberately platform- or implementation-specific. |

The portable public Value API, Message/Signature/Handler equality, Host handle
results, external catalog duplicate behavior, compact bindings, message-name
bindings and source-aware assembler dumps are no longer retained as C# tests;
their stable Markdown IDs are listed in `ConformanceCoverage.md`.

The retained count is guarded by review rather than a hard-coded build rule,
because 5.9 is expected to reduce the binary-format group. When a retained test
becomes expressible as stable portable behavior, add the Markdown case and its
coverage ID first, remove or narrow the native test in the same change, and
update this inventory.
