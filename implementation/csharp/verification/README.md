<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Test hierarchy

The test sources are covered by the Game Event Script
[Apache License 2.0](../../../LICENSE) and its
[licensing policy](../../../LICENSING.md).

Tests live beside their owning modules in `../GameEventScript.*/tests`:
Runtime (API, values, Host/VM and binary codecs), Compiler (including optimizer),
CSharpBridge, Tool and Conformance each have a separate MSTest project.
`GameEventScript.Repository.Tests` here owns cross-module API, licensing,
documentation and distribution checks. `TestSupport` contains linked helpers
and `Tests.props` shares only test configuration; neither owns test cases.

The shared `../../../conformance` corpus contains portable Markdown suites,
parser fixtures, binary resources and cross-language references. The
`../GameEventScript.Conformance/tests` adapter executes it without duplicating
portable cases into module test projects. Its `fixtures` directory contains
native callback/type bindings shared with the Runtime fixture exporter, and
its `worker` directory contains the isolated-process adapter. Shared parser
bootstrap inputs remain integrity-protected by
`conformance/fixtures/MarkdownV1/manifest.tsv`.

Run all six test projects from the repository root:

```bash
dotnet test GameEventScript.sln --configuration Release \
  --filter "TestCategory!=Performance|TestCategory=Allocation"
```

To run one module, use its test project, for example:

```bash
dotnet test implementation/csharp/GameEventScript.Compiler/tests/GameEventScript.Compiler.Tests.csproj
```

`../GameEventScript.Tool/tests` also tests the terminal key adapter directly with simulated input:
Ghostty/xterm and CSI-u modified Enter, fragmented/truncated sequences, Unicode
paste, and preservation of other keys and their modifiers. These are CLI adapter
tests, not portable language Conformance cases.

`../GameEventScript.Tool/tests` exercises the built CLI as a separate process, including file
input/output, argument handling, exit codes, diagnostic rendering, and loading
and executing its `.gesb` output. This includes combined source files, ordered
wildcard expansion, verbosity-independent binary output, GESA file/stdout output,
and decode diagnostics using existing invalid binary fixtures. `check` tests
verify full compilation without execution or file writes. `run` tests verify
source/binary inputs, ordered composition of multiple binaries, Main arguments,
separate scenarios, native ConsoleOut/ConsoleErr/ErrorCode handlers, script exit
codes, opt-in color, and redirected interactive input. Interactive tests include
empty sessions, additive source/binary loading, recovery from rejected loads,
and initialization limits. Argument tests cover `--arg`, `--args`, and `--` with
Text-only semantics, option boundaries, and negative values. Inspection tests
cover `:list`, `:handler`, `:dump`, and `:source`, including combined sources, multiple
binaries, native subscriptions, tag filters, anonymous/duplicate modules,
failed loads, and exclusion of temporary inputs. Source inspection covers archived
documents without their original files, missing archives, and GES/GESA coloring
with exact text preservation and `NO_COLOR`. Seed options, runtime-limit
termination, diagnostics, and exit codes are also covered. These are
CLI process adapters around the existing portable behavior, whose oracle remains
the shared Markdown corpus. The test project builds the tool automatically;
these tests require no global installation. Temporary files stay below
`artifacts/csharp/tests` and are removed after each test.

`GameEventScript.Repository.Tests/ApiSurface` checks all four library boundaries. The separate
`GameEventScript.RuntimeConsumer` application references only Runtime and
CSharpBridge and executes compiler-produced `.gesb` fixtures with and without
debug metadata, including runtime literal parsing. It rejects Compiler and
Conformance in its output directory, dependency manifest, and loaded assemblies.
The release smoke checks reuse it with NuGet packages and staged DLLs.

Portable public behavior is added to Markdown first. A native test remains only
for a language adapter, implementation detail, performance/allocation property,
or bootstrap behavior that cannot use the corpus as its sole oracle. The
portable coverage map is maintained in
[Conformance Coverage](../../../specs/Conformance/Coverage.md).

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
`scripts/test-csharp-performance.sh` runs test projects serially so separate
assemblies do not compete with elapsed-time measurements.

The ordinary Markdown corpus uses an echo provider for performance expectations
and verifies correctness and report structure only. Those echoed numbers are
not measurements and cannot qualify any allocation profile. Generated received
references are never approved automatically.

The `Isolation` category executes all four `program.call-graph-depth` Markdown
cases, including both shallow controls, through the separate
`GameEventScript.Conformance.Worker` executable. The ordinary
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
