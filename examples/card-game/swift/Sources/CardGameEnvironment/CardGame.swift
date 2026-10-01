// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime

/// An action offered by the active GES rules. An absent card means a button action.
public struct OfferedAction: Equatable {
    /// Rule-defined action name, such as play, draw, or pass.
    public let kind: String
    /// Optional concrete card to select.
    public let card: Int?

    /// Creates an action selection; actual permission is checked by GES on submission.
    public init(kind: String, card: Int? = nil) {
        self.kind = kind
        self.card = card
    }
}

/// Explicit outcome of one serial request. Rejections preserve the board and turn.
public struct ActionResult {
    /// Monotonically increasing session-local request identifier.
    public let request: Int
    /// Whether the rule requested and completed an action.
    public let accepted: Bool
    /// Rejection reason, or an empty string on success.
    public let reason: String
}

/// Single-caller card environment. Owns mutable state while GES owns setup and game rules.
/// Source compilation and execution are synchronous and perform no file or terminal I/O.
public final class CardGame {
    /// Player display names in stable, zero-based player order.
    public let players: [String]
    /// Active player selected by the rules; nil before setup completes.
    public var currentPlayer: Int? { bindings.currentPlayer }
    /// Winner selected by the rules, or nil while playing or after a draw.
    public var winner: Int? { bindings.winner }
    /// Whether the rules have ended the game, including a draw.
    public var finished: Bool { bindings.finished }
    /// Rule-provided explanation of the last completed action, or an empty string.
    public var notice: String { bindings.notice }
    /// Latest complete action offer, for the active player only.
    public var actions: [OfferedAction] { bindings.actions }
    /// Revision used to reject stale UI requests; increments after accepted actions.
    public private(set) var revision = 0
    /// Whether a rule/runtime fault has stopped this session. Construct a new session to restart.
    public private(set) var failed = false
    /// Copied zone state for trusted native inspection; use viewJSON for player-filtered output.
    public var zones: [Zone] { bindings.board.zones }
    /// Immutable card definitions for trusted native inspection, including hidden cards.
    public var cards: [Card] { bindings.board.cards }

    private let bindings: Bindings
    private let host: GameEventScriptHost
    private var nextRequest = 1

    /// Compiles the supplied GES, starts a seeded host, and sends Setup(players).
    /// Throws for invalid player counts, compiler diagnostics, binding errors, or bounded execution failure.
    public init(rules: String, players: [String] = ["Alice", "Bob"], seed: Int64 = 42) throws {
        guard (2...4).contains(players.count), players.allSatisfy({ !$0.isEmpty }) else { throw CardGameError("Provide two to four named players") }
        guard rules.utf8.count <= 131_072 else { throw CardGameError("Rule source exceeds 128 KiB") }
        self.players = players
        bindings = Bindings(playerCount: players.count)
        var limits = GameEventScriptRuntimeLimits()
        limits.maxQueuedMessagesPerRun = 2048
        limits.maxExecutionSteps = 50_000
        limits.maxLoopIterations = 2048
        limits.maxGeneratedCollectionItems = 2048
        host = try GameEventScriptHost(seed: seed, limits: limits, observer: bindings, extensions: bindings)
        do {
            let program = try GameEventScriptBuilder.create().addScript(rules, sourceName: "rules.ges").compile()
            _ = try host.load(program)
        } catch let error as GameEventScriptCompileError {
            throw CardGameError(error.diagnostics.map(formatDiagnostic).joined(separator: "\n"))
        }
        for (name, labels) in Bindings.commands {
            _ = try host.subscribe(GameEventScriptMessageSignature(name: name, parameters: labels), handler: bindings)
        }
        guard try host.start().state == .ready else { throw CardGameError("GES initialization failed") }
        try receive("Setup", [("players", .list(players.indices.map { .integer(Int64($0)) }))])
        try pump()
        bindings.settingUp = false
        guard currentPlayer != nil, !actions.isEmpty || finished else { throw CardGameError("Setup did not establish a turn and action offer") }
    }

