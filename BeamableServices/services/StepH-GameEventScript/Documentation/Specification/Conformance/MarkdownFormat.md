# Conformance Markdown format specification

This document is the normative authoring-format specification for portable Game
Event Script conformance suites. The key words **must**, **must not**,
**required**, **should**, and **may** are to be interpreted as normative
requirements.

The format is intended to be implemented independently in C#, Swift, Kotlin,
C++, and other ports. It is deliberately a small structural Markdown scanner
plus a strict YAML subset. It is not CommonMark plus full YAML.

Conformance Markdown is test data. It is not the product message/wire format.

## Encoding and logical lines

- A document is strict UTF-8. An optional UTF-8 BOM is recognized only at byte
  offset zero and is not part of the document content. The same byte sequence
  elsewhere represents the ordinary Unicode scalar U+FEFF and is retained,
  including inside a GES source payload.
- `LF`, `CRLF`, and `CR` are recognized as logical line endings. A parser
  interprets all three as `LF`; other Unicode line separators are ordinary text.
- Source ranges are offsets and lengths into the original UTF-8 byte sequence,
  excluding an optional BOM. This permits a received writer to preserve all
  unrelated bytes exactly.
- Line and column diagnostics are one-based Unicode-scalar positions. A tab is
  one scalar for column counting.
- Structural markers are recognized only at column zero and only outside a
  fenced block. Their ASCII spelling and case are exact.
- A document containing invalid UTF-8, an unclosed recognized fence, or a NUL
  byte is invalid.

## Structural Markdown profile

Only the following Markdown structures carry V1 semantics:

- the YAML frontmatter at the beginning of the file;
- headings whose line starts exactly with `## Test: `;
- the reserved heading `## Fixtures` before the first test;
- the exact heading `### Steps` inside a test;
- fenced blocks with one of the recognized info strings below;
- the pipe table immediately following `### Steps`.

All other headings, paragraphs, lists, block quotes, callouts, and code blocks
are prose. They are retained by the source document but ignored by the parser.
An unrecognized fenced block whose info string starts with `ges` inside a test
is an error, because it is likely a misspelled semantic block.

### Canonical human-readable layout

Normative suites in the checked-in corpus must use a single H1 display title
immediately followed by this caution callout:

```markdown
> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.
```

A short suite-level explanation follows the callout. Every test must be
preceded by a `---` thematic break, followed by its `## Test: ` heading and a
short prose explanation of the behavior under test. Semantic blocks use these
H3 display headings:

| Heading | Content that follows |
| --- | --- |
| `### Case description` | the `gesBlock: case` YAML fence |
| `### Source code under test` | one or more `ges` source fences |
| `### Steps` | the executable Steps table |
| `### Expectation` | the `gesBlock: expect` YAML fence |
| `### Expected Game Event Script Assembler` | the `gesa` snapshot fence |

Except for the already semantic `### Steps` heading, these presentation
elements remain nonsemantic in V1. A conforming parser must derive behavior from
the semantic fences and Steps table rather than from prose or presentation
headings.

### Frontmatter

After the optional BOM, the first logical line must be exactly `---`. The next
line that is exactly `---` closes the frontmatter. `...` is not a closing
marker. The content between the markers is one YAML-subset mapping. No content
may precede the opening marker.

### Test boundaries

A line of the form

```text
## Test: Display title
```

starts a test. The title is trimmed of surrounding ASCII space and tab and must
not be empty. It is a display value only. It may change without changing test
identity.

The test continues up to, but does not include, the next `## Test: ` heading or
the end of the document. Other H2 headings do not end the test. A suite must
contain at least one test.

### Fixtures

`## Fixtures` may occur at most once and only before the first test. Everything
from that heading to the first test is nonnormative documentation, including
tables and recognized-looking code examples. A V1 parser must not materialize,
resolve, substitute, or execute these values.

A future matrix feature must use a new explicitly versioned construct. No later
format version may silently reinterpret a V1 `## Fixtures` section as executable
data.

### Fenced blocks

A semantic fence inside a test starts with exactly three backticks at column
zero, followed by one of these exact info strings, and ends with exactly three
backticks at column zero:

