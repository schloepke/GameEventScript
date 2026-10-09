// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import XCTest

@testable import GameEventScriptRuntime

/// Native storage controls complement the shared arithmetic semantics corpus.
final class RegisterArithmeticTests: XCTestCase {
    func testRegisterStorageStaysCompact() {
        // C#'s scalar/reference/metadata architecture must not regress to nested payload enums.
        XCTAssertLessThanOrEqual(MemoryLayout<GesValue>.stride, 32)
        XCTAssertEqual(MemoryLayout<GameEventScriptMessage>.stride, MemoryLayout<AnyObject>.stride)
        XCTAssertEqual(MemoryLayout<GameEventScriptMessageSignature>.stride, MemoryLayout<AnyObject>.stride)
    }

    func testNumericProjectionIsIndependentOfCollectionCount() {
        var value = GesValue.dice([Int32.max, Int32.max, 1])
        XCTAssertEqual(value.numericIntegerValue, 4_294_967_295)
        XCTAssertEqual(value.length, 3)
        let retained = value
        value.setList([.integer(7)])
        XCTAssertNil(value.numericIntegerValue)
        XCTAssertEqual(value.length, 1)
        XCTAssertEqual(retained.numericIntegerValue, 4_294_967_295)
        value.setText("a😀b")
        XCTAssertFalse(value.isNumeric)
        XCTAssertEqual(value.length, 3)
        value.setIntegerRange(from: 1, to: Int64.max)
        XCTAssertNil(value.numericIntegerValue)
        XCTAssertEqual(value.length, Int(Int32.max))
        value.setDice([])
        XCTAssertEqual(value.numericIntegerValue, 0)
        XCTAssertEqual(value.length, 0)
    }

    func testDirectArrayMutationPreservesCopiesAndResetsMetadata() throws {
        var values: [GesValue] = [.list([.text("original")])]
        let retained = values[0]
        values[0].setInteger(-7, unit: .meter)
        XCTAssertEqual(retained.asList, [.text("original")])
        XCTAssertEqual(values[0].integerValue, -7)
        XCTAssertEqual(values[0].unit, .meter)
        XCTAssertNil(values[0].listValue)
        XCTAssertEqual(values[0].length, 0)
        XCTAssertTrue(values[0].isNumeric)
        XCTAssertEqual(values[0].truth, true)
        values[0].setText("FALSE")
        XCTAssertEqual(values[0].unit, .none)
        XCTAssertFalse(values[0].isNumeric)
        XCTAssertEqual(values[0].truth, false)
        XCTAssertEqual(values[0].length, 5)
        values[0].setPercentage(0)
        XCTAssertTrue(values[0].hasValue)
        XCTAssertEqual(values[0].truth, false)
        values[0].setFloat(.nan)
        XCTAssertFalse(values[0].hasValue)
        XCTAssertFalse(values[0].isNumeric)
        XCTAssertNil(values[0].truth)
        XCTAssertEqual(values[0], .nothing)
        XCTAssertEqual(values[0].hashValue, GesValue.nothing.hashValue)
        let text = GesValue.text("true")
        values[0].setTextConstant(text, tag: true)
        XCTAssertEqual(values[0], try .tag("true"))
        XCTAssertEqual(values[0].truth, false)
        values[0].setTextConstant(text, tag: false)
        XCTAssertEqual(values[0].truth, true)
    }

    func testRegisterOverwriteReleasesInternalObjects() throws {
        let state = GesVmState(maxRegisters: 32, maxCallDepth: 4)
        weak var released: GesIterator?
        do {
            let iterator = try XCTUnwrap(GesIterator(.list([.integer(1)])))
            released = iterator
            state.setIterator(0, iterator)
        }
        XCTAssertNotNil(released)
        XCTAssertTrue(state.value(0).isNothing)
        state.setInteger(0, 9)
        XCTAssertNil(released)
        XCTAssertNil(state.slot(0).iteratorValue)
        XCTAssertTrue(state.slot(0).isRegisterData)
    }

    func testMessageAndHandlerWrapExistingImmutablePayloads() throws {
        let signature = try GameEventScriptMessageSignature(name: "Done", parameters: ["value"])
        let message = try XCTUnwrap(signature.createMessage([.integer(7)]))
        var value = GesValue.message(message)
        XCTAssertTrue(value.messageValue!.storage === message.storage)
        let copy = value
        value.setHandler(signature)
        XCTAssertTrue(value.signatureValue!.storage === signature.storage)
        XCTAssertEqual(copy.messageValue, message)
        value.setNothing()
        XCTAssertEqual(copy.messageValue, message)
    }

