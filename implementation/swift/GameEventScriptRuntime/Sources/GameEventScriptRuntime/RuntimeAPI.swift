// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Caller errors in API arguments or lifecycle operations.
public enum GameEventScriptAPIError: Error, Sendable {
    /// An argument violates an API precondition; the associated string explains it.
    case invalidArgument(String)
    /// The requested operation is invalid in the current lifecycle state.
    case invalidOperation(String)
}

/// Stable pipeline stage in which a diagnostic originated.
public enum GameEventScriptDiagnosticPhase: Int, Sendable {
    /// Source tokenization or grammar recognition.
    case parse = 1
    /// Source semantic validation.
    case validate = 2
    /// Lowering or bytecode compilation.
    case compile = 3
    /// Portable binary decoding or validation.
    case decode = 4
    /// Resolving a Program against a host.
    case link = 5
    /// Execution of script or native callbacks.
    case runtime = 6
}

/// Role of the optional symbol identified by a diagnostic.
public enum GameEventScriptSymbolKind: Sendable {
    /// No specific symbol category is available.
    case unknown
    /// A source or external type.
    case type
    /// A declared predicate.
    case predicate
    /// A declared function.
    case function
    /// A message handler.
    case handler
    /// A message signature or emission.
    case message
    /// A lexical variable binding.
    case variable
    /// A program-wide definition.
    case globalDefinition
}

/// Optional source span; lines and columns are one-based, source IDs identify compiler inputs.
public struct GameEventScriptSourceLocation: Sendable {
    /// Display name of the compiler input.
    public let sourceName: String
    /// One-based start line when known.
    public let line: Int?
    /// One-based start column when known.
    public let column: Int?
    /// One-based end line when known.
    public let endLine: Int?
    /// One-based end column when known.
    public let endColumn: Int?
    /// Declaring module, or UnknownModule when unavailable.
    public let moduleName: String
    /// Compiler-assigned document identifier when available.
    public let sourceID: UInt32?

    /// Creates source context; unknown coordinates remain nil rather than becoming zero.
    public init(sourceName: String, line: Int? = nil, column: Int? = nil, endLine: Int? = nil, endColumn: Int? = nil, moduleName: String = "UnknownModule", sourceID: UInt32? = nil) {
        self.sourceName = sourceName
        self.line = line
        self.column = column
        self.endLine = endLine
        self.endColumn = endColumn
        self.moduleName = moduleName
        self.sourceID = sourceID
    }
}

/// Stable phase/code and structured context for a source, binary, linking or runtime failure.
public struct GameEventScriptDiagnostic: Sendable {
    /// Pipeline stage that owns this diagnostic.
    public let phase: GameEventScriptDiagnosticPhase
    /// Stable machine-readable classification; use with phase rather than matching English text.
    public let code: String
    /// Human-readable explanation, not a stable comparison key.
    public let message: String
    /// Affected symbol name when available.
    public let symbol: String?
    /// Role of the affected symbol.
    public let symbolKind: GameEventScriptSymbolKind
    /// Optional original source span.
    public let sourceLocation: GameEventScriptSourceLocation?
    /// Affected Program name; runtime boundaries may fill missing context.
    public var programName: String?
    /// Affected handler signature; runtime boundaries may fill missing context.
    public var handlerName: String?
    /// Optional implementation-specific details for troubleshooting.
    public let technicalDetails: String?

    /// Creates a diagnostic from a stable phase/code pair and optional structured context.
    public init(
        phase: GameEventScriptDiagnosticPhase,
        code: String,
        message: String = "",
        symbol: String? = nil,
        symbolKind: GameEventScriptSymbolKind = .unknown,
        sourceLocation: GameEventScriptSourceLocation? = nil,
        programName: String? = nil,
        handlerName: String? = nil,
        technicalDetails: String? = nil
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
    /// Application-provided runtime diagnostic preserved across callback boundaries.
    public let diagnostic: GameEventScriptDiagnostic

    /// Creates an explicit application fault without replacing its code at runtime boundaries.
    ///
    /// - Throws: An API error when the code is empty, whitespace-only or uses the reserved runtime. prefix.
    public init(code: String, message: String, symbol: String? = nil, technicalDetails: String? = nil, programName: String? = nil, handlerName: String? = nil) throws {
        if code.unicodeScalars.allSatisfy({ $0.properties.isWhitespace }) || code.hasPrefix("runtime.") { throw GameEventScriptAPIError.invalidArgument("Invalid extension fault code") }
        diagnostic = .init(phase: .runtime, code: code, message: message, symbol: symbol, programName: programName, handlerName: handlerName, technicalDetails: technicalDetails)
    }
}

/// Failure to bind a validated Program to this host's imports or resource limits.
public struct GameEventScriptDynamicLinkError: Error, Sendable {
    /// Stable link-phase classification with Program and optional symbol context.
    public let diagnostic: GameEventScriptDiagnostic

