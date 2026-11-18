// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// The stable `network.*` codes a connection closes or cancels a transfer with.
public enum GesNetworkCodes {
    /// The peer speaks another protocol or another protocol version.
    public static let protocolMismatch = "network.protocolMismatch"
    /// The networks of both sides differ; the detail names the differing roots.
    public static let schemaMismatch = "network.schemaMismatch"
    /// A received packet or frame breaks the wire format.
    public static let malformed = "network.malformed"
    /// A received packet exceeds the packet limit.
    public static let packetTooLarge = "network.packetTooLarge"
    /// A received message exceeds the largest encoding of its route's root.
    public static let messageTooLarge = "network.messageTooLarge"
    /// Received messages not yet taken exceed the pending limit.
    public static let queueFull = "network.queueFull"
    /// The peer sends more frames per second than allowed.
    public static let rateExceeded = "network.rateExceeded"
    /// Nothing arrived from the peer within the timeout.
    public static let timeout = "network.timeout"
    /// The server refused the client.
    public static let refused = "network.refused"
    /// The peer sent on a route only this side may send on.
    public static let unexpectedRoute = "network.unexpectedRoute"
    /// One side closed the connection in order.
    public static let closed = "network.closed"
    /// The transport ended without a close packet.
    public static let transportClosed = "network.transportClosed"
    /// A transfer announces more bytes than the receiver's transfer limit.
    public static let transferTooLarge = "network.transferTooLarge"
    /// A transfer's header, compressed data or hash is invalid.
    public static let transferCorrupt = "network.transferCorrupt"
    /// The host cancelled a transfer without giving its own code.
    public static let transferCancelled = "network.transferCancelled"
    /// A server without a key received a key request or a sealed `hello`.
    public static let insecure = "network.insecure"
    /// A client that requires `GesNetworkSecurity.verified` has no pinned key and received a server key.
    public static let unverified = "network.unverified"
    /// A sealed `hello`, `welcome` or packet does not authenticate.
    public static let authenticationFailed = "network.authenticationFailed"
}

/// The result of queueing a message.
public enum GesNetworkSendStatus: Sendable, Equatable {
    /// The message is queued; on a latest route it replaces an older message not yet started.
    case queued
    /// The connection is not connected.
    case notConnected
    /// The reliable queue or the bulk queue would exceed its limit; the message is not queued.
    case queueFull
    /// The message exceeds the largest encoding of the route's root, or, on a bulk route, this side's or the peer's
    /// transfer limit.
    case messageTooLarge
    /// The route does not exist, or this side does not send it with this verb.
    case invalidRoute
    /// The server's host has not subscribed the client to the topic.
    case notSubscribed
}

/// The state of a connection.
public enum GesNetworkState: Sendable, Equatable {
    /// A client that has not received the welcome yet.
    case hello
    /// A server waiting for the client's hello.
    case awaitHello
    /// A server waiting for the host to accept or refuse the client.
    case awaitAccept
    /// Both sides exchange messages.
    case connected
    /// The connection ended; it sends at most its close packet.
    case closed
}

/// How the transport delimits packets.
public enum GesNetworkTransport: Sendable, Equatable {
    /// The transport keeps packet boundaries, as a WebSocket or an in-process channel.
    case message
    /// The transport is a byte stream, as TCP; every packet carries a varint length prefix.
    case stream
    /// One datagram per packet, for example UDP. Packets may be lost, duplicated, delayed or reordered; the connection
    /// resends what is lost and restores the order of each class.
    case datagram
}

/// The security level of a session.
public enum GesNetworkSecurity: Sendable, Equatable {
    /// The session is not encrypted.
    case none
    /// The session is encrypted with a server key the client received during the handshake; it protects only against
    /// attackers who read but do not change packets. A server reports this level for every encrypted session.
    case encrypted
    /// The session is encrypted with the server key the client was given in advance.
    case verified
}

/// The kind of a connection event.
public enum GesNetworkEventKind: Sendable, Equatable {
    /// No event is waiting.
    case none
    /// A client's hello arrived; the payload is its token. The host accepts or refuses it.
    case connecting
    /// The connection is connected.
    case connected
    /// A message arrived on a route.
    case message
    /// The connection closed with a code and a detail.
    case closed
    /// A transfer on a bulk route advanced: the receiver got more of its bytes, or the peer confirmed more of the bytes
    /// sent.
    case transferProgress
    /// A transfer was cancelled by this side, by its host or by the peer; the session continues.
    case transferCancelled
}

/// One event of a connection.
public struct GesNetworkEvent {
    /// The kind of the event.
    public let kind: GesNetworkEventKind
    /// The route of a message or transfer event; -1 for other events.
    public let route: Int
    /// The message bytes or the client's token. The bytes stay valid until the next call of
    /// `GesNetworkConnection.nextEvent()` or the release of the connection.
    public let payload: UnsafeRawBufferPointer
    /// The `network.*` code of a close or the code of a cancelled transfer; empty otherwise.
    public let code: String
    /// The detail of a close, at most 512 UTF-8 bytes; empty otherwise.
    public let detail: String
    /// Whether the peer closed the connection or cancelled the transfer.
    public let byPeer: Bool
    /// The transfer id, 0 to 65535, of a transfer event or of a message on a bulk route; -1 for other events.
    public let transferId: Int
    /// The bytes of a progress event: received ones on the receiver, confirmed ones on the sender. Both count the
    /// transfer's encoded bytes; 0 for other events.
    public let bytes: Int64
    /// The encoded length of a progress event's transfer; 0 for other events.
    public let total: Int64
}

