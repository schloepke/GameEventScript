// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/packed.gess: packed records as structs over one word, shared
/// between models and wire code that `--models` names, and the direct conversion between wire records and models.
final class GeneratedPackedCodeTests: XCTestCase {
    private func makeGrid() -> PackedModel.Grid {
        let cell = PackedModel.Cell(level: 7, open: true, kind: .water, slope: 0.3, marks: .held)
        var second = cell
        second.level = 2
        second.open = false
        var sand = cell
        sand.kind = .sand
        var origin = cell
        origin.slope = 0.9
        return PackedModel.Grid(
            name: "grid",
            cells: [cell, second, PackedModel.Cell(level: 9, open: false, kind: .rock, slope: 1, marks: [.seen, .held])],
            pairs: [PackedModel.Pair(low: cell, high: sand)],
            origin: origin,
            note: "note",
            reading: .code("ab"),
            labels: ["a", "bc"],
            depth: 2.5,
            rows: [PackedModel.Grid_Row(index: 3, cells: [cell])]
        )
    }

    func testPackedRecordIsOneWordWithCheckedFields() {
        let cell = PackedModel.Cell(level: 7, open: true, kind: .water, slope: 0.34, marks: .held)
        // level 7 at bit 0, open at 4, water (2) at 5, slope 3 tenths at 7, held (2) at 11.
        XCTAssertEqual(UInt16(7 | (1 << 4) | (2 << 5) | (3 << 7) | (2 << 11)), cell.rawValue)
        XCTAssertEqual(0.3, cell.slope, accuracy: 1e-12)
        XCTAssertEqual(MemoryLayout<UInt16>.stride, MemoryLayout<PackedModel.Cell>.stride)
        XCTAssertEqual(cell, PackedModel.Cell(rawValue: cell.rawValue))
        XCTAssertNil(PackedModel.Cell(rawValue: 15))
        XCTAssertNil(PackedModel.Cell(rawValue: 3 << 5))
        XCTAssertNil(PackedModel.Cell(rawValue: 1 << 13))
        XCTAssertEqual(15, PackedModel.Cell(uncheckedRawValue: 15).rawValue)
        var high = cell
        high.level = 1
        let pair = PackedModel.Pair(low: cell, high: high)
        XCTAssertEqual(UInt32(cell.rawValue) | (UInt32(high.rawValue) << 13), pair.rawValue)
        XCTAssertEqual(high, pair.high)
    }

    func testWireRecordsConvertToModelsDirectly() throws {
        let model = makeGrid()
        let wire = PackedWire.Grid()
        wire.read(model: model)
        let bytes = try wire.encoded()
        XCTAssertEqual(model.toValue(), try wire.toValue())
        let back = PackedWire.Grid()
        try back.read(from: bytes)
        XCTAssertEqual(model, PackedModel.Grid(wire: back))
        // Wire code that declares its own packed types writes the same bytes.
        let own = PackedOwn.Grid()
        try own.read(from: bytes)
        XCTAssertEqual(bytes, try own.encoded())
    }

    func testListsOfPackedRecordsCheckEachWord() throws {
        let wire = PackedWire.Grid()
        wire.cells = [PackedModel.Cell(level: 0, open: false, kind: .rock, slope: 0, marks: [])]
        var bytes = try wire.encoded()
        // An empty name takes 5 bits and the count 7, so the first cell's level starts at bit 12.
        bytes[1] |= 0xf0
        XCTAssertThrowsError(try PackedWire.Grid().read(from: bytes)) { error in
            XCTAssertEqual(GesWireError.invalidIndex, (error as? GesWireError)?.code)
            XCTAssertEqual("cells[].level", (error as? GesWireError)?.path)
        }
        wire.cells[0] = PackedModel.Cell(uncheckedRawValue: 15)
        XCTAssertThrowsError(try wire.encoded()) { error in
            XCTAssertEqual(GesWireError.outOfRange, (error as? GesWireError)?.code)
            XCTAssertEqual("cells[].level", (error as? GesWireError)?.path)
        }
    }

    func testSharedPackedRootsWriteThroughExtensions() throws {
        let cell = PackedModel.Cell(level: 9, open: true, kind: .sand, slope: 1, marks: .seen)
        let bytes = try cell.encoded()
        XCTAssertEqual(PackedModel.Cell.maxByteCount, bytes.count)
        XCTAssertEqual([UInt8(truncatingIfNeeded: cell.rawValue), UInt8(cell.rawValue >> 8)], bytes)
        var read = PackedModel.Cell()
        try read.read(from: bytes)
        XCTAssertEqual(cell, read)
        XCTAssertEqual([0], try cell.deltaEncoded(against: cell))
        var changed = cell
        changed.level = 1
        var target = PackedModel.Cell()
        try target.readDelta(from: try changed.deltaEncoded(against: cell), against: cell)
        XCTAssertEqual(changed, target)
        XCTAssertThrowsError(try PackedModel.Cell(uncheckedRawValue: 1 << 14).encoded()) { error in
            XCTAssertEqual(GesWireError.outOfRange, (error as? GesWireError)?.code)
        }
    }
}
