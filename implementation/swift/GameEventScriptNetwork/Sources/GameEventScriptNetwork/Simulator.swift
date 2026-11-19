// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Connects a client and a server connection in one process with seeded latency.
///
/// Each packet is delayed by the latency plus a draw from 0 to the jitter inclusive, but never arrives before a packet
/// sent earlier in the same direction. Between datagram connections it may also lose, duplicate and reorder packets.
/// Equal seeds, options and inputs give equal packets, events and times. The host takes the events of both connections
/// between calls of `advance(_:)`. Not thread-safe.
public final class GesNetworkSimulator {
    private struct Flight {
        let time: Int64
        let sequence: Int64
        var bytes: [UInt8]
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
    private let loss: Int
    private let duplicate: Int
    private let reorder: Bool
    // The packets on their way in arrival order from their head: by time, then in the order sent.
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
    ///   - lossPerMille: The share of packets lost in per mille, 0 to 1000.
    ///   - duplicatePerMille: The share of packets delivered twice in per mille, 0 to 1000.
    ///   - reorder: Whether a packet arrives after its own delay instead of behind packets sent earlier.
    /// - Throws: `GesApiError.invalidArgument` when the connections are not a client and a server at the same time, the
    ///   latency or jitter is negative, a rate is outside 0 to 1000, or loss, duplicates or reordering are requested for
    ///   connections that do not both use `GesNetworkTransport.datagram`.
    public init(
        client: GesNetworkConnection,
        server: GesNetworkConnection,
        seed: Int64,
        latencyMicroseconds: Int64,
        jitterMicroseconds: Int64,
        lossPerMille: Int = 0,
        duplicatePerMille: Int = 0,
        reorder: Bool = false
    ) throws {
        if !client.isClient || server.isClient { throw GesApiError.invalidArgument("The simulator connects a client and a server.") }
        if client.now != server.now { throw GesApiError.invalidArgument("Both connections start at the same time.") }
        if latencyMicroseconds < 0 { throw GesApiError.invalidArgument("The latency is not negative.") }
        if jitterMicroseconds < 0 { throw GesApiError.invalidArgument("The jitter is not negative.") }
        if lossPerMille < 0 || lossPerMille > 1000 { throw GesApiError.invalidArgument("The loss is 0 to 1000 per mille.") }
        if duplicatePerMille < 0 || duplicatePerMille > 1000 { throw GesApiError.invalidArgument("The duplicates are 0 to 1000 per mille.") }
        if (lossPerMille > 0 || duplicatePerMille > 0 || reorder) && (client.transport != .datagram || server.transport != .datagram) {
            throw GesApiError.invalidArgument("Loss, duplicates and reordering require datagram connections.")
        }
        loss = lossPerMille
        duplicate = duplicatePerMille
        self.reorder = reorder
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
        let deadline = min(client.nextDeadline, server.nextDeadline, arrival(toServer: true), arrival(toServer: false))
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
            if next > now || next == .max { break }
            self.now = next
            let serverDue = arrival(toServer: true) == next
            let clientDue = arrival(toServer: false) == next
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

    // A stream or message connection that takes no bytes leaves its packets waiting until its frame bucket refills.
    private func arrival(toServer direction: Bool) -> Int64 {
        let receiver = direction ? server : client
        let time: Int64
        if direction {
            if toServerHead == toServer.count { return .max }
            time = toServer[toServerHead].time
        } else {
            if toClientHead == toClient.count { return .max }
            time = toClient[toClientHead].time
        }
        return receiver.canReceive ? time : max(time, receiver.nextDeadline)
    }

    private func deliver(toServer direction: Bool) throws {
        // The bucket refills up to the arrival; if it still holds no frame, the packet waits for the next refill.
        let receiver = direction ? server : client
        try receiver.advance(now)
        if !receiver.canReceive { return }
        var flight = direction ? Self.pop(&toServer, &toServerHead) : Self.pop(&toClient, &toClientHead)
        try flight.bytes.withUnsafeBytes { try receiver.receive(UnsafeRawBufferPointer(rebasing: $0[0..<flight.length]), now: now) }
        free.append(flight.bytes)
        flight.bytes = []
    }

    // The queue gives up its reference to the bytes, so the free buffer is unique and is reused without a copy. The
    // delivered prefix goes once it is half the queue, so the storage stays bounded while packets remain in flight.
    private static func pop(_ queue: inout [Flight], _ head: inout Int) -> Flight {
        let flight = queue[head]
        queue[head].bytes = []
        head += 1
        if head == queue.count {
            queue.removeAll(keepingCapacity: true)
            head = 0
        } else if head >= 32 && head * 2 >= queue.count {
            queue.removeSubrange(0..<head)
            head = 0
        }
        return flight
    }

    private func transmitAll() throws {
        try send(client, toServer: true)
        try send(server, toServer: false)
    }

    private func send(_ sender: GesNetworkConnection, toServer direction: Bool) throws {
        while true {
            let length = try sender.transmit(into: &buffer)
            if length == 0 { return }
            let delay = nextDelay()
            if loss > 0 && random.nextInclusiveInteger(0, 999) < Int64(loss) { continue }
            enqueue(toServer: direction, delay, length)
            if duplicate > 0 && random.nextInclusiveInteger(0, 999) < Int64(duplicate) { enqueue(toServer: direction, nextDelay(), length) }
        }
    }

    private func nextDelay() -> Int64 { latency + random.nextInclusiveInteger(0, jitter) }

    // Adds a packet in arrival order; without reordering it arrives no earlier than the packet sent before.
    private func enqueue(toServer direction: Bool, _ delay: Int64, _ length: Int) {
        var time = now + delay
        if !reorder {
            if direction {
                time = max(time, lastToServer)
                lastToServer = time
            } else {
                time = max(time, lastToClient)
                lastToClient = time
            }
        }
        var bytes = free.popLast() ?? [UInt8](repeating: 0, count: buffer.count)
        bytes.withUnsafeMutableBytes { target in
            buffer.withUnsafeBytes { target.baseAddress!.copyMemory(from: $0.baseAddress!, byteCount: length) }
        }
        let flight = Flight(time: time, sequence: sequence, bytes: bytes, length: length)
        sequence += 1
        if direction {
            Self.insert(flight, into: &toServer, after: toServerHead)
        } else {
            Self.insert(flight, into: &toClient, after: toClientHead)
        }
    }

    private static func insert(_ flight: Flight, into queue: inout [Flight], after head: Int) {
        var index = queue.count
        while index > head && queue[index - 1].time > flight.time { index -= 1 }
        queue.insert(flight, at: index)
    }
}
