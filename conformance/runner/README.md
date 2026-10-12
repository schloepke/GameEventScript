<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Independent Python Conformance Runner

Requires Python 3.10 or newer, with the standard library only. No GES compiler,
Runtime, AST, assembler parser, .NET, Swift, YAML package or generated schema is
imported. Run from any directory; the default corpus is resolved relative to the
runner, not the working directory.

## Directory structure

```text
runner/
├── main.py          CLI entry point
├── core/            Corpus/YAML reader, immutable inputs, execution and transport
├── checks/          Assertions for each test kind and shared comparison helpers
├── runtime/         Host actions, native-handler inputs and random configuration
├── measurements/    Generic measurement protocol and metric evaluation
├── reporting/       Result reports and Received-Markdown writer
└── tests/           Unit tests and language-neutral protocol controls
```

`checks/*_checks.py` implements assertions against the shared Markdown cases.
`tests/test_*.py` tests the runner and those assertions themselves. Implementation-specific adapter tests live
with their adapter, outside this directory. This separates
Conformance execution code from its own verification tests. The package
`__init__.py` files are intentionally minimal; imports name their owning package.

The CLI and test-discovery commands below are unchanged. Test discovery starts
at `conformance/runner` and recursively discovers the `tests` package.

## Scope

The Python runner and internal C# adapter cover all 1,956 currently authored
cases: 1,925 correctness cases and 31 measured performance cases. Python owns
validation and assertions. The adapter executes real compiler/runtime APIs and
returns observations, never expected results or a pass/fail decision.

`run` selects the whole corpus. Performance is opt-in; without a measurement
profile the 31 cases are explicitly skipped, with exit code 3. No unsupported
case is silently filtered out. `validate` checks every selected kind and nested
execution schema before an adapter is started. `requests`/`verify` provide the
separate correctness observation-file workflow; their default selection is the
1,925 non-performance cases. `--kind supported` selects that same subset explicitly.

The legacy C# runner has been removed. C# acceptance uses this runner with the
internal C# adapter, including managed allocation and optional timing profiles.
Swift retains its existing runner until the separate adapter migration.

Normalized cases and their nested containers are read-only snapshots. Explicit
`copy.deepcopy` produces editable working copies for measurement preparation and
negative controls. Original UTF-8 bytes and source ranges are retained separately;
`parse_document(bytes, logical_name)` itself performs no filesystem access.
The CLI reads files and the binary resource resolver handles bounded fixtures.
Input diagnostics contain stable codes and UTF-8 offsets with scalar line/column
locations. Unsupported or invalid fields fail validation; English diagnostic
`detailContains` assertions are deliberately rejected in favor of stable fields.

## Commands

From the repository root:

```sh
python3 conformance/runner/main.py list
python3 conformance/runner/main.py list --kind bytecodeSnapshot
python3 conformance/runner/main.py requests --output artifacts/conformance/python/requests.json
python3 conformance/runner/main.py verify \
  --observations artifacts/conformance/python/observations.json \
  --output artifacts/conformance/python/report.json
python3 -m unittest discover -s conformance/runner -p 'test_*.py'
```

To run the internal C# compiler adapter directly:

```sh
dotnet build implementation/csharp/GameEventScript.Conformance/adapter/GameEventScript.ConformanceAdapter.csproj -c Release
python3 conformance/runner/main.py run \
  --output artifacts/conformance/python/csharp-report.json \
  --adapter dotnet artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll
```

Put `--adapter` last; its remaining arguments are passed literally, without a
shell. A run uses one fresh process for a batch, with a fresh compiler per case.
The default batch deadline is 60 seconds (`--timeout` overrides it); input,
stdout and stderr are bounded at 32 MiB each. Process failures, malformed replies
and timeouts are errors rather than expected compiler failures. On POSIX timeout
cleanup kills the process group; on Windows it kills the adapter process.
Adapters must not spawn child processes. Temporary transport files are removed.

`--corpus` accepts a suite file or a directory of suites. Do not select the whole
`conformance` directory: it intentionally also contains invalid parser fixtures
and documents in other formats. `--case suite.id/case-id` selects an exact case
and can be repeated. Unknown IDs and empty/conflicting selections are errors.

`list` and `requests` do not execute GES. `verify` compares observations; it does
not launch a compiler. Exit codes: 0 = selected cases passed, 1 = assertion
failure, 2 = invalid input or execution/setup error, 3 = incomplete due to an
optional-capability skip. Error takes precedence over failure, then skip.

