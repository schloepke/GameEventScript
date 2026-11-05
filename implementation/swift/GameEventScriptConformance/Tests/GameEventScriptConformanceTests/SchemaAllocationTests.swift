// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import ConformanceInstrumentation
import GameEventScriptRuntime
import XCTest

/// The writer and reader allocate nothing on success.
final class SchemaAllocationTests: XCTestCase {
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
