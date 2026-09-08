<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Test hierarchy

The test sources are covered by the Game Event Script
[Apache License 2.0](../../../../LICENSE) and its
[licensing policy](../../../../LICENSING.md).

The test project has two semantic roots:

- `../../../../conformance` contains the portable Markdown suites, parser
  fixtures, binary resources, and cross-language references. `Conformance`
  contains the C# conformance runner, parser, report and test-framework
  adapters, so IDE test trees expose them directly as `Conformance` rather than
  as native implementation tests. Shared parser bootstrap inputs are
  integrity-protected by `conformance/fixtures/MarkdownV1/manifest.tsv`.
- `Native` contains the remaining C#-specific tests, grouped by purpose:
  `Api`, `ApiSurface`, `BinaryFormat`, `Compiler`, `Core`, `CSharpBridge`, and
  `Runtime`.

Portable public behavior is added to Markdown first. A native test remains only
for a language adapter, implementation detail, performance/allocation property,
or bootstrap behavior that cannot use the corpus as its sole oracle. The
portable coverage map is maintained in
[Conformance Coverage](../../../../specs/Conformance/Coverage.md).

The `Allocation` category includes the shared `performance.low-allocation`
workloads through a C# measurement adapter, its escaping-allocation/reuse
controls, the independent VM hot-path gate, and native Reader budgets. Run it
with `--configuration Release --filter "TestCategory=Allocation"`.

The shared dispatch cases select `csharp-dotnet-release-managed`, a zero-byte
managed-heap check usable by the C# CI on different operating systems. It uses
`GC.GetAllocatedBytesForCurrentThread` around synchronous Receive and pumping
of reused messages. Each of three fresh hosts receives the declared warmup and
one additional preflight iteration whose work/callback counts are checked
against the measured batch. Compilation, linking, initialization, inputs,
instrumentation, preflight, collections and assertions are outside the counter.
The maximum sample is compared without rounding or tolerance. Reports under
`artifacts/conformance/received/performance` include a `.measurement.json`
manifest with runtime/build/GC details, raw samples, work counts and the explicit
absence of native-heap coverage. Existing elapsed-time benchmarks continue to
use their macOS ARM64 profile and remain outside the `Allocation` category.

The ordinary Markdown corpus uses an echo provider for performance expectations
and verifies correctness and report structure only. Those echoed numbers are
not measurements and cannot qualify any allocation profile. Generated received
references are never approved automatically.

The `Isolation` category executes all four `program.call-graph-depth` Markdown
cases, including both shallow controls, through the separate
`StepH.GameEventScript.Conformance.Worker` executable. The ordinary
non-performance corpus includes these cases exactly once through that path.
They are selected by stable suite ID, never retried in-process or treated as
expected crashes. `ConformanceProcessIsolationTests` checks discovery, process
termination, timeout, bounded stdout/stderr, and result validation.

The worker is built by the test-project reference and stays below
`artifacts/csharp/conformance-worker`. It parses a snapshot of the same Markdown
and invokes the portable runner for one binary validation/rewrite case without
runtime steps or compiler comparison. The case executes on a thread with a
requested 1 MiB stack to keep native-stack conditions explicit across C# hosts.
This stack size is a test-adapter configuration, not a portable language limit.
The parent allows 30 seconds, retains at most 64 KiB each of stdout and stderr,
kills a timed-out or overflowing process tree, and bounds cleanup to five
seconds. Automatic .NET minidumps are disabled for the worker.

One JSON response carries protocol version, full case ID, source-document and
fixture SHA-256, status, code, structured mismatches and optional technical
details. It is accepted only after exit code zero, complete bounded output,
identity validation and consistent status/assertions. Other exits and invalid
responses produce failed Conformance results at `/execution`. Per-invocation
Markdown snapshots, native execution metadata and bounded outputs are retained
under `artifacts/conformance/received/isolated`. The original shared reference
results are never replaced automatically. Run the cases and infrastructure with
`--filter "TestCategory=Isolation|FullyQualifiedName~ConformanceProcessIsolationTests"`.
