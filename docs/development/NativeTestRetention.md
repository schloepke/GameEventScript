<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Native Test Retention

This inventory is the boundary between portable Markdown Conformance and native
C# verification. Portable public behavior belongs under
`conformance`; C# test adapters belong under
`implementation/csharp/tests/StepH.GameEventScript.Tests/Conformance`, and native C# tests belong under
`implementation/csharp/tests/StepH.GameEventScript.Tests/Native`.

There are 105 C# test methods. The Markdown corpus contains 1,040 cases in 74
suites: 1,033 semantic cases and 7 bytecode snapshots. Of the C# methods, 41
bootstrap the Conformance infrastructure from the `Conformance` root and 64
test deliberately native implementation concerns below `Native`.

| Retention class | Methods | Native subtree | Why they remain native |
| --- | ---: | --- | --- |
| Conformance bootstrap | 41 | `Conformance` | The Markdown parser, schema binder, bounded resource resolver, runner adapter, result writers and generated-report integration cannot use the corpus as their only oracle. They are colocated with the corpus so IDEs display them as Conformance tests. |
| `.gesb` and Program implementation | 17 | `Native/BinaryFormat` | Low-level writer framing, reader-limit plumbing, retention-policy branches, reserved security behavior, compiler identity experiments, defensive CLR arrays and concrete instruction layout remain direct implementation tests. Portable fixture reading, validation, canonical rewriting and execution are in `program.binary-format`. |
| Compiler and optimizer internals | 14 | `Native/Compiler`, `Native/Core/GesOptimizerTests.cs` | These inspect internal plans, rewrites, physical registers, source ranges and invalid graphs that valid source cannot construct. Observable compiler output and resource/cycle behavior is in Markdown. |
| Native Core/API bootstrap | 6 | `Native/Api`, `Native/Core/GameEventScriptNumberTests.cs` | These verify C# exception wrappers, host-owned random state and markers, the canonical formatter and the ULP comparison helper used by the C# runner. |
| C# bridge, runtime, repository contracts and allocation | 27 | `Native/CSharpBridge`, `Native/Runtime`, `Native/ApiSurface` | Reflection/attributes, dictionary adapters, API/XML/documentation/licensing snapshots, auto-runner/threading, VM/context identity and allocation measurement are deliberately platform-, repository- or implementation-specific. |

The portable public Value API, Message/Signature/Handler equality, Host handle
results, external catalog duplicate behavior, compact bindings, message-name
bindings and source-aware assembler dumps are no longer retained as C# tests;
their stable Markdown IDs are listed in the normative
[Conformance Coverage](../../specs/Conformance/Coverage.md).

The retained count is guarded by review rather than a hard-coded build rule.
When a retained test becomes expressible as stable portable behavior, add the
Markdown case and its coverage ID first, remove or narrow the native test in the
same change, and update this inventory.
