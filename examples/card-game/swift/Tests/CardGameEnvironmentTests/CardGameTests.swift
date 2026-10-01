// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import CardGameEnvironment
import Foundation
import XCTest

final class CardGameTests: XCTestCase {
    private var rules: String {
        get throws {
            let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            return try String(contentsOf: root.appendingPathComponent("games/mau-mau/rules.ges"), encoding: .utf8)
        }
    }

    private func validate(_ game: CardGame, file: StaticString = #filePath, line: UInt = #line) {
        let locations = game.zones.flatMap(\.cards)
        XCTAssertEqual(locations.count, 32, file: file, line: line)
        XCTAssertEqual(Set(locations), Set(1...32), file: file, line: line)
    }

    func testSetupAndDeterminism() throws {
        let first = try CardGame(rules: rules, seed: 42)
        let second = try CardGame(rules: rules, seed: 42)
        XCTAssertEqual(first.viewJSON(for: 0), second.viewJSON(for: 0))
        XCTAssertEqual(first.zones.map { $0.cards.count }, [21, 1, 5, 5])
        XCTAssertEqual(first.currentPlayer, 0)
        XCTAssertFalse(first.actions.isEmpty)
        validate(first)
    }

    func testRejectedWrongPlayerIllegalCardAndStaleRevisionDoNotMutate() throws {
        let game = try CardGame(rules: rules)
        let before = game.viewJSON(for: 0)
        let wrong = try game.submit(player: 1, action: .init(kind: "draw"), revision: 0)
        XCTAssertFalse(wrong.accepted)
        let foreign = try XCTUnwrap(game.zones.first(where: { $0.id == "hand1" })?.cards.first)
        XCTAssertFalse(try game.submit(player: 0, action: .init(kind: "play", card: foreign), revision: 0).accepted)
        XCTAssertFalse(try game.submit(player: 0, action: .init(kind: "draw"), revision: 99).accepted)
        XCTAssertFalse(try game.submit(player: 0, action: .init(kind: "play", card: Int.max), revision: 0).accepted)
        let hand = try XCTUnwrap(game.zones.first(where: { $0.id == "hand0" }))
        let illegal = try XCTUnwrap(hand.cards.first(where: { id in !game.actions.contains(.init(kind: "play", card: id)) }))
        XCTAssertFalse(try game.submit(player: 0, action: .init(kind: "play", card: illegal), revision: 0).accepted)
        XCTAssertEqual(game.viewJSON(for: 0), before)
        validate(game)
    }

    func testDrawEndsTurnAndAcceptedActionInvalidatesOldOffer() throws {
        let game = try CardGame(rules: rules)
        XCTAssertTrue(try game.submit(player: 0, action: .init(kind: "draw"), revision: 0).accepted)
        XCTAssertEqual(game.currentPlayer, 1)
        XCTAssertEqual(game.revision, 1)
        XCTAssertEqual(game.zones.first(where: { $0.id == "hand0" })?.cards.count, 6)
        XCTAssertFalse(try game.submit(player: 1, action: .init(kind: "draw"), revision: 0).accepted)
        validate(game)
    }