| Info string | Meaning | Cardinality |
| --- | --- | --- |
| `yaml` with `gesBlock: case` | Case metadata and execution configuration | exactly one per test |
| `ges` | One GES source input | kind-dependent |
| `yaml` with `gesBlock: expect` | Structured expectations | zero or one, kind-dependent |
| `gesa` | Expected Game Event Script Assembler dump | exactly one for `bytecodeSnapshot` |

Opening or closing fences may not have trailing whitespace. Semantic fences may
not be indented or nested. The payload is the sequence of logical content lines
joined by `LF`; the structural line ending immediately before the closing fence
is not part of the payload. An author can represent a terminal payload newline
by leaving an additional empty content line before the closing fence.

GES source and GESA payloads are passed on with logical `LF` line endings. Every
exact `yaml` fence inside a test is semantic, uses the YAML subset below, and
must contain exactly one root discriminator named `gesBlock`. Its value is
exactly `case` or `expect`. Additional fence text such as `yaml ges-case` is not
part of V1 and is rejected as an unknown semantic fence. YAML fences outside a
test, including those under `## Fixtures`, remain ordinary documentation.

## Portable YAML subset

The frontmatter, semantic `yaml` blocks, and YAML flow values all use the same
restricted YAML 1.2-inspired profile.

### Supported forms

- one root mapping;
- block mappings and block sequences using exactly two spaces per indentation
  level;
- flow mappings `{ key: value }` and flow sequences `[value, value]` on one
  logical line;
- plain, single-quoted, and double-quoted scalar values;
- comments beginning with `#` outside quotes when `#` is the first non-space
  character or is preceded by whitespace;
- lowercase `null`, `true`, and `false`;
- signed base-10 integers with grammar `-?(0|[1-9][0-9]*)`;
- finite decimal values with JSON number grammar. A decimal token is retained
  losslessly until its schema field converts it to Int64, UInt64, or Binary64.

Block mapping keys must be a plain token matching
`[A-Za-z][A-Za-z0-9_.-]*` or a quoted string. Keys are case-sensitive. Duplicate
keys are invalid after quoted-string decoding. Tabs are invalid in indentation.
Trailing whitespace has no meaning.

Double-quoted strings support the JSON escapes `\"`, `\\`, `\/`, `\b`, `\f`,
`\n`, `\r`, `\t`, and `\uXXXX`; a valid surrogate pair represents one Unicode
scalar and an unpaired surrogate is invalid. A single quote inside a
single-quoted string is written as `''`. Plain strings must be quoted when they
would otherwise be resolved as null, boolean, or number. Values beginning with
`#`, `&`, `*`, `!`, `%`, `@`, or a backtick must be quoted.

### Explicitly unsupported forms

Anchors, aliases, merge keys, tags, directives, block scalars (`|` and `>`),
complex keys, explicit `?` keys, multiple YAML documents, implicit dates,
sexagesimal numbers, non-decimal integers, `.nan`, `.inf`, and implementation-
specific scalar resolution are invalid. A parser must report them; it must not
silently accept a larger host-library YAML dialect.

Schema mappings are closed. An unknown property is an error unless this
specification explicitly declares it open. Property names are always matched
case-sensitively.

The mappings keyed by declared IDs or canonical names are intentionally open:
step expectations by Step ID, performance profiles by Profile ID, metrics by
Metric ID, and opcode `counts`/`minimumCounts` by canonical opcode name. Their
keys and values are still fully validated by the enclosing schema.

## IDs, suite frontmatter, and inheritance

An ID component matches:

```text
[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*
```

`suiteId` and each case-local `id` use this grammar. The full case ID is
`suiteId + "/" + case.id`. Suite IDs must be unique in a corpus and case IDs
must be unique in a suite. IDs are never derived from a path, heading, title, or
testframework method.

The frontmatter root supports these fields:

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `formatVersion` | integer | yes | must be `1` |
| `suiteId` | string | yes | stable suite identity |
| `title` | string | no | display title; otherwise `suiteId` |
| `kind` | test-kind string | no | default test kind |
| `level` | `atomic` or `scenario` | no | default level |
| `categories` | ID sequence | no | default runner/framework categories |
| `tags` | string sequence | no | default tags |
| `requires` | capability requirement | no | default capabilities |
| `compile` | compile options | no | default compile options |
| `runtimeLimits` | runtime-limit mapping | no | default runtime limits |
| `comparison` | comparison options | no | default value comparison |

