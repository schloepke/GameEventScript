<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Host runtime specification

This document defines the portable runtime boundary shared by
Swift, Kotlin, Go, Rust, C++, C#, and Unity implementations. C# is the
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
a seed, entropy bytes, or an immutable sequence configuration, never a live
mutable generator; building two hosts from the same configuration creates two
independent streams.

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

An extension function may throw `GameEventScriptExtensionFaultException` to
deliberately report a defined failure, or throw any other exception
unanticipated by the runtime; [Diagnostics](Diagnostics.md) owns the resulting
stable codes and the shared "current handler only, host stays reusable, never
`nothing`" contract shared with native handlers and the external-type boundary.

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

All portable failures follow [Diagnostics](Diagnostics.md). Ordinary runtime handler failures
are reported to the observer, abort/reset only the failing handler, and leave the
remaining immutable dispatch snapshot runnable. A pump result that observed a
handler failure uses `RuntimeError` and carries its first diagnostic. Successful
VM stepping, resume, and dispatch do not allocate diagnostic objects.

## Loading and startup

`Build()` creates a host in its initial loading phase. `IsReady` is false, even
when `IsIdle` is true. Register native handlers and call `Load` for every initial
Program before `Start`. `Receive` returns false before a successful Start;
`ExecuteFrame` and `RunToCompletion` reject pumping a host that is not ready.

`Load` validates and links before committing registration. Validation, resource,
link, and initialization-queue capacity failures leave registrations, queued
messages, active execution and random state unchanged. An initialization snapshot
reserves one queue slot under `MaxQueuedMessagesPerRun`; insufficient capacity
raises `link.initializationQueueFull`. A Program without initialization needs no
slot. Loading is available to embedding code and native message handlers.
Reentrant loading during active script/extension execution or the initial Start
is rejected; paused execution between frames does not prohibit host-side loading.
Scripts and extension contexts have no loading API.

### Initial group

`Start()` runs only initialization snapshots, in Load order. Handlers inside one
instance retain their declaration order. All initial registrations are already
available when any initialization emits, so its enqueue-time snapshot can include
another member of the group. No ordinary queued message is dispatched by Start.

Every initialization uses the ordinary runtime safety limits. Exhaustion fails
startup; the initial group never becomes partially ready. Successful Start marks
the host ready and returns `StartResult.Ready`, while emitted messages may remain
queued. StartResult also reports initialization opcode, completed-snapshot, emit,
and publish counters; these exclude subsequent ordinary dispatch. A runtime fault or exhausted safety limit returns `RuntimeError` or
`RuntimeLimitReached`, with a structured diagnostic. Failure removes the group's
registrations and discards all its queued outputs. The host remains not ready and
cannot be started again. Repeating Start returns its original result without
executing code. Reentrant Start and pump calls are rejected.

### Later instances

After Start, Load queues initialization at the normal FIFO position and returns
an Instance with absent `StartResult`. No extra priority is assigned to init.
The current logical message completes before the new initialization starts.
A later initialization may pause across `ExecuteFrame` calls. Its instance cannot
execute ordinary handlers until all its initialization handlers succeed. A later
Program without initialization has an immediate Ready result.

Success stores Ready on the instance and releases its buffered outputs. Failure
stores RuntimeError or RuntimeLimitReached and its diagnostic, removes the
instance's registrations, discards its buffered outputs, and cancels every pending
recipient entry belonging to that instance. Other recipients of the same logical
message retain their order and still execute. Messages with no surviving recipient
are removed without dispatch or undeliverable redistribution. This failure
cancellation is stronger than ordinary Detach, which retains captured recipients.
The host remains ready; the embedding decides whether to retry with a new Load.
A new instance never inherits the previous instance's captured deliveries.

The instance's immutable completed StartResult remains readable after removal.
A startup safety limit is represented by `runtime.initializationLimitReached`;
the regular runtime-limit observer also identifies the limit and bound. Runtime
faults retain their original diagnostic code and context.

