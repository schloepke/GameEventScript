<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Test hierarchy

The test sources are covered by the Game Event Script
[Apache License 2.0](../../../LICENSE) and its
[licensing policy](../../../LICENSING.md).

Tests live beside their owning modules in `../GameEventScript.*/tests`:
Runtime (API, values, Host/VM and binary codecs), Compiler (including optimizer),
CSharpBridge, Tool and Syntax each have a separate MSTest project.
`GameEventScript.Repository.Tests` here owns cross-module API, licensing,
documentation and distribution checks. `TestSupport` contains linked helpers
and `Tests.props` shares only test configuration; neither owns test cases.

The shared `../../../conformance` corpus contains portable Markdown suites,
parser fixtures, binary resources and cross-language references. The
independent [Python runner](../../../conformance/runner/README.md) executes it via
`../GameEventScript.Conformance/adapter`, without a C# parser/runner library.
The adapter's `tests` directory qualifies implementation instrumentation and
protocol handling. Shared `fixtures` contain native callback/type bindings used
by the adapter and Runtime fixture exporter. Compiler bounded-stack and process
failure checks live in `../GameEventScript.Compiler/tests` and its `worker`.
Shared parser
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

The native `Allocation` category retains the independent VM hot-path gate,
Reader budgets, arithmetic/pipeline checks and escaping-allocation controls.
Run it with `--configuration Release --filter "TestCategory=Allocation"`.
Shared corpus allocation cases run separately through
`sh scripts/test-csharp-conformance.sh`, using the C# adapter's managed profile.
See the [adapter measurement documentation](../GameEventScript.Conformance/adapter/README.md)
for exact scope, aggregation, instrumentation controls and hardware requirements.

`scripts/test-csharp-performance.sh` runs native measurement tests serially,
then the Python runner with both C# profiles. Reports below
`artifacts/conformance/python` contain raw measurement evidence. No reference
values are echoed or automatically updated.

Binary corpus cases execute through the adapter on a requested 1 MiB native
stack, including shallow/deep call-graph controls. The independent runner
bounds process time and output, verifies response identity and fails on crashes.
Compiler source-nesting probes retain their separate worker under
`artifacts/csharp/compiler-worker`, with timeout/output/termination controls in
`CompilerProcessIsolationTests`. Run these native controls using
`--filter "TestCategory=Isolation|FullyQualifiedName~CompilerProcessIsolationTests"`.
