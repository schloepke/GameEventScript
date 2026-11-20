// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import NIOConcurrencyHelpers
import NIOCore
import NIOPosix

/// Accepts TCP, WebSocket or UDP clients for a server. SwiftNIO event loops accept sockets and, for WebSocket, answer
/// the HTTP upgrade, or for UDP read the shared socket, validate unknown addresses and route each datagram to its
/// client; the host takes each ready client with `accept(now:)` where it runs its sessions.
///
/// A listener is not `Sendable`; it belongs to one isolation domain at a time and may be transferred to another.
/// Releasing it closes it like `close()`.
public final class GesNetworkSocketListener {
    /// The address and port the listener accepts on, with the port the system chose for port 0.
    public let localEndPoint: SocketAddress

    private let channel: Channel
    private let queue: GesNetworkSocketQueue
    private let network: GesBoundNetwork
    private let options: GesNetworkOptions
    private let framing: GesNetworkSocketSession.Framing
    // A UDP listener's socket, which replaces its channel when SwiftNIO closes it; nil for TCP and WebSocket.
    private let datagram: (current: () -> Channel?, close: () -> Void)?
    private var closed = false

    init(
        channel: Channel,
        queue: GesNetworkSocketQueue,
        network: GesBoundNetwork,
        options: GesNetworkOptions,
        framing: GesNetworkSocketSession.Framing,
        datagram: (current: () -> Channel?, close: () -> Void)? = nil
    ) {
        self.channel = channel
        self.datagram = datagram
        self.queue = queue
        self.network = network
        self.options = options
        self.framing = framing
        localEndPoint = channel.localAddress!
    }

    deinit { close() }

    // The channel the listener currently reads on.
    var socket: Channel? { datagram?.current() ?? channel }

    /// Starts accepting TCP clients for server connections of the stream transport.
    /// - Parameters:
    ///   - host: The local address, such as `127.0.0.1` or `::`.
    ///   - port: The local port; 0 lets the system choose.
    ///   - network: The bound network of every session.
    ///   - options: The options of every server connection, using `.stream`.
    /// - Returns: The listener.
    /// - Throws: `GesApiError.invalidArgument` when the options use another transport, name an insecure test key seed or are out of range, or
    ///   set no server security; a SwiftNIO error when the
    ///   address cannot be bound.
    public static func listenTcp(host: String, port: Int, network: GesBoundNetwork, options: GesNetworkOptions) async throws -> sending GesNetworkSocketListener {
        try await listen(host: host, port: port, path: nil, network: network, options: options)
    }

    /// Starts accepting WebSocket clients for server connections of the message transport.
    /// - Parameters:
    ///   - host: The local address, such as `127.0.0.1` or `::`.
    ///   - port: The local port; 0 lets the system choose.
    ///   - path: The request path clients upgrade on, starting with `/`; other paths receive 404.
    ///   - network: The bound network of every session.
    ///   - options: The options of every server connection, using `.message`.
    /// - Returns: The listener. It speaks plain `ws`; a browser client needs `wss` through a TLS-terminating proxy.
    /// - Throws: `GesApiError.invalidArgument` when the path does not start with `/`, or the options use another
    ///   transport, name an insecure test key seed or are out of range, or set no server security; a SwiftNIO error when the address cannot be bound.
    public static func listenWebSocket(host: String, port: Int, path: String, network: GesBoundNetwork, options: GesNetworkOptions) async throws -> sending GesNetworkSocketListener {
        if !path.hasPrefix("/") { throw GesApiError.invalidArgument("The path starts with '/'.") }
        return try await listen(host: host, port: port, path: path, network: network, options: options)
    }

    // The options of every server connection with the given transport, checked by creating a connection, so a
    // listener never accepts clients it cannot serve; returns them with the packet limit.
    static func resolve(_ resolved: GesNetworkOptions, _ transport: GesNetworkTransport, _ network: GesBoundNetwork) throws -> (GesNetworkOptions, Int) {
        if resolved.transport != transport { throw GesApiError.invalidArgument("The options must use the \(transport) transport.") }
        if resolved.insecureTestKeySeed != nil { throw GesApiError.invalidArgument("A socket transport refuses insecure test keys.") }
        return (resolved, try GesNetworkConnection.makeServer(network: network, now: 0, options: resolved).maxPacketBytes)
    }

