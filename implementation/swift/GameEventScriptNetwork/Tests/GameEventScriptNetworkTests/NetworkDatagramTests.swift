// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import XCTest

final class NetworkDatagramTests: XCTestCase {
    private static let source = """
        wire schema :World as { tick: :Number integer between 0 and 65535 }
        wire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }
        wire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }
        wire messages Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends Commands reliable
          server sends :Blob reliable
          server publishes :World latest at most every 50ms
          server sends :Pack bulk
        }
        """

    private let commands = 0
    private let blob = 1
    private let world = 2
    private let bulkBlob = 3

    private static var options: GesNetworkOptions {
        var options = GesNetworkOptions.plain
        options.transport = .datagram
        return options
    }

    private func pair(_ options: GesNetworkOptions = NetworkDatagramTests.options) throws -> (GesNetworkConnection, GesNetworkConnection) {
        let network = try GesSchemaSet(parsing: Self.source).network("Game").bind()
        return (
            try GesNetworkConnection.makeClient(network: network, now: 0, options: options),
            try GesNetworkConnection.makeServer(network: network, now: 0, options: options)
        )
    }

    private func connected(_ seed: Int64, _ loss: Int, _ duplicate: Int, _ reorder: Bool, options: GesNetworkOptions = NetworkDatagramTests.options) throws -> GesNetworkSimulator {
        let (client, server) = try pair(options)
        let simulator = try GesNetworkSimulator(
            client: client,
            server: server,
            seed: seed,
            latencyMicroseconds: 20_000,
            jitterMicroseconds: 15_000,
            lossPerMille: loss,
            duplicatePerMille: duplicate,
            reorder: reorder
        )
        try simulator.advance(0)
        var step = 0
        while step < 1000 && server.state == .awaitHello {
            try simulator.advance(simulator.nextDeadline)
            step += 1
        }
        XCTAssertEqual(server.state, .awaitAccept, "server \(server.state), client \(client.state)")
        XCTAssertEqual(server.nextEvent().kind, .connecting)
        try server.accept()
        step = 0
        while step < 1000 && client.state == .hello {
            try simulator.advance(simulator.nextDeadline)
            step += 1
        }
        XCTAssertEqual(client.state, .connected, "client \(client.state) \(codes(client)), server \(server.state) \(codes(server))")
        XCTAssertEqual(client.nextEvent().kind, .connected)
        return simulator
    }

    private func codes(_ connection: GesNetworkConnection) -> String {
        var text = ""
        while true {
            let next = connection.nextEvent()
            if next.kind == .none { return text }
            text += "\(next.kind):\(next.code) "
        }
    }

    private func bytes(_ length: Int, _ seed: Int) -> [UInt8] { (0..<length).map { UInt8(truncatingIfNeeded: $0 * 7 + seed) } }

    private func messages(_ connection: GesNetworkConnection) -> [(route: Int, payload: [UInt8])] {
        var messages: [(route: Int, payload: [UInt8])] = []
        while true {
            let next = connection.nextEvent()
            if next.kind == .none { return messages }
            XCTAssertNotEqual(next.kind, .closed, next.code)
            if next.kind == .message { messages.append((next.route, Array(next.payload))) }
        }
    }

    func testHelloIsPaddedToTheMinimumDatagramAndRepeatedWithDoublingInterval() throws {
        let (client, _) = try pair()
        var buffer = [UInt8](repeating: 0, count: 2048)
        XCTAssertEqual(try client.transmit(into: &buffer), 1200)
        XCTAssertEqual(try client.transmit(into: &buffer), 0)
        XCTAssertEqual(client.nextDeadline, 1_000_000)
        try client.advance(1_000_000)
        XCTAssertEqual(try client.transmit(into: &buffer), 1200)
        XCTAssertEqual(client.nextDeadline, 3_000_000)
    }

    // 1024 packets of 40 small messages each carry far more than the 16384 recorded fragments; acknowledged packets
    // free theirs, so the sender keeps sending.
    func testManySmallMessagesKeepFlowingBeyondTheRecordedFragments() throws {
        let simulator = try connected(2, 0, 0, false)
        var received = 0
        for round in 0..<1200 {
            for index in 0..<40 {
                XCTAssertEqual(simulator.server.send(route: blob, payload: [UInt8(truncatingIfNeeded: round * 40 + index)]), .queued)
            }
            try simulator.advance(simulator.now + 16_667)
            for (route, message) in messages(simulator.client) {
                XCTAssertEqual(route, blob)
                XCTAssertEqual(message[0], UInt8(truncatingIfNeeded: received))
                received += 1
            }
        }
        // At most the last rounds remain in flight.
        XCTAssertGreaterThanOrEqual(received, 1190 * 40)
    }

    func testReliableAndBulkArriveCompleteAndInOrderWithoutImpairment() throws {
        try reliableAndBulkArriveCompleteAndInOrder(1, 0, 0, false)
    }

