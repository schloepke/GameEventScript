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
| Optional | `performance` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |
| Optional | `bytecode-snapshot` | ✅ | ↪ | ✅ | ○ | ○ | ○ | ○ |

Required Core support may never be represented by a skipped case. A port may
omit an optional capability during development, but it must then advertise that
omission and skip exactly the affected cases with the stable runner code. The
checked-in C# reference is the initial all-capabilities reference.

Swift was verified on 2026-09-21 (Release, Swift 6.4, macOS arm64) against
94 documents and 1,514 cases with corpus SHA-256
`218C55A3DFA9C5B2521C66DDE0147AEB10F04A362F61A86FFD3F6E9E0B2ED33B`.
The hardware-independent native report passes **1,483 cases**, with **zero errors
or failures** and **31 optional performance measurements skipped**. A full native
run with the calibrated Swift profile passes **all 1,514 cases**, including the
31 measured workloads. All Core capabilities and the
optional GESA snapshots pass. The test adapter also executes those 31 scenarios'
behavior through native compilation, giving **1,514/1,514 behavior checks**.
Compiler coverage includes 156 expected compilation failures, 15 bytecode
constraint cases, three metadata cases, nine source-based GESA snapshots and canonical runtime
segment comparisons against shared binary fixtures. Four additional GESA snapshots
compare dumps of identical binary fixtures, independently of compiler optimization.

Separate Runtime verification passes **1,255/1,255** cases:

| Scope | Passed | Input / oracle |
| --- | ---: | --- |
| Script scenarios | 1,160 | C#-compiled `.gesb`; original Markdown runtime assertions |
| Expected link errors | 4 | C#-compiled `.gesb`; original Markdown diagnostics |
| Binary codecs, validation, fixture execution and fixture GESA snapshots | 51 | Shared canonical/invalid binaries and Markdown |
| GESA dumps | 9 | C#-compiled Programs; shared GESA snapshots |
| Performance-scenario behavior | 31 | Runtime correctness only; no measurement/profile acceptance |

Runtime-only verification covers host dispatch/lifecycle, VM behavior, extensions,
external callbacks, observer traces, limits, and private random state. The
measured Swift profile is `swift-6.4-release-macos26-arm64-m3max`, qualified on
Apple M3 Max/macOS 26.6.2/Swift 6.4 in Release. All sixteen warmed dispatch cases
measure zero cumulative requested bytes and zero allocation events. Text and
payload budgets are profile-specific; C# numbers are not transferred to Swift.
The [performance guide](../../implementation/swift/Performance.md) defines scope
and calibration. The three portable library packages and the optional SwiftBridge
have separate approved public API snapshots.

The generated [performance baseline matrix](../../artifacts/conformance/PerformanceMatrix.md)
shows C# and Swift references per case in separate time and allocation tables,
with normalized units and explicit gaps where a profile has no measurement.
Refresh it from the shared corpus using
`ruby --disable-gems scripts/write-performance-matrix.rb`.

The [Swift verification script](../../scripts/test-swift.sh) emits the
[strict result](../../artifacts/swift/conformance-results/ConformanceResults.json),
[Markdown report](../../artifacts/swift/conformance-results/ConformanceResults.md),
[compact result](../../artifacts/swift/conformance-results/SwiftResults.json), and
separate [Runtime JSON](../../artifacts/swift/conformance-results/RuntimeResults.json)
and [Runtime Markdown](../../artifacts/swift/conformance-results/RuntimeResults.md).
Generated inputs and reports remain below ignored `artifacts`. Swift CI attaches
these reports as its `swift-conformance` artifact. The profile-matched
[measured full report](../../artifacts/swift/measured-conformance/ConformanceResults.json)
and [measurement provenance](../../artifacts/swift/measured-conformance/SwiftPerformance.measurement.json)
are produced separately with `--performance`. The verification script runs
the strict runner without `--allow-incomplete` and requires it to succeed.

The [native Swift CLI](../../implementation/swift/GameEventScriptTool/README.md)
provides `ges compile/check/run/dump` and the interactive event console. CLI
argument/file/terminal adapters are verified separately by
`scripts/test-swift-tool.sh`; they do not introduce additional Core capabilities
or change the shared corpus counts above. C# and Swift CLI binaries can execute
each other's `.gesb` output.

The optional [SwiftBridge](../../implementation/swift/GameEventScriptSwiftBridge/README.md)
adds native Swift adapters on top of the existing Runtime protocols. Its native
tests and Compiler/Runtime integration tests run through `scripts/test-swift-bridge.sh`
and the full Swift CI script. These adapter checks do not add Core capabilities
or alter the shared corpus counts.
