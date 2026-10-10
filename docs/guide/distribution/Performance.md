<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Performance and allocation release checks

Release 0.4.0 uses the existing checked-in performance budgets. Verification
never recalibrates or relaxes them. These are implementation regression checks,
not cross-language speed comparisons or promises for all supported platforms.
The initial 0.4.0 findings are [resolved and documented](../../development/Release040Performance.md).
Targeted allocation fixes and reviewed reference updates pass both measured gates;
each final release candidate still requires its own clean revision-bound evidence.

## Existing gates

| Gate | Scope | Where it runs |
| --- | --- | --- |
| C# allocation tests | Native allocation assertions and shared Markdown allocation workloads; zero budgets remain exactly zero | Ordinary C# CI, Release, `TestCategory=Allocation` |
| C# measured performance | Shared elapsed-time workloads (`csharp-dotnet-release-macos-arm64`), shared managed allocation profile (`csharp-dotnet-release-managed`), native performance/allocation tests | Manual C# Performance Gate, macOS ARM64 self-hosted runner; .NET SDK 10.0.201 and .NET 8 runtime |
| Swift measured performance | Shared workloads, correctness and GESA checks; elapsed time, requested allocation bytes/counts and instrumentation controls | Separate local Release run on the qualified `swift-6.4-release-macos26-arm64-m3max` host |

Swift's ordinary CI deliberately does not measure timing on generic hosted
runners. Its detailed instrumentation scope, exact machine/toolchain identity,
measurement statistics and reviewed baseline procedure are documented in the
[Swift performance guide](../../../implementation/swift/Performance.md).
The C# timing profile has a broader platform identity; preserve exact runtime,
OS and CPU details with results rather than inferring comparability from its
profile name alone. Neither profile defines a requirement for other machines.

## Collect release evidence

On the qualified macOS ARM64 measurement host, with no concurrent builds or
benchmarks, run:

```sh
python3 scripts/test-release-performance-controls.py
python3 scripts/test-release-performance.py
```

The wrapper executes the existing C# gate and then the existing Swift gate,
including their Release builds. `--language csharp` or `--language swift`
selects just one implementation. The Swift adapter still enforces its exact
hardware/toolchain profile. No calibration mode is exposed by this wrapper.
The wrapper stops on failure; run the other language separately to collect its
independent findings.

Each invocation creates a fresh `artifacts/release-performance/run-*` directory
containing logs, result reports and `evidence.json`. The receipt includes the
Git revision, working-tree status, a digest of tracked and non-ignored untracked
source files, toolchain identity, timestamps, measured workload counts and
SHA-256 hashes of the collected files. It fails if source changes during the
run. C# reports must be newly written; native TRX results must contain executed
passing tests. Both implementations must have complete successful report
summaries and actual passing measurements. Skips, empty measurements and
failures are not release evidence. Failed runs retain their logs and failed
receipt; a process killed before writing a receipt is also not a pass.

The manual [C# Performance Gate](../../../.github/workflows/csharp-performance.yml)
uses this wrapper and uploads its evidence. Local Swift evidence must be
retained with the release validation records. A full two-language run is a
convenience, not a requirement to execute both on a CI runner. Lightweight
negative controls run in ordinary C# CI; no additional work is added to Swift
PR CI.

For release approval, require successful evidence for **both implementations**
from the exact final candidate with a clean working tree, plus the ordinary C#
allocation CI gate. Dirty-tree receipts are useful during development but do
not qualify a release. Compare toolchain and workload identities when reviewing
regressions. Investigate failures; do not turn a failed measurement into a pass
by retrying until it happens to fit or automatically updating a baseline.
A justified baseline refresh requires separate reviewed changes and new
verification. A missing matching measurement host leaves this release check
open; it does not authorize skipping it.

Use the [platform qualification checklist](Platforms.md#release-qualification)
for the remaining release evidence. All reports are generated below ignored
`artifacts`; they must not enter the source packages.
