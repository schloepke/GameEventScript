// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import NIOCore
import NIOPosix
import NIOSSL
import NIOWebSocket

/// Carries one `GesNetworkConnection` over a TCP stream, a WebSocket or UDP. SwiftNIO event loops only move bytes: the
/// host calls `update(now:)` on its own task or thread, which feeds received bytes into the connection, advances it
/// and hands its packets to the socket. The connection is touched only by the host.
///
/// A session is not `Sendable`; it belongs to one isolation domain at a time and may be transferred to another.
/// Releasing it closes the socket like `close()`.
public final class GesNetworkSocketSession {
    /// The connection this session carries. Use it only where `update(now:)` is called.
    public let connection: GesNetworkConnection

    // How packets travel on the channel.
    enum Framing {
        case stream
        case webSocket
        case datagram
    }

    private let channel: Channel
    private let inbox: GesNetworkSocketInbox
    private let framing: Framing
    // The remote address of a UDP session, which sends through it; nil for every other session.
    let peer: GesNetworkDatagramPeer?
    private var transmitBuffer: [UInt8]
    private var chunks: [ByteBuffer] = []
    private var lastWrite: EventLoopFuture<Void>?
    private var outgoingCompleted = false
    private var closed = false

    init(connection: GesNetworkConnection, channel: Channel, inbox: GesNetworkSocketInbox, framing: Framing, peer: GesNetworkDatagramPeer? = nil) {
        self.connection = connection
        self.channel = channel
        self.inbox = inbox
        self.framing = framing
        self.peer = peer
        transmitBuffer = [UInt8](repeating: 0, count: connection.maxPacketBytes + 3)
    }

    deinit {
        if !closed { release() }
    }

    /// Opens a TCP connection for a client connection of the stream transport.
    /// - Parameters:
    ///   - host: The server's host name or address.
    ///   - port: The server's port.
    ///   - connection: A client connection created with `.stream`; its timeout runs from its creation.
    /// - Returns: The session.
    /// - Throws: `GesApiError.invalidArgument` when the connection is no client of the stream transport or uses insecure test keys; a SwiftNIO
    ///   error when the connection cannot be opened.
    public static func connectTcp(host: String, port: Int, connection: sending GesNetworkConnection) async throws -> sending GesNetworkSocketSession {
        try require(connection, .stream, client: true)
        let inbox = GesNetworkSocketInbox()
        let channel = try await ClientBootstrap(group: MultiThreadedEventLoopGroup.singleton)
            .channelOption(.tcpOption(.tcp_nodelay), value: 1)
            .channelOption(.recvAllocator, value: FixedSizeRecvByteBufferAllocator(capacity: GesNetworkSocketInbox.readBytes))
            .channelInitializer { channel in
                channel.eventLoop.makeCompletedFuture { try channel.pipeline.syncOperations.addHandler(GesNetworkSocketStreamHandler(inbox: inbox)) }
            }
            .connect(host: host, port: port).get()
        return GesNetworkSocketSession(connection: connection, channel: channel, inbox: inbox, framing: .stream)
    }

    /// Opens a WebSocket for a client connection of the message transport.
    /// - Parameters:
    ///   - url: The `ws` or `wss` endpoint, such as `wss://example.org/game?room=1`; IPv6 hosts stand in brackets.
    ///   - connection: A client connection created with `.message`; its timeout runs from its creation.
    /// - Returns: The session.
    /// - Throws: `GesApiError.invalidArgument` when the connection is no client of the message transport or uses insecure test keys or the URL
    ///   is no `ws` or `wss` endpoint; `GesNetworkSocketError` when the server refuses the upgrade; a SwiftNIO error
    ///   when the connection cannot be opened.
    public static func connectWebSocket(url: String, connection: sending GesNetworkConnection) async throws -> sending GesNetworkSocketSession {
        try require(connection, .message, client: true)
        let endpoint = try GesNetworkSocketEndpoint(url)
        let tls = try endpoint.secure ? NIOSSLContext(configuration: .makeClientConfiguration()) : nil
        let inbox = GesNetworkSocketInbox()
        let limit = connection.maxPacketBytes
        let loop = MultiThreadedEventLoopGroup.singleton.next()
        let upgraded = loop.makePromise(of: Void.self)
        let channel: Channel
        do {
            channel = try await ClientBootstrap(group: loop)
                .channelOption(.tcpOption(.tcp_nodelay), value: 1)
                .channelOption(.recvAllocator, value: FixedSizeRecvByteBufferAllocator(capacity: GesNetworkSocketInbox.readBytes))
                .channelInitializer { channel in
                    channel.eventLoop.makeCompletedFuture {
                        let pipeline = channel.pipeline.syncOperations
                        if let tls { try pipeline.addHandler(NIOSSLClientHandler(context: tls, serverHostname: endpoint.serverName)) }
                        try pipeline.addHandler(GesNetworkSocketClientUpgradeHandler(endpoint: endpoint, inbox: inbox, limit: limit, upgraded: upgraded))
                    }
                }
                .connect(host: endpoint.host, port: endpoint.port).get()
        } catch {
            // The handler may never have run; a promise must not be left open.
            upgraded.fail(error)
            throw error
        }

        do {
            try await upgraded.futureResult.get()
        } catch {
            channel.close(promise: nil)
            throw error
        }
        return GesNetworkSocketSession(connection: connection, channel: channel, inbox: inbox, framing: .webSocket)
    }