## Observation-file boundary

The V1 process envelope covers all correctness kinds, including general host
scenarios and API checks. Performance uses a separate measured invocation after
correctness passes. `--describe` advertises identity, capabilities, supported
correctness kinds and the measurement profiles available on this machine/build.
`requests` includes ordered sources, exact names, compile options and
required capabilities, but never assertion expectations. Binary `programVersion` is also a compiler input
when checking provenance, so it is passed to the adapter. Its SHA-256 request digest
binds observations to the selected normalized inputs; it is not an authenticity
check and does not replace the complete corpus fingerprint contract.

An observation file has this shape:

```json
{
  "formatVersion": 1,
  "requestDigest": "copy from requests.json",
  "implementation": { "id": "example", "version": "development" },
  "capabilities": ["compiler", "program-binary", "bytecode-snapshot"],
  "observations": {
    "suite.id/case-id": { "status": "ok", "dump": "actual GESA text" }
  }
}
```

For an execution failure use `{"status":"error","detail":"optional detail"}`.
Compilation failures instead use `status: "compileError"` and a nonempty
`diagnostics` array. Each diagnostic contains stable `phase` and `code`, and may
include `symbol`, `symbolKind`, `sourceName`, `line`, `column`, `endLine`,
`endColumn`, `programName` and `handlerName`. Python compares all explicitly
constrained fields against any matching diagnostic; it never compares English
messages. An explicit expected null context accepts an absent or null field.
A generic execution error cannot satisfy an expected compiler failure.

Successful `bytecode` observations contain `opcodes`, a mapping of canonical
opcode names to nonnegative integer counts; absent opcodes count as zero.
Python checks contains/excludes, exact counts and minimum counts.
Successful `compileMetadata` observations contain `metadata`, with complete
`programResources`, `messageDefinitions` and `handlerResources` as described in
`specs/Conformance/MarkdownFormat.md`. Python compares specified constraints,
including ordered signature lists and exact resource counts. No AST is exposed.
Successful compilation for a `compileError` case returns only `status: "ok"`
and fails its assertion. Unexpected compilation failures in the other kinds
are execution errors. Numeric metadata and counts reject booleans and floats.
The producer never supplies a conformance pass/fail result. Missing observations
for supported cases are errors. A missing core capability is an error; a missing
optional capability is an explicit skip. Unknown observations, stale request
digests and malformed envelopes are rejected.

Dump comparison follows `specs/Conformance/Runner.md`: strict UTF-8, CRLF/CR
converted to LF, then exact bytes. Trailing whitespace, final newlines, Unicode
normalization, labels, registers, source comments and instruction order are not
ignored. Reports retain both normalized texts and the first differing byte.

## Binary fixture boundary

`--fixtures` selects the resource root (default: `conformance/fixtures`). Python
resolves portable relative paths within that root, rejects traversal and escaping
symlinks, limits each resource to 64 MiB, and checks its manifest SHA-256 before
sending any bytes. A missing, oversized or changed fixture is an input error,
never an expected binary read failure. The 32 MiB process envelope limit still
applies; use smaller selections if a batch exceeds it.

Binary requests add `binary: {data, includeDump, compareCompiledRuntime,
programVersion, runtime}`. `data` is canonical Base64. The adapter receives no filesystem
paths or expected read results. Provenance sources are passed only when the case
explicitly requests comparison with compiled runtime sections.

Binary observations use `status: "ok"` even for a reader/validator rejection.
Their `binary.outcome` is `valid`, `readError`, or `validationError`. Error results
contain `errorCode`, `byteOffset`, `sectionType`, and `entryIndex` (nullable context).
Valid results contain `moduleName`, `requiredRegisterCount`,
`requiredCallStackDepth`, `opaqueSectionCount`, `programVersion`, `buildMetadata`
(null or `{compilerId, compilerVersion}`), and Base64 `rewritten` bytes.
`runtimeSections` and `compiledRuntimeSections` contain Base64 required sections
when comparison is requested, and null otherwise. An optional top-level `dump`
is returned only when requested. The adapter reads with PreserveAll retention.
Python performs byte equality, SHA-256, metadata, error-context and dump assertions.
`runtime` is null when no Steps are present. Otherwise it contains `limits`, `random`, `hostCount`, `nativeHandlers`, `deferredPrograms`, `stepActions`, `initializationPump`, `publishSink`, and
ordered `steps`, each with `id`, a typed `input` message, `pump`, and `budget`.
All eleven runtime-limit overrides are supported. Pump modes are `completion`,
`enqueue`, `frame`, and `frames`; frame modes require a positive Int32 opcode
budget, other modes transmit null. Repeated frame pumping is bounded at 1,000,000
frames per Step, in addition to the adapter process timeout. Expected local/outbound messages, diagnostics
and limit constraints never reach the adapter.

