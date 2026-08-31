# Host Architecture

This document defines the portable runtime boundary shared by the planned
Swift, Kotlin, C++, C#, and Unity implementations. The C# implementation is the
current reference implementation. Language-specific threading and reflection
must stay outside the portable core.

## Responsibilities

`GameEventScriptProgram` is the immutable compiler result. It contains bytecode,
constant tables, and bind metadata but no VM state, random generator, delegate,
host binding, or queue. A program may be loaded into multiple hosts at the same
time. All public program tables copy their input and expose read-only views.

`GameEventScriptHost` is one autonomous serial execution unit. It owns the local
message queue, exact- and name-subscription indexes, deterministic random stream,
runtime limits, observer, optional outbound sink, linked program instances, and
at most one reusable `GesVmState`. A host may contain only native handlers and
does not create a VM until the first program is loaded.

`GameEventScriptInstance` is one host-specific link of one program. `Load` is
additive and returns an instance. Linking resolves extension and external-type
references against that host. `Detach` is idempotent and removes the instance
from future dispatch snapshots.

`GameEventScriptSubscription` represents one native registration. `Unsubscribe`
is idempotent and affects future snapshots only.

`GameEventScriptContext` is created once per host and passed to native handlers
and extensions. It exposes the host random stream, limits, `Emit`, and `Publish`.
It does not own a queue and is not a session.

`GameEventScriptVirtualMachine` is a stateless executor. `GesVmState` contains
only the currently resumable execution: active linked program, instruction
pointer, registers, frames, stages, random stack, and active message. The host
reuses this state serially for every script handler and fully resets it after
completion or failure. VM pooling across hosts is not part of this architecture.

## Message Semantics

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

  handler completes or fails
    -> fully reset VM state
    -> Dispatching

  handler safety limit reached
    -> reset VM state
    -> RuntimeLimitReached
```

`ExecuteFrame(opcodeBudget)` is a caller-thread scheduler slice. Its opcode
budget is independent from safety limits and may pause only script bytecode;
native handlers remain atomic. `RunToCompletion()` pumps synchronously until the
host becomes idle or a runtime limit stops the run. Core code starts no thread
and performs no synchronization.

## C# Automatic Runner

`CSharpBridge.GameEventScriptCSharpHostRunner` is optional. It serializes access
to one host with a C# lock and schedules at most one pump job for that host on a
shared dispatcher. Concurrent `Receive`, `Load`, subscribe, detach, and
unsubscribe calls go through the runner. Swift, Kotlin, C++, and Unity provide
their own actor, executor, event-loop, or main-thread policy around the same
synchronous core contract.

## Hot-Path Rules

- Script registrations store `GameEventScriptInstance + EntryAddress`; no
  per-invocation delegate closure, runner object, or invocation interface exists.
- The queue is a preallocated growing ring of logical message envelopes.
- Dispatch selection walks captured arrays without constructing handler lists.
- Execution and publish results are value types.
- Program loading prewarms VM register capacity. Later register growth is
  geometric and bounded by `MaxRegisterValues`.
- After warmup, queue dispatch, handler selection, frame-result creation, and VM
  resume must not allocate. Message creation, emitted value payloads, extension
  behavior, and JSON decoding are measured separately.

## Conformance Porting Contract

Every implementation runs the JSON suites through this sequence:

```text
source -> GameEventScriptProgram -> Host.Load
input JSON -> GameEventScriptMessage -> Host.Receive
Host.ExecuteFrame or Host.RunToCompletion -> observed local/outbound messages
```

The same JSON cases define ordering, tags, initialization, multiple programs,
frame pause/resume, runtime limits, and Emit/Publish/Receive behavior for every
language in the monorepo.