/// The limits and timings of a connection. Times are microseconds.
public struct GesNetworkOptions: Sendable, Equatable {
    /// How the transport delimits packets.
    public var transport: GesNetworkTransport = .message
    /// The largest packet this side sends or receives, from 1200 to 65535 bytes, or 0 for the transport's default:
    /// 1200 on the datagram transport, else 16384.
    public var maxPacketBytes = 0
    /// The largest number of received bytes not yet taken as events, 1 to 1073741824.
    public var maxPendingReceiveBytes: Int64 = 4_194_304
    /// The most frames the peer may send per second, 1 to 1000000.
    public var maxFramesPerSecond = 4096
    /// The largest number of reliable bytes waiting to be sent, 1 to 1073741824.
    public var maxSendQueueBytes: Int64 = 1_048_576
    /// How long the connection waits for anything from the peer, 1 to 3600000000.
    public var timeoutMicroseconds: Int64 = 10_000_000
    /// How long a connected side stays silent before it sends an empty data packet, 1 to 3600000000.
    public var keepaliveMicroseconds: Int64 = 1_000_000
    /// How long an acknowledgement may wait for frames to travel with, 0 to 3600000000.
    public var ackDelayMicroseconds: Int64 = 10_000
    /// The bytes per second this side sends, 0 to 1073741824; 0 sends without a budget.
    public var sendBudgetBytesPerSecond: Int64 = 0
    /// The baseline limit this side announces, 0 to 1073741824: the full record bytes a topic's ring of recent states
    /// holds. The smaller limit of both sides applies.
    public var maxBaselineBytes: Int64 = 65536
    /// The transfer limit this side announces, 0 to 1073741824: the largest uncompressed data of one bulk message it
    /// sends or receives. It also bounds the bulk send queue.
    public var maxTransferBytes: Int64 = 67_108_864
    /// Whether this side accepts and sends deflate-compressed transfers.
    public var deflateTransfers = true
    /// The lowest security level this side accepts. A client without a pinned key closes with
    /// `GesNetworkCodes.unverified` when it requires `GesNetworkSecurity.verified`; a server above
    /// `GesNetworkSecurity.none` needs a `key`. The default is `GesNetworkSecurity.verified`.
    public var minimumSecurity: GesNetworkSecurity = .verified
    /// The server's pinned public key of 32 bytes; client only. A client with a pinned key seals its first `hello` and
    /// reaches `GesNetworkSecurity.verified`. The default is none.
    public var serverPublicKey: [UInt8]?
    /// The server's key; server only. A server with a key encrypts every session. The default is none.
    public var key: GesNetworkKey?
    /// A seed that derives the connection's ephemeral keys instead of the system's random generator. INSECURE: anyone
    /// who knows the seed reads the session. For tests and golden bytes only: the socket transports refuse such
    /// options and connections. The default is none.
    public var insecureTestKeySeed: UInt64?

    /// Creates the default options.
    public init() {}

    // The largest byte limits and send budget, frame rate and time interval in microseconds.
    static let maximumBytes: Int64 = 1 << 30
    static let maximumFramesPerSecond = 1_000_000
    static let maximumInterval: Int64 = 3_600_000_000

    // The packet limit in effect: the configured one, or the transport's default.
    var packetLimit: Int { maxPacketBytes != 0 ? maxPacketBytes : transport == .datagram ? GesNetworkWire.minimumPacketBytes : 16384 }

    func validate() throws {
        if maxPacketBytes != 0 && (maxPacketBytes < GesNetworkWire.minimumPacketBytes || maxPacketBytes > GesNetworkWire.maximumPacketBytes) {
            throw GesApiError.invalidArgument("The packet limit is 0 or 1200 to 65535 bytes.")
        }
        if !(1...Self.maximumBytes).contains(maxPendingReceiveBytes) { throw GesApiError.invalidArgument("The pending receive limit is 1 to 1073741824 bytes.") }
        if !(1...Self.maximumFramesPerSecond).contains(maxFramesPerSecond) { throw GesApiError.invalidArgument("The frame rate is 1 to 1000000 frames per second.") }
        if !(1...Self.maximumBytes).contains(maxSendQueueBytes) { throw GesApiError.invalidArgument("The send queue limit is 1 to 1073741824 bytes.") }
        if !(1...Self.maximumInterval).contains(timeoutMicroseconds) { throw GesApiError.invalidArgument("The timeout is 1 to 3600000000 microseconds.") }
        if !(1...Self.maximumInterval).contains(keepaliveMicroseconds) { throw GesApiError.invalidArgument("The keepalive is 1 to 3600000000 microseconds.") }
        if !(0...Self.maximumInterval).contains(ackDelayMicroseconds) { throw GesApiError.invalidArgument("The acknowledgement delay is 0 to 3600000000 microseconds.") }
        if !(0...Self.maximumBytes).contains(sendBudgetBytesPerSecond) { throw GesApiError.invalidArgument("The send budget is 0 to 1073741824 bytes per second.") }
        if maxBaselineBytes < 0 || maxBaselineBytes > Int64(GesNetworkWire.maximumBaselineBytes) {
            throw GesApiError.invalidArgument("The baseline limit is 0 to 1073741824 bytes.")
        }
        if maxTransferBytes < 0 || maxTransferBytes > Int64(GesNetworkWire.maximumTransferBytes) {
            throw GesApiError.invalidArgument("The transfer limit is 0 to 1073741824 bytes.")
        }
        if let serverPublicKey, serverPublicKey.count != GesNetworkX25519.keyBytes {
            throw GesApiError.invalidArgument("A server public key holds 32 bytes.")
        }
    }
}
