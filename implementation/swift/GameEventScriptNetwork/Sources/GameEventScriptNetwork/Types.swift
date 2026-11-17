// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// The stable `network.*` codes a connection closes with.
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
}

/// The result of queueing a message.
public enum GesNetworkSendStatus: Sendable, Equatable {
    /// The message is queued; on a latest route it replaces an older message not yet started.
    case queued
    /// The connection is not connected.
    case notConnected
    /// The send queue would exceed its byte limit; the message is not queued.
    case queueFull
    /// The message exceeds the largest encoding of the route's root.
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
}

/// One event of a connection.
public struct GesNetworkEvent {
    /// The kind of the event.
    public let kind: GesNetworkEventKind
    /// The route of a message; -1 for other events.
    public let route: Int
    /// The message bytes or the client's token. The bytes stay valid until the next call of
    /// `GesNetworkConnection.nextEvent()` or the release of the connection.
    public let payload: UnsafeRawBufferPointer
    /// The `network.*` code of a close; empty otherwise.
    public let code: String
    /// The detail of a close, at most 512 UTF-8 bytes; empty otherwise.
    public let detail: String
    /// Whether the peer closed the connection.
    public let byPeer: Bool
}

/// The limits and timings of a connection. Times are microseconds.
public struct GesNetworkOptions: Sendable, Equatable {
    /// How the transport delimits packets.
    public var transport: GesNetworkTransport = .message
    /// The largest packet this side sends or receives, from 1200 to 65535 bytes.
    public var maxPacketBytes = 16384
    /// The largest number of received bytes not yet taken as events.
    public var maxPendingReceiveBytes: Int64 = 4_194_304
    /// The most frames the peer may send per second.
    public var maxFramesPerSecond = 4096
    /// The largest number of reliable and bulk bytes waiting to be sent.
    public var maxSendQueueBytes: Int64 = 1_048_576
    /// How long the connection waits for anything from the peer.
    public var timeoutMicroseconds: Int64 = 10_000_000
    /// How long a connected side stays silent before it sends an empty data packet.
    public var keepaliveMicroseconds: Int64 = 1_000_000
    /// How long an acknowledgement may wait for frames to travel with.
    public var ackDelayMicroseconds: Int64 = 10_000
    /// The bytes per second this side sends; 0 sends without a budget.
    public var sendBudgetBytesPerSecond: Int64 = 0
    /// The baseline limit this side announces, 0 to 1073741824: the full record bytes a topic's ring of recent states
    /// holds. The smaller limit of both sides applies.
    public var maxBaselineBytes: Int64 = 65536

    /// Creates the default options.
    public init() {}

    func validate() throws {
        if maxPacketBytes < GesNetworkWire.minimumPacketBytes || maxPacketBytes > GesNetworkWire.maximumPacketBytes {
            throw GesApiError.invalidArgument("The packet limit is 1200 to 65535 bytes.")
        }
        if maxPendingReceiveBytes < 1 { throw GesApiError.invalidArgument("The pending receive limit is at least 1 byte.") }
        if maxFramesPerSecond < 1 { throw GesApiError.invalidArgument("The frame rate is at least 1 frame per second.") }
        if maxSendQueueBytes < 1 { throw GesApiError.invalidArgument("The send queue limit is at least 1 byte.") }
        if timeoutMicroseconds < 1 { throw GesApiError.invalidArgument("The timeout is at least 1 microsecond.") }
        if keepaliveMicroseconds < 1 { throw GesApiError.invalidArgument("The keepalive is at least 1 microsecond.") }
        if ackDelayMicroseconds < 0 { throw GesApiError.invalidArgument("The acknowledgement delay is not negative.") }
        if sendBudgetBytesPerSecond < 0 { throw GesApiError.invalidArgument("The send budget is not negative.") }
        if maxBaselineBytes < 0 || maxBaselineBytes > Int64(GesNetworkWire.maximumBaselineBytes) {
            throw GesApiError.invalidArgument("The baseline limit is 0 to 1073741824 bytes.")
        }
    }
}
