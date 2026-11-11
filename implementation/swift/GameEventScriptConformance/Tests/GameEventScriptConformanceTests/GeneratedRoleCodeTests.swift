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

    private func report(title: String = "Pump") -> RolesModel.Panel_Report {
        RolesModel.Panel_Report(
            title: title,
            status: .warn,
            load: 640,
            spot: GesPoint(x: 1.5, y: 2.25),
            entries: [RolesModel.Panel_Entry(name: "left", level: 0.25), RolesModel.Panel_Entry(name: "right", level: 1)],
            reading: .number(42),
            alarms: .power
        )
    }

    func testAModelRoundTripsThroughItsPlainValue() throws {
        let model = report()
        let value = model.toValue()
        XCTAssertEqual(value.customTypeName, "Panel_Report")
        let fields = try XCTUnwrap(value.asMap)
        XCTAssertEqual(fields.get("entries")?.listValue?.first?.customTypeName, "Panel_Entry", "A nested record is a Record, not an external value.")
        XCTAssertTrue(fields.get("note")?.isNothing == true, "An absent optional is nothing.")
        XCTAssertEqual(fields.get("reading")?.integerValue, 42, "A union holds its alternative's value.")
        XCTAssertEqual(try RolesModel.Panel_Report(value: value), model)
        var other = report()
        other.note = "spare"
        other.reading = .code("x")
        other.alarms = [.heat, .power]
        XCTAssertEqual(try RolesModel.Panel_Report(value: other.toValue()), other)
    }

    func testAModelConvertsToAWireRecordAndBack() throws {
        let model = report()
        let wire = RolesWire.Panel_Report()
        try wire.read(value: model.toValue())
        XCTAssertEqual(wire.title, "Pump")
        XCTAssertEqual(wire.entries.count, 2)
        XCTAssertEqual(try RolesModel.Panel_Report(value: wire.toValue()), model)
        let back = RolesWire.Panel_Report()
        _ = try back.read(from: try wire.encoded())
        XCTAssertEqual(try RolesModel.Panel_Report(value: back.toValue()), model)
    }

    func testListsOfTextsSetTheirElementsFromStrings() throws {
        let cards = RolesWire.World_Cards()
        cards.labelsValues = ["red", "blue"]
        XCTAssertEqual(cards.labelsValues, ["red", "blue"])
        let first = cards.labels[0]
        cards.setLabelsCount(1)
        XCTAssertTrue(cards.labels[0] === first, "The record reuses its elements.")
        cards.labels[0].valueUTF8 = Array("green".utf8)
        let back = RolesWire.World_Cards()
        _ = try back.read(from: try cards.encoded())
        XCTAssertEqual(back.labelsValues, ["green"])
    }

    func testByteCountsAreExactBeforeWriting() throws {
        let wire = RolesWire.Panel_Report()
        try wire.read(value: report().toValue())
        let baseline = RolesWire.Panel_Report()
        let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: RolesWire.Panel_Report.maxByteCount, alignment: 8)
        defer { buffer.deallocate() }
        XCTAssertEqual(try wire.encodedByteCount(), try wire.write(into: buffer))
        XCTAssertEqual(try wire.encoded().count, try wire.encodedByteCount())
        let count = try wire.writeDelta(against: baseline, into: buffer)
        XCTAssertEqual(try wire.deltaEncodedByteCount(against: baseline), count)
        XCTAssertEqual(try wire.deltaEncoded(against: baseline), Array(buffer[0..<count]))
        wire.title = "a title that is far too long"
        XCTAssertThrowsError(try wire.encodedByteCount()) { XCTAssertEqual(($0 as? GesWireError)?.code, GesWireError.tooLong) }
    }

    func testAModelValueThatDoesNotFitFailsOnTheWireSide() throws {
        let model = report(title: "a title that is far too long")
        let value = model.toValue()
        XCTAssertEqual(try RolesModel.Panel_Report(value: value), model, "The model value itself holds any text.")
        XCTAssertThrowsError(try RolesWire.Panel_Report().read(value: value)) { error in
            XCTAssertEqual((error as? GesWireError)?.code, GesWireError.tooLong)
            XCTAssertEqual((error as? GesWireError)?.path, "title")
        }
    }

    func testAValueThatDoesNotConvertNamesItsField() throws {
        let fields = try XCTUnwrap(report().toValue().asMap)
        let keys = (0..<fields.length).map { fields.key(at: $0) }
        let values = keys.map { $0 == "load" ? GesValue.float(640, unit: .meter) : fields.get($0)! }
        XCTAssertThrowsError(try RolesModel.Panel_Report(value: .record(typeName: "Panel_Report", keys: keys, values: values))) { error in
            XCTAssertEqual((error as? GesWireError)?.code, GesWireError.typeMismatch)
            XCTAssertEqual((error as? GesWireError)?.path, "load")
        }
        XCTAssertThrowsError(try RolesModel.Panel_Report(value: .record(typeName: "Other", keys: keys, values: values))) { error in
            XCTAssertEqual((error as? GesWireError)?.path, "")
        }
        let map = GesValue.map(keys: keys, values: keys.map { fields.get($0)! })
        XCTAssertEqual(try RolesModel.Panel_Report(value: map), report(), "A Map of only the fields reads too.")
    }

    func testANestedValueThatDoesNotConvertNamesItsPathAsTheWireSideDoes() throws {
        let fields = try XCTUnwrap(report().toValue().asMap)
        let keys = (0..<fields.length).map { fields.key(at: $0) }
        let entry = GesValue.record(typeName: "Panel_Entry", keys: ["name", "level"], values: [.text("left"), .float(0.25, unit: .meter)])
        for (entries, path) in [(GesValue.list([entry]), "entries[].level"), (.list([.text("left")]), "entries[]"), (.text("left"), "entries")] {
            let value = GesValue.record(typeName: "Panel_Report", keys: keys, values: keys.map { $0 == "entries" ? entries : fields.get($0)! })
            XCTAssertThrowsError(try RolesModel.Panel_Report(value: value)) { error in
                XCTAssertEqual((error as? GesWireError)?.code, GesWireError.typeMismatch)
                XCTAssertEqual((error as? GesWireError)?.path, path)
            }
            XCTAssertThrowsError(try RolesWire.Panel_Report().read(value: value)) { error in
                XCTAssertEqual((error as? GesWireError)?.path, path)
            }
        }
    }

    func testMissingAndNothingValuesHaveTheCodesOfTheWireSide() throws {
        let fields = try XCTUnwrap(report().toValue().asMap)
        let keys = (0..<fields.length).map { fields.key(at: $0) }
        let without = { (name: String) -> GesValue in
            let kept = keys.filter { $0 != name }
            return .map(keys: kept, values: kept.map { fields.get($0)! })
        }
        let with = { (name: String, held: GesValue) -> GesValue in .map(keys: keys, values: keys.map { $0 == name ? held : fields.get($0)! }) }
        let cases: [(GesValue, String, String)] = [
            (without("title"), GesWireError.missingField, "title"),
            (with("title", .nothing), GesWireError.nothingValue, "title"),
            (with("entries", .list([.nothing])), GesWireError.nothingValue, "entries[]"),
        ]
        for (value, code, path) in cases {
            XCTAssertThrowsError(try RolesModel.Panel_Report(value: value)) { error in
                XCTAssertEqual((error as? GesWireError)?.code, code)
                XCTAssertEqual((error as? GesWireError)?.path, path)
            }
            XCTAssertThrowsError(try RolesWire.Panel_Report().read(value: value)) { error in
                XCTAssertEqual((error as? GesWireError)?.code, code)
                XCTAssertEqual((error as? GesWireError)?.path, path)
            }
        }
        XCTAssertEqual(try RolesModel.Panel_Report(value: without("note")), report(), "An optional field may be left out.")
    }

    func testModelsCarryTheSchemaHashOfTheirWireRecords() {
        XCTAssertEqual(RolesModel.Panel_Report.schemaHash, RolesWire.Panel_Report.schemaHash)
        XCTAssertEqual(RolesBound.Panel_Report.schemaHash, RolesModel.Panel_Report.schemaHash)
    }

    func testLetsAreStaticMembersInBaseUnits() {
        XCTAssertEqual(RolesWire.World_Body.heaviest, 1000)
        XCTAssertEqual(RolesModel.World_Body.heaviest, 1000)
        XCTAssertEqual(RolesWire.World_Body.wear, 0.025)
        XCTAssertEqual(RolesModel.World_Body.wear, 0.025)
        XCTAssertEqual(RolesWire.World_Cards.deck, Int64(4))
    }

    func testDefaultsFillWhatAModelOrValueLeavesOut() throws {
        XCTAssertEqual(RolesModel.RadarView(contacts: []), RolesModel.RadarView(range: 500, reach: 500, contacts: [], side: nil, lead: nil, active: true))
        let radar = RolesModel.RadarView(range: 300, contacts: [1], side: .left)
        XCTAssertEqual(radar.reach, 300)
        XCTAssertEqual(radar.lead, .left)
        XCTAssertTrue(radar.active)

        let read = try RolesModel.RadarView(value: .map(keys: ["contacts", "side"], values: [.list([]), try .tag("right")]))
        XCTAssertEqual(read, RolesModel.RadarView(range: 500, reach: 500, contacts: [], side: .right, lead: .right, active: true))
        let kept = try RolesModel.RadarView(value: .map(keys: ["contacts", "side", "lead"], values: [.list([]), try .tag("right"), .nothing]))
        XCTAssertNil(kept.lead)
        let bound = try RolesBound.RadarView(value: .map(keys: ["range", "contacts"], values: [.float(7, unit: .meter), .list([])]))
        XCTAssertEqual(bound.reach, 7)

        var body = RolesWire.World_Body()
        try body.read(value: .map(keys: ["mass"], values: [.integer(3, unit: .kilogram)]))
        XCTAssertEqual(body.hull, .light)
    }

    func testBoundModelsAlsoConvertToPlainValues() throws {
        let value = report().toValue()
        let bound = try RolesBound.Panel_Report(value: value)
        XCTAssertEqual(bound.toValue(), value)
        XCTAssertEqual(RolesBound.Panel_Report(gesValue: bound.gesValue()), bound)
    }
}
