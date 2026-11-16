// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Checks the public bit primitives and wire rules that generated schema code builds on.
final class BitTests: XCTestCase {
    func testACountingWriterCountsTheBytesOfTheSameWrites() throws {
        try withBuffer(96) { buffer in
            var writer = GesBitWriter(into: buffer)
            var counting = GesBitWriter.counting()
            for index in 0..<40 {
                try writer.write(UInt64(index), width: 11)
                try counting.write(UInt64(index), width: 11)
            }
            writer.setBit(3)
            counting.setBit(3)
            writer.rewind(to: 10)
            counting.rewind(to: 10)
            try writer.write(5, width: 3)
            try counting.write(5, width: 3)
            let bytes = UnsafeMutableRawBufferPointer.allocate(byteCount: 13, alignment: 1)
            defer { bytes.deallocate() }
            bytes.initializeMemory(as: UInt8.self, repeating: 7)
            try writer.writeBytes(UnsafeRawBufferPointer(bytes))
            try counting.writeBytes(UnsafeRawBufferPointer(bytes))
            XCTAssertEqual(writer.bitPosition, counting.bitPosition)
            XCTAssertEqual(try writer.finish(), try counting.finish())
            XCTAssertEqual(writer.requiredByteCount, 0)
            XCTAssertEqual(counting.requiredByteCount, 48, "Bits that a rewind took back still needed room.")
        }
    }

    func testEveryWidthRoundTripsAcrossWordBoundaries() throws {
        var generator = SystemRandomNumberGenerator()
        let widths = (0..<400).map { $0 % 65 }
        let values = widths.map { width -> UInt64 in
            let bits = generator.next() as UInt64
            return width == 64 ? bits : bits & ((1 << UInt64(width)) - 1)
        }
        try withBuffer(2048) { buffer in
            var writer = GesBitWriter(into: buffer)
            var total = 0
            for (width, value) in zip(widths, values) {
                try writer.write(value, width: width)
                total += width
                XCTAssertEqual(writer.bitPosition, total)
            }
            let length = try writer.finish()
            XCTAssertEqual(length, (total + 7) / 8)
            var reader = GesBitReader(data: UnsafeRawBufferPointer(rebasing: buffer[0..<length]))
            for (width, value) in zip(widths, values) { XCTAssertEqual(try reader.read(width, path: "f"), value) }
            XCTAssertEqual(try reader.finish(), length)
        }
    }

    func testBitsAreLeastSignificantFirstInLittleEndianBytes() throws {
        try withBuffer(3) { buffer in
            var writer = GesBitWriter(into: buffer)
            try writer.write(0b101, width: 3)
            try writer.write(0x1FF, width: 9)
            try [UInt8(0xAB)].withUnsafeBytes { bytes in try writer.writeBytes(bytes) }
            XCTAssertEqual(try writer.finish(), 3)
            XCTAssertEqual(Array(buffer), [0b1111_1101, 0b1011_1111, 0b0000_1010])
        }
    }

    func testSetBitAndRewindReachFlushedWords() throws {
        try withBuffer(16) { buffer in
            var writer = GesBitWriter(into: buffer)
            try writer.write(0, width: 3)
            try writer.write(.max, width: 64)
            try writer.write(0, width: 5)
            writer.setBit(1)
            writer.setBit(70)
            XCTAssertEqual(writer.bitPosition, 72)
            writer.rewind(to: 66)
            try writer.write(0b10, width: 2)
            XCTAssertEqual(writer.bitPosition, 68)
            XCTAssertEqual(try writer.finish(), 9)

            var reader = GesBitReader(data: UnsafeRawBufferPointer(rebasing: buffer[0..<9]))
            XCTAssertEqual(try reader.read(3, path: ""), 0b010)
            XCTAssertEqual(try reader.read(63, path: ""), UInt64.max >> 1)
            XCTAssertEqual(try reader.read(2, path: ""), 0b10)
            reader.seek(to: 3)
            XCTAssertEqual(try reader.read(1, path: ""), 1)

            var again = GesBitWriter(into: buffer)
            try again.write(.max, width: 64)
            try again.write(1, width: 6)
            again.rewind(to: 60)
            try again.write(0, width: 4)
            XCTAssertEqual(again.bitPosition, 64)
            XCTAssertEqual(try again.finish(), 8)
            var whole = GesBitReader(data: UnsafeRawBufferPointer(rebasing: buffer[0..<8]))
            XCTAssertEqual(try whole.read(64, path: ""), UInt64.max >> 4)
        }
    }

    func testAFullBufferFailsWithoutWritingPastIt() throws {
        try withBuffer(1) { buffer in
            assertWire(GesWireError.bufferTooSmall, "") {
                var writer = GesBitWriter(into: buffer)
                try writer.write(0x3FF, width: 10)
                _ = try writer.finish()
            }
        }
        try withBuffer(7) { buffer in
            assertWire(GesWireError.bufferTooSmall, "") {
                var writer = GesBitWriter(into: buffer)
                try writer.write(.max, width: 64)
            }
        }
    }