    init(code: String, program: String, symbol: String? = nil) { diagnostic = .init(phase: .link, code: code, symbol: symbol, programName: program) }
}

struct GesRuntimeError: Error { let diagnostic: GameEventScriptDiagnostic }

/// Host safeguards for execution, queues, registers and generated collections.
public struct GameEventScriptRuntimeLimits: Sendable {
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

/// Outcome of initial-group or later per-instance initialization.
public enum GameEventScriptStartState: Sendable {
    /// All initialization covered by this result succeeded.
    case ready
    /// Initialization failed with a runtime diagnostic.
    case runtimeError
    /// Initialization exceeded a configured runtime limit.
    case runtimeLimitReached
}

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

/// Outcome of one host pump call.
public enum GameEventScriptExecutionState: Sendable {
    /// Work remains after the frame or scheduling budget was consumed.
    case paused
    /// The host finished the available work.
    case completed
    /// A configured runtime safeguard stopped execution.
    case runtimeLimitReached
    /// A script or native callback failed.
    case runtimeError
}

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

/// Independent local delivery and outbound publication outcomes.
public struct GameEventScriptPublishResult: Sendable {
    /// Whether the message was accepted for local delivery.
    public let localAccepted: Bool
    /// Whether the outbound sink was called.
    public let outboundAttempted: Bool
    /// Whether the outbound sink accepted the message.
    public let outboundAccepted: Bool
    /// Whether outbound publication is staged until initialization succeeds.
    public let outboundDeferred: Bool

    /// Creates publication outcome flags; deferred outbound delivery defaults to false.
    public init(localAccepted: Bool, outboundAttempted: Bool, outboundAccepted: Bool, outboundDeferred: Bool = false) {
        self.localAccepted = localAccepted
        self.outboundAttempted = outboundAttempted
        self.outboundAccepted = outboundAccepted
        self.outboundDeferred = outboundDeferred
    }

    /// Whether local, outbound or deferred outbound delivery was accepted.
    public var anyAccepted: Bool { localAccepted || outboundAccepted || outboundDeferred }
}

/// Synchronous native handler invoked atomically within serial host dispatch.
public protocol GameEventScriptNativeMessageHandler {
    /// Handles a message with the owning host context. Do not pump the same host reentrantly.
    ///
    /// - Throws: An application fault or callback error, classified by the host at the native boundary.
    func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws
}

/// Optional synchronous boundary for delivering publications outside the host.
public protocol GameEventScriptPublishSink {
    /// Attempts outbound delivery. Return true on acceptance; thrown errors are classified by the host publication
    /// boundary.
    func publish(_ message: GameEventScriptMessage) throws -> Bool
}

/// Synchronous observation hooks for host dispatch and failures. Default implementations do nothing.
public protocol GameEventScriptRuntimeObserver {
    /// Observes a local emit and whether the host accepted it; the default implementation does nothing.
    func messageEmitted(_ message: GameEventScriptMessage, accepted: Bool)

    /// Observes a publish with local and outbound outcome flags; the default implementation does nothing.
    func messagePublished(_ message: GameEventScriptMessage, result: GameEventScriptPublishResult)

    /// Observes entry into a selected handler; the default implementation does nothing.
    func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String)

    /// Observes completion of a selected handler; the default implementation does nothing.
    func dispatchCompleted(_ message: GameEventScriptMessage, signatureID: String)

    /// Observes a named runtime limit with its configured value and explanatory detail; the default implementation does
    /// nothing.
    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int)

    /// Observes a classified runtime failure; the default implementation does nothing.
    func runtimeError(_ diagnostic: GameEventScriptDiagnostic)
}

extension GameEventScriptRuntimeObserver {
    /// Observes a local emit and whether the host accepted it; the default implementation does nothing.
    public func messageEmitted(_ message: GameEventScriptMessage, accepted: Bool) {}

    /// Observes a publish with local and outbound outcome flags; the default implementation does nothing.
    public func messagePublished(_ message: GameEventScriptMessage, result: GameEventScriptPublishResult) {}

    /// Observes entry into a selected handler; the default implementation does nothing.
    public func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String) {}

    /// Observes completion of a selected handler; the default implementation does nothing.
    public func dispatchCompleted(_ message: GameEventScriptMessage, signatureID: String) {}

    /// Observes a named runtime limit with its configured value and explanatory detail; the default implementation does
    /// nothing.
    public func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) {}

    /// Observes a classified runtime failure; the default implementation does nothing.
    public func runtimeError(_ diagnostic: GameEventScriptDiagnostic) {}
}