The optional `runtime` observation contains a `hosts` array, including for a single
Host. Each entry contains `initialization` and a `steps` map keyed by Step ID.
Its length must exactly match `hostCount` (default 1, maximum 256). The adapter
compiles or reads the Program once and reuses that immutable object across fresh
Hosts, each with its own queue, Context, random stream and VM state. Python
compares every Host independently and prefixes mismatch paths with `/hosts/N`.
Each phase contains ordered `trace`, `actions`, `local`, `outbound`, `diagnostics` and `runtimeLimits`
arrays. Every phase reports `hostReady`, `programStarts` and `idleDurationMicroseconds`; Steps also report `accepted`,
`paused`, and `waiting`. A fresh host uses the fixture Program, the configured publish
sink and a virtual clock starting at zero. Initialization calls Start and drains
on success unless `initializationPump` is `start`. Failed Start is never pumped; observations reset between steps. Encoding occurs after
callbacks return. Python checks empty default channels/diagnostics, exact messages,
and ordered structured runtime diagnostics (phase/code plus optional context),
ordered limit inclusion and limit exclusion, including `{ any: true }` wildcards.
The former English `detailContains` assertion in the corpus now uses structured
queue-limit and dispatch traces. New `detailContains` assertions are still
rejected; English diagnostic text is never silently ignored. Pause/wait constraints are optional.

The initial runtime value transport covers Nothing, Boolean, Text, exact Int64 and Binary64,
Quantity variants with units, Percentage, Tags, Dice, Vector, Point, Int64/Binary64
Ranges, nested Messages, record snapshots, recursive Lists and Maps. Numeric payloads are decimal strings, never JSON numbers; Boolean
and Int64 remain distinct. Maps and records use last-entry-wins construction and compare by Unicode-scalar
keys. Record type names remain exact. Lists and message arguments retain order. Binary64 accepts explicit `NaN`, `Infinity` and `-Infinity`. Decimal overflow
is rejected. Python implements the corpus's exact-bit and ULP comparison modes;
comparison settings never cross the adapter boundary. Exact mode distinguishes
signed zeros; ULP mode treats both zeros as equal. Infinities compare only to the
same infinity, and expected NaN represents Nothing as in the shared contract.
ULP tolerance applies only to Binary64 numeric payloads, including quantities,
percentages, Vector/Point components and Binary64 Range components; kinds, units, Int64, text and container structure remain exact.

Dice preserve individual roll order and never compare only their sum.
Other value types fail explicitly until their exact
transport/comparison contract is implemented.

Unexpected execution/writer/compiler failures use `status: "error"` and cannot
satisfy a binary-error expectation.

Random configuration contains exactly one of `seed` (canonical signed Int64 text),
`sequence` (nonempty finite Binary64 strings), or `entropy` (uppercase hexadecimal
byte pairs). Python validates authored canonical spellings and preserves sequence
order. Omitted configuration becomes seed zero. Sequence exhaustion continues
with fallback seed zero. Entropy bytes go through the host entropy API; Python
never derives seeds or simulates a PRNG. Each case owns fresh random state.

## Source runtime scenarios

Supported `scriptApi` cases compile ordered source groups into Programs (or use a native-only Host) and use the same `runtime`
request/observation as binary Steps. Optional binary roundtrip is honored before
host execution. Compiler or link failures are setup errors, not expected runtime
results. Each Program has its own compiler, and each case its own host; failure in one case does not
contaminate the next. Only source inputs, compile options, runtime limits, random configuration and
input messages cross the adapter boundary. Comparison settings and expected
messages never reach the implementation.

