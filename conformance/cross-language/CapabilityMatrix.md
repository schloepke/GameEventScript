<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Cross-language capability matrix

This matrix records implementations for the exact shared corpus identified by
their attached result artifacts. A check means the capability passed all cases
that require it. A circle means that no accepted result exists yet. Unity uses
the C# DLL and is not a separate language implementation; its arrow does not
claim that Unity integration testing has already been completed. The Swift column uses strict native compilation and execution results; separate
Runtime checks also verify interoperability with C# compiler output.

| Class | Capability | C#/.NET reference | Unity via C# DLL | Swift | Kotlin | Go | Rust | C++ |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Core | `compiler` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `program-binary` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `host` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `vm` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `message-api` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `value-api` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `external-types` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `native-handlers` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `publish-sink` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `observer` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Optional | `performance` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Optional | `bytecode-snapshot` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |

Required Core support may never be represented by a skipped case. A port may
omit an optional capability during development, but it must then advertise that
omission and skip exactly the affected cases with the stable runner code. The
checked-in C# reference is the initial all-capabilities reference.

Swift was verified on 2026-09-19 (Release, Swift 6.4, macOS arm64) against
89 documents and 1,460 cases with corpus SHA-256
`1CF33A20AADED8EA2BE3837B18D6544C1D29A66D7EE8D2D81F49C247B6B417BA`.
The strict native report passes **1,429 cases**, with **zero errors or failures**
and **31 optional performance cases skipped**. All Core capabilities and the
optional GESA snapshots pass. The test adapter also executes those 31 scenarios'
behavior through native compilation, giving **1,460/1,460 behavior checks**.
Compiler coverage includes 131 expected compilation failures, 15 bytecode
constraint cases, two metadata cases, eight GESA snapshots and canonical runtime
segment comparisons against shared binary fixtures.

Separate Runtime verification passes **1,227/1,227** cases:

| Scope | Passed | Input / oracle |
| --- | ---: | --- |
| Script scenarios | 1,137 | C#-compiled `.gesb`; original Markdown runtime assertions |
| Expected link errors | 4 | C#-compiled `.gesb`; original Markdown diagnostics |
| Binary codecs, validation, and fixture execution | 47 | Shared canonical/invalid binaries and Markdown |
| GESA dumps | 8 | C#-compiled Programs; shared GESA snapshots |
| Performance-scenario behavior | 31 | Runtime correctness only; no measurement/profile acceptance |

Runtime-only verification covers host dispatch/lifecycle, VM behavior, extensions,
external callbacks, observer traces, limits, and private random state. The
calibrated Swift performance profile remains separate work; C# timing/allocation
numbers are not transferred to Swift. All three packages' public APIs are checked
against approved symbol snapshots.

The [Swift verification script](../../scripts/test-swift.sh) emits the
[strict result](../../artifacts/swift/conformance-results/ConformanceResults.json),
[Markdown report](../../artifacts/swift/conformance-results/ConformanceResults.md),
[compact result](../../artifacts/swift/conformance-results/SwiftResults.json), and
separate [Runtime JSON](../../artifacts/swift/conformance-results/RuntimeResults.json)
and [Runtime Markdown](../../artifacts/swift/conformance-results/RuntimeResults.md).
Generated inputs and reports remain below ignored `artifacts`. Swift CI attaches
these reports as its `swift-conformance` artifact. The verification script runs
the strict runner without `--allow-incomplete` and requires it to succeed.
