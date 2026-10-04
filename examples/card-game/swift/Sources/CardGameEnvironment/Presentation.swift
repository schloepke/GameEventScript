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
            return
                "{\"id\":\(jsonString(zone.id)),\"label\":\(jsonString(zone.label)),\"owner\":\(zone.owner.map(String.init) ?? "null"),\"position\":\(jsonString(zone.position)),\"row\":\(zone.row),\"layout\":\(jsonString(zone.layout)),\"count\":\(zone.cards.count),\"cards\":[\(shown)]}"
        }.joined(separator: ",")
        let rowJSON = bindings.board.rows.map { "{\"owner\":\($0.owner.map(String.init) ?? "null"),\"index\":\($0.index),\"position\":\(jsonString($0.position))}" }.joined(separator: ",")
        let offers = bindings.offeredActivations.filter { activation in
            guard let viewer, players.indices.contains(viewer) else { return false }
            return viewer == currentPlayer || activation.isGlobal
        }
        let actionJSON = offers.flatMap { activation in
            activation.offers.map { offer -> String in
                let area = activation.area.map { $0.kind == .tag ? jsonString($0.asText) : $0.asText } ?? "null"
                return
                    "{\"id\":\(activation.id),\"kind\":\(jsonString(offer.kind)),\"label\":\(jsonString(activation.label)),\"zone\":\(activation.zone.map(jsonString) ?? "null"),\"area\":\(area),\"global\":\(activation.isGlobal),\"group\":\(activation.group.flatMap { id in bindings.groups.first { $0.id == id }.map { jsonString($0.kind) } } ?? "null"),\"priority\":\(activation.priority),\"optional\":\(activation.optional),\"remaining\":\(activation.remaining.map(String.init) ?? "null"),\"card\":\(offer.card.map(String.init) ?? "null")}"
            }
        }.joined(separator: ",")
        return
            "{\"players\":[\(players.map(jsonString).joined(separator: ","))],\"currentPlayer\":\(currentPlayer.map(String.init) ?? "null"),\"winners\":[\(winners.map(String.init).joined(separator: ","))],\"finished\":\(finished),\"failed\":\(failed),\"revision\":\(revision),\"turn\":\(turn),\"round\":\(round),\"waitingForRound\":\(bindings.waitingForRound),\"direction\":\(bindings.direction),\"notice\":\(jsonString(notice)),\"notices\":[\(bindings.notices.map(jsonString).joined(separator: ","))],\"tableNotice\":\(jsonString(bindings.tableNotice)),\"zones\":[\(zoneJSON)],\"rows\":[\(rowJSON)],\"actions\":[\(actionJSON)]}"
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