The same defaultable fields may occur in the `gesBlock: case` mapping. Resolution
follows these rules:

- a test scalar replaces the suite scalar;
- `compile`, `runtimeLimits`, and `comparison` are overlaid by field, with test
  fields replacing fields of the same name;
- suite categories/tags followed by test categories/tags are concatenated and
  then de-duplicated by exact ordinal spelling while retaining first occurrence;
- core and optional capability lists are combined and de-duplicated in the same
  way;
- all other case fields are never inherited.

`level` must resolve to a value; it is never inferred from a directory. `kind`
must also resolve to a supported value.

A capability requirement has this shape:

```yaml
requires:
  core: [compiler, host, vm, external-types]
  optional: []
```

Core capabilities required by a test kind are added implicitly and cannot be
downgraded to optional. Capability behavior is defined by
[Runner](Runner.md).

## Case metadata

Every test contains exactly one `yaml` root mapping whose first-class
discriminator is `gesBlock: case`. It supports:

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `gesBlock` | `case` | yes | identifies this YAML block as case metadata |
| `id` | ID | yes | stable local identity |
| `kind` | test-kind string | inherited | kind override |
| `level` | level string | inherited | level override |
| `categories` | ID sequence | no | additional categories |
| `tags` | string sequence | no | additional tags |
| `requires` | capability requirement | no | additional requirements |
| `compile` | mapping | no | compile options |
| `runtimeLimits` | mapping | no | host runtime-limit overrides |
| `comparison` | mapping | no | value comparison override |
| `sources` | source descriptor sequence | conditional | names and program grouping |
| `random` | mapping | no | deterministic random configuration |
| `publishSink` | `accept`, `absent`, `reject`, or `throw` | no | configured publish-sink behavior; default `accept` |
| `externalTypeRegistry` | `environment`, `absent`, or `mismatch` | no | runtime registry used for External-Type link tests; default `environment` |
| `hostCount` | positive integer | no | run the same compiled Programs and expectations independently in this many Hosts; default `1` |
| `deferredPrograms` | ID sequence | no | source-program groups loaded only by a native host action |
| `nativeHandlers` | sequence | no | declarative portable native handlers |
| `stepActions` | mapping | no | ordered host actions performed immediately before a named step's Receive |
| `messageApi` | mapping | `messageApi` only | signature and message input |
| `valueApi` | mapping | `valueApi` only | portable value construction, comparison and copy input |
| `externalTypeApi` | mapping | `externalTypeApi` only | portable external-type catalog input |
| `binaryFixture` | mapping | `programBinary` only | immutable `.gesb` fixture manifest entry |
| `performance` | mapping | `performance` only | workload configuration |

Compile options are:

```yaml
compile:
  debugInfo: [debugSymbols, sourceMap, sourceArchive]
  binaryRoundTrip: false
```

`debugInfo` is a sequence containing each listed value at most once. An empty
sequence means no debug segments. Its default is all three values.
`binaryRoundTrip` defaults to `false`; when true, the compiled program is
written as canonical `.gesb`, read again, and only then used by the test.
Enabling it implicitly requires the core capability `program-binary`.

`runtimeLimits` uses the exact portable names `maxProcessedEventsPerRun`,
`maxQueuedMessagesPerRun`, `maxExecutionSteps`, `maxRegisterValues`,
`maxLoopIterations`, `maxCallDepth`, `maxRandomScopeDepth`, `maxRangeItems`,
`maxGeneratedCollectionItems`, `maxDiceCount`, and `maxDiceSides`. Values are
positive integers. Omitted fields use Core defaults.

`comparison` is:

```yaml
comparison:
  binary64:
    mode: exact
```

or:

```yaml
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
```

Exact comparison is the default. ULP mode is allowed only where platform math
or another documented operation requires it. Kind, unit, integer, text,
boolean, tag, ordering, and structure remain exact in both modes.

`random` contains exactly one of `seed` (signed Int64) or `sequence` (a
non-empty sequence of canonical Binary64 strings). Tests that execute a random
operation must supply deterministic random configuration. V1 test kinds do not support expectations based on nondeterministic behavior.

### Sources and programs

The order of `ges` fences is compiler input order. If `sources` is absent,
exactly one `ges` fence is required and receives source name
`<suiteId>.<caseId>.ges` and program ID `main`.

