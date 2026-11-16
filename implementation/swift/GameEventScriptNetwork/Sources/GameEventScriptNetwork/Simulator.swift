// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Connects a client and a server connection in one process with seeded latency.
///
/// Each packet is delayed by the latency plus a draw from 0 to the jitter inclusive, but never arrives before a packet
/// sent earlier in the same direction. Equal seeds, options and inputs give equal packets, events and times. The host
/// takes the events of both connections between calls of `advance(_:)`. Not thread-safe.
public final class GesNetworkSimulator {
    private struct Flight {
        let time: Int64
        let sequence: Int64
        let bytes: [UInt8]
        let length: Int
    }

    /// The client connection.
    public let client: GesNetworkConnection
    /// The server connection.
    public let server: GesNetworkConnection
    /// The simulated time in microseconds.
    public private(set) var now: Int64

    private let random: GesRandomGenerator
    private let latency: Int64
    private let jitter: Int64
    private var toServer: [Flight] = []
    private var toServerHead = 0
    private var toClient: [Flight] = []
    private var toClientHead = 0
    private var free: [[UInt8]] = []
    private var buffer = [UInt8](repeating: 0, count: GesNetworkWire.maximumPacketBytes + 3)
    private var lastToServer: Int64
    private var lastToClient: Int64
    private var sequence: Int64 = 0

    /// Creates a simulated network between two connections created at the same time.
    /// - Parameters:
    ///   - client: The client connection.
    ///   - server: The server connection.
    ///   - seed: The seed of the random generator that draws the jitter.
    ///   - latencyMicroseconds: The base latency of every packet in microseconds.
    ///   - jitterMicroseconds: The largest additional delay in microseconds.
    /// - Throws: `GesApiError.invalidArgument` when the connections are not a client and a server at the same time, or
    ///   the latency or jitter is negative.
    public init(client: GesNetworkConnection, server: GesNetworkConnection, seed: Int64, latencyMicroseconds: Int64, jitterMicroseconds: Int64) throws {
        if !client.isClient || server.isClient { throw GesApiError.invalidArgument("The simulator connects a client and a server.") }
        if client.now != server.now { throw GesApiError.invalidArgument("Both connections start at the same time.") }
        if latencyMicroseconds < 0 { throw GesApiError.invalidArgument("The latency is not negative.") }
        if jitterMicroseconds < 0 { throw GesApiError.invalidArgument("The jitter is not negative.") }
        self.client = client
        self.server = server
        random = GesRandomGenerator(seed: seed)
        latency = latencyMicroseconds
        jitter = jitterMicroseconds
        now = client.now
        lastToServer = client.now
        lastToClient = client.now
    }

    /// The number of packets on their way.
    public var packetsInFlight: Int { toServer.count - toServerHead + toClient.count - toClientHead }

    /// The earliest time at which a packet arrives or a connection has a deadline.
    public var nextDeadline: Int64 {
        var deadline = min(client.nextDeadline, server.nextDeadline)
        if toServerHead < toServer.count { deadline = min(deadline, toServer[toServerHead].time) }
        if toClientHead < toClient.count { deadline = min(deadline, toClient[toClientHead].time) }
        return max(deadline, now)
    }

    /// Runs the network up to `now`: both connections transmit, and each arrival and deadline is processed at its
    /// time in order, the client's packets before the server's at equal times.
    /// - Parameter now: The new time in microseconds, not earlier than `now`.
    /// - Throws: `GesApiError.invalidArgument` when the time goes back.
    public func advance(_ now: Int64) throws {
        if now < self.now { throw GesApiError.invalidArgument("Time does not go back.") }
        while true {
            try transmitAll()
            let next = nextDeadline
            if next > now { break }
            self.now = next
            let serverDue = toServerHead < toServer.count && toServer[toServerHead].time == next
            let clientDue = toClientHead < toClient.count && toClient[toClientHead].time == next
            if serverDue && (!clientDue || toServer[toServerHead].sequence < toClient[toClientHead].sequence) {
                try deliver(toServer: true)
            } else if clientDue {
                try deliver(toServer: false)
            } else {
                try client.advance(next)
                try server.advance(next)
            }
        }

        self.now = now
        try client.advance(now)
        try server.advance(now)
        try transmitAll()
    }

    private func deliver(toServer direction: Bool) throws {
        let flight: Flight
        if direction {
            flight = toServer[toServerHead]
            toServerHead += 1
            if toServerHead == toServer.count {
                toServer.removeAll(keepingCapacity: true)
                toServerHead = 0
            }
        } else {
            flight = toClient[toClientHead]
            toClientHead += 1
            if toClientHead == toClient.count {
                toClient.removeAll(keepingCapacity: true)
                toClientHead = 0
            }
        }
        try flight.bytes.withUnsafeBytes { try (direction ? server : client).receive(UnsafeRawBufferPointer(rebasing: $0[0..<flight.length]), now: now) }
        free.append(flight.bytes)
    }

    private func transmitAll() throws {
        try send(client, toServer: true)
        try send(server, toServer: false)
    }

    private func send(_ sender: GesNetworkConnection, toServer direction: Bool) throws {
        while true {
            let length = try sender.transmit(into: &buffer)
            if length == 0 { return }
            let delay = latency + random.nextInclusiveInteger(0, jitter)
            var bytes = free.popLast() ?? [UInt8](repeating: 0, count: buffer.count)
            bytes.withUnsafeMutableBytes { target in
                buffer.withUnsafeBytes { target.baseAddress!.copyMemory(from: $0.baseAddress!, byteCount: length) }
            }
            if direction {
                lastToServer = max(now + delay, lastToServer)
                toServer.append(Flight(time: lastToServer, sequence: sequence, bytes: bytes, length: length))
            } else {
                lastToClient = max(now + delay, lastToClient)
                toClient.append(Flight(time: lastToClient, sequence: sequence, bytes: bytes, length: length))
            }
            sequence += 1
        }
    }
}

/// The in-process transport: a `GesNetworkSimulator` without latency, so a packet arrives at the time it is sent.
/// Not thread-safe.
public final class GesNetworkLoopback {
    private let simulator: GesNetworkSimulator

    /// Connects a client and a server connection created at the same time.
    /// - Parameters:
    ///   - client: The client connection.
    ///   - server: The server connection.
    /// - Throws: `GesApiError.invalidArgument` when the connections are not a client and a server at the same time.
    public init(client: GesNetworkConnection, server: GesNetworkConnection) throws {
        simulator = try GesNetworkSimulator(client: client, server: server, seed: 0, latencyMicroseconds: 0, jitterMicroseconds: 0)
    }

    /// The client connection.
    public var client: GesNetworkConnection { simulator.client }
    /// The server connection.
    public var server: GesNetworkConnection { simulator.server }
    /// The time in microseconds.
    public var now: Int64 { simulator.now }
    /// The earliest deadline of both connections.
    public var nextDeadline: Int64 { simulator.nextDeadline }

    /// Exchanges all due packets and runs every deadline up to `now`.
    /// - Parameter now: The new time in microseconds, not earlier than `now`.
    /// - Throws: `GesApiError.invalidArgument` when the time goes back.
    public func advance(_ now: Int64) throws { try simulator.advance(now) }
}