    /// Submits a selected action without pre-moving cards. GES revalidates against current state.
    /// Stale revisions, invalid players, and ended games are rejected before rule execution.
    /// Throws on runtime failure and permanently stops this session; no rollback of previous commands is promised.
    public func submit(player: Int, action: OfferedAction, revision expectedRevision: Int) throws -> ActionResult {
        guard !failed else { throw CardGameError("Session failed; restart required") }
        let request = nextRequest
        nextRequest += 1

        func reject(_ reason: String) -> ActionResult { .init(request: request, accepted: false, reason: reason) }

        guard players.indices.contains(player) else { return reject("Unknown player") }
        guard !finished else { return reject("Game has ended") }
        guard expectedRevision == revision else { return reject("Stale action; refresh the board") }
        guard action.kind.utf8.count <= 80 else { return reject("Invalid action") }
        bindings.pending = .init(id: request, player: player, action: action)
        bindings.result = nil
        do {
            try receive(
                "ActionRequested",
                [
                    ("request", .integer(Int64(request))), ("player", .integer(Int64(player))),
                    ("action", .text(action.kind)), ("card", action.card.map { .integer(Int64($0)) } ?? .nothing),
                ]
            )
            try pump()
            guard let result = bindings.result, bindings.pending == nil else { throw CardGameError("Rules did not explicitly complete or reject the request") }
            if result.accepted {
                revision += 1
                guard finished || !actions.isEmpty else { throw CardGameError("Rules did not offer the next actions") }
            }
            return result
        } catch {
            failed = true
            bindings.actions = []
            throw error
        }
    }

    private func receive(_ name: String, _ values: [(String, GesValue)]) throws {
        guard host.receive(try message(name, values)) else { throw CardGameError("Host rejected incoming message") }
    }

    private func pump() throws {
        // Bound the complete message cascade as well as each individual GES handler.
        for _ in 0..<64 {
            let result = try host.runToCompletion()
            if let fault = bindings.fault { throw CardGameError(fault) }
            if result.state == .completed { return }
            guard result.state == .paused else { throw CardGameError("GES stopped: \(result.state)") }
        }
        throw CardGameError("Rule message cascade exceeded its budget")
    }
}

func message(_ name: String, _ values: [(String, GesValue)]) throws -> GameEventScriptMessage {
    try .init(name: name, arguments: values.map { try .init(name: $0.0, value: $0.1) })
}

final class Bindings: GameEventScriptNativeMessageHandler, GameEventScriptExtensionRegistry, GameEventScriptRuntimeObserver {
    struct Pending {
        let id: Int
        let player: Int
        let action: OfferedAction
    }

    static let commands: [(String, [String])] = [
        ("CreateZone", ["zone", "owner", "visibility"]), ("CreateCard", ["zone", "properties"]),
        ("Shuffle", ["zone"]), ("Take", ["source", "destination", "count"]),
        ("BeginTurn", ["player"]), ("Allowed", ["player", "actions"]),
        ("MoveCard", ["request", "card", "source", "destination"]),
        ("DrawCard", ["request", "source", "destination", "recycle", "keep"]),
        ("DrawCards", ["source", "destination", "count", "recycle", "keep"]), ("Notice", ["text"]),
        ("Complete", ["request"]), ("Reject", ["request", "reason"]), ("Finish", ["winner"]),
    ]
    var board = Board()
    let playerCount: Int
    var currentPlayer: Int?
    var winner: Int?
    var finished = false
    var actions: [OfferedAction] = []
    var pending: Pending?
    var result: ActionResult?
    var fault: String?
    var settingUp = true
    var notice = ""

    init(playerCount: Int) { self.playerCount = playerCount }

    func resolve(_ reference: GameEventScriptExtensionReference) throws -> (any GameEventScriptExtensionFunction)? {
        guard reference.extensionName == "board", Query.signatures[reference.functionName] == reference.argumentLabels else { return nil }
        return Query(bindings: self, name: reference.functionName)
    }

