// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import XCTest

final class NetworkEndTests: XCTestCase {
    private static let source = """
        wire schema :Pos as { v: :Number integer between 0 and 255 }
        wire schema :Aim as { v: :Number integer between 0 and 255 }
        wire schema :State as { v: :Number integer between 0 and 255 }
        wire schema :Log as { v: :Number integer between 0 and 255 }
        wire messages Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends Commands reliable
          client publishes :Pos latest
          client publishes :Aim latest
          server publishes :State latest
          server publishes :Log reliable
        }
        """

    private let commands = 0
    private let pos = 1
    private let aim = 2
    private let state = 3
    private let log = 4

    private func network() throws -> GesBoundNetwork { try GesSchemaSet(parsing: Self.source).network("Game").bind() }

    private final class Recorder {
        var events: [String] = []
        var onEvent: ((GesNetworkEndEventKind, Int) throws -> Void)?

        func handle(_ event: GesNetworkEndEvent) throws {
            switch event.kind {
            case .message:
                events.append("message \(event.route) \(Self.hex(event.payload))")
            case .tick:
                events.append("tick \(event.tickNumber)")
            case .connecting:
                events.append("connecting \(event.client) \(Self.hex(event.payload))")
            case .disconnected, .clientDisconnected:
                events.append("\(Self.name(event.kind)) \(event.client) \(event.code) \(event.byPeer ? "True" : "False")")
            default:
                events.append("\(Self.name(event.kind)) \(event.client)")
            }
            try onEvent?(event.kind, event.client)
        }

        func take() -> [String] {
            let taken = events
            events.removeAll()
            return taken
        }

        static func name(_ kind: GesNetworkEndEventKind) -> String {
            let text = "\(kind)"
            return text.prefix(1).uppercased() + text.dropFirst()
        }

        static func hex(_ bytes: UnsafeRawBufferPointer) -> String {
            let digits = Array("0123456789ABCDEF")
            var text = ""
            for byte in bytes {
                text.append(digits[Int(byte >> 4)])
                text.append(digits[Int(byte & 15)])
            }
            return text
        }
    }

    @discardableResult
    private func poll(_ pair: GesNetworkPair, _ rounds: Int = 1, client: Recorder = Recorder(), server: Recorder = Recorder()) throws -> (Recorder, Recorder) {
        for _ in 0..<rounds {
            try pair.client.poll(client.handle)
            try pair.server.poll(server.handle)
        }
        return (client, server)
    }

    private func assertThrows(_ expected: GesApiError, _ body: () throws -> Void, file: StaticString = #filePath, line: UInt = #line) {
        do {
            try body()
            XCTFail("Expected \(expected).", file: file, line: line)
        } catch let error as GesApiError {
            switch (error, expected) {
            case (.invalidArgument, .invalidArgument), (.invalidOperation, .invalidOperation): break
            default: XCTFail("Expected \(expected), got \(error).", file: file, line: line)
            }
        } catch {
            XCTFail("Expected \(expected), got \(error).", file: file, line: line)
        }
    }

    func testSynchronousPairIsConnectedAndDeliversBeforeTheNextPoll() throws {
        let pair = try GesNetworkPair.synchronous(network: network(), token: Array("t".utf8))
        defer { pair.release() }
        XCTAssertEqual(pair.client.connection.state, .connected)
        XCTAssertEqual(pair.server.clientCount, 1)
        let (client, server) = try poll(pair)
        XCTAssertEqual(client.take(), ["Connected -1"])
        XCTAssertEqual(server.take(), ["ClientConnected 0"])

        XCTAssertEqual(pair.client.send(route: commands, payload: [5]), .queued)
        XCTAssertEqual(pair.server.publish(route: state, payload: [7]), .queued)
        XCTAssertEqual(pair.server.send(route: commands, payload: [1]), .invalidRoute, "A route the server does not send.")
        try poll(pair, client: client, server: server)
        XCTAssertEqual(client.take(), ["message \(state) 07", "Received -1"])
        XCTAssertEqual(server.take(), ["message \(commands) 05", "Received -1"])
    }

