<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Conformance runner specification

This document defines the normative execution and result contract for the
normalized test model in [Markdown format](MarkdownFormat.md). The authoring parser and
the runner are separate components: parsing never executes a script, and the
runner never interprets Markdown or YAML.

The runner API is synchronous, threadless, fileless, networkless, and independent
of MSTest, XCTest, JUnit, or any other test framework.

## Inputs and operations

The runner consumes only fully validated immutable conformance documents and an
explicit immutable environment. It exposes three conceptual operations:

- `RunCase(document, caseId, environment)`;
- `RunDocument(document, environment)`;
- `RunCorpus(documents, environment)`.

`RunCase` does not execute other cases. `RunDocument` runs cases in document
order. `RunCorpus` runs documents in caller-supplied order and cases in document
order. The caller is responsible for deterministic discovery order; the runner
rejects duplicate suite or full case IDs before executing any corpus case.

Every case gets a newly constructed compiler/host/test environment unless its
kind explicitly has no such component. No mutable state, host queue, random
state, registry mutation, performance sample, or observer event crosses a case
boundary. Implementations may cache immutable definitions that cannot affect
observable behavior.

## Conformance environment

An environment contains:

- stable `runnerId` and `runnerVersion` strings;
- an implementation ID and implementation version;
- a set of supported capabilities;
- portable compiler, program reader/writer, host, VM, extension catalog/runtime
  registry, and message/value factories as required by the declared capabilities;
- declarative native-handler and publish-sink factories;
- an optional bounded resource resolver;
- an optional performance profile ID and performance measurement provider;
- explicit runner limits.

The performance provider receives the fully normalized case and selected
profile ID after the runner has completed the ordinary correctness execution.
It returns an immutable set of canonical Binary64 metric strings with explicit
units. Measurement mechanics remain implementation-specific; metric validation,
bound calculation, classification, and result construction remain in the
portable runner.

An optional result sink is notified once for every completed case, in execution
order. It does not participate in comparison and receives the same immutable
case result that is retained by the aggregate report.

The runner does not obtain defaults from process culture, current directory,
environment variables, wall-clock time, locale, timezone, or ambient random
state. Adapters may use such inputs to construct an explicit environment, but
they are not hidden runner dependencies.

### Capabilities

Capability IDs use the authoring ID grammar. V1 defines these core IDs:

- `compiler`
- `program-binary`
- `host`
- `vm`
- `message-api`
- `value-api`
- `external-types`
- `native-handlers`
- `publish-sink`
- `observer`

V1 defines these optional IDs:

- `performance`
- `bytecode-snapshot`

Kinds add their required capabilities as specified by
[Markdown format](MarkdownFormat.md). If a required core capability is absent, the case is
an `error` with code `conformance.runner.missingCoreCapability`; it is never a
skip. If an explicitly optional capability is absent, the case is `skipped` with
code `conformance.runner.missingOptionalCapability`. An unknown capability ID in
a V1 document is a schema error.

An environment must not advertise a capability and then skip cases requiring
it. Incomplete language ports express honest capability sets; the aggregate
report makes those omissions visible.

### Resource resolver

The optional resource resolver accepts a portable resource ID plus an explicit
maximum byte count and returns immutable bytes or a structured error. It never
receives an instruction to interpret a path or URL. The runner itself never
opens a file, accesses a package resource, or performs network I/O.

The resolver is not used for embedded `ges` or `gesa` blocks. `programBinary`
passes only the declared `resourceId` and `MaxResourceBytes`. A resolver returns
immutable bytes or the structured status `notFound`, `limitExceeded`, or
`error`. The runner independently rejects oversized returned data and verifies
the manifest SHA-256 before decoding it. Resource failures and integrity
mismatches are test-environment errors, never expected format failures.

## Preflight

Before executing the first selected case, the runner validates:

