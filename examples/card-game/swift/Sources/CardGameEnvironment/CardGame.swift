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
    /// Winners selected by the rules, in supplied order. Empty before completion or when nobody wins.
    public var winners: [Int] { bindings.winners }
    /// Whether the rules have ended the game, including a draw.
    public var finished: Bool { bindings.finished }
    /// Most recent modal notice in the current request, or an empty string.
    public var notice: String { bindings.notice }
    /// Latest complete action offer, for the active player only.
    public var actions: [OfferedAction] { bindings.actions }
    /// Revision used to reject stale UI requests; increments after accepted actions.
    public private(set) var revision = 0
    /// One-based turn number; actions within a turn do not advance it.
    public private(set) var turn = 0
    /// One-based round number; skipped seats count toward completing the current circuit.
    public var round: Int { bindings.round }
    /// Whether a rule/runtime fault has stopped this session. Construct a new session to restart.
    public private(set) var failed = false
    /// Copied zone state for trusted native inspection; use viewJSON for player-filtered output.
    public var zones: [Zone] { bindings.board.zones }
    /// Immutable card definitions for trusted native inspection, including hidden cards.
    public var cards: [Card] { bindings.board.cards }

    let bindings: Bindings
    private let host: GameEventScriptHost
    private var nextRequest = 1
    private var roundProgress = 0
    private var roundOpen = false
    private var hasEndRound = false

    /// Compiles the supplied GES, starts a seeded host, and runs PrepareGame(players) and its queued messages before automatically starting the first round with player 0.
    /// Accepts one to four named players. Game-specific player limits belong to the rules.
    /// Throws for invalid player counts, compiler diagnostics, binding errors, or bounded execution failure.
    public init(rules: String, players: [String] = ["Alice", "Bob"], seed: Int64 = 42) throws {
        guard (1...4).contains(players.count), players.allSatisfy({ !$0.isEmpty }) else { throw CardGameError("Provide one to four named players") }
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
            hasEndRound = program.bindings.contains { binding in
                program.stringConstants[Int(binding.name)] == "EndRound" && binding.requiredTags.isEmpty
                    && (binding.kind == .messageNameHandler || (binding.kind == .messageHandler && binding.argumentNames.map { program.stringConstants[Int($0)] } == ["number", "players"]))
            }
            _ = try host.load(program)
        } catch let error as GameEventScriptCompileError {
            throw CardGameError(error.diagnostics.map(formatDiagnostic).joined(separator: "\n"))
        }
        for (name, labels) in Bindings.commands {
            _ = try host.subscribe(GameEventScriptMessageSignature(name: name, parameters: labels), handler: bindings)
        }
        guard try host.start().state == .ready else { throw CardGameError("GES initialization failed") }
        try event("PrepareGame", [("players", bindings.playerValues)], phase: .prepareGame)
        bindings.currentPlayer = 0
        try beginRound()
        if !finished { try beginTurn() }
    }

    /// Submits a selected action without pre-moving cards. GES revalidates against current state.
    /// Stale revisions, invalid players, and ended games are rejected before rule execution.
    /// Throws on runtime failure and permanently stops this session; no rollback of previous commands is promised.
    public func submit(player: Int, action: OfferedAction, revision expectedRevision: Int) throws -> ActionResult {
        guard !failed else { throw CardGameError("Session failed; restart required") }
        bindings.notices = []
        bindings.notice = ""
        let request = nextRequest
        nextRequest += 1

        func reject(_ reason: String) -> ActionResult { .init(request: request, accepted: false, reason: reason) }

        guard players.indices.contains(player) else { return reject("Unknown player") }
        guard !finished else { return reject("Game has ended") }
        guard expectedRevision == revision else { return reject("Stale action; refresh the board") }
        guard action.kind.utf8.count <= 80 else { return reject("Invalid action") }
        guard let turnPlayer = currentPlayer else { return reject("Action was not offered") }
        bindings.phase = .action
        guard let activation = bindings.offeredActivations.first(where: { $0.kind == action.kind }), activation.offers.contains(action) else { return reject("Action has expired") }
        guard activation.isGlobal || player == currentPlayer else { return reject("Action was not offered to this player") }
        bindings.executingAction = activation.id
        bindings.pending = .init(id: request, player: player, action: action)
        bindings.result = nil
        do {
            var arguments: [(String, GesValue)] = [("action", .integer(Int64(activation.id)))]
            if !activation.isGlobal { arguments.append(("player", .integer(Int64(player)))) }
            if activation.cards != nil { arguments.append(("card", action.card.map { .integer(Int64($0)) } ?? .nothing)) }
            try receive(activation.handler.name, arguments)
            try pump()
            guard let result = bindings.result, bindings.pending == nil else { throw CardGameError("Rules did not explicitly complete or reject the request") }
            if result.accepted {
                try bindings.settleAction(activation.id)
                revision += 1
                if bindings.endRequested {
                    try endGame()
                } else if bindings.waitingForRound {
                    try resumeRoundIfRequested()
                    if !finished && !bindings.waitingForRound { try beginTurn() }
                } else if bindings.turnEnded {
                    try advanceTurn(after: turnPlayer)
                    if !finished && !bindings.waitingForRound { try beginTurn() }
                }
                guard finished || bindings.waitingForRound || !actions.isEmpty else { throw CardGameError("Rules must offer another action or emit NextPlayersTurn()") }
            }
            bindings.executingAction = nil
            bindings.phase = finished ? .finished : .idle
            return result
        } catch {
            failed = true
            bindings.removeAllActions()
            throw error
        }
    }

    private func event(_ name: String, _ values: [(String, GesValue)], phase: Bindings.Phase) throws {
        bindings.phase = phase
        try receive(name, values)
        try pump()
    }

    private func beginRound() throws {
        bindings.skipCount = 0
        bindings.round += 1
        roundOpen = true
        try event("BeginRound", [("number", .integer(Int64(round))), ("players", bindings.playerValues)], phase: .beginRound)
        if bindings.endRequested { try endGame() }
    }

    private func endRound(waitForNext: Bool = false) throws {
        guard roundOpen else { return }
        roundOpen = false
        bindings.waitingForRound = waitForNext
        bindings.nextRoundRequested = waitForNext && !hasEndRound
        bindings.turnEnded = false
        bindings.finishRequested = false
        bindings.removeTurnActions()
        try event("EndRound", [("number", .integer(Int64(round))), ("players", bindings.playerValues)], phase: .endRound)
    }

    private func resumeRoundIfRequested() throws {
        guard bindings.waitingForRound, bindings.nextRoundRequested else { return }
        guard !bindings.hasRequiredActions else { return }
        bindings.cleanupActions(player: bindings.currentPlayer)
        bindings.waitingForRound = false
        bindings.nextRoundRequested = false
        try beginRound()
    }

    private func advanceTurn(after player: Int) throws {
        try event("EndTurn", [("player", .integer(Int64(player)))], phase: .endTurn)
        if bindings.endRequested {
            try endGame()
            return
        }
        bindings.cleanupActions(player: player)
        let next = bindings.nextPlayer(after: player)
        roundProgress += 1 + bindings.skipCount
        bindings.currentPlayer = next
        if roundProgress >= players.count {
            roundProgress %= players.count
            try endRound(waitForNext: true)
            if bindings.endRequested { try endGame() }
            if !finished { try resumeRoundIfRequested() }
        }
    }

    private func beginTurn() throws {
        // Bound automatic skips across separate host pumps without recursive calls.
        for _ in 0..<128 {
            turn += 1
            bindings.turnEnded = false
            bindings.finishRequested = false
            bindings.skipCount = 0
            bindings.prepareTurnActions()
            let player = bindings.currentPlayer!
            try event("BeginTurn", [("player", .integer(Int64(player)))], phase: .beginTurn)
            if bindings.endRequested { try endGame() }
            if finished { return }
            if bindings.finishRequested {
                guard !bindings.hasRequiredActions else { throw CardGameError("Required actions remain") }
                bindings.turnEnded = true
                try advanceTurn(after: player)
                if finished || bindings.waitingForRound { return }
            } else {
                guard !actions.isEmpty else { throw CardGameError("BeginTurn must offer actions, request NextPlayersTurn or end the game") }
                bindings.phase = .idle
                return
            }
        }
        throw CardGameError("Automatic turn advancement exceeded its budget")
    }

    private func endGame() throws {
        try endRound()
        bindings.waitingForRound = false
        bindings.removeAllActions()
        try event("EndGame", [("players", bindings.playerValues)], phase: .endGame)
        guard finished else { throw CardGameError("EndGame must emit Finish(winners), using an empty list when nobody wins") }
        bindings.phase = .finished
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
    enum Phase { case prepareGame, beginRound, beginTurn, action, endTurn, endRound, endGame, idle, finished }

    struct Pending {
        let id: Int
        let player: Int
        let action: OfferedAction
    }

    static let commands: [(String, [String])] = [
        ("BeginRound", ["number", "players"]), ("EndRound", ["number", "players"]),
        ("Action", ["spec"]), ("ActionGroup", ["spec"]), ("ClearActions", []), ("ClearActions", ["actions"]),
        ("Action", ["action", "label", "optional", "finishTurn", "consumable", "handler", "area"]),
        ("Action", ["action", "label", "optional", "finishTurn", "consumable", "handler", "cards"]),
        ("Action", ["action", "label", "optional", "finishTurn", "consumable", "handler", "zone"]),
        ("Action", ["action", "button", "optional", "finishTurn", "consumable", "handler", "area"]),
        ("Action", ["action", "button", "optional", "finishTurn", "consumable", "handler", "cards"]),
        ("Action", ["action", "button", "optional", "finishTurn", "consumable", "handler", "zone"]),
        ("SetActionCards", ["action", "cards"]), ("ConsumeAction", ["action"]),
        ("NextPlayersTurn", []), ("NextPlayersTurn", ["nextPlayer"]), ("NextPlayersTurn", ["repeatTurnForPlayer"]), ("NextRound", []),
        ("EndGame", []),
        ("Notice", ["_"]), ("Notice", ["_", "title"]), ("NoticeTable", ["_"]),
        ("NoticeTable", ["_", "pushOld"]), ("NoticeTable", ["_", "stackClear"]), ("NoticeTable", ["pop"]),
        ("ZoneBadge", ["_", "zone"]), ("ZoneBadge", ["_", "zone", "color"]),
        ("TableBadge", ["_"]), ("TableBadge", ["_", "color"]),
        ("PlayerBadge", ["_", "player"]), ("PlayerBadge", ["_", "player", "color"]),
        ("Complete", ["action"]), ("Reject", ["action", "reason"]), ("Finish", ["winners"]),
    ]
    var board = Board()
    var gameCreated = false
    var round = 0
    var waitingForRound = false
    var nextRoundRequested = false
    var direction = 1
    var state: [String: GesValue] = [:]
    var playerState: [Int: [String: GesValue]] = [:]
    let playerCount: Int
    var currentPlayer: Int?
    var winners: [Int] = []
    var finished = false
    var activations: [ActionActivation] = []
    var groups: [ActionGroup] = []
    var nextActivation = 1
    var executingAction: Int?
    var finishRequested = false
    var actions: [OfferedAction] {
        offeredActivations.flatMap(\.offers)
    }

    var pending: Pending?
    var result: ActionResult?
    var fault: String?
    var phase = Phase.prepareGame
    var turnEnded = false
    var endRequested = false
    var skipCount = 0
    var lastAction = ""
    var playerValues: GesValue { .list((0..<playerCount).map { .integer(Int64($0)) }) }

    func nextPlayer(after player: Int) -> Int { (player + direction * (1 + skipCount) + playerCount * 2) % playerCount }

    var notice = ""
    var notices: [(text: String, title: String)] = []
    var tableNotice = ""
    var tableNoticeStack: [String] = []
    var playerBadges: [Int: PresentationBadge] = [:]
    var zoneBadges: [String: PresentationBadge] = [:]
    var tableBadge: PresentationBadge?

    init(playerCount: Int) { self.playerCount = playerCount }

    func resolve(_ reference: GameEventScriptExtensionReference) throws -> (any GameEventScriptExtensionFunction)? {
        guard reference.extensionName == "board" else { return nil }
        if reference.functionName == "state", reference.argumentLabels == ["player", "key"] { return BoardExtension(bindings: self, name: "playerstate") }
        if reference.functionName == "setstate", reference.argumentLabels == ["player", "key", "value"] { return BoardExtension(bindings: self, name: "setplayerstate") }
        guard BoardExtension.signatures[reference.functionName] == reference.argumentLabels else { return nil }
        return BoardExtension(bindings: self, name: reference.functionName)
    }

    func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws {
        let args = message.arguments
        switch message.name {
        case "BeginRound", "EndRound": break  // Optional script hooks still have a native recipient.
        case "NextPlayersTurn":
            guard !endRequested, !waitingForRound else { throw CardGameError("Use NextRound between rounds") }
            if phase == .action {
                guard pending == nil, result?.accepted == true, !turnEnded else { throw CardGameError("NextPlayersTurn requires an accepted action") }
                finishRequested = true
            } else if phase == .beginTurn {
                finishRequested = true
            } else {
                guard phase == .endTurn else { throw CardGameError("NextPlayersTurn is available in BeginTurn, after action acceptance or during EndTurn(player)") }
            }
            if args.signatureLabels == ["nextPlayer"] {
                let next = try integer(args[0])
                guard (0..<playerCount).contains(next), let currentPlayer else { throw CardGameError("Unknown next player") }
                let distance = ((next - currentPlayer) * direction + playerCount) % playerCount
                skipCount = (distance == 0 ? playerCount : distance) - 1
            } else if args.signatureLabels == ["repeatTurnForPlayer"] {
                guard args[0].kind == .boolean else { throw CardGameError("repeatTurnForPlayer must be Boolean") }
                skipCount = args[0].asBoolean ? playerCount - 1 : 0
            } else {
                skipCount = 0
            }
        case "NextRound":
            if endRequested { break }
            guard waitingForRound, phase == .endRound || (phase == .action && pending == nil && result?.accepted == true) else {
                throw CardGameError("NextRound requires EndRound or an accepted between-round action")
            }
            nextRoundRequested = true
        case "ClearActions":
            try clearActions(args.signatureLabels.isEmpty ? nil : args[0])
        case "ActionGroup":
            try activateGroup(args[0])
        case "EndGame":
            guard [.beginRound, .beginTurn, .action, .endTurn, .endRound].contains(phase), pending == nil, !endRequested else { throw CardGameError("Cannot end the game in this phase") }
            endRequested = true
            removeAllActions()
        case "Action":
            try activate(args)
        case "SetActionCards":
            try requireActionEditing()
            let kind = try tag(args[0])
            guard let index = activations.firstIndex(where: { $0.kind == kind && ($0.isGlobal || $0.player == currentPlayer) && !$0.consumed }), activations[index].cards != nil else { throw CardGameError("Unknown card action") }
            activations[index].cards = try actionCards(args[1])
        case "ConsumeAction":
            guard phase == .action, pending == nil, result?.accepted == true, executingAction == (try integer(args[0])),
                let index = activations.firstIndex(where: { $0.id == executingAction }), activations[index].mode == "manual", !activations[index].consumed
            else { throw CardGameError("ConsumeAction requires the successfully completed manual action") }
            activations[index].consumed = true
        case "Notice":
            let message = try text(args[0])
            let title = try args.count == 2 ? text(args[1]) : "Game Notice"
            notice = message
            notices.append((text: message, title: title))
        case "NoticeTable":
            if args.signatureLabels == ["pop"] {
                guard args[0].kind == .boolean else { throw CardGameError("pop must be Boolean") }
                if args[0].asBoolean, let previous = tableNoticeStack.popLast() { tableNotice = previous }
            } else {
                let replacement = try text(args[0])
                if args.count == 2 {
                    guard args[1].kind == .boolean else { throw CardGameError("NoticeTable flags must be Boolean") }
                    if args[1].asBoolean {
                        if args.signatureLabels[1] == "pushOld" {
                            guard tableNoticeStack.count < 64 else { throw CardGameError("Maximum 64 saved table notices") }
                            tableNoticeStack.append(tableNotice)
                        } else {
                            tableNoticeStack.removeAll(keepingCapacity: true)
                        }
                    }
                }
                tableNotice = replacement
            }
        case "PlayerBadge", "ZoneBadge", "TableBadge":
            let label = try text(args[0])
            let color = args.signatureLabels.last == "color" ? try tag(args[args.count - 1]) : "green"
            guard ["green", "red", "yellow", "blue", "gray"].contains(color) else { throw CardGameError("Invalid badge color") }
            let badge = label.isEmpty ? nil : PresentationBadge(text: label, color: color)
            switch message.name {
            case "PlayerBadge": playerBadges[try checkedPlayer(args[1])] = badge
            case "ZoneBadge":
                let zone = try tag(args[1])
                _ = try board.zoneIndex(zone)
                zoneBadges[zone] = badge
            default: tableBadge = badge
            }
        case "Complete": complete(try requirePending(args[0]))
        case "Reject":
            let request = try requirePending(args[0])
            result = .init(request: request.id, accepted: false, reason: try text(args[1]))
            pending = nil
        case "Finish":
            guard phase == .endGame, !finished else { throw CardGameError("Finish is only available in EndGame") }
            guard let entries = args[0].listValue else { throw CardGameError("Winners must be a list of player IDs") }
            let values = try entries.map(integer)
            guard Set(values).count == values.count, values.allSatisfy({ (0..<playerCount).contains($0) }) else { throw CardGameError("Invalid or duplicate winner") }
            winners = values
            finished = true
            removeAllActions()
        default: throw CardGameError("Unknown board command")
        }
    }

    func requireBoardMutation() throws {
        guard [.prepareGame, .beginRound, .endRound, .action, .endTurn].contains(phase), !finished else {
            throw CardGameError("Board mutation is unavailable in this phase")
        }
    }

    func createBoard(_ setup: GesValue) throws {
        guard phase == .prepareGame, !gameCreated else { throw CardGameError("Board creation is allowed once during preparation") }
        // Validate zones, cards and global actions before installing any of them.
        let staged = Bindings(playerCount: playerCount)
        staged.board = try Board.create(setup, playerCount: playerCount)
        staged.activations = activations
        staged.groups = groups
        staged.nextActivation = nextActivation
        try staged.setupActions(field(setup, "actions"))
        board = staged.board
        activations = staged.activations
        nextActivation = staged.nextActivation
        gameCreated = true
    }

    private func requirePending(_ value: GesValue) throws -> Pending {
        guard phase == .action, let pending, executingAction == (try integer(value)) else { throw CardGameError("Unknown or completed request") }
        return pending
    }

    private func complete(_ request: Pending) {
        result = .init(request: request.id, accepted: true, reason: "")
        pending = nil
        lastAction = request.action.kind
    }

    func runtimeError(_ diagnostic: GameEventScriptDiagnostic) { fault = formatDiagnostic(diagnostic) }

    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) { fault = "\(limitName): \(detail) (\(limit))" }
}

