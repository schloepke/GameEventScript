// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// One connection's transport as an end drives it: it passes received packets to the connection and hands the
/// connection's packets to the transport. Implementations come from a connector or an acceptor.
public protocol GesNetworkLink: AnyObject {
    /// The connection this link carries.
    var connection: GesNetworkConnection { get }

    /// Passes every received packet to the connection, advances it to `now` and reports the end of the transport with
    /// `GesNetworkConnection.transportClosed()`.
    /// - Parameter now: The current time in microseconds.
    /// - Throws: `GesApiError.invalidArgument` when the time goes back behind the connection's.
    func receive(now: Int64) throws

    /// Hands every packet the connection has due to the transport.
    /// - Throws: An error of the connection's `transmit(into:)`.
    func transmit() throws

    /// Releases the transport once the packets already handed to it have left. Does nothing when called again.
    func close()
}

/// Opens the transport of a client end.
public protocol GesNetworkConnector: AnyObject {
    /// The transport the connector opens; the client end's options must use it.
    var transport: GesNetworkTransport { get }

    /// Starts opening the transport of a client connection without blocking. The link may still be connecting: it
    /// takes packets once the transport is open, and a failure to open ends the transport.
    /// - Parameter connection: The client connection.
    /// - Returns: The link of the connection.
    /// - Throws: An error when the connector cannot start opening the transport at all.
    func connect(_ connection: GesNetworkConnection) throws -> any GesNetworkLink
}

/// Accepts the transports of a server end's clients.
public protocol GesNetworkAcceptor: AnyObject {
    /// The link type of the accepted clients; a server end sees it as `any GesNetworkLink`.
    associatedtype Accepted: GesNetworkLink

    /// Takes the next accepted client without blocking.
    /// - Parameter now: The current time in microseconds, which the new server connection starts at.
    /// - Returns: The link of a new server connection, or `nil` when no client waits.
    /// - Throws: An error when the acceptor cannot create the server connection.
    func accept(now: Int64) throws -> Accepted?

    /// Stops accepting and releases the clients that wait. Does nothing when called again.
    func close()
}

/// The kind of an end event.
public enum GesNetworkEndEventKind: Sendable, Equatable {
    /// A client asks to connect; the payload is its token. Server only: the end accepts it after the handler returns
    /// unless the handler refused it with `GesNetworkServerEnd.refuse(client:detail:)`.
    case connecting
    /// The client's session is established, with the reached security. Client only.
    case connected
    /// The client's connection closed, with its code, detail and whether the peer closed it. Client only.
    case disconnected
    /// A client was accepted and is connected, with the reached security. Server only.
    case clientConnected
    /// A client's connection closed, with its code, detail and whether the peer closed it; the client leaves the end.
    /// Server only.
    case clientDisconnected
    /// A complete message arrived on a route.
    case message
    /// A tick is complete: the message events of its changed members precede it.
    case tick
    /// A transfer on a bulk route advanced.
    case transferProgress
    /// A transfer was cancelled; the session continues.
    case transferCancelled
    /// The last event of a poll that reported at least one message or tick.
    case received
}

/// One end event. The payload is borrowed and valid only until the handler returns.
public struct GesNetworkEndEvent {
    /// The event kind.
    public let kind: GesNetworkEndEventKind
    /// The end-local id of the client on a server end; -1 on a client end and for `GesNetworkEndEventKind.received`.
    public let client: Int
    /// The route index of a message or transfer event; -1 for other events.
    public let route: Int
    /// The message payload or the client's token; empty for other events. The bytes stay valid until the handler
    /// returns; copy them to keep them.
    public let payload: UnsafeRawBufferPointer
    /// The close code of a disconnect or the code of a cancelled transfer; empty for other events.
    public let code: String
    /// The close detail of a disconnect; empty for other events.
    public let detail: String
    /// Whether the peer closed the connection of a disconnect or cancelled the transfer of a cancelled event.
    public let byPeer: Bool
    /// The reached security of a connect event; `GesNetworkSecurity.none` for other events.
    public let security: GesNetworkSecurity
    /// The number, 0 to 65535, of a tick event; -1 for other events.
    public let tickNumber: Int
    /// The transfer id of a transfer event or of a message on a bulk route; -1 for other events.
    public let transferId: Int
    /// The bytes of a progress event; 0 for other events.
    public let bytes: Int64
    /// The encoded length of a progress event's transfer; 0 for other events.
    public let total: Int64

