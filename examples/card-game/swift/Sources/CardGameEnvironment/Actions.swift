// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

struct ActionActivation {
    let isGlobal: Bool
    let id: Int
    let kind: String
    let label: String
    let button: Bool
    let optional: Bool
    let finishTurn: Bool
    let mode: String
    let zone: String?
    let area: GesValue?
    let handler: GameEventScriptMessageSignature
    let player: Int?
    let queued: Bool
    let priority: Int
    let group: Int?
    var ready: Bool
    var remaining: Int?
    var cards: [Int]?
    var consumed = false

    var offers: [OfferedAction] {
        cards.map { $0.map { OfferedAction(kind: kind, card: $0) } } ?? [OfferedAction(kind: kind)]
    }
}

struct ActionGroup {
    let id: Int
    let kind: String
    let player: Int
    let queued: Bool
    let priority: Int
    let optional: Bool
    let exclusive: Bool
    let data: GesValue
    var ready: Bool
    var selected: Int?
    var consumed = false
}

extension Bindings {
    func requireActionEditing() throws {
        guard
            [.prepareGame, .beginRound, .beginTurn, .endTurn, .endRound].contains(phase)
                || (phase == .action && pending == nil && result?.accepted == true), !endRequested
        else {
            throw CardGameError("Action editing requires a lifecycle hook or an accepted action")
        }
    }

    func actionCards(_ value: GesValue) throws -> [Int] {
        guard let values = value.listValue, values.count <= 512 else { throw CardGameError("Action cards must be a list of card IDs") }
        let ids = try values.map { try integer($0) }
        guard Set(ids).count == ids.count else { throw CardGameError("Duplicate action card") }
        for id in ids { _ = try board.card(id) }
        return ids
    }

    func checkedPlayer(_ value: GesValue) throws -> Int {
        let player = try integer(value)
        guard (0..<playerCount).contains(player) else { throw CardGameError("Unknown player") }
        return player
    }

    func boolean(_ spec: GesValue, _ key: String, default fallback: Bool) throws -> Bool {
        let value = field(spec, key)
        if value.kind == .nothing { return fallback }
        guard value.kind == .boolean else { throw CardGameError("\(key) must be Boolean") }
        return value.asBoolean
    }

    func validateFields(_ value: GesValue, _ allowed: [String]) throws {
        guard let entries = value.mapEntries, entries.allSatisfy({ allowed.contains($0.key) }) else { throw CardGameError("Unknown action specification field") }
    }

    func activate(_ args: GameEventScriptMessageArguments) throws {
        try requireActionEditing()
        if args.signatureLabels == ["spec"] {
            try activateSpec(args[0])
        } else {
            try activateSpec(.map(args.signatureLabels.enumerated().map { .init(key: $0.element, value: args[$0.offset]) }))
        }
    }

    func setupActions(_ value: GesValue) throws {
        if value.kind == .nothing { return }
        guard let entries = value.listValue else { throw CardGameError("Setup actions must be a list") }
        for entry in entries { try activateSpec(entry, isGlobal: true) }
    }

    func activateGroup(_ spec: GesValue) throws {
        try requireActionEditing()
        try validateFields(spec, ["group", "player", "priority", "optional", "exclusive", "actions", "data"])
        let kind = try tag(field(spec, "group"))
        let queued = field(spec, "player").kind != .nothing
        guard queued || currentPlayer != nil else { throw CardGameError("Preparation offers require a player") }
        let player = queued ? try checkedPlayer(field(spec, "player")) : currentPlayer!
        try validateTag(kind, player: player, isGlobal: false)
        guard let alternatives = field(spec, "actions").listValue, !alternatives.isEmpty else { throw CardGameError("Group requires alternatives") }
        let priority = field(spec, "priority").kind == .nothing ? 0 : try integer(field(spec, "priority"))
        let group = ActionGroup(
            id: nextActivation,
            kind: kind,
            player: player,
            queued: queued,
            priority: priority,
            optional: try boolean(spec, "optional", default: false),
            exclusive: try boolean(spec, "exclusive", default: true),
            data: field(spec, "data"),
            ready: !queued
        )
        nextActivation += 1
        groups.append(group)
        for alternative in alternatives { try activateSpec(alternative, group: group) }
    }

    private func validateTag(_ kind: String, player: Int?, isGlobal: Bool) throws {
        guard !kind.isEmpty, kind.utf8.count <= 80, activations.count + groups.count < 256,
            !activations.contains(where: { $0.kind == kind && (isGlobal || $0.isGlobal || $0.player == player) }),
            !groups.contains(where: { $0.kind == kind && (isGlobal || $0.player == player) })
        else { throw CardGameError("Duplicate action tag for this player or too many offers") }
    }

