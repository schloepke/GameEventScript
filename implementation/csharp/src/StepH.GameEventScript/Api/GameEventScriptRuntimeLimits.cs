// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents runtime execution limits for a game event script, providing control over various
/// script execution parameters such as loop iterations, call stack depth, and allowed operations.
/// This ensures that scripts execute within predefined boundaries to prevent excessive resource usage
/// or infinite execution.
/// </summary>
public sealed class GameEventScriptRuntimeLimits
{
    /// <summary>
    /// Provides the default configuration for runtime limits used in game event script execution.
    /// This property returns a shared instance of <see cref="GameEventScriptRuntimeLimits"/>
    /// with predefined limits for event processing, execution steps, and loop iterations.
    /// </summary>
    /// <remarks>
    /// The default instance is used as a fallback if no custom limits are specified
    /// during the creation of a runtime context or host. This ensures that
    /// reasonable constraints are in place to avoid excessive resource usage during script execution.
    /// </remarks>
    public static GameEventScriptRuntimeLimits Default { get; } = new();

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
    /// Specifies the maximum number of messages that may be waiting in an active
    /// host or explicit run queue.
    /// </summary>
    /// <remarks>
    /// A value less than or equal to zero disables the queue-length limit. When the limit is
    /// reached, the newest message is discarded, the publish call returns false, and
    /// the runtime observer is notified when one is configured.
    /// </remarks>
    public int MaxQueuedMessagesPerRun { get; init; }

    /// <summary>
    /// Specifies the maximum number of execution steps allowed during the evaluation of a game event script.
    /// This limit is used to control the computational workload of scripts and to prevent scripts
    /// from consuming excessive runtime resources or entering infinite execution loops.
    /// </summary>
    /// <remarks>
    /// The value of this property defines an upper bound on the number of discrete steps
    /// a script is permitted to execute before being halted. A step typically corresponds to a fundamental
    /// operation or instruction within the script's runtime environment. Setting this limit ensures
    /// predictable script performance and safeguards against potential misuse or unintentional excessive
    /// resource consumption.
    /// </remarks>
    public int MaxExecutionSteps { get; init; } = 100_000;

    /// <summary>Maximum number of values in the reusable VM register storage of one host.</summary>
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
    /// Defines the maximum allowable depth of the call stack during game event script execution.
    /// This property sets a limit on the number of nested function or method calls
    /// in a script, ensuring that scripts terminate within a controlled depth to avoid
    /// stack overflow or excessive resource consumption.
    /// </summary>
    /// <remarks>
    /// The value of this property is used to monitor and restrict the depth of nested
    /// calls during script execution. Recursive call graphs are rejected by the compiler and loader.
    /// If the number of nested calls exceeds this limit,
    /// the script runtime will halt further execution to prevent resource exhaustion.
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
    /// Specifies the maximum number of items allowed in a range-based operation
    /// during game event script execution. This limit helps ensure that range operations,
    /// such as enumerating over a collection or processing a sequence, do not exhaust
    /// resources by creating excessively large intermediate collections or performing
    /// operations on an overly large dataset.
    /// </summary>
    /// <remarks>
    /// This property is commonly used to enforce bounds on script-driven operations that
    /// involve ranges, such as generating collections or iterating over sequences. If a range
    /// exceeds the specified limit, the script runtime may terminate execution, or the
    /// relevant operation could fail as a runtime-limit condition.
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
    /// Specifies the maximum number of dice that can be processed during the execution
    /// of a game event script. This limit ensures controlled usage of resources
    /// when handling random number generation mechanics in scripts.
    /// </summary>
    /// <remarks>
    /// The value of this property represents the upper bound on the allowable
    /// number of dice rolls in a single script execution. Exceeding this limit during
    /// runtime will report a runtime-limit condition and the operation being
    /// disallowed. This safeguard prevents excessive resource consumption
    /// caused by handling an unreasonably high number of dice rolls.
    /// </remarks>
    public int MaxDiceCount { get; init; } = 1_000;

    /// <summary>
    /// Specifies the maximum number of sides that a single dice can have in a game event script.
    /// This property imposes an upper limit on how large the side count for any dice roll can be,
    /// providing a constraint to prevent excessive computational complexity or unrealistic scenarios.
    /// </summary>
    /// <remarks>
    /// This limit helps ensure that dice rolls remain computationally manageable
    /// and consistent with the intended design of the game event scripting system.
    /// Scripts that attempt to define a dice with a side count exceeding this value
    /// will be terminated or flagged as exceeding runtime limits.
    /// </remarks>
    public int MaxDiceSides { get; init; } = 1_000_000;
}
