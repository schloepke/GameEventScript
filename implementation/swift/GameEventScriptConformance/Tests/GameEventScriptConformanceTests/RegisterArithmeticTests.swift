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
                state.set(0, .integer(29))
                state.set(1, .integer(5))
                state.set(2, .text("previous destination"))
                XCTAssertTrue(state.setIntegerArithmeticResult(.init(opcode: opcode, word0: destination, word1: 0, word2: 1)))
                XCTAssertEqual(state.value(Int(destination)), expected)
            }
        }
    }

    func testNonIntegerOperandsLeaveRegistersUntouchedForFallback() {
        let state = GesVmState(maxRegisters: 32, maxCallDepth: 4)
        for value: GesValue in [.float(1.5), .percentage(0.2), .text("prefix"), .nothing] {
            for destination: UInt16 in [0, 1, 2] {
                state.set(0, value)
                state.set(1, .integer(5))
                state.set(2, .text("sentinel"))
                XCTAssertFalse(state.setIntegerArithmeticResult(.init(opcode: .add, word0: destination, word1: 0, word2: 1)))
                XCTAssertEqual(state.value(0), value)
                XCTAssertEqual(state.value(1), .integer(5))
                XCTAssertEqual(state.value(2), .text("sentinel"))
            }
        }
    }
}
