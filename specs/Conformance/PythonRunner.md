<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Independent Python runner and internal adapters

The implementation under `conformance/runner` is independent of all GES
implementations. The language-neutral Markdown and execution contracts remain
owned by [MarkdownFormat](MarkdownFormat.md) and [Runner](Runner.md).

Load-error cases additionally verify real Host.Load diagnostics independently of
compiler failures. External-type catalog cases verify counts or duplicate-name
rejection through the portable API. All currently authored `valueApi` cases
exercise real value construction, normalization, flags, length, equality, hash
equality and defensive copies. Input entry order and duplicate keys are preserved
across transport; only the implementation normalizes them. Copy checks mutate the
original source array after construction and observe the resulting value.
Literal-input `valueApi` cases call the public lenient or strict literal parsing
API; adapters report the recognized value, absence or the literal limit.
Expected results remain exclusively in Python. The initial 21 `messageApi` cases
cover ordered signatures and arguments, construction failures, equality/hash
equality and signature-based creation. Another 48 cases qualify the independent conformance-message comparison against
raw constructed messages, using the same typed comparison as runtime expectations.
Expected messages and numeric comparison options never reach the adapter. Product
JSON cases are supported through the actual product codec, with exact canonical
text comparison and structured `message.*` failures. JSON observations carry the
serialized envelope instead of typed Conformance transport. Combining JSON with
`compareConformanceMessage` requests an additional typed observation; expectations
remain in Python.

The current executable scope covers `bytecodeSnapshot`, `compileError`,
`bytecode`, `compileMetadata` and all currently authored `programBinary` cases,
including their Steps, plus all currently authored `scriptApi` cases: single- and
multi-Program scenarios and native-only Hosts. Inventory validates every selected execution schema. Runtime transport currently covers Nothing,
Boolean, Text, Tag, Dice, exact Int64/Binary64, Quantity, Percentage,
Vector, Point, both Range variants, Messages, record snapshots, Lists and Maps, with local/outbound messages and limit
observations. All runtime-limit overrides and completion, enqueue, single-frame
and repeated-frame pumping are supported. Limit assertions use stable names,
configured values, and wildcard constraints; English detail matching is rejected; stable limit fields and observer traces
replace such assertions.
Deterministic random configuration covers signed Int64 seeds, finite start
sequences, and entropy bytes passed to the corresponding host APIs. Multiple Hosts reuse the same immutable Program and are observed and checked
independently; `hostCount` is bounded at 256. The runner
does not implement a PRNG; omitted configuration and sequence fallback use seed zero.
Python owns exact-bit/ULP comparisons while preserving storage kind
and unit; expected NaN matches Nothing. Declarative native handlers support matching, priorities, emissions, argument
forwarding and deliberate/unexpected faults. Runtime diagnostics are checked in
order against stable fields and context. Load/detach and subscribe/unsubscribe actions are supported in native handlers
and before step inputs. Their Boolean or structured link-error results are
checked in Python; action expectations never reach the adapter. Virtual-time advances and optional readiness, Program-start and idle-duration
assertions are supported, including explicit initialization observations and
Start-only pumping. Observer traces cover emit, publish, dispatch start/completion, runtime limits and
diagnostics in exact callback order. Omitted trace expectations make no ordering
assertion; an explicit empty trace requires no callbacks. Diagnostic and limit
entries retain their normal partial-constraint semantics. Trace messages use the
case's numeric comparison mode. The `publishSink` input selects `accept` (default),
`absent`, `reject`, or `throw`; outbound observations retain every sink call even
when it rejects or throws. No sink is installed for `absent`.
All currently authored kinds are implemented. Unknown schemas fail explicitly
rather than producing partial acceptance. The default run includes every case;
optional performance execution is represented by a skip unless profiles are
selected. The legacy C# runner has been removed; C# acceptance uses this runner
and the internal adapter. Swift uses the same protocol and Python assertions;
its legacy parser and runner have also been removed.

Adapter observations contain raw canonical dumps, structured compiler
diagnostics, opcode counts, Program metadata and rewritten fixture bytes rather
than assertion results. Python validates fixture integrity and performs binary
comparisons without implementing a GES binary reader. The oracle applies the exact LF/UTF-8 snapshot comparison
specified by Runner.md. The V1 observation-file/process-batch envelope and its
`python.observation.*` codes describe the process transport boundary; the portable
assertion and input diagnostic contracts remain owned by Runner.md and MarkdownFormat.md.

See the [runner usage and scope](../../conformance/runner/README.md) for commands,
limits and profile-provider contracts. Generated requests, observations
and reports belong under ignored `artifacts`, never in the authored corpus.

Performance schema and bound controls cover all authored profiles. The runner
contains no implementation-specific measurement provider. Profile identifiers
come from the adapter's `--describe` response and are matched against corpus
references. The generic `--measure` invocation sends workload directives and
metric IDs/units, never references or tolerances. Correctness must pass first.
Python checks the response identity/digest, metric structure and bounds. Optional
evidence is opaque diagnostic data. Native instrumentation, counter controls,
aggregation and their verification belong to the adapter implementation.
See the runner README for the complete V1 process envelope. Implementation-specific
integration tests live alongside the adapter; standalone runner tests use synthetic
observations and arbitrary profile names. No language adapter is needed to test
or install the runner.

Normalized inputs are immutable snapshots; explicit copies support negative
controls. The fileless byte-buffer parser retains original UTF-8 ranges; the CLI
owns file access. The complete selection is validated before execution.
`run` and observation-file `verify` emit the portable V1 report fields. Two tooling
extensions retain `sourceDocuments` (suite ID to original-byte SHA-256)
and optional `measurementEvidence` (qualified raw provider reports).

The `received` command requires a single source document, explicit case selection,
a report and a separate output path. It verifies source hashes, case identity,
profile/metric identity and original ranges before replacing only GESA payloads
or performance reference tokens. It never updates the authored source itself.
This implements the restricted received writer from Runner.md; it is not a
serializer for arbitrary Markdown or changed expectations.
