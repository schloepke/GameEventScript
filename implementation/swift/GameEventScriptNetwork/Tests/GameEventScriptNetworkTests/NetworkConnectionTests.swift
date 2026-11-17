// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import XCTest

final class NetworkConnectionTests: XCTestCase {
    private static let source = """
        wire schema :World as { tick: :Number integer between 0 and 65535 }
        wire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }
        wire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends :Commands reliable
          server sends :Blob reliable
          server publishes :World latest at most every 50ms
          server sends :Pack bulk
        }
        """

    private let commands = 0
    private let blob = 1
    private let world = 2
    private let bulkBlob = 3

    private func game(_ source: String = source) throws -> GesBoundNetwork { try GesSchemaSet(parsing: source).network("Game").bind() }

    private func connected(_ options: GesNetworkOptions = GesNetworkOptions()) throws -> GesNetworkLoopback {
        let network = try game()
        let client = try GesNetworkConnection.makeClient(network: network, token: Array("t".utf8), now: 0, options: options)
        let server = try GesNetworkConnection.makeServer(network: network, now: 0, options: options)
        let loop = try GesNetworkLoopback(client: client, server: server)
        try loop.advance(0)
        XCTAssertEqual(drain(server), ["connecting t"])
        try server.accept()
        try loop.advance(0)
        XCTAssertEqual(drain(client), ["connected"])
        XCTAssertEqual(drain(server), ["connected"])
        XCTAssertEqual(client.state, .connected)
        return loop
    }

    private func drain(_ connection: GesNetworkConnection) -> [String] {
        var events: [String] = []
        while true {
            let next = connection.nextEvent()
            switch next.kind {
            case .none:
                return events
            case .connecting:
                events.append("connecting " + String(decoding: next.payload, as: UTF8.self))
            case .connected:
                events.append("connected")
            case .message:
                events.append("message \(next.route) \(hex(next.payload))")
            case .closed:
                events.append("closed \(next.code) '\(next.detail)' \(next.byPeer ? "peer" : "local")")
            case .transferProgress, .transferCancelled:
                break
            }
        }
    }

    private func hex<C: Collection>(_ bytes: C) -> String where C.Element == UInt8 {
        let digits = Array("0123456789ABCDEF")
        var text = ""
        for byte in bytes {
            text.append(digits[Int(byte >> 4)])
            text.append(digits[Int(byte & 15)])
        }
        return text
    }

    private func bytes(_ length: Int, _ seed: Int) -> [UInt8] { (0..<length).map { UInt8(truncatingIfNeeded: $0 * 7 + seed) } }

    private func pair(_ options: GesNetworkOptions) throws -> (GesNetworkConnection, GesNetworkConnection) {
        let network = try game()
        return (try GesNetworkConnection.makeClient(network: network, now: 0, options: options), try GesNetworkConnection.makeServer(network: network, now: 0, options: options))
    }

    func testHandshakeReportsTheTokenAndConnectsBothSides() throws {
        let loop = try connected()
        XCTAssertEqual(loop.server.state, .connected)
    }

    func testRefusalClosesBothSidesWithTheHostDetail() throws {
        let (client, server) = try pair(GesNetworkOptions())
        let loop = try GesNetworkLoopback(client: client, server: server)
        try loop.advance(0)
        _ = drain(server)
        try server.refuse("full")
        try loop.advance(0)
        XCTAssertEqual(drain(server), ["closed network.refused 'full' local"])
        XCTAssertEqual(drain(client), ["closed network.refused 'full' peer"])
    }

    func testSchemaMismatchNamesTheDifferingRoots() throws {
        let other = Self.source.replacingOccurrences(of: "max 6000", with: "max 5000")
        let client = try GesNetworkConnection.makeClient(network: try game(other), now: 0)
        let server = try GesNetworkConnection.makeServer(network: try game(), now: 0)
        try GesNetworkLoopback(client: client, server: server).advance(0)
        XCTAssertEqual(drain(server), ["closed network.schemaMismatch 'Blob, Pack' local"])
        XCTAssertEqual(drain(client), ["closed network.schemaMismatch 'Blob, Pack' peer"])
    }

    func testSchemaMismatchWithEqualRootsNamesTheNetwork() throws {
        let other = Self.source.replacingOccurrences(of: "at most every 50ms", with: "at most every 60ms")
        let client = try GesNetworkConnection.makeClient(network: try game(other), now: 0)
        let server = try GesNetworkConnection.makeServer(network: try game(), now: 0)
        try GesNetworkLoopback(client: client, server: server).advance(0)
        XCTAssertEqual(drain(server), ["closed network.schemaMismatch 'Game' local"])
    }