If `sources` is present, it must contain exactly one descriptor per `ges` fence:

```yaml
sources:
  - name: rules.ges
    program: rules
  - name: helpers.ges
    program: rules
  - name: opponent.ges
    program: opponent
```

`name` is a non-empty portable source name, not a path to open. `program` is an
ID and defaults to `main`. Sources with the same program ID are added to one
compiler builder in descriptor order. Programs are loaded into the host in the
order their ID first occurs. Interleaving descriptors for different programs is
valid but should be avoided for readability.

No parser or runner opens a source path. For `programBinary`, source fences are
provenance: they record the logical compiler input named by the manifest but are
not compiled or substituted by the runner. External binary bytes use only the
resource-resolver contract defined below.

### Binary fixture manifest entries

Each `programBinary` test contains exactly one `binaryFixture` mapping:

```yaml
binaryFixture:
  id: gesb-v1-valid-runtime
  resourceId: gesb-v1.valid-runtime
  relativePath: GesbV1/valid-runtime.gesb
  sha256: 594808EB171F039AF2182A22360C39183CC4960412EF3D76B97E3632603EECF8
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
  derivation: optional human-readable transformation description
```

`id` and `resourceId` are stable portable IDs. `resourceId` is the only value
given to the injected resolver. `relativePath` is packaging metadata using
forward slashes; it must be relative, contain no empty, `.` or `..` component,
and is never opened by the parser or runner. `sha256` is exactly 64 uppercase
hexadecimal digits and identifies the complete resource bytes. Compiler ID and
version, resolved `compile` options, source fences, and the required
`programVersion` record reproducible provenance. `compareCompiledRuntime`
defaults to false; when true it requires source and `compiler`, compiles exactly
one source Program group with the recorded ProgramVersion, and compares only
the canonical Required Runtime sections. BuildMetadata and optional debug/source
sections therefore may differ between language compilers. `derivation` is used
only when helpful to explain a deliberately altered fixture and is
non-executable.

### Declarative native handlers

The V1 shape preserves portable host-dispatch cases and supports a closed set of
host-lifecycle actions:

```yaml
nativeHandlers:
  - id: notify-handler
    message: Notify
    parameters: [playerId, count]
    messageName: false
    priority: 0
    initiallySubscribed: true
    throw: false
    actions:
      - loadProgram: deferred-rules
      - subscribeHandler: late-handler
    emit:
      - name: ExternalSeen
        forwardArguments: true
```

`message` is required. `messageName: true` selects name-only matching and then
`parameters` must be empty; its default is false. `id` defaults to the stable metadata-order name
`native-0001`, `native-0002`, and so on. `parameters` defaults to an empty
ordered sequence, `priority` to zero, `initiallySubscribed` to true, `throw` to
false, and `actions`/`emit` to empty. An emit entry requires `name` and exactly
one of `forwardArguments: true` or an ordered `args` sequence.

Each action mapping contains exactly one of `loadProgram`, `detachProgram`,
`subscribeHandler`, or `unsubscribeHandler`, plus an optional boolean
`expectResult`. Its operation value is an existing program
or native-handler ID of the appropriate kind. Only a program listed in
`deferredPrograms` may be a `loadProgram` target. Actions execute in metadata
order before emits. `expectResult` compares the portable boolean operation
result. Repeated detach/unsubscribe operations return false after the first
state change; load/subscribe ensure the target is active and return true. The
set is intentionally closed and declarative; arbitrary native code is not test
data.

A `scriptApi` case with at least one native handler may have no GES source and
thereby defines a native-only Host. `hostCount` is bounded by the parser's
`MaxHostsPerTest`; every Host loads the same immutable compiled Program objects,
but owns independent Context, queue, random generator and VM state. Expectations
are compared independently for every Host. This option is intended for Program
reuse and isolation tests, not performance measurement.

`publishSink` configures the fixed test sink. `absent` installs no sink;
`accept` records the call and returns true; `reject` records the call and returns
false; `throw` records the call and throws a platform exception which the Host
must convert to the portable `runtime.publishSinkFailure` diagnostic. Outbound
messages record calls handed to the sink, regardless of acceptance.

