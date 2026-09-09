<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Host runtime specification

This document defines the portable runtime boundary shared by
Swift, Kotlin, C++, C#, and Unity implementations. C# is the
reference implementation. Language-specific threading and reflection
must stay outside the portable core.

## Responsibilities

`GameEventScriptProgram` is the immutable parsed `.gesb` artifact. It contains
portable runtime segments and optional debug/source/build/opaque segments, but
no VM state, random generator, delegate, host binding, or queue. The normative
container is defined by [Binary format](BinaryFormat.md). A program may be loaded into multiple hosts at
the same time. All public program segments copy their input and expose read-only views. Its
resource metadata contains only portable integers: per-handler requirements in
message-handler binds and their maxima in the program header model.

`GameEventScriptHost` is one autonomous serial execution unit. It owns the local
message queue, exact- and name-subscription indexes, private random stream,
runtime limits, observer, optional outbound sink, linked program instances, and
at most one reusable `GesVmState`. A host may contain only native handlers and
does not create a VM until the first program is loaded. Host construction accepts
a seed or an immutable sequence configuration, never a live mutable generator;
building two hosts from the same configuration creates two independent streams.

`GameEventScriptInstance` is one host-specific link of one program. `Load` is
additive and returns an instance. Linking resolves extension and external-type
references against that host. The host assigns a stable, non-reused registration
ID. The instance retains only its host and ID for lifecycle operations; it does
not retain a detach closure. `Detach` is idempotent and removes the instance from
future dispatch snapshots. Before registering anything, loading rejects a cyclic
synchronous call graph or requirements above `MaxRegisterValues` and
`MaxCallDepth`. It then pre-warms the reusable register storage from the program
maximum.

`GameEventScriptSubscription` represents one native registration. `Unsubscribe`
uses the same host-owned registration-ID model, is idempotent, and affects future
snapshots only. Registration IDs are host-local lifecycle identities and are not
wire or program identities.

Portable native handlers implement `IGameEventScriptNativeMessageHandler` and
receive the message plus the host's context through `Handle`. The Core stores the
handler object directly and contains no `Action` delegate adapter. C# callers may
use the `Action<GameEventScriptMessage, GameEventScriptContext>` convenience
overloads in `CSharpBridge`; other ports provide their own language-idiomatic
adapters without changing the Core contract.

`GameEventScriptContext` is created once per host and passed to native handlers
and extensions. It exposes the host random stream, limits, `Emit`, and `Publish`.
Its random generator supports nested `Push()`, `Push(Int64)`, and `Pop()` scopes.
It does not own a queue, VM, or scheduler. A context or its random generator must
not be retained for asynchronous use after a native or extension callback.

`GameEventScriptVirtualMachine` is a stateless executor. `GesVmState` contains
only the currently resumable execution: active linked program, instruction
pointer, registers, frames, stages, and active message. Random state and its
scope stack belong to the host-owned generator so extensions observe the same
active scoped stream as bytecode. The host
reuses this state serially for every script handler and fully resets it after
completion or failure. VM pooling across hosts is not part of this architecture.

All portable failures follow [Diagnostics](Diagnostics.md). Runtime handler failures
are reported to the observer, abort/reset only the failing handler, and leave the
remaining immutable dispatch snapshot runnable. A pump result that observed a
handler failure uses `RuntimeError` and carries its first diagnostic. Successful
VM stepping, resume, and dispatch do not allocate diagnostic objects.

## Program Load Atomicity

`Load` must either register the complete instance and enqueue its initialization
snapshot, or reject the operation without changing the host's registrations,
queued messages, active dispatch, VM state, or random stream. Validation and
dynamic linking complete before the host commits any registration.

When the Program has initialization handlers, their single logical-message
snapshot requires one queue slot. After linking and before registering handlers
or preparing VM storage, `Load` checks that this slot is available under
`MaxQueuedMessagesPerRun`. A full queue rejects the load with the structured
link diagnostic `link.initializationQueueFull`. The caller receives no instance,
and no initialization message is dropped or deferred implicitly. This rejected
load does not emit a queue runtime-limit observation or fault an active handler
whose native caller handles the link failure.

The active message does not occupy a queued-message slot. One remaining slot is
sufficient, and a Program without initialization handlers needs no queue slot.
A nonpositive queue limit permits loading without this capacity restriction.
Successful initialization keeps its position after older queued messages and
before later messages. Existing captured dispatch snapshots remain unchanged.

After pending messages have been processed, the caller may retry the same
immutable Program. A successful retry creates one instance and queues its
initialization exactly once. These rules also apply when loading inside a native
callback or between frames of a paused script handler.

## Random Ownership, Boundaries, and Limits

`MaxRandomScopeDepth` is the exact number of simultaneously active regular
random scopes permitted in one host. Zero permits no nested scope. On first use,
the generator allocates that many parent-state slots plus one reserved fault-gate slot;
the gate does not reduce the configured usable depth. Runtime boundary markers
are separate metadata and do not count as random scopes.

