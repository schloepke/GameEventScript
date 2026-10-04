// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

struct ZoneRow {
    let owner: Int?
    let index: Int
    let position: String
}

extension Board {
    static func create(_ setup: GesValue, playerCount: Int) throws -> Board {
        try fields(setup, allowed: ["table", "players", "actions"])
        var result = Board()
        try result.setupZones(field(setup, "table"), owner: nil)
        var owners = Set<Int>()
        for player in try list(field(setup, "players")) {
            try fields(player, allowed: ["id", "zones"])
            let owner = try integer(field(player, "id"))
            guard (0..<playerCount).contains(owner), owners.insert(owner).inserted else { throw CardGameError("Invalid or duplicate player setup") }
            try result.setupZones(field(player, "zones"), owner: owner)
        }
        guard owners.count == playerCount else { throw CardGameError("Setup must describe every player exactly once") }
        return result
    }

    private mutating func setupZones(_ values: GesValue, owner: Int?) throws {
        var currentRows: [String: Int] = [:]
        var playerPosition = "left"
        for value in try list(values) {
            try fields(value, allowed: ["id", "label", "position", "newRow", "layout", "visibility", "cards"])
            let rowFlag = field(value, "newRow")
            guard rowFlag.kind == .nothing || rowFlag.kind == .boolean else { throw CardGameError("newRow must be Boolean") }
            let newRow = rowFlag.kind == .boolean && rowFlag.asBoolean
            var position = try defaultTag(value, "position", owner == nil ? "center" : playerPosition)
            if owner == nil {
                switch position {
                case "left": position = "w"
                case "right": position = "e"
                case "top": position = "n"
                case "bottom": position = "s"
                default: break
                }
            }
            let group = owner == nil ? position : "player"
            let previous = currentRows[group]
            let row = (previous ?? 0) + (newRow && previous != nil ? 1 : 0)
            guard row < 16 else { throw CardGameError("Maximum 16 rows per player or table position") }
            if owner != nil {
                guard ["left", "center", "right"].contains(position) else { throw CardGameError("Invalid player position") }
                guard previous == nil || newRow || position == playerPosition else { throw CardGameError("Change player position only when starting a new row") }
                playerPosition = position
            }
            if previous == nil || newRow {
                rows.append(.init(owner: owner, index: row, position: position))
            }
            currentRows[group] = row
            let id = try tag(field(value, "id"))
            let layout = try defaultTag(value, "layout", "pile")
            let visibility = try defaultTag(value, "visibility", owner == nil ? "public" : "owner")
            try addZone(id, owner: owner, position: position, layout: layout, visibility: visibility, label: text(field(value, "label")), row: row)
            let cards = field(value, "cards")
            if cards.kind != .nothing {
                for card in try list(cards) { try addCard(zone: id, properties: card) }
            }
        }
    }
}

private func fields(_ value: GesValue, allowed: Set<String>) throws {
    guard let entries = value.mapEntries, entries.allSatisfy({ allowed.contains($0.key) }) else { throw CardGameError("Setup contains invalid or unknown fields") }
}

private func list(_ value: GesValue) throws -> [GesValue] {
    guard let values = value.listValue else { throw CardGameError("Expected a setup list") }
    return values
}

private func defaultTag(_ value: GesValue, _ key: String, _ fallback: String) throws -> String {
    let value = field(value, key)
    return value.kind == .nothing ? fallback : try tag(value)
}
