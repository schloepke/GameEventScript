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
/// host calls `receive(now:)` and `transmit()`, or `update(now:)` for both, on its own task or thread, which feeds
/// received bytes into the connection, advances it and hands its packets to the socket. The connection is touched only
/// by the host. As a `GesNetworkLink` it is the transport of an end.
///
/// A session is not `Sendable`; it belongs to one isolation domain at a time and may be transferred to another.
/// Releasing it closes the socket like `close()`.
public final class GesNetworkSocketSession {
    /// The connection this session carries. Use it only where `receive(now:)` and `transmit()` are called.
    public let connection: GesNetworkConnection

    // How packets travel on the channel.
    enum Framing {
        case stream
        case webSocket
        case datagram
    }

    // Nil while a connector's transport still opens.
    private var channel: Channel?
    private let inbox: GesNetworkSocketInbox
    private let framing: Framing
    // The remote address of a UDP session, which sends through it; nil for every other session.
    private(set) var peer: GesNetworkDatagramPeer?
    private var writer: GesNetworkSocketWriter?
    // The transport a connector opens in the background; nil for a session created open.
    private let opening: GesNetworkSocketOpening?
    // The packets of one update, transmitted straight into the buffer the writer hands to the event loop.
    private var outgoing: ByteBuffer
    private let packetBytes: Int
    private var chunks: [ByteBuffer] = []
    private var nextChunk = 0
    private var inboxEnded = false
    private var outgoingCompleted = false
    private var closed = false

    init(connection: GesNetworkConnection, channel: Channel, inbox: GesNetworkSocketInbox, framing: Framing, peer: GesNetworkDatagramPeer? = nil) {
        self.connection = connection
        self.inbox = inbox
        self.framing = framing
        packetBytes = connection.maxPacketBytes + 3
        outgoing = channel.allocator.buffer(capacity: framing == .stream ? GesNetworkSocketWriter.streamBytes : 0)
        opening = nil
        start(channel, peer)
    }

    // A session whose transport opens in the background: packets wait in the connection until it is open, and a
    // failure to open ends the transport through the inbox.
    init(connection: GesNetworkConnection, inbox: GesNetworkSocketInbox, framing: Framing, opening: GesNetworkSocketOpening) {
        self.connection = connection
        self.inbox = inbox
        self.framing = framing
        packetBytes = connection.maxPacketBytes + 3
        outgoing = ByteBufferAllocator().buffer(capacity: framing == .stream ? GesNetworkSocketWriter.streamBytes : 0)
        self.opening = opening
    }

    private func start(_ channel: Channel, _ peer: GesNetworkDatagramPeer?) {
        self.channel = channel
        self.peer = peer
        writer = GesNetworkSocketWriter(channel: channel, framing: framing, peer: peer, masks: connection.isClient, inbox: inbox)
    }