    func testPlayerViewsHideIdentitiesAndOffers() throws {
        let game = try CardGame(rules: rules)
        let view = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(game.viewJSON(for: 0).utf8)) as? [String: Any])
        let zones = try XCTUnwrap(view["zones"] as? [[String: Any]])
        for name in ["draw", "hand1"] {
            let zone = try XCTUnwrap(zones.first(where: { $0["id"] as? String == name }))
            XCTAssertEqual((zone["cards"] as? [Any])?.count, 0)
        }
        let spectator = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(game.viewJSON(for: nil).utf8)) as? [String: Any])
        XCTAssertEqual((spectator["actions"] as? [Any])?.count, 0)
    }

    func testRulesReallyOwnMatchingAndSetup() throws {
        let source = try rules.replacingOccurrences(of: "card.properties.suit = top.properties.suit or card.properties.rank = top.properties.rank", with: "true")
            .replacingOccurrences(of: "count: 5", with: "count: 1")
        let game = try CardGame(rules: source)
        XCTAssertEqual(game.zones.map { $0.cards.count }, [29, 1, 1, 1])
        let action = try XCTUnwrap(game.actions.first(where: { $0.kind == "play" }))
        XCTAssertTrue(try game.submit(player: 0, action: action, revision: 0).accepted)
        XCTAssertTrue(game.finished)
        XCTAssertEqual(game.winner, 0)
        XCTAssertTrue(game.actions.isEmpty)
        XCTAssertFalse(try game.submit(player: 1, action: .init(kind: "draw"), revision: 1).accepted)
    }

    func testSeededGamesFinishAndPreserveEveryCard() throws {
        var recycled = false
        for seed in 0..<30 {
            let game = try CardGame(rules: rules, seed: Int64(seed))
            for _ in 0..<1000 {
                if game.finished { break }
                let player = try XCTUnwrap(game.currentPlayer)
                let action = try XCTUnwrap(game.actions.first)
                let drawBefore = game.zones.first(where: { $0.id == "draw" })!.cards.count
                let topBefore = game.zones.first(where: { $0.id == "discard" })!.cards.last
                XCTAssertTrue(try game.submit(player: player, action: action, revision: game.revision).accepted)
                if action.kind == "draw" && drawBefore == 0 {
                    recycled = true
                    XCTAssertEqual(game.zones.first(where: { $0.id == "discard" })!.cards, topBefore.map { [$0] })
                }
                validate(game)
            }
            XCTAssertTrue(game.finished, "Seed \(seed) did not finish")
            if let winner = game.winner { XCTAssertTrue(game.zones.first(where: { $0.id == "hand\(winner)" })!.cards.isEmpty) }
        }
        XCTAssertTrue(recycled, "The seed set must exercise recycling")
    }

    func testFourPlayers() throws {
        let game = try CardGame(rules: rules, players: ["A", "B", "C", "D"])
        XCTAssertEqual(game.zones.map { $0.cards.count }, [11, 1, 5, 5, 5, 5])
        for _ in 0..<1000 {
            if game.finished { break }
            XCTAssertTrue(try game.submit(player: XCTUnwrap(game.currentPlayer), action: XCTUnwrap(game.actions.first), revision: game.revision).accepted)
            validate(game)
        }
        XCTAssertTrue(game.finished)
    }

    func testMissingRuleResponseFailsClosed() throws {
        let source = try rules.replacingOccurrences(of: "on ActionRequested(request, player, action, card)", with: "on Ignored(request, player, action, card)")
        let game = try CardGame(rules: source)
        XCTAssertThrowsError(try game.submit(player: 0, action: .init(kind: "draw"), revision: 0))
        XCTAssertTrue(game.failed)
        XCTAssertTrue(game.actions.isEmpty)
        XCTAssertThrowsError(try game.submit(player: 0, action: .init(kind: "draw"), revision: 0))
    }

    func testBlockedGameEndsInDrawFromGES() throws {
        let source = try rules.replacingOccurrences(of: "card.properties.suit = top.properties.suit or card.properties.rank = top.properties.rank", with: "false")
        let game = try CardGame(rules: source)
        for _ in 0..<21 {
            XCTAssertTrue(try game.submit(player: XCTUnwrap(game.currentPlayer), action: .init(kind: "draw"), revision: game.revision).accepted)
        }
        XCTAssertTrue(game.finished)
        XCTAssertNil(game.winner)
        XCTAssertTrue(game.actions.isEmpty)
        validate(game)
    }

    func testInvalidMechanicalCommandDoesNotPartiallyMoveCards() throws {
        let source = try rules.replacingOccurrences(of: "DrawCard(request: request, source: 'draw', destination: hand(player: player)", with: "DrawCard(request: request, source: 'draw', destination: 'draw'")
        let game = try CardGame(rules: source)
        let before = game.zones.map(\.cards)
        XCTAssertThrowsError(try game.submit(player: 0, action: .init(kind: "draw"), revision: 0))
        XCTAssertEqual(game.zones.map(\.cards), before)
        XCTAssertTrue(game.failed)
        XCTAssertEqual(game.revision, 0)
        validate(game)
    }

    func testMalformedRulesAndMessageLoopAreBounded() throws {
        XCTAssertThrowsError(try CardGame(rules: "This is not GES"))
        XCTAssertThrowsError(try CardGame(rules: "on Setup(players) { emit Again() }\non Again() { emit Again() }"))
    }
}
