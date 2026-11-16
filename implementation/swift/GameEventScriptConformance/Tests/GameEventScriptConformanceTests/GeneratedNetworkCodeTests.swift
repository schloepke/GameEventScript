// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/network.gess: the constants of a network, which agree with the
/// network the runtime binds with the same parameter values.
final class GeneratedNetworkCodeTests: XCTestCase {
    func testConstantsMatchTheBoundNetwork() throws {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().appendingPathComponent("../../../../../")
        let source = try String(contentsOf: root.appendingPathComponent("conformance/fixtures/SchemaGeneration/network.gess"), encoding: .utf8)
        let network = try GesSchemaSet(parsing: source).network("Game")
        let parameters = GesSchemaParameters()
        try parameters.with("worldSize", literal: "2km")
        XCTAssertEqual(try network.bind(parameters: parameters).hash, NetworkWire.Game.hash)
        let other = GesSchemaParameters()
        try other.with("worldSize", literal: "3km")
        XCTAssertNotEqual(try network.bind(parameters: other).hash, NetworkWire.Game.hash, "A parameter enters the hash.")
        XCTAssertEqual(network.routes[0].index, NetworkWire.Game.clientSendsCommands)
        XCTAssertEqual(network.routes[1].index, NetworkWire.Game.serverPublishesWorld)
        XCTAssertEqual(network.routes[2].index, NetworkWire.Game.serverSendsChunk)
        XCTAssertEqual(network.routes[1].rateMicroseconds, NetworkWire.Game.serverPublishesWorldEveryMicroseconds)
        XCTAssertEqual(50_000, NetworkWire.Game.serverPublishesWorldEveryMicroseconds)
    }
}
