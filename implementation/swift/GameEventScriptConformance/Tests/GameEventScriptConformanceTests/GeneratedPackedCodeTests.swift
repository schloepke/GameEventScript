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
            rows: [PackedModel.Grid_Row(index: 3, cells: [cell])],
            offset: PackedModel.Offset(delta: -3, gear: 12, lift: 0.5, kind: .high)
        )
    }

    func testEveryPackedFieldLandsAtItsBits() {
        // delta -3 is index 5 at bit 0, gear 12 index 2 at bit 4, lift 0.5m index 3 at bit 6, high index 2 at bit 9.
        let expected = UInt16(5 | (2 << 4) | (3 << 6) | (2 << 9))
        XCTAssertEqual(expected, PackedModel.Offset(delta: -3, gear: 12, lift: 0.5, kind: .high).rawValue)
        var offset = PackedModel.Offset(delta: 7, gear: 13, lift: 1, kind: .low)
        offset.delta = -3
        offset.gear = 12
        offset.lift = 0.5
        offset.kind = .high
        XCTAssertEqual(expected, offset.rawValue)
        XCTAssertEqual(-3, offset.delta)
        XCTAssertEqual(12, offset.gear)
        XCTAssertEqual(0.5, offset.lift)
        XCTAssertEqual(.high, offset.kind)
        var lowest = offset
        lowest.delta = -8
        lowest.gear = 10
        lowest.lift = -1
        lowest.kind = .low
        XCTAssertEqual(0, lowest.rawValue)
    }

    func testGlobalModelsConvertBesideNamespacedWire() throws {
        let cell = Cell(level: 7, open: true, kind: .water, slope: 0.3, marks: .held)
        let model = Grid(name: "grid", cells: [cell], origin: cell, depth: 1, offset: Offset(delta: 1, gear: 11, lift: -0.5, kind: .mid))
        let wire = PackedGlobal.Grid()
        wire.read(model: model)
        let back = PackedGlobal.Grid()
        try back.read(from: try wire.encoded())
        XCTAssertEqual(model, Grid(wire: back))
        XCTAssertEqual([UInt8(truncatingIfNeeded: cell.rawValue), UInt8(cell.rawValue >> 8)], try PackedGlobal.CellWire.encoded(cell))
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

    /// A packed root that the models declare gets its wire members as the enum `CellWire` of its wire namespace, so that
    /// another wire generation of the module may share the model type too.
    func testSharedPackedRootsWriteThroughTheirWireEnum() throws {
        let cell = PackedModel.Cell(level: 9, open: true, kind: .sand, slope: 1, marks: .seen)
        let bytes = try PackedWire.CellWire.encoded(cell)
        XCTAssertEqual(PackedWire.CellWire.maxByteCount, bytes.count)
        XCTAssertEqual([UInt8(truncatingIfNeeded: cell.rawValue), UInt8(cell.rawValue >> 8)], bytes)
        var read = PackedModel.Cell(uncheckedRawValue: 0)
        try PackedWire.CellWire.read(&read, from: bytes)
        XCTAssertEqual(cell, read)
        var copied = PackedModel.Cell(uncheckedRawValue: 0)
        PackedWire.CellWire.copy(&copied, from: cell)
        PackedWire.CellWire.quantize(copied)
        XCTAssertTrue(PackedWire.CellWire.wireEquals(cell, copied))
        XCTAssertEqual([0], try PackedWire.CellWire.deltaEncoded(cell, against: cell))
        var changed = cell
        changed.level = 1
        var target = PackedModel.Cell(uncheckedRawValue: 0)
        try PackedWire.CellWire.readDelta(&target, from: try PackedWire.CellWire.deltaEncoded(changed, against: cell), against: cell)
        XCTAssertEqual(changed, target)
        // The global models share Cell with two wire namespaces, each with its own enum.
        XCTAssertEqual(bytes, try PackedGlobal.CellWire.encoded(Cell(level: 9, open: true, kind: .sand, slope: 1, marks: .seen)))
        XCTAssertThrowsError(try PackedWire.CellWire.encoded(PackedModel.Cell(uncheckedRawValue: 1 << 14))) { error in
            XCTAssertEqual(GesWireError.outOfRange, (error as? GesWireError)?.code)
        }
    }
}
