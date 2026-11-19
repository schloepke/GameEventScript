// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptNetworkSockets
import GameEventScriptRuntime
import NIOCore
import XCTest

final class SocketEndTests: XCTestCase {
    private static let source = """
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)
        wire schema :State as { v: :Number integer between 0 and 255 }

        network :Game as {
          client sends :Commands reliable
          server publishes :State latest
        }
        """

    private static let commands = 0
    private static let state = 1
    private static let limit: Int64 = 10_000_000

    private static var now: Int64 { Int64(NIODeadline.now().uptimeNanoseconds / 1000) }

    private static func game() throws -> GesBoundNetwork { try GesSchemaSet(parsing: source).network("Game").bind() }

    private static func options(_ transport: GesNetworkTransport) -> GesNetworkOptions {
        var options = GesNetworkOptions()
        options.transport = transport
        options.clientSecurity = .unencrypted
        options.serverSecurity = .unencrypted
        return options
    }

    // Hands an end to a child task; the test touches it only there until the task ends.
    private struct Unchecked<Value>: @unchecked Sendable {
        let value: Value
    }

    private final class Recorder {
        var events: [String] = []

        func handle(_ event: GesNetworkEndEvent) {
            switch event.kind {
            case .received, .transferProgress: return
            case .transferCancelled: events.append("TransferCancelled \(event.transferId) \(event.code)")
            case .message: events.append("message \(event.route) \(event.payload.map { ($0 < 16 ? "0" : "") + String($0, radix: 16, uppercase: true) }.joined())")
            case .disconnected, .clientDisconnected: events.append("\(Self.name(event.kind)) \(event.code)")
            default: events.append(Self.name(event.kind))
            }
        }

        static func name(_ kind: GesNetworkEndEventKind) -> String {
            let text = "\(kind)"
            return text.prefix(1).uppercased() + text.dropFirst()
        }
    }

