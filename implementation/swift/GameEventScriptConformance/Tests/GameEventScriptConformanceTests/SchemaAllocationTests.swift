// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import ConformanceInstrumentation
import GameEventScriptRuntime
import XCTest

/// The writer and reader allocate nothing on success.
final class SchemaAllocationTests: XCTestCase {
    private struct Mismatch: Error {}

    func testWriterAndReaderAllocateNothing() throws {
        #if !canImport(Darwin)
            throw XCTSkip("The allocation counter uses libmalloc.")
        #else
            guard ges_allocation_install(), ges_allocation_controls() else { throw XCTSkip("The allocation counter is not available.") }
            let schema = try GesSchemaSet(
                parsing: """
                    schema :Drone as {
                      id: :Number integer between 0 and 255 key
                      x: :Quantity(m) between -200m and 200m step 5cm
                      heading: :Quantity(°) wrapping between 0° and 360° step 0.5°
                      active: :Boolean
                    }
                    """
            ).schema("Drone").bind()
            let (id, x, heading, active) = (schema.field("id")!, schema.field("x")!, schema.field("heading")!, schema.field("active")!)
            let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: 32, alignment: 8)
            defer { buffer.deallocate() }

            func round() throws {
                var writer = GesSchemaWriter(schema, into: buffer)
                try writer.writeInteger(id, 7)
                try writer.writeNumber(x, 12.34)
                try writer.writeNumber(heading, 90)
                try writer.writeBoolean(active, true)
                let length = try writer.finish()
                var reader = GesSchemaReader(schema, data: UnsafeRawBufferPointer(rebasing: buffer[0..<length]))
                _ = try reader.readKey()
                _ = try reader.readNumber(x)
                _ = try reader.readNumber(heading)
                _ = try reader.readBoolean(active)
                _ = try reader.finish()
            }

