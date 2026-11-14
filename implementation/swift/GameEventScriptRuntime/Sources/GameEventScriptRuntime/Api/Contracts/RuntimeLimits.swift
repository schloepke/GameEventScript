// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Host safeguards for execution, queues, registers and generated collections.
public struct GesRuntimeLimits: Sendable {
    /// Maximum processed logical messages per pump call; nonpositive disables this scheduling bound.
    public var maxProcessedMessagesPerRun = 64
    /// Maximum logical messages a host holds queued, delayed entries included; nonpositive disables the bound. A full
    /// queue drops the message of an Emit or Publish statement and continues, stops a result-bearing or delayed send's
    /// handler, and fails `load` of an initialization with `link.initializationQueueFull`.
    public var maxQueuedMessages = 0
    /// Maximum script execution steps per handler; nonpositive disables this bound.
    public var maxExecutionSteps = 100_000
    /// Register limit used while linking and executing; nonpositive selects the runtime fallback capacity.
    public var maxRegisterValues = 512
    /// Maximum loop iterations per handler; nonpositive disables this bound.
    public var maxLoopIterations = 100_000
    /// Maximum nesting of function and predicate calls; zero permits only root handlers rather than disabling the bound,
    /// so `load` rejects a Program that calls with `link.requiredCallStackDepthExceeded`.
    public var maxCallDepth = 64
    /// Maximum regular nested random scopes; must be in 0...65535.
    public var maxRandomScopeDepth = 16
    /// Maximum items a Range may enumerate or materialize; nonpositive disables this bound. A longer Range loops not at all
    /// and casts or shuffles to an empty List with a limit observation; `parse` totals its Ranges and stops the handler.
    public var maxRangeItems = 10_000
    /// Maximum materialized collection size; nonpositive disables this bound.
    public var maxGeneratedCollectionItems = 10_000
    /// Maximum nesting depth of a List, Map, Record or Message a handler constructs; nonpositive disables this bound.
    ///
    /// A List, Map, Record or Message is one level deeper than its deepest item, field or argument; other values have depth 0.
    /// Constructing a deeper value stops the handler, so recursive conversions, comparisons and releases stay within the stack.
    public var maxValueDepth = 64
    /// Maximum number of dice in a roll; nonpositive disables this bound. A larger roll yields empty Dice with a limit
    /// observation.
    public var maxDiceCount = 1_000
    /// Maximum sides of a die; nonpositive disables this bound. A roll with more sides yields empty Dice with a limit
    /// observation.
    public var maxDiceSides = 1_000_000

    /// Creates the standard runtime limits shown by the property defaults.
    public init() {}
}