    private static func listen(host: String, port: Int, path: String?, network: GesBoundNetwork, options: GesNetworkOptions) async throws -> sending GesNetworkSocketListener {
        let (resolved, limit) = try resolve(options, path == nil ? .stream : .message, network)
        let queue = GesNetworkSocketQueue()
        let channel = try await ServerBootstrap(group: MultiThreadedEventLoopGroup.singleton)
            .serverChannelOption(.backlog, value: 256)
            .serverChannelOption(.socketOption(.so_reuseaddr), value: 1)
            .serverChannelOption(.maxMessagesPerRead, value: 1)
            .serverChannelInitializer { channel in
                channel.eventLoop.makeCompletedFuture { try channel.pipeline.syncOperations.addHandler(GesNetworkSocketGateHandler(queue: queue)) }
            }
            .childChannelOption(.tcpOption(.tcp_nodelay), value: 1)
            .childChannelOption(.recvAllocator, value: FixedSizeRecvByteBufferAllocator(capacity: GesNetworkSocketInbox.readBytes))
            .childChannelInitializer { child in
                child.eventLoop.makeCompletedFuture {
                    let address = child.remoteAddress?.ipAddress
                    if let path {
                        if !queue.beginUpgrade(address) { throw GesNetworkSocketQueue.Refused() }
                        try child.pipeline.syncOperations.addHandler(GesNetworkSocketServerUpgradeHandler(path: path, limit: limit, address: address, queue: queue))
                    } else {
                        if !queue.enter(address) { throw GesNetworkSocketQueue.Refused() }
                        let inbox = GesNetworkSocketInbox(signal: queue.signal)
                        do {
                            try child.pipeline.syncOperations.addHandler(GesNetworkSocketStreamHandler(inbox: inbox))
                        } catch {
                            queue.leave(address)
                            throw error
                        }
                        queue.offer(child, inbox, address)
                    }
                }
            }
            .bind(host: host, port: port).get()
        queue.attach(channel)
        return GesNetworkSocketListener(channel: channel, queue: queue, network: network, options: resolved, framing: path == nil ? .stream : .webSocket)
    }

    /// Takes the next client that is ready, without waiting, and creates its server connection at `now`. Call
    /// `GesNetworkSocketSession.update(now:)` to run it.
    /// - Parameter now: The current time in microseconds, from which the connection's timeout runs.
    /// - Returns: The session of the client, or nil when no client is ready.
    /// - Throws: `GesApiError.invalidOperation` when the listener is closed.
    public func accept(now: Int64) throws -> GesNetworkSocketSession? {
        if closed { throw GesApiError.invalidOperation("The listener is closed.") }
        guard let (child, inbox, peer) = queue.take() else { return nil }
        let connection = try GesNetworkConnection.makeServer(network: network, now: now, options: options)
        return GesNetworkSocketSession(connection: connection, channel: child, inbox: inbox, framing: framing, peer: peer)
    }

    /// Stops accepting and closes every client not yet taken. TCP and WebSocket sessions already taken stay open; UDP
    /// sessions share the listener's socket, so they stop carrying packets and their connections time out. Closing
    /// again does nothing.
    public func close() {
        if closed { return }
        closed = true
        if let datagram { datagram.close() } else { channel.close(promise: nil) }
        queue.close()
    }
}

extension GesNetworkSocketListener: GesNetworkAcceptor {
    // Wakes the end that waits for this listener.
    var signal: GesNetworkSocketSignal { queue.signal }
}

// The clients that are ready or still upgrading, shared between the event loops and the host. Accepting pauses
// while they reach the capacity and resumes when the host takes one; an IP address may hold at most `perAddress` of
// them, and its further sockets are closed.
final class GesNetworkSocketQueue: @unchecked Sendable {
    static let capacity = 64
    static let perAddress = 8

    // Closes a socket the queue refuses.
    struct Refused: Error {}

    private typealias Entry = (channel: Channel, inbox: GesNetworkSocketInbox, peer: GesNetworkDatagramPeer?, address: String?)

