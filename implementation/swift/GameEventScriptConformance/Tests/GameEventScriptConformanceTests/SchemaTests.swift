// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Checks the implementation details of the writer and reader that the value-based corpus cannot reach.
final class SchemaTests: XCTestCase {
    private static let botSource = """
        constant $maxHealth be 135
        parameter worldWidth: :Quantity(m)
        parameter worldHeight: :Quantity(m)

        schema :Bot as {
          id: :Number integer between 0 and 65535 key
          x: :Quantity(m) between 0m and worldWidth step 1cm delta within 1.27m
          y: :Quantity(m) between 0m and worldHeight step 1cm delta within 1.27m
          heading: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°
          turretBearing: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°
          radarBearing: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°
          health: :Number integer between 0 and $maxHealth
          firing: :Boolean
          shielded: :Boolean
          overheated: :Boolean
        }
        """

    private func bot() throws -> GesBoundSchema {
        let parameters = GesSchemaParameters()
        try parameters.with("worldWidth", literal: "5000m")
        try parameters.with("worldHeight", literal: "5km")
        return try GesSchemaSet(parsing: Self.botSource).schema("Bot").bind(parameters: parameters)
    }

    func testHashVectorsAndSpellingIndependence() throws {
        let bound = try bot()
        XCTAssertEqual(String(bound.hash, radix: 16), "baecf12abb34e7c1")
        XCTAssertEqual(String(bound.bindingHash, radix: 16), "551dd726cf2392d4")
        let plain = try GesSchemaSet(parsing: "schema :Force as { force: :Quantity(N) between 0kN and 50kN step 0.01kN }")
        let spelled = try GesSchemaSet(parsing: "// other\n\nschema :Force as {\n force: :Quantity(N) between 0N and 50000N step 10N\n}\n")
        XCTAssertEqual(try plain.schema("Force").canonicalForm, try spelled.schema("Force").canonicalForm)
        XCTAssertEqual(try plain.schema("Force").canonicalForm, "gess1\nschema Force\nfield force Quantity(newton) between 0e0 5e4 step 1e1\n")
    }

    func testWriterAndReaderMatchTheWorkedExample() throws {
        let schema = try bot()
        XCTAssertEqual(schema.maxByteCount, 13)
        let (id, x, y, heading) = (schema.field("id")!, schema.field("x")!, schema.field("y")!, schema.field("heading")!)
        let (turret, radar, health) = (schema.field("turretBearing")!, schema.field("radarBearing")!, schema.field("health")!)
        let (firing, shielded, overheated) = (schema.field("firing")!, schema.field("shielded")!, schema.field("overheated")!)
        var buffer = [UInt8](repeating: 0, count: 32)
        let length = try buffer.withUnsafeMutableBytes { bytes -> Int in
            var writer = GesSchemaWriter(schema, into: bytes)
            try writer.writeInteger(id, 42)
            try writer.writeNumber(x, 1234.565)
            try writer.writeNumber(y, 4999.996)
            try writer.writeNumber(heading, -90)
            try writer.writeNumber(turret, 359.95)
            try writer.writeNumber(radar, 10)
            try writer.writeInteger(health, 135)
            try writer.writeBoolean(firing, true)
            try writer.writeBoolean(shielded, false)
            try writer.writeBoolean(overheated, true)
            return try writer.finish()
        }
        XCTAssertEqual(length, 13)
        try buffer.withUnsafeBytes { bytes in
            var reader = GesSchemaReader(schema, data: UnsafeRawBufferPointer(rebasing: bytes[0..<length]))
            XCTAssertEqual(try reader.readKey(), 42)
            XCTAssertEqual(try reader.readNumber(x), 1234.56)
            XCTAssertEqual(try reader.readNumber(y), 5000)
            XCTAssertEqual(try reader.readNumber(heading), 270)
            XCTAssertEqual(try reader.readNumber(turret), 0)
            XCTAssertEqual(try reader.readNumber(radar), 10)
            XCTAssertEqual(try reader.readInteger(health), 135)
            XCTAssertTrue(try reader.readBoolean(firing))
            XCTAssertFalse(try reader.readBoolean(shielded))
            XCTAssertTrue(try reader.readBoolean(overheated))
            XCTAssertEqual(try reader.finish(), 13)
        }
    }

    func testDescriptionIsSharedAndConvertsBounds() throws {
        let set = try GesSchemaSet(parsing: "parameter w: :Quantity(m)\nschema :U as {\n  x: :Quantity(m) between 0.5m and w\n}\nschema :A as {\n  u: :U\n}\n")
        let unit = try set.schema("U")
        XCTAssertTrue(try set.schema("A").fields[0].alternatives[0].schema === unit)
        XCTAssertTrue(set.schemas[0] === unit)
        let x = unit.fields[0].alternatives[0]
        XCTAssertEqual(x.lower?.doubleValue, 0.5)
        XCTAssertNil(x.upper?.doubleValue)
        XCTAssertEqual(x.upper?.parameter, "w")
        let parameters = try GesSchemaParameters().with("w", literal: "5m")
        XCTAssertTrue(try unit.bind(parameters: parameters).schema === unit)
    }

    func testDescriptionOutlivesItsSetAndIsFreed() throws {
        final class Box {
            weak var schema: GesSchema?
            weak var set: GesSchemaSet?
        }

        let source = "schema :U as {\n  x: :Boolean\n}\nschema :A as {\n  u: :U\n}\nmessage M(a: :A)\nmessages :S as (M)\n"
        let box = Box()
        do {
            let set = try GesSchemaSet(parsing: source)
            box.set = set
            box.schema = try set.schema("A")
        }
        XCTAssertNil(box.set)
        XCTAssertNil(box.schema)
        var held: GesSchema? = try GesSchemaSet(parsing: source).schema("S")
        XCTAssertEqual(held?.members[0].fields[0].alternatives[0].schema?.fields[0].name, "u")
        let bound = try held?.bind()
        XCTAssertTrue(bound?.schema === held)
        box.schema = held
        held = nil
        withExtendedLifetime(bound) { XCTAssertNotNil(box.schema) }
    }

