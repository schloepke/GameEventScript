// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import NIOCore
import NIOPosix
import NIOWebSocket
import XCTest

@testable import GameEventScriptNetworkSockets

final class SocketTests: XCTestCase {
    private static let source = """
        wire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends :Commands reliable
          server sends :Blob reliable
        }
        """

    private static let commands = 0
    private static let blob = 1
    private static let limit: Int64 = 10_000_000
    private static let token = Array("tok".utf8)

    private static var now: Int64 { Int64(NIODeadline.now().uptimeNanoseconds / 1000) }

    private static func game() throws -> GesBoundNetwork {
        try GesSchemaSet(sources: [GesSchemaSource(name: "game.gess", text: source)]).network("Game").bind()
    }

    private static func options(_ transport: GesNetworkTransport) -> GesNetworkOptions {
        var options = GesNetworkOptions()
        options.transport = transport
        options.clientSecurity = .unencrypted
        options.serverSecurity = .unencrypted
        return options
    }

    private static func hexadecimal(_ bytes: UnsafeRawBufferPointer) -> String {
        bytes.map { $0 < 16 ? "0" + String($0, radix: 16, uppercase: true) : String($0, radix: 16, uppercase: true) }.joined()
    }

    private static func next(_ connection: GesNetworkConnection) -> String? {
        let next = connection.nextEvent()
        switch next.kind {
        case .none: return nil
        case .connecting: return "connecting " + String(decoding: next.payload, as: UTF8.self)
        case .connected: return "connected"
        case .message: return "message \(next.route) \(hexadecimal(next.payload))"
        case .closed: return "closed \(next.code) \(next.byPeer ? "peer" : "local")"
        case .transferProgress: return "progress \(next.route) \(next.transferId) \(next.bytes)/\(next.total)"
        case .transferCancelled: return "cancelled \(next.route) \(next.transferId) \(next.code)"
        case .tick: return "tick \(next.tickNumber)"
        }
    }

    // Updates the sessions until the session's next event arrives.
    private static func waitFor(_ session: GesNetworkSocketSession, _ others: GesNetworkSocketSession...) throws -> String {
        let deadline = now + limit
        while now < deadline {
            for other in others { try other.update(now: now) }
            try session.update(now: now)
            if let next = next(session.connection) { return next }
            usleep(1000)
        }
        XCTFail("No event arrived in time.")
        return ""
    }

    // Updates the given client sessions, which a datagram handshake needs, until the listener offers a client.
    private static func acceptOne(_ listener: GesNetworkSocketListener, _ clients: GesNetworkSocketSession...) throws -> GesNetworkSocketSession {
        let deadline = now + limit
        while now < deadline {
            for client in clients { try client.update(now: now) }
            if let session = try listener.accept(now: now) { return session }
            usleep(1000)
        }
        throw GesNetworkSocketError("No client was accepted in time.")
    }

    private static func roundTrip(_ client: GesNetworkSocketSession, _ server: GesNetworkSocketSession) throws {
        XCTAssertEqual(try waitFor(server, client), "connecting tok")
        try server.connection.accept()
        XCTAssertEqual(try waitFor(server, client), "connected")
        XCTAssertEqual(try waitFor(client, server), "connected")

        XCTAssertEqual(client.connection.send(route: commands, payload: [1]), .queued)
        XCTAssertEqual(try waitFor(server, client), "message 0 01")

        // Larger than one packet, so the message arrives in fragments.
        let blob = (0..<6000).map { UInt8(truncatingIfNeeded: $0 * 7 + 3) }
        XCTAssertEqual(server.connection.send(route: Self.blob, payload: blob), .queued)
        XCTAssertEqual(try waitFor(client, server), "message 1 " + blob.withUnsafeBytes { hexadecimal($0) })

        server.connection.close("bye")
        XCTAssertEqual(next(server.connection), "closed network.closed local")
        XCTAssertEqual(try waitFor(client, server), "closed network.closed peer")
    }

