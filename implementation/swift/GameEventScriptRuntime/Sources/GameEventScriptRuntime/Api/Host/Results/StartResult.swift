// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Outcome, work counters and optional diagnostic for initialization.
public struct GameEventScriptStartResult: Sendable {
    /// Completion or failure classification for this operation.
    public let state: GameEventScriptStartState
    private let diagnosticStorage: GesStartDiagnostic?
    /// Failure context when supplied, otherwise nil.
    public var diagnostic: GameEventScriptDiagnostic? { diagnosticStorage?.value }
    /// Number of script bytecode instructions executed by this operation.
    public let executedOpcodes: Int
    /// Number of logical messages processed by this operation.
    public let processedMessages: Int
    /// Number of local emit operations observed during this operation.
    public let emittedMessages: Int
    /// Number of publish operations observed during this operation.
    public let publishedMessages: Int

    /// Creates an initialization result with counters and optional failure context.
    public init(state: GameEventScriptStartState, diagnostic: GameEventScriptDiagnostic? = nil, executedOpcodes: Int = 0, processedMessages: Int = 0, emittedMessages: Int = 0, publishedMessages: Int = 0) {
        self.state = state
        diagnosticStorage = diagnostic.map(GesStartDiagnostic.init)
        self.executedOpcodes = executedOpcodes
        self.processedMessages = processedMessages
        self.emittedMessages = emittedMessages
        self.publishedMessages = publishedMessages
    }
}

// Successful startup stores no diagnostic payload; allocate the large diagnostic only on failure.
final class GesStartDiagnostic: Sendable {
    let value: GameEventScriptDiagnostic

    init(_ value: GameEventScriptDiagnostic) { self.value = value }
}
