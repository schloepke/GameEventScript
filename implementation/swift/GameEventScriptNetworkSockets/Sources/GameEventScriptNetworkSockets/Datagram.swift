// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import NIOConcurrencyHelpers
import NIOCore
import NIOPosix

extension GesNetworkSocketListener {
    /// Starts receiving UDP datagrams for server connections of the datagram transport. Datagrams of an unknown
    /// address pass through address validation with a random key of this listener; a client whose `hello` carries a
    /// valid cookie becomes ready for `accept(now:)`. The datagrams of a known address go to its session. Errors of
    /// single datagrams, such as an unreachable network, do not end the listener.
    /// - Parameters:
    ///   - host: The local address, such as `127.0.0.1` or `::`.
    ///   - port: The local port; 0 lets the system choose.
    ///   - network: The bound network of every session.
    ///   - options: The options of every server connection, using `.datagram`.
    /// - Returns: The listener. Its sessions send through its socket, so close it only after them.
    /// - Throws: `GesApiError.invalidArgument` when the options use another transport, name an insecure test key seed or are out of range, or
    ///   set no server security; a SwiftNIO error when the
    ///   address cannot be resolved or bound.
    public static func listenUdp(host: String, port: Int, network: GesBoundNetwork, options: GesNetworkOptions) async throws -> sending GesNetworkSocketListener {
        let (resolved, limit) = try resolve(options, .datagram, network)
        let local = try await NIOThreadPool.singleton.runIfActive { try SocketAddress.makeAddressResolvingHost(host, port: port) }
        let queue = GesNetworkSocketQueue()
        let socket = GesNetworkSocketDatagramSocket(limit: limit)
        socket.handler = try GesNetworkSocketDatagramRouter(queue: queue, socket: socket)
        let channel = try await socket.open(local: local).get()
        return GesNetworkSocketListener(
            channel: channel,
            queue: queue,
            network: network,
            options: resolved,
            framing: .datagram,
            datagram: (current: { socket.current }, close: { socket.close() })
        )
    }
}

extension GesNetworkSocketSession {
    /// Opens a UDP socket for a client connection of the datagram transport. The socket is bound to a system-chosen
    /// port of the server address's family and not connected: it sends to the server's address and ignores datagrams
    /// from any other address. Errors of single datagrams, such as an unreachable port, do not end the transport.
    /// - Parameters:
    ///   - host: The server's host name or address; the first address it resolves to is used.
    ///   - port: The server's port.
    ///   - connection: A client connection created with `.datagram`; its timeout runs from its creation.
    /// - Returns: The session. UDP has no connection setup, so a missing server shows only as the connection's
    ///   timeout.
    /// - Throws: `GesApiError.invalidArgument` when the connection is no client of the datagram transport or uses insecure test keys; a SwiftNIO
    ///   error when the host cannot be resolved or the socket cannot be opened.
    public static func connectUdp(host: String, port: Int, connection: sending GesNetworkConnection) async throws -> sending GesNetworkSocketSession {
        try require(connection, .datagram, client: true)
        let inbox = GesNetworkSocketInbox()
        let (channel, peer) = try await openUdp(host: host, port: port, limit: connection.maxPacketBytes, inbox: inbox)
        return GesNetworkSocketSession(connection: connection, channel: channel, inbox: inbox, framing: .datagram, peer: peer)
    }

    static func openUdp(host: String, port: Int, limit: Int, inbox: GesNetworkSocketInbox) async throws -> (Channel, GesNetworkDatagramPeer) {
        let server = try await NIOThreadPool.singleton.runIfActive { try SocketAddress.makeAddressResolvingHost(host, port: port) }
        let local = try SocketAddress(ipAddress: server.protocol == .inet6 ? "::" : "0.0.0.0", port: 0)
        let socket = GesNetworkSocketDatagramSocket(limit: limit)
        let peer = try GesNetworkDatagramPeer(server: server, socket: socket, inbox: inbox)
        socket.handler = GesNetworkSocketDatagramHandler(peer: peer)
        socket.ended = { peer.inbox.end() }
        return (try await socket.open(local: local).get(), peer)
    }
}

// One remote address of a UDP socket with its received datagrams. A listener's peer is a client on the listener's
// shared socket and registered with it; a client's peer is the server and owns the client's socket.
final class GesNetworkDatagramPeer: @unchecked Sendable {
    let address: SocketAddress
    let inbox: GesNetworkSocketInbox
    // True for a client on a listener's shared socket, whose end gives the address back to address validation.
    let isShared: Bool
    private let owner: GesNetworkSocketDatagramSocket
    private let key: [UInt8]

    fileprivate init(client: SocketAddress, socket: GesNetworkSocketDatagramSocket, signal: GesNetworkSocketSignal) {
        address = client
        inbox = GesNetworkSocketInbox(signal: signal)
        owner = socket
        isShared = true
        key = []
    }

