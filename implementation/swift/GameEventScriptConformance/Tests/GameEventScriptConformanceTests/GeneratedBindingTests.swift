// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

/// Models generated with --bindings: scripts read and construct them as external types, as in the C# tests.
final class GeneratedBindingTests: XCTestCase {
    private static let script = """
        on Start(view) {
          let bot be view.bots[1]
          let made be :World_Body(fields: [mass: 90kg, hull: #light])
          let radar be :RadarView(fields: [range: 300m, contacts: [1, 2]])
          let built be :BotView(fields: [bot: 7, name: 'Bo', alive: true, health: 50%, speed: 2.5, heading: 90°, mood: #angry, team: 'blue', position: :Point(3m, 4m), velocity: :Vector(1m, 0m), body: made, radar: radar, facing: #north, marks: [:Point(1m, 1m)], units: ['x', made], grid: [[1, 2]], rolls: :Dice[6, 3], seal: :Bytes("a0ff"), chunks: [:Bytes[1], :Bytes[]], status: [armed:, cloaked: false], ranks: [[gold:, silver:], [:]], hints: [#left, #right]])
          emit Seen(name: bot.name, x: bot.position.x, mass: bot.body.mass, radar: bot.radar, facing: bot.facing, units: bot.units[:count], hint: bot.hints[1], built: built, bad: :World_Body(fields: [mass: 90m, hull: #light]), unknown: :World_Body(fields: [mass: 1kg, hull: #light, extra: 1]), copied: :World_Body(fields: made.fields))
        }
        """

    func testScriptsReadAndConstructGeneratedRecords() throws {
        let types = try RolesBound.GesTypes()
        let program = try GesProgramBuilder().withExternalTypeCatalog(types).addScript(Self.script).compile()
        let host = try GameEventScriptHost.makeBuilder().withExternalTypeRegistry(types).build()
        _ = try host.load(program)
        var seen: GameEventScriptMessage?
        _ = try host.subscribe(.init(name: "Seen", parameters: ["name", "x", "mass", "radar", "facing", "units", "hint", "built", "bad", "unknown", "copied"])) { message, _ in seen = message }
        _ = try host.startForTest()
        var bot = RolesBound.BotView(
            bot: 4,
            name: "Ada",
            alive: true,
            health: 1,
            speed: 0,
            heading: 0,
            mood: .calm,
            team: "red",
            position: GesPoint(x: 2, y: 5),
            velocity: GesVector(),
            body: RolesBound.World_Body(mass: 120, hull: .heavy),
            facing: .quantity(45),
            hints: [.right]
        )
        bot.units = [.text("a")]
        let view = RolesBound.WorldView(tick: 1, time: 0, arena: GesVector(), bots: [bot])
        XCTAssertTrue(host.receive(try .init(name: "Start", arguments: [try .init(name: "view", value: view.gesValue())])))
        XCTAssertEqual(try host.runToCompletion().state, .completed)
        let arguments = try XCTUnwrap(seen).arguments
        XCTAssertEqual(arguments[0].textValue, "Ada")
        XCTAssertEqual(arguments[1].asNumber, 2)
        XCTAssertEqual(arguments[1].unit, .meter)
        XCTAssertEqual(arguments[2].asNumber, 120)
        XCTAssertTrue(arguments[3].isNothing, "An absent optional record reads as nothing.")
        XCTAssertEqual(arguments[4].asNumber, 45)
        XCTAssertEqual(arguments[5].asInteger, 1)
        XCTAssertEqual(arguments[6].textValue, "right")
        let built = try XCTUnwrap(RolesBound.BotView(gesValue: arguments[7]))
        XCTAssertEqual(built.bot, 7)
        XCTAssertEqual(built.health, 0.5)
        XCTAssertEqual(built.mood, .angry)
        XCTAssertEqual(built.position, GesPoint(x: 3, y: 4))
        XCTAssertEqual(built.body, RolesBound.World_Body(mass: 90, hull: .light))
        XCTAssertEqual(built.radar, RolesBound.RadarView(range: 300, contacts: [1, 2]))
        XCTAssertEqual(built.facing, .tag(.north))
        XCTAssertNil(built.note)
        XCTAssertEqual(built.units, [.text("x"), .world_Body(RolesBound.World_Body(mass: 90, hull: .light))])
        XCTAssertEqual(built.grid, [[1, 2]])
        XCTAssertEqual(built.rolls, [6, 3])
        XCTAssertEqual(built.hints, [.left, .right])
        XCTAssertEqual(built.status, .armed)
        XCTAssertEqual(built.ranks, [[.gold, .silver], []])
        XCTAssertTrue(arguments[8].isNothing, "A value of another unit constructs nothing.")
        XCTAssertTrue(arguments[9].isNothing, "A field that the type does not declare constructs nothing.")
        XCTAssertEqual(RolesBound.World_Body(gesValue: arguments[10]), RolesBound.World_Body(mass: 90, hull: .light), "The fields of a record construct an equal record.")
    }

    func testASetRefusesATypeThatTheNextCatalogDeclares() throws {
        XCTAssertThrowsError(try RolesBound.GesTypes(nextCatalog: try RolesBound.GesTypes()))
        XCTAssertEqual(try RolesBound.GesTypes().types.map(\.name), ["World_Body", "RadarView", "BotView", "GearPreset", "GearView", "Panel_Badge", "Panel_Entry", "Panel_Report", "RadarEcho", "WorldView"])
    }
}