`externalTypeRegistry` leaves the runner environment's registry unchanged for
`environment`, installs no runtime registry for `absent`, and installs a fixed
constructor with a deliberately different definition for `mismatch`. The
compiler still uses the environment's portable catalog in all three modes.

`stepActions` is a mapping from an existing Step ID to the same ordered,
closed host-action sequence used by native handlers. These actions execute
immediately before that step's `Receive`. This makes enqueue/load ordering and
changes while a VM is paused expressible without embedding test callbacks.

## Ordered steps

Runtime and performance tests express chronological input in exactly one table
under an exact `### Steps` heading. After optional blank lines, its header and
delimiter are exactly:

```text
| step | receive | pump | budget |
| --- | --- | --- | --- |
```

Each following nonblank pipe-table row has four cells:

- `step` is an ID unique within the case;
- `receive` is the exact incoming message name;
- `pump` is `completion`, `frames`, `enqueue`, or `frame`;
- `budget` is empty for `completion` and `enqueue`, and a positive UInt32 for
  `frames` and `frame`.

Cells are trimmed of surrounding ASCII space and tab. Backslash escapes, inline
Markdown, multiline cells, and additional columns are not supported. A table
ends at the first blank or non-table line. At least one row is required whenever
the test kind requires steps.

For `completion`, the runner calls the synchronous run-to-completion operation
once. For `frames`, it repeatedly executes frames of the specified budget until
the host is idle or reports a runtime limit. The expectation records whether at
least one frame returned `paused`. `enqueue` performs no pump after `Receive`.
`frame` performs exactly one `ExecuteFrame(budget)` call and records whether
that result was paused.

The corresponding `yaml` block with `gesBlock: expect` keys its detailed input
and outputs by step ID:

```yaml
gesBlock: expect
steps:
  add:
    input:
      tags: []
      args:
        - name: value
          value: { type: ":integer", value: "7" }
    accepted: true
    local:
      - name: Done
        args:
          - name: result
            value: { type: ":integer", value: "12" }
    outbound: []
    paused: false
    runtimeLimits:
      include: []
      exclude: []
    diagnostics: []
    trace:
      - event: publish
        message: { name: Remote }
        result: { localAccepted: true, outboundAttempted: true, outboundAccepted: false, anyAccepted: true }
```

`input.tags` and `input.args` default to empty. `accepted` defaults to true;
`local`, `outbound`, runtime-limit lists, and diagnostics default to empty.
`paused` is optional and is not compared when absent. A step expectation key
must name a table row, and every table row may have at most one expectation
entry. Message lists and argument lists are order-sensitive. `local` observes
both Emit and the local half of Publish; `outbound` observes messages handed to
the configured publish sink in call order.

Each `runtimeLimits.include` or `.exclude` entry may constrain `name`,
`detailContains`, and `limit`; at least one field is required. The explicit
wildcard `{ any: true }` may be used alone, most commonly in `exclude`, to match
every runtime-limit observation. Each diagnostics
entry uses the portable diagnostic expectation shape from the error kinds
below, except that `phase` and `code` remain required. Entries and observations
are compared in order; exclusions must not occur anywhere in the observation
sequence.

`trace` is optional. When absent, observer callback order is not compared. When
present, including as `[]`, it is an exact ordered expectation over all observer
callbacks in that channel interval. Supported event shapes are:

```yaml
trace:
  - event: emit
    message: { name: Local }
    accepted: true
  - event: publish
    message: { name: Remote }
    result:
      localAccepted: true
      outboundAttempted: true
      outboundAccepted: false
      anyAccepted: true
  - event: dispatchStarted
    message: { name: Start }
    signatureId: "Start()"
  - event: dispatchCompleted
    message: { name: Start }
    signatureId: "Start()"
  - event: runtimeLimit
    runtimeLimit: { name: MaxLoopIterations, limit: 10 }
  - event: diagnostic
    diagnostic: { phase: runtime, code: runtime.publishSinkFailure }
```

Emit requires the complete `accepted` flag. Publish requires all four fields of
`GameEventScriptPublishResult`; `outboundAccepted` implies
`outboundAttempted`, and `anyAccepted` must equal `localAccepted OR
outboundAccepted`. Dispatch events require the exact dispatched message and
signature ID. Runtime-limit and diagnostic constraints use the same shapes as
their standalone expectation lists. Message/value comparison uses the case's
ordinary Binary64 comparison mode.