    // Takes the transport once the connector opened it.
    private func adopt() {
        if channel == nil, let (channel, peer) = opening?.take() { start(channel, peer) }
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
    ///   error when the connection cannot be opened or is not opened within 10 s.
    public static func connectTcp(host: String, port: Int, connection: sending GesNetworkConnection) async throws -> sending GesNetworkSocketSession {
        try require(connection, .stream, client: true)
        let inbox = GesNetworkSocketInbox()
        let channel = try await openTcp(host: host, port: port, inbox: inbox)
        return GesNetworkSocketSession(connection: connection, channel: channel, inbox: inbox, framing: .stream)
    }

    static func openTcp(host: String, port: Int, inbox: GesNetworkSocketInbox) async throws -> Channel {
        try await ClientBootstrap(group: MultiThreadedEventLoopGroup.singleton)
            .connectTimeout(GesNetworkSocketUpgrade.timeout)
            .channelOption(.tcpOption(.tcp_nodelay), value: 1)
            .channelOption(.recvAllocator, value: FixedSizeRecvByteBufferAllocator(capacity: GesNetworkSocketInbox.readBytes))
            .channelInitializer { channel in
                channel.eventLoop.makeCompletedFuture { try channel.pipeline.syncOperations.addHandler(GesNetworkSocketStreamHandler(inbox: inbox)) }
            }
            .connect(host: host, port: port).get()
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
        let inbox = GesNetworkSocketInbox()
        let channel = try await openWebSocket(endpoint: endpoint, limit: connection.maxPacketBytes, inbox: inbox)
        return GesNetworkSocketSession(connection: connection, channel: channel, inbox: inbox, framing: .webSocket)
    }

    static let upgradeTimedOut = "The server did not complete the WebSocket upgrade in time."

    static func openWebSocket(endpoint: GesNetworkSocketEndpoint, limit: Int, inbox: GesNetworkSocketInbox) async throws -> Channel {
        let tls = try endpoint.secure ? NIOSSLContext(configuration: .makeClientConfiguration()) : nil
        let loop = MultiThreadedEventLoopGroup.singleton.next()
        let upgraded = loop.makePromise(of: Void.self)
        // A server that accepts the connection but never completes TLS or the upgrade must not hold it open: connecting,
        // TLS and the upgrade together have the same time limit as an upgrade on a listener.
        let deadline = NIODeadline.now() + GesNetworkSocketUpgrade.timeout
        let channel: Channel
        do {
            channel = try await ClientBootstrap(group: loop)
                .connectTimeout(GesNetworkSocketUpgrade.timeout)
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
            // The handler may never have run; a promise must not be left open. The time limit ending while connecting is
            // the same failure as ending during the upgrade.
            upgraded.fail(error)
            if case ChannelError.connectTimeout = error { throw GesNetworkSocketError(Self.upgradeTimedOut) }
            throw error
        }

        let timeout = channel.eventLoop.scheduleTask(deadline: deadline) {
            upgraded.fail(GesNetworkSocketError(Self.upgradeTimedOut))
        }
        do {
            try await upgraded.futureResult.get()
            timeout.cancel()
        } catch {
            timeout.cancel()
            channel.close(promise: nil)
            throw error
        }
        return channel
    }

    static func require(_ connection: GesNetworkConnection, _ transport: GesNetworkTransport, client: Bool) throws {
        if connection.transport != transport { throw GesApiError.invalidArgument("The connection must use the \(transport) transport.") }
        if connection.isClient != client { throw GesApiError.invalidArgument(client ? "The connection must be a client." : "The connection must be a server.") }
        if connection.usesInsecureTestKeys { throw GesApiError.invalidArgument("A socket transport refuses insecure test keys.") }
    }

    /// Feeds the received bytes into the connection, advances it to `now`, reports the end of the transport and sends
    /// every due packet: `receive(now:)` followed by `transmit()`. Call it where the session lives, then take the
    /// connection's events.
    /// - Parameter now: The current time in microseconds, not earlier than the connection's time.
    /// - Throws: `GesApiError.invalidOperation` when the session is closed; `GesApiError.invalidArgument` when the
    ///   time goes back.
    public func update(now: Int64) throws {
        try receive(now: now)
        try transmit()
    }

    /// Feeds the received bytes into the connection, advances it to `now` and reports the end of the transport. Call
    /// it where the session lives. While `GesNetworkConnection.canReceive` is false the bytes wait, and once the
    /// received chunks fill the inbox the socket stops reading, so the peer's sending slows down.
    /// - Parameter now: The current time in microseconds, not earlier than the connection's time.
    /// - Throws: `GesApiError.invalidOperation` when the session is closed; `GesApiError.invalidArgument` when the
    ///   time goes back.
    public func receive(now: Int64) throws {
        if closed { throw GesApiError.invalidOperation("The session is closed.") }
        adopt()
        // Advancing refills the frame bucket, which may let in the chunks that still wait. It follows the first feed,
        // so the timeout does not run out while received bytes wait.
        var drained = try feed(now)
        try connection.advance(now)
        if !drained { drained = try feed(now) }
        if inboxEnded && drained { connection.transportClosed() }
    }

    // Takes new chunks from the inbox only once the taken ones are fed, so a full inbox holds the socket's reading.
    private func feed(_ now: Int64) throws -> Bool {
        while true {
            if nextChunk == chunks.count {
                chunks.removeAll(keepingCapacity: true)
                nextChunk = 0
                // The end flag is taken with the chunks, so no chunk received before the end is left behind.
                let (ended, resume) = inbox.take(into: &chunks)
                inboxEnded = ended
                if resume { channel?.read() }
                if chunks.isEmpty { return true }
            }
            if !connection.canReceive { return false }
            let chunk = chunks[nextChunk]
            nextChunk += 1
            try chunk.withUnsafeReadableBytes { try connection.receive($0, now: now) }
        }
    }

    /// Hands every packet the connection has due to the socket. While a connector's transport still opens the
    /// packets wait in the connection. Call it where the session lives.
    /// - Throws: `GesApiError.invalidOperation` when the session is closed.
    public func transmit() throws {
        if closed { throw GesApiError.invalidOperation("The session is closed.") }
        adopt()
        guard let writer, !outgoingCompleted else { return }
        try fill(writer.room)
        // The connection may close while it transmits; its close packet then leaves within the same fill.
        let closed = connection.state == .closed
        if closed { outgoingCompleted = true }
        if outgoing.readableBytes > 0 || closed { writer.hand(&outgoing, finish: closed) }
    }

    // Transmits the connection's due packets into the outgoing buffer, up to about the given bytes. A closed connection
    // holds only its close packet, which leaves past that bound, also when the connection closes during the fill. A
    // stream carries the packets back to back; every other framing gets each packet with its length.
    private func fill(_ room: Int) throws {
        let prefix = framing == .stream ? 0 : 4
        var room = room
        while room > 0 || connection.state == .closed {
            let length = try outgoing.writeWithUnsafeMutableBytes(minimumWritableBytes: prefix + packetBytes) { bytes in
                let length = try connection.transmit(into: UnsafeMutableRawBufferPointer(rebasing: bytes[prefix..<prefix + packetBytes]))
                if length == 0 { return 0 }
                if prefix != 0 { bytes.storeBytes(of: UInt32(length).bigEndian, toByteOffset: 0, as: UInt32.self) }
                return prefix + length
            }
            if length == 0 { break }
            room -= length
        }
    }

    /// Closes the socket after the packets already handed to it; nothing more is sent. When the connection is closed
    /// while the transport still opens, its last packets are written once the transport opened. A server session of a
    /// UDP listener gives its client's address back to address validation instead, because it shares the listener's
    /// socket. Closing again does nothing.
    public func close() {
        if closed { return }
        closed = true
        adopt()
        if channel == nil, let opening, connection.state == .closed, (try? fill(0)) != nil {
            let plan = GesNetworkSocketOpening.Plan(framing: framing, masks: connection.isClient, inbox: inbox)
            if opening.finish(outgoing, plan) { return }
        }
        release()
    }

    private func release() {
        if let channel {
            // The writer ends the transport after the close packet of a closed connection.
            if outgoingCompleted { Self.releaseInOrder(channel, peer, inbox) } else { Self.release(channel, peer) }
        } else if let (channel, peer) = opening?.release() {
            Self.release(channel, peer)
        }
    }

    // A disposed session of a closed connection writes its last packets for at most this long.
    static let orderlyEnd = TimeAmount.seconds(10)

    static func release(_ channel: Channel, _ peer: GesNetworkDatagramPeer?) {
        if let peer {
            peer.release()
        } else {
            channel.close(promise: nil)
        }
    }

    // Releases a transport whose output the writer ends after the last packets. Closing a TCP socket at once would
    // discard the packets that are not yet written, and closing it while received bytes are unread resets the
    // connection; so the bytes that still arrive are read and dropped until the peer ends its side, which closes the
    // channel, or until the time limit.
    static func releaseInOrder(_ channel: Channel, _ peer: GesNetworkDatagramPeer?, _ inbox: GesNetworkSocketInbox) {
        if let peer {
            peer.release()
            return
        }
        inbox.discard()
        channel.read()
        let limit = channel.eventLoop.scheduleTask(in: orderlyEnd) { channel.close(promise: nil) }
        channel.closeFuture.whenComplete { _ in limit.cancel() }
    }
}

extension GesNetworkSocketSession: GesNetworkLink {}

extension GesNetworkSocketOpening {
    // How a session that closed while its transport opened writes its last packets.
    struct Plan {
        let framing: GesNetworkSocketSession.Framing
        let masks: Bool
        let inbox: GesNetworkSocketInbox

