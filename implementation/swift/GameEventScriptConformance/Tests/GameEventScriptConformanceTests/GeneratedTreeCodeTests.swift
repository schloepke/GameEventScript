// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/tree.gess: recursive Lists, whose records take their level and
/// fail with the depth they exceed, in wire code that agrees with the value path of the runtime.
final class GeneratedTreeCodeTests: XCTestCase {
    private func bind(_ name: String, _ units: TreeWire.Bind...) -> TreeWire.Bind {
        let bind = TreeWire.Bind()
        bind.name = name
        bind.units = units
        return bind
    }

    private func unit(_ id: Int64, _ units: TreeWire.Unit...) -> TreeWire.Unit {
        let unit = TreeWire.Unit()
        unit.id = id
        unit.units = units
        return unit
    }

    private func node(_ tag: String, _ kids: TreeWire.Node...) -> TreeWire.Node {
        let node = TreeWire.Node()
        node.tag = tag
        node.kids = kids
        return node
    }

    /// Every tree as deep as its depth: three binds, three units and, below the leaf, three nodes.
    private func full() -> TreeWire.Loadout {
        let loadout = TreeWire.Loadout()
        loadout.root = bind("a", bind("b", bind("c")), bind("d"))
        loadout.team = unit(1, unit(2, unit(3)), unit(4))
        loadout.leaf.tag = "l"
        loadout.leaf.name = "n"
        loadout.leaf.kids = [node("x", node("y", node("z")))]
        return loadout
    }

    private func assertTooDeep(_ path: String, file: StaticString = #filePath, line: UInt = #line, _ action: () throws -> Void) {
        XCTAssertThrowsError(try action(), file: file, line: line) { error in
            let wire = error as? GesWireError
            XCTAssertEqual(wire?.code, GesWireError.tooDeep, file: file, line: line)
            XCTAssertEqual(wire?.path, path, file: file, line: line)
            XCTAssertEqual(wire?.detail, "deeper than depth 3", file: file, line: line)
        }
    }

    func testTreesAsDeepAsTheirDepthRoundTripLikeTheValuePath() throws {
        let loadout = full()
        let bytes = try loadout.encoded()
        let read = TreeWire.Loadout()
        XCTAssertEqual(try read.read(from: bytes), bytes.count)
        XCTAssertTrue(try read.wireEquals(loadout))
        XCTAssertEqual(read.root.units[0].units[0].name, "c")
        XCTAssertEqual(read.team.units[0].units[0].id, 3)
        XCTAssertEqual(read.leaf.kids[0].kids[0].kids[0].tag, "z")

        // The value path of the runtime writes the same bytes and reads the same tree.
        let again = TreeWire.Loadout()
        try again.read(value: try loadout.toValue())
        XCTAssertEqual(try again.encoded(), bytes)
        try read.quantize()
        XCTAssertEqual(try read.encoded(), bytes)
    }

    func testARecordBelowTheDepthNamesTheDepth() throws {
        let loadout = full()
        loadout.root.units[0].units[0].units.append(bind("e"))
        assertTooDeep("root.units[].units[].units") { _ = try loadout.encoded() }
        assertTooDeep("root.units[].units[].units") { _ = try loadout.encodedByteCount() }
        assertTooDeep("root.units[].units[].units") { try loadout.quantize() }
        assertTooDeep("root.units[].units[].units") { _ = try loadout.wireEquals(self.full()) }
        assertTooDeep("root.units[].units[].units") { _ = try loadout.deltaEncoded(against: self.full()) }
        assertTooDeep("root.units[].units[].units") { _ = try loadout.toValue() }

        // Where `with` copies the recursive List, three records of the declaring schema nest below the copy.
        let leaf = full()
        leaf.leaf.kids[0].kids[0].kids[0].kids.append(node("w"))
        assertTooDeep("leaf.kids[].kids[].kids[].kids") { _ = try leaf.encoded() }

        // A keyed tree is no keyed List at its depth either.
        let team = full()
        team.team.units[0].units[0].units.append(unit(9))
        assertTooDeep("team.units[].units[].units") { _ = try team.deltaEncoded(against: self.full()) }
    }

    func testDeltasDescendThroughEveryLevel() throws {
        let baseline = full()
        let changed = full()
        changed.root.units[0].units[0].name = "e"
        changed.team.units[0].units.append(unit(5))
        changed.leaf.kids[0].kids[0].kids[0].tag = "q"
        let delta = try changed.deltaEncoded(against: baseline)
        let read = TreeWire.Loadout()
        XCTAssertEqual(try read.readDelta(from: delta, against: baseline), delta.count)
        XCTAssertTrue(try read.wireEquals(changed))
        let unchanged = try full().deltaEncoded(against: baseline)
        XCTAssertEqual(try read.readDelta(from: unchanged, against: baseline), unchanged.count)
        XCTAssertTrue(try read.wireEquals(baseline))
    }

    func testModelsHoldTheirTreeAndConvertThroughTheValuePath() throws {
        let model = try TreeModel.Loadout(value: try full().toValue())
        XCTAssertEqual(model.root.units[0].units[0].name, "c")
        XCTAssertEqual(model.team.units[0].units[0].id, 3)
        let wire = TreeWire.Loadout()
        try wire.read(value: model.toValue())
        XCTAssertEqual(try wire.encoded(), try full().encoded())
    }
}