- the normalized model invariant and all IDs/references;
- duplicate IDs in the selected document/corpus;
- that the environment is internally complete for every advertised capability;
- runner limits and resource limits;
- the selected performance profile when performance cases are selected and the
  environment advertises the optional `performance` capability.

An invalid authoring document should already have failed parsing. A forged or
programmatically constructed invalid normalized model still produces `error`,
not an implementation exception escaping the runner.

## Common execution rules

- Case setup, compilation, linking, initialization, steps, and comparison occur
  in that order where applicable.
- Programs are compiled and loaded in the order defined by their source
  descriptors. Native handlers are registered in metadata order.
- Programs listed in `deferredPrograms` are compiled during setup but are not
  loaded until their fixed native `loadProgram` action executes. Native
  lifecycle actions operate only on validated IDs and are idempotent.
- The runner performs one initialization run-to-completion pump after setup and
  before the first step, even when no initialization output is expected.
- A runtime step first applies its optional ordered `stepActions`, then calls
  `Receive`, records its acceptance, and uses the table's pump mode.
  `completion` makes one run-to-completion call. `frames` repeatedly calls
  `ExecuteFrame(budget)` until idle or runtime-limit state, `frame` calls it
  exactly once, and `enqueue` performs no pump. Frame modes record whether a
  result was paused. When an action declares `expectResult`, its boolean result
  is compared before `Receive`.
- Native handlers are atomic according to the portable Host contract.
- Local messages, outbound sink messages, runtime-limit observations, and
  runtime diagnostics are recorded and compared separately. When an expectation
  contains `trace`, every Emit, Publish, DispatchStarted, DispatchCompleted,
  RuntimeLimitReached, and RuntimeError callback is additionally compared as one
  exact ordered sequence.
- Any unhandled implementation exception is caught at the runner boundary and
  becomes an `error`. Its platform text may be included as nonnormative technical
  detail but never determines a pass.

The runner uses no parallel execution internally. A testframework adapter may
run independent cases concurrently only when it gives each case an independent
environment and the implementation under test permits it. Performance cases
must not be run concurrently unless their selected profile explicitly permits
that measurement mode.

## Kind execution

### Script API

For a source-backed case, the runner compiles each program once and optionally
performs its requested `.gesb` roundtrip. A native-only case skips compilation
and VM setup entirely. The runner creates the declared number of hosts with the declared runtime
limits, links/loads non-deferred programs, registers initially enabled native
handlers, pumps initialization, and executes the ordered steps. With
`hostCount > 1`, the identical immutable Program objects and the same expected
scenario are used independently for every Host; outputs are never merged.

An absent expected channel means the format-defined empty default, not “do not
compare”. Message name, tags, argument order, argument names, value kinds,
units, and non-Binary64 payloads compare exactly. Maps and records use their
portable semantic equality contract.

### Compile and load errors

`compileError` passes only when compilation fails and at least one emitted
diagnostic matches every present expectation field, including required phase
and code. Successful compilation fails the case.

`loadError` first requires successful compilation and optional binary roundtrip.
It passes only when `Host.Load` fails with a matching diagnostic and leaves the
new host observably unchanged. A compile failure is an error in the test setup,
not the expected load failure.

Diagnostic message text, exception class names, stack traces, and technical
details are never compared.

### Message API

The runner constructs signatures and messages through public portable message
APIs. When an error is expected, only its stable message error code is compared.
Otherwise every present normalized property expectation is exact. Optional
comparisons cover signature, message and handler equality plus the equal-value
hash invariant; ordered values may also be bound through a signature.

### Value API

The runner constructs a portable value, optionally mutates its source arrays,
then compares its normalized representation, public flags/readers, equality and
equal-value hash invariant. This kind does not compile or execute GES source.

### External type API

The runner constructs the declared portable external-type catalog and compares
its count or stable duplicate-name error. No Reflection or platform type is
involved.

### Compile metadata and bytecode constraints

`compileMetadata` inspects only public portable program metadata. `bytecode`
counts the final optimized instruction stream by canonical opcode name.
Compiler-internal AST, lowering nodes, and native enum ordinal positions are not
observable.

