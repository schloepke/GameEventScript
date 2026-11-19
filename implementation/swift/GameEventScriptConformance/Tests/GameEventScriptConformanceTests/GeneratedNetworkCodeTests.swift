// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptNetwork
import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/network.gess: the constants of a network, which agree with the
/// network the runtime binds with the same parameter values, its typed send and publish functions, its typed ends and the cases
/// of its message set.
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
        let plain = GesNetworkOptions.message(client: .unencrypted)
        let server = GesNetworkOptions.message(server: .unencrypted)
        let loop = try GesNetworkSimulator(
            client: try .makeClient(network: network, now: 0, options: plain),
            server: try .makeServer(network: network, now: 0, options: server),
            seed: 0,
            latencyMicroseconds: 0,
            jitterMicroseconds: 0
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

    private func bound(_ worldSize: String = "2km") throws -> GesBoundNetwork {
        let parameters = GesSchemaParameters()
        try parameters.with("worldSize", literal: worldSize)
        return try fixture().bind(parameters: parameters)
    }

    private static func describe(_ event: NetworkWire.Game.ClientEvent) -> String {
        switch event {
        case .connected: "connected"
        case .disconnected(let code, let detail, let byPeer): "disconnected \(code) \(detail) \(byPeer)"
        case .world(let world): "world \(world.tick) \(world.x)"
        case .chunk(let chunk): "chunk \(chunk.index)"
        case .tick(let number): "tick \(number)"
        case .transferProgress, .transferCancelled: "transfer"
        case .received: "received"
        }
    }

    private static func describe(_ event: NetworkWire.Game.ServerEvent) -> String {
        switch event {
        case .connecting(let client, let token): "connecting \(client) \(token.count)"
        case .clientConnected(let client, _): "connected \(client)"
        case .clientDisconnected(let client, let code, _, _): "disconnected \(client) \(code)"
        case .commands(let client, .move(let move)): "move \(client) \(move.x)"
        case .commands(let client, .fire): "fire \(client)"
        case .tick(let client, let number): "tick \(client) \(number)"
        case .transferProgress, .transferCancelled: "transfer"
        case .received: "received"
        }
    }

    func testTypedEndsSendTicksAndDispatchTypedEvents() throws {
        let pair = try GesNetworkPair.synchronous(network: try bound(), token: Array("t".utf8))
        defer { pair.release() }
        let client = try NetworkWire.Game.Client(pair.client)
        let server = try NetworkWire.Game.Server(pair.server)
        var clientEvents: [String] = []
        var serverEvents: [String] = []
        try client.poll { clientEvents.append(Self.describe($0)) }
        try server.poll { serverEvents.append(Self.describe($0)) }
        XCTAssertEqual(clientEvents, ["connected"])
        XCTAssertEqual(serverEvents, ["connected 0"])
        clientEvents.removeAll()
        serverEvents.removeAll()

        var move = NetworkWire.Move()
        move.x = 5
        XCTAssertEqual(try client.send(.move(move)), .queued)
        XCTAssertEqual(try client.send(.fire(NetworkWire.Fire())), .queued)
        var chunk = NetworkWire.Chunk()
        chunk.index = 3
        XCTAssertEqual(try server.send(chunk, client: 0), .queued)
        var world = NetworkWire.World()
        world.tick = 7
        world.x = 12
        try server.publishTick { tick in XCTAssertEqual(try tick.publish(world), .queued) }

        // The bulk route :Chunk also reports its transfer progress; this test leaves it out.
        try client.poll { event in
            let text = Self.describe(event)
            if text != "transfer" { clientEvents.append(text) }
        }
        try server.poll { event in
            let text = Self.describe(event)
            if text != "transfer" { serverEvents.append(text) }
        }
        XCTAssertEqual(clientEvents, ["chunk 3", "world 7 12.0", "tick 0", "received"])
        XCTAssertEqual(serverEvents, ["move 0 5.0", "fire 0", "received"])
        XCTAssertEqual(server.clientCount, 1)
        XCTAssertThrowsError(try server.send(chunk, client: 9))

        struct Failure: Error {}

        XCTAssertThrowsError(try server.publishTick { _ in throw Failure() })
        XCTAssertEqual(try server.publishTick { _ in }, .queued, "A throwing body closes its tick.")
    }

    func testTypedEndRejectsAnotherNetworkAndClosesOnAnUndecodablePayload() throws {
        let other = try GesNetworkPair.synchronous(network: try bound("3km"), token: Array("t".utf8))
        XCTAssertThrowsError(try NetworkWire.Game.Client(other.client))
        other.release()

        let pair = try GesNetworkPair.synchronous(network: try bound(), token: Array("t".utf8))
        defer { pair.release() }
        let client = try NetworkWire.Game.Client(pair.client)
        var events: [String] = []
        try client.poll { events.append(Self.describe($0)) }
        events.removeAll()
        // The x of :World holds at most 2000, so 2047 is no valid value.
        XCTAssertEqual(pair.server.publish(route: NetworkWire.Game.serverPublishesWorld, payload: [0xFF, 0xFF, 0xFF, 0xFF]), .queued)
        try client.poll { events.append(Self.describe($0)) }
        XCTAssertFalse(events.contains { $0.hasPrefix("world") }, "\(events)")
        XCTAssertNotEqual(pair.client.connection.state, .connected)
    }

    func testMessageCasesConvertBothWays() {
        var commands = NetworkWire.Commands()
        commands.selected = .move
        commands.move.x = 9
        let converted = commands.asCase
        guard case .move(let move) = converted else { return XCTFail("\(converted)") }
        XCTAssertEqual(move.x, 9)
        var assigned = NetworkWire.Commands()
        assigned.assign(.fire(NetworkWire.Fire()))
        XCTAssertEqual(assigned.selected, .fire)
        assigned.assign(converted)
        XCTAssertEqual(assigned.selected, .move)
        XCTAssertEqual(assigned.move.x, 9)
    }
}