    // Opens a WebSocket without a connection, for frames a session never sends.
    private static func rawWebSocket(port: Int, target: String) async throws -> Channel {
        let endpoint = try GesNetworkSocketEndpoint("ws://127.0.0.1:\(port)\(target)")
        let loop = MultiThreadedEventLoopGroup.singleton.next()
        let upgraded = loop.makePromise(of: Void.self)
        let inbox = GesNetworkSocketInbox()
        let channel = try await ClientBootstrap(group: loop)
            .channelInitializer { channel in
                channel.eventLoop.makeCompletedFuture {
                    try channel.pipeline.syncOperations.addHandler(GesNetworkSocketClientUpgradeHandler(endpoint: endpoint, inbox: inbox, limit: 65536, upgraded: upgraded))
                }
            }
            .connect(host: endpoint.host, port: endpoint.port).get()
        try await upgraded.futureResult.get()
        return channel
    }

    // Opens a UDP socket without a session, for datagrams a session never sends.
    private static func rawDatagram(host: String, port: Int) async throws -> (Channel, GesNetworkSocketInbox) {
        let inbox = GesNetworkSocketInbox()
        let channel = try await DatagramBootstrap(group: MultiThreadedEventLoopGroup.singleton)
            .channelInitializer { channel in
                channel.eventLoop.makeCompletedFuture { try channel.pipeline.syncOperations.addHandler(RawDatagramHandler(inbox: inbox)) }
            }
            .connect(host: host, port: port).get()
        return (channel, inbox)
    }

    private static func send(_ channel: Channel, _ bytes: some Sequence<UInt8>) async throws {
        try await channel.writeAndFlush(ByteBuffer(bytes: bytes)).get()
    }

    // Waits until a socket replaces a closed one.
    private static func replacement(of first: Channel, _ current: () -> Channel?) throws -> Channel {
        let deadline = now + limit
        while now < deadline {
            if let socket = current(), socket !== first { return socket }
            usleep(1000)
        }
        throw GesNetworkSocketError("No socket replaced the closed one in time.")
    }

    private static func waitClosed(_ socket: Channel) throws {
        let deadline = now + limit
        while socket.isActive && now < deadline { usleep(1000) }
        XCTAssertFalse(socket.isActive)
    }

    // Waits for the next datagram of a raw socket.
    private static func receive(_ inbox: GesNetworkSocketInbox) throws -> [ByteBuffer] {
        let deadline = now + limit
        var chunks: [ByteBuffer] = []
        while now < deadline {
            _ = inbox.take(into: &chunks)
            if !chunks.isEmpty { return chunks }
            usleep(1000)
        }
        throw GesNetworkSocketError("No datagram arrived in time.")
    }

    // Runs the client side of a datagram handshake by hand on a raw socket until the listener offers the server session.
    private static func handshake(_ channel: Channel, _ inbox: GesNetworkSocketInbox, _ connection: GesNetworkConnection, _ listener: GesNetworkSocketListener) async throws
        -> GesNetworkSocketSession
    {
        var buffer = [UInt8](repeating: 0, count: 2048)
        var chunks: [ByteBuffer] = []
        let deadline = now + limit
        while now < deadline {
            while true {
                let length = try connection.transmit(into: &buffer)
                if length == 0 { break }
                try await send(channel, buffer[0..<length])
            }
            if let session = try listener.accept(now: now) {
                XCTAssertEqual(try waitFor(session), "connecting tok")
                return session
            }

            _ = inbox.take(into: &chunks)
            for chunk in chunks { try chunk.withUnsafeReadableBytes { try connection.receive($0, now: now) } }
            chunks.removeAll()
            try connection.advance(now)
            usleep(1000)
        }
        throw GesNetworkSocketError("No client was accepted in time.")
    }

    private static let serverKey = try! GesNetworkKey(privateKey: (1...32).map { UInt8($0) })

    private static func secureServer(_ transport: GesNetworkTransport) -> GesNetworkOptions {
        var options = GesNetworkOptions()
        options.transport = transport
        options.serverSecurity = .key(serverKey)
        return options
    }

    private static func secureClient(_ transport: GesNetworkTransport) -> GesNetworkOptions {
        var options = GesNetworkOptions()
        options.transport = transport
        options.clientSecurity = try! .pinned(serverKey.publicKey)
        return options
    }

    private static func frame(_ opcode: WebSocketOpcode, _ bytes: [UInt8]) -> WebSocketFrame {
        WebSocketFrame(fin: true, opcode: opcode, maskKey: .random(), data: ByteBuffer(bytes: bytes))
    }