### Bytecode snapshot

`bytecodeSnapshot` compiles with the resolved options and produces the canonical
Game Event Script Assembler dump. Expected and actual text are converted to
strict UTF-8 with logical `LF` and compared byte-for-byte. A mismatch records
both texts and the first differing UTF-8 byte offset.

Build metadata that is intentionally implementation-specific must not appear in
a cross-language snapshot, or the case must declare a capability/profile scope
that makes it non-cross-language. Required runtime segments and canonical
portable dump content remain comparable.

### Program binary

`programBinary` resolves one bounded immutable `.gesb` resource, verifies its
SHA-256, and calls the full Program reader. Structural read errors and semantic
validation errors are classified by the stable format-error ranges specified
in [Markdown format](MarkdownFormat.md) and compared without platform exception text.

For a valid Program the runner compares manifest/build identity and all present
metadata expectations, invokes the canonical writer, and compares requested
byte equality and rewrite SHA-256. If Steps are present, it loads that parsed
Program into a fresh Host and executes the ordinary initialization and step
pipeline. Embedded source remains provenance and is never opened or compiled.
The runner never interprets `relativePath`; packaging adapters map resource IDs
to files, package resources, or caller-provided memory.

### Performance

A performance case first performs one ordinary correctness execution. A
correctness mismatch is `failed` and no performance pass may hide it.

The environment's exact performance profile selects one expectation profile.
If the environment advertises `performance` but no matching profile exists, the
case is `error` with `conformance.runner.missingPerformanceProfile`; it is not a
skip. Adapters that do not intend to benchmark omit the optional `performance`
capability and receive a normal skip.

Warmup samples are discarded. Measured samples use the workload counts from the
case and the measurement provider's explicitly documented scopes. Each measured
metric must have exactly the declared unit. Bytes are converted to KiB by
division by 1024, never 1000. Per-iteration metrics divide by the exact measured
iteration count.

For every metric, the permitted upper bound is calculated as defined by the
authoring specification. A value equal to the bound passes. A lower value always
passes. NaN, infinity, a negative measurement, missing metric, additional
unexpected required metric, unit mismatch, or invalid iteration count is an
error rather than a regression failure. A finite value above the bound is a
`failed` result with code `conformance.performance.regression`.

Performance values never affect non-performance cases and are not normative
cross-platform equivalence values.

## Binary64 comparison

Exact mode compares IEEE-754 Binary64 bits after the portable value
canonicalization rules. Consequently stored zero is canonical positive zero and
invalid scalar NaN is observable as `nothing`, not as a NaN payload.

ULP mode uses the ordered-bit distance algorithm from
[Number semantics](../Semantics/Numbers.md) and the declared non-negative maximum. The mode
applies recursively to finite Binary64 fields in floats, percentages, vectors,
points, ranges, lists, maps, records, messages, and arguments. Infinities require
the same sign. It never weakens type, unit, ordering, key, or count comparison.

## Status and failure classification

Every selected valid case produces exactly one of:

| Status | Meaning |
| --- | --- |
| `passed` | execution and every expectation matched |
| `failed` | the implementation produced a valid observable result that did not match |
| `skipped` | one or more declared optional capabilities were absent |
| `error` | invalid environment/model, missing core support, or runner/implementation failure prevented a valid comparison |

A status has one of the following stable codes:

- `conformance.passed`;
- `conformance.assertion.mismatch`;
- `conformance.runner.missingOptionalCapability`;
- `conformance.runner.missingCoreCapability`,
  `conformance.runner.invalidEnvironment`, `conformance.runner.invalidModel`,
  `conformance.runner.unhandledException`, and
  `conformance.runner.missingPerformanceProfile`;
- `conformance.compile.expectedError` and
  `conformance.load.expectedError`;
