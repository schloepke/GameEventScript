// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// A client end and a server end connected within one process, without sockets.
///
/// A realtime pair runs on the system clock and moves packets when the ends poll. A synchronous pair runs on a virtual
/// clock, is connected when created and moves every packet before a send, publish, tick, close or poll returns: what
/// one end sends is reported by the other end's next poll. Both ends of a pair belong to one caller at a time.
public final class GesNetworkPair {
    // The state the ends, the connector, the acceptor and the links share. It refers to the ends weakly, so that the
    // ends own it and no cycle remains.
    final class Core {
        let clock: any GesClock
        let virtualClock: GesVirtualClock?
        let options: GesNetworkOptions
        var packetBytes = GesNetworkWire.minimumPacketBytes
        var pool: [[UInt8]] = []
        var links: [Link] = []
        var accepting: [Link] = []
        var exchanging = false
        weak var client: GesNetworkClientEnd?
        weak var server: GesNetworkServerEnd?

        init(synchronous: Bool, options: GesNetworkOptions) {
            if synchronous {
                let clock = GesVirtualClock()
                self.clock = clock
                virtualClock = clock
            } else {
                clock = GesSystemClock()
                virtualClock = nil
            }
            self.options = options
        }

        // Moves packets between the links of a synchronous pair until none moves. Waits while an end polls, because
        // the event the handler holds borrows the connection's buffers.
        func exchange() {
            guard let virtualClock, !exchanging, client?.polling != true, server?.polling != true else { return }
            exchanging = true
            defer { exchanging = false }
            let now = virtualClock.elapsedMicroseconds
            for _ in 0..<64 {
                var moved = false
                for link in links where link.move() { moved = true }
                if !moved { return }
                for link in links { link.receiveQuietly(now) }
            }
        }

        func rent() -> [UInt8] { pool.popLast() ?? [UInt8](repeating: 0, count: packetBytes) }

        func remove(_ link: Link) {
            if let index = links.firstIndex(where: { $0 === link }) { links.remove(at: index) }
        }
    }

    private final class Connector: GesNetworkConnector {
        let core: Core

        init(_ core: Core) { self.core = core }

        var transport: GesNetworkTransport { core.options.transport }

        func connect(_ connection: GesNetworkConnection) throws -> any GesNetworkLink {
            let server = try GesNetworkConnection.makeServer(network: connection.network, now: core.clock.elapsedMicroseconds, options: core.options)
            let clientLink = Link(core, connection)
            let serverLink = Link(core, server)
            clientLink.peer = serverLink
            serverLink.peer = clientLink
            core.links.append(clientLink)
            core.links.append(serverLink)
            core.accepting.append(serverLink)
            return clientLink
        }
    }

    private final class Acceptor: GesNetworkAcceptor {
        let core: Core

        init(_ core: Core, _ network: GesBoundNetwork) throws {
            self.core = core
            // Validates the server options when the pair is created.
            _ = try GesNetworkConnection.makeServer(network: network, now: 0, options: core.options)
        }

        func accept(now: Int64) throws -> Link? { core.accepting.isEmpty ? nil : core.accepting.removeFirst() }

        func close() {
            let pending = core.accepting
            core.accepting.removeAll()
            for link in pending { link.close() }
        }
    }

    // One side of an in-process connection: packets wait in the inbox of the receiving side. A link refers to the
    // shared state without owning it: its end owns the connector or acceptor that does.
    final class Link: GesNetworkLink {
        unowned let core: Core
        let connection: GesNetworkConnection
        weak var peer: Link?
        private var inbox: [(bytes: [UInt8], length: Int)] = []
        private var inboxHead = 0
        private(set) var closed = false
        private var ended = false

        init(_ core: Core, _ connection: GesNetworkConnection) {
            self.core = core
            self.connection = connection
        }

        func receive(now: Int64) throws {
            if ended { return }
            let now = max(now, connection.now)
            while inboxHead < inbox.count {
                let (bytes, length) = inbox[inboxHead]
                inbox[inboxHead] = ([], 0)
                inboxHead += 1
                defer { core.pool.append(bytes) }
                try bytes.withUnsafeBytes { try connection.receive(UnsafeRawBufferPointer(rebasing: $0[0..<length]), now: now) }
            }
            inbox.removeAll(keepingCapacity: true)
            inboxHead = 0
            try connection.advance(now)
            if peer?.closed ?? true || closed {
                ended = true
                connection.transportClosed()
            }
        }

        // The links of a pair keep time with the connection, so receiving cannot fail on time.
        func receiveQuietly(_ now: Int64) { try? receive(now: now) }

        func transmit() throws { _ = move() }