    func testReliableAndBulkArriveCompleteAndInOrderDespiteLoss() throws {
        try reliableAndBulkArriveCompleteAndInOrder(5, 200, 0, false)
    }

    func testReliableAndBulkArriveCompleteAndInOrderDespiteDuplicatesAndReordering() throws {
        try reliableAndBulkArriveCompleteAndInOrder(3, 0, 300, true)
    }

    func testReliableAndBulkArriveCompleteAndInOrderDespiteLossDuplicatesAndReordering() throws {
        try reliableAndBulkArriveCompleteAndInOrder(4, 250, 150, true)
    }

    private func reliableAndBulkArriveCompleteAndInOrder(_ seed: Int64, _ loss: Int, _ duplicate: Int, _ reorder: Bool) throws {
        let simulator = try connected(seed, loss, duplicate, reorder)
        var expectedReliable: [[UInt8]] = []
        var expectedBulk: [[UInt8]] = []
        var reliable: [[UInt8]] = []
        var bulk: [[UInt8]] = []
        var commandCount = 0

        func collect() {
            for message in messages(simulator.client) {
                if message.route == blob { reliable.append(message.payload) } else { bulk.append(message.payload) }
            }
            for message in messages(simulator.server) {
                XCTAssertEqual(Int(message.payload[0]), commandCount)
                commandCount += 1
            }
        }

        for round in 0..<40 {
            let blobPayload = bytes(round * 97 % 3000 + 1, round)
            let pack = bytes(round * 211 % 5000 + 1, round + 50)
            XCTAssertEqual(simulator.server.send(route: blob, payload: blobPayload), .queued)
            XCTAssertEqual(simulator.server.send(route: bulkBlob, payload: pack), .queued)
            XCTAssertEqual(simulator.client.send(route: commands, payload: [UInt8(round)]), .queued)
            expectedReliable.append(blobPayload)
            expectedBulk.append(pack)
            try simulator.advance(simulator.now + 16_667)
            collect()
        }

        var step = 0
        while step < 2000 && (reliable.count < 40 || bulk.count < 40 || commandCount < 40) {
            try simulator.advance(simulator.now + 16_667)
            collect()
            step += 1
        }

        XCTAssertEqual(commandCount, 40)
        XCTAssertEqual(reliable, expectedReliable)
        XCTAssertEqual(bulk, expectedBulk)
        try simulator.advance(simulator.now + 2_000_000)
        XCTAssertEqual(simulator.server.queuedSendBytes, 0)
        XCTAssertEqual(simulator.client.queuedSendBytes, 0)
        XCTAssertEqual(simulator.client.state, .connected)
        XCTAssertEqual(simulator.server.state, .connected)
    }

    func testLatestDeliversIncreasingStatesAndEndsWithTheNewestDespiteLoss() throws {
        let simulator = try connected(9, 300, 100, true)
        try simulator.server.subscribe(route: world)
        var last = -1
        for tick in 0..<200 {
            _ = simulator.server.publish(route: world, payload: [UInt8(truncatingIfNeeded: tick), UInt8(truncatingIfNeeded: tick >> 8)])
            try simulator.advance(simulator.now + 16_667)
            for message in messages(simulator.client) {
                let value = Int(message.payload[0]) | Int(message.payload[1]) << 8
                XCTAssertGreaterThan(value, last)
                last = value
            }
        }

        try simulator.advance(simulator.now + 3_000_000)
        for message in messages(simulator.client) { last = Int(message.payload[0]) | Int(message.payload[1]) << 8 }
        XCTAssertEqual(last, 199)
    }

    func testRoundTripFollowsTheSimulatedLatency() throws {
        let simulator = try connected(5, 100, 0, true)
        for _ in 0..<200 {
            _ = simulator.client.send(route: commands, payload: [1])
            try simulator.advance(simulator.now + 10_000)
            _ = messages(simulator.server)
        }
        XCTAssertTrue((40_000...80_000).contains(simulator.client.roundTripMicroseconds), "\(simulator.client.roundTripMicroseconds)")
    }

    func testEqualSeedsGiveEqualTraces() throws {
        XCTAssertEqual(try trace(21), try trace(21))
        XCTAssertNotEqual(try trace(21), try trace(22))
    }

    private func trace(_ seed: Int64) throws -> String {
        let simulator = try connected(seed, 200, 100, true)
        var trace = ""
        for round in 0..<60 {
            _ = simulator.server.send(route: blob, payload: bytes(1500, round))
            try simulator.advance(simulator.now + 16_667)
            for message in messages(simulator.client) { trace += "\(simulator.now) \(message.payload[0])\n" }
        }
        return trace
    }

    func testSilenceAfterLossTimesOut() throws {
        let simulator = try connected(1, 0, 0, false)
        let client = simulator.client
        var buffer = [UInt8](repeating: 0, count: 2048)
        _ = client.send(route: commands, payload: [1])
        while try client.transmit(into: &buffer) > 0 {}
        try client.advance(client.now + 10_000_000)
        while try client.transmit(into: &buffer) > 0 {}
        XCTAssertEqual(client.state, .closed)
        XCTAssertEqual(simulator.server.state, .connected)
    }