    func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws {
        let args = message.arguments
        switch message.name {
        case "CreateZone":
            guard settingUp else { throw CardGameError("Zones can only be created during setup") }
            let owner = args[1].kind == .nothing ? nil : try integer(args[1])
            guard owner == nil || (0..<playerCount).contains(owner!) else { throw CardGameError("Unknown zone owner") }
            try board.addZone(text(args[0]), owner: owner, visibility: text(args[2]))
        case "CreateCard":
            guard settingUp else { throw CardGameError("Cards can only be created during setup") }
            try board.addCard(zone: text(args[0]), properties: args[1])
        case "Shuffle":
            guard settingUp else { throw CardGameError("Setup-only shuffle") }
            try board.shuffle(text(args[0]), random: context.random)
        case "Take":
            guard settingUp else { throw CardGameError("Setup-only take") }
            try board.take(from: text(args[0]), to: text(args[1]), count: integer(args[2]))
        case "BeginTurn":
            guard pending == nil, !finished else { throw CardGameError("Cannot begin a turn during a pending action or ended game") }
            let player = try integer(args[0])
            guard (0..<playerCount).contains(player) else { throw CardGameError("Unknown player") }
            currentPlayer = player
            actions = []
            try emit("PlayerRoundStart", [("player", .integer(Int64(player)))], context)
        case "Allowed":
            guard try integer(args[0]) == currentPlayer, !finished, let list = args[1].listValue else { throw CardGameError("Invalid action offer") }
            actions = try list.map { value in
                let id = field(value, "card")
                return OfferedAction(kind: try text(field(value, "kind")), card: id.kind == .nothing ? nil : try integer(id))
            }
        case "MoveCard":
            let request = try requirePending(args[0])
            var replacement = board
            try replacement.move(integer(args[1]), from: text(args[2]), to: text(args[3]))
            board = replacement
            try complete(request, context)
        case "DrawCard":
            let request = try requirePending(args[0])
            var replacement = board
            try replacement.draw(from: text(args[1]), to: text(args[2]), count: 1, recycling: text(args[3]), keeping: integer(args[4]), random: context.random)
            board = replacement
            try complete(request, context)
        case "DrawCards":
            guard pending == nil, !finished else { throw CardGameError("Rule effects require a completed request and an active game") }
            var replacement = board
            try replacement.draw(from: text(args[0]), to: text(args[1]), count: integer(args[2]), recycling: text(args[3]), keeping: integer(args[4]), random: context.random)
            board = replacement
        case "Notice": notice = try text(args[0])
        case "Complete": try complete(requirePending(args[0]), context)
        case "Reject":
            let request = try requirePending(args[0])
            result = .init(request: request.id, accepted: false, reason: try text(args[1]))
            pending = nil
        case "Finish":
            guard pending == nil, !finished else { throw CardGameError("Cannot finish with a pending request") }
            let value = args[0].kind == .nothing ? nil : try integer(args[0])
            guard value == nil || (0..<playerCount).contains(value!) else { throw CardGameError("Unknown winner") }
            winner = value
            finished = true
            actions = []
        default: throw CardGameError("Unknown board command")
        }
    }

    private func requirePending(_ value: GesValue) throws -> Pending {
        guard let pending, pending.id == (try integer(value)) else { throw CardGameError("Unknown or completed request") }
        return pending
    }

    private func complete(_ request: Pending, _ context: GameEventScriptContext) throws {
        notice = ""
        result = .init(request: request.id, accepted: true, reason: "")
        pending = nil
        actions = []
        try emit("ActionCompleted", [("request", .integer(Int64(request.id))), ("player", .integer(Int64(request.player))), ("action", .text(request.action.kind))], context)
    }

    private func emit(_ name: String, _ values: [(String, GesValue)], _ context: GameEventScriptContext) throws {
        guard context.emit(try message(name, values)) else { throw CardGameError("Host queue rejected a board event") }
    }

    func runtimeError(_ diagnostic: GameEventScriptDiagnostic) { fault = formatDiagnostic(diagnostic) }

    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) { fault = "\(limitName): \(detail) (\(limit))" }
}

private struct Query: GameEventScriptExtensionFunction {
    static let signatures: [String: [String]] = ["cards": ["zone"], "card": ["id"], "top": ["zone"], "current": [], "playercount": [], "players": []]
    let bindings: Bindings
    let name: String

    func invoke(_ call: GesExtensionCall) throws {
        switch name {
        case "cards":
            call.setValue(.list(try bindings.board.zone(text(call.arguments[0])).cards.map { try bindings.board.card($0).value }))
        case "card":
            if call.arguments[0].kind == .nothing { call.setNothing() } else { call.setValue((try? bindings.board.card(integer(call.arguments[0])).value) ?? .nothing) }
        case "top":
            let id = try bindings.board.zone(text(call.arguments[0])).cards.last
            call.setValue(try id.map { try bindings.board.card($0).value } ?? .nothing)
        case "current": call.setValue(bindings.currentPlayer.map { .integer(Int64($0)) } ?? .nothing)
        case "playercount": call.setInteger(Int64(bindings.playerCount))
        case "players": call.setValue(.list((0..<bindings.playerCount).map { .integer(Int64($0)) }))
        default: throw CardGameError("Unknown board query")
        }
    }
}

func formatDiagnostic(_ diagnostic: GameEventScriptDiagnostic) -> String {
    let location = diagnostic.sourceLocation
    let prefix = location.map { $0.sourceName + ($0.line.map { ":\($0)" } ?? "") + ($0.column.map { ":\($0)" } ?? "") + ": " } ?? ""
    var lines = [prefix + diagnostic.code + (diagnostic.message.isEmpty ? "" : ": " + diagnostic.message)]
    if let handler = diagnostic.handlerName, !handler.isEmpty { lines.append("Handler: " + handler) }
    if let details = diagnostic.technicalDetails, !details.isEmpty, details != diagnostic.message { lines.append(details) }
    return lines.joined(separator: "\n")
}
