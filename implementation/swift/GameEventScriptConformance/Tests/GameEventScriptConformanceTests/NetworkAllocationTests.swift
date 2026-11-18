// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import ConformanceInstrumentation
import GameEventScriptNetwork
import GameEventScriptRuntime
import XCTest

/// A warmed network session allocates nothing per round, plain and encrypted, over streams and datagrams.
final class NetworkAllocationTests: XCTestCase {
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

    func testWarmedStreamSessionAllocatesNothing() throws {
        try measure(transport: .stream, encrypted: false)
        try measure(transport: .stream, encrypted: true)
    }

    func testWarmedDatagramSessionAllocatesNothing() throws {
        try measure(transport: .datagram, encrypted: false)
        try measure(transport: .datagram, encrypted: true)
    }

    private func options(_ transport: GesNetworkTransport, encrypted: Bool, server: Bool) throws -> GesNetworkOptions {
        var options = GesNetworkOptions()
        options.clientSecurity = .unencrypted
        options.serverSecurity = .unencrypted
        options.transport = transport
        options.maxPacketBytes = 1200
        if encrypted {
            options.clientSecurity = .development
            options.insecureTestKeySeed = 7
            if server { options.serverSecurity = .key(try GesNetworkKey(privateKey: (1...32).map { UInt8($0) })) }
        }
        return options
    }

    private func measure(transport: GesNetworkTransport, encrypted: Bool) throws {
        #if !canImport(Darwin)
            throw XCTSkip("The allocation counter uses libmalloc.")
        #else
            guard ges_allocation_install(), ges_allocation_controls() else { throw XCTSkip("The allocation counter is not available.") }
            let label = "\(transport) encrypted: \(encrypted)"
            let network = try GesSchemaSet(parsing: Self.source).network("Game").bind()
            let client = try GesNetworkConnection.makeClient(network: network, now: 0, options: try options(transport, encrypted: encrypted, server: false))
            let server = try GesNetworkConnection.makeServer(network: network, now: 0, options: try options(transport, encrypted: encrypted, server: true))
            // Datagrams travel through random loss, duplication and reordering; streams arrive in order.
            let datagram = transport == .datagram
            let simulator = try GesNetworkSimulator(
                client: client,
                server: server,
                seed: 3,
                latencyMicroseconds: datagram ? 20_000 : 0,
                jitterMicroseconds: datagram ? 15_000 : 0,
                lossPerMille: datagram ? 100 : 0,
                duplicatePerMille: datagram ? 50 : 0,
                reorder: datagram
            )
            try simulator.advance(0)
            var step = 0
            while step < 1000 && server.state == .awaitHello {
                try simulator.advance(simulator.nextDeadline)
                step += 1
            }
            XCTAssertEqual(server.nextEvent().kind, .connecting, label)
            try server.accept()
            step = 0
            while step < 1000 && client.state == .hello {
                try simulator.advance(simulator.nextDeadline)
                step += 1
            }
            XCTAssertEqual(client.state, .connected, label)
            XCTAssertEqual(client.security, encrypted ? .encrypted : .none, label)
            try server.subscribe(route: world)

            let state = UnsafeMutableRawBufferPointer.allocate(byteCount: 2, alignment: 8)
            let data = UnsafeMutableRawBufferPointer.allocate(byteCount: 3000, alignment: 8)
            let command = UnsafeMutableRawBufferPointer.allocate(byteCount: 1, alignment: 8)
            defer {
                state.deallocate()
                data.deallocate()
                command.deallocate()
            }
            state.initializeMemory(as: UInt8.self, repeating: 0)
            for index in 0..<data.count { data[index] = UInt8(truncatingIfNeeded: index * 7 + 9) }
            command[0] = 3
            var received = 0

            func round() throws {
                state[0] &+= 1
                _ = server.publish(route: world, payload: UnsafeRawBufferPointer(state))
                _ = server.send(route: blob, payload: UnsafeRawBufferPointer(data))
                _ = server.send(route: bulkBlob, payload: UnsafeRawBufferPointer(data))
                _ = client.send(route: commands, payload: UnsafeRawBufferPointer(command))
                try simulator.advance(simulator.now + 16_667)
                drain(client)
                drain(server)
            }

            func drain(_ connection: GesNetworkConnection) {
                while true {
                    let next = connection.nextEvent()
                    if next.kind == .none { break }
                    received &+= next.payload.count
                }
            }

            // Random loss reaches rare new peaks of held messages and packets in flight, and the buffers grow to each.
            // With this seed the last growth happens before round 5500.
            for _ in 0..<6000 { try round() }
            XCTAssertTrue(ges_allocation_begin())
            for _ in 0..<2500 { try round() }
            let sample = ges_allocation_end()
            XCTAssertTrue(sample.valid, label)
            XCTAssertEqual(sample.allocations, 0, label)
            XCTAssertEqual(sample.bytes, 0, label)
            XCTAssertEqual(client.state, .connected, label)
            XCTAssertGreaterThan(received, 0, label)
        #endif
    }
}
