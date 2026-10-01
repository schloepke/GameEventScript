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
    /// Stable name chosen by the GES setup.
    public let id: String
    /// Optional owning player index.
    public let owner: Int?
    /// Display policy: hidden, owner, top, or public.
    public let visibility: String
    /// Ordered card identifiers; only the environment can mutate this copy.
    public internal(set) var cards: [Int] = []
}

struct Board {
    var zones: [Zone] = []
    var cards: [Card] = []

    func zoneIndex(_ id: String) throws -> Int {
        guard let index = zones.firstIndex(where: { $0.id == id }) else { throw CardGameError("Unknown zone: \(id)") }
        return index
    }

    func zone(_ id: String) throws -> Zone { zones[try zoneIndex(id)] }

    func card(_ id: Int) throws -> Card {
        guard id > 0 && id <= cards.count else { throw CardGameError("Unknown card: \(id)") }
        return cards[id - 1]
    }

    mutating func addZone(_ id: String, owner: Int?, visibility: String) throws {
        guard !id.isEmpty, id.count <= 80, zones.count < 64, !zones.contains(where: { $0.id == id }), ["hidden", "owner", "top", "public"].contains(visibility) else { throw CardGameError("Invalid or duplicate zone") }
        zones.append(Zone(id: id, owner: owner, visibility: visibility))
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

    mutating func shuffle(_ id: String, random: GameEventScriptRandomGenerator) throws {
        let index = try zoneIndex(id)
        guard zones[index].cards.count > 1 else { return }
        for end in stride(from: zones[index].cards.count - 1, through: 1, by: -1) {
            zones[index].cards.swapAt(end, Int(random.nextInclusiveInteger(0, Int64(end))))
        }
    }

    mutating func draw(from source: String, to destination: String, count: Int, recycling recycleSource: String, keeping: Int, random: GameEventScriptRandomGenerator) throws {
        let available = try zone(source).cards.count
        let discarded = try zone(recycleSource).cards.count
        _ = try zoneIndex(destination)
        guard source != destination, recycleSource != source, recycleSource != destination, keeping >= 0, keeping <= discarded, count >= 0, count <= available + discarded - keeping else {
            throw CardGameError("Invalid draw operation or insufficient cards")
        }
        for _ in 0..<count {
            if try zone(source).cards.isEmpty { try recycle(from: recycleSource, to: source, keeping: keeping, random: random) }
            try take(from: source, to: destination, count: 1)
        }
    }

    mutating func recycle(from source: String, to destination: String, keeping: Int, random: GameEventScriptRandomGenerator) throws {
        let pile = try zone(source)
        guard source != destination, try zone(destination).cards.isEmpty, keeping >= 0, keeping <= pile.cards.count else { throw CardGameError("Invalid recycle operation") }
        for id in pile.cards.dropLast(keeping) { try move(id, from: source, to: destination) }
        try shuffle(destination, random: random)
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

func field(_ value: GesValue, _ name: String) -> GesValue { value.asMap?.get(name) ?? .nothing }