    func testTheEndOfDataNamesTheFieldAndTheLimit() throws {
        try [UInt8(0xFF), 0x00, 0x01].withUnsafeBytes { data in
            assertWire(GesWireError.truncated, "a.b") {
                var reader = GesBitReader(data: data)
                _ = try reader.read(25, path: "a.b")
            }
            assertWire(GesWireError.limitExceeded, "c") {
                var reader = GesBitReader(data: data, byteLimit: 2)
                _ = try reader.read(17, path: "c")
            }
            assertWire(GesWireError.truncated, "d") { try GesBitReader(data: data).require(25, path: "d") }
            assertWire(GesWireError.invalidPadding, "") {
                var reader = GesBitReader(data: data)
                _ = try reader.read(1, path: "")
                _ = try reader.finish()
            }
            try withBuffer(4) { destination in
                assertWire(GesWireError.truncated, "e") {
                    var reader = GesBitReader(data: data)
                    try reader.readBytes(into: destination, path: "e")
                }
                XCTAssertEqual(Array(destination), [0, 0, 0, 0])
            }
            var whole = GesBitReader(data: data, byteLimit: 3)
            XCTAssertEqual(try whole.read(8, path: ""), 0xFF)
            try whole.require(16, path: "")
            XCTAssertEqual(whole.bitPosition, 8)
        }
    }

    func testQuantizationFollowsTheSpecificationVectors() throws {
        // Step 1cm (p = 1, k = 2) in [0m, 5000m].
        func index(_ value: Double, _ numerator: Double = 1, _ lower: Int64 = 0, _ upper: Int64 = 500_000) throws(GesWireError) -> UInt64 {
            try GesWireRules.quantizedIndex(
                value,
                lowerValue: Double(lower) * numerator / 100,
                upperValue: Double(upper) * numerator / 100,
                tenPower: 100,
                numerator: numerator,
                lower: lower,
                path: "x"
            )
        }

        XCTAssertEqual(try index(1234.565), 123_456)
        XCTAssertEqual(try index(0.135), 14)
        XCTAssertEqual(try index(1.005), 100)
        XCTAssertEqual(try index(0.300_000_000_000_000_04), 30)
        XCTAssertEqual(try index(4999.996), 500_000)
        XCTAssertEqual(try index(12.34, 5, 0, 100_000), 247)
        XCTAssertEqual(GesWireRules.quantizedValue(123_456, lower: 0, numerator: 1, tenPower: 100), 1234.56)
        XCTAssertEqual(GesWireRules.quantizedValue(247, lower: 0, numerator: 5, tenPower: 100), 12.35)
        assertWire(GesWireError.outOfRange, "x") { _ = try index(5000.004) }
        assertWire(GesWireError.outOfRange, "x") { _ = try index(-0.004) }
        assertWire(GesWireError.outOfRange, "x") { _ = try index(.nan) }

        // 0.1° in a period of 360°, with a lower bound of 0.
        XCTAssertEqual(try GesWireRules.wrappedQuantizedIndex(-90, tenPower: 10, numerator: 1, lower: 0, period: 3600, path: "h"), 2700)
        XCTAssertEqual(try GesWireRules.wrappedQuantizedIndex(359.95, tenPower: 10, numerator: 1, lower: 0, period: 3600, path: "h"), 0)
        assertWire(GesWireError.outOfRange, "h") { _ = try GesWireRules.wrappedQuantizedIndex(.infinity, tenPower: 10, numerator: 1, lower: 0, period: 3600, path: "h") }
        assertWire(GesWireError.outOfRange, "h") { _ = try GesWireRules.wrappedQuantizedIndex(1e15, tenPower: 10, numerator: 1, lower: 0, period: 3600, path: "h") }
        XCTAssertEqual(GesWireRules.wrappedIndex(-1, lower: 0, period: 360), 359)
        XCTAssertEqual(GesWireRules.wrappedIndex(-360, lower: 0, period: 360), 0)
        XCTAssertEqual(GesWireRules.wrappedIndex(.max, lower: .max - 5, period: 10), 5)
    }

    func testTextAndTagRulesWorkOnBytes() {
        func valid(_ bytes: [UInt8]) -> Bool { bytes.withUnsafeBytes { GesWireRules.isValidUTF8($0) } }

        func tag(_ text: String) -> Bool { Array(text.utf8).withUnsafeBytes { GesWireRules.isTagName($0) } }

        XCTAssertTrue(valid(Array("aä€😀".utf8)))
        XCTAssertTrue(valid([]))
        XCTAssertFalse(valid([0xC0, 0x80]))
        XCTAssertFalse(valid([0xED, 0xA0, 0x80]))
        XCTAssertFalse(valid([0xE2, 0x82]))
        XCTAssertFalse(valid([0xF4, 0x90, 0x80, 0x80]))
        XCTAssertTrue(tag("a1B"))
        XCTAssertFalse(tag("A1"))
        XCTAssertFalse(tag("a_b"))
        XCTAssertFalse(tag(""))
    }

    func testWireErrorsCanBeCreatedWithTheirCodes() {
        let error = GesWireError(code: GesWireError.invalidDelta, path: "bots.x")
        XCTAssertEqual(error.code, "wire.invalidDelta")
        XCTAssertEqual(error.path, "bots.x")
    }

    private func withBuffer(_ count: Int, _ body: (UnsafeMutableRawBufferPointer) throws -> Void) throws {
        let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: count, alignment: 8)
        defer { buffer.deallocate() }
        buffer.initializeMemory(as: UInt8.self, repeating: 0)
        try body(buffer)
    }

    private func assertWire(_ code: String, _ path: String, file: StaticString = #filePath, line: UInt = #line, _ body: () throws -> Void) {
        do {
            try body()
            XCTFail("Expected \(code).", file: file, line: line)
        } catch let error as GesWireError {
            XCTAssertEqual(error.code, code, file: file, line: line)
            XCTAssertEqual(error.path, path, file: file, line: line)
        } catch {
            XCTFail("Unexpected \(error).", file: file, line: line)
        }
    }
}
