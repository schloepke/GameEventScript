// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>
/// The safety limits of one host: work, nesting and collection sizes per handler and the queue of the host. They bound
/// time and stack use, not memory (specs/HostRuntime.md). Exceeding most limits stops the active handler with a
/// structured runtime-limit observation; the remarks of each property state its exact outcome.
/// </summary>
public sealed class GesRuntimeLimits
{
    /// <summary>A shared instance with the default limits of every property.</summary>
    public static GesRuntimeLimits Default { get; } = new();

    /// <summary>
    /// Specifies the maximum number of logical messages completed by a pump before
    /// it stops with a processing-limit result while more messages remain queued.
    /// </summary>
    /// <remarks>
    /// The default is 64; a nonpositive value disables this limit. Each message counts once,
    /// after all captured handlers finish. Exactly reaching the limit while draining the queue
    /// completes normally. Otherwise pending messages remain queued for a later pump call.
    /// </remarks>
    public int MaxProcessedEventsPerRun { get; init; } = 64;

    /// <summary>
    /// Specifies the maximum number of logical messages a host may hold queued, delayed entries included.
    /// </summary>
    /// <remarks>
    /// The default is 0, which like every nonpositive value disables the limit. When it is reached, an Emit or Publish
    /// statement and a native Emit or Publish drop the local message, report the limit to the observer and continue; a
    /// result-bearing or delayed send stops its handler with this limit; an initialization snapshot that finds no slot
    /// fails <c>Load</c> with <c>link.initializationQueueFull</c>.
    /// </remarks>
    public int MaxQueuedMessagesPerRun { get; init; }

    /// <summary>
    /// Specifies the maximum number of opcodes one script handler may execute.
    /// </summary>
    /// <remarks>
    /// The default is 100,000; a nonpositive value disables the limit. The count persists across frame pauses and
    /// resets for each handler. Exactly the limit is allowed; a handler that would execute more stops with this limit.
    /// </remarks>
    public int MaxExecutionSteps { get; init; } = 100_000;

    /// <summary>
    /// Specifies the maximum number of values in the reusable VM register storage of one host.
    /// </summary>
    /// <remarks>
    /// The default is 512; a nonpositive value selects that portable default. <c>Load</c> rejects a Program whose
    /// declared register requirement exceeds it.
    /// </remarks>
    public int MaxRegisterValues { get; init; } = 512;

    /// <summary>
    /// Specifies the maximum number of successful iterator advances across loops and iterator-backed selectors in one handler.
    /// </summary>
    /// <remarks>
    /// The default is 100,000; a nonpositive value disables this limit. A present nothing item counts,
    /// but iterator exhaustion does not. Exactly the limit is allowed; the next successful advance
    /// stops the handler before its body or selector expression executes. The counter persists across
    /// frame pauses and resets for each handler.
    /// </remarks>
    public int MaxLoopIterations { get; init; } = 100_000;

    /// <summary>
    /// Specifies the maximum nesting of function and predicate calls in one handler.
    /// </summary>
    /// <remarks>
    /// The default is 64. Unlike the other limits, zero is not "disabled": it permits only root handlers, so a Program
    /// that calls a function or predicate fails <c>Load</c> with <c>link.requiredCallStackDepthExceeded</c>. Values
    /// are clamped to the encoded u16 range. Call graphs are acyclic, so <c>Load</c> checks the Program's static depth.
    /// </remarks>
    public int MaxCallDepth { get; init; } = 64;

    /// <summary>
    /// Defines the maximum number of simultaneously active nested random scopes
    /// owned by one host.
    /// </summary>
    /// <remarks>
    /// The lazily initialized scope storage provides this many regular states
    /// plus one internal fault-gate state. Reaching the configured depth is valid; attempting one
    /// additional push faults the current handler without changing the parent
    /// random stream. The value must be between 0 and 65,535.
    /// </remarks>
    public int MaxRandomScopeDepth { get; init; } = 16;

    /// <summary>
    /// Specifies the maximum number of items a Range may enumerate or materialize.
    /// </summary>
    /// <remarks>
    /// The default is 10,000; a nonpositive value disables the limit. A loop over a longer Range runs no iteration, and a
    /// List cast or shuffle of one yields an empty List; each reports this limit to the observer while the handler
    /// continues. Inside <c>parse</c> the limit bounds the total of all Ranges it materializes and stops the handler.
    /// </remarks>
    public int MaxRangeItems { get; init; } = 10_000;

    /// <summary>
    /// Specifies the maximum size of each collection incrementally materialized by generated lists and iterator-backed selectors.
    /// </summary>
    /// <remarks>
    /// The default is 10,000; a nonpositive value disables this limit. Counts include retained nothing items,
    /// but exclude discarded duplicates and skipped map keys. Grouping limits the number of groups and
    /// each group's size independently. Before an insertion would exceed the limit, the runtime rejects
    /// it and stops the handler without returning a partial result. Separate collections have separate
    /// size limits; this property does not bound existing inputs or allocated bytes.
    /// </remarks>
    public int MaxGeneratedCollectionItems { get; init; } = 10_000;

    /// <summary>
    /// Specifies the maximum nesting depth of a List, Map, Record or Message that a handler constructs.
    /// </summary>
    /// <remarks>
    /// The default is 64; a nonpositive value disables this limit. A List, Map, Record or Message is one level
    /// deeper than its deepest item, field or argument, and every other value has depth 0, so a List of numbers
    /// has depth 1. Constructing a deeper value stops the handler with this runtime limit. The bound keeps the
    /// recursive conversion, comparison and release of script-built values within the native stack. Values
    /// that native code builds through the API are not checked until a handler nests them further.
    /// </remarks>
    public int MaxValueDepth { get; init; } = 64;

    /// <summary>
    /// Specifies the maximum number of dice one roll may create.
    /// </summary>
    /// <remarks>
    /// The default is 1,000; a nonpositive value disables the limit. A larger roll yields empty Dice and reports this
    /// limit to the observer; the handler continues.
    /// </remarks>
    public int MaxDiceCount { get; init; } = 1_000;

    /// <summary>
    /// Specifies the maximum number of sides of one die.
    /// </summary>
    /// <remarks>
    /// The default is 1,000,000; a nonpositive value disables the limit. A roll with more sides yields empty Dice and
    /// reports this limit to the observer; the handler continues.
    /// </remarks>
    public int MaxDiceSides { get; init; } = 1_000_000;
}