        // Writes the packets on the open transport, ends it after them and releases it.
        func finish(_ packets: ByteBuffer, on channel: Channel, _ peer: GesNetworkDatagramPeer?) {
            var packets = packets
            GesNetworkSocketWriter(channel: channel, framing: framing, peer: peer, masks: masks, inbox: inbox).hand(&packets, finish: true)
            GesNetworkSocketSession.releaseInOrder(channel, peer, inbox)
        }
    }
}

// The transport a connector opens on SwiftNIO's event loops for a session on the host: the host takes it once it is
// open. A transport that opens after the session closed is released at once, after the last packets of its closed
// connection.
final class GesNetworkSocketOpening: @unchecked Sendable {
    private enum State {
        case pending
        case opened(Channel, GesNetworkDatagramPeer?)
        case finishing(ByteBuffer, Plan)
        case done
    }

    private let lock = NIOLock()
    private var state = State.pending

    // Hands the open transport to the session, or writes the last packets of a session that closed meanwhile; false
    // when the session closed without them, and the caller releases the transport.
    func complete(_ channel: Channel, _ peer: GesNetworkDatagramPeer?) -> Bool {
        let finishing: (ByteBuffer, Plan)?? = lock.withLock {
            switch state {
            case .pending:
                state = .opened(channel, peer)
                return .some(nil)
            case .finishing(let packets, let plan):
                state = .done
                return (packets, plan)
            case .opened, .done:
                return nil
            }
        }
        guard let finishing else { return false }
        if let (packets, plan) = finishing { plan.finish(packets, on: channel, peer) }
        return true
    }