    fileprivate init(server: SocketAddress, socket: GesNetworkSocketDatagramSocket, inbox: GesNetworkSocketInbox) throws {
        self.inbox = inbox
        var key = [UInt8](repeating: 0, count: 18)
        guard let length = datagramAddressKey(server, into: &key) else { throw GesApiError.invalidArgument("The host has no IP address.") }
        address = server
        owner = socket
        isShared = false
        self.key = Array(key[0..<length])
    }

    // The socket datagrams currently go through; nil while it is being replaced.
    var socket: Channel? { owner.current }

    // A listener's peer gives its address back to address validation; a client's peer closes its socket. Releasing
    // again does nothing.
    func release() {
        if isShared { owner.remove(self) } else { owner.close() }
    }

    // True when a datagram comes from the server; the scratch holds the source's key.
    fileprivate func sent(_ source: SocketAddress, scratch bytes: inout [UInt8]) -> Bool {
        guard let length = datagramAddressKey(source, into: &bytes), length == key.count else { return false }
        return bytes[0..<length].elementsEqual(key)
    }
}

// A UDP socket that outlives SwiftNIO's channels. SwiftNIO closes a datagram channel on send errors it treats as
// fatal, such as an unreachable network or full buffers; the socket then binds a new channel to the same local address,
// so the other side still sees the same address, and keeps its handler and the known client addresses. Only a failed
// bind or close() ends it.
private final class GesNetworkSocketDatagramSocket: @unchecked Sendable {
    // Set once before the first open; the same handler reads every channel of the socket. Ending the socket drops
    // both, which reference the socket.
    var handler: ChannelHandler? {
        get { lock.withLock { storedHandler } }
        set { lock.withLock { storedHandler = newValue } }
    }

    // Runs when the socket ends without close().
    var ended: (() -> Void)? {
        get { lock.withLock { storedEnded } }
        set { lock.withLock { storedEnded = newValue } }
    }

    private var storedHandler: ChannelHandler?
    private var storedEnded: (() -> Void)?
    private let limit: Int
    private let lock = NIOLock()
    private var channel: Channel?
    private var local: SocketAddress?
    private var closed = false
    private var peers: [SocketAddress: GesNetworkDatagramPeer] = [:]

    init(limit: Int) { self.limit = limit }

    var current: Channel? { lock.withLock { channel } }

    func find(_ address: SocketAddress) -> GesNetworkDatagramPeer? { lock.withLock { peers[address] } }

    func add(_ peer: GesNetworkDatagramPeer) { lock.withLock { peers[peer.address] = peer } }

    func remove(_ peer: GesNetworkDatagramPeer) {
        lock.withLock {
            if peers[peer.address] === peer { peers[peer.address] = nil }
        }
    }

    // Binds a channel to the local address and makes it current.
    func open(local: SocketAddress) -> EventLoopFuture<Channel> {
        DatagramBootstrap(group: MultiThreadedEventLoopGroup.singleton)
            // One byte beyond the packet limit, so a longer datagram arrives truncated to limit + 1 bytes and its
            // connection closes with network.packetTooLarge.
            .channelOption(.recvAllocator, value: FixedSizeRecvByteBufferAllocator(capacity: limit + 1))
            .channelInitializer { channel in
                channel.eventLoop.makeCompletedFuture {
                    guard let handler = self.handler else { throw ChannelError.ioOnClosedChannel }
                    try channel.pipeline.syncOperations.addHandler(handler)
                    try channel.pipeline.syncOperations.addHandler(GesNetworkSocketDatagramWatch(socket: self))
                }
            }
            .bind(to: local)
            .map { channel in
                let close = self.lock.withLock {
                    if self.closed { return true }
                    self.channel = channel
                    self.local = channel.localAddress
                    return false
                }
                if close { channel.close(promise: nil) }
                return channel
            }
    }

    func close() {
        let current = lock.withLock {
            end()
            return self.channel
        }
        current?.close(promise: nil)
    }

    // A current channel closed: bind again unless the socket is closed; a failed bind ends the socket.
    fileprivate func lost(_ channel: Channel) {
        let local: SocketAddress? = lock.withLock {
            guard !closed, self.channel === channel else { return nil }
            self.channel = nil
            return self.local
        }
        guard let local else { return }
        open(local: local).whenFailure { _ in
            let ended = self.lock.withLock {
                let ended = self.storedEnded
                self.end()
                return ended
            }
            ended?()
        }
    }

    // Called under the lock.
    private func end() {
        closed = true
        peers.removeAll()
        storedHandler = nil
        storedEnded = nil
    }
}

// Reports a closed channel to its socket; errors reported for single datagrams, such as an unreachable port, do not
// end it.
private final class GesNetworkSocketDatagramWatch: ChannelInboundHandler {
    typealias InboundIn = Never

    private let socket: GesNetworkSocketDatagramSocket

    init(socket: GesNetworkSocketDatagramSocket) { self.socket = socket }

    func channelInactive(context: ChannelHandlerContext) {
        socket.lost(context.channel)
        context.fireChannelInactive()
    }