    func testServerEndSendsToOneClientAndChecksRoutes() throws {
        let pair = try GesNetworkPair.synchronous(network: network(), token: Array("t".utf8))
        defer { pair.release() }
        let (client, server) = try poll(pair)
        _ = client.take()
        _ = server.take()
        XCTAssertEqual(pair.server.send(route: state, payload: [1]), .invalidRoute)
        XCTAssertEqual(pair.server.publish(route: pos, payload: [1]), .invalidRoute)
        XCTAssertEqual(pair.client.publish(route: state, payload: [1]), .invalidRoute)
        assertThrows(.invalidArgument("")) { try pair.server.close(client: 5, detail: "x") }
        XCTAssertEqual(pair.server.publish(route: log, payload: [9]), .queued)
        try pair.server.unsubscribe(client: 0, route: log)
        XCTAssertEqual(pair.server.publish(route: log, payload: [8]), .queued, "An unsubscribed client is skipped.")
        XCTAssertEqual(try pair.server.send(route: commands, payload: [1], client: 0), .invalidRoute)
        try poll(pair, client: client, server: server)
        XCTAssertEqual(client.take(), ["message \(log) 09", "Received -1"])
    }

    func testServerTicksReachTheClientAndAClientThatJoinsDuringOne() throws {
        let pair = try GesNetworkPair.synchronous(network: network(), token: Array("t".utf8))
        defer { pair.release() }
        let (client, server) = try poll(pair)
        _ = client.take()
        _ = pair.server.publish(route: state, payload: [1])
        try pair.server.beginTick()
        try pair.server.include(route: state)
        XCTAssertEqual(pair.server.publish(route: log, payload: [2]), .queued)
        XCTAssertEqual(try pair.server.endTick(), .queued)
        try pair.server.beginTick()
        _ = pair.server.publish(route: state, payload: [3])
        try pair.server.endTick()
        try poll(pair, client: client, server: server)
        XCTAssertEqual(client.take(), ["message \(state) 01", "message \(log) 02", "tick 0", "message \(state) 03", "tick 1", "Received -1"])
        assertThrows(.invalidOperation("")) { try pair.server.endTick() }
        try pair.server.beginTick()
        assertThrows(.invalidOperation("")) { try pair.server.beginTick() }
        assertThrows(.invalidArgument("")) { try pair.server.include(route: log) }
        try pair.server.endTick()
    }

    func testAnIncludedStateReachesAClientThatMissedIt() throws {
        let pair = try GesNetworkPair.synchronous(network: network(), token: Array("t".utf8))
        defer { pair.release() }
        let (client, server) = try poll(pair)
        _ = client.take()
        _ = pair.server.publish(route: state, payload: [1])
        try poll(pair, client: client, server: server)
        _ = client.take()
        try pair.server.unsubscribe(client: 0, route: state)
        _ = pair.server.publish(route: state, payload: [2])
        try pair.server.subscribe(client: 0, route: state)
        try pair.server.beginTick()
        try pair.server.include(route: state)
        try pair.server.endTick()
        try poll(pair, client: client, server: server)
        // The client's last state is 01, so the tick sends it the kept 02 instead of naming 01.
        XCTAssertEqual(client.take(), ["message \(state) 02", "tick 0", "Received -1"])
    }