private struct BoardExtension: GameEventScriptExtensionFunction {
    static let signatures: [String: [String]] = [
        "actions": ["player"], "cards": ["zone"], "card": ["id"], "top": ["zone"], "current": [], "playercount": [], "players": [], "next": ["player"], "table": [], "zones": ["player"], "lastaction": [], "roundnumber": [], "direction": [],
        "state": ["key"], "ending": [],
        "create": ["setup"], "draw": ["source", "destination"], "take": ["source", "destination", "count"],
        "move": ["card", "source", "destination"], "movecards": ["source", "destination", "cards"],
        "setorder": ["zone", "cards"], "setstate": ["key", "value"], "reverse": [],
    ]
    let bindings: Bindings
    let name: String

    func invoke(_ call: GesExtensionCall) throws {
        let args = call.arguments
        switch name {
        case "create":
            try bindings.createBoard(args[0])
            call.setNothing()
        case "draw", "take", "move", "movecards", "setorder":
            try bindings.requireBoardMutation()
            switch name {
            case "draw":
                let source = try tag(args[0])
                let destination = try tag(args[1])
                let pile = try bindings.board.zone(source)
                _ = try bindings.board.zone(destination)
                guard source != destination else { throw CardGameError("Source and destination must differ") }
                if let id = pile.cards.last {
                    try bindings.board.move(id, from: source, to: destination)
                    call.setValue(try bindings.board.card(id).value)
                } else {
                    call.setNothing()
                }
            case "take":
                let source = try tag(args[0])
                let destination = try tag(args[1])
                let count = try integer(args[2])
                try bindings.board.take(from: source, to: destination, count: count)
                call.setValue(.list(try bindings.board.zone(destination).cards.suffix(count).map { try bindings.board.card($0).value }))
            case "move":
                let id = try integer(args[0])
                try bindings.board.move(id, from: tag(args[1]), to: tag(args[2]))
                call.setValue(try bindings.board.card(id).value)
            case "movecards":
                guard let values = args[2].listValue else { throw CardGameError("Expected card IDs") }
                let ids = try values.map(integer)
                try bindings.board.moveCards(ids, from: tag(args[0]), to: tag(args[1]))
                call.setValue(.list(try ids.map { try bindings.board.card($0).value }))
            default:
                guard let values = args[1].listValue else { throw CardGameError("Expected card IDs") }
                try bindings.board.setCardOrder(values.map(integer), zone: tag(args[0]))
                call.setNothing()
            }
        case "reverse":
            try bindings.requireBoardMutation()
            bindings.direction = -bindings.direction
            call.setInteger(Int64(bindings.direction))
        case "setstate", "setplayerstate":
            guard !bindings.finished else { throw CardGameError("Game state is no longer editable") }
            if name == "setplayerstate" {
                let player = try bindings.checkedPlayer(args[0])
                try setState(&bindings.playerState[player, default: [:]], key: tag(args[1]), value: args[2])
            } else {
                try setState(&bindings.state, key: tag(args[0]), value: args[1])
            }
            call.setNothing()
        case "actions": call.setValue(try bindings.actionEntries(player: try bindings.checkedPlayer(call.arguments[0])))
        case "playerstate": call.setValue(bindings.playerState[try bindings.checkedPlayer(call.arguments[0])]?[try tag(call.arguments[1])] ?? .nothing)
        case "cards":
            call.setValue(.list(try bindings.board.zone(tag(call.arguments[0])).cards.map { try bindings.board.card($0).value }))
        case "card":
            if call.arguments[0].kind == .nothing { call.setNothing() } else { call.setValue((try? bindings.board.card(integer(call.arguments[0])).value) ?? .nothing) }
        case "top":
            let id = try bindings.board.zone(tag(call.arguments[0])).cards.last
            call.setValue(try id.map { try bindings.board.card($0).value } ?? .nothing)
        case "next":
            let player = try integer(call.arguments[0])
            guard (0..<bindings.playerCount).contains(player) else { throw CardGameError("Unknown player") }
            call.setInteger(Int64(bindings.nextPlayer(after: player)))
        case "ending": call.setValue(.boolean(bindings.endRequested))
        case "direction": call.setInteger(Int64(bindings.direction))
        case "state": call.setValue(bindings.state[try tag(call.arguments[0])] ?? .nothing)
        case "roundnumber": call.setValue(.integer(Int64(bindings.round)))
        case "table": call.setValue(.list(try bindings.board.zones.filter { $0.owner == nil }.map(zoneValue)))
        case "zones":
            let player = try integer(call.arguments[0])
            guard (0..<bindings.playerCount).contains(player) else { throw CardGameError("Unknown player") }
            call.setValue(.list(try bindings.board.zones.filter { $0.owner == player }.map(zoneValue)))
        case "lastaction": call.setText(bindings.lastAction)
        case "current": call.setValue(bindings.currentPlayer.map { .integer(Int64($0)) } ?? .nothing)
        case "playercount": call.setInteger(Int64(bindings.playerCount))
        case "players": call.setValue(.list((0..<bindings.playerCount).map { .integer(Int64($0)) }))
        default: throw CardGameError("Unknown board query")
        }
    }
}