    func testMessageArgumentsReuseBuffersAndPreserveSnapshots() throws {
        let signature = try GameEventScriptMessageSignature(name: "Done", parameters: ["_", "value", "_"])
        var input: [GesValue] = [.integer(1), .text("kept"), .boolean(true)]
        let message = try XCTUnwrap(signature.createMessage(input))
        let arguments = message.arguments
        input.withUnsafeBufferPointer { original in
            arguments.values.withUnsafeBufferPointer { stored in XCTAssertEqual(original.baseAddress, stored.baseAddress) }
        }
        signature.parameters.withUnsafeBufferPointer { original in
            arguments.signatureLabels.withUnsafeBufferPointer { stored in XCTAssertEqual(original.baseAddress, stored.baseAddress) }
        }
        let pairs = try [GameEventScriptMessageArgument(value: .integer(1)), GameEventScriptMessageArgument(name: "value", value: .text("kept")), GameEventScriptMessageArgument(value: .boolean(true))]
        let rebuilt = try GameEventScriptMessageArguments(pairs)
        XCTAssertEqual(arguments, rebuilt)
        XCTAssertEqual(arguments.hashValue, rebuilt.hashValue)
        XCTAssertEqual(Array(arguments), pairs)
        XCTAssertEqual(try arguments.indexOf("value"), 1)
        XCTAssertNil(try arguments.indexOf("_"))
        var first = arguments.makeIterator()
        var second = arguments.makeIterator()
        XCTAssertEqual(first.next(), pairs[0])
        XCTAssertEqual(first.next(), pairs[1])
        XCTAssertEqual(second.next(), pairs[0])
        XCTAssertEqual(first.next(), pairs[2])
        XCTAssertNil(first.next())
        XCTAssertNil(first.next())
        input[0] = .integer(99)
        var exposed = arguments.values
        exposed[1] = .nothing
        var labels = arguments.signatureLabels
        labels[1] = "changed"
        XCTAssertEqual(message.arguments, rebuilt)
        XCTAssertEqual(message.signatureId, "Done(_,value,_)")
        XCTAssertEqual(Array(GameEventScriptMessageArguments.empty), [])
        XCTAssertNil(signature.createMessage([]))
        XCTAssertThrowsError(try GameEventScriptMessageArguments([.init(name: "value", value: .integer(1)), .init(name: "value", value: .integer(2))]))
    }

    func testRecordReusesCanonicalMapStorageWhenOverwritingMapRegister() throws {
        let state = GesVmState(maxRegisters: 32, maxCallDepth: 4)
        state.setMap(0, [.init(key: "z", value: .integer(1)), .init(key: "a", value: .integer(2)), .init(key: "z", value: .integer(3))])
        let map = try XCTUnwrap(state.value(0).asMap)
        state.setRecord(0, typeName: "Sample", fields: map)
        let record = state.value(0)
        let fields = try XCTUnwrap(record.asMap)
        map.entries.withUnsafeBufferPointer { original in
            fields.entries.withUnsafeBufferPointer { stored in XCTAssertEqual(original.baseAddress, stored.baseAddress) }
        }
        XCTAssertEqual(record.customTypeName, "Sample")
        XCTAssertEqual(fields.entries.map(\.key), ["a", "z"])
        XCTAssertEqual(fields.get("z"), .integer(3))
        var exposed = fields.entries
        exposed[0] = .init(key: "changed", value: .nothing)
        state.setNothing(0)
        XCTAssertEqual(record.asMap, map)
        XCTAssertEqual(record, .record(typeName: "Sample", entries: map.entries))
    }

    func testTextIdentityCastCanOverwriteItsSource() throws {
        let state = GesVmState(maxRegisters: 32, maxCallDepth: 4)
        let host = try GameEventScriptHost()
        let context = GameEventScriptContext(host: host, random: .init(seed: 0), limits: .init(), extensions: nil, observer: nil)
        state.setText(0, "kept 🐉 e\u{301}")
        let snapshot = state.value(0)
        try GesCasts.cast(state.value(0), .text, context, sink: state.output(0))
        XCTAssertEqual(state.value(0), snapshot)
        state.setNothing(0)
        XCTAssertEqual(snapshot.asText, "kept 🐉 e\u{301}")
    }

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
