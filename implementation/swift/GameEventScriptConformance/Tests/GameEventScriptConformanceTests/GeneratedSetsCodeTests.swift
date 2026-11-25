// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/sets.gess: message sets with parameters as the envelope
/// message named after the set, the converters between model sets, and the wire code that converts to the models.
final class GeneratedSetsCodeTests: XCTestCase {
    private func set() throws -> GesSchemaSet {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().appendingPathComponent("../../../../../")
        let source = try String(contentsOf: root.appendingPathComponent("conformance/fixtures/SchemaGeneration/sets.gess"), encoding: .utf8)
        return try GesSchemaSet(parsing: source)
    }

    private func commands() -> SetsModel.DroneCommands {
        SetsModel.DroneCommands(
            ids: [SetsModel.DroneId(id: 7), SetsModel.DroneId(id: 4095)],
            urgent: true,
            .moveTo(SetsModel.MoveTo(target: GesPoint(x: 1.5, y: -2, z: 3), note: "north", fast: true))
        )
    }

    func testModelHashesMatchTheSchemas() throws {
        let set = try set()
        XCTAssertEqual(SetsModel.DroneCommands.schemaHash, try set.schema("DroneCommands").hash)
        XCTAssertEqual(SetsModel.Landing.schemaHash, try set.schema("Landing").hash)
    }

    func testModelSetIsTheEnvelopeMessage() throws {
        let commands = commands()
        let message = commands.toMessage()
        XCTAssertEqual(message.name, "DroneCommands")
        XCTAssertEqual(message.arguments.map(\.name), ["ids", "urgent", "message"])
        XCTAssertEqual(try SetsModel.DroneCommands(message: message), commands)

        let bound = try set().schema("DroneCommands").bind()
        let bytes = try GesSchemaValues.encode(bound, .message(message))
        XCTAssertEqual(try GesSchemaValues.decode(bound, bytes).value.messageValue, message)
    }

    func testModelSetDefaultsItsParameters() {
        let commands = SetsModel.DroneCommands(.stop(SetsModel.Stop()))
        XCTAssertTrue(commands.ids.isEmpty)
        XCTAssertNil(commands.urgent)
        XCTAssertEqual(SetsModel.Landing(.land(SetsModel.Land())).meta, .empty)
    }

    func testLenientReadCompletesParametersAndMessage() throws {
        let inner = try GameEventScriptMessage(name: "Land", arguments: [])
        let message = try GameEventScriptMessage(name: "Landing", arguments: [GesMessageArgument(name: "message", value: .message(inner))])
        let landing = try SetsModel.Landing(lenientMessage: message)
        XCTAssertEqual(landing.meta, .empty)
        XCTAssertEqual(landing.message, .land(SetsModel.Land(slow: false)))
        XCTAssertThrowsError(try SetsModel.Landing(message: message))
    }

    func testReadRejectsTheBareMessage() {
        do {
            _ = try SetsModel.DroneCommands(message: SetsModel.Stop().toMessage())
            XCTFail("expected a type mismatch")
        } catch {
            XCTAssertEqual(error.code, GesWireError.typeMismatch)
            XCTAssertEqual(error.path, "")
        }
    }

    func testConvertersTakeSharedMembers() {
        let move = commands().message
        let landing = SetsModel.Landing.Case(move)
        guard case .moveTo = landing else { return XCTFail("expected MoveTo") }
        XCTAssertEqual(SetsModel.DroneCommands.Case(landing!), move)
        XCTAssertNil(SetsModel.Landing.Case(.stop(SetsModel.Stop())))
        XCTAssertNil(SetsModel.DroneCommands.Case(.land(SetsModel.Land(slow: true))))
    }

    func testWireConvertsToTheModel() throws {
        let commands = commands()
        let wire = SetsWire.DroneCommands()
        wire.read(model: commands)
        XCTAssertEqual(wire.selected, .moveTo)
        XCTAssertEqual(wire.toModel(), commands)
        XCTAssertEqual(try wire.toMessage(), commands.toMessage())

        let read = SetsWire.DroneCommands()
        _ = try read.read(from: wire.encoded())
        XCTAssertEqual(read.toModel(), commands)

        let fromMessage = SetsWire.DroneCommands()
        try fromMessage.read(message: commands.toMessage())
        XCTAssertEqual(try fromMessage.encoded(), try wire.encoded())
    }
}