    func testReliableAndBulkMessagesArriveCompleteInOrderAcrossFragments() throws {
        var options = GesNetworkOptions()
        options.maxPacketBytes = 1200
        let loop = try connected(options)
        let first = bytes(3000, 1)
        let second = bytes(10, 2)
        let bulk = bytes(5000, 3)
        XCTAssertEqual(loop.server.send(route: bulkBlob, payload: bulk), .queued)
        XCTAssertEqual(loop.server.send(route: blob, payload: first), .queued)
        XCTAssertEqual(loop.server.send(route: blob, payload: second), .queued)
        try loop.advance(1)
        XCTAssertEqual(drain(loop.client), ["message \(blob) \(hex(first))", "message \(blob) \(hex(second))", "message \(bulkBlob) \(hex(bulk))"])
        XCTAssertEqual(loop.server.queuedSendBytes, 0)
        XCTAssertEqual(loop.client.pendingReceiveBytes, 0)
    }

    func testLatestDeliversOnlyTheNewestUnstartedMessage() throws {
        let loop = try connected()
        try loop.server.subscribe(route: world)
        _ = loop.server.publish(route: world, payload: [1, 0])
        _ = loop.server.publish(route: world, payload: [2, 0])
        _ = loop.server.publish(route: world, payload: [3, 0])
        try loop.advance(1)
        XCTAssertEqual(drain(loop.client), ["message \(world) 0300"])
        _ = loop.server.publish(route: world, payload: [4, 0])
        try loop.advance(2)
        XCTAssertEqual(drain(loop.client), ["message \(world) 0400"])
    }

    func testSendReportsEveryStatusInItsOrder() throws {
        let client = try GesNetworkConnection.makeClient(network: try game(), now: 0)
        XCTAssertEqual(client.send(route: blob, payload: [1]), .invalidRoute)
        XCTAssertEqual(client.send(route: 7, payload: [1]), .invalidRoute)
        XCTAssertEqual(client.send(route: commands, payload: [1]), .notConnected)

        var options = GesNetworkOptions()
        options.maxSendQueueBytes = 100
        let loop = try connected(options)
        XCTAssertEqual(loop.client.send(route: commands, payload: [UInt8](repeating: 0, count: 1000)), .messageTooLarge)
        XCTAssertEqual(loop.server.send(route: blob, payload: [UInt8](repeating: 0, count: 90)), .queued)
        XCTAssertEqual(loop.server.send(route: blob, payload: [UInt8](repeating: 0, count: 11)), .queueFull)
        XCTAssertEqual(loop.server.send(route: world, payload: [1, 0]), .invalidRoute)
        XCTAssertEqual(loop.server.publish(route: world, payload: [1, 0]), .notSubscribed)
        try loop.server.subscribe(route: world)
        XCTAssertEqual(loop.server.publish(route: world, payload: [1, 0]), .queued)
    }

    func testHostCloseDropsQueuedMessagesAndReachesThePeer() throws {
        let loop = try connected()
        _ = loop.server.send(route: blob, payload: [1, 2])
        loop.server.close("bye")
        XCTAssertEqual(loop.server.queuedSendBytes, 0)
        try loop.advance(1)
        XCTAssertEqual(drain(loop.server), ["closed network.closed 'bye' local"])
        XCTAssertEqual(drain(loop.client), ["closed network.closed 'bye' peer"])
        XCTAssertEqual(loop.server.send(route: blob, payload: [1]), .notConnected)
    }

    func testKeepaliveHoldsAnIdleSessionAndSilenceTimesOut() throws {
        let loop = try connected()
        try loop.advance(30_000_000)
        XCTAssertEqual(loop.client.state, .connected)
        XCTAssertEqual(loop.server.state, .connected)

        let lone = try GesNetworkConnection.makeServer(network: try game(), now: 0)
        try lone.advance(9_999_999)
        XCTAssertEqual(lone.state, .awaitHello)
        try lone.advance(10_000_000)
        XCTAssertEqual(drain(lone), ["closed network.timeout '' local"])
    }

    func testRoundTripFollowsTheSimulatedLatency() throws {
        let (client, server) = try pair(GesNetworkOptions())
        let simulator = try GesNetworkSimulator(client: client, server: server, seed: 7, latencyMicroseconds: 20_000, jitterMicroseconds: 0)
        try handshake(simulator)
        try server.subscribe(route: world)
        var time = simulator.now
        while time <= 2_000_000 {
            _ = client.send(route: commands, payload: [1])
            try simulator.advance(time)
            _ = drain(client)
            _ = drain(server)
            time += 10_000
        }
        XCTAssertTrue((40_000...60_000).contains(client.roundTripMicroseconds), "\(client.roundTripMicroseconds)")
    }

