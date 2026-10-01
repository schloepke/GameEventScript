// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

extension CardGame {
    /// Serializes a player-filtered snapshot. Hidden card identities and opponent hands are omitted.
    /// A nil viewer is a spectator and receives no owner-only cards or action offers.
    /// This is presentation filtering for a local prototype, not a multiplayer security boundary.
    public func viewJSON(for viewer: Int?) -> String {
        let zoneJSON = zones.map { zone -> String in
            let visible: [Int]
            switch zone.visibility {
            case "public": visible = zone.cards
            case "owner": visible = zone.owner == viewer && viewer != nil ? zone.cards : []
            case "top": visible = Array(zone.cards.suffix(1))
            default: visible = []
            }
            let shown = visible.map { id in
                let card = cards[id - 1]
                let properties = card.properties.mapEntries ?? []
                return "{\"id\":\(id),\"properties\":{\(properties.map { jsonString($0.key) + ":" + jsonString($0.value.asText) }.joined(separator: ","))}}"
            }.joined(separator: ",")
            return "{\"id\":\(jsonString(zone.id)),\"owner\":\(zone.owner.map(String.init) ?? "null"),\"count\":\(zone.cards.count),\"cards\":[\(shown)]}"
        }.joined(separator: ",")
        let offers = viewer == currentPlayer && viewer != nil ? actions : []
        let actionJSON = offers.map { "{\"kind\":\(jsonString($0.kind)),\"card\":\($0.card.map(String.init) ?? "null")}" }.joined(separator: ",")
        return
            "{\"players\":[\(players.map(jsonString).joined(separator: ","))],\"currentPlayer\":\(currentPlayer.map(String.init) ?? "null"),\"winner\":\(winner.map(String.init) ?? "null"),\"finished\":\(finished),\"failed\":\(failed),\"revision\":\(revision),\"notice\":\(jsonString(notice)),\"zones\":[\(zoneJSON)],\"actions\":[\(actionJSON)]}"
    }

}

/// Escapes arbitrary text as a JSON string without Foundation or browser dependencies.
public func jsonString(_ value: String) -> String {
    var result = "\""
    for scalar in value.unicodeScalars {
        switch scalar.value {
        case 34: result += "\\\""
        case 92: result += "\\\\"
        case 0..<32: result += "\\u" + String(repeating: "0", count: 4 - String(scalar.value, radix: 16).count) + String(scalar.value, radix: 16)
        default: result.unicodeScalars.append(scalar)
        }
    }
    return result + "\""
}