    init(_ kind: GesNetworkEndEventKind, client: Int, source: GesNetworkEvent, security: GesNetworkSecurity) {
        self.kind = kind
        self.client = client
        route = source.route
        payload = source.payload
        code = source.code
        detail = source.detail
        byPeer = source.byPeer
        self.security = security
        tickNumber = source.tickNumber
        transferId = source.transferId
        bytes = source.bytes
        total = source.total
    }

    init(_ kind: GesNetworkEndEventKind, client: Int, security: GesNetworkSecurity) {
        self.kind = kind
        self.client = client
        route = -1
        payload = UnsafeRawBufferPointer(start: nil, count: 0)
        code = ""
        detail = ""
        byPeer = false
        self.security = security
        tickNumber = -1
        transferId = -1
        bytes = 0
        total = 0
    }

    // Translates a connection event kind into an end event kind.
    static func kind(_ kind: GesNetworkEventKind, client: Bool) -> GesNetworkEndEventKind {
        switch kind {
        case .connecting: .connecting
        case .connected: client ? .connected : .clientConnected
        case .closed: client ? .disconnected : .clientDisconnected
        case .message: .message
        case .tick: .tick
        case .transferProgress: .transferProgress
        case .none, .transferCancelled: .transferCancelled
        }
    }
}

// What the ends ask a connection without sending: the checks of send and publish apart from the state, and the kept
// states of latest topics.
extension GesNetworkConnection {
    var maxSendQueueBytes: Int64 { maxSendQueue }

    // The status send or publish would give for a payload of this length on a connected connection, apart from the
    // queue and, on a server, the subscription.
    func check(_ route: Int, _ length: Int, publish: Bool) -> GesNetworkSendStatus {
        if route < 0 || route >= routeCount || !localSends[route] || (topics[route] == nil) == publish { return .invalidRoute }
        if classes[route] == .bulk { return Int64(length) > transferLimit(route) ? .messageTooLarge : .queued }
        if publish { return Int64(length) >= maxBytes[route] ? .messageTooLarge : .queued }
        return Int64(length) > maxBytes[route] ? .messageTooLarge : .queued
    }

    // Whether the route is a latest topic this side publishes: one a tick can name.
    func isLocalLatestTopic(_ route: Int) -> Bool {
        route >= 0 && route < routeCount && localSends[route] && topics[route] != nil && sendLatest[route] != nil
    }

    // Whether the route queues reliably: a reliable send or a reliable topic.
    func isReliable(_ route: Int) -> Bool { classes[route] == .reliable }

    func isLatest(_ route: Int) -> Bool { classes[route] == .latest }

    // The bytes an uncompressed transfer of this length takes in the send queue: its header and its data.
    static func queuedTransferBytes(_ length: Int) -> Int64 { Int64(1 + GesNetworkWire.varintSize64(UInt64(length)) + GesNetworkWire.hashBytes + length) }

    /// Whether the last published state of a latest topic is the record.
    func lastIs(_ route: Int, _ record: [UInt8], _ length: Int) -> Bool {
        hasLast[route] && lastLengths[route] == length && lastRecords[route][0..<length] == record[0..<length]
    }

    func isSubscribed(_ route: Int) -> Bool { topics[route]?.subscribed == true }
}

// Keeps a copy of a payload in a reusable buffer, which grows at least to double its size.
func gesNetworkKeep(_ buffer: inout [UInt8], _ payload: UnsafeRawBufferPointer) {
    if buffer.count < payload.count { buffer = [UInt8](repeating: 0, count: max(payload.count, 16, buffer.count * 2)) }
    if payload.count > 0 { buffer.withUnsafeMutableBytes { $0.copyMemory(from: payload) } }
}

// Passes the first bytes of a buffer as a borrowed payload.
func gesNetworkWith<Result>(_ buffer: [UInt8], _ length: Int, _ body: (UnsafeRawBufferPointer) throws -> Result) rethrows -> Result {
    try buffer.withUnsafeBytes { try body(UnsafeRawBufferPointer(rebasing: $0[0..<length])) }
}