### Initialization outputs

Init `Emit` captures recipients and reserves its FIFO position immediately.
Local delivery waits until that instance succeeds. Queue limits include staged messages and queued initialization
snapshots. Overflow of staged outputs exhausts the initialization. External
Receive calls between paused init frames enter the ordinary queue independently
and are not discarded as that init's output. Observer emit notifications describe
attempts; their existence is not proof of committed delivery.

Init `Publish` also defers the external sink call: until the entire initial group
succeeds, or until the individual later init succeeds. Its immediate result has
`OutboundDeferred` when a sink exists, with OutboundAttempted/OutboundAccepted
false. AnyAccepted includes deferred acceptance. The observer receives the final
publish result on release; failed initializations never call the sink. Successful
local acceptance during init is provisional. Sink rejection/failure at release
retains ordinary publish semantics and does not undo successful initialization.
Direct external effects performed by arbitrary native callbacks are not rolled
back. Host readiness promises successful init, not completion of its emitted
message chains.

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

A constructor or a field accessor may likewise throw
`GameEventScriptExtensionFaultException` or an unanticipated exception; see
[Diagnostics](Diagnostics.md) for the resulting codes and contract.

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
  `OutboundAccepted`, `OutboundDeferred`, and derived `AnyAccepted`. Sink rejection or exception
  never rolls back or corrupts local delivery. The observer sees the result.
- Queue limits count logical messages, not handler invocations.
- Each queued message owns immutable exact/name subscription snapshots and a
  cursor. Every matching handler finishes in priority/registration order before
  the next logical message starts.
- A detach, unsubscribe, subscribe, or load during dispatch does not modify
  already captured snapshots. It applies when a later message is enqueued.