The host marks the random scope depth when every native or script handler starts.
An extension call adds a nested marker. A script marker remains active while its
VM execution is paused between frames, and may therefore be released on a
different thread from the one that created it. Markers and scope state are
ordinary host-owned heap state; they never use thread-local, async-local, or
native call-stack storage.

The first push beyond `MaxRandomScopeDepth` copies the current valid stream into
the reserved gate slot, latches a `MaxRandomScopeDepth` runtime-limit fault, and
uses the current state only as disposable work until the active boundary returns.
Further pushes increment only a suppressed-depth counter and matching pops
decrement it; neither operation may reach the valid parent stack. Random draws
after the fault cannot alter the saved stream. `Emit` and `Publish` attempted
while the gate is active are observed as rejected, and Publish does not invoke
the outbound sink.

At boundary release the generator restores the gate state, unwinds every regular
scope above the marker, and clears the gate. Bytecode observes a rejected push
immediately and stops the current handler. An extension or native handler is an
atomic trusted callback and cannot be preempted portably; the VM or host stops
the handler as soon as that callback returns. No exception is part of this
contract. The observer receives one runtime-limit event and the pump returns
`RuntimeLimitReached`; the host remains reusable.

A pop may not cross the innermost runtime marker. Crossing it is a runtime error.
A callback that returns with otherwise balanced execution but leaves scopes open
is also a runtime error, after the marker first restores the parent stream. If a
different runtime limit already aborted a script body before its generated pops,
marker cleanup is expected recovery and adds no second imbalance diagnostic.

## External Type Boundary

External types use two deliberately separate inputs. Compilation receives an
`IGameEventScriptExternalTypeCatalog` containing only declarative type, field,
constructor, parameter, and signature data. It does not contain executable
bindings. The resulting program stores only portable external-constructor import
bindings; it never stores the catalog or its definitions.

Host linking receives an `IGameEventScriptExternalTypeRegistry` that resolves a
constructor import to an `IGameEventScriptExternalTypeConstructor`. A resolved
constructor must report exactly the definition requested by the import. Runtime
values cross the portable boundary as `IGameEventScriptExternalValue`, exposing
their declarative definition and field values without exposing a platform host
object.

The C# bridge may implement both inputs with one convenience object. Its
reflection metadata, attributes, CLR instances, field delegates, conversion,
and constructor invocation remain entirely inside `CSharpBridge`. Portable JSON
conformance uses a manual catalog, constructor registry, and value
implementation and therefore does not depend on C# reflection.

## Message Semantics

- Message arguments are ordered `GameEventScriptMessageArgument` name/value
  pairs. Their order is part of `SignatureId`; neither Core construction nor
  conformance decoding derives it from a map's iteration order.
- Named argument labels must be unique after portable normalization. Repeated
  `_` labels remain valid because they represent distinct positional arguments.
- C# tuple construction is a `CSharpBridge` convenience. Its dictionary adapter
  requires a known `GameEventScriptMessageSignature` and binds values in that
  signature's declared order.
- `Receive(message)` captures current subscription snapshots and queues the
  message locally. It never invokes the outbound sink.
- `Emit(message)` does the same from a running handler.
- `Publish(message)` first attempts the same local enqueue and then calls the
  host's single synchronous `IGameEventScriptPublishSink`, if configured.
- `GameEventScriptPublishResult` reports `LocalAccepted`, `OutboundAttempted`,
  `OutboundAccepted`, and derived `AnyAccepted`. Sink rejection or exception
  never rolls back or corrupts local delivery. The observer sees the result.
- Queue limits count logical messages, not handler invocations.
- Each queued message owns immutable exact/name subscription snapshots and a
  cursor. Every matching handler finishes in priority/registration order before
  the next logical message starts.
- A detach, unsubscribe, subscribe, or load during dispatch does not modify
  already captured snapshots. It applies when a later message is enqueued.
- `on initialization` is captured once per loaded instance and queued at the
  exact `Load` position: after messages already waiting and before messages
  received later.

## Portable State Machine

```text
Idle
  Receive / Emit / Publish / Load(initialization)
    -> enqueue message snapshot
    -> Ready

Ready
  pump
    -> dequeue one logical message
    -> Dispatching

Dispatching
  next native handler
    -> reset per-handler safety budget
    -> invoke atomically
    -> Dispatching

  next script handler
    -> reset per-handler safety budget
    -> bind linked program + entry + message to the host VM state
    -> ScriptRunning

  no remaining handler
    -> complete logical message
    -> Ready when queue is non-empty, otherwise Idle

ScriptRunning
  ExecuteFrame budget remains
    -> execute opcodes synchronously
    -> ScriptRunning or Dispatching

  ExecuteFrame budget exhausted
    -> Paused (VM state is retained)

  handler completes
    -> fully reset VM state
    -> Dispatching

  handler fails
    -> report diagnostic, fully reset VM state
    -> continue Dispatching; pump result is RuntimeError

  handler safety limit reached
    -> reset VM state
    -> RuntimeLimitReached
```