    // Keeps the last packets of a closed session for the transport that still opens, or writes them on one that
    // opened but was not taken; false when the opening ended, and the session releases what remains.
    func finish(_ packets: ByteBuffer, _ plan: Plan) -> Bool {
        let opened: (Channel, GesNetworkDatagramPeer?)?? = lock.withLock {
            switch state {
            case .pending:
                state = .finishing(packets, plan)
                return .some(nil)
            case .opened(let channel, let peer):
                state = .done
                return (channel, peer)
            case .finishing, .done:
                return nil
            }
        }
        guard let opened else { return false }
        if let (channel, peer) = opened { plan.finish(packets, on: channel, peer) }
        return true
    }

    // The open transport, once; nil while it still opens.
    func take() -> (Channel, GesNetworkDatagramPeer?)? {
        lock.withLock {
            guard case .opened(let channel, let peer) = state else { return nil }
            state = .done
            return (channel, peer)
        }
    }

    // Ends the opening: returns a transport that opened but was not taken, for the session to release.
    func release() -> (Channel, GesNetworkDatagramPeer?)? {
        lock.withLock {
            defer { state = .done }
            guard case .opened(let channel, let peer) = state else { return nil }
            return (channel, peer)
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
    private let inbox: GesNetworkSocketInbox
    private let lock = NIOLock()
    // Guarded by the lock.
    private var queued: ByteBuffer
    private var scheduled = false
    private var finishing = false
    // Whether the host found no room, so taking the queued bytes wakes its end.
    private var held = false
    // Used only on the channel's event loop.
    private var taken: ByteBuffer

    init(channel: Channel, framing: GesNetworkSocketSession.Framing, peer: GesNetworkDatagramPeer?, masks: Bool, inbox: GesNetworkSocketInbox) {
        self.channel = channel
        self.framing = framing
        self.peer = peer
        self.masks = masks
        self.inbox = inbox
        queued = channel.allocator.buffer(capacity: 0)
        taken = queued
    }

    // How many more bytes the host may hand over now. Without room, the end wakes once the event loop takes the
    // queued bytes or the channel becomes writable again.
    var room: Int {
        if let peer {
            if let socket = peer.socket, !socket.isWritable { return 0 }
        } else if !channel.isWritable {
            return 0
        }
        return lock.withLock {
            let room = Self.unwrittenBytes - queued.readableBytes
            if room <= 0 { held = true }
            return room
        }
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
        let (finish, wake): (Bool, Bool) = lock.withLock {
            swap(&queued, &taken)
            scheduled = false
            defer {
                finishing = false
                held = false
            }
            return (finishing, held)
        }
        if wake { inbox.wake() }

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