The adapter includes the shared test extension effects `test.echo(_)`,
`test.notify(_)`, `test.truth()`, `test.fail()`, `test.failWithRandomScope()`, `test.toJson(_)`, and
`test.fromJson(_)`, `test.vectorSum(_)`, `nav.shortestTurn(from,to)` and the
unary `nav.isNorth` and `math.floor(_)` fixtures. These use Runtime APIs directly and contain no assertions.
Unbound extensions produce a setup error; there is no permissive fallback.

Native handlers support ordered parameters or name-only matching, signed Int32
priorities, initial subscription state, fixed emissions and argument forwarding.
Programs load before native handlers subscribe; registration order is retained.
Unexpected exceptions and declared fault codes/context execute before emissions.
The adapter returns structured runtime diagnostics; Python compares their count,
order and authored context constraints, never their English text. Native definitions
contain fixture inputs only. Native and pre-Receive step actions support `loadProgram`, `detachProgram`,
`subscribeHandler`, and `unsubscribeHandler`. Only deferred Program IDs may be
loaded explicitly. Actions use `{operation, target}` transport without expectations.
Each phase records ordered action invocations with an origin (`step/ID` or
`native/ID`) and a complete result sequence. Results contain a Boolean `result`
and null `error`, or null `result` and a structured link diagnostic. Python checks
expected results and errors, including errors handled inside native callbacks.
No expectation influences whether the adapter catches a link error and continues.
The `advanceMicroseconds` action advances a host-local virtual clock without
pumping. Its nonnegative Int64 amount is a decimal string; overflow returns false
and leaves the clock unchanged. Optional lifecycle assertions compare readiness,
Program start states and exact idle duration (`none` or Int64 text). Missing Program
handles fail an asserted start state. Observer traces cover emit, publish, dispatch start/completion, runtime limits and
diagnostics in exact callback order. Omitted trace expectations make no ordering
assertion; an explicit empty trace requires no callbacks. Diagnostic and limit
entries retain their normal partial-constraint semantics. Trace messages use the
case's numeric comparison mode. The `publishSink` input selects `accept` (default),
`absent`, `reject`, or `throw`; outbound observations retain every sink call even
when it rejects or throws. No sink is installed for `absent`.

## Load failures and external catalogs

`loadError` transmits `api: {limits, externalTypeRegistry}` and normal source/compile
inputs. The adapter compiles first, then invokes `Host.Load` without starting the
Host. Registry modes are `environment`, `absent`, and the fixed `mismatch` fixture.
Observations use `status: loaded` with empty diagnostics, or `status: loadError`
with one structured link diagnostic. Compile failures and unrelated exceptions
are execution errors; they cannot satisfy an expected link failure.

`externalTypeApi` transmits `api: {typeNames: [...]}` without GES source. The
adapter builds real definitions and a catalog and returns `externalType` with
`typeCount` or `error: duplicateTypeName`. Definitions are constructed outside the
catalog's duplicate-error boundary, so invalid type names cannot masquerade as
duplicate failures. Python compares the outcome; expected errors/counts never
reach the adapter. The original duplicate-name case now uses valid PascalCase
names (`Sample`) to exercise the intended catalog validation.

## Value API

`valueApi` transmits `api: {value, equalTo, notEqualTo, mutateSourceAfterCreate}`.
Absent comparands are null. The adapter constructs real GES values and reports
normalized values, flags, length, custom type name, equality and hash equality.
Python checks the authored expectations; it does not implement GES equality.
Input Map/Record entries retain their original order and duplicate keys, including
nested collections, so normalization is exercised by the implementation.
Defensive-copy cases mutate the source array after constructing a List, Dice,
Map or Record and then observe the constructed value. Negative controls verify
that reordered Map observations and incorrect hash results are rejected.

## Message API construction and identity