    func errorCaught(context: ChannelHandlerContext, error: Error) {}
}

// Reads every datagram of a UDP listener's shared socket and routes it by its source address: a known address to its
// session, an unknown one through address validation.
private final class GesNetworkSocketDatagramRouter: ChannelInboundHandler {
    typealias InboundIn = AddressedEnvelope<ByteBuffer>
    typealias OutboundOut = AddressedEnvelope<ByteBuffer>

    // A cookie stays valid for one to two windows.
    private static let cookieWindowMicroseconds: Int64 = 10_000_000

    private let queue: GesNetworkSocketQueue
    private let socket: GesNetworkSocketDatagramSocket
    private let cookies: GesNetworkCookies
    private let start = NIODeadline.now()
    private var retry = [UInt8](repeating: 0, count: GesNetworkCookies.retryBytes)
    private var key = [UInt8](repeating: 0, count: 18)

    init(queue: GesNetworkSocketQueue, socket: GesNetworkSocketDatagramSocket) throws {
        self.queue = queue
        self.socket = socket
        var random = SystemRandomNumberGenerator()
        let secret = (0..<32).map { _ in UInt8.random(in: .min ... .max, using: &random) }
        cookies = try GesNetworkCookies(key: secret, windowMicroseconds: Self.cookieWindowMicroseconds)
    }

    func channelRead(context: ChannelHandlerContext, data: NIOAny) {
        let envelope = Self.unwrapInboundIn(data)
        if let peer = socket.find(envelope.remoteAddress) {
            peer.inbox.pushDatagram(envelope.data)
            return
        }

        guard let length = datagramAddressKey(envelope.remoteAddress, into: &key) else { return }
        let now = (NIODeadline.now() - start).nanoseconds / 1000
        let result = envelope.data.withUnsafeReadableBytes { packet in
            key.withUnsafeBytes { key in
                retry.withUnsafeMutableBytes { retry in
                    (try? cookies.check(packet: packet, address: UnsafeRawBufferPointer(rebasing: key[0..<length]), now: now, retry: retry)) ?? .drop
                }
            }
        }

        switch result {
        case .drop:
            break
        case .retry:
            let answer = AddressedEnvelope(remoteAddress: envelope.remoteAddress, data: context.channel.allocator.buffer(bytes: retry))
            // A lost retry is answered again when the client sends hello again.
            context.writeAndFlush(Self.wrapOutboundOut(answer), promise: nil)
        case .accept:
            admit(context, envelope)
        }
    }

    func errorCaught(context: ChannelHandlerContext, error: Error) {}

    // Registers a validated address with its hello and offers it to the host; a full queue drops the hello, and the
    // client sends it again.
    private func admit(_ context: ChannelHandlerContext, _ hello: AddressedEnvelope<ByteBuffer>) {
        let peer = GesNetworkDatagramPeer(client: hello.remoteAddress, socket: socket, signal: queue.signal)
        peer.inbox.pushDatagram(hello.data)
        socket.add(peer)
        if !queue.offer(context.channel, peer) { peer.release() }
    }
}

// Moves the server's datagrams of a client's UDP socket into its inbox, one per packet; full inboxes drop them, and
// datagrams of other addresses are ignored. UDP has no end, and the connection's timeout covers a lost server.
private final class GesNetworkSocketDatagramHandler: ChannelInboundHandler {
    typealias InboundIn = AddressedEnvelope<ByteBuffer>

    private let peer: GesNetworkDatagramPeer
    private var key = [UInt8](repeating: 0, count: 18)

    init(peer: GesNetworkDatagramPeer) { self.peer = peer }

    func channelRead(context: ChannelHandlerContext, data: NIOAny) {
        let envelope = Self.unwrapInboundIn(data)
        if peer.sent(envelope.remoteAddress, scratch: &key) { peer.inbox.pushDatagram(envelope.data) }
    }

    func errorCaught(context: ChannelHandlerContext, error: Error) {}
}

// The address for validation and for comparing sources: the 4 or 16 bytes of the IP address and the port, most
// significant byte first; nil for an address without IP.
private func datagramAddressKey(_ address: SocketAddress, into key: inout [UInt8]) -> Int? {
    let length: Int
    switch address {
    case .v4(let ip): length = withUnsafeBytes(of: ip.address.sin_addr) { copyAddressBytes($0, into: &key) }
    case .v6(let ip): length = withUnsafeBytes(of: ip.address.sin6_addr) { copyAddressBytes($0, into: &key) }
    default: return nil
    }
    let port = UInt16(truncatingIfNeeded: address.port ?? 0)
    key[length] = UInt8(truncatingIfNeeded: port >> 8)
    key[length + 1] = UInt8(truncatingIfNeeded: port)
    return length + 2
}

private func copyAddressBytes(_ bytes: UnsafeRawBufferPointer, into key: inout [UInt8]) -> Int {
    for index in 0..<bytes.count { key[index] = bytes[index] }
    return bytes.count
}