    private func activateSpec(_ spec: GesValue, isGlobal: Bool = false, group: ActionGroup? = nil) throws {
        let base = ["action", "label", "button", "finishTurn", "consumable", "handler", "area", "zone", "cards"]
        try validateFields(spec, base + (group == nil ? ["optional", "priority"] + (isGlobal ? [] : ["player"]) : []))
        let queued = group?.queued ?? (!isGlobal && field(spec, "player").kind != .nothing)
        let player = try group?.player ?? (isGlobal ? nil : queued ? checkedPlayer(field(spec, "player")) : currentPlayer)
        guard isGlobal || player != nil else { throw CardGameError("Preparation offers require a player") }
        let kind = try tag(field(spec, "action"))
        try validateTag(kind, player: player, isGlobal: isGlobal)
        let captions = (spec.mapEntries ?? []).filter { ["label", "button"].contains($0.key) }
        guard captions.count == 1 else { throw CardGameError("Action requires exactly one label or button") }
        let button = captions[0].key == "button"
        let label = try text(captions[0].value)
        let optional = try group?.optional ?? boolean(spec, "optional", default: true)
        let finishTurn = try boolean(spec, "finishTurn", default: false)
        guard !waitingForRound || queued || !finishTurn else { throw CardGameError("Between-round actions use NextRound, not finishTurn") }
        let priority = try group?.priority ?? (field(spec, "priority").kind == .nothing ? 0 : integer(field(spec, "priority")))
        let consumption = field(spec, "consumable")
        let mode: String
        let remaining: Int?
        if consumption.kind == .nothing || consumption.kind == .tag {
            mode = consumption.kind == .nothing ? "auto" : try tag(consumption)
            guard ["auto", "manual", "never"].contains(mode) else { throw CardGameError("Unknown consumption mode") }
            remaining = mode == "auto" ? 1 : nil
        } else {
            mode = "count"
            let count = try integer(consumption)
            guard (1...1000).contains(count) else { throw CardGameError("Consumption count must be 1–1000") }
            remaining = count
        }
        guard mode != "never" || (optional && !finishTurn) else { throw CardGameError("#never requires optional: true and finishTurn: false") }
        let targets = ["area", "zone", "cards"].filter { field(spec, $0).kind != .nothing }
        guard targets.count == 1 else { throw CardGameError("Action requires exactly one area, zone or cards target") }
        let zone = targets[0] == "zone" ? try tag(field(spec, "zone")) : nil
        if let zone { _ = try board.zone(zone) }
        let area = targets[0] == "area" ? field(spec, "area") : nil
        if let area, area.kind != .tag || area.asText != "table" { _ = try checkedPlayer(area) }
        let cards = targets[0] == "cards" ? try actionCards(field(spec, "cards")) : nil
        let labels = ["action"] + (isGlobal ? [] : ["player"]) + (cards == nil ? [] : ["card"])
        guard let handler = field(spec, "handler").signatureValue, handler.parameters == labels else { throw CardGameError("Expected a handler with parameters " + labels.joined(separator: ", ")) }
        activations.append(
            .init(
                isGlobal: isGlobal,
                id: nextActivation,
                kind: kind,
                label: label,
                button: button,
                optional: optional,
                finishTurn: finishTurn,
                mode: mode,
                zone: zone,
                area: area,
                handler: handler,
                player: player,
                queued: queued,
                priority: priority,
                group: group?.id,
                ready: isGlobal || !queued,
                remaining: remaining,
                cards: cards
            )
        )
        nextActivation += 1
    }

    var requiredPriorities: [Int] {
        activations.filter { $0.group == nil && !$0.optional && !$0.consumed && ($0.isGlobal || ($0.ready && $0.player == currentPlayer)) }.map(\.priority)
            + groups.filter { !$0.optional && !$0.consumed && $0.ready && $0.player == currentPlayer }.map(\.priority)
    }

    var hasRequiredActions: Bool { !requiredPriorities.isEmpty }

    var offeredActivations: [ActionActivation] {
        let floor = requiredPriorities.max()
        return activations.filter { action in
            guard !action.consumed, action.isGlobal || (action.ready && action.player == currentPlayer) else { return false }
            if let id = action.group {
                guard let group = groups.first(where: { $0.id == id }), !group.consumed,
                    group.selected == nil || !group.exclusive || group.selected == action.id
                else { return false }
            }
            return action.isGlobal || floor == nil || action.priority >= floor!
        }.sorted { $0.isGlobal == $1.isGlobal ? ($0.priority == $1.priority ? $0.id < $1.id : $0.priority > $1.priority) : !$0.isGlobal }
    }

