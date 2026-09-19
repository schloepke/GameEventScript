// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

public enum GameEventScriptAPIError: Error, Sendable {
    case invalidArgument(String)
    case invalidOperation(String)
}
public enum GameEventScriptDiagnosticPhase: Int, Sendable {
    case parse = 1
    case validate = 2
    case compile = 3
    case decode = 4
    case link = 5
    case runtime = 6
}
public enum GameEventScriptSymbolKind: Sendable {
    case unknown, type, predicate, function, handler, message, variable, globalDefinition
}

/// Optional source span; lines and columns are one-based, source IDs identify compiler inputs.
public struct GameEventScriptSourceLocation: Sendable {
    public let sourceName: String
    public let line: Int?
    public let column: Int?
    public let endLine: Int?
    public let endColumn: Int?
    public let moduleName: String
    public let sourceID: UInt32?
    public init(
        sourceName: String, line: Int? = nil, column: Int? = nil, endLine: Int? = nil,
        endColumn: Int? = nil, moduleName: String = "UnknownModule", sourceID: UInt32? = nil
    ) {
        self.sourceName = sourceName
        self.line = line
        self.column = column
        self.endLine = endLine
        self.endColumn = endColumn
        self.moduleName = moduleName
        self.sourceID = sourceID
    }
}

public struct GameEventScriptDiagnostic: Sendable {
    public let phase: GameEventScriptDiagnosticPhase
    public let code: String
    public let message: String
    public let symbol: String?
    public let symbolKind: GameEventScriptSymbolKind
    public let sourceLocation: GameEventScriptSourceLocation?
    public var programName: String?
    public var handlerName: String?
    public let technicalDetails: String?
    public init(
        phase: GameEventScriptDiagnosticPhase, code: String, message: String = "", symbol: String? = nil,
        symbolKind: GameEventScriptSymbolKind = .unknown, sourceLocation: GameEventScriptSourceLocation? = nil,
        programName: String? = nil, handlerName: String? = nil, technicalDetails: String? = nil
    ) {
        self.phase = phase
        self.code = code
        self.message = message
        self.symbol = symbol
        self.symbolKind = symbolKind
        self.sourceLocation = sourceLocation
        self.programName = programName
        self.handlerName = handlerName
        self.technicalDetails = technicalDetails
    }
}

/// An explicit application fault retains its stable code across callback boundaries.
public struct GameEventScriptExtensionFault: Error, Sendable {
    public let diagnostic: GameEventScriptDiagnostic
    public init(
        code: String, message: String, symbol: String? = nil, technicalDetails: String? = nil,
        programName: String? = nil, handlerName: String? = nil
    ) throws {
        if code.unicodeScalars.allSatisfy({ $0.properties.isWhitespace }) || code.hasPrefix("runtime.") {
            throw GameEventScriptAPIError.invalidArgument("Invalid extension fault code")
        }
        diagnostic = .init(
            phase: .runtime, code: code, message: message, symbol: symbol, programName: programName,
            handlerName: handlerName, technicalDetails: technicalDetails)
    }
}
public struct GameEventScriptDynamicLinkError: Error, Sendable {
    public let diagnostic: GameEventScriptDiagnostic
    init(code: String, program: String, symbol: String? = nil) {
        diagnostic = .init(phase: .link, code: code, symbol: symbol, programName: program)
    }
}
struct GesRuntimeError: Error { let diagnostic: GameEventScriptDiagnostic }

public struct GameEventScriptRuntimeLimits: Sendable {
    public var maxProcessedEventsPerRun = 64
    public var maxQueuedMessagesPerRun = 0
    public var maxExecutionSteps = 100_000
    public var maxRegisterValues = 512
    public var maxLoopIterations = 100_000
    public var maxCallDepth = 64
    public var maxRandomScopeDepth = 16
    public var maxRangeItems = 10_000
    public var maxGeneratedCollectionItems = 10_000
    public var maxDiceCount = 1_000
    public var maxDiceSides = 1_000_000
    public init() {}
}
public enum GameEventScriptExecutionState: Sendable { case paused, completed, runtimeLimitReached, runtimeError }
public struct GameEventScriptExecutionResult: Sendable {
    public let state: GameEventScriptExecutionState
    public let executedOpcodes: Int
    public let processedMessages: Int
    public let emittedMessages: Int
    public let publishedMessages: Int
    public let diagnostic: GameEventScriptDiagnostic?
}
public struct GameEventScriptPublishResult: Sendable {
    public let localAccepted: Bool
    public let outboundAttempted: Bool
    public let outboundAccepted: Bool
    public var anyAccepted: Bool { localAccepted || outboundAccepted }
}
public protocol GameEventScriptNativeMessageHandler {
    func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws
}
public protocol GameEventScriptPublishSink {
    func publish(_ message: GameEventScriptMessage) throws -> Bool
}
public protocol GameEventScriptRuntimeObserver {
    func messageEmitted(_ message: GameEventScriptMessage, accepted: Bool)
    func messagePublished(_ message: GameEventScriptMessage, result: GameEventScriptPublishResult)
    func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String)
    func dispatchCompleted(_ message: GameEventScriptMessage, signatureID: String)
    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int)
    func runtimeError(_ diagnostic: GameEventScriptDiagnostic)
}

extension GameEventScriptRuntimeObserver {
    public func messageEmitted(_ message: GameEventScriptMessage, accepted: Bool) {}
    public func messagePublished(_ message: GameEventScriptMessage, result: GameEventScriptPublishResult) {}
    public func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String) {}
    public func dispatchCompleted(_ message: GameEventScriptMessage, signatureID: String) {}
    public func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) {}
    public func runtimeError(_ diagnostic: GameEventScriptDiagnostic) {}
}