            try round()
            XCTAssertTrue(ges_allocation_begin())
            for _ in 0..<1000 { try round() }
            let sample = ges_allocation_end()
            XCTAssertTrue(sample.valid)
            XCTAssertEqual(sample.allocations, 0)
            XCTAssertEqual(sample.bytes, 0)
        #endif
    }

    func testBitPrimitivesAllocateNothing() throws {
        #if !canImport(Darwin)
            throw XCTSkip("The allocation counter uses libmalloc.")
        #else
            guard ges_allocation_install(), ges_allocation_controls() else { throw XCTSkip("The allocation counter is not available.") }
            let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: 64, alignment: 8)
            let text = UnsafeMutableRawBufferPointer.allocate(byteCount: 2, alignment: 8)
            let destination = UnsafeMutableRawBufferPointer.allocate(byteCount: 2, alignment: 8)
            defer {
                buffer.deallocate()
                text.deallocate()
                destination.deallocate()
            }
            text.copyBytes(from: [0x61, 0x62])
            var sink: UInt64 = 0

            func round() throws {
                var writer = GesBitWriter(into: buffer)
                try writer.write(5, width: 3)
                try writer.write(.max, width: 64)
                try writer.writeBytes(UnsafeRawBufferPointer(text))
                writer.setBit(1)
                let length = try writer.finish()
                var reader = GesBitReader(data: UnsafeRawBufferPointer(rebasing: buffer[0..<length]))
                sink &+= try reader.read(3, path: "a")
                sink &+= try reader.read(64, path: "b")
                try reader.readBytes(into: destination, path: "c")
                sink &+= UInt64(try reader.finish())
                sink &+= try GesWireRules.quantizedIndex(12.34, lowerValue: 0, upperValue: 50, tenPower: 100, numerator: 1, lower: 0, path: "x")
                sink &+= GesWireRules.isValidUTF8(UnsafeRawBufferPointer(text)) ? 1 : 0
            }

            try round()
            XCTAssertTrue(ges_allocation_begin())
            for _ in 0..<1000 { try round() }
            let sample = ges_allocation_end()
            XCTAssertTrue(sample.valid)
            XCTAssertEqual(sample.allocations, 0)
            XCTAssertEqual(sample.bytes, 0)
            XCTAssertNotEqual(sink, 0)
        #endif
    }

    func testKeyedListDeltasAllocateNothing() throws {
        #if !canImport(Darwin)
            throw XCTSkip("The allocation counter uses libmalloc.")
        #else
            guard ges_allocation_install(), ges_allocation_controls() else { throw XCTSkip("The allocation counter is not available.") }
            let schema = try GesSchemaSet(
                parsing: """
                    schema :Leaf as {
                      id: :Number integer key
                      v: :Number integer between 0 and 7
                    }

                    schema :Item as {
                      id: :Number integer between 0 and 255 key
                      hp: :Number integer between 0 and 15
                      leaves: :List(:Leaf) max 2
                    }

                    schema :S as {
                      items: :List(:Item) max 4
                    }
                    """
            ).schema("S").bind()

            func leaf(_ id: Int64, _ v: Int64) -> GesValue { .record(typeName: "Leaf", [("id", .integer(id)), ("v", .integer(v))]) }

            func item(_ id: Int64, _ hp: Int64, _ leaves: [GesValue] = []) -> GesValue { .record(typeName: "Item", [("id", .integer(id)), ("hp", .integer(hp)), ("leaves", .list(leaves))]) }

            func encoded(_ items: [GesValue]) throws -> [UInt8] { try GesSchemaValues.encode(schema, .record(typeName: "S", [("items", .list(items))])) }

            let first = try encoded([item(1, 2, [leaf(-1, 1), leaf(4, 2)]), item(2, 3), item(5, 4)])
            let second = try encoded([item(1, 2, [leaf(-1, 3), leaf(4, 2)]), item(3, 7, [leaf(1, 1)]), item(5, 9)])
            let delta = UnsafeMutableRawBufferPointer.allocate(byteCount: schema.maxByteCount, alignment: 8)
            let record = UnsafeMutableRawBufferPointer.allocate(byteCount: schema.maxByteCount, alignment: 8)
            defer {
                delta.deallocate()
                record.deallocate()
            }

            func round() throws {
                try first.withUnsafeBytes { baseline in
                    try second.withUnsafeBytes { target in
                        let length = try GesSchemaDelta.encode(schema, record: target, baseline: baseline, into: delta)
                        let decoded = try GesSchemaDelta.decode(schema, delta: UnsafeRawBufferPointer(rebasing: UnsafeRawBufferPointer(delta)[0..<length]), baseline: baseline, into: record)
                        if decoded.byteCount != second.count { XCTFail("The record has the wrong size") }
                    }
                }
            }

            try round()
            XCTAssertEqual(Array(record[0..<second.count]), second)
            XCTAssertTrue(ges_allocation_begin())
            for _ in 0..<1000 { try round() }
            let sample = ges_allocation_end()
            XCTAssertTrue(sample.valid)
            XCTAssertEqual(sample.allocations, 0)
            XCTAssertEqual(sample.bytes, 0)
        #endif
    }

    func testDeltasAllocateNothing() throws {
        #if !canImport(Darwin)
            throw XCTSkip("The allocation counter uses libmalloc.")
        #else
            guard ges_allocation_install(), ges_allocation_controls() else { throw XCTSkip("The allocation counter is not available.") }
            let schema = try GesSchemaSet(
                parsing: """
                    schema :Part as {
                      v: :Number integer between 0 and 3
                      on: :Boolean
                    }

                    schema :S as {
                      id: :Number integer between 0 and 255 key
                      x: :Quantity(m) between 0m and 100m step 1cm delta within 1m
                      name: :Text max 4 optional
                      items: :List(:Text max 2) max 2
                      part: :Part
                      pick: :Boolean | :Part
                    }
                    """
            ).schema("S").bind()

            func sample(_ x: Int64, _ name: String) -> GesValue {
                let part = GesValue.record(typeName: "Part", keys: ["v", "on"], values: [.integer(x), .boolean(x == 1)])
                return GesValue.record(
                    typeName: "S",
                    keys: ["id", "x", "name", "items", "part", "pick"],
                    values: [.integer(7), .float(Double(x) * 40, unit: .meter), .text(name), .list([.text("a"), .text(String(name.prefix(1)))]), part, part]
                )
            }

            let first = try GesSchemaValues.encode(schema, sample(1, "ab"))
            let second = try GesSchemaValues.encode(schema, sample(2, "aä"))
            let delta = UnsafeMutableRawBufferPointer.allocate(byteCount: schema.maxByteCount, alignment: 8)
            let record = UnsafeMutableRawBufferPointer.allocate(byteCount: schema.maxByteCount, alignment: 8)
            defer {
                delta.deallocate()
                record.deallocate()
            }
            try first.withUnsafeBytes { baseline in
                try second.withUnsafeBytes { current in
                    func round() throws {
                        let length = try GesSchemaDelta.encode(schema, record: current, baseline: baseline, into: delta)
                        let decoded = try GesSchemaDelta.decode(schema, delta: UnsafeRawBufferPointer(rebasing: delta[0..<length]), baseline: baseline, into: record)
                        if decoded.byteCount != current.count { throw Mismatch() }
                    }

                    try round()
                    XCTAssertEqual(Array(record[0..<current.count]), second)
                    XCTAssertTrue(ges_allocation_begin())
                    for _ in 0..<1000 { try round() }
                    let sample = ges_allocation_end()
                    XCTAssertTrue(sample.valid)
                    XCTAssertEqual(sample.allocations, 0)
                    XCTAssertEqual(sample.bytes, 0)
                }
            }
        #endif
    }

    func testUnionsAndDiceAllocateNothing() throws {
        #if !canImport(Darwin)
            throw XCTSkip("The allocation counter uses libmalloc.")
        #else
            guard ges_allocation_install(), ges_allocation_controls() else { throw XCTSkip("The allocation counter is not available.") }
            let schema = try GesSchemaSet(parsing: "schema :S as { v: :Quantity(°) wrapping between 0° and 360° step 1° | :Tag(#a, #b) | :Nothing, next: :Boolean, d: :Dice max 2 between 1 and 6, n: :Tag max 3 }").schema("S").bind()
            let (v, tag, next, d, roll) = (schema.field("v")!, schema.field("v:Tag")!, schema.field("next")!, schema.field("d")!, schema.field("d[]")!)
            let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: 32, alignment: 8)
            defer { buffer.deallocate() }
            let name = schema.field("n")!
            let tagName = UnsafeMutableRawBufferPointer.allocate(byteCount: 2, alignment: 1)
            defer { tagName.deallocate() }
            tagName[0] = 0x61
            tagName[1] = 0x62
            let received = UnsafeMutableRawBufferPointer.allocate(byteCount: 3, alignment: 1)
            defer { received.deallocate() }

            func round() throws {
                var writer = GesSchemaWriter(schema, into: buffer)
                try writer.selectAlternative(v, 1)
                try writer.writeTag(tag, 1)
                try writer.writeBoolean(next, true)
                try writer.beginList(d, count: 1)
                try writer.writeInteger(roll, 4)
                try writer.writeText(name, UnsafeRawBufferPointer(tagName))
                var length = try writer.finish()
                var reader = GesSchemaReader(schema, data: UnsafeRawBufferPointer(rebasing: buffer[0..<length]))
                if try reader.readAlternative(v) != 1 { throw Mismatch() }
                if try reader.readTag(tag) != 1 { throw Mismatch() }
                if try !reader.readBoolean(next) { throw Mismatch() }
                if try reader.beginList(d) != 1 { throw Mismatch() }
                if try reader.readInteger(roll) != 4 { throw Mismatch() }
                if try reader.readText(name, into: received) != 2 { throw Mismatch() }
                _ = try reader.finish()

                writer = GesSchemaWriter(schema, into: buffer)
                try writer.selectAlternative(v, 2)
                try writer.writeBoolean(next, false)
                try writer.beginList(d, count: 0)
                try writer.writeText(name, UnsafeRawBufferPointer(tagName))
                length = try writer.finish()
                reader = GesSchemaReader(schema, data: UnsafeRawBufferPointer(rebasing: buffer[0..<length]))
                if try reader.readAlternative(v) != 2 { throw Mismatch() }
                if try reader.readBoolean(next) { throw Mismatch() }
                if try reader.beginList(d) != 0 { throw Mismatch() }
                if try reader.readText(name, into: received) != 2 { throw Mismatch() }
                _ = try reader.finish()
            }

            try round()
            XCTAssertTrue(ges_allocation_begin())
            for _ in 0..<1000 { try round() }
            let sample = ges_allocation_end()
            XCTAssertTrue(sample.valid)
            XCTAssertEqual(sample.allocations, 0)
            XCTAssertEqual(sample.bytes, 0)
        #endif
    }

    func testDecodingAllocatesOnlyWhatTextsHold() throws {
        #if !canImport(Darwin)
            throw XCTSkip("The allocation counter uses libmalloc.")
        #else
            guard ges_allocation_install(), ges_allocation_controls() else { throw XCTSkip("The allocation counter is unavailable.") }
            let schema = try GesSchemaSet(parsing: "schema :S as { items: :List(:Text max 1000) max 1000 }").schema("S").bind()
            let items = [GesValue](repeating: .text(""), count: 1000)
            let data = try GesSchemaValues.encode(schema, .record(typeName: "S", keys: ["items"], values: [.list(items)]), bufferSize: 2048)
            XCTAssertTrue(ges_allocation_begin())
            _ = try GesSchemaValues.decode(schema, data)
            let sample = ges_allocation_end()
            XCTAssertTrue(sample.valid)
            XCTAssertLessThan(sample.bytes, 200_000)
        #endif
    }
}
