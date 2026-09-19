<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Cross-language capability matrix

This matrix records implementations for the exact shared corpus identified by
their attached result artifacts. A check means the capability passed all cases
that require it. A circle means that no accepted result exists yet. Unity uses
the C# DLL and is not a separate language implementation; its arrow does not
claim that Unity integration testing has already been completed.

| Class | Capability | C#/.NET reference | Unity via C# DLL | Swift | Kotlin | Go | Rust | C++ |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Core | `compiler` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `program-binary` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `host` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `vm` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `message-api` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `value-api` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Core | `external-types` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `native-handlers` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `publish-sink` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `observer` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Optional | `performance` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Optional | `bytecode-snapshot` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |

Required Core support may never be represented by a skipped case. A port may
omit an optional capability during development, but it must then advertise that
omission and skip exactly the affected cases with the stable runner code. The
checked-in C# reference is the initial all-capabilities reference.

Swift's initial API foundation was verified on 2026-09-19 against the 89-document,
1,460-case corpus with SHA-256
`1CF33A20AADED8EA2BE3837B18D6544C1D29A66D7EE8D2D81F49C247B6B417BA`.
All 69 cases requiring `message-api` and all 15 requiring `value-api` passed.
The remaining 1,376 cases report missing Core capability errors. This does not
constitute complete-port acceptance.

The [Swift verification script](../../scripts/test-swift.sh) emits the
[full result](../../artifacts/swift/conformance-results/ConformanceResults.json),
[Markdown report](../../artifacts/swift/conformance-results/ConformanceResults.md),
and [compact result](../../artifacts/swift/conformance-results/SwiftResults.json).
These generated files stay under ignored `artifacts`; the Swift CI workflow
attaches the same files as its `swift-conformance` artifact.