- `on initialization` is a special per-instance handler, never normal external
  input. The initial Start barrier and subsequent FIFO initialization follow
  [Loading and startup](#loading-and-startup).

## Portable State Machine

The following pump states apply after successful Start. Initial loading and the
terminal startup-failure state are defined in [Loading and startup](#loading-and-startup).
Here `Ready` means queued work, distinct from the public `IsReady` startup flag.

```text
Idle
  Receive / Emit / Publish / Load(late initialization)
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
    -> Ready when runnable work remains, Waiting when only timers remain, otherwise Idle

Waiting
  caller pumps at or after NextMessageDelay
    -> promote due messages behind runnable FIFO work
    -> Ready, or remain Waiting if no message is due

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
host becomes idle, only future work remains, or a runtime limit stops the run. Core code starts no thread
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

### Literal parsing limits

Each `parse` invocation has fixed portable bounds, applied only while executing
`ParseLiteral`: at most 1,048,576 input Unicode scalars (`MaxLiteralInputScalars`),
64 nested List/Map/Dice/Vector/Point containers (`MaxLiteralDepth`), and 65,536
total List items, Map entries, Dice rolls, and explicitly supplied Vector/Point
components across the whole input (`MaxLiteralItems`). A Dice, Vector, or Point
literal consumes one container level, including an empty form; its syntax does
not imply an additional List. Omitted spatial components do not consume items.
Duplicate Map keys
still count as entries. Scalar roots do not consume an item or nesting slot.
Exactly each bound is allowed; the next scalar, nested container, or item
exceeds it. Input length is checked before recognition. Nesting and item bounds
are checked incrementally before creating the next container or reading the
next item; trailing syntax errors do not undo a limit already encountered.

Exceeding a bound exhausts the current handler with the existing runtime-limit
mechanism and the corresponding stable limit name and numeric bound. It does
not return original Text as a recognition fallback or emit a partial value.
Previously completed effects remain observable and subsequent queued messages
can run normally. The counters are local to one parse invocation; programs
without `parse` maintain no parsing state or counters.

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

Both native bridge runners are optional. They take ownership without implicitly
starting a loading host. Register handlers, load Programs, and call the runner's
synchronous Start; successful Start schedules remaining ordinary work. A runner
wrapping an already ready host schedules its pending work immediately. Both
runners serialize callbacks on a shared background dispatcher and expose readiness,
the last execution result and synchronized instance startup results.

`CSharpBridge.GameEventScriptCSharpHostRunner` serializes access
to one host with a C# lock and schedules at most one pump job for that host on a
shared dispatcher. Concurrent `Receive`, `Load`, subscribe, detach, and
unsubscribe calls go through the runner. Creating a runner immediately schedules
one pump when its host is ready and already has pending work, including queued
messages, late initialization, or a paused script handler. No later `Receive` or `Load` is
required to start that work. An idle host waits for later accepted work.

The initial scheduling uses the same serialization gate and outstanding-job
guard as subsequent runner operations. The caller stops accessing the host
directly when transferring ownership; pending callbacks may start before
runner creation returns. Disposing the runner suppresses scheduled pumps that
have not started, while preserving the host's pending work.

Swift, Kotlin, Go, Rust, C++, and Unity provide
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
source -> GameEventScriptProgram -> optional .gesb Write/Read -> Host.Load -> Host.Start
YAML step input -> GameEventScriptMessage -> Host.Receive
Host.ExecuteFrame or Host.RunToCompletion -> observed local/outbound messages
```

The same Markdown cases define ordering, tags, initialization, multiple programs,
frame pause/resume, runtime limits, and Emit/Publish/Receive behavior for every
language in the monorepo. Message `args` are always encoded as an ordered YAML
sequence of mappings with `name` and `value` entries, including nested message
values and expected local/outbound messages.

## Delayed dispatch

Each Host borrows a monotonic clock reporting nonnegative whole microseconds.
The default clock measures actual elapsed time; an embedding can supply a clock
for deterministic simulation or testing. Clock reads never pump messages.

A positive finite time quantity is converted to microseconds, rounded upward.
Integer seconds are converted with checked integer arithmetic. Binary64 seconds
are multiplied by 1,000,000 using binary64 arithmetic and rounded upward; the
result must be smaller than 2^63. A negative input is invalid even if rounding
would produce zero. The absolute deadline is clock time at acceptance plus the
delay; overflow rejects the send. This is a minimum scheduling delay, not a
real-time execution guarantee.

Delayed entries capture the same recipient snapshot as instant enqueue. Newly
registered recipients do not receive older entries. Ordinary detach retains its
captured-delivery behavior. When deadlines become due, entries are appended to
the runnable FIFO in deadline order; equal deadlines retain enqueue order.
Promotion occurs between complete logical messages, never in the middle of an
active or paused handler. Future entries do not block runnable messages.

Publish delays both local delivery and the outbound sink invocation. A later sink
rejection or failure cannot change the Boolean already returned to script.
The existing sink diagnostic behavior remains applicable. Emit observations occur
at acceptance; delayed publish observations occur when the publication is released.

Init computes deadlines when the expression executes but cannot release its
outputs before its initial group or later-instance initialization succeeds.
Already due entries become eligible after that barrier. Initial-group failure
clears all outputs; later-instance failure clears its staged outputs and removes
captured deliveries to the failed instance without canceling independent outbound
publication or other recipients. Accepted sends outlive ordinary sender detach.
Delayed entries count towards the existing logical-message queue limit; promotion
does not reserve a second slot.

`RunToCompletion` and `ExecuteFrame` never wait or advance the clock. `Waiting`
means only future work remains, `Completed` means no work remains, and `Paused`
retains its frame-budget meaning. Errors and limits retain precedence.
`PendingMessageCount` includes delayed entries but excludes the active message;
`IsIdle` is false while delayed entries remain. `NextMessageDelay` reports remaining
whole microseconds, clamped to zero, or no value when there is no delayed entry.
Bridges and command-line adapters own timers and serialize all Host access.
