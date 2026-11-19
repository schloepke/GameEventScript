// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import XCTest

final class NetworkTickTests: XCTestCase {
    private static let source = """
        wire schema :Alpha as { v: :Number integer between 0 and 255 }
        wire schema :Beta as { v: :Number integer between 0 and 255 }
        wire schema :Log as { v: :Number integer between 0 and 255 }
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends :Commands reliable
          server publishes :Alpha latest
          server publishes :Beta latest
          server publishes :Log reliable
        }
        """

    private let commands = 0
    private let alpha = 1
    private let beta = 2
    private let log = 3

    private func connected(_ transport: GesNetworkTransport, loss: Int = 0, seed: Int64 = 0) throws -> GesNetworkSimulator {
        let network = try GesSchemaSet(parsing: Self.source).network("Game").bind()
        var options = GesNetworkOptions.plain
        options.transport = transport
        let client = try GesNetworkConnection.makeClient(network: network, now: 0, options: options)
        let server = try GesNetworkConnection.makeServer(network: network, now: 0, options: options)
        let simulator = try GesNetworkSimulator(
            client: client,
            server: server,
            seed: seed,
            latencyMicroseconds: loss == 0 ? 0 : 5_000,
            jitterMicroseconds: loss == 0 ? 0 : 20_000,
            lossPerMille: loss,
            duplicatePerMille: 0,
            reorder: loss != 0
        )
        try simulator.advance(0)
        var step = 0
        while step < 1000 && server.state == .awaitHello {
            try simulator.advance(simulator.nextDeadline)
            step += 1
        }
        try server.accept()
        step = 0
        while step < 1000 && client.state == .hello {
            try simulator.advance(simulator.nextDeadline)
            step += 1
        }
        XCTAssertEqual(client.state, .connected, "seed \(seed)")
        _ = drain(client)
        _ = drain(server)
        try server.subscribe(route: alpha)
        try server.subscribe(route: beta)
        try server.subscribe(route: log)
        return simulator
    }

    private func drain(_ connection: GesNetworkConnection) -> [String] {
        var events: [String] = []
        while true {
            let next = connection.nextEvent()
            switch next.kind {
            case .none: return events
            case .message: events.append("message \(next.route) " + next.payload.map { String(format: "%02X", $0) }.joined())
            case .tick: events.append("tick \(next.tickNumber)")
            default: events.append("\(next.kind) \(next.code)")
            }
        }
    }

    private func settle(_ simulator: GesNetworkSimulator) throws {
        for _ in 0..<50 { try simulator.advance(simulator.now + 10_000) }
    }

    func testTickReportsItsChangedMembersAndThenTheTick() throws {
        for transport in [GesNetworkTransport.message, .stream, .datagram] {
            let simulator = try connected(transport)
            let server = simulator.server
            try server.beginTick()
            XCTAssertEqual(server.publish(route: alpha, payload: [1]), .queued)
            XCTAssertEqual(server.publish(route: beta, payload: [2]), .queued)
            XCTAssertEqual(server.publish(route: log, payload: [3]), .queued)
            XCTAssertEqual(try server.endTick(), .queued)
            try settle(simulator)
            // The reliable topic is no member and travels at once.
            XCTAssertEqual(drain(simulator.client), ["message \(log) 03", "message \(alpha) 01", "message \(beta) 02", "tick 0"], "\(transport)")

            try server.beginTick()
            _ = server.publish(route: alpha, payload: [4])
            try server.include(route: beta)
            try server.endTick()
            try settle(simulator)
            XCTAssertEqual(drain(simulator.client), ["message \(alpha) 04", "tick 1"], "\(transport)")

            // Only an included member: the tick names the state the client holds.
            try server.beginTick()
            try server.include(route: alpha)
            try server.endTick()
            try settle(simulator)
            XCTAssertEqual(drain(simulator.client), ["tick 2"], "\(transport)")

            // A state published alone is reported on arrival; an empty tick sends nothing.
            _ = server.publish(route: beta, payload: [5])
            try server.beginTick()
            try server.endTick()
            try settle(simulator)
            XCTAssertEqual(drain(simulator.client), ["message \(beta) 05"], "\(transport)")
        }
    }

