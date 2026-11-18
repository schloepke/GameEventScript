// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import XCTest

final class NetworkSecurityTests: XCTestCase {
    private static let source = """
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends :Commands reliable
          server sends :Commands reliable
        }
        """

    private let commands = 0
    private let replies = 1
    // The packet types of a data and a sealed packet.
    private let data: UInt8 = 5
    private let sealed: UInt8 = 6

    private static let serverKey = try! GesNetworkKey(privateKey: (1...32).map { UInt8($0) })

    private func game() throws -> GesBoundNetwork { try GesSchemaSet(parsing: Self.source).network("Game").bind() }

    private func clientOptions(_ transport: GesNetworkTransport, _ minimum: GesNetworkSecurity, _ pinned: [UInt8]?) -> GesNetworkOptions {
        var options = GesNetworkOptions()
        options.transport = transport
        switch minimum {
        case .verified: options.clientSecurity = try! .pinned(pinned!)
        case .encrypted: options.clientSecurity = .development
        default: options.clientSecurity = .unencrypted
        }
        options.insecureTestKeySeed = 7
        return options
    }

    private func serverOptions(_ transport: GesNetworkTransport, _ key: GesNetworkKey?) -> GesNetworkOptions {
        var options = GesNetworkOptions()
        options.transport = transport
        options.serverSecurity = key.map { .key($0) } ?? .unencrypted
        options.insecureTestKeySeed = 7
        return options
    }

    // Moves every due packet from one side to the other; returns the packets moved.
    @discardableResult
    private func pump(_ from: GesNetworkConnection, _ to: GesNetworkConnection, _ now: Int64, _ change: (([UInt8]) -> [UInt8])? = nil) throws -> [[UInt8]] {
        var moved: [[UInt8]] = []
        var buffer = [UInt8](repeating: 0, count: 70_000)
        while true {
            let length = try from.transmit(into: &buffer)
            if length == 0 { return moved }
            var packet = Array(buffer[0..<length])
            if let change { packet = change(packet) }
            moved.append(packet)
            try to.receive(packet, now: now)
        }
    }

    @discardableResult
    private func exchange(_ client: GesNetworkConnection, _ server: GesNetworkConnection, _ now: Int64, _ change: (([UInt8]) -> [UInt8])? = nil) throws -> [[UInt8]] {
        var moved: [[UInt8]] = []
        for _ in 0..<4 {
            moved += try pump(client, server, now, change)
            moved += try pump(server, client, now, change)
        }
        return moved
    }

    @discardableResult
    private func drain(_ connection: GesNetworkConnection) -> [String] {
        var events: [String] = []
        while true {
            let next = connection.nextEvent()
            switch next.kind {
            case .none: return events
            case .closed: events.append("closed \(next.code)")
            case .message: events.append("message \(next.route) \(next.payload[0])")
            case .connecting: events.append("connecting")
            case .connected: events.append("connected")
            default: events.append("\(next.kind)")
            }
        }
    }

    private func connect(
        _ transport: GesNetworkTransport,
        _ minimum: GesNetworkSecurity,
        _ pinned: [UInt8]?,
        _ key: GesNetworkKey?
    ) throws -> (GesNetworkConnection, GesNetworkConnection) {
        let network = try game()
        let client = try GesNetworkConnection.makeClient(network: network, token: Array("t".utf8), now: 0, options: clientOptions(transport, minimum, pinned))
        let server = try GesNetworkConnection.makeServer(network: network, now: 0, options: serverOptions(transport, key))
        try exchange(client, server, 0)
        XCTAssertEqual(drain(server), ["connecting"])
        try server.accept()
        XCTAssertEqual(drain(server), ["connected"])
        try exchange(client, server, 0)
        return (client, server)
    }

    func testPinnedKeyGivesVerifiedSessionWhosePacketsAreSealed() throws {
        for transport in [GesNetworkTransport.stream, .message, .datagram] {
            let (client, server) = try connect(transport, .verified, Self.serverKey.publicKey, Self.serverKey)
            XCTAssertEqual(drain(client), ["connected"])
            XCTAssertEqual(client.security, .verified)
            XCTAssertEqual(server.security, .encrypted)
            XCTAssertEqual(client.send(route: commands, payload: [0x5a]), .queued)
            XCTAssertEqual(server.send(route: replies, payload: [0x6b]), .queued)
            let moved = try exchange(client, server, 1000)
            XCTAssertFalse(moved.isEmpty)
            let prefix = transport == .stream ? 1 : 0
            for packet in moved { XCTAssertEqual(packet[prefix], sealed, "\(transport)") }
            XCTAssertEqual(drain(server), ["message 0 90"])
            XCTAssertEqual(drain(client), ["message 1 107"])
        }
    }

