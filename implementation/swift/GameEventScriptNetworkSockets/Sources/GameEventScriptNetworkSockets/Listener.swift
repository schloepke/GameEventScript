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
    ///   - options: The options of every server connection, using `.stream`; the defaults when omitted.
    /// - Returns: The listener.
    /// - Throws: `GesApiError.invalidArgument` when the options use another transport or are out of range, or
    ///   name no key while their lowest security level is above none, as the defaults do; a SwiftNIO error when the
    ///   address cannot be bound.
    public static func listenTcp(host: String, port: Int, network: GesBoundNetwork, options: GesNetworkOptions? = nil) async throws -> sending GesNetworkSocketListener {
        try await listen(host: host, port: port, path: nil, network: network, options: options)
    }

    /// Starts accepting WebSocket clients for server connections of the message transport.
    /// - Parameters:
    ///   - host: The local address, such as `127.0.0.1` or `::`.
    ///   - port: The local port; 0 lets the system choose.
    ///   - path: The request path clients upgrade on, starting with `/`; other paths receive 404.
    ///   - network: The bound network of every session.
    ///   - options: The options of every server connection, using `.message`; the defaults when omitted.
    /// - Returns: The listener. It speaks plain `ws`; a browser client needs `wss` through a TLS-terminating proxy.
    /// - Throws: `GesApiError.invalidArgument` when the path does not start with `/`, or the options use another
    ///   transport or are out of range, or name no key while their lowest security level is above none, as the
    ///   defaults do; a SwiftNIO error when the address cannot be bound.
    public static func listenWebSocket(host: String, port: Int, path: String, network: GesBoundNetwork, options: GesNetworkOptions? = nil) async throws -> sending GesNetworkSocketListener {
        if !path.hasPrefix("/") { throw GesApiError.invalidArgument("The path starts with '/'.") }
        return try await listen(host: host, port: port, path: path, network: network, options: options)
    }

    // The options of every server connection with the given transport, checked by creating a connection, so a
    // listener never accepts clients it cannot serve; returns them with the packet limit.
    static func resolve(_ options: GesNetworkOptions?, _ transport: GesNetworkTransport, _ network: GesBoundNetwork) throws -> (GesNetworkOptions, Int) {
        var resolved = options ?? GesNetworkOptions()
        if options == nil { resolved.transport = transport }
        if resolved.transport != transport { throw GesApiError.invalidArgument("The options must use the \(transport) transport.") }
        return (resolved, try GesNetworkConnection.makeServer(network: network, now: 0, options: resolved).maxPacketBytes)
    }

    private static func listen(host: String, port: Int, path: String?, network: GesBoundNetwork, options: GesNetworkOptions?) async throws -> sending GesNetworkSocketListener {
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
                    if let path {
                        queue.beginUpgrade()
                        try child.pipeline.syncOperations.addHandler(GesNetworkSocketServerUpgradeHandler(path: path, limit: limit, queue: queue))
                    } else {
                        let inbox = GesNetworkSocketInbox()
                        try child.pipeline.syncOperations.addHandler(GesNetworkSocketStreamHandler(inbox: inbox))
                        queue.offer(child, inbox)
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

// The clients that are ready or still upgrading, shared between the event loops and the host. Accepting pauses
// while they reach the capacity and resumes when the host takes one.
final class GesNetworkSocketQueue: @unchecked Sendable {
    static let capacity = 64

    private let lock = NIOLock()
    // A UDP client is its peer on the listener's shared channel.
    private var entries: [(Channel, GesNetworkSocketInbox, GesNetworkDatagramPeer?)] = []
    private var head = 0
    private var upgrading = 0
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

    func beginUpgrade() { lock.withLock { upgrading += 1 } }

    func upgradeEnded() {
        let server = lock.withLock {
            upgrading -= 1
            return release()
        }
        server?.read()
    }

    func offer(_ channel: Channel, _ inbox: GesNetworkSocketInbox) {
        let accepted = lock.withLock {
            if closed { return false }
            entries.append((channel, inbox, nil))
            return true
        }
        if !accepted { channel.close(promise: nil) }
    }

    // Queues a validated UDP client; false when the queue is full or closed, and the client sends hello again.
    func offer(_ channel: Channel, _ peer: GesNetworkDatagramPeer) -> Bool {
        lock.withLock {
            if closed || entries.count - head + upgrading >= Self.capacity { return false }
            entries.append((channel, peer.inbox, peer))
            return true
        }
    }

    func take() -> (Channel, GesNetworkSocketInbox, GesNetworkDatagramPeer?)? {
        let (entry, server): ((Channel, GesNetworkSocketInbox, GesNetworkDatagramPeer?)?, Channel?) = lock.withLock {
            if head == entries.count { return (nil, nil) }
            let entry = entries[head]
            head += 1
            if head == entries.count {
                entries.removeAll(keepingCapacity: true)
                head = 0
            }
            return (entry, release())
        }
        server?.read()
        return entry
    }

    func close() {
        let pending = lock.withLock {
            closed = true
            let pending = entries[head...].map { ($0.0, $0.2) }
            entries.removeAll()
            head = 0
            return pending
        }
        for (channel, peer) in pending {
            if let peer { peer.release() } else { channel.close(promise: nil) }
        }
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