    func testClientEndQueuesUntilTheHandshakeInOrder() throws {
        let pair = try GesNetworkPair.realtime(network: network(), token: Array("AB".utf8))
        defer { pair.release() }
        XCTAssertEqual(pair.client.send(route: commands, payload: [1]), .queued)
        XCTAssertEqual(pair.client.publish(route: pos, payload: [1]), .queued)
        XCTAssertEqual(pair.client.publish(route: pos, payload: [2]), .queued)
        XCTAssertEqual(pair.client.publish(route: aim, payload: [9]), .queued)
        try pair.client.beginTick()
        XCTAssertEqual(pair.client.publish(route: aim, payload: [3]), .queued)
        try pair.client.include(route: pos)
        XCTAssertEqual(try pair.client.endTick(), .queued)
        XCTAssertEqual(pair.client.publish(route: pos, payload: [4]), .queued)
        XCTAssertEqual(pair.client.send(route: pos, payload: [1]), .invalidRoute)
        XCTAssertEqual(pair.client.send(route: commands, payload: [UInt8](repeating: 0, count: 100)), .messageTooLarge)
        assertThrows(.invalidArgument("")) {
            try pair.client.beginTick()
            defer { _ = try? pair.client.endTick() }
            try pair.client.include(route: self.commands)
        }

        let (client, server) = try poll(pair, 10)
        XCTAssertEqual(client.events, ["Connected -1"])
        let events = server.events.filter { $0 != "Received -1" }
        // The Aim state the tick replaced and the Pos state older than the tick never travel; as on a connected client,
        // the newer Pos state replaces the tick's pending one before the packet leaves.
        XCTAssertEqual(
            events,
            ["connecting 0 4142", "ClientConnected 0", "message \(commands) 01", "message \(pos) 04", "message \(aim) 03", "tick 0"],
            events.joined(separator: " | ")
        )
    }

    func testAReplacedTickBeforeTheHandshakeKeepsItsStates() throws {
        let pair = try GesNetworkPair.realtime(network: network())
        defer { pair.release() }
        try pair.client.beginTick()
        _ = pair.client.publish(route: pos, payload: [1])
        _ = pair.client.publish(route: aim, payload: [2])
        try pair.client.endTick()
        try pair.client.beginTick()
        _ = pair.client.publish(route: aim, payload: [3])
        try pair.client.endTick()
        let (_, server) = try poll(pair, 10)
        // The replaced tick's Pos state becomes a latest state; its Aim state is replaced by the newer tick's.
        XCTAssertEqual(
            server.events.filter { $0 != "Received -1" },
            ["connecting 0 ", "ClientConnected 0", "message \(pos) 01", "message \(aim) 03", "tick 0"]
        )
    }

    func testQueuedReliablePublishesCountTheirModeByte() throws {
        let network = try GesSchemaSet(parsing: "wire schema :Note as { v: :Number integer between 0 and 255 }\nnetwork :Game as { client publishes :Note reliable }\n")
            .network("Game").bind()
        var options = GesNetworkOptions.inProcess()
        options.maxSendQueueBytes = 3
        let pair = try GesNetworkPair.realtime(network: network, options: options)
        defer { pair.release() }
        // A one-byte state takes two bytes in the queue, as on the connection, which could not take the second.
        XCTAssertEqual(pair.client.publish(route: 0, payload: [1]), .queued)
        XCTAssertEqual(pair.client.publish(route: 0, payload: [2]), .queueFull)
        let (_, server) = try poll(pair, 10)
        XCTAssertEqual(server.events.filter { $0 != "Received -1" }, ["connecting 0 ", "ClientConnected 0", "message 0 01"])
    }

    func testATickOpenAtTheHandshakeMovesToTheConnection() throws {
        let pair = try GesNetworkPair.realtime(network: network())
        defer { pair.release() }
        try pair.client.beginTick()
        _ = pair.client.publish(route: pos, payload: [1])
        let (client, server) = try poll(pair, 10)
        XCTAssertEqual(pair.client.connection.state, .connected)
        _ = pair.client.publish(route: aim, payload: [2])
        try pair.client.endTick()
        try poll(pair, 5, client: client, server: server)
        XCTAssertEqual(
            server.events.filter { $0 != "Received -1" },
            ["connecting 0 ", "ClientConnected 0", "message \(pos) 01", "message \(aim) 02", "tick 0"]
        )
    }