    func testImpairmentRequiresDatagramConnectionsAndValidRates() throws {
        let network = try GesSchemaSet(parsing: Self.source).network("Game").bind()
        let client = try GesNetworkConnection.makeClient(network: network, now: 0, options: .plain)
        let server = try GesNetworkConnection.makeServer(network: network, now: 0, options: .plain)
        XCTAssertThrowsError(try GesNetworkSimulator(client: client, server: server, seed: 0, latencyMicroseconds: 0, jitterMicroseconds: 0, lossPerMille: 1))
        XCTAssertThrowsError(try GesNetworkSimulator(client: client, server: server, seed: 0, latencyMicroseconds: 0, jitterMicroseconds: 0, reorder: true))
        let (datagramClient, datagramServer) = try pair()
        XCTAssertThrowsError(
            try GesNetworkSimulator(client: datagramClient, server: datagramServer, seed: 0, latencyMicroseconds: 0, jitterMicroseconds: 0, lossPerMille: 1001)
        )
        XCTAssertThrowsError(
            try GesNetworkSimulator(client: datagramClient, server: datagramServer, seed: 0, latencyMicroseconds: 0, jitterMicroseconds: 0, duplicatePerMille: -1)
        )
    }

    func testCancelledTransfersAreSkippedAndLaterOnesArriveDespiteLoss() throws {
        try checkCancelledTransfers(11, 0, 0, false)
        try checkCancelledTransfers(12, 250, 150, true)
    }

    private func checkCancelledTransfers(_ seed: Int64, _ loss: Int, _ duplicate: Int, _ reorder: Bool) throws {
        let simulator = try connected(seed, loss, duplicate, reorder)
        var kept: [[UInt8]] = []
        for index in 0..<20 {
            let data = bytes(index * 331 % 5000 + 1, index)
            XCTAssertEqual(simulator.server.send(route: bulkBlob, payload: data), .queued)
            if index % 3 == 1 {
                XCTAssertTrue(try simulator.server.cancel(route: bulkBlob, transferId: simulator.server.lastTransferId))
            } else {
                kept.append(data)
            }
        }

        var received: [[UInt8]] = []
        var cancelled: [Int] = []
        var step = 0
        while step < 4000 && (received.count < kept.count || cancelled.count < 7) {
            try simulator.advance(simulator.now + 16_667)
            while true {
                let next = simulator.client.nextEvent()
                if next.kind == .none { break }
                XCTAssertNotEqual(next.kind, .closed, next.code)
                if next.kind == .message { received.append(Array(next.payload)) }
                if next.kind == .transferCancelled {
                    XCTAssertTrue(next.byPeer)
                    cancelled.append(next.transferId)
                }
            }
            _ = messages(simulator.server)
            step += 1
        }

        XCTAssertEqual(cancelled.sorted(), [1, 4, 7, 10, 13, 16, 19])
        XCTAssertEqual(received, kept)
        try simulator.advance(simulator.now + 2_000_000)
        XCTAssertEqual(simulator.server.queuedSendBytes, 0)
    }

    func testAnUnconfirmedCancelKeepsLaterTransfersWithinTheReceiveWindow() throws {
        // Many small transfers in a short time: the receiver allows their frame rate.
        var options = Self.options
        options.maxFramesPerSecond = 1_000_000
        let simulator = try connected(13, 0, 0, false, options: options)
        XCTAssertEqual(simulator.server.send(route: bulkBlob, payload: [1]), .queued)
        XCTAssertTrue(try simulator.server.cancel(route: bulkBlob, transferId: simulator.server.lastTransferId))
        for index in 1..<8192 { XCTAssertEqual(simulator.server.send(route: bulkBlob, payload: [UInt8(truncatingIfNeeded: index)]), .queued, "transfer \(index)") }

        // The cancel of transfer 0 is not confirmed yet, so transfer 8192 would lie outside the receiver's window.
        XCTAssertEqual(simulator.server.send(route: bulkBlob, payload: [0]), .queueFull)
        var received = 0
        var step = 0
        while step < 4000 && received < 8191 {
            try simulator.advance(simulator.now + 16_667)
            while true {
                let next = simulator.client.nextEvent()
                if next.kind == .none { break }
                XCTAssertNotEqual(next.kind, .closed, next.code)
                if next.kind == .message { received += 1 }
            }
            _ = messages(simulator.server)
            step += 1
        }

        XCTAssertEqual(received, 8191)
        try simulator.advance(simulator.now + 2_000_000)
        XCTAssertEqual(simulator.server.send(route: bulkBlob, payload: [0]), .queued)
        XCTAssertEqual(simulator.client.state, .connected)
    }
}