    func testClosedTicksWaitAndANewerOneReplacesTheWaitingOne() throws {
        for transport in [GesNetworkTransport.message, .datagram] {
            let simulator = try connected(transport)
            let server = simulator.server
            for value in UInt8(1)...3 {
                try server.beginTick()
                _ = server.publish(route: alpha, payload: [value])
                _ = server.publish(route: beta, payload: [value + 16])
                try server.endTick()
            }
            try settle(simulator)
            // The first tick is in transmission, the second waits and the third replaces it. The second tick's states
            // become latest states, which replace the first tick's states that have not started yet.
            XCTAssertEqual(
                drain(simulator.client),
                ["message \(alpha) 02", "message \(beta) 12", "tick 0", "message \(alpha) 03", "message \(beta) 13", "tick 1"],
                "\(transport)"
            )
        }
    }

    func testTicksStayConsistentUnderLossAndReordering() throws {
        // Seeds whose handshake survives the loss.
        for seed: Int64 in [1, 3, 4, 5, 6, 7, 8, 10, 11, 12] {
            let simulator = try connected(.datagram, loss: 200, seed: seed)
            let server = simulator.server
            let client = simulator.client
            var alphaValue = -1
            var betaValue = -1
            var lastTick = -1
            var ticks = 0
            for round in 0..<200 {
                try server.beginTick()
                _ = server.publish(route: alpha, payload: [UInt8(round)])
                // Beta changes every third round and is included otherwise.
                if round % 3 == 0 { _ = server.publish(route: beta, payload: [UInt8(round)]) } else { try server.include(route: beta) }
                try server.endTick()
                try simulator.advance(simulator.now + 16_667)
                for entry in drain(client) {
                    let parts = entry.split(separator: " ")
                    if parts[0] == "message" {
                        let value = Int(parts[2], radix: 16)!
                        if parts[1] == "\(alpha)" { alphaValue = value } else { betaValue = value }
                        continue
                    }
                    XCTAssertEqual(parts[0], "tick", entry)
                    let number = Int(parts[1])!
                    XCTAssertGreaterThan(number, lastTick, "seed \(seed)")
                    lastTick = number
                    ticks += 1
                    // A complete tick shows Alpha and Beta of one round.
                    XCTAssertEqual(betaValue, alphaValue - alphaValue % 3, "seed \(seed) tick \(number)")
                }
            }
            for _ in 0..<200 { try simulator.advance(simulator.now + 16_667) }
            for entry in drain(client) {
                let parts = entry.split(separator: " ")
                guard parts[0] == "message" else { continue }
                let value = Int(parts[2], radix: 16)!
                if parts[1] == "\(alpha)" { alphaValue = value } else { betaValue = value }
            }
            XCTAssertEqual(client.state, .connected)
            XCTAssertEqual(alphaValue, 199, "seed \(seed)")
            XCTAssertEqual(betaValue, 198, "seed \(seed)")
            XCTAssertGreaterThan(ticks, 20, "seed \(seed)")
        }
    }

    func testTickCallsCheckTheirOrderAndRoutes() throws {
        let simulator = try connected(.message)
        let server = simulator.server
        XCTAssertThrowsError(try server.include(route: alpha))
        XCTAssertThrowsError(try server.endTick())
        try server.beginTick()
        XCTAssertThrowsError(try server.beginTick())
        XCTAssertThrowsError(try server.include(route: log))
        XCTAssertThrowsError(try server.include(route: commands))
        XCTAssertThrowsError(try server.include(route: alpha))
        _ = server.publish(route: alpha, payload: [1])
        try server.include(route: alpha)
        try server.endTick()

        // A topic the client left is no member.
        try server.unsubscribe(route: beta)
        try server.beginTick()
        _ = server.publish(route: alpha, payload: [2])
        XCTAssertEqual(server.publish(route: beta, payload: [2]), .notSubscribed)
        try server.endTick()
        try settle(simulator)
        XCTAssertEqual(drain(simulator.client), ["message \(alpha) 01", "tick 0", "message \(alpha) 02", "tick 1"])

        server.close("bye")
        try server.beginTick()
        XCTAssertEqual(try server.endTick(), .notConnected)
    }
}