private func zoneValue(_ zone: Zone) throws -> GesValue {
    try .map([
        .init(key: "id", value: .tag(zone.id)), .init(key: "label", value: .text(zone.label)), .init(key: "position", value: .tag(zone.position)),
        .init(key: "row", value: .integer(Int64(zone.row))), .init(key: "layout", value: .tag(zone.layout)),
        .init(key: "count", value: .integer(Int64(zone.cards.count))),
    ])
}

func formatDiagnostic(_ diagnostic: GameEventScriptDiagnostic) -> String {
    let location = diagnostic.sourceLocation
    let prefix = location.map { $0.sourceName + ($0.line.map { ":\($0)" } ?? "") + ($0.column.map { ":\($0)" } ?? "") + ": " } ?? ""
    var lines = [prefix + diagnostic.code + (diagnostic.message.isEmpty ? "" : ": " + diagnostic.message)]
    if let handler = diagnostic.handlerName, !handler.isEmpty { lines.append("Handler: " + handler) }
    if let details = diagnostic.technicalDetails, !details.isEmpty, details != diagnostic.message { lines.append(details) }
    return lines.joined(separator: "\n")
}

private func setState(_ state: inout [String: GesValue], key: String, value: GesValue) throws {
    guard value.kind == .nothing || state[key] != nil || state.count < 64 else { throw CardGameError("Maximum 64 rule state keys") }
    if value.kind == .nothing { state.removeValue(forKey: key) } else { state[key] = value }
}