An optional top-level `initialization` expectation has `local`, `outbound`,
`runtimeLimits`, `diagnostics`, and optional exact `trace` with the same
meanings. It describes the
single run-to-completion pump performed after all programs and native handlers
are installed and before the first step. An absent `initialization` mapping is
equivalent to all four empty expectations.

## Portable message and value shape

A message is:

```yaml
name: Done
tags: [combat]
args:
  - name: result
    value: { type: ":integer", value: "12" }
```

`name` is required; `tags` and `args` default to empty. Arguments are always an
ordered sequence. A mapping keyed by argument name is invalid.

Values use the existing portable conformance shape:

| `type` | Additional fields |
| --- | --- |
| `:nothing` | none |
| `:text`, `:tag` | `value` string |
| `:boolean` | `value` boolean |
| `:integer` | canonical signed-Int64 `value` string; optional `unit` |
| `:float` | canonical Binary64 `value` string; optional `unit` |
| `:percentage` | canonical Binary64 ratio `value` string |
| `:vector`, `:point` | Binary64 strings `x`, `y`, `z`; optional `unit` |
| `:list` | ordered `items` value sequence |
| `:map` | `entries`, an ordered sequence of `{ key, value }` |
| `:dice` | ordered Int32 `rolls` |
| `:range` | numeric strings `from`, `to`, `step`; optional `rangeKind` is `integer` or `float` |
| `:message` | nested `message` |
| any declared custom type | ordered `entries` sequence |

Numeric strings and special values follow [Number semantics](../Semantics/Numbers.md).
Message names, argument names, tags, units, and custom types follow the portable
Core contracts. Map/record semantic comparison follows
[Determinism](../Semantics/Determinism.md). Duplicate construction keys use
last-entry-wins semantics before scalar-ordinal key sorting.

## Expectation fields by test kind

Supported V1 kinds are `scriptApi`, `compileError`, `loadError`, `messageApi`,
`valueApi`, `externalTypeApi`, `compileMetadata`, `bytecode`, `performance`, and
`bytecodeSnapshot`, and `programBinary`. A kind's
required core capabilities are additive to explicit `requires.core`.

The presence of `nativeHandlers` implicitly requires `native-handlers`.
`compile.binaryRoundTrip: true` implicitly requires `program-binary`. Source
that uses an external type must explicitly require `external-types`; the runner
does not infer capabilities by parsing GES source.

### `scriptApi`

The source-backed form requires `compiler`, `host`, `vm`, `observer`, and
`publish-sink`; it requires at least one source and either a Steps table or an
explicit `initialization` expectation. It uses the common step expectation
shape above.

The native-only form with no source instead requires only `host`, `observer`
and the implicitly added `native-handlers` capability. It does not require a
compiler, VM or publish sink because the closed V1 native actions can only emit
locally. It still requires Steps or an explicit initialization expectation.

### `compileError` and `loadError`

`compileError` requires `compiler`; `loadError` requires `compiler`, `host`, and
`vm`. Each requires source and a `yaml` block with `gesBlock: expect` containing:

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.missingCallable
  symbol: missing
  symbolKind: function
  sourceName: sample.ges
  line: 3
  column: 7
  endLine: 3
  endColumn: 14
  programName: Sample
  handlerName: Start
```

`phase` and `code` are required. Other fields are optional exact constraints.
Human-readable messages and technical details cannot be expectations. The
diagnostic contract is [Diagnostics](../Diagnostics.md).

### `messageApi`

Requires `message-api` and no GES source. The `gesBlock: case` mapping contains:

```yaml
gesBlock: case
messageApi:
  signature:
    name: Start
    parameters: [a, b]
  message:
    name: Start
    args: []
  compareSignature: { name: Start, parameters: [a, b] }
  compareMessage: { name: Start, args: [] }
  compareHandler: { name: Start, parameters: [a, b] }
  createArguments: []