    func testEqualSeedsGiveEqualTraces() throws {
        XCTAssertEqual(try trace(11), try trace(11))
        XCTAssertNotEqual(try trace(11), try trace(12))
    }

    func testStreamTransportReassemblesPacketsFromSingleBytes() throws {
        var options = GesNetworkOptions()
        options.transport = .stream
        let (client, server) = try pair(options)
        var buffer = [UInt8](repeating: 0, count: 70_000)
        try pump(client, server, &buffer)
        XCTAssertEqual(drain(server), ["connecting "])
        try server.accept()
        try pump(server, client, &buffer)
        XCTAssertEqual(drain(client), ["connected"])
        let payload = bytes(4000, 5)
        _ = server.send(route: blob, payload: payload)
        try pump(server, client, &buffer)
        XCTAssertEqual(drain(client), ["message \(blob) \(hex(payload))"])
    }

    func testStreamLengthZeroIsMalformedAndTooLongIsTooLarge() throws {
        var options = GesNetworkOptions()
        options.transport = .stream
        options.maxPacketBytes = 1200
        var server = try pair(options).1
        try server.receive([0], now: 0)
        XCTAssertEqual(drain(server), ["closed network.malformed '' local"])
        server = try pair(options).1
        try server.receive([0xb1, 0x09], now: 0)
        XCTAssertEqual(drain(server), ["closed network.packetTooLarge '' local"])
    }

    func testMalformedPacketsCloseWithTheirCode() throws {
        var server = try pair(GesNetworkOptions()).1
        try server.receive([9], now: 0)
        XCTAssertEqual(drain(server), ["closed network.malformed '' local"])

        server = try pair(GesNetworkOptions()).1
        try server.receive([1] + Array("GESX".utf8), now: 0)
        XCTAssertEqual(drain(server), ["closed network.protocolMismatch '' local"])

        server = try pair(GesNetworkOptions()).1
        try server.receive([5, 0, 0, 0], now: 0)
        XCTAssertEqual(drain(server), ["closed network.malformed '' local"])
    }

    func testClientRejectsAFrameOnItsOwnRoute() throws {
        let loop = try connected()
        // data asking for acknowledgement, seq 0, frame: channel 1 (client route), id 0, offset 0, total 1, chunk 1, byte.
        try loop.client.receive([5, 2, 0, 0, 1, 0, 0, 0, 1, 1, 9], now: 0)
        XCTAssertEqual(drain(loop.client), ["closed network.unexpectedRoute '' local"])
    }

    func testFrameRateLimitClosesTheConnection() throws {
        var options = GesNetworkOptions()
        options.maxFramesPerSecond = 2
        let loop = try connected(options)
        for index in 0..<3 { _ = loop.client.send(route: commands, payload: [UInt8(index)]) }
        try loop.advance(1)
        XCTAssertEqual(Array(drain(loop.server).dropFirst(2)), ["closed network.rateExceeded '' local"])
    }

    func testSendBudgetDelaysFramesUntilTheBucketRefills() throws {
        var options = GesNetworkOptions()
        options.maxPacketBytes = 1200
        let loop = try connected(options)
        try loop.server.setSendBudgetBytesPerSecond(12_000)
        _ = loop.server.send(route: blob, payload: bytes(5000, 0))
        try loop.advance(1)
        XCTAssertEqual(drain(loop.client), [])
        try loop.advance(500_000)
        XCTAssertEqual(drain(loop.client).count, 1)
    }

    // Runs the simulator until the server's host is asked and accepts the client.
    private func handshake(_ simulator: GesNetworkSimulator) throws {
        try simulator.advance(simulator.now)
        while simulator.server.state == .awaitHello { try simulator.advance(simulator.nextDeadline) }
        XCTAssertEqual(drain(simulator.server), ["connecting "])
        try simulator.server.accept()
    }

    // Moves every due packet from one stream connection to the other one byte at a time.
    private func pump(_ from: GesNetworkConnection, _ to: GesNetworkConnection, _ buffer: inout [UInt8]) throws {
        while true {
            let length = try from.transmit(into: &buffer)
            if length == 0 { return }
            for index in 0..<length { try to.receive([buffer[index]], now: to.now) }
        }
    }

    private func trace(_ seed: Int64) throws -> String {
        var options = GesNetworkOptions()
        options.maxPacketBytes = 1200
        let (client, server) = try pair(options)
        let simulator = try GesNetworkSimulator(client: client, server: server, seed: seed, latencyMicroseconds: 5_000, jitterMicroseconds: 30_000)
        var trace = ""
        try handshake(simulator)
        try server.subscribe(route: world)
        var time = simulator.now
        while time < 1_000_000 {
            _ = server.publish(route: world, payload: [UInt8(truncatingIfNeeded: time / 16_667), 0])
            if time % 100_000 < 16_667 { _ = server.send(route: blob, payload: bytes(2500, Int(time))) }
            try simulator.advance(time)
            for entry in drain(client) { trace += "\(simulator.now) \(entry.prefix(16))\n" }
            _ = drain(server)
            time += 16_667
        }
        return trace + "\(client.roundTripMicroseconds)"
    }