    static func require(_ connection: GesNetworkConnection, _ transport: GesNetworkTransport, client: Bool) throws {
        if connection.transport != transport { throw GesApiError.invalidArgument("The connection must use the \(transport) transport.") }
        if connection.isClient != client { throw GesApiError.invalidArgument(client ? "The connection must be a client." : "The connection must be a server.") }
        if connection.usesInsecureTestKeys { throw GesApiError.invalidArgument("A socket transport refuses insecure test keys.") }
    }

    /// Feeds the received bytes into the connection, advances it to `now`, reports the end of the transport and sends
    /// every due packet. Call it where the session lives, then take the connection's events.
    /// - Parameter now: The current time in microseconds, not earlier than the connection's time.
    /// - Throws: `GesApiError.invalidOperation` when the session is closed; `GesApiError.invalidArgument` when the
    ///   time goes back.
    public func update(now: Int64) throws {
        if closed { throw GesApiError.invalidOperation("The session is closed.") }
        // The end flag is taken with the chunks, so no chunk received before the end is left behind.
        let (ended, resume) = inbox.take(into: &chunks)
        defer { chunks.removeAll(keepingCapacity: true) }
        for chunk in chunks {
            try chunk.withUnsafeReadableBytes { try connection.receive($0, now: now) }
        }

        if resume { channel.read() }
        try connection.advance(now)
        if ended { connection.transportClosed() }
        if outgoingCompleted { return }
        var wrote = false
        while true {
            let length = try transmitBuffer.withUnsafeMutableBytes { try connection.transmit(into: $0) }
            if length == 0 { break }
            var buffer = channel.allocator.buffer(capacity: length)
            transmitBuffer.withUnsafeBytes { _ = buffer.writeBytes(UnsafeRawBufferPointer(rebasing: $0[0..<length])) }
            switch framing {
            case .webSocket:
                let frame = WebSocketFrame(fin: true, opcode: .binary, maskKey: connection.isClient ? .random() : nil, data: buffer)
                lastWrite = channel.write(frame)
            case .datagram:
                // The socket is not connected, so each datagram names its address.
                lastWrite = peer?.write(buffer)
            case .stream:
                lastWrite = channel.write(buffer)
            }
            wrote = true
        }

        if wrote {
            if let peer { peer.flush() } else { channel.flush() }
        }
        if connection.state == .closed {
            outgoingCompleted = true
            finish()
        }
    }

    /// Closes the socket without sending anything more. A server session of a UDP listener gives its client's
    /// address back to address validation instead, because it shares the listener's socket. Closing again does nothing.
    public func close() {
        if closed { return }
        closed = true
        release()
    }

    private func release() {
        if let peer {
            peer.release()
        } else {
            channel.close(promise: nil)
        }
    }

    // The connection closed and its last packet is queued: end the transport in order.
    private func finish() {
        switch framing {
        case .stream:
            break
        case .webSocket:
            var status = channel.allocator.buffer(capacity: 2)
            status.writeInteger(UInt16(1000))
            let frame = WebSocketFrame(fin: true, opcode: .connectionClose, maskKey: connection.isClient ? .random() : nil, data: status)
            channel.writeAndFlush(frame, promise: nil)
            return
        case .datagram:
            // UDP has no end; a closed server connection gives its address back to address validation.
            guard let peer, peer.isShared else { return }
            (lastWrite ?? channel.eventLoop.makeSucceededVoidFuture()).whenComplete { _ in peer.release() }
            return
        }

        let channel = channel
        (lastWrite ?? channel.eventLoop.makeSucceededVoidFuture()).whenComplete { _ in channel.close(mode: .output, promise: nil) }
    }
}
