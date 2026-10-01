// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import XCTest

@testable import GameEventScriptRuntime

/// Native storage controls complement the shared arithmetic semantics corpus.
final class RegisterArithmeticTests: XCTestCase {
    func testIntegerResultsCanOverwriteEitherOperandAfterRegisterGrowth() {
        let cases: [(GameEventScriptBytecodeOpCode, GesValue)] = [
            (.add, .integer(34)), (.subtract, .integer(24)), (.multiply, .integer(145)),
            (.divide, .float(5.8)), (.integerDivide, .integer(5)), (.modulo, .integer(4)), (.remainder, .integer(4)),
        ]
        let state = GesVmState(maxRegisters: 128, maxCallDepth: 4)
        XCTAssertTrue(state.ensure(96))
        state.frameStart = 40
        for (opcode, expected) in cases {
            for destination: UInt16 in [0, 1, 2] {
                state.setValue(0, .integer(29))
                state.setValue(1, .integer(5))
                state.setValue(2, .text("previous destination"))
                XCTAssertTrue(state.setIntegerArithmeticResult(.init(opcode: opcode, word0: destination, word1: 0, word2: 1)))
                XCTAssertEqual(state.value(Int(destination)), expected)
            }
        }
    }

    func testTypedWritesReplaceReferenceValuesAndNormalizeNumbers() {
        let state = GesVmState(maxRegisters: 128, maxCallDepth: 4)
        XCTAssertTrue(state.ensure(96))
        state.frameStart = 40
        state.setValue(0, .list([.text("old")]))
        state.setInteger(0, 12, unit: .meter)
        XCTAssertEqual(state.integer(0), 12)
        XCTAssertEqual(state.unit(0), .meter)
        state.setFloat(0, 3.0, unit: .second)
        XCTAssertEqual(state.value(0), .integer(3, unit: .second))
        state.setFloat(0, .nan)
        XCTAssertTrue(state.isNothing(0))
        state.setPercentage(0, .infinity)
        XCTAssertEqual(state.value(0), .float(.infinity))
        state.setBoolean(0, true)
        XCTAssertEqual(state.truth(0), true)
        XCTAssertEqual(state.unit(0), .none)
        state.move(0, 0)
        XCTAssertEqual(state.truth(0), true)
        state.move(1, 0)
        state.setNothing(0)
        XCTAssertNil(state.truth(0))
        XCTAssertEqual(state.truth(1), true)
        state.setValue(0, .list([.text("retained")]))
        state.move(1, 0)
        state.setInteger(0, 1)
        XCTAssertEqual(state.value(1), .list([.text("retained")]))
    }

    func testNonIntegerOperandsLeaveRegistersUntouchedForFallback() {
        let state = GesVmState(maxRegisters: 32, maxCallDepth: 4)
        for value: GesValue in [.float(1.5), .percentage(0.2), .text("prefix"), .nothing] {
            for destination: UInt16 in [0, 1, 2] {
                state.setValue(0, value)
                state.setValue(1, .integer(5))
                state.setValue(2, .text("sentinel"))
                XCTAssertFalse(state.setIntegerArithmeticResult(.init(opcode: .add, word0: destination, word1: 0, word2: 1)))
                XCTAssertEqual(state.value(0), value)
                XCTAssertEqual(state.value(1), .integer(5))
                XCTAssertEqual(state.value(2), .text("sentinel"))
            }
        }
    }

    func testTypedHelperOutputsCanOverwriteTheirInputs() throws {
        let state = GesVmState(maxRegisters: 128, maxCallDepth: 4)
        XCTAssertTrue(state.ensure(96))
        state.frameStart = 40
        let host = try GameEventScriptHost()
        let context = GameEventScriptContext(host: host, random: .init(seed: 0), limits: .init(), extensions: nil, observer: nil)
        for destination in [0, 1, 2] {
            state.setText(0, "prefix")
            state.setInteger(1, 12)
            GesMath.add(state.value(0), state.value(1), sink: state.output(destination))
            XCTAssertEqual(state.value(destination), .text("prefix12"))

            state.setFloat(0, 1.25)
            state.setFloat(1, 2.5)
            GesMath.arithmetic(.multiply, state.value(0), state.value(1), sink: state.output(destination))
            XCTAssertEqual(state.value(destination), .float(3.125))

            state.setVector(0, x: 1, y: 2, z: 3, unit: .meter)
            state.setVector(1, x: 4, y: 5, z: 6, unit: .meter)
            GesMath.add(state.value(0), state.value(1), sink: state.output(destination))
            XCTAssertEqual(state.value(destination), .vector(x: 5, y: 7, z: 9, unit: .meter))

            state.setText(0, "12.5%")
            try GesCasts.cast(state.value(0), .percentage, context, sink: state.output(destination))
            XCTAssertEqual(state.value(destination), .percentage(0.125))
        }
        state.setList(0, [.text("kept"), .integer(2)])
        state.value(0).index(1, sink: state.output(0))
        XCTAssertEqual(state.value(0), .text("kept"))
        state.setMap(0, [.init(key: "x", value: .integer(42))])
        try state.value(0).member("x", sink: state.output(0))
        XCTAssertEqual(state.value(0), .integer(42))

        let iterator = GesIterator(.integerRange(from: 1, to: 2))!
        state.setIterator(0, iterator)
        XCTAssertNotNil(iterator.next(sink: state.output(0)))
        XCTAssertEqual(state.value(0), .integer(1))
        XCTAssertNotNil(iterator.next(sink: state.output(0)))
        XCTAssertEqual(state.value(0), .integer(2))
        XCTAssertNil(iterator.next(sink: state.output(0)))
        XCTAssertEqual(state.value(0), .integer(2))
    }

    func testTypedStagingSurvivesGrowthAndRejectsOverflowWithoutOverwritingLocals() throws {
        let state = GesVmState(maxRegisters: 40, maxCallDepth: 4)
        state.frameStart = 1
        state.frameLength = 30
        state.setText(0, "local")
        state.stageInteger(7, unit: .meter)
        state.stageFloat(2.5)
        state.stagePercentage(0.2)
        state.stageBoolean(true)
        state.stageText("argument")
        try state.stageTag("ready")
        state.stageNothing()
        XCTAssertEqual(state.stageLength, 7)
        XCTAssertEqual((0..<7).map(state.staged), [.integer(7, unit: .meter), .float(2.5), .percentage(0.2), .boolean(true), .text("argument"), try .tag("ready"), .nothing])
        XCTAssertThrowsError(try state.stageTag("Invalid"))
        XCTAssertEqual(state.stageLength, 7)
        state.stageInteger(8)
        state.stageInteger(9)
        state.stageInteger(10)
        XCTAssertEqual(state.stageLength, 9)
        XCTAssertEqual(state.error?.code, "runtime.registerOverflow")
        XCTAssertEqual(state.value(0), .text("local"))
        state.clearStage()
        XCTAssertEqual(state.stageLength, 0)
        XCTAssertEqual(state.value(0), .text("local"))
    }

}