    // Every event of a connection with its transfer fields.
    private func transfers(_ connection: GesNetworkConnection) -> [String] {
        var events: [String] = []
        while true {
            let next = connection.nextEvent()
            switch next.kind {
            case .none:
                return events
            case .transferProgress:
                events.append("progress \(next.route) \(next.transferId) \(next.bytes)/\(next.total)")
            case .transferCancelled:
                events.append("cancelled \(next.route) \(next.transferId) \(next.code) \(next.byPeer ? "peer" : "local")")
            case .message:
                events.append("message \(next.route) \(next.transferId) \(next.payload.count)")
            default:
                events.append("\(next.kind) \(next.code)")
            }
        }
    }

    func testTransferArrivesWithProgressOnBothSidesAndCompressesWhenShorter() throws {
        var options = GesNetworkOptions()
        options.maxPacketBytes = 1200
        let loop = try connected(options)
        XCTAssertEqual(loop.server.send(route: bulkBlob, payload: [UInt8](repeating: 0, count: 5000)), .queued)
        XCTAssertEqual(loop.server.lastTransferId, 0)
        // 1 compression byte, the 2-byte size, the hash and far fewer than 5000 deflated bytes.
        XCTAssertLessThan(loop.server.queuedSendBytes, 200)
        try loop.advance(1)
        let received = transfers(loop.client)
        XCTAssertEqual(received.last, "message \(bulkBlob) 0 5000")
        XCTAssertTrue(received[0].hasPrefix("progress \(bulkBlob) 0 "))
        try loop.advance(20_000)
        XCTAssertEqual(transfers(loop.server).last, received[received.count - 2])
    }

    func testTransferWithoutCompressionCarriesTheData() throws {
        var options = GesNetworkOptions()
        options.deflateTransfers = false
        let loop = try connected(options)
        let data = bytes(3000, 5)
        XCTAssertEqual(loop.server.send(route: bulkBlob, payload: data), .queued)
        XCTAssertEqual(loop.server.queuedSendBytes, 3000 + 1 + 2 + 32)
        try loop.advance(1)
        var next = loop.client.nextEvent()
        while next.kind == .transferProgress { next = loop.client.nextEvent() }
        XCTAssertEqual(next.kind, .message)
        XCTAssertEqual(Array(next.payload), data)
    }

    func testTransferAboveThePeerLimitIsTooLarge() throws {
        let network = try game()
        var options = GesNetworkOptions()
        options.maxTransferBytes = 100
        let client = try GesNetworkConnection.makeClient(network: network, now: 0, options: options)
        let server = try GesNetworkConnection.makeServer(network: network, now: 0)
        let loop = try GesNetworkLoopback(client: client, server: server)
        try loop.advance(0)
        _ = server.nextEvent()
        try server.accept()
        try loop.advance(0)
        XCTAssertEqual(server.send(route: bulkBlob, payload: [UInt8](repeating: 0, count: 101)), .messageTooLarge)
        XCTAssertEqual(server.send(route: bulkBlob, payload: [UInt8](repeating: 0, count: 100)), .queued)
    }

    func testCancelledTransferReachesThePeerAndTheSessionContinues() throws {
        let loop = try connected()
        _ = loop.server.send(route: bulkBlob, payload: bytes(4000, 1))
        let first = loop.server.lastTransferId
        _ = loop.server.send(route: bulkBlob, payload: bytes(10, 2))
        XCTAssertTrue(try loop.server.cancel(route: bulkBlob, transferId: first, code: "game.skipped"))
        XCTAssertFalse(try loop.server.cancel(route: bulkBlob, transferId: first))
        XCTAssertEqual(transfers(loop.server), ["cancelled \(bulkBlob) 0 game.skipped local"])
        try loop.advance(1)
        let received = transfers(loop.client)
        XCTAssertEqual(received.first, "cancelled \(bulkBlob) 0 game.skipped peer")
        XCTAssertEqual(received.last, "message \(bulkBlob) 1 10")
        XCTAssertEqual(loop.client.state, .connected)
        XCTAssertThrowsError(try loop.server.cancel(route: blob, transferId: 0))
        XCTAssertThrowsError(try loop.server.cancel(route: bulkBlob, transferId: 1, code: "bad code"))
    }
}