    func prepareTurnActions() {
        // Previous turn offers have already been cleaned up, including EndTurn emissions.
        for index in activations.indices where activations[index].player == currentPlayer { activations[index].ready = true }
        for index in groups.indices where groups[index].player == currentPlayer { groups[index].ready = true }
    }

    func cleanupActions(player: Int?) {
        removeOffers(
            actions: Set(activations.filter { !$0.isGlobal && $0.player == player && (!$0.queued || $0.consumed) }.map(\.id)),
            groups: Set(groups.filter { $0.player == player && (!$0.queued || $0.consumed) }.map(\.id))
        )
        for index in activations.indices where activations[index].player == player { activations[index].ready = false }
        for index in groups.indices where groups[index].player == player { groups[index].ready = false }
    }

    func clearActions(_ selection: GesValue?) throws {
        try requireActionEditing()
        let ids: Set<Int>
        if let selection {
            guard let entries = selection.listValue else { throw CardGameError("ClearActions expects queried action entries") }
            ids = Set(try entries.map { try integer(field($0, "ref")) })
        } else {
            ids = Set(activations.filter { !$0.isGlobal && $0.player == currentPlayer }.map(\.id) + groups.filter { $0.player == currentPlayer }.map(\.id))
        }
        removeOffers(actions: ids, groups: ids)
    }

    func removeTurnActions() {
        removeOffers(actions: Set(activations.filter { !$0.isGlobal && !$0.queued }.map(\.id)), groups: Set(groups.filter { !$0.queued }.map(\.id)))
    }

    func removeAllActions() {
        activations = []
        groups = []
    }

    private func removeOffers(actions ids: Set<Int>, groups groupIDs: Set<Int>) {
        activations.removeAll { ids.contains($0.id) || $0.group.map { groupIDs.contains($0) } == true }
        groups.removeAll { group in groupIDs.contains(group.id) || !activations.contains(where: { $0.group == group.id }) }
        for index in groups.indices where groups[index].selected.map({ ids.contains($0) }) == true { groups[index].selected = nil }
    }

    func actionEntries(player: Int) throws -> GesValue {
        func entry(_ pairs: [(String, GesValue)]) -> GesValue { .map(pairs.map { .init(key: $0.0, value: $0.1) }) }
        let actions = try activations.filter { $0.player == player }.map { action in
            try entry([
                ("ref", .integer(Int64(action.id))), ("id", .tag(action.kind)), ("kind", .tag("action")), ("player", .integer(Int64(player))),
                ("priority", .integer(Int64(action.priority))), ("optional", .boolean(action.optional)), ("queued", .boolean(action.queued)), ("active", .boolean(action.ready && action.player == currentPlayer)),
                ("consumed", .boolean(action.consumed)),
                ("group", action.group.flatMap { id in try groups.first { $0.id == id }.map { try .tag($0.kind) } } ?? .nothing),
                ("remaining", action.remaining.map { .integer(Int64($0)) } ?? .nothing),
            ])
        }
        return .list(
            try groups.filter { $0.player == player }.map { group in
                try entry([
                    ("ref", .integer(Int64(group.id))), ("id", .tag(group.kind)), ("kind", .tag("group")), ("player", .integer(Int64(player))),
                    ("priority", .integer(Int64(group.priority))), ("optional", .boolean(group.optional)), ("queued", .boolean(group.queued)), ("active", .boolean(group.ready && group.player == currentPlayer)),
                    ("exclusive", .boolean(group.exclusive)), ("consumed", .boolean(group.consumed)), ("data", group.data),
                ])
            } + actions
        )
    }

    func settleAction(_ id: Int) throws {
        guard !endRequested else { return }
        if let index = activations.firstIndex(where: { $0.id == id }) {
            if let remaining = activations[index].remaining, !activations[index].consumed {
                activations[index].remaining = remaining - 1
                activations[index].consumed = remaining == 1
            }
            if let groupID = activations[index].group, let groupIndex = groups.firstIndex(where: { $0.id == groupID }) {
                if groups[groupIndex].exclusive { groups[groupIndex].selected = id }
                if activations[index].consumed {
                    groups[groupIndex].consumed = true
                    for other in activations.indices where activations[other].group == groupID { activations[other].consumed = true }
                }
            }
            if activations[index].consumed && activations[index].finishTurn { finishRequested = true }
        }
        if !waitingForRound && finishRequested && !hasRequiredActions { turnEnded = true }
    }
}