    func testTcpCarriesASession() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenTcp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.stream))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.stream))
        let client = try await GesNetworkSocketSession.connectTcp(host: "127.0.0.1", port: listener.localEndPoint.port!, connection: connection)
        let server = try Self.acceptOne(listener)
        try Self.roundTrip(client, server)
        client.close()
        server.close()
        listener.close()
    }

    func testWebSocketCarriesASession() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: network, options: Self.options(.message))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.message))
        let url = "ws://127.0.0.1:\(listener.localEndPoint.port!)/game?room=1"
        let client = try await GesNetworkSocketSession.connectWebSocket(url: url, connection: connection)
        let server = try Self.acceptOne(listener)
        try Self.roundTrip(client, server)
        client.close()
        server.close()
        listener.close()
    }

    func testUdpCarriesASession() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenUdp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.datagram))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.datagram))
        let client = try await GesNetworkSocketSession.connectUdp(host: "127.0.0.1", port: listener.localEndPoint.port!, connection: connection)
        let server = try Self.acceptOne(listener, client)
        try Self.roundTrip(client, server)
        client.close()
        server.close()
        listener.close()
    }

    func testTcpCarriesAnEncryptedSession() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenTcp(host: "127.0.0.1", port: 0, network: network, options: Self.secureServer(.stream))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.secureClient(.stream))
        let client = try await GesNetworkSocketSession.connectTcp(host: "127.0.0.1", port: listener.localEndPoint.port!, connection: connection)
        let server = try Self.acceptOne(listener)
        try Self.roundTrip(client, server)
        XCTAssertEqual(client.connection.security, .verified)
        client.close()
        server.close()
        listener.close()
    }

    func testUdpCarriesAnEncryptedSession() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenUdp(host: "127.0.0.1", port: 0, network: network, options: Self.secureServer(.datagram))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.secureClient(.datagram))
        let client = try await GesNetworkSocketSession.connectUdp(host: "127.0.0.1", port: listener.localEndPoint.port!, connection: connection)
        let server = try Self.acceptOne(listener, client)
        try Self.roundTrip(client, server)
        XCTAssertEqual(client.connection.security, .verified)
        client.close()
        server.close()
        listener.close()
    }

    func testAListenerNeedsAServerSecurity() async throws {
        let network = try Self.game()
        var options = GesNetworkOptions()
        options.transport = .stream
        await assertInvalidArgument { _ = try await GesNetworkSocketListener.listenTcp(host: "127.0.0.1", port: 0, network: network, options: options) }
    }

    func testSocketTransportsRefuseInsecureTestKeys() async throws {
        let network = try Self.game()
        var server = Self.secureServer(.stream)
        server.insecureTestKeySeed = 1
        await assertInvalidArgument { _ = try await GesNetworkSocketListener.listenTcp(host: "127.0.0.1", port: 0, network: network, options: server) }
        server.transport = .datagram
        await assertInvalidArgument { _ = try await GesNetworkSocketListener.listenUdp(host: "127.0.0.1", port: 0, network: network, options: server) }
        for transport in [GesNetworkTransport.stream, .message, .datagram] {
            var options = Self.secureClient(transport)
            options.insecureTestKeySeed = 1
            XCTAssertTrue(try GesNetworkConnection.makeClient(network: network, now: 0, options: options).usesInsecureTestKeys)
            await assertInvalidArgument {
                let connection = try GesNetworkConnection.makeClient(network: network, now: 0, options: options)
                switch transport {
                case .stream: _ = try await GesNetworkSocketSession.connectTcp(host: "127.0.0.1", port: 1, connection: connection)
                case .message: _ = try await GesNetworkSocketSession.connectWebSocket(url: "ws://127.0.0.1:1/game", connection: connection)
                case .datagram: _ = try await GesNetworkSocketSession.connectUdp(host: "127.0.0.1", port: 1, connection: connection)
                }
            }
        }
    }

    func testUdpCarriesASessionOverIpv6() async throws {
        let network = try Self.game()
        let listener: GesNetworkSocketListener
        do {
            listener = try await GesNetworkSocketListener.listenUdp(host: "::1", port: 0, network: network, options: Self.options(.datagram))
        } catch {
            // Containers often have no IPv6 loopback.
            throw XCTSkip("IPv6 loopback unavailable: \(error)")
        }
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.datagram))
        let client = try await GesNetworkSocketSession.connectUdp(host: "::1", port: listener.localEndPoint.port!, connection: connection)
        let server = try Self.acceptOne(listener, client)
        try Self.roundTrip(client, server)
        client.close()
        server.close()
        listener.close()
    }

    func testAUdpClientIgnoresOtherAddresses() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenUdp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.datagram))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.datagram))
        let client = try await GesNetworkSocketSession.connectUdp(host: "127.0.0.1", port: listener.localEndPoint.port!, connection: connection)
        let peer = try XCTUnwrap(client.peer)
        let local = try XCTUnwrap(peer.socket?.localAddress?.port)
        let (stranger, _) = try await Self.rawDatagram(host: "127.0.0.1", port: local)
        try await Self.send(stranger, [2, 24, 1, 2, 3])
        try await Self.send(stranger, [UInt8](repeating: 7, count: 1200))
        usleep(50_000)
        var chunks: [ByteBuffer] = []
        let (ended, _) = peer.inbox.take(into: &chunks)
        XCTAssertTrue(chunks.isEmpty)
        XCTAssertFalse(ended)
        let server = try Self.acceptOne(listener, client)
        try Self.roundTrip(client, server)
        try await stranger.close()
        client.close()
        server.close()
        listener.close()
    }

    func testAUdpClientRebindsAClosedSocket() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenUdp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.datagram))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.datagram))
        let client = try await GesNetworkSocketSession.connectUdp(host: "127.0.0.1", port: listener.localEndPoint.port!, connection: connection)
        let server = try Self.acceptOne(listener, client)
        let peer = try XCTUnwrap(client.peer)
        let first = try XCTUnwrap(peer.socket)
        let port = first.localAddress?.port

        // SwiftNIO closes a socket on fatal send errors; the client binds the same local address again.
        try await first.close()
        let socket = try Self.replacement(of: first) { peer.socket }
        XCTAssertEqual(socket.localAddress?.port, port)
        try Self.roundTrip(client, server)
        client.close()
        try Self.waitClosed(socket)
        server.close()
        listener.close()
    }

    func testAUdpListenerRebindsAClosedSocket() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenUdp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.datagram))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.datagram))
        let client = try await GesNetworkSocketSession.connectUdp(host: "127.0.0.1", port: listener.localEndPoint.port!, connection: connection)
        let server = try Self.acceptOne(listener, client)
        let first = try XCTUnwrap(listener.socket)

        // SwiftNIO closes a socket on fatal send errors; the listener binds the same local address again and keeps
        // its sessions.
        try await first.close()
        let socket = try Self.replacement(of: first) { listener.socket }
        XCTAssertEqual(socket.localAddress?.port, listener.localEndPoint.port)
        try Self.roundTrip(client, server)
        server.close()
        listener.close()
        try Self.waitClosed(socket)
        client.close()
    }

    func testUdpValidatesAnUnknownAddressWithoutState() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenUdp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.datagram))
        let (socket, inbox) = try await Self.rawDatagram(host: "127.0.0.1", port: listener.localEndPoint.port!)

        // Anything but a full-size hello is dropped without an answer.
        try await Self.send(socket, [3, 0, 0, 0])
        var hello = [UInt8](repeating: 0, count: 2048)
        let length = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.datagram)).transmit(into: &hello)
        XCTAssertEqual(length, 1200)
        try await Self.send(socket, hello[0..<length])
        let received = try Self.receive(inbox)
        XCTAssertEqual(received.count, 1)
        XCTAssertEqual(received[0].readableBytes, GesNetworkCookies.retryBytes)
        XCTAssertEqual(received[0].getBytes(at: received[0].readerIndex, length: 2), [2, 24])
        XCTAssertNil(try listener.accept(now: Self.now))
        try await socket.close()
        listener.close()
    }

    func testAClosedUdpSessionReleasesItsAddress() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenUdp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.datagram))
        let (socket, inbox) = try await Self.rawDatagram(host: "127.0.0.1", port: listener.localEndPoint.port!)
        for _ in 0..<2 {
            let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.datagram))
            let server = try await Self.handshake(socket, inbox, connection, listener)
            try server.connection.refuse("full")
            try server.update(now: Self.now)
            XCTAssertEqual(server.connection.state, .closed)
            let deadline = Self.now + Self.limit
            while connection.state != .closed && Self.now < deadline {
                for chunk in try Self.receive(inbox) { try chunk.withUnsafeReadableBytes { try connection.receive($0, now: Self.now) } }
            }
            XCTAssertEqual(connection.state, .closed)
            server.close()
        }
        try await socket.close()
        listener.close()
    }

    func testAnOversizedDatagramIsTooLarge() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenUdp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.datagram))
        let (socket, inbox) = try await Self.rawDatagram(host: "127.0.0.1", port: listener.localEndPoint.port!)
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.datagram))
        let server = try await Self.handshake(socket, inbox, connection, listener)
        try await Self.send(socket, [UInt8](repeating: 0, count: server.connection.maxPacketBytes + 100))
        XCTAssertEqual(try Self.waitFor(server), "closed network.packetTooLarge local")
        try await socket.close()
        server.close()
        listener.close()
    }

    func testASlowPeerFillsTheSendQueue() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenTcp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.stream))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.stream))
        let client = try await GesNetworkSocketSession.connectTcp(host: "127.0.0.1", port: listener.localEndPoint.port!, connection: connection)
        let server = try Self.acceptOne(listener)
        XCTAssertEqual(try Self.waitFor(server, client), "connecting tok")
        try server.connection.accept()
        XCTAssertEqual(try Self.waitFor(server, client), "connected")
        XCTAssertEqual(try Self.waitFor(client, server), "connected")

        // The client stops reading, so the socket fills and the session must leave further packets in the connection.
        let blob = [UInt8](repeating: 7, count: 1000)
        let deadline = Self.now + Self.limit
        var sent = 0
        var full = false
        while !full && sent < 64 << 20 && Self.now < deadline {
            try server.update(now: Self.now)
            let status = server.connection.send(route: Self.blob, payload: blob)
            full = status == .queueFull
            if !full { XCTAssertEqual(status, .queued) }
            sent += blob.count
        }
        XCTAssertTrue(full, "The send queue never filled.")

        // The close packet leaves past the full socket's bound and reaches the client behind every queued message.
        server.connection.close("done")
        try server.transmit()
        var last = ""
        while !last.hasPrefix("closed") && Self.now < deadline + Self.limit { last = try Self.waitFor(client, server) }
        XCTAssertEqual(last, "closed network.closed peer")
        client.close()
        server.close()
        listener.close()
    }

    func testTheEndOfTheTransportClosesTheConnection() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenTcp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.stream))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.stream))
        let client = try await GesNetworkSocketSession.connectTcp(host: "127.0.0.1", port: listener.localEndPoint.port!, connection: connection)
        let server = try Self.acceptOne(listener)
        XCTAssertEqual(try Self.waitFor(server, client), "connecting tok")
        client.close()
        XCTAssertEqual(try Self.waitFor(server), "closed network.transportClosed local")
        XCTAssertThrowsError(try client.update(now: Self.now))
        server.close()
        listener.close()
    }

    func testAWebSocketTextMessageIsMalformed() async throws {
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: Self.game(), options: Self.options(.message))
        let socket = try await Self.rawWebSocket(port: listener.localEndPoint.port!, target: "/game")
        let server = try Self.acceptOne(listener)
        try await socket.writeAndFlush(Self.frame(.text, Array("hello".utf8))).get()
        XCTAssertEqual(try Self.waitFor(server), "closed network.malformed local")
        try await socket.close()
        server.close()
        listener.close()
    }

    func testAnOversizedWebSocketMessageIsTooLarge() async throws {
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: Self.game(), options: Self.options(.message))
        let socket = try await Self.rawWebSocket(port: listener.localEndPoint.port!, target: "/game")
        let server = try Self.acceptOne(listener)
        try await socket.writeAndFlush(Self.frame(.binary, [UInt8](repeating: 0, count: 16385))).get()
        XCTAssertEqual(try Self.waitFor(server), "closed network.packetTooLarge local")
        try await socket.close()
        server.close()
        listener.close()
    }

    func testAFragmentedWebSocketMessageIsOnePacket() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: network, options: Self.options(.message))
        let socket = try await Self.rawWebSocket(port: listener.localEndPoint.port!, target: "/game")
        let server = try Self.acceptOne(listener)
        // A Hello split over two frames: a single byte never forms a valid packet on its own.
        let client = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.message))
        var hello = [UInt8](repeating: 0, count: 2048)
        let length = try client.transmit(into: &hello)
        // A write completes only once flushed, so the first frame is awaited after the second.
        let first = socket.write(WebSocketFrame(fin: false, opcode: .binary, maskKey: .random(), data: ByteBuffer(bytes: hello[0..<1])))
        try await socket.writeAndFlush(WebSocketFrame(fin: true, opcode: .continuation, maskKey: .random(), data: ByteBuffer(bytes: hello[1..<length]))).get()
        try await first.get()
        XCTAssertEqual(try Self.waitFor(server), "connecting tok")
        try await socket.close()
        server.close()
        listener.close()
    }

    // Opens a raw TCP socket that sends nothing.
    private static func rawConnect(_ port: Int) async throws -> Channel {
        try await ClientBootstrap(group: MultiThreadedEventLoopGroup.singleton).connect(host: "127.0.0.1", port: port).get()
    }

    // True when the server ends the socket in time.
    private static func ended(_ channel: Channel) -> Bool {
        let deadline = now + limit
        while now < deadline {
            if !channel.isActive { return true }
            usleep(1000)
        }
        return false
    }

    func testAnAddressHoldsAtMostEightWaitingClients() async throws {
        let listener = try await GesNetworkSocketListener.listenTcp(host: "127.0.0.1", port: 0, network: Self.game(), options: Self.options(.stream))
        var clients: [Channel] = []
        for _ in 0..<10 { clients.append(try await Self.rawConnect(listener.localEndPoint.port!)) }
        // The listener closes two sockets and keeps eight for the host.
        let deadline = Self.now + Self.limit
        while Self.now < deadline && clients.filter({ !$0.isActive }).count < 2 { usleep(1000) }
        XCTAssertEqual(clients.filter { !$0.isActive }.count, 2)
        var sessions: [GesNetworkSocketSession] = []
        while Self.now < deadline && sessions.count < 8 {
            if let session = try listener.accept(now: Self.now) { sessions.append(session) } else { usleep(1000) }
        }
        XCTAssertEqual(sessions.count, 8)
        XCTAssertNil(try listener.accept(now: Self.now))
        for session in sessions { session.close() }
        // Taken clients give their places back.
        let another = try await Self.rawConnect(listener.localEndPoint.port!)
        try Self.acceptOne(listener).close()
        try await another.close()
        for client in clients { try? await client.close() }
        listener.close()
    }

    func testUpgradingClientsCountForTheirAddress() async throws {
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: Self.game(), options: Self.options(.message))
        var clients: [Channel] = []
        for _ in 0..<8 { clients.append(try await Self.rawConnect(listener.localEndPoint.port!)) }
        // Eight sockets wait in their upgrade without sending a request; the ninth is closed.
        let ninth = try await Self.rawConnect(listener.localEndPoint.port!)
        XCTAssertTrue(Self.ended(ninth))
        XCTAssertTrue(clients.allSatisfy { $0.isActive })
        XCTAssertNil(try listener.accept(now: Self.now))
        for client in clients { try? await client.close() }
        listener.close()
    }

    func testAnotherPathIsNotFound() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: network, options: Self.options(.message))
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.message))
        do {
            _ = try await GesNetworkSocketSession.connectWebSocket(url: "ws://127.0.0.1:\(listener.localEndPoint.port!)/other", connection: connection)
            XCTFail("The upgrade succeeded.")
        } catch let error as GesNetworkSocketError {
            XCTAssertTrue(error.message.contains("404"), error.message)
        }
        XCTAssertNil(try listener.accept(now: Self.now))
        listener.close()
        XCTAssertThrowsError(try listener.accept(now: Self.now))
    }

    func testTheUpgradeAnswerFollowsRfc6455() {
        let request =
            "GET /game HTTP/1.1\r\nHost: x\r\nUpgrade: websocket\r\nConnection: keep-alive, Upgrade\r\n"
            + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n"
        let answer = GesNetworkSocketUpgrade.answer(request, path: "/game")
        XCTAssertTrue(answer.upgrade)
        XCTAssertTrue(answer.text.contains("Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n"))
        let old = GesNetworkSocketUpgrade.answer(request.replacingAll("Version: 13", "Version: 8"), path: "/game")
        XCTAssertFalse(old.upgrade)
        XCTAssertTrue(old.text.hasPrefix("HTTP/1.1 426"))
        XCTAssertTrue(GesNetworkSocketUpgrade.answer(request.replacingAll("Upgrade: websocket\r\n", ""), path: "/game").text.hasPrefix("HTTP/1.1 400"))
        XCTAssertTrue(GesNetworkSocketUpgrade.answer(request.replacingAll("GET", "POST"), path: "/game").text.hasPrefix("HTTP/1.1 400"))
        XCTAssertTrue(GesNetworkSocketUpgrade.answer(request.replacingAll("/game", "/game?room=1"), path: "/game").upgrade)
        XCTAssertTrue(GesNetworkSocketUpgrade.answer(request, path: "/other").text.hasPrefix("HTTP/1.1 404"))
    }

    func testTheHashesMatchTheirStandards() {
        XCTAssertEqual(GesNetworkSocketUpgrade.base64(GesNetworkSocketUpgrade.sha1(Array("abc".utf8))), "qZk+NkcGgWq6PiVxeFDCbJzQ2J0=")
        XCTAssertEqual(GesNetworkSocketUpgrade.base64(GesNetworkSocketUpgrade.sha1([])), "2jmj7l5rSw0yVb/vlWAYkK/YBwk=")
        XCTAssertEqual(GesNetworkSocketUpgrade.base64(Array("f".utf8)), "Zg==")
        XCTAssertEqual(GesNetworkSocketUpgrade.base64(Array("fo".utf8)), "Zm8=")
        XCTAssertEqual(GesNetworkSocketUpgrade.base64(Array("foo".utf8)), "Zm9v")
    }

    func testUrlsAreParsed() throws {
        let plain = try GesNetworkSocketEndpoint("ws://example.org/game?room=1")
        XCTAssertEqual([plain.host, String(plain.port), plain.target, plain.hostHeader, plain.serverName ?? "-"], ["example.org", "80", "/game?room=1", "example.org", "example.org"])
        let secure = try GesNetworkSocketEndpoint("WSS://[::1]:8443?x")
        XCTAssertEqual([secure.host, String(secure.port), secure.target, secure.hostHeader, secure.serverName ?? "-"], ["::1", "8443", "/?x", "[::1]:8443", "-"])
        XCTAssertTrue(secure.secure)
        XCTAssertNil(try GesNetworkSocketEndpoint("wss://127.0.0.1").serverName)
        for invalid in ["http://x/", "ws://", "ws://x:0/", "ws://x:65536/", "ws://[::1/", "ws://user@x/", "x"] {
            XCTAssertThrowsError(try GesNetworkSocketEndpoint(invalid), invalid)
        }
    }

    func testTheTransportMustMatch() async throws {
        let network = try Self.game()
        await assertInvalidArgument { _ = try await GesNetworkSocketListener.listenTcp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.message)) }
        await assertInvalidArgument { _ = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "game", network: network, options: Self.options(.message)) }
        await assertInvalidArgument {
            let message = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.message))
            _ = try await GesNetworkSocketSession.connectTcp(host: "127.0.0.1", port: 1, connection: message)
        }
        await assertInvalidArgument {
            let server = try GesNetworkConnection.makeServer(network: network, now: Self.now, options: Self.options(.message))
            _ = try await GesNetworkSocketSession.connectWebSocket(url: "ws://127.0.0.1:1/", connection: server)
        }
        await assertInvalidArgument { _ = try await GesNetworkSocketListener.listenUdp(host: "127.0.0.1", port: 0, network: network, options: Self.options(.stream)) }
        await assertInvalidArgument {
            let message = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now, options: Self.options(.message))
            _ = try await GesNetworkSocketSession.connectUdp(host: "127.0.0.1", port: 1, connection: message)
        }
    }

    private func assertInvalidArgument(_ body: () async throws -> Void, file: StaticString = #filePath, line: UInt = #line) async {
        do {
            try await body()
            XCTFail("No error was thrown.", file: file, line: line)
        } catch GesApiError.invalidArgument {
        } catch {
            XCTFail("Unexpected error \(error).", file: file, line: line)
        }
    }
}

// Moves every datagram of a raw socket into an inbox.
private final class RawDatagramHandler: ChannelInboundHandler {
    typealias InboundIn = AddressedEnvelope<ByteBuffer>

    private let inbox: GesNetworkSocketInbox

    init(inbox: GesNetworkSocketInbox) { self.inbox = inbox }

    func channelRead(context: ChannelHandlerContext, data: NIOAny) { inbox.push(Self.unwrapInboundIn(data).data) }
}

extension String {
    fileprivate func replacingAll(_ target: String, _ replacement: String) -> String {
        var result = ""
        var rest = self[...]
        while let range = rest.range(of: target) {
            result += rest[..<range.lowerBound] + replacement
            rest = rest[range.upperBound...]
        }
        return result + rest
    }
}
