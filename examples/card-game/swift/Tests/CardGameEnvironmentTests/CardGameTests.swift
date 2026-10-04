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

    func testSynchronousMutationsReturnSnapshotsAndRequireExplicitCompletion() throws {
        let source = """
            on PrepareGame(players) {
                :board.create(setup: [table: [
                    [id: #draw, label: 'Draw', cards: [[rank: 1], [rank: 2], [rank: 3]]], [id: #discard, label: 'Discard']
                ], players: players[:select player => [id: player, zones: []]]])
                let originalCards be :board.cards(zone: #draw)
                let drawnCard be :board.draw(source: #draw, destination: #discard)
                let drawnCards be :board.take(source: #draw, destination: #discard, count: 2)
                let emptyDraw be :board.draw(source: #draw, destination: #discard)
                :board.setorder(zone: #discard, cards: [1, 2, 3])
                let movedCard be :board.move(card: 2, source: #discard, destination: #draw)
                let movedCards be :board.movecards(source: #discard, destination: #draw, cards: [3, 1])
                :board.setstate(key: #snapshot, value: originalCards)
                :board.setstate(player: 0, key: #seen, value: drawnCard.id)
                let direction be :board.reverse()
                if (originalCards[:count] = 3 and drawnCard.id = 3 and drawnCards[:select item => item.id] = [2, 1] and emptyDraw is nothing and movedCard.id = 2 and movedCards[:select item => item.id] = [3, 1] and :board.top(zone: #draw).id = 1 and :board.cards(zone: #discard)[:count] = 0 and :board.state(key: #snapshot) = originalCards and :board.state(player: 0, key: #seen) = 3 and direction = -1 and :board.direction() = -1) {
                    emit NoticeTable(text: 'Snapshots verified')
                }
            }
            on BeginTurn(player) {
                emit Action(spec: [action: #draw, label: 'Draw', consumable: #never, zone: #draw, handler: Draw(action, player)])
            }
            on Draw(action, player) {
                let card be :board.draw(source: #draw, destination: #discard)
                if card.id = :board.top(zone: #discard).id { emit Complete(action: action) }
            }
            """
        let game = try CardGame(rules: source)
        XCTAssertTrue(game.viewJSON(for: 0).contains("Snapshots verified"))
        XCTAssertTrue(try game.submit(player: 0, action: .init(kind: "draw"), revision: 0).accepted)
        let unanswered = try CardGame(rules: source.replacingOccurrences(of: "emit Complete(action: action)", with: "emit Notice(text: 'Moved')"))
        XCTAssertThrowsError(try unanswered.submit(player: 0, action: .init(kind: "draw"), revision: 0))
        XCTAssertEqual(unanswered.zones.first { $0.id == "discard" }?.cards, [1])
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

    func testDrawOnceThenPassAndInvalidateOldOffer() throws {
        let game = try CardGame(rules: rules)
        XCTAssertTrue(try game.submit(player: 0, action: .init(kind: "draw"), revision: 0).accepted)
        XCTAssertEqual(game.currentPlayer, 0)
        XCTAssertEqual(game.turn, 1)
        XCTAssertEqual(game.revision, 1)
        XCTAssertFalse(game.actions.contains(.init(kind: "draw")))
        XCTAssertTrue(game.actions.contains(.init(kind: "pass")))
        XCTAssertFalse(try game.submit(player: 0, action: .init(kind: "draw"), revision: 1).accepted)
        XCTAssertTrue(try game.submit(player: 0, action: .init(kind: "pass"), revision: 1).accepted)
        XCTAssertEqual(game.currentPlayer, 1)
        XCTAssertEqual(game.turn, 2)
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
        let source = try rules.replacingOccurrences(
            of:
                "function fits(card, top) be top.properties.rank <> 'J' when card.properties.rank = 'J' otherwise matches(card: card, top: top, wish: :board.state(key: #wish))",
            with: "function fits(card, top) be true"
        )
        .replacingOccurrences(of: "[1, 1, 1, 1, 1]", with: "[1]")
        let game = try CardGame(rules: source)
        XCTAssertEqual(game.zones.map { $0.cards.count }, [29, 1, 1, 1])
        let action = try XCTUnwrap(game.actions.first(where: { $0.kind == "play" }))
        XCTAssertTrue(try game.submit(player: 0, action: action, revision: 0).accepted)
        XCTAssertTrue(game.finished)
        XCTAssertEqual(game.winners, [0])
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
            for winner in game.winners { XCTAssertTrue(game.zones.first(where: { $0.id == "hand\(winner)" })!.cards.isEmpty) }
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
        let source = try rules.replacingOccurrences(of: "on Draw(action, player)", with: "on Ignored(action, player)")
        let game = try CardGame(rules: source)
        XCTAssertThrowsError(try game.submit(player: 0, action: .init(kind: "draw"), revision: 0))
        XCTAssertTrue(game.failed)
        XCTAssertTrue(game.actions.isEmpty)
        XCTAssertThrowsError(try game.submit(player: 0, action: .init(kind: "draw"), revision: 0))
    }

    func testBlockedGameEndsInDrawFromGES() throws {
        let source = try rules.replacingOccurrences(
            of:
                "function fits(card, top) be top.properties.rank <> 'J' when card.properties.rank = 'J' otherwise matches(card: card, top: top, wish: :board.state(key: #wish))",
            with: "function fits(card, top) be false"
        )
        let game = try CardGame(rules: source)
        for _ in 0..<21 {
            XCTAssertTrue(try game.submit(player: XCTUnwrap(game.currentPlayer), action: .init(kind: "draw"), revision: game.revision).accepted)
            XCTAssertTrue(try game.submit(player: XCTUnwrap(game.currentPlayer), action: .init(kind: "pass"), revision: game.revision).accepted)
        }
        XCTAssertTrue(game.finished)
        XCTAssertTrue(game.winners.isEmpty)
        XCTAssertTrue(game.actions.isEmpty)
        validate(game)
    }

    func testInvalidMechanicalCommandDoesNotPartiallyMoveCards() throws {
        let source = try rules.replacingOccurrences(of: ":board.draw(source: #draw, destination: hand(player: player)", with: ":board.draw(source: #draw, destination: #draw")
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
        XCTAssertThrowsError(try CardGame(rules: "on PrepareGame(players) { emit Again() }\non Again() { emit Again() }"))
    }
    func testBulkRummyDecksAndOrderedPreparation() throws {
        let source = """
            function deck() be ['clubs', 'spades', 'hearts', 'diamonds'][:fold cards be [], suit =>
                cards | ['2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K', 'A'][:select rank => [suit: suit, rank: rank, score: 10]]]
            on PrepareGame(players) {
                :board.create(setup: [
                    table: [
                        [id: #draw, label: 'Draw pile', visibility: #hidden, cards: deck() | deck() | [[suit: 'joker', rank: 'Joker'], [suit: 'joker', rank: 'Joker'], [suit: 'joker', rank: 'Joker'], [suit: 'joker', rank: 'Joker']]],
                        [id: #second, label: 'Second pile', visibility: #top]
                    ],
                    players: players[:select player => [id: player, zones: [
                        [id: (('hand' + (player as :Text)) as :Tag), label: 'Hand', layout: #spread]
                    ]]]
                ])
                emit DealCards(players: players)
            }
            on DealCards(players) {
                for player in players :board.take(source: #draw, destination: (('hand' + (player as :Text)) as :Tag), count: 13)
                emit BeginGame(players: players)
            }
            on BeginGame(players) { emit NoticeTable(text: 'Ready') }
            on BeginTurn(player) { emit Action(action: #inspect, label: 'Inspect', optional: true, finishTurn: true, consumable: #auto, handler: Inspect(action, player), area: player) }
            on Inspect(action, player) {
                emit Complete(action: action)
                emit NextPlayersTurn()
            }
            on EndTurn(player) { emit NextPlayersTurn(repeatTurnForPlayer: true) }
            """
        let game = try CardGame(rules: source, players: ["A", "B", "C", "D"])
        XCTAssertEqual(game.cards.count, 108)
        XCTAssertEqual(game.zones.map { $0.cards.count }, [56, 0, 13, 13, 13, 13])
        XCTAssertEqual(game.zones.prefix(2).map(\.position), ["center", "center"])
        XCTAssertEqual(game.zones.dropFirst(2).compactMap(\.owner), [0, 1, 2, 3])
        XCTAssertTrue(try game.submit(player: 0, action: .init(kind: "inspect"), revision: 0).accepted)
        XCTAssertEqual(game.currentPlayer, 0)
        let skipped = try CardGame(
            rules: source.replacingOccurrences(of: "emit NextPlayersTurn(repeatTurnForPlayer: true)", with: "emit NextPlayersTurn(nextPlayer: (:board.next(player: player) + 1) mod :board.playercount())"),
            players: ["A", "B", "C", "D"]
        )
        _ = try skipped.submit(player: 0, action: .init(kind: "inspect"), revision: 0)
        XCTAssertEqual(skipped.currentPlayer, 2)
    }

    func testPrepareGameRejectsInvalidBulkCardsAndLayout() throws {
        let source = try rules
        XCTAssertThrowsError(try CardGame(rules: source.replacingOccurrences(of: "cards: deck()[:shuffle]", with: "cards: [123]")))
        XCTAssertNoThrow(try CardGame(rules: source.replacingOccurrences(of: "position: #center, layout: #pile", with: "position: #center, layout: #spread")))
    }

    func testSetupRowsAndCompassPositions() throws {
        let source = try rules.replacingOccurrences(of: "position: #center", with: "position: #nw")
            .replacingOccurrences(of: "position: #left", with: "position: #right")
        let game = try CardGame(rules: source)
        XCTAssertEqual(game.zones.prefix(2).map(\.position), ["nw", "nw"])
        XCTAssertEqual(game.zones.dropFirst(2).map(\.row), [0, 0])
        for replacement in ["#bottomLeft", "'nw'"] {
            XCTAssertThrowsError(try CardGame(rules: source.replacingOccurrences(of: "#nw", with: replacement)))
        }
        XCTAssertThrowsError(try CardGame(rules: source.replacingOccurrences(of: "position: #right", with: "position: #nw")))
        XCTAssertThrowsError(try CardGame(rules: source.replacingOccurrences(of: "id: player,", with: "id: 0,")))
        XCTAssertThrowsError(try CardGame(rules: source.replacingOccurrences(of: "emit NoticeTable(text: 'Match suit or rank. Draw once, then play or pass.')", with: ":board.create(setup: board(players: players))")))
    }

    func testFlatZoneListsAndTagReferences() throws {
        let source = """
            on PrepareGame(players) {
                :board.create(setup: [table: [
                    [id: #a, label: 'A', position: #nw, newRow: true],
                    [id: #b, label: 'B', position: #center],
                    [id: #c, label: 'C', position: #nw, newRow: true],
                    [id: #d, label: 'D', position: #nw],
                    [id: #e, label: 'E', position: #center, newRow: true]
                ], players: players[:select player => [id: player, zones: [
                    [id: ('hand' + (player as :Text)) as :Tag, label: 'Hand', position: #right],
                    [id: ('pile' + (player as :Text)) as :Tag, label: 'Pile', newRow: true, position: #center],
                    [id: ('extra' + (player as :Text)) as :Tag, label: 'Extra']
                ]]]])
            }
            on BeginTurn(player) {
                emit Notice(text: (:board.table()[0].id as :Text))
                emit Action(action: #inspect, label: 'Inspect', optional: true, finishTurn: false, consumable: #never, handler: Inspect(action, player), zone: #a)
            }
            on Inspect(action, player) { emit Complete(action: action) }
            """
        let game = try CardGame(rules: source)
        XCTAssertEqual(game.zones.map(\.row), [0, 0, 1, 1, 1, 0, 1, 1, 0, 1, 1])
        XCTAssertEqual(game.zones.suffix(3).map(\.position), ["right", "center", "center"])
        for (from, to) in [("id: #a", "id: 'a'"), ("zone: #a", "zone: 'a'"), ("newRow: true", "newRow: #true"), ("newRow: true, position: #center", "position: #center")] {
            XCTAssertThrowsError(try CardGame(rules: source.replacingOccurrences(of: from, with: to)))
        }
    }

    func testRoundHooksIncludeSkippedSeatsAndPartialFinalRound() throws {
        let source = """
            on PrepareGame(players) {
                :board.create(setup: [table: [
                    [id: #bank, label: 'Bank', cards: (from 1 to 30)[:select number => [rank: number]]],
                    [id: #begins, label: 'Begins'], [id: #ends, label: 'Ends']
                ], players: players[:select player => [id: player, zones: []]]])
            }
            on BeginRound(number, players) {
                :board.take(source: #bank, destination: #begins, count: 1)
                emit Notice(text: (:board.roundnumber() as :Text))
            }
            on EndRound(number, players) { :board.take(source: #bank, destination: #ends, count: 1)\nemit NextRound() }
            on BeginTurn(player) {
                emit Action(action: #step, label: 'Step', optional: true, finishTurn: true, consumable: #auto, handler: Step(action, player), area: player)
            }
            on Step(action, player) { emit Complete(action: action) }
            on EndTurn(player) { emit Notice(text: 'Turn ended') }
            on EndGame(players) { emit Finish(winners: []) }
            """
        func create(_ rules: String) throws -> CardGame { try CardGame(rules: rules, players: ["A", "B", "C", "D"]) }
        func step(_ game: CardGame) throws { _ = try game.submit(player: game.currentPlayer!, action: .init(kind: "step"), revision: game.revision) }
        func count(_ game: CardGame, _ zone: String) -> Int { game.zones.first { $0.id == zone }!.cards.count }
        let game = try create(source)
        XCTAssertEqual(game.round, 1)
        XCTAssertEqual(game.notice, "1")
        for _ in 0..<3 {
            try step(game)
            XCTAssertEqual(game.round, 1)
        }
        try step(game)
        XCTAssertEqual(game.round, 2)
        XCTAssertEqual(game.currentPlayer, 0)
        XCTAssertEqual(count(game, "ends"), 1)
        XCTAssertEqual(count(game, "begins"), 2)
        let skipped = try create(source.replacingOccurrences(of: "emit Notice(text: 'Turn ended')", with: "emit NextPlayersTurn(nextPlayer: (:board.next(player: player) + 1) mod :board.playercount())"))
        try step(skipped)
        XCTAssertEqual(skipped.round, 1)
        XCTAssertEqual(skipped.currentPlayer, 2)
        try step(skipped)
        XCTAssertEqual(skipped.round, 2)
        let repeatPlayer = try create(source.replacingOccurrences(of: "emit Notice(text: 'Turn ended')", with: "if player = 1 emit NextPlayersTurn(repeatTurnForPlayer: true)"))
        try step(repeatPlayer)
        try step(repeatPlayer)
        XCTAssertEqual(repeatPlayer.round, 2)
        XCTAssertEqual(repeatPlayer.currentPlayer, 1)
        XCTAssertEqual(count(repeatPlayer, "ends"), 1)
        for ending in [
            source.replacingOccurrences(of: "emit Notice(text: 'Turn ended')", with: "emit EndGame()"),
            source.replacingOccurrences(of: "on Step(action, player) { emit Complete(action: action) }", with: "on Step(action, player) { emit Complete(action: action)\nemit EndGame() }"),
        ] {
            let final = try create(ending)
            try step(final)
            XCTAssertTrue(final.finished)
            XCTAssertEqual(count(final, "ends"), 1)
            XCTAssertEqual(count(final, "begins"), 1)
        }
        let stopAtBoundary = try create(
            source.replacingOccurrences(
                of: "on EndRound(number, players) { :board.take(source: #bank, destination: #ends, count: 1)\nemit NextRound() }",
                with: "on EndRound(number, players) { :board.take(source: #bank, destination: #ends, count: 1)\nemit EndGame() }"
            )
        )
        for _ in 0..<4 { try step(stopAtBoundary) }
        XCTAssertTrue(stopAtBoundary.finished)
        XCTAssertEqual(count(stopAtBoundary, "ends"), 1)
        XCTAssertEqual(count(stopAtBoundary, "begins"), 1)
    }

    func testGlobalActionsPersistAndNoticesRemainSeparate() throws {
        let source = """
            on PrepareGame(players) {
                :board.create(setup: [table: [], players: players[:select player => [id: player, zones: []]], actions: [
                    [action: #info, label: 'Info', optional: true, finishTurn: false, consumable: 2, handler: Info(action), area: #table]
                ]])
                emit NoticeTable(text: 'Persistent table text')
            }
            on BeginTurn(player) { emit Action(action: #step, label: 'Step', optional: true, finishTurn: true, consumable: #auto, handler: Step(action, player), area: player) }
            on Step(action, player) { emit Complete(action: action) }
            on Info(action) {
                emit Notice(text: 'One')
                emit Complete(action: action)
                emit Notice(text: 'Two')
            }
            on EndTurn(player) { emit NextPlayersTurn() }
            """
        let game = try CardGame(rules: source)
        XCTAssertTrue(game.viewJSON(for: 1).contains("\"kind\":\"info\""))
        XCTAssertFalse(game.viewJSON(for: 1).contains("\"kind\":\"step\""))
        XCTAssertTrue(try game.submit(player: 1, action: .init(kind: "info"), revision: 0).accepted)
        XCTAssertEqual(game.turn, 1)
        XCTAssertTrue(game.viewJSON(for: 0).contains("\"notices\":[\"One\",\"Two\"]"))
        XCTAssertTrue(try game.submit(player: 0, action: .init(kind: "step"), revision: 1).accepted)
        XCTAssertEqual(game.currentPlayer, 1)
        XCTAssertTrue(game.viewJSON(for: 1).contains("\"notices\":[]"))
        XCTAssertTrue(game.viewJSON(for: 1).contains("\"tableNotice\":\"Persistent table text\""))
        XCTAssertTrue(try game.submit(player: 0, action: .init(kind: "info"), revision: 2).accepted)
        XCTAssertFalse(game.actions.contains(.init(kind: "info")))
        XCTAssertEqual(game.turn, 2)
        XCTAssertThrowsError(try CardGame(rules: source.replacingOccurrences(of: "handler: Info(action)", with: "handler: Step(action, player)")))
    }

    func testExplicitEndTurnDestinationsFromAction() throws {
        let source = """
            on PrepareGame(players) {}
            on BeginTurn(player) { emit Action(action: #step, label: 'Step', optional: true, finishTurn: false, consumable: #auto, handler: Step(action, player), area: player) }
            on Step(action, player) {
                emit Complete(action: action)
                emit NextPlayersTurn(nextPlayer: 3)
            }
            on EndTurn(player) { emit NoticeTable(text: 'Turn ended') }
            """
        for (command, expectedPlayer, expectedRound) in [("NextPlayersTurn(nextPlayer: 3)", 3, 1), ("NextPlayersTurn(repeatTurnForPlayer: true)", 0, 2), ("NextPlayersTurn(repeatTurnForPlayer: false)", 1, 1), ("NextPlayersTurn()", 1, 1)] {
            let game = try CardGame(rules: source.replacingOccurrences(of: "NextPlayersTurn(nextPlayer: 3)", with: command), players: ["A", "B", "C", "D"])
            XCTAssertTrue(try game.submit(player: 0, action: .init(kind: "step"), revision: 0).accepted)
            XCTAssertEqual(game.currentPlayer, expectedPlayer)
            XCTAssertEqual(game.round, expectedRound)
        }
    }

    func testRoundWaitsForRequiredHumanActionsBeforeNextRound() throws {
        let source = """
            on PrepareGame(players) {}
            on BeginTurn(player) { emit Action(action: #step, label: 'Step', optional: true, finishTurn: true, consumable: #auto, handler: Step(action, player), area: player) }
            on Step(action, player) { emit Complete(action: action) }
            on EndTurn(player) { emit NoticeTable(text: 'Turn complete') }
            on EndRound(number, players) {
                if not :board.ending() emit Action(action: #deal, label: 'Deal round card', optional: false, finishTurn: false, consumable: 2, handler: Deal(action, player), area: #table)
            }
            on Deal(action, player) {
                emit Complete(action: action)
                emit NextRound()
            }
            """
        let game = try CardGame(rules: source)
        for player in [0, 1] { _ = try game.submit(player: player, action: .init(kind: "step"), revision: game.revision) }
        XCTAssertEqual(game.round, 1)
        XCTAssertTrue(game.viewJSON(for: 0).contains("\"waitingForRound\":true"))
        XCTAssertFalse(game.actions.contains(.init(kind: "step")))
        _ = try game.submit(player: 0, action: .init(kind: "deal"), revision: game.revision)
        XCTAssertEqual(game.round, 1)
        _ = try game.submit(player: 0, action: .init(kind: "deal"), revision: game.revision)
        XCTAssertEqual(game.round, 2)
        XCTAssertEqual(game.turn, 3)
        XCTAssertTrue(game.actions.contains(.init(kind: "step")))
    }

    func testReverseDirectionAndExplicitCardOrdering() throws {
        let source = """
            on PrepareGame(players) {
                :board.create(setup: [table: [[id: #deck, label: 'Deck', cards: [[rank: 'A'], [rank: 'B'], [rank: 'C']]]], players: players[:select player => [id: player, zones: []]]])
                :board.setorder(zone: #deck, cards: [2, 3, 1])
            }
            on BeginTurn(player) { emit Action(action: #step, label: 'Step', optional: true, finishTurn: true, consumable: #auto, handler: Step(action, player), area: player) }
            on Step(action, player) { emit Complete(action: action) }
            on EndTurn(player) {
                if player = 0 :board.reverse()
                if player = 3 emit NextPlayersTurn(nextPlayer: :board.next(player: :board.next(player: player)))
            }
            """
        let game = try CardGame(rules: source, players: ["A", "B", "C", "D"])
        XCTAssertEqual(game.zones[0].cards, [2, 3, 1])
        for (player, next) in [(0, 3), (3, 1), (1, 0)] {
            _ = try game.submit(player: player, action: .init(kind: "step"), revision: game.revision)
            XCTAssertEqual(game.currentPlayer, next)
        }
        XCTAssertEqual(game.round, 2)
        XCTAssertThrowsError(try CardGame(rules: source.replacingOccurrences(of: "cards: [2, 3, 1]", with: "cards: [1, 1, 2]")))
    }

    func testAutomaticTurnSkippingAndRoundBoundary() throws {
        let source = """
            on PrepareGame(players) { :board.setstate(key: #ends, value: 0) }
            on BeginTurn(player) {
                if :board.roundnumber() = 1 or player = 0 {
                    emit NextPlayersTurn()
                } else {
                    emit Action(action: #wait, label: 'Wait', optional: true, finishTurn: true, consumable: #auto, handler: Wait(action, player), area: player)
                }
            }
            on Wait(action, player) { emit Complete(action: action) }
            on EndTurn(player) { :board.setstate(key: #ends, value: :board.state(key: #ends) + 1) }
            on EndRound(number, players) { emit NoticeTable(text: (:board.state(key: #ends) as :Text))
                emit NextRound() }
            """
        let game = try CardGame(rules: source)
        XCTAssertEqual(game.currentPlayer, 1)
        XCTAssertEqual(game.turn, 4)
        XCTAssertEqual(game.round, 2)
        XCTAssertTrue(game.viewJSON(for: 1).contains("\"tableNotice\":\"2\""))
        let paused = try CardGame(rules: source.replacingOccurrences(of: "emit NextRound()", with: ""))
        XCTAssertEqual(paused.turn, 2)
        XCTAssertTrue(paused.viewJSON(for: 0).contains("\"waitingForRound\":true"))
        XCTAssertThrowsError(try CardGame(rules: "on BeginTurn(player) { emit NextPlayersTurn() } on EndTurn(player) {}"))
    }

    func testWinnerListsAndAutomaticSkipEndingGame() throws {
        let source = """
            on PrepareGame(players) {}
            on BeginTurn(player) { emit NextPlayersTurn() }
            on EndTurn(player) { emit EndGame() }
            on EndGame(players) { emit Finish(winners: [0, 1]) }
            """
        for winners in ["[]", "[0]", "[0, 1]"] {
            let game = try CardGame(rules: source.replacingOccurrences(of: "[0, 1]", with: winners))
            XCTAssertTrue(game.finished)
            XCTAssertEqual(game.winners.count, winners == "[]" ? 0 : winners == "[0]" ? 1 : 2)
            XCTAssertEqual(game.turn, 1)
        }
        for invalid in ["[0, 0]", "[2]", "[-1]", "nothing", "0"] {
            XCTAssertThrowsError(try CardGame(rules: source.replacingOccurrences(of: "[0, 1]", with: invalid)))
        }
    }

    func testPublicTableSpreadIncludesEveryCard() throws {
        let source = """
            on PrepareGame(players) {
                :board.create(setup: [table: [[id: #trick, label: 'Trick', layout: #spread, visibility: #public, cards: ['7', '8', '9'][:select rank => [suit: 'clubs', rank: rank]]]], players: players[:select player => [id: player, zones: []]]])
            }
            on BeginTurn(player) { emit Action(action: #wait, label: 'Wait', optional: true, finishTurn: false, consumable: #never, handler: Wait(action, player), area: player) }
            on Wait(action, player) { emit Complete(action: action) }
            """
        let game = try CardGame(rules: source)
        XCTAssertEqual(game.zones[0].layout, "spread")
        XCTAssertEqual(game.zones[0].cards.count, 3)
        let view = game.viewJSON(for: nil)
        for rank in ["7", "8", "9"] { XCTAssertTrue(view.contains("\"rank\":\"\(rank)\"")) }
    }

    func testQueuedExclusiveGroupsPrioritiesAndRejection() throws {
        let source = """
            on PrepareGame(players) {
                emit ActionGroup(spec: [group: #debt, player: 1, priority: 10, optional: false, exclusive: true, actions: [
                    [action: #draw, label: 'Draw', consumable: 2, handler: Draw(action, player), area: 1],
                    [action: #alternative, label: 'Alternative', handler: Alternative(action, player), area: 1]
                ]])
            }
            on BeginTurn(player) { emit Action(spec: [action: #normal, label: 'Normal', priority: 0, finishTurn: true, handler: Normal(action, player), area: player]) }
            on Normal(action, player) { emit Complete(action: action) }
            on Draw(action, player) { emit Complete(action: action) }
            on Alternative(action, player) { emit Reject(action: action, reason: 'No') }
            on EndTurn(player) {}
            """
        let game = try CardGame(rules: source)
        XCTAssertEqual(game.actions.map(\.kind), ["normal"])
        _ = try game.submit(player: 0, action: .init(kind: "normal"), revision: 0)
        XCTAssertEqual(game.actions.map(\.kind), ["draw", "alternative"])
        XCTAssertFalse(try game.submit(player: 1, action: .init(kind: "normal"), revision: 1).accepted)
        XCTAssertFalse(try game.submit(player: 1, action: .init(kind: "alternative"), revision: 1).accepted)
        XCTAssertEqual(game.actions.map(\.kind), ["draw", "alternative"])
        _ = try game.submit(player: 1, action: .init(kind: "draw"), revision: 1)
        XCTAssertEqual(game.actions.map(\.kind), ["draw"])
        _ = try game.submit(player: 1, action: .init(kind: "draw"), revision: 2)
        XCTAssertEqual(game.actions.map(\.kind), ["normal"])
        _ = try game.submit(player: 1, action: .init(kind: "normal"), revision: 3)
        _ = try game.submit(player: 0, action: .init(kind: "normal"), revision: 4)
        XCTAssertEqual(game.actions.map(\.kind), ["normal"])
        let optional = try CardGame(rules: source.replacingOccurrences(of: "optional: false", with: "optional: true"))
        _ = try optional.submit(player: 0, action: .init(kind: "normal"), revision: 0)
        XCTAssertEqual(optional.actions.map(\.kind), ["draw", "alternative", "normal"])
        _ = try optional.submit(player: 1, action: .init(kind: "normal"), revision: 1)
        _ = try optional.submit(player: 0, action: .init(kind: "normal"), revision: 2)
        XCTAssertEqual(optional.actions.map(\.kind), ["draw", "alternative", "normal"])
    }

    func testPlayerScopedClearAndSkipKeepOtherPlayersOffers() throws {
        let source = """
            on PrepareGame(players) {
                for player in players {
                    emit Action(spec: [action: #debt, player: player, label: 'Debt', optional: false, priority: 10, handler: Pay(action, player), area: player])
                }
                :board.setstate(player: 0, key: #skip, value: true)
            }
            on BeginTurn(player) {
                if :board.state(player: player, key: #skip) = true {
                    emit ClearActions()
                    :board.setstate(player: player, key: #skip, value: nothing)
                    emit NextPlayersTurn()
                } else {
                    emit Action(spec: [action: #normal, label: 'Normal', handler: Normal(action, player), finishTurn: true, area: player])
                }
            }
            on EndTurn(player) {}
            on Pay(action, player) { emit Complete(action: action) }
            on Normal(action, player) { emit Complete(action: action) }
            """
        let game = try CardGame(rules: source)
        XCTAssertEqual(game.currentPlayer, 1)
        XCTAssertEqual(game.actions.map(\.kind), ["debt"])
        _ = try game.submit(player: 1, action: .init(kind: "debt"), revision: 0)
        _ = try game.submit(player: 1, action: .init(kind: "normal"), revision: 1)
        XCTAssertEqual(game.currentPlayer, 0)
        XCTAssertEqual(game.actions.map(\.kind), ["normal"])
        XCTAssertThrowsError(try CardGame(rules: source.replacingOccurrences(of: "emit ClearActions()", with: "")))
    }

    func testSelectiveClearUsesStableReferencesAndRemovesEmptyGroups() throws {
        let source = """
            on PrepareGame(players) {
                emit ActionGroup(spec: [group: #debt, player: 0, actions: [
                    [action: #a, label: 'A', handler: Done(action, player), area: 0],
                    [action: #b, label: 'B', handler: Done(action, player), area: 0]
                ]])
            }
            on BeginTurn(player) {
                let old be :board.actions(player: player)
                emit ClearActions(actions: old[:filter item where item.id = #a])
                emit ClearActions(actions: old[:filter item where item.id = #b])
                emit Action(spec: [action: #a, label: 'Replacement', finishTurn: true, handler: Done(action, player), area: player])
                emit ClearActions(actions: old)
                emit Inspect(player: player)
            }
            on Inspect(player) { emit NoticeTable(text: (:board.actions(player: player)[:count] as :Text)) }
            on Done(action, player) { emit Complete(action: action) }
            on EndTurn(player) {}
            """
        let game = try CardGame(rules: source)
        XCTAssertEqual(game.actions.map(\.kind), ["a"])
        XCTAssertTrue(game.viewJSON(for: 0).contains("\"tableNotice\":\"1\""))
        XCTAssertTrue(try game.submit(player: 0, action: .init(kind: "a"), revision: 0).accepted)
        XCTAssertEqual(game.currentPlayer, 1)
    }

    func testSelfQueuedActionWaitsForNextTurnAndManualGroupCompletion() throws {
        let source = """
            on PrepareGame(players) {}
            on BeginTurn(player) {
                if player = 0 and :board.actions(player: player)[:count] = 0 {
                    emit ActionGroup(spec: [group: #later, player: player, actions: [[action: #manual, label: 'Manual', consumable: #manual, handler: Manual(action, player), area: player]]])
                }
                emit Action(spec: [action: #normal, label: 'Normal', finishTurn: true, handler: Normal(action, player), area: player])
            }
            on EndTurn(player) {}
            on Normal(action, player) { emit Complete(action: action) }
            on Manual(action, player) {
                emit Complete(action: action)
                emit ConsumeAction(action: action)
            }
            """
        let game = try CardGame(rules: source)
        XCTAssertEqual(game.actions.map(\.kind), ["normal"])
        _ = try game.submit(player: 0, action: .init(kind: "normal"), revision: 0)
        _ = try game.submit(player: 1, action: .init(kind: "normal"), revision: 1)
        XCTAssertEqual(game.actions.map(\.kind), ["manual", "normal"])
        _ = try game.submit(player: 0, action: .init(kind: "manual"), revision: 2)
        XCTAssertEqual(game.actions.map(\.kind), ["normal"])
    }

    private func arrangedMauMau(first: String, second: String, draw: String = "['Q', 'K', 'A', '10', '9', '8'][:select rank => [suit: 'clubs', rank: rank]]") throws -> String {
        try rules.replacingOccurrences(
            of: ":board.create(setup: board(players: players))",
            with: """
                :board.create(setup: [table: [
                    [id: #draw, label: 'Draw', visibility: #hidden, cards: \(draw)],
                    [id: #discard, label: 'Discard', visibility: #top, cards: [[suit: 'clubs', rank: '9']]]
                ], players: [
                    [id: 0, zones: [[id: #hand0, label: 'Hand', layout: #spread, cards: \(first)]]],
                    [id: 1, zones: [[id: #hand1, label: 'Hand', layout: #spread, cards: \(second)]]]
                ]])
                """
        )
        .replacingOccurrences(of: "[1, 1, 1, 1, 1]", with: "[]")
        .replacingOccurrences(of: ":board.take(source: #draw, destination: #discard, count: 1)", with: "")
    }

    func testPaidPenaltyDoesNotCarryIntoNormalSevenPlay() throws {
        let source = try arrangedMauMau(first: "[[suit: 'clubs', rank: '7'], [suit: 'hearts', rank: '9']]", second: "[[suit: 'diamonds', rank: '7'], [suit: 'clubs', rank: '10']]")
        let game = try CardGame(rules: source)
        _ = try game.submit(player: 0, action: XCTUnwrap(game.actions.first { $0.kind == "play" }), revision: game.revision)
        for _ in 0..<2 { _ = try game.submit(player: 1, action: .init(kind: "penalty"), revision: game.revision) }
        let seven = try XCTUnwrap(game.actions.first { $0.kind == "play" && $0.card.flatMap { id in game.cards.first { $0.id == id } }?.properties.asMap?.get("rank")?.asText == "7" })
        _ = try game.submit(player: 1, action: seven, revision: game.revision)
        XCTAssertTrue(game.viewJSON(for: 0).contains("\"remaining\":2"))
        XCTAssertFalse(game.viewJSON(for: 0).contains("\"remaining\":4"))
    }

    func testStackedSevensAreDrawnByPlayerBeforeNormalTurn() throws {
        let source = try arrangedMauMau(first: "[[suit: 'clubs', rank: '7'], [suit: 'hearts', rank: '9']]", second: "[[suit: 'diamonds', rank: '7'], [suit: 'clubs', rank: '10']]")
        let game = try CardGame(rules: source)
        for kind in ["play", "stack"] {
            let action = try XCTUnwrap(game.actions.first { $0.kind == kind })
            _ = try game.submit(player: game.currentPlayer!, action: action, revision: game.revision)
        }
        XCTAssertEqual(game.currentPlayer, 0)
        XCTAssertEqual(game.zones.first { $0.id == "hand0" }!.cards.count, 1)
        for index in 0..<4 {
            XCTAssertFalse(game.actions.contains(.init(kind: "draw")))
            _ = try game.submit(player: 0, action: .init(kind: "penalty"), revision: game.revision)
            XCTAssertEqual(game.currentPlayer, 0)
            XCTAssertEqual(game.zones.first { $0.id == "hand0" }!.cards.count, index + 2)
            XCTAssertFalse(game.actions.contains { $0.kind == "stack" })
        }
        XCTAssertEqual(game.turn, 3)
        XCTAssertTrue(game.actions.contains(.init(kind: "draw")))
        _ = try game.submit(player: 0, action: .init(kind: "draw"), revision: game.revision)
        _ = try game.submit(player: 0, action: .init(kind: "pass"), revision: game.revision)
        XCTAssertEqual(game.currentPlayer, 1)
        XCTAssertFalse(game.actions.contains(.init(kind: "penalty")))
    }

    func testPenaltyRecyclesAndCapsAtAvailableCards() throws {
        let source = try arrangedMauMau(first: "[[suit: 'clubs', rank: '7'], [suit: 'hearts', rank: '9']]", second: "[[suit: 'diamonds', rank: '7'], [suit: 'clubs', rank: '10']]", draw: "[[suit: 'clubs', rank: 'Q']]")
        let game = try CardGame(rules: source)
        for kind in ["play", "stack"] {
            _ = try game.submit(player: game.currentPlayer!, action: XCTUnwrap(game.actions.first { $0.kind == kind }), revision: game.revision)
        }
        let top = game.zones.first { $0.id == "discard" }!.cards.last
        for _ in 0..<3 { _ = try game.submit(player: 0, action: .init(kind: "penalty"), revision: game.revision) }
        XCTAssertFalse(game.actions.contains(.init(kind: "penalty")))
        XCTAssertTrue(game.actions.contains { $0.kind == "play" })
        XCTAssertEqual(game.currentPlayer, 0)
        XCTAssertEqual(game.zones.first { $0.id == "discard" }!.cards, top.map { [$0] })
        XCTAssertEqual(Set(game.zones.flatMap(\.cards)).count, game.cards.count)
    }

    func testJackRequiresColorAndOverwritesPrintedSuit() throws {
        let source = try arrangedMauMau(first: "[[suit: 'hearts', rank: 'J'], [suit: 'clubs', rank: 'Q']]", second: "[[suit: 'diamonds', rank: '10'], [suit: 'hearts', rank: '9'], [suit: 'spades', rank: 'J']]")
        let game = try CardGame(rules: source)
        let jack = try XCTUnwrap(game.actions.first { $0.kind == "play" && game.cards[$0.card! - 1].properties.asMap?.get("rank")?.asText == "J" })
        _ = try game.submit(player: 0, action: jack, revision: 0)
        XCTAssertEqual(game.currentPlayer, 0)
        XCTAssertEqual(Set(game.actions.map(\.kind)), ["clubs", "spades", "hearts", "diamonds"])
        _ = try game.submit(player: 0, action: .init(kind: "diamonds"), revision: game.revision)
        XCTAssertEqual(game.currentPlayer, 1)
        let playable = game.actions.filter { $0.kind == "play" }.map { game.cards[$0.card! - 1].properties.asMap?.get("suit")?.asText }
        XCTAssertEqual(Set(playable.compactMap { $0 }), ["diamonds"])
    }

    func testJackOnJackRejectedEvenWithMatchingRequestedSuit() throws {
        let source = try arrangedMauMau(first: "[[suit: 'hearts', rank: 'J'], [suit: 'clubs', rank: 'Q']]", second: "[[suit: 'spades', rank: 'J'], [suit: 'spades', rank: '9']]")
        for forceOffer in [false, true] {
            let game = try CardGame(
                rules: forceOffer ? source.replacingOccurrences(of: "let plays be playable(player: player)[:select item => item.id]", with: "let plays be :board.cards(zone: hand(player: player))[:select item => item.id]") : source
            )
            let jack = try XCTUnwrap(game.actions.first { $0.kind == "play" && game.cards[$0.card! - 1].properties.asMap?.get("rank")?.asText == "J" })
            _ = try game.submit(player: 0, action: jack, revision: game.revision)
            _ = try game.submit(player: 0, action: .init(kind: "spades"), revision: game.revision)
            let forbidden = try XCTUnwrap(game.cards.first { $0.properties.asMap?.get("rank")?.asText == "J" && $0.properties.asMap?.get("suit")?.asText == "spades" })
            let offer = OfferedAction(kind: "play", card: forbidden.id)
            XCTAssertEqual(game.actions.contains(offer), forceOffer)
            let revision = game.revision
            let zones = game.zones.map(\.cards)
            XCTAssertFalse(try game.submit(player: 1, action: offer, revision: revision).accepted)
            XCTAssertEqual(game.revision, revision)
            XCTAssertEqual(game.zones.map(\.cards), zones)
        }
    }

    private func actionRules(_ mode: String, optional: Bool = false, finish: Bool = true, body: String = "emit Complete(action: action)") -> String {
        """
        on PrepareGame(players) {}
        on BeginTurn(player) {
            emit Action(action: #work, label: 'Work', optional: \(optional), finishTurn: \(finish), consumable: \(mode), handler: Work(action, player), area: player)
        }
        on Work(action, player) { \(body) }
        on EndTurn(player) {}
        """
    }

    func testCountedAndManualActions() throws {
        let counted = try CardGame(rules: actionRules("2"))
        _ = try counted.submit(player: 0, action: .init(kind: "work"), revision: 0)
        XCTAssertEqual(counted.currentPlayer, 0)
        XCTAssertEqual(counted.actions, [.init(kind: "work")])
        _ = try counted.submit(player: 0, action: .init(kind: "work"), revision: 1)
        XCTAssertEqual(counted.currentPlayer, 1)

        let manual = try CardGame(rules: actionRules("#manual", body: "emit Complete(action: action)\nemit ConsumeAction(action: action)"))
        _ = try manual.submit(player: 0, action: .init(kind: "work"), revision: 0)
        XCTAssertEqual(manual.currentPlayer, 1)

        let retained = try CardGame(rules: actionRules("#manual"))
        _ = try retained.submit(player: 0, action: .init(kind: "work"), revision: 0)
        XCTAssertEqual(retained.currentPlayer, 0)
        XCTAssertEqual(retained.actions.count, 1)
    }

    func testRejectedActionsNeverConsumeAndNeverActionsRepeat() throws {
        let rejected = try CardGame(rules: actionRules("2", body: "emit Reject(action: action, reason: 'No')"))
        for _ in 0..<3 {
            XCTAssertFalse(try rejected.submit(player: 0, action: .init(kind: "work"), revision: 0).accepted)
        }
        XCTAssertEqual(rejected.revision, 0)
        XCTAssertEqual(rejected.currentPlayer, 0)
        let repeatable = try CardGame(rules: actionRules("#never", optional: true, finish: false))
        for revision in 0..<3 {
            XCTAssertTrue(try repeatable.submit(player: 0, action: .init(kind: "work"), revision: revision).accepted)
            XCTAssertEqual(repeatable.currentPlayer, 0)
        }
        for mode in ["0", "-1", "#unknown", "#never"] {
            XCTAssertThrowsError(try CardGame(rules: actionRules(mode)))
        }
    }

    func testRequiredActionsDelayAutomaticTurnEnd() throws {
        let source = """
            on PrepareGame(players) {}
            on BeginTurn(player) {
                emit Action(action: #finish, label: 'Finish', optional: true, finishTurn: true, consumable: #auto, handler: Work(action, player), area: player)
                emit Action(action: #work, label: 'Work', optional: false, finishTurn: false, consumable: 2, handler: Work(action, player), area: player)
            }
            on Work(action, player) { emit Complete(action: action) }
            on EndTurn(player) {}
            """
        let game = try CardGame(rules: source)
        _ = try game.submit(player: 0, action: .init(kind: "finish"), revision: 0)
        XCTAssertEqual(game.currentPlayer, 0)
        _ = try game.submit(player: 0, action: .init(kind: "work"), revision: 1)
        XCTAssertEqual(game.currentPlayer, 0)
        _ = try game.submit(player: 0, action: .init(kind: "work"), revision: 2)
        XCTAssertEqual(game.currentPlayer, 1)
    }

    func testDrawnCardBecomesPlayableAndPlayingEndsTurn() throws {
        let source = try rules.replacingOccurrences(
            of:
                "function fits(card, top) be top.properties.rank <> 'J' when card.properties.rank = 'J' otherwise matches(card: card, top: top, wish: :board.state(key: #wish))",
            with: "function fits(card, top) be true"
        )
        let game = try CardGame(rules: source)
        XCTAssertFalse(game.actions.contains(.init(kind: "pass")))
        let card = try XCTUnwrap(game.zones.first(where: { $0.id == "draw" })?.cards.last)
        _ = try game.submit(player: 0, action: .init(kind: "draw"), revision: 0)
        XCTAssertTrue(game.actions.contains(.init(kind: "play", card: card)))
        _ = try game.submit(player: 0, action: .init(kind: "play", card: card), revision: 1)
        XCTAssertEqual(game.revision, 2)
        XCTAssertEqual(game.zones.first(where: { $0.id == "discard" })?.cards.last, card)
        XCTAssertEqual(game.zones.first(where: { $0.id == "hand0" })?.cards.count, 5)
    }

}