    func testRepeatedPresenceReadsOneBit() throws {
        let bound = try GesSchemaSet(parsing: "schema :S as {\n  a: :Boolean optional\n  b: :Boolean\n}\n").schema("S").bind()
        let (a, b) = (bound.field("a")!, bound.field("b")!)
        let data: [UInt8] = [0b011]
        try data.withUnsafeBytes { bytes in
            var reader = GesSchemaReader(bound, data: bytes)
            XCTAssertTrue(try reader.readPresence(a))
            XCTAssertTrue(try reader.readPresence(a))
            XCTAssertTrue(try reader.readBoolean(a))
            XCTAssertFalse(try reader.readBoolean(b))
            XCTAssertEqual(try reader.finish(), 1)
        }
    }

    func testRejectedTagLeavesTheWriterUnchanged() throws {
        let state = try GesSchemaSet(parsing: "schema :S as {\n  state: :Tag(#a, #b) optional\n}\n").schema("S").bind()
        let field = state.field("state")!
        var buffer = [UInt8](repeating: 0, count: 2)
        let length = try buffer.withUnsafeMutableBytes { bytes -> Int in
            var writer = GesSchemaWriter(state, into: bytes)
            XCTAssertThrowsError(try writer.writeTag(field, 9))
            try writer.writeTag(field, 1)
            return try writer.finish()
        }
        XCTAssertEqual(length, 1)
        XCTAssertEqual(buffer[0], 0b11)
    }

    func testListsNestAndOptionalsMarkAbsence() throws {
        let set = try GesSchemaSet(parsing: "schema :Mount as { size: :Number integer between 0 and 3\n label: :Text max 4 optional }\nschema :Unit as { mounts: :List(:Mount) max 2\n next: :Boolean }")
        let unit = try set.schema("Unit").bind()
        let (mounts, element, size, label, next) = (unit.field("mounts")!, unit.field("mounts[]")!, unit.field("mounts[].size")!, unit.field("mounts[].label")!, unit.field("next")!)
        var buffer = [UInt8](repeating: 0, count: 32)
        let text: [UInt8] = [97, 98]
        let length = try buffer.withUnsafeMutableBytes { bytes -> Int in
            var writer = GesSchemaWriter(unit, into: bytes)
            try writer.beginList(mounts, count: 2)
            try writer.beginNested(element)
            try writer.writeInteger(size, 1)
            try text.withUnsafeBytes { try writer.writeText(label, $0) }
            try writer.beginNested(element)
            try writer.writeInteger(size, 3)
            try writer.writeAbsent(label)
            try writer.writeBoolean(next, true)
            return try writer.finish()
        }
        try buffer.withUnsafeBytes { bytes in
            var reader = GesSchemaReader(unit, data: UnsafeRawBufferPointer(rebasing: bytes[0..<length]))
            XCTAssertEqual(try reader.beginList(mounts), 2)
            try reader.beginNested(element)
            XCTAssertEqual(try reader.readInteger(size), 1)
            XCTAssertTrue(try reader.readPresence(label))
            var destination = [UInt8](repeating: 0, count: 4)
            var count = 0
            var copy = reader
            try destination.withUnsafeMutableBytes { count = try copy.readText(label, into: $0) }
            reader = copy
            XCTAssertEqual(count, 2)
            try reader.beginNested(element)
            XCTAssertEqual(try reader.readInteger(size), 3)
            XCTAssertFalse(try reader.readPresence(label))
            XCTAssertTrue(try reader.readBoolean(next))
            XCTAssertEqual(try reader.finish(), length)
        }
    }

    func testATagWithAMaximumWritesOnlyTagNames() throws {
        let bound = try GesSchemaSet(parsing: "schema :S as { t: :Tag max 3 }").schema("S").bind()
        let field = bound.field("t")!
        var buffer = [UInt8](repeating: 0, count: 8)
        try buffer.withUnsafeMutableBytes { bytes in
            var writer = GesSchemaWriter(bound, into: bytes)
            for invalid in ["", "Ab", "a_", "1a", "ä"] {
                XCTAssertThrowsError(try Array(invalid.utf8).withUnsafeBytes { try writer.writeText(field, $0) }) { error in
                    XCTAssertEqual((error as? GesWireError)?.code, "wire.invalidTag", invalid)
                }
            }
            try Array("ab".utf8).withUnsafeBytes { try writer.writeText(field, $0) }
            XCTAssertEqual(try writer.finish(), 3)
        }
        XCTAssertEqual(Array(buffer[0..<3]), [0x86, 0x89, 0x01])
    }

    func testAnInvalidAlternativeWritesNothing() throws {
        let bound = try GesSchemaSet(parsing: "schema :S as { a: :Boolean | :Text max 1 optional }").schema("S").bind()
        let (a, flag) = (bound.field("a")!, bound.field("a:Boolean")!)
        var buffer = [UInt8](repeating: 0, count: 8)
        try buffer.withUnsafeMutableBytes { bytes in
            var writer = GesSchemaWriter(bound, into: bytes)
            XCTAssertThrowsError(try writer.selectAlternative(a, 2))
            try writer.selectAlternative(a, 0)
            try writer.writeBoolean(flag, true)
            XCTAssertEqual(try writer.finish(), 1)
        }
        XCTAssertEqual(buffer[0], 0b101)
    }
}
