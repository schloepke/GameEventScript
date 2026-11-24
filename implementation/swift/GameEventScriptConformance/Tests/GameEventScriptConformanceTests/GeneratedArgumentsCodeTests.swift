// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/arguments.gess: message argument defaults in generated
/// initializers and the lenient read, which agrees with `GesSchemaValues.completeMessage`.
final class GeneratedArgumentsCodeTests: XCTestCase {
    private func bound(_ name: String) throws -> GesBoundSchema {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().appendingPathComponent("../../../../../")
        let source = try String(contentsOf: root.appendingPathComponent("conformance/fixtures/SchemaGeneration/arguments.gess"), encoding: .utf8)
        return try GesSchemaSet(parsing: source).schema(name).bind()
    }

    private func message(_ name: String, _ arguments: (String, GesValue)...) throws -> GameEventScriptMessage {
        try GameEventScriptMessage(name: name, arguments: arguments.map { try GesMessageArgument(name: $0.0, value: $0.1) })
    }

    func testInitializerTakesTheDefaults() {
        let fire = ArgumentsWire.Fire(count: 3)
        XCTAssertEqual(fire.count, 3)
        XCTAssertNil(fire.chargeOptional)
        XCTAssertEqual(fire.when, .aimed)
        XCTAssertEqual(fire.delay, 2)
        XCTAssertEqual(fire.ratio, 0.5)
        XCTAssertFalse(fire.loud)
        XCTAssertEqual(ArgumentsWire.Fire(charge: 4).chargeOptional, 4)
        let turn = ArgumentsWire.Turn(side: .right)
        XCTAssertEqual(turn.speed, 3)
        XCTAssertEqual(turn.side, .right)
        let say = ArgumentsWire.Say(text: "hi")
        XCTAssertEqual(say.text, "hi")
        XCTAssertTrue(say.loud)
    }

    func testParameterlessCreationStaysZero() {
        let fire = ArgumentsWire.Fire()
        XCTAssertEqual(fire.count, 0)
        XCTAssertEqual(fire.delay, 0)
        XCTAssertFalse(ArgumentsWire.Say().loud)
    }

    func testLenientReadFillsTheDefaults() throws {
        let input = try message("Fire", ("count", .integer(5)), ("loud", .boolean(true)))
        var fire = ArgumentsWire.Fire()
        try fire.read(lenientMessage: input)
        XCTAssertEqual(fire.count, 5)
        XCTAssertNil(fire.chargeOptional)
        XCTAssertEqual(fire.when, .aimed)
        XCTAssertEqual(fire.delay, 2)
        XCTAssertEqual(fire.ratio, 0.5)
        XCTAssertTrue(fire.loud)
        let schema = try bound("Fire")
        XCTAssertEqual(try fire.encoded(), try GesSchemaValues.encode(schema, .message(GesSchemaValues.completeMessage(schema, input))))
        XCTAssertEqual(try fire.encoded(), try ArgumentsWire.Fire(count: 5, loud: true).encoded())
    }

    func testLenientReadOfASetFillsTheMember() throws {
        let actions = ArgumentsWire.Actions()
        try actions.read(lenientMessage: message("Turn", ("side", .tag("right"))))
        XCTAssertEqual(actions.selected, .turn)
        XCTAssertEqual(actions.turn.speed, 3)
        XCTAssertEqual(actions.turn.side, .right)
    }

    func testLenientReadRejectsAMissingArgument() throws {
        var turn = ArgumentsWire.Turn()
        let input = try message("Turn", ("speed", .integer(2)))
        XCTAssertThrowsError(try turn.read(lenientMessage: input)) { error in
            let wire = error as? GesWireError
            XCTAssertEqual(wire?.code, GesWireError.missingField)
            XCTAssertEqual(wire?.path, "side")
        }
    }
}
