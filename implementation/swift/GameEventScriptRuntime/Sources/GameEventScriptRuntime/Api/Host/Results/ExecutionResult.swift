// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Outcome, work counters and optional diagnostic for a host pump call.
public struct GameEventScriptExecutionResult: Sendable {
    /// Completion or failure classification for this operation.
    public let state: GameEventScriptExecutionState
    /// Number of script bytecode instructions executed by this operation.
    public let executedOpcodes: Int
    /// Number of logical messages processed by this operation.
    public let processedMessages: Int
    /// Number of local emit operations observed during this operation.
    public let emittedMessages: Int
    /// Number of publish operations observed during this operation.
    public let publishedMessages: Int
    /// Failure context when supplied, otherwise nil.
    public let diagnostic: GameEventScriptDiagnostic?
}