    // Wakes the end that waits for the listener's clients and their sockets.
    let signal = GesNetworkSocketSignal()
    private let lock = NIOLock()
    // A UDP client is its peer on the listener's shared channel.
    private var entries: [Entry] = []
    private var head = 0
    private var upgrading = 0
    // The ready and upgrading clients of each IP address.
    private var addresses: [String: Int] = [:]
    private var held = false
    private var closed = false
    private var server: Channel?

    func attach(_ channel: Channel) {
        let resume = lock.withLock {
            server = channel
            return held
        }
        if resume { channel.read() }
    }

    // Whether the listener may accept another socket; otherwise it holds until a place is free.
    func mayAccept() -> Bool {
        lock.withLock {
            if !closed && entries.count - head + upgrading < Self.capacity { return true }
            held = true
            return false
        }
    }

    // Counts a client of the address; false when the address holds `perAddress` clients or the queue is closed.
    func enter(_ address: String?) -> Bool {
        lock.withLock { enterLocked(address) }
    }

    // Gives back the place of a client of the address that is not queued.
    func leave(_ address: String?) {
        lock.withLock { leaveLocked(address) }
    }

    // Counts an upgrading client of the address; false like `enter(_:)`.
    func beginUpgrade(_ address: String?) -> Bool {
        lock.withLock {
            if !enterLocked(address) { return false }
            upgrading += 1
            return true
        }
    }

    // Ends an upgrade; a client that is not queued gives its address's place back.
    func upgradeEnded(_ address: String?, queued: Bool) {
        let server = lock.withLock {
            upgrading -= 1
            if !queued { leaveLocked(address) }
            return release()
        }
        server?.read()
    }

    func offer(_ channel: Channel, _ inbox: GesNetworkSocketInbox, _ address: String?) {
        let accepted = lock.withLock {
            if closed { return false }
            entries.append((channel, inbox, nil, address))
            return true
        }
        if accepted { signal.set() } else { channel.close(promise: nil) }
    }

    // Queues a validated UDP client; false when the queue is full or closed or its IP address holds `perAddress`
    // clients, and the client sends hello again.
    func offer(_ channel: Channel, _ peer: GesNetworkDatagramPeer) -> Bool {
        let accepted = lock.withLock {
            if entries.count - head + upgrading >= Self.capacity { return false }
            let address = peer.address.ipAddress
            if !enterLocked(address) { return false }
            entries.append((channel, peer.inbox, peer, address))
            return true
        }
        if accepted { signal.set() }
        return accepted
    }

    func take() -> (Channel, GesNetworkSocketInbox, GesNetworkDatagramPeer?)? {
        let (entry, server): (Entry?, Channel?) = lock.withLock {
            if head == entries.count { return (nil, nil) }
            let entry = entries[head]
            head += 1
            if head == entries.count {
                entries.removeAll(keepingCapacity: true)
                head = 0
            }
            leaveLocked(entry.address)
            return (entry, release())
        }
        server?.read()
        guard let entry else { return nil }
        return (entry.channel, entry.inbox, entry.peer)
    }

    func close() {
        let pending = lock.withLock {
            closed = true
            let pending = entries[head...].map { ($0.channel, $0.peer) }
            entries.removeAll()
            head = 0
            addresses.removeAll()
            return pending
        }
        for (channel, peer) in pending {
            if let peer { peer.release() } else { channel.close(promise: nil) }
        }
    }

    private func enterLocked(_ address: String?) -> Bool {
        if closed { return false }
        guard let address else { return true }
        let count = addresses[address, default: 0]
        if count >= Self.perAddress { return false }
        addresses[address] = count + 1
        return true
    }

    private func leaveLocked(_ address: String?) {
        guard let address, let count = addresses[address] else { return }
        if count <= 1 { addresses[address] = nil } else { addresses[address] = count - 1 }
    }

    // The server channel to read on when accepting held and a place is free; called under the lock.
    private func release() -> Channel? {
        if !held || closed || entries.count - head + upgrading >= Self.capacity { return nil }
        held = false
        return server
    }
}

// Holds the server channel's reads while the queue is full.
final class GesNetworkSocketGateHandler: ChannelOutboundHandler {
    typealias OutboundIn = Never

    private let queue: GesNetworkSocketQueue

    init(queue: GesNetworkSocketQueue) { self.queue = queue }

    func read(context: ChannelHandlerContext) {
        if queue.mayAccept() { context.read() }
    }
}