`ExecuteFrame(opcodeBudget)` is a caller-thread scheduler slice. Its opcode
budget is independent from safety limits and may pause only script bytecode;
native handlers remain atomic. `RunToCompletion()` pumps synchronously until the
host becomes idle or a runtime limit stops the run. Core code starts no thread
and performs no synchronization. A host is serial but not thread-affine: only
one caller may access it at a time, while later frames may run on another thread
when the embedding environment supplies the required happens-before handoff.

The processed-message limit counts completed logical messages, after all of
their captured handlers have finished. After completing the configured number,
the pump reports `RuntimeLimitReached` and one corresponding observer event only
if another logical message remains queued. That next message stays queued and
is not started until a later pump call. If completing the last message drains
the queue exactly at the limit, the pump returns `Completed` without a
processed-message-limit observation, unless an independent error or limit
determines the result. These rules apply to both `ExecuteFrame` and
`RunToCompletion`, to native and script handlers, and to messages enqueued by
handlers during the pump.

### Iterator and Generated-Collection Limits

`MaxLoopIterations` counts successful iterator advances across all loops and
iterator-backed selectors in one handler. A present `nothing` item consumes one
iteration, just like any other value. Exhaustion and non-iterable sources consume
no iteration. Exactly the configured number of advances is allowed; attempting
another successful advance stops the handler before its loop body or selector
expression executes. Nested and sequential loops share this counter. Frame
pauses retain it, and starting the next handler resets it.

`MaxGeneratedCollectionItems` bounds each collection incrementally materialized
by generated lists and iterator-backed selectors. The limit applies to retained
items, rather than the number of source items visited or a cumulative total
across separate collections:

- List projections, filters, and generated lists count every appended result,
  including `nothing`.
- Map projections count distinct accepted keys. Replacing an existing key's value
  does not grow the map; skipped empty or `nothing` keys do not count.
- Distinct-by projections count retained distinct keys, so discarded duplicates
  do not count again.
- Order-by projections count every buffered source item.
- Group-by projections limit both the number of groups in the result map and
  the number of items in each group's list, independently.

Exactly the configured collection size is allowed. Before an insertion would
exceed it, the runtime rejects that insertion without growing the collection and
stops the handler without returning a partial projection result. Previously
completed effects remain observable. This is an incremental materialization
limit, not a byte-allocation budget or a bound on existing input collections.

For either limit, a nonpositive value disables that limit. An exceeded limit
produces one structured runtime-limit observation for the failing handler and
stops the pump with `RuntimeLimitReached`. Normal handler cleanup restores random
boundaries and clears VM state. Remaining handlers and queued messages stay
available for a later pump call.

## C# Automatic Runner

`CSharpBridge.GameEventScriptCSharpHostRunner` is optional. It serializes access
to one host with a C# lock and schedules at most one pump job for that host on a
shared dispatcher. Concurrent `Receive`, `Load`, subscribe, detach, and
unsubscribe calls go through the runner. Creating a runner immediately schedules
one pump when its host already has pending work, including queued messages,
initialization, or a paused script handler. No later `Receive` or `Load` is
required to start that work. An idle host waits for later accepted work.

The initial scheduling uses the same serialization gate and outstanding-job
guard as subsequent runner operations. The caller stops accessing the host
directly when transferring ownership; pending callbacks may start before
runner creation returns. Disposing the runner suppresses scheduled pumps that
have not started, while preserving the host's pending work.

Swift, Kotlin, C++, and Unity provide
their own actor, executor, event-loop, or main-thread policy around the same
synchronous core contract.

## Hot-Path Rules

- Script registrations store `GameEventScriptInstance + EntryAddress`; no
  per-invocation delegate closure, runner object, or invocation interface exists.
- The queue is a preallocated growing ring of logical message envelopes.
- Dispatch selection walks captured arrays without constructing handler lists.
- Handler registrations prepare their dispatch signature once and reuse it for
  VM entry, observer callbacks, and diagnostics.
- Execution and publish results are value types.
- Program loading prewarms VM register capacity. Later register growth is
  geometric and bounded by `MaxRegisterValues`.
- Synchronous script call graphs are acyclic. A root handler has call-stack depth
  zero; every nested call adds one entry.
- After warmup, queue dispatch, handler selection, frame-result creation, and VM
  resume must not allocate. Message creation, emitted value payloads, extension
  behavior, and Conformance Markdown/YAML decoding are measured separately.
- Random boundary and scope storage is initialized on first use and retained by
  the host. Push, Pop, marker creation, overpush gating, and frame resume allocate
  nothing after that warmup.

## Conformance Porting Contract

Every implementation runs the Conformance Markdown cases through this sequence:

```text
source -> GameEventScriptProgram -> optional .gesb Write/Read -> Host.Load
YAML step input -> GameEventScriptMessage -> Host.Receive
Host.ExecuteFrame or Host.RunToCompletion -> observed local/outbound messages
```

The same Markdown cases define ordering, tags, initialization, multiple programs,
frame pause/resume, runtime limits, and Emit/Publish/Receive behavior for every
language in the monorepo. Message `args` are always encoded as an ordered YAML
sequence of mappings with `name` and `value` entries, including nested message
values and expected local/outbound messages.