        // Hands every due packet to the peer's inbox; returns whether one moved.
        func move() -> Bool {
            if closed || ended { return false }
            var moved = false
            while true {
                var buffer = core.rent()
                let length = (try? connection.transmit(into: &buffer)) ?? 0
                if length == 0 {
                    core.pool.append(buffer)
                    return moved
                }
                if let peer, !peer.closed {
                    peer.inbox.append((buffer, length))
                } else {
                    core.pool.append(buffer)
                }
                moved = true
            }
        }

        // The packets already handed over still reach the peer, which then sees the transport end.
        func close() {
            if closed { return }
            closed = true
            for index in inboxHead..<inbox.count { core.pool.append(inbox[index].bytes) }
            inbox.removeAll()
            inboxHead = 0
            core.remove(self)
            if peer?.ended ?? true { return }
            core.exchange()
        }
    }

    private let core: Core

    /// The client end.
    public let client: GesNetworkClientEnd
    /// The server end.
    public let server: GesNetworkServerEnd

    private init(network: GesBoundNetwork, token: [UInt8], options: GesNetworkOptions, synchronous: Bool) throws {
        core = Core(synchronous: synchronous, options: options)
        client = try GesNetworkClientEnd(network: network, connector: Connector(core), options: options, token: token, clock: core.clock)
        core.packetBytes = max(client.connection.maxPacketBytes + 3, GesNetworkWire.minimumPacketBytes)
        server = try GesNetworkServerEnd(network: network, acceptor: Acceptor(core, network), clock: core.clock)
        core.client = client
        core.server = server
        if !synchronous { return }
        client.pair = core
        server.pair = core
        // The handshake runs now; the connected events wait for the first polls.
        var round = 0
        while round < 16 && client.connection.state == .hello {
            try client.poll({ _ in }, report: false)
            try server.poll({ _ in }, report: false)
            round += 1
        }
        if client.connection.state != .connected {
            throw GesApiError.invalidOperation("The handshake of the pair did not complete: \(client.connection.state).")
        }
    }

    /// Creates a realtime pair on the system clock. The ends connect while they poll.
    /// - Parameters:
    ///   - network: The bound network both ends share.
    ///   - token: The token the server end reports with the client's `connecting` event.
    ///   - options: The options of both connections; `GesNetworkOptions.inProcess()` by default.
    /// - Returns: The pair.
    /// - Throws: `GesApiError.invalidArgument` when the options or the token are invalid.
    public static func realtime(network: GesBoundNetwork, token: [UInt8] = [], options: GesNetworkOptions = .inProcess()) throws -> GesNetworkPair {
        try GesNetworkPair(network: network, token: token, options: options, synchronous: false)
    }

    /// Creates a synchronous pair on a virtual clock that starts at 0. It is connected when this returns, and both ends
    /// report their connected events on their first polls. The server accepts the client without a `connecting` event.
    /// - Parameters:
    ///   - network: The bound network both ends share.
    ///   - token: The client's token.
    ///   - options: The options of both connections; `GesNetworkOptions.inProcess()` by default.
    /// - Returns: The connected pair.
    /// - Throws: `GesApiError.invalidArgument` when the options or the token are invalid;
    ///   `GesApiError.invalidOperation` when the handshake does not complete, for example with mismatched securities.
    public static func synchronous(network: GesBoundNetwork, token: [UInt8] = [], options: GesNetworkOptions = .inProcess()) throws -> GesNetworkPair {
        try GesNetworkPair(network: network, token: token, options: options, synchronous: true)
    }

    /// Whether the pair runs on a virtual clock.
    public var isSynchronous: Bool { core.virtualClock != nil }

    /// The time of the pair's clock in microseconds.
    public var now: Int64 { core.clock.elapsedMicroseconds }

    /// Moves the virtual clock of a synchronous pair forward, runs the deadlines of both ends and moves the packets
    /// they produce. Events wait for the next polls.
    /// - Parameter microseconds: The whole microseconds to add.
    /// - Throws: `GesApiError.invalidOperation` when the pair is realtime or an end is being polled;
    ///   `GesApiError.invalidArgument` when the amount is negative.
    public func advance(by microseconds: Int64) throws {
        guard let virtualClock = core.virtualClock else { throw GesApiError.invalidOperation("Only a synchronous pair has a virtual clock.") }

        // The event a handler holds borrows the connection's buffers, which receiving would overwrite.
        if core.client?.polling == true || core.server?.polling == true { throw GesApiError.invalidOperation("An end of the pair is being polled.") }
        try virtualClock.advance(by: microseconds)
        let now = virtualClock.elapsedMicroseconds
        for link in core.links { try link.receive(now: now) }
        core.exchange()
    }

    /// Releases both ends.
    public func release() {
        client.release()
        server.release()
    }
}
