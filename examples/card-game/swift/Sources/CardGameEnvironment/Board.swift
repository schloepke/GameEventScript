// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// A classified prototype failure. A failed rule session must be replaced before further actions.
public struct CardGameError: Error, CustomStringConvertible {
    /// Human-readable explanation for the embedding.
    public let description: String

    /// Creates an environment error without changing game state.
    public init(_ description: String) { self.description = description }
}

/// One concrete card; its properties are defined by GES, not by Swift game rules.
public struct Card {
    /// Stable, session-local positive identifier.
    public let id: Int
    /// Immutable properties such as suit and rank.
    public let properties: GesValue

    var value: GesValue { .map([.init(key: "id", value: .integer(Int64(id))), .init(key: "properties", value: properties)]) }
}

/// An ordered container. The final card is the top of the pile.
public struct Zone {
    /// Stable tag name chosen by GES; native storage omits the # prefix.
    public let id: String
    /// Display label supplied by GES, independent of the stable zone identifier.
    public let label: String
    /// Optional owning player index.
    public let owner: Int?
    /// Zero-based row within the player area or table position.
    public let row: Int
    /// Compass position for table zones, or row alignment for player zones.
    public let position: String
    /// Presentation: one pile, or a spread of cards in a table or player area.
    public let layout: String
    /// Display policy: hidden, owner, top, or public.
    public let visibility: String
    /// Ordered card identifiers; only the environment can mutate this copy.
    public internal(set) var cards: [Int] = []
}

struct Board {
    var zones: [Zone] = []
    var cards: [Card] = []
    var rows: [ZoneRow] = []

    func zoneIndex(_ id: String) throws -> Int {
        guard let index = zones.firstIndex(where: { $0.id == id }) else { throw CardGameError("Unknown zone: \(id)") }
        return index
    }

    func zone(_ id: String) throws -> Zone { zones[try zoneIndex(id)] }

    func card(_ id: Int) throws -> Card {
        guard id > 0 && id <= cards.count else { throw CardGameError("Unknown card: \(id)") }
        return cards[id - 1]
    }

    mutating func addZone(_ id: String, owner: Int?, position: String, layout: String, visibility: String, label: String, row: Int) throws {
        guard !id.isEmpty, id.count <= 80, zones.count < 64, !zones.contains(where: { $0.id == id }), ["hidden", "owner", "top", "public"].contains(visibility) else { throw CardGameError("Invalid or duplicate zone") }
        guard owner != nil || ["nw", "n", "ne", "w", "center", "e", "sw", "s", "se"].contains(position), ["pile", "spread"].contains(layout) else {
            throw CardGameError("Invalid zone position or layout")
        }
        zones.append(Zone(id: id, label: label, owner: owner, row: row, position: position, layout: layout, visibility: visibility))
    }

    mutating func addCard(zone: String, properties: GesValue) throws {
        let index = try zoneIndex(zone)
        guard properties.kind == .map, cards.count < 512 else { throw CardGameError("Card properties must be a map; maximum 512 cards") }
        let card = Card(id: cards.count + 1, properties: properties)
        cards.append(card)
        zones[index].cards.append(card.id)
    }

    mutating func move(_ id: Int, from source: String, to destination: String) throws {
        let from = try zoneIndex(source)
        let to = try zoneIndex(destination)
        guard from != to, let index = zones[from].cards.firstIndex(of: id) else { throw CardGameError("Card is not in the source zone, or zones are identical") }
        zones[from].cards.remove(at: index)
        zones[to].cards.append(id)
    }

    mutating func take(from source: String, to destination: String, count: Int) throws {
        let pile = try zone(source)
        _ = try zoneIndex(destination)
        guard source != destination, count >= 0, count <= pile.cards.count else { throw CardGameError("Cannot take that many cards") }
        for id in pile.cards.suffix(count).reversed() { try move(id, from: source, to: destination) }
    }

    mutating func setCardOrder(_ ids: [Int], zone id: String) throws {
        let index = try zoneIndex(id)
        guard ids.count == zones[index].cards.count, Set(ids).count == ids.count, Set(ids) == Set(zones[index].cards) else {
            throw CardGameError("Card order must contain every zone card exactly once")
        }
        zones[index].cards = ids
    }

    mutating func moveCards(_ ids: [Int], from source: String, to destination: String) throws {
        let pile = try zone(source)
        _ = try zoneIndex(destination)
        guard source != destination, Set(ids).count == ids.count, ids.allSatisfy({ pile.cards.contains($0) }) else {
            throw CardGameError("Cards must be distinct members of the source zone")
        }
        for id in ids { try move(id, from: source, to: destination) }
    }

}

func integer(_ value: GesValue) throws -> Int {
    guard value.kind == .integer, value.unit == .none, let result = Int(exactly: value.asInteger) else { throw CardGameError("Expected a platform-representable unitless integer") }
    return result
}

func text(_ value: GesValue) throws -> String {
    guard value.kind == .text else { throw CardGameError("Expected text") }
    return value.asText
}

func tag(_ value: GesValue) throws -> String {
    guard value.kind == .tag else { throw CardGameError("Expected tag") }
    return value.asText
}

func field(_ value: GesValue, _ name: String) -> GesValue { value.asMap?.get(name) ?? .nothing }