```

For the single negative shape test `args` may instead be a mapping. The mapping
is retained as ordered source data in the normalized model, but it is never
accepted as message arguments. This form is valid only when the expectation is
exactly `error: invalidArgumentsShape`; every constructible message continues
to require an ordered argument sequence.

Its expectation contains at least one of:

```yaml
message:
  name: Start
  signatureId: "Start(a,b)"
  messageSignatureId: "Start()"
  matches: false
  argumentCount: 0
  signatureEquals: true
  signatureHashEquals: true
  messageEquals: true
  messageHashEquals: true
  handlerEquals: true
  handlerHashEquals: true
  createdMessageSignatureId: "Start(a,b)"
  error: duplicateArgumentName
```

If `error` is present, successful construction fails the case and the other
fields must be absent. Message error codes are stable ASCII identifiers.

### `valueApi`

Requires `value-api` and no GES source. `valueApi.value` is constructed through
the portable public value API. Optional `equalTo` and `notEqualTo` values test
semantic equality. `mutateSourceAfterCreate: true` mutates source arrays after
construction to verify defensive storage.

```yaml
valueApi:
  value:
    type: ":list"
    items: [{ type: ":integer", value: "1" }]
  equalTo:
    type: ":list"
    items: [{ type: ":integer", value: "1" }]
  mutateSourceAfterCreate: true
```

The expectation requires `normalized` and may constrain flags, boolean
conversion, length, custom type name, equality and the equal-value hash
invariant.

```yaml
value:
  normalized:
    type: ":list"
    items: [{ type: ":integer", value: "1" }]
  hasValue: true
  length: 1
  equal: true
  equalHash: true
```

### `externalTypeApi`

Requires `external-types` and no GES source. V1 constructs a catalog from the
ordered `typeNames` list. Its expectation contains either `typeCount` or the
stable error `duplicateTypeName`.

```yaml
externalTypeApi:
  typeNames: [sample, sample]
```

```yaml
externalType:
  error: duplicateTypeName
```

### `compileMetadata`

Requires `compiler` and source. Its expectation contains one or more of:

```yaml
metadata:
  messageDefinitions:
    - name: Start
      count: 1
      signatureIds: ["Start(value)"]
  programResources:
    requiredRegisterCount: 7
    requiredCallStackDepth: 2
  handlerResources:
    - name: Start
      signatureId: "Start(value)"
      requiredRegisterCount: 7
      requiredCallStackDepth: 2
```

Every present property is an exact constraint. Sequence order is significant
where the underlying program contract defines order.

### `bytecode`

Requires `compiler` and source. Its expectation is:

```yaml
opcodes:
  contains: [Multiply, EmitMessage]
  excludes: [Move]
  counts: { RandomTake: 1 }
  minimumCounts: { PropertyAccess: 7 }
```

At least one constraint is required. Opcode names are exact canonical names.
`contains` means count greater than zero; `excludes` means zero.

### `bytecodeSnapshot`

Requires `compiler`, the optional capability `bytecode-snapshot`, and source. It
has exactly one `gesa` block and no `gesBlock: expect` block. The compiler's
canonical dumper output and the block payload are normalized to logical `LF`
and compared byte-for-byte as UTF-8. No whitespace, address, symbol,
source-comment, or metadata field is ignored.

### `programBinary`

Requires `program-binary`, one `binaryFixture` mapping, and one binary
expectation. Source fences are optional provenance and are never compiled.
`compile.binaryRoundTrip` is invalid because the input is already binary.

```yaml
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 594808EB171F039AF2182A22360C39183CC4960412EF3D76B97E3632603EECF8
  moduleName: BinaryFixture
  requiredRegisterCount: 3
  requiredCallStackDepth: 0
  opaqueSectionCount: 0
```

`outcome` is exactly `valid`, `readError`, or `validationError`. Read errors
cover header, framing, section representation, strict UTF-8, and bounded table
materialization (`GameEventScriptProgramFormatErrorCode` 1 through 21).
Validation errors cover cross-reference, opcode/operand, call-graph, resource,
and debug/source invariants (codes 22 through 38). Error outcomes require the
canonical `errorCode` name and may constrain `byteOffset`, `sectionType`, and
`entryIndex`; they reject all success fields.

A valid outcome may constrain the fields above. `rewriteByteExact` compares the
fixture with a fresh canonical writer result, while `rewriteSha256` identifies
that result independently of whether the input order was canonical. The
fixture's `programVersion` is always compared. When BuildMetadata is present,
its compiler identity is compared with the manifest; runtime behavior never
depends on it.

A valid case may contain Steps and ordinary step expectations. That form also
requires `host`, `vm`, `observer`, and `publish-sink`; the parsed fixture Program
is loaded and executed instead of compiling the provenance source. Error cases
cannot contain Steps. Initialization expectations are valid only together with
Steps.

### `performance`

Requires the same core capabilities as `scriptApi` plus the optional capability
`performance`. It uses ordinary sources, steps, and correctness expectations.
Its case metadata additionally contains:

```yaml
performance:
  iterations: 1000
  warmupIterations: 10
  compileWarmupIterations: 3