    func testKeyFromRetryGivesEncryptedSession() throws {
        for transport in [GesNetworkTransport.stream, .datagram] {
            let (client, server) = try connect(transport, .encrypted, nil, Self.serverKey)
            XCTAssertEqual(drain(client), ["connected"])
            XCTAssertEqual(client.security, .encrypted)
            _ = client.send(route: commands, payload: [3])
            try exchange(client, server, 1000)
            XCTAssertEqual(drain(server), ["message 0 3"])
        }
    }

    func testPlainClientIsEncryptedByAServerWithAKey() throws {
        let (client, _) = try connect(.stream, .none, nil, Self.serverKey)
        XCTAssertEqual(drain(client), ["connected"])
        XCTAssertEqual(client.security, .encrypted)
    }

    func testPlainSessionReportsNone() throws {
        let (client, server) = try connect(.stream, .none, nil, nil)
        XCTAssertEqual(drain(client), ["connected"])
        XCTAssertEqual(client.security, .none)
        XCTAssertEqual(server.security, .none)
    }

    func testServerWithoutKeyClosesAKeyRequestInsecure() throws {
        let network = try game()
        let client = try GesNetworkConnection.makeClient(network: network, now: 0, options: clientOptions(.stream, .encrypted, nil))
        let server = try GesNetworkConnection.makeServer(network: network, now: 0, options: serverOptions(.stream, nil))
        try exchange(client, server, 0)
        XCTAssertEqual(drain(server), ["closed network.insecure"])
        XCTAssertEqual(drain(client), ["closed network.insecure"])
    }

    func testWrongPinnedKeyFailsAuthentication() throws {
        let other = try GesNetworkKey(privateKey: [UInt8](repeating: 0, count: 32)).publicKey
        let network = try game()
        let client = try GesNetworkConnection.makeClient(network: network, now: 0, options: clientOptions(.stream, .verified, other))
        let server = try GesNetworkConnection.makeServer(network: network, now: 0, options: serverOptions(.stream, Self.serverKey))
        try exchange(client, server, 0)
        XCTAssertEqual(drain(server), ["closed network.authenticationFailed"])
    }

    func testEachSideNeedsItsSecurity() throws {
        let network = try game()
        XCTAssertThrowsError(try GesNetworkConnection.makeServer(network: network, now: 0, options: GesNetworkOptions()))
        XCTAssertThrowsError(try GesNetworkConnection.makeClient(network: network, now: 0, options: GesNetworkOptions()))
        XCTAssertThrowsError(try GesNetworkConnection.makeServer(network: network, now: 0, options: clientOptions(.stream, .encrypted, nil)))
        XCTAssertThrowsError(try GesNetworkClientSecurity.pinned([UInt8](repeating: 1, count: 31)))
    }

    func testPinnedSecurityKeepsTheKey() throws {
        let security = try GesNetworkClientSecurity.pinned(Self.serverKey.publicKey)
        XCTAssertEqual(security.level, .verified)
        XCTAssertEqual(security.serverPublicKey, Self.serverKey.publicKey)
        XCTAssertEqual(GesNetworkClientSecurity.development.level, .encrypted)
        XCTAssertEqual(GesNetworkClientSecurity.unencrypted.level, .none)
    }

    func testChangedSealedPacketClosesAStream() throws {
        let (client, server) = try connect(.message, .verified, Self.serverKey.publicKey, Self.serverKey)
        drain(client)
        _ = client.send(route: commands, payload: [1])
        try pump(client, server, 1000) { packet in
            var packet = packet
            packet[packet.count - 1] ^= 1
            return packet
        }
        XCTAssertEqual(drain(server), ["closed network.authenticationFailed"])
    }

    func testChangedSealedDatagramIsDropped() throws {
        let (client, server) = try connect(.datagram, .verified, Self.serverKey.publicKey, Self.serverKey)
        drain(client)
        _ = client.send(route: commands, payload: [1])
        try pump(client, server, 1000) { packet in
            var packet = packet
            packet[packet.count - 1] ^= 1
            return packet
        }
        XCTAssertEqual(drain(server), [])
        XCTAssertEqual(server.state, .connected)
        // A plain data packet on an encrypted session is dropped as well.
        try server.receive([data, 2, 9, 0], now: 1000)
        XCTAssertEqual(server.state, .connected)
    }

    func testTestSeedMakesTheHandshakeReproducible() throws {
        func run() throws -> [[UInt8]] {
            let network = try game()
            let client = try GesNetworkConnection.makeClient(network: network, now: 0, options: clientOptions(.stream, .verified, Self.serverKey.publicKey))
            let server = try GesNetworkConnection.makeServer(network: network, now: 0, options: serverOptions(.stream, Self.serverKey))
            var moved = try exchange(client, server, 0)
            try server.accept()
            moved += try exchange(client, server, 0)
            return moved
        }

        XCTAssertEqual(try run(), try run())
    }
}