The first 21 `messageApi` cases cover signatures, ordered arguments, normalized
names/tags, message and Handler equality/hash equality, and signature-based
message creation. `api` contains only the authored construction inputs; optional
comparison inputs request real API equality operations. Labels, tags and their
whitespace reach the API unchanged. The negative argument-mapping fixture is
retained and explicitly rejected as `invalidArgumentsShape` at the adapter boundary.
Construction failures are reported as stable message error codes without matching
English diagnostic text. Python owns the expected-result comparisons.
The 48 `compareConformanceMessage` cases exercise the same typed comparison
operation used for runtime observations, including exact/ULP comparisons, numeric
storage, units and nested containers. The comparison message and options remain
in Python and never reach the adapter. Successful non-JSON Message API observations
include the real constructed message as `normalized`; the adapter never reports
`conformanceEquals`. Inverting all 48 Boolean expectations must fail all 48 cases.
All 41 product JSON cases are also supported. Optional `api.json` is transported
as opaque text, including malformed JSON; only the implementation codec parses
it. `roundTripJson` invokes real serialization/deserialization when true. JSON
operations return exact canonical `message.json` instead of the runner's typed
`normalized` message; this also supports codec values such as Series and Handler
without reimplementing the product codec in Python. Stable `message.*` failures
come from the codec's structured exception code. Python compares canonical JSON
byte-for-byte as text, without parsing or rewriting it. Use `roundTripJson: false`
to request serialization without a roundtrip. Combining JSON operations with
`compareConformanceMessage` requests an additional normalized observation;
the comparison expectation remains exclusively in Python.

## Performance protocol

The runner has no implementation registry, platform names, runtime counters or
instrumentation-specific assertions. It treats profile IDs as opaque identifiers.
A new implementation can support the protocol without changing Python code.

1. `--describe` returns `formatVersion: 1`, `implementation`, `capabilities`,
   `supportedKinds` and `performanceProfiles` (an array of available profile IDs).
2. `run --performance-profile PROFILE` selects references for that profile from
   the corpus. Repeated profiles are ordered preferences per case. An unavailable
   requested profile or missing matching reference is an error. Without a profile,
   performance cases are explicitly skipped.
3. A normal adapter invocation executes the selected workloads for correctness.
   A failure prevents measurement; unmeasured cases cannot pass as performance results.
4. The runner invokes the same executable with `--measure`. The ordinary batch
   request adds a `measurement` object to each case: `profile`, `iterations`,
   `warmupIterations`, `compileWarmupIterations`, `observeRuntime`, and `metrics`
   containing only `id` and `unit`. The digest covers these directives.
5. The adapter responds with the usual version, digest, identity and observations.
   Successful observations contain `status: "ok"` and `measurement: {profile,
   metrics: [{id, unit, value}]}`. Values are finite nonnegative Binary64 strings.
   Failures use `status: "error"` with optional technical `detail`.

Python validates identities, metric IDs, units and numeric values and applies the
smallest authored bound. References and tolerances never reach the adapter.
Optional `evidence` is opaque diagnostic data, retained in the report; it is not
interpreted by the runner and cannot determine pass/fail. Qualification of native
counters, measurement scope, aggregation and platform assumptions belongs to the
adapter's own implementation tests. A real Swift adapter can therefore use its
own instrumentation and profile names without a runner change.

```sh
python3 conformance/runner/main.py validate
python3 conformance/runner/main.py run \
  --performance-profile YOUR-PROFILE \
  --output artifacts/conformance/python/report.json \
  --adapter YOUR-ADAPTER
```

Concrete C# profiles, commands and measurement controls are documented with the
[internal C# adapter](../../implementation/csharp/GameEventScript.Conformance/adapter/README.md).

## Reports

`run` and `verify` write the V1 result shape from `specs/Conformance/Runner.md`,
with complete case identities, explicit skips, actual assembler text and metric
results. Metrics use finite invariant shortest-roundtrip Binary64 strings with
lowercase `e`, an unpadded exponent and no redundant `.0`. Raw adapter numeric
spellings are normalized for the report. Optional opaque adapter evidence remains
in `measurementEvidence`. `sourceDocuments` binds Received updates to SHA-256 of
the exact original bytes, including BOM and line endings. Neither extension is
sent as an assertion expectation to the adapter.

## Received Markdown

```sh
python3 conformance/runner/main.py received \
  --corpus conformance/suites/compile/program-dumps.md \
  --case compile.program-dumps/compact-bindings \
  --report artifacts/conformance/python/complete-report.json \
  --output artifacts/conformance/python/program-dumps.received.md
```

Received output is explicit and never overwrites the source or the report.
Only selected GESA payloads and selected performance profile `reference` tokens
are replaced. Prose, comments, tolerances, other profiles, BOM and unrelated line
endings remain byte-identical. Newly inserted payloads use the document's line
ending when unambiguous, otherwise LF. Missing/duplicate results, stale source
hashes, mismatching case/profile/metric identities, absent or overlapping ranges,
and noncanonical measurements are rejected. A failed assertion can produce a
Received candidate for review; execution errors and skips cannot. No command
automatically accepts the new baselines.
