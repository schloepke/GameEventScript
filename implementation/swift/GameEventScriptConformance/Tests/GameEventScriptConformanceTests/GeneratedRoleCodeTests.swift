// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/roles: spatial wire fields in 2D and 3D use the runtime's
/// GesPoint and GesVector, and model types are plain values that compare by value.
final class GeneratedRoleCodeTests: XCTestCase {
    func testSpatialFieldsUseTheRuntimeTypesIn2DAnd3D() throws {
        let world = RolesWire.World()
        world.tick = 7
        world.arena = GesVector(x: 2000, y: 1500)
        var bot = RolesWire.World_Bot()
        bot.id = 3
        bot.position = GesPoint(x: 12.34, y: 56.78)
        bot.velocity = GesVector(x: -1.5, y: 2.25, z: 0.5)
        bot.body.mass = 120
        bot.token = [0xa0, 0x00, 0xff]
        world.bots.append(bot)
        let read = RolesWire.World()
        read.arena = GesVector(x: 0, y: 0, z: 9)
        _ = try read.read(from: try world.encoded())
        XCTAssertEqual(read.arena, GesVector(x: 2000, y: 1500), "A 2D field reads its omitted axis as zero.")
        XCTAssertEqual(read.bots[0].position, GesPoint(x: 12.34, y: 56.78))
        XCTAssertEqual(read.bots[0].velocity, GesVector(x: -1.5, y: 2.25, z: 0.5))
        XCTAssertEqual(read.bots[0].token, [0xa0, 0x00, 0xff], "Bytes need not be UTF-8.")
        XCTAssertTrue(try read.wireEquals(world))
    }

    func testANonzeroOmittedAxisIsATypeMismatch() throws {
        let world = RolesWire.World()
        world.arena = GesVector(x: 1, y: 2, z: 3)
        XCTAssertThrowsError(try world.encoded()) { error in
            XCTAssertEqual((error as? GesWireError)?.code, GesWireError.typeMismatch)
            XCTAssertEqual((error as? GesWireError)?.path, "arena")
        }
        let other = RolesWire.World()
        var bot = RolesWire.World_Bot()
        bot.position = GesPoint(x: 1, y: 1, z: 1)
        other.bots.append(bot)
        XCTAssertThrowsError(try other.encoded()) { error in
            XCTAssertEqual((error as? GesWireError)?.code, GesWireError.typeMismatch)
            XCTAssertEqual((error as? GesWireError)?.path, "bots[].position")
        }
        XCTAssertThrowsError(try world.deltaEncoded(against: RolesWire.World())) { error in
            XCTAssertEqual((error as? GesWireError)?.code, GesWireError.typeMismatch)
        }
    }

    func testModelsCompareByValue() throws {
        func bot() -> RolesModel.BotView {
            RolesModel.BotView(
                bot: 4,
                name: "Ada",
                alive: true,
                health: 0.5,
                speed: 1,
                heading: 90,
                mood: .calm,
                team: "red",
                position: GesPoint(x: 1, y: 2),
                velocity: GesVector(),
                body: RolesModel.World_Body(mass: 80, hull: .heavy),
                facing: .tag(.north),
                marks: [GesPoint(x: 0, y: -0.0), GesPoint(x: 3, y: 4)],
                units: [.text("x"), .world_Body(RolesModel.World_Body(mass: 1, hull: .light))],
                grid: [[1, 2], []],
                rolls: [6, 3],
                hints: [.left]
            )
        }

        var right = bot()
        right.marks[0] = GesPoint(x: 0, y: 0)
        XCTAssertEqual(bot(), right, "0 and -0 are equal.")
        XCTAssertEqual(bot().hashValue, right.hashValue)
        XCTAssertEqual(bot().id, 4)
        right.grid[0].append(3)
        XCTAssertNotEqual(bot(), right)
        right = bot()
        right.facing = nil
        XCTAssertNotEqual(bot(), right)
        XCTAssertNil(right.radar)
        XCTAssertNil(right.note)
        let view = RolesModel.WorldView(tick: 1, time: 0.5, arena: GesVector(x: 10, y: 10), bots: [bot()], badge: RolesModel.Panel_Badge(label: "hi"))
        XCTAssertEqual(view, RolesModel.WorldView(tick: 1, time: 0.5, arena: GesVector(x: 10, y: 10), bots: [bot()], badge: RolesModel.Panel_Badge(label: "hi")))
        XCTAssertEqual(RolesModel.BotView.MoodTag.allCases, [.calm, .angry])
        XCTAssertEqual(RolesModel.BotView.MoodTag.angry.rawValue, "angry")
    }
}
