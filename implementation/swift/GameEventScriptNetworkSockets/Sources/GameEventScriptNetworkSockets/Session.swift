// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import NIOConcurrencyHelpers
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
    private let writer: GesNetworkSocketWriter
    // The packets of one update, transmitted straight into the buffer the writer hands to the event loop.
    private var outgoing: ByteBuffer
    private let packetBytes: Int
    private var chunks: [ByteBuffer] = []
    private var outgoingCompleted = false
    private var closed = false

    init(connection: GesNetworkConnection, channel: Channel, inbox: GesNetworkSocketInbox, framing: Framing, peer: GesNetworkDatagramPeer? = nil) {
        self.connection = connection
        self.channel = channel
        self.inbox = inbox
        self.framing = framing
        self.peer = peer
        packetBytes = connection.maxPacketBytes + 3
        writer = GesNetworkSocketWriter(channel: channel, framing: framing, peer: peer, masks: connection.isClient)
        outgoing = channel.allocator.buffer(capacity: framing == .stream ? GesNetworkSocketWriter.streamBytes : 0)
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
        // A stream carries the packets back to back; every other framing gets each packet with its length.
        let prefix = framing == .stream ? 0 : 4
        var room = writer.room
        while room > 0 {
            let length = try outgoing.writeWithUnsafeMutableBytes(minimumWritableBytes: prefix + packetBytes) { bytes in
                let length = try connection.transmit(into: UnsafeMutableRawBufferPointer(rebasing: bytes[prefix..<prefix + packetBytes]))
                if length == 0 { return 0 }
                if prefix != 0 { bytes.storeBytes(of: UInt32(length).bigEndian, toByteOffset: 0, as: UInt32.self) }
                return prefix + length
            }
            if length == 0 { break }
            room -= length
        }

        let closed = connection.state == .closed
        if closed { outgoingCompleted = true }
        if outgoing.readableBytes > 0 || closed { writer.hand(&outgoing, finish: closed) }
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
}

// Hands the packets of the host thread to the event loop. The host transmits the packets of an update into one buffer
// and hands it over whole, so an update costs one event loop task. Three buffers rotate between the host, the hand-over
// and the event loop and are reused once the socket wrote them.
private final class GesNetworkSocketWriter: @unchecked Sendable {
    // The initial capacity of a stream's buffers.
    static let streamBytes = 65536

    // Packets wait in the connection while this many bytes are handed over and not yet taken by the event loop, or
    // while the channel holds more unwritten bytes than its high water mark, so a slow peer fills the connection's
    // send queue rather than this session.
    static let unwrittenBytes = 262144

    private let channel: Channel
    private let framing: GesNetworkSocketSession.Framing
    private let peer: GesNetworkDatagramPeer?
    private let masks: Bool
    private let lock = NIOLock()
    // Guarded by the lock.
    private var queued: ByteBuffer
    private var scheduled = false
    private var finishing = false
    // Used only on the channel's event loop.
    private var taken: ByteBuffer

    init(channel: Channel, framing: GesNetworkSocketSession.Framing, peer: GesNetworkDatagramPeer?, masks: Bool) {
        self.channel = channel
        self.framing = framing
        self.peer = peer
        self.masks = masks
        queued = channel.allocator.buffer(capacity: 0)
        taken = queued
    }

    // How many more bytes the host may hand over now.
    var room: Int {
        if let peer {
            if let socket = peer.socket, !socket.isWritable { return 0 }
        } else if !channel.isWritable {
            return 0
        }
        return Self.unwrittenBytes - lock.withLock { queued.readableBytes }
    }

    // Takes the bytes of the outgoing buffer and leaves it empty. Finishing ends the transport after these bytes.
    func hand(_ outgoing: inout ByteBuffer, finish: Bool) {
        let schedule: Bool = lock.withLock {
            if queued.readableBytes == 0 {
                swap(&queued, &outgoing)
            } else {
                queued.writeBuffer(&outgoing)
            }
            if finish { finishing = true }
            if scheduled { return false }
            scheduled = true
            return true
        }
        outgoing.clear()
        if schedule { channel.eventLoop.execute { self.drain() } }
    }

    private func drain() {
        let finish: Bool = lock.withLock {
            swap(&queued, &taken)
            scheduled = false
            defer { finishing = false }
            return finishing
        }

        switch framing {
        case .stream:
            if taken.readableBytes == 0 {
                if finish { channel.close(mode: .output, promise: nil) }
                return
            }
            if finish {
                // The output closes once the last packets are written.
                let channel = channel
                let written = channel.eventLoop.makePromise(of: Void.self)
                written.futureResult.whenComplete { _ in channel.close(mode: .output, promise: nil) }
                channel.writeAndFlush(taken, promise: written)
            } else {
                channel.writeAndFlush(taken, promise: nil)
            }
            taken.moveReaderIndex(to: taken.writerIndex)
        case .webSocket:
            var wrote = false
            while let length = taken.readInteger(as: UInt32.self), let packet = taken.readSlice(length: Int(length)) {
                channel.write(WebSocketFrame(fin: true, opcode: .binary, maskKey: masks ? .random() : nil, data: packet), promise: nil)
                wrote = true
            }
            if finish {
                var status = channel.allocator.buffer(capacity: 2)
                status.writeInteger(UInt16(1000))
                channel.writeAndFlush(WebSocketFrame(fin: true, opcode: .connectionClose, maskKey: masks ? .random() : nil, data: status), promise: nil)
            } else if wrote {
                channel.flush()
            }
        case .datagram:
            guard let peer else { return }
            // The socket is not connected, so each datagram names its address; while the socket is being replaced
            // the datagrams are lost.
            if let socket = peer.socket {
                var wrote = false
                while let length = taken.readInteger(as: UInt32.self), let packet = taken.readSlice(length: Int(length)) {
                    socket.write(AddressedEnvelope(remoteAddress: peer.address, data: packet), promise: nil)
                    wrote = true
                }
                if wrote { socket.flush() }
            }
            taken.moveReaderIndex(to: taken.writerIndex)
            // UDP has no end; a closed server connection gives its address back to address validation.
            if finish && peer.isShared { peer.release() }
        }
    }
}
