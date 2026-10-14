// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import XCTest

@testable import GameEventScriptRuntime

/// Ownership and relocation controls for the manually allocated register file.
final class RegisterStorageTests: XCTestCase {
    func testRepeatedGrowthPreservesReferencesAndInitializesNewSlots() throws {
        let state = GesVirtualMachine(maxRegisters: 512, maxCallDepth: 8)
        let values: [GesValue] = [.text("retained text 😀"), .list([.text("nested")]), .map([.init(key: "key", value: .text("value"))])]
        for (index, value) in values.enumerated() { state.setValue(index, value) }
        let iterator = try XCTUnwrap(GesIterator(.list([.integer(7), .integer(9)])))
        state.setIterator(3, iterator)
        for required in [65, 193, 511] {
            let previous = state.registers.count
            XCTAssertTrue(state.ensure(required))
            XCTAssertGreaterThan(state.registers.count, previous)
            for index in previous..<state.registers.count { XCTAssertTrue(state.slot(index).isNothing) }
            for (index, value) in values.enumerated() { XCTAssertEqual(state.value(index), value) }
            XCTAssertTrue(state.iterator(3) === iterator)
            state.setText(required - 1, "new slot")
            XCTAssertEqual(state.value(required - 1), .text("new slot"))
            state.setInteger(4, Int64(required))
            XCTAssertEqual(state.value(4), .integer(Int64(required)))
        }
        XCTAssertEqual(state.iterator(3)?.next(), .integer(7))
        XCTAssertEqual(state.iterator(3)?.next(), .integer(9))
    }

    func testResetAfterGrowthReleasesObjectsAndAllowsReuse() throws {
        let state = GesVirtualMachine(maxRegisters: 512, maxCallDepth: 8)
        weak var released: GesIterator?
        weak var releasedHighSlot: GesIterator?
        do {
            let iterator = try XCTUnwrap(GesIterator(.list([.text("owned")])))
            released = iterator
            state.setIterator(0, iterator)
        }
        XCTAssertTrue(state.ensure(129))
        do {
            let iterator = try XCTUnwrap(GesIterator(.list([.text("high slot")])))
            releasedHighSlot = iterator
            state.setIterator(128, iterator)
        }
        XCTAssertNotNil(releasedHighSlot)
        XCTAssertNotNil(released)
        state.reset()
        XCTAssertNil(released)
        XCTAssertNil(releasedHighSlot)
        for index in 0..<state.registers.count { XCTAssertTrue(state.slot(index).isNothing) }
        state.frameLength = 1
        state.setText(0, "reused")
        state.stageInteger(42)
        XCTAssertEqual(state.value(0), .text("reused"))
        XCTAssertEqual(state.staged(0), .integer(42))
        state.reset()
        XCTAssertTrue(state.slot(0).isNothing)
        XCTAssertTrue(state.slot(1).isNothing)
    }

    func testDeinitAfterGrowthReleasesObjectsWithoutReset() throws {
        weak var released: GesIterator?
        weak var releasedHighSlot: GesIterator?
        do {
            let state = GesVirtualMachine(maxRegisters: 512, maxCallDepth: 8)
            do {
                let iterator = try XCTUnwrap(GesIterator(.list([.text("owned")])))
                released = iterator
                state.setIterator(0, iterator)
            }
            XCTAssertTrue(state.ensure(65))
            XCTAssertTrue(state.ensure(257))
            do {
                let iterator = try XCTUnwrap(GesIterator(.list([.text("high slot")])))
                releasedHighSlot = iterator
                state.setIterator(256, iterator)
            }
            XCTAssertNotNil(releasedHighSlot)
            XCTAssertNotNil(released)
            withExtendedLifetime(state) {}
        }
        XCTAssertNil(released)
        XCTAssertNil(releasedHighSlot)
    }

    func testDynamicParsingGrowsBeyondPrewarmedProgramCapacity() throws {
        let fields = (1...40).map { "_ f\($0): :Number" }.joined(separator: ", ")
        let arguments = (1...40).map(String.init).joined(separator: ", ")
        let source = """
            record :Wide as { \(fields) }
            record :Outer as { \(fields), _ text: :Text, child: :Wide computed by parse text }
            on Start { let value be parse ':Outer(\(arguments), ":Wide(\(arguments))")' }
            """
        let program = try GesProgramBuilder().addScript(source).compile()
        let linked = try GesLinkedProgram(program, extensions: nil, types: nil)
        let entry = try XCTUnwrap(program.bindings.first { program.stringConstants[Int($0.name)] == "Start" }).entryAddress
        for budget in [1, 10000] {
            let host = try GameEventScriptHost.createBuilder().build()
            let context = GesContext(host: host, random: .init(), limits: .init(), extensions: nil, observer: nil)
            let state = GesVirtualMachine(maxRegisters: 512, maxCallDepth: 64)
            state.prepareCapacity(Int(program.requiredRegisterCount))
            let before = state.registers.count
            try state.begin(linked: linked, message: .init(name: "Start"), matchArguments: true, entry: Int(entry), signature: "Start()")
            var slices = 0
            while state.processing && slices < 10000 {
                _ = state.runSlice(context: context, budget: budget)
                slices += 1
            }
            XCTAssertFalse(state.processing)
            XCTAssertNil(state.error)
            XCTAssertGreaterThan(state.registers.count, before)
        }
    }
}
