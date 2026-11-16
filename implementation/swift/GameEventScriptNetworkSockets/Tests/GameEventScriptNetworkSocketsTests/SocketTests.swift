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

    private static func acceptOne(_ listener: GesNetworkSocketListener) throws -> GesNetworkSocketSession {
        let deadline = now + limit
        while now < deadline {
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
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: network)
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now)
        let url = "ws://127.0.0.1:\(listener.localEndPoint.port!)/game?room=1"
        let client = try await GesNetworkSocketSession.connectWebSocket(url: url, connection: connection)
        let server = try Self.acceptOne(listener)
        try Self.roundTrip(client, server)
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
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: Self.game())
        let socket = try await Self.rawWebSocket(port: listener.localEndPoint.port!, target: "/game")
        let server = try Self.acceptOne(listener)
        try await socket.writeAndFlush(Self.frame(.text, Array("hello".utf8))).get()
        XCTAssertEqual(try Self.waitFor(server), "closed network.malformed local")
        try await socket.close()
        server.close()
        listener.close()
    }

    func testAnOversizedWebSocketMessageIsTooLarge() async throws {
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: Self.game())
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
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: network)
        let socket = try await Self.rawWebSocket(port: listener.localEndPoint.port!, target: "/game")
        let server = try Self.acceptOne(listener)
        // A Hello split over two frames: a single byte never forms a valid packet on its own.
        let client = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now)
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

    func testAnotherPathIsNotFound() async throws {
        let network = try Self.game()
        let listener = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "/game", network: network)
        let connection = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now)
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
        await assertInvalidArgument { _ = try await GesNetworkSocketListener.listenWebSocket(host: "127.0.0.1", port: 0, path: "game", network: network) }
        await assertInvalidArgument {
            let message = try GesNetworkConnection.makeClient(network: network, token: Self.token, now: Self.now)
            _ = try await GesNetworkSocketSession.connectTcp(host: "127.0.0.1", port: 1, connection: message)
        }
        await assertInvalidArgument {
            let server = try GesNetworkConnection.makeServer(network: network, now: Self.now)
            _ = try await GesNetworkSocketSession.connectWebSocket(url: "ws://127.0.0.1:1/", connection: server)
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
