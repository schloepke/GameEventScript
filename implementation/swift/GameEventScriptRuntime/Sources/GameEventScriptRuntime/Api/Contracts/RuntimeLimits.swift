// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Host safeguards for execution, queues, registers and generated collections.
public struct GesRuntimeLimits: Sendable {
    /// Maximum processed logical messages per pump call; nonpositive disables this scheduling bound.
    public var maxProcessedEventsPerRun = 64
    /// Maximum pending queue size; nonpositive disables the queue bound.
    public var maxQueuedMessagesPerRun = 0
    /// Maximum script execution steps per handler; nonpositive disables this bound.
    public var maxExecutionSteps = 100_000
    /// Register limit used while linking and executing; nonpositive selects the runtime fallback capacity.
    public var maxRegisterValues = 512
    /// Maximum loop iterations per handler; nonpositive disables this bound.
    public var maxLoopIterations = 100_000
    /// Maximum nested call depth available to a linked Program.
    public var maxCallDepth = 64
    /// Maximum regular nested random scopes; must be in 0...65535.
    public var maxRandomScopeDepth = 16
    /// Maximum generated range length; nonpositive disables this bound.
    public var maxRangeItems = 10_000
    /// Maximum materialized collection size; nonpositive disables this bound.
    public var maxGeneratedCollectionItems = 10_000
    /// Maximum number of dice in a roll; nonpositive disables this bound.
    public var maxDiceCount = 1_000
    /// Maximum sides of a die; nonpositive disables this bound.
    public var maxDiceSides = 1_000_000

    /// Creates the standard runtime limits shown by the property defaults.
    public init() {}
}