    func testServerEndRefusesAndClosesClients() throws {
        let pair = try GesNetworkPair.realtime(network: network())
        defer { pair.release() }
        let server = Recorder()
        server.onEvent = { kind, id in
            if kind == .connecting { try pair.server.refuse(client: id, detail: "full") }
        }
        _ = pair.client.send(route: commands, payload: [1])
        let (client, _) = try poll(pair, 10, server: server)
        XCTAssertEqual(client.events, ["Disconnected -1 network.refused True"])
        XCTAssertEqual(server.events, ["connecting 0 ", "ClientDisconnected 0 network.refused False"])
        XCTAssertEqual(pair.server.clientCount, 0)
        XCTAssertEqual(pair.client.send(route: commands, payload: [1]), .notConnected)
    }

    func testClosingAndReleasingEndsReportDisconnects() throws {
        let pair = try GesNetworkPair.synchronous(network: network())
        defer { pair.release() }
        var (client, server) = try poll(pair)
        _ = client.take()
        _ = server.take()
        pair.client.close("bye")
        try poll(pair, client: client, server: server)
        XCTAssertEqual(client.take(), ["Disconnected -1 network.closed False"])
        XCTAssertEqual(server.take(), ["ClientDisconnected 0 network.closed True"])
        XCTAssertEqual(pair.server.clientCount, 0)

        let second = try GesNetworkPair.synchronous(network: network())
        defer { second.release() }
        (client, server) = try poll(second)
        _ = server.take()
        second.client.release()
        assertThrows(.invalidOperation("")) { try second.client.poll(client.handle) }
        try second.server.poll(server.handle)
        XCTAssertEqual(server.take(), ["ClientDisconnected 0 network.transportClosed False"])
    }

    func testSynchronousPairAdvancesItsVirtualClock() throws {
        var options = GesNetworkOptions.inProcess()
        options.timeoutMicroseconds = 1_000_000
        options.keepaliveMicroseconds = 200_000
        let pair = try GesNetworkPair.synchronous(network: network(), options: options)
        defer { pair.release() }
        XCTAssertTrue(pair.isSynchronous)
        for _ in 0..<50 { try pair.advance(by: 100_000) }
        XCTAssertEqual(pair.now, 5_000_000)
        XCTAssertEqual(pair.client.connection.state, .connected, "Keepalives keep the session.")
        let realtime = try GesNetworkPair.realtime(network: network())
        defer { realtime.release() }
        XCTAssertFalse(realtime.isSynchronous)
        assertThrows(.invalidOperation("")) { try realtime.advance(by: 1) }
        let reentrant = Recorder()
        reentrant.onEvent = { _, _ in
            let inner = Recorder()
            try realtime.client.poll(inner.handle)
        }
        assertThrows(.invalidOperation("")) { try self.poll(realtime, 10, client: reentrant) }
    }

    func testEndsExposeTheirNetworkAndRejectAForeignTransport() throws {
        let network = try network()
        let pair = try GesNetworkPair.synchronous(network: network)
        defer { pair.release() }
        XCTAssertTrue(pair.client.network === network)
        XCTAssertTrue(pair.server.network === network)
        XCTAssertEqual(pair.client.nextDeadline, pair.client.connection.nextDeadline)
        assertThrows(.invalidArgument("")) { _ = try pair.server.connection(client: 7) }
        var options = GesNetworkOptions.inProcess()
        options.transport = .stream
        assertThrows(.invalidArgument("")) {
            _ = try GesNetworkClientEnd(network: network, connector: pair.client.connector, options: options, clock: GesVirtualClock())
        }
    }

    func testASynchronousDatagramPairAcknowledgesAtOnce() throws {
        var options = GesNetworkOptions.inProcess()
        options.transport = .datagram
        options.ackDelayMicroseconds = 10_000
        options.maxSendQueueBytes = 1
        let pair = try GesNetworkPair.synchronous(network: network(), options: options)
        defer { pair.release() }
        _ = try poll(pair)
        // The virtual clock stands still, so a delayed acknowledgement would keep the queue full until the pair advances.
        XCTAssertEqual(pair.client.send(route: commands, payload: [1]), .queued)
        XCTAssertEqual(pair.client.send(route: commands, payload: [2]), .queued)
        let (_, server) = try poll(pair)
        XCTAssertEqual(server.events.filter { $0.hasPrefix("message") }, ["message 0 01", "message 0 02"], server.events.joined(separator: " | "))
    }

