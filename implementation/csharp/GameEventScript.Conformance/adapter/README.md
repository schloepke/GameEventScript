<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Internal C# Conformance Adapter

Small executable test harness, not a distributable module. It references only
Compiler and Runtime, not the GES CLI, Bridge or Conformance parser/runner.
`IsPackable` and `IsPublishable` are false. It is not included in public package
or CLI release manifests. Outputs live under `artifacts/csharp/conformance-adapter`.

Correctness scope: `bytecodeSnapshot`, `compileError`, `bytecode`,
`compileMetadata`, `programBinary`, `loadError`, `externalTypeApi`, `valueApi`, `messageApi` cases, and single- and multi-Program `scriptApi`
scenarios and native-only Hosts supported by Python preflight. Compiler options include
ordered sources with their exact logical names, debug flags and binary
roundtrip. Every compilation uses a new compiler. No GES source names are opened as
filesystem paths. No Markdown, expectations or assertion logic reach the adapter.
Binary fixtures arrive as bounded Base64 bytes. The adapter returns reader error
context, metadata, canonical writer bytes and optionally dumps or required runtime
sections. It never reads fixture paths or compares expected values.
`--describe` advertises `supportedBinarySteps: true`.
Execution reuses the compiled Program across `hostCount` independent hosts (1–256),
returning a separate observation for each. Each execution uses a fresh host, a virtual clock at zero, an accepting publication
sink, and a small typed-value codec. Observed values are encoded outside callbacks.
Value transport preserves Int64/Binary64 storage, quantity/spatial units, Range
components, nested messages and record type names. Comparison
tolerances stay exclusively in Python. The closed protocol rejects unsupported
runtime options and value kinds. Shared
test extension effects live in `RuntimeFixtures.cs` and contain no assertions.
External-type fixtures are source-linked from the existing fixture directory;
this does not introduce a dependency on the old Conformance runner. All runtime
limit overrides and `completion`, `enqueue`, `frame`, and `frames` pumping are
supported. Random inputs use the host seed, sequence, or entropy API directly;
sequence fallback and omitted configuration use seed zero.
Native handlers are defined by closed JSON fixture inputs, registered after the
Program, and can emit ordered messages or raise declared/unexpected faults. No
assertions execute in those callbacks. Load/detach and subscribe/unsubscribe actions retain real instance/subscription
handles and emit raw result observations. Programs are compiled once in source
group order; deferred Programs are loaded only by an explicit action. Virtual time
advances through explicit actions with checked Int64 arithmetic. Each phase records
readiness, Program start states and idle duration. Initialization can stop after
Start or drain ordinary work on success. Observer callbacks are retained in order
with message values serialized after callbacks return. Publish sinks support
acceptance, rejection, deliberate exceptions and absence; all actual sink calls
remain visible in the outbound channel.
Value API observations use real constructors, flags, equality and hash codes.
Ordered Map/Record inputs retain duplicates; the transport does not normalize
them first. Defensive-copy checks mutate the original source arrays after value
construction. No expected values reach these operations.
Message API operations preserve raw labels/tags and invoke public signature,
message and Handler APIs. Construction error classification does not inspect
English exception text. Successful observations also include the actual constructed message; Python owns
conformance-message comparisons and their numeric options. Product JSON input stays opaque until the real codec reads it; `roundTripJson`
uses that codec in both directions. JSON operations return canonical JSON instead
of a typed normalized observation by default; `includeNormalized` additionally
returns the typed value for an independent comparison. Format exceptions report their stable code.
No product JSON parser or writer is implemented in Python.
All currently authored kinds are covered, including the measured modes below.
The legacy C# and Swift runners have been removed; both implementations use
this observation protocol with the independent Python runner. Binary fixture observations execute on a requested
1 MiB native stack, preserving the previous deep call-graph guard. The Python
process boundary detects crashes, bounds output and enforces deadlines.

Build with:

```sh
dotnet build implementation/csharp/GameEventScript.Conformance/adapter/GameEventScript.ConformanceAdapter.csproj -c Release
```

Invoke the resulting DLL with `dotnet`. With `--describe`, stdout is a JSON
capability/implementation description, including `performanceProfiles` available
on the actual platform/build. Release advertises the managed profile everywhere
and the timing profile on macOS ARM64; Debug advertises no measured profile. With no arguments, stdin is one JSON
request batch and stdout is one JSON observation envelope, as documented in
`conformance/runner/README.md`. Close stdin after the request. The process exits
after the batch; it is not a REPL or a persistent service.

The entire request is structurally checked before compilation starts. Unknown
fields, duplicate JSON properties, unsupported kinds, duplicate case IDs and
invalid options exit with code 2 and technical detail on stderr. No partial
response is emitted. Compilation errors instead produce a per-case `compileError`
observation with stable phase/code and structured diagnostic context, allowing later cases to execute.
An exit code of 0 means a valid observation envelope was produced, **not that the
conformance tests passed**. Only Python decides pass/fail.

Integration verification:

```sh
GES_CSHARP_ADAPTER="$PWD/artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll" \
  python3 -m unittest discover -s implementation/csharp/GameEventScript.Conformance/adapter/tests -p 'test_*.py'
```

The C# workflow builds and runs this control. No distribution or publication is
performed, and existing full Conformance gates remain unchanged.

## Managed allocation measurement

The opt-in `--measure` mode accepts script execution inputs plus a
`measurement` directive with profile, iteration/warmup counts, observer setting
and metric IDs/units. Only `csharp-dotnet-release-managed` and `run.allocated` in
bytes are currently implemented. It does not advertise general performance support.
Use Python `run --performance-profile ...`  so correctness
runs before measurement; expected baselines never reach this executable.

Release builds qualify the counter with zero-reuse/escaping-allocation controls,
then measure three fresh-host samples. Inputs, compilation, linking, initialization,
warmup and preflight stay outside the measured region. The measured loop directly
executes Receive and completion/frames, without JSON, observation lists or encoding.
Counting callbacks do not allocate. Work and callback counts must equal preflight
counts scaled by iterations. Results include all samples, controls, SDK/runtime,
configuration, OS/architecture and explicit current-thread managed-heap coverage.

## Time and compiler measurements

`--measure` with profile `csharp-dotnet-release-macos-arm64` is restricted to Release/macOS/ARM64 and the matching C# profile.
It uses three samples for AST construction, bytecode generation and host/load;
internal compiler access is granted only to this non-distributable harness.
Runtime samples reuse the allocation harness with observer/work controls. Elapsed
minima and allocation maxima are independent. Raw samples, timer/frequency and
scope details accompany full-precision metrics; Python reconstructs and compares
them. Use Python's `run --performance-profile ...` command to enforce correctness-first execution.
Measurement support does not assert that the existing budgets pass and never
rewrites corpus references.

## Adapter verification ownership

`tests/test_csharp_adapter.py` verifies this concrete adapter, while
`tests/instrumentation/` and the allocation/timing tests qualify C# counters,
raw samples and aggregation. They are not imported by the standalone runner.
Set `GES_MEASURE_TIMING=1` for the matching macOS ARM64 profile to include timing
qualification. Ordinary runner tests need no .NET installation or adapter variables.

Full measured acceptance on the matching Release/macOS ARM64 environment:

```sh
python3 conformance/runner/main.py run \
  --performance-profile csharp-dotnet-release-managed \
  --performance-profile csharp-dotnet-release-macos-arm64 \
  --output artifacts/conformance/python/complete-report.json \
  --adapter dotnet artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll
```

Both profiles use the same `--measure` process command. This adapter dispatches by
`measurement.profile`; the runner never chooses a C# measurement implementation.
