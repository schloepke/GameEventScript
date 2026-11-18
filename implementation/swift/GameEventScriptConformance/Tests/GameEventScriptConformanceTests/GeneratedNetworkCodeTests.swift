// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptNetwork
import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/network.gess: the constants of a network, which agree with the
/// network the runtime binds with the same parameter values, and its typed send and publish functions.
final class GeneratedNetworkCodeTests: XCTestCase {
    private func fixture() throws -> GesNetwork {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().appendingPathComponent("../../../../../")
        let source = try String(contentsOf: root.appendingPathComponent("conformance/fixtures/SchemaGeneration/network.gess"), encoding: .utf8)
        return try GesSchemaSet(parsing: source).network("Game")
    }

    func testConstantsMatchTheBoundNetwork() throws {
        let network = try fixture()
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

    func testTypedFunctionsSendTheEncodedRecords() throws {
        let parameters = GesSchemaParameters()
        try parameters.with("worldSize", literal: "2km")
        let network = try fixture().bind(parameters: parameters)
        var plain = GesNetworkOptions()
        plain.minimumSecurity = .none
        let loop = try GesNetworkLoopback(
            client: try .makeClient(network: network, now: 0, options: plain),
            server: try .makeServer(network: network, now: 0, options: plain)
        )
        try loop.advance(0)
        XCTAssertEqual(loop.server.nextEvent().kind, .connecting)
        try loop.server.accept()
        try loop.advance(0)
        XCTAssertEqual(loop.client.nextEvent().kind, .connected)
        XCTAssertEqual(loop.server.nextEvent().kind, .connected)

        let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: 16, alignment: 8)
        defer { buffer.deallocate() }
        var world = NetworkWire.World()
        world.tick = 7
        world.x = 12
        var chunk = NetworkWire.Chunk()
        chunk.index = 3
        XCTAssertEqual(try NetworkWire.Game.serverPublishWorld(world, on: loop.server, buffer: buffer), .notSubscribed)
        try loop.server.subscribe(route: NetworkWire.Game.serverPublishesWorld)
        XCTAssertEqual(try NetworkWire.Game.serverPublishWorld(world, on: loop.server, buffer: buffer), .queued)
        XCTAssertEqual(try NetworkWire.Game.serverSendChunk(chunk, on: loop.server, buffer: buffer), .queued)
        var commands = NetworkWire.Commands()
        commands.selected = .move
        commands.move.x = 5
        XCTAssertEqual(try NetworkWire.Game.clientSendCommands(commands, on: loop.client, buffer: buffer), .queued)
        XCTAssertEqual(try NetworkWire.Game.serverSendChunk(chunk, on: loop.client, buffer: buffer), .invalidRoute)
        try loop.advance(1_000)

        var received = loop.client.nextEvent()
        XCTAssertEqual(received.route, NetworkWire.Game.serverPublishesWorld)
        var read = NetworkWire.World()
        _ = try read.read(from: received.payload)
        XCTAssertEqual(read.tick, 7)
        XCTAssertEqual(read.x, 12)
        received = loop.client.nextEvent()
        XCTAssertEqual(received.route, NetworkWire.Game.serverSendsChunk)
        received = loop.server.nextEvent()
        XCTAssertEqual(received.route, NetworkWire.Game.clientSendsCommands)
        var move = NetworkWire.Commands()
        _ = try move.read(from: received.payload)
        XCTAssertEqual(move.move.x, 5)
    }
}