    func testASynchronousPairHoldsPacketsForAnEndThatTakesNoBytes() throws {
        let network = try GesSchemaSet(parsing: "wire schema :Blob as { data: :List(:Number integer between 0 and 255) max 200000 }\nnetwork :Game as { client sends :Blob bulk }\n")
            .network("Game").bind()
        var options = GesNetworkOptions.inProcess()
        options.maxPacketBytes = 1200
        options.maxFramesPerSecond = 1
        options.deflateTransfers = false
        let pair = try GesNetworkPair.synchronous(network: network, options: options)
        defer { pair.release() }
        _ = try poll(pair)
        // The first transfer empties the server's frame bucket.
        XCTAssertEqual(pair.client.send(route: 0, payload: [1]), .queued)
        XCTAssertEqual(pair.client.send(route: 0, payload: [UInt8](repeating: 0, count: 150_000)), .queued)
        // The server takes one frame a second, so 64 packets wait for it and the rest of the transfer stays queued.
        XCTAssertGreaterThan(pair.client.connection.queuedSendBytes, 0)
        // Once the queue is empty, up to 64 packets still wait for the server's frames.
        for _ in 0..<300 { try pair.advance(by: 1_000_000) }
        let (_, server) = try poll(pair)
        XCTAssertEqual(pair.client.connection.queuedSendBytes, 0)
        XCTAssertEqual(pair.client.connection.state, .connected)
        XCTAssertEqual(server.events.filter { $0.hasPrefix("message 0 ") && $0.count == 10 + 300_000 }.count, 1)
    }

    func testBulkSendsBeforeTheHandshakeAreBounded() throws {
        let network = try GesSchemaSet(parsing: "wire schema :Blob as { data: :List(:Number integer between 0 and 255) max 60 }\nnetwork :Game as { client sends :Blob bulk }\n")
            .network("Game").bind()
        var options = GesNetworkOptions.inProcess()
        // The first transfer takes 39 bytes: its 34-byte header and its data.
        options.maxTransferBytes = 41
        let pair = try GesNetworkPair.realtime(network: network, options: options)
        defer { pair.release() }
        // As on the connection, the queued transfers of this side stay within its transfer limit.
        XCTAssertEqual(pair.client.send(route: 0, payload: [1, 2, 3, 4, 5]), .queued)
        XCTAssertEqual(pair.client.send(route: 0, payload: [6, 7, 8]), .queueFull)
        XCTAssertEqual(pair.client.send(route: 0, payload: [6, 7]), .queued)
        let (_, server) = try poll(pair, 50)
        XCTAssertEqual(
            server.events.filter { $0 != "Received -1" && $0 != "TransferProgress 0" },
            ["connecting 0 ", "ClientConnected 0", "message 0 0102030405", "message 0 0607"]
        )
    }

    func testEmptyReliableSendsBeforeTheHandshakeAreBoundedByCount() throws {
        let network = try GesSchemaSet(parsing: "wire schema :Empty as {}\nnetwork :Game as { client sends :Empty reliable }\n").network("Game").bind()
        var options = GesNetworkOptions.inProcess()
        options.maxSendQueueBytes = 1
        let pair = try GesNetworkPair.realtime(network: network, options: options)
        defer { pair.release() }
        // Messages without bytes never reach the byte limit; as on the connection, their count is bounded.
        for _ in 0..<65536 { XCTAssertEqual(pair.client.send(route: 0, payload: []), .queued) }
        XCTAssertEqual(pair.client.send(route: 0, payload: []), .queueFull)
    }