    private static func transport(_ transport: GesNetworkTransport) async throws -> (GesNetworkSocketListener, GesNetworkSocketConnector) {
        let network = try game()
        let listener: GesNetworkSocketListener
        switch transport {
        case .stream: listener = try await .listenTcp(host: "127.0.0.1", port: 0, network: network, options: options(transport))
        case .message: listener = try await .listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: network, options: options(transport))
        case .datagram: listener = try await .listenUdp(host: "127.0.0.1", port: 0, network: network, options: options(transport))
        }
        let port = listener.localEndPoint.port!
        let connector: GesNetworkSocketConnector
        switch transport {
        case .stream: connector = try .tcp(host: "127.0.0.1", port: port)
        case .message: connector = try .webSocket("ws://127.0.0.1:\(port)/game")
        case .datagram: connector = try .udp(host: "127.0.0.1", port: port)
        }
        return (listener, connector)
    }

    // Waits for either end, at most 50 ms.
    private static func waitEither(_ client: GesNetworkClientEnd, _ server: GesNetworkServerEnd) async throws {
        let client = Unchecked(value: client)
        let server = Unchecked(value: server)
        await withTaskGroup(of: Void.self) { group in
            group.addTask { try? await client.value.wait() }
            group.addTask { try? await server.value.wait() }
            group.addTask { try? await Task.sleep(nanoseconds: 50_000_000) }
            await group.next()
            group.cancelAll()
        }
    }

    // Polls both ends, waiting for either between polls, until the condition holds.
    private static func run(
        _ client: GesNetworkClientEnd,
        _ clientEvents: Recorder,
        _ server: GesNetworkServerEnd,
        _ serverEvents: Recorder,
        until done: () -> Bool
    ) async throws {
        let deadline = now + limit
        while now < deadline {
            try client.poll(clientEvents.handle)
            try server.poll(serverEvents.handle)
            if done() { return }
            try await waitEither(client, server)
        }
        XCTFail("Not done in time: client \(clientEvents.events); server \(serverEvents.events).")
    }

    private func endsConnectExchangeAndClose(_ transport: GesNetworkTransport) async throws {
        let (listener, connector) = try await Self.transport(transport)
        let clock = GesSystemClock()
        let server = try GesNetworkServerEnd(network: Self.game(), acceptor: listener, clock: clock)
        let client = try GesNetworkClientEnd(network: Self.game(), connector: connector, options: Self.options(transport), token: Array("tok".utf8), clock: clock)
        defer {
            client.release()
            server.release()
        }
        XCTAssertTrue(client.connector === connector)
        XCTAssertTrue(server.acceptor === listener)
        let clientEvents = Recorder()
        let serverEvents = Recorder()

        // Queued before the handshake, sent once connected.
        XCTAssertEqual(client.send(route: Self.commands, payload: [1]), .queued)
        try await Self.run(client, clientEvents, server, serverEvents) { serverEvents.events.contains("message 0 01") }
        XCTAssertEqual(clientEvents.events, ["Connected"])
        XCTAssertEqual(serverEvents.events, ["Connecting", "ClientConnected", "message 0 01"])

        XCTAssertEqual(server.publish(route: Self.state, payload: [7]), .queued)
        try await Self.run(client, clientEvents, server, serverEvents) { clientEvents.events.contains("message 1 07") }

        client.close("bye")
        try await Self.run(client, clientEvents, server, serverEvents) { server.clientCount == 0 }
        XCTAssertEqual(clientEvents.events, ["Connected", "message 1 07", "Disconnected network.closed"])
        XCTAssertEqual(serverEvents.events.last, "ClientDisconnected network.closed")
    }

    func testEndsConnectExchangeAndCloseOverTcp() async throws { try await endsConnectExchangeAndClose(.stream) }

    private static func bulk() throws -> GesBoundNetwork {
        try GesSchemaSet(parsing: "wire schema :Blob as { data: :List(:Number integer between 0 and 255) max 60 }\nnetwork :Game as {\n  client sends :Blob bulk\n  server sends :Blob bulk\n}\n")
            .network("Game").bind()
    }

    // A TCP client end and server end whose transfer limits differ.
    private static func bulkEnds(client clientTransfer: Int64, server serverTransfer: Int64) async throws -> (GesNetworkClientEnd, GesNetworkServerEnd) {
        var serverOptions = options(.stream)
        serverOptions.maxTransferBytes = serverTransfer
        var clientOptions = options(.stream)
        clientOptions.maxTransferBytes = clientTransfer
        let listener = try await GesNetworkSocketListener.listenTcp(host: "127.0.0.1", port: 0, network: bulk(), options: serverOptions)
        let clock = GesSystemClock()
        let server = try GesNetworkServerEnd(network: bulk(), acceptor: listener, clock: clock)
        let client = try GesNetworkClientEnd(network: bulk(), connector: GesNetworkSocketConnector.tcp(host: "127.0.0.1", port: listener.localEndPoint.port!), options: clientOptions, clock: clock)
        return (client, server)
    }

    func testAKeptTransferBeyondThePeersLimitIsCancelled() async throws {
        // Kept before the handshake against this side's limit, the 6-byte transfer exceeds the server's limit of 4 bytes.
        let (client, server) = try await Self.bulkEnds(client: 1000, server: 4)
        defer {
            client.release()
            server.release()
        }
        XCTAssertEqual(client.send(route: 0, payload: [1, 2]), .queued)
        XCTAssertEqual(client.send(route: 0, payload: [1, 2, 3, 4, 5, 6]), .queued)
        XCTAssertEqual(client.send(route: 0, payload: [3, 4]), .queued)
        let clientEvents = Recorder()
        let serverEvents = Recorder()
        try await Self.run(client, clientEvents, server, serverEvents) {
            serverEvents.events.contains("message 0 0304") && clientEvents.events.count >= 2 && serverEvents.events.count >= 5
        }
        XCTAssertEqual(clientEvents.events, ["Connected", "TransferCancelled 1 network.transferTooLarge"])
        XCTAssertEqual(serverEvents.events, ["Connecting", "ClientConnected", "message 0 0102", "TransferCancelled 1 network.transferTooLarge", "message 0 0304"])
    }

    func testASendToEveryClientReportsAPayloadBeyondAClientsLimit() async throws {
        let (client, server) = try await Self.bulkEnds(client: 4, server: 1000)
        defer {
            client.release()
            server.release()
        }
        let clientEvents = Recorder()
        let serverEvents = Recorder()
        try await Self.run(client, clientEvents, server, serverEvents) { clientEvents.events.contains("Connected") && serverEvents.events.contains("ClientConnected") }
        XCTAssertEqual(server.send(route: 1, payload: [1, 2, 3, 4, 5, 6]), .messageTooLarge)
        XCTAssertEqual(server.send(route: 1, payload: [1, 2]), .queued)
        try await Self.run(client, clientEvents, server, serverEvents) { clientEvents.events.contains("message 1 0102") }
        XCTAssertEqual(clientEvents.events, ["Connected", "message 1 0102"])
    }

    func testEndsConnectExchangeAndCloseOverWebSocket() async throws { try await endsConnectExchangeAndClose(.message) }

    func testEndsConnectExchangeAndCloseOverUdp() async throws { try await endsConnectExchangeAndClose(.datagram) }

    func testAConnectorThatCannotOpenEndsTheTransport() async throws {
        let client = try GesNetworkClientEnd(
            network: Self.game(),
            connector: GesNetworkSocketConnector.tcp(host: "127.0.0.1", port: 1),
            options: Self.options(.stream),
            clock: GesSystemClock()
        )
        defer { client.release() }
        let events = Recorder()
        let deadline = Self.now + Self.limit
        while events.events.isEmpty && Self.now < deadline {
            try client.poll(events.handle)
            if events.events.isEmpty { try await client.wait() }
        }

        XCTAssertEqual(events.events, ["Disconnected network.transportClosed"])
    }

    func testWaitReturnsWhenBytesArrive() async throws {
        let (listener, connector) = try await Self.transport(.stream)
        let clock = GesSystemClock()
        let server = try GesNetworkServerEnd(network: Self.game(), acceptor: listener, clock: clock)
        let client = try GesNetworkClientEnd(network: Self.game(), connector: connector, options: Self.options(.stream), clock: clock)
        defer {
            client.release()
            server.release()
        }
        let clientEvents = Recorder()
        let serverEvents = Recorder()
        try await Self.run(client, clientEvents, server, serverEvents) { server.clientCount == 1 && clientEvents.events.count == 1 }

        // Without input the server end has no deadline before its keepalive; the client's next poll sends and wakes it.
        let sending = Unchecked(value: (client, clientEvents))
        let sender = Task.detached {
            try await Task.sleep(nanoseconds: 100_000_000)
            let (client, events) = sending.value
            _ = client.send(route: Self.commands, payload: [3])
            try client.poll(events.handle)
        }
        let deadline = Self.now + Self.limit
        while !serverEvents.events.contains("message 0 03") && Self.now < deadline {
            try await server.wait()
            try server.poll(serverEvents.handle)
        }

        try await sender.value
        XCTAssertTrue(serverEvents.events.contains("message 0 03"))

        let pair = try GesNetworkPair.synchronous(network: Self.game())
        defer { pair.release() }
        do {
            try await pair.client.wait()
            XCTFail("A pair end has no socket to wait for.")
        } catch GesApiError.invalidArgument {}
        XCTAssertThrowsError(try GesNetworkSocketConnector.udp(host: "x", port: 0)) { XCTAssertTrue(Self.isInvalidArgument($0)) }
        XCTAssertThrowsError(try GesNetworkSocketConnector.webSocket("http://x/")) { XCTAssertTrue(Self.isInvalidArgument($0)) }
    }

    private static func isInvalidArgument(_ error: any Error) -> Bool {
        if case GesApiError.invalidArgument = error { return true }
        return false
    }
}