```

All counts are non-negative UInt32 and `iterations` is positive. The expectation
adds profile-specific baselines:

```yaml
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.per-invoke-elapsed:
          reference: 0.002669
          unit: ms
          toleranceRelative: 0.20
          toleranceAbsolute: 0.001
        run.per-invoke-allocated:
          reference: 0.172
          maximum: 0.172
          unit: KiB
```

Profile IDs use the normal ID grammar. Metric IDs use the same grammar with
additional dot-separated components. V1 performance metrics are lower-is-better.
Units are `ns`, `us`, `ms`, `s`, `B`, `KiB`, `count`, or those units followed
by `/iteration`. One `KiB` is exactly 1024 bytes.

`reference` is required and is the value updated in received output. At least
one of `maximum`, `toleranceRelative`, or `toleranceAbsolute` is required.
Relative tolerance is a non-negative ratio, not a percentage. The permitted
upper bound is the smallest supplied bound among `maximum`,
`reference * (1 + toleranceRelative)`, and
`reference + toleranceAbsolute`. A measured value at or below the bound passes;
an improvement never fails. All values are finite non-negative Binary64 values.

Workload and correctness are shared across ports. Profiles and their baselines
may be language-, runtime-, configuration-, OS-, and architecture-specific.

## Normalized immutable document model

Parsing and schema validation produce one immutable normalized document with:

- format version and one suite with resolved defaults;
- cases in document order, each with stable local and full ID, display title,
  resolved kind, level, categories, tags, capabilities, options, source inputs,
  steps, and typed expectations;
- messages and values in their explicit portable ordered forms;
- original UTF-8 source ranges for the frontmatter, every test, every semantic
  block, each performance `reference` scalar, and the `gesa` payload.

The normalized model contains no Markdown AST, YAML AST, filesystem path,
testframework object, executable delegate, or platform exception. All exposed
collections are immutable snapshots. Parsers may retain the original document
bytes only as an explicit immutable source-document value used for received
output.

## Invalid documents and versioning

Structural, YAML, schema, duplicate-ID, cross-reference, and kind/cardinality
violations are errors. The parser returns no executable partial document.
Diagnostics have stable codes, original UTF-8 byte ranges, and line/column
locations. At minimum, implementations distinguish:

- `conformance.markdown.invalidUtf8`, `.missingFrontmatter`,
  `.unterminatedFrontmatter`, `.invalidTestHeading`, `.unterminatedFence`,
  `.unknownSemanticFence`, and `.invalidStepsTable`;
- `conformance.yaml.syntax`, `.unsupportedFeature`, `.duplicateKey`,
  `.invalidScalar`, and `.limitExceeded`;
- `conformance.schema.unknownField`, `.missingField`, `.invalidValue`,
  `.duplicateId`, `.unknownReference`, `.invalidCardinality`,
  `.unknownKind`, and `.unsupportedVersion`.

Format version 1 is closed. A parser that does not support the declared version
must reject the document. New fields or semantics require an explicitly
documented compatible revision strategy or a new format version; they must not
be inferred from prose.

## Minimal example

````markdown
---
formatVersion: 1
suiteId: runtime.math
title: Runtime math
kind: scriptApi
level: atomic
categories: [conformance]
tags: [runtime, math]
requires:
  core: [compiler, host, vm]
---

# Runtime math

## Test: Integer addition

```yaml
gesBlock: case
id: integer-add
```

```ges
on Start(value) {
  let result be value + 5
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| add | Start | completion | |

```yaml
gesBlock: expect
steps:
  add:
    input:
      args:
        - name: value
          value: { type: ":integer", value: "7" }
    local:
      - name: Done
        args:
          - name: result
            value: { type: ":integer", value: "12" }
```
````