    func testAdvancingThePairDuringAPollThrows() throws {
        let pair = try GesNetworkPair.synchronous(network: network())
        defer { pair.release() }
        let client = Recorder()
        client.onEvent = { _, _ in try pair.advance(by: 1) }
        assertThrows(.invalidOperation("")) { try self.poll(pair, client: client) }
    }

    func testAThrowingConnectingHandlerStillAcceptsTheClient() throws {
        struct HandlerError: Error {}

        let pair = try GesNetworkPair.realtime(network: network())
        defer { pair.release() }
        let server = Recorder()
        server.onEvent = { kind, _ in
            if kind == .connecting { throw HandlerError() }
        }
        let client = Recorder()
        var thrown = false
        for _ in 0..<10 {
            try pair.client.poll(client.handle)
            do {
                try pair.server.poll(server.handle)
            } catch is HandlerError {
                thrown = true
            }
        }
        XCTAssertTrue(thrown)
        XCTAssertEqual(pair.client.connection.state, .connected)
        XCTAssertEqual(client.events, ["Connected -1"])
    }

    func testASendOutsideAPollBringsTheNextDeadlineForward() throws {
        let pair = try GesNetworkPair.realtime(network: network())
        defer { pair.release() }
        try poll(pair, 10)
        XCTAssertEqual(pair.client.connection.state, .connected)
        var idle = pair.client.nextDeadline
        _ = pair.client.send(route: commands, payload: [1])
        XCTAssertLessThan(pair.client.nextDeadline, idle, "The send is due with the next poll.")
        let server = Recorder()
        try pair.server.poll(server.handle)
        idle = pair.server.nextDeadline
        _ = pair.server.publish(route: log, payload: [2])
        XCTAssertLessThan(pair.server.nextDeadline, idle, "The publish is due with the next poll.")
    }

    func testAConnectionClosedByTheHandlerSkipsItsRemainingEvents() throws {
        let pair = try GesNetworkPair.synchronous(network: network())
        defer { pair.release() }
        var (client, server) = try poll(pair)
        _ = client.take()
        _ = server.take()
        server.onEvent = { kind, id in
            if kind == .message { try pair.server.close(client: id, detail: "undecodable") }
        }
        _ = pair.client.send(route: commands, payload: [1])
        _ = pair.client.send(route: commands, payload: [2])
        try pair.server.poll(server.handle)
        XCTAssertEqual(server.take(), ["message \(commands) 01", "ClientDisconnected 0 network.closed False", "Received -1"])

        let second = try GesNetworkPair.synchronous(network: network())
        defer { second.release() }
        (client, server) = try poll(second)
        _ = client.take()
        client.onEvent = { kind, _ in
            if kind == .message { second.client.close("undecodable") }
        }
        _ = second.server.publish(route: log, payload: [3])
        _ = second.server.publish(route: log, payload: [4])
        try second.client.poll(client.handle)
        XCTAssertEqual(client.take(), ["message \(log) 03", "Disconnected -1 network.closed False", "Received -1"])
    }

    func testAConnectionClosedInTheLateConnectedEventSkipsItsPendingEvents() throws {
        let pair = try GesNetworkPair.synchronous(network: network())
        defer { pair.release() }
        let server = Recorder()
        server.onEvent = { kind, id in
            if kind == .clientConnected { try pair.server.close(client: id, detail: "rejected") }
        }
        _ = pair.client.send(route: commands, payload: [1])
        try pair.server.poll(server.handle)
        XCTAssertEqual(server.take(), ["ClientConnected 0", "ClientDisconnected 0 network.closed False"])

        let second = try GesNetworkPair.synchronous(network: network())
        defer { second.release() }
        let client = Recorder()
        client.onEvent = { kind, _ in
            if kind == .connected { second.client.close("rejected") }
        }
        _ = second.server.publish(route: log, payload: [3])
        try second.client.poll(client.handle)
        XCTAssertEqual(client.take(), ["Connected -1", "Disconnected -1 network.closed False"])
    }
}