- `conformance.performance.regression`;
- `conformance.resource.unavailable`,
  `conformance.resource.limitExceeded`, and
  `conformance.resource.integrityMismatch`.

`failed` means the test reached its intended assertion boundary. Infrastructure
or malformed-input problems are `error`. Adapters must preserve this distinction
instead of mapping both to an assertion failure internally.

The aggregate status is `error` if any case errored, otherwise `failed` if any
case failed, otherwise `passed` if every selected case passed or skipped. A run
containing only skipped cases has aggregate status `skipped`.

## Case result model

Each immutable case result contains:

- full case ID, suite ID, local case ID, display title, kind, level, categories,
  and tags;
- status and stable code;
- missing optional capabilities for a skip;
- zero or more structured mismatches with a path, expected value, actual value,
  and optional diagnostic/source range;
- ordered portable diagnostics and runtime-limit observations when relevant;
- optional actual GESA text for snapshot mismatch/update;
- optional performance profile, measurements, references, and allowed bounds;
- optional nonnormative technical detail.

Expected/actual values use the same portable message/value shapes as the
authoring format. A mismatch path is a stable JSON-Pointer-like ASCII path such
as `/steps/add/local/0/args/0/value`; it does not contain a translated display
message.

A mismatch has required string fields `path` and `code`, optional `expected`
and `actual` portable nodes, and optional `diagnostic`. A result diagnostic uses
required `phase`, `code`, and `message` plus the optional portable fields from
[Diagnostics](../Diagnostics.md); `technicalDetails` may be emitted only as explicitly
nonnormative detail. A performance result contains `profile` and a metric map;
each metric contains canonical Binary64 strings `measured`, `reference`, and
`allowed`, plus `unit` and a boolean `passed`. Missing capabilities are sorted
ordinally. Null is used only for schema fields explicitly shown as nullable;
absent optional diagnostic fields are omitted.

## Aggregate and canonical JSON result

`RunDocument` and `RunCorpus` return an immutable report containing runner and
implementation identity, the exact advertised capability set, aggregate counts,
aggregate status, and ordered case results.

`ConformanceResults.json` is the normative machine result. Its top-level shape
is:

```json
{
  "schemaVersion": 1,
  "runner": {
    "id": "steph.ges.conformance.csharp",
    "version": "0.1.0"
  },
  "implementation": {
    "id": "steph.ges.csharp",
    "version": "0.1.0"
  },
  "capabilities": ["compiler", "host", "vm"],
  "performanceProfile": null,
  "status": "passed",
  "summary": {
    "total": 1,
    "passed": 1,
    "failed": 0,
    "skipped": 0,
    "error": 0
  },
  "cases": [
    {
      "id": "runtime.math/integer-add",
      "suiteId": "runtime.math",
      "caseId": "integer-add",
      "title": "Integer addition",
      "kind": "scriptApi",
      "level": "atomic",
      "categories": ["conformance"],
      "tags": ["runtime", "math"],
      "status": "passed",
      "code": "conformance.passed",
      "missingCapabilities": [],
      "mismatches": [],
      "diagnostics": [],
      "runtimeLimits": [],
      "actualAssembler": null,
      "performance": null
    }
  ]
}
```

The writer emits strict UTF-8 JSON, no BOM, `LF`, two-space indentation, and a
terminal `LF`. Object property order is the order shown by the V1 schema.
Capabilities are sorted by Unicode-scalar ordinal order; cases retain execution
order; tags and event sequences retain semantic order. Int64, UInt64, and
Binary64 domain values use canonical strings so a JSON implementation cannot
round them through its default number type. Summary counts are JSON integers.

Every case object contains `runtimeLimits`, `actualAssembler`, and
`performance` at the shown positions. Runtime-limit entries contain `name`,
`detail`, and integer `limit`. `actualAssembler` is either the normalized GESA
text or `null`. Performance is either `null` or an object containing `profile`
and a `metrics` object keyed by metric ID; every metric contains, in order,
`measured`, `reference`, `allowed`, `unit`, and `passed`. Optional mismatch and
diagnostic fields are omitted when absent. Optional nonnormative
`technicalDetails` is the final case property when explicitly requested from
the runner.

The normative result contains no timestamp, hostname, username, absolute path,
wall-clock duration, process ID, random build ID, or stack trace. A tool may
write such data to a separate nonnormative envelope.

Result JSON is an execution/exchange format. It is not an alternative test
authoring format and the runner does not read it as a suite.

## Cross-language result projection

The full result intentionally contains implementation identity, diagnostics,
assembler text, and profile-local performance data. Cross-language comparison
therefore uses the additional compact artifact produced by
`ConformanceCrossLanguageResultJsonWriter`.

The writer consumes the complete parsed corpus plus a complete run report. It
computes the exact corpus fingerprint, sorts cases by full stable ID, and emits
only case identity, kind, level, Core/optional requirements, status, and stable
code. It rejects partial, duplicate, additional, or metadata-inconsistent
reports. It never reruns a case.

The exact fingerprint algorithm, compact JSON schema, C# reference artifact,
parser bootstrap process, capability policy, and acceptance comparison are
normatively defined in [Cross-language acceptance](CrossLanguageAcceptance.md).

## Human-readable Markdown report

`ConformanceReport.md` is generated solely from the structured report. It
contains, in order:

- runner/implementation identity and optional performance profile;
- aggregate status and Passed/Failed/Skipped/Error counts;
- advertised and missing capabilities;
- one summary row per case keyed by full stable ID;
- failure/error sections with structured mismatches and diagnostics;
- a performance table with measured, reference, allowed, and unit values.

The Markdown report is informative. The structured report and canonical JSON
define machine meaning; prose wording and table layout may evolve without a
schema-version change.

## Received Markdown

The received writer accepts the original immutable source document plus a
structured run report. It may update only:

- the scalar token of a selected performance metric's `reference`; and
- the payload range of the selected case's `gesa` block.

It uses the parser's original UTF-8 source ranges. Every byte outside the
replaced ranges is copied exactly, including prose, comments, whitespace,
tables, line endings, BOM presence, tolerances, and maximum values. Replacements
use the original document line-ending style where a style is unambiguous.

Overlapping, missing, stale, or ambiguous ranges are an error. The received
writer returns bytes/text to its caller and never overwrites a source file. A
filesystem or testframework adapter may save `<Suite>.received.md` as an
explicit approval candidate.

The portable writer updates every performance measurement and actual assembler
present in the selected report results for the source suite. A report may be a
whole-corpus report, but matching full/local IDs, title, kind, level, performance
profile, metric set, old reference, and unit must agree with the parsed source.
It reports `conformance.received.invalidReport`,
`conformance.received.missingCase`, `conformance.received.missingRange`,
`conformance.received.staleRange`, or
`conformance.received.overlappingRange` for incompatible reports, missing
ranges, stale range contents, or overlaps. A mixed-line-ending source has no
unambiguous document style; inserted multiline GESA then uses `LF` while all
existing bytes remain untouched.

## Adapter responsibilities

A CLI or testframework adapter may discover files, read bytes, provide fixture
resources, choose a performance profile, and write result/report/received
artifacts. It must:

- pass document bytes to the portable parser under explicit limits;
- expose every H2 case as an individually addressable test when the framework
  supports parameterized tests;
- preserve the runner's stable full ID in display/output metadata;
- use the same runner path for individual and aggregate execution;
- avoid replacing skipped/error distinctions with framework-specific guesses;
- never modify the authored suite without a separate explicit approval action;
- emit a compact cross-language result for a complete corpus run and retain the
  full result as the diagnostic source;
- compare ports by full stable case ID and corpus fingerprint, never by native
  testframework names or discovery order.

Framework assertions, reflection-based discovery, environment-variable lookup,
and filesystem paths belong to adapters, not to the portable parser, normalized
model, runner, result writers, or received writer.
