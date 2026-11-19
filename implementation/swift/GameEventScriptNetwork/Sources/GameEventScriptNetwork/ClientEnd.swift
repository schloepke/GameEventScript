// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// The client end of a session: it owns one client connection and its transport and drives both from `poll(_:)`.
///
/// Sends, publishes and ticks made before the session is established wait in the end and reach the connection when it
/// connects. One caller at a time; not thread-affine.
public final class GesNetworkClientEnd {
    private struct Queued {
        let route: Int
        let publish: Bool
        let payload: [UInt8]
    }

    /// The connection of the end.
    public let connection: GesNetworkConnection
    /// The connector that opens the transport.
    public let connector: any GesNetworkConnector
    /// The clock the end reads the time from.
    public let clock: any GesClock

    private var link: (any GesNetworkLink)?
    private var released = false
    private(set) var polling = false
    // Whether a send, publish, tick or close outside a poll left packets that only the next poll transmits.
    private var transmitDue = false
    // Set by a synchronous pair, which runs both ends after every send.
    var pair: GesNetworkPair.Core?
    // The connected event that a synchronous pair's handshake took and the next poll reports.
    private var reportConnected = false

    // Sends before the handshake: reliable and bulk ones in order, latest ones as the newest per route.
    private var queue: [Queued] = []
    private var queuedBytes: Int64 = 0
    private var queuedTransferBytes: Int64 = 0
    private var queuedTransfers = 0
    private var latest: [[UInt8]]
    private var latestLengths: [Int]
    private var latestSerials: [Int64]
    private var latestPublish: [Bool]
    private var hasLatest: [Bool]
    private var serial: Int64 = 0

    // The last published state of each latest topic, which a tick before the handshake includes.
    private var kept: [[UInt8]]
    private var keptLengths: [Int]
    private var hasKept: [Bool]

    // A tick opened before the handshake, and the newest closed one that waits for it.
    private var tickOpen = false
    private var tickOnConnection = false
    private var stage: [[UInt8]]
    private var stageLengths: [Int]
    private var staged: [Bool]
    private var closed: [[UInt8]]
    private var closedLengths: [Int]
    private var closedMembers: [Bool]
    private var hasClosed = false
    private var closedSerial: Int64 = 0

    /// Creates a client end. Starts no I/O: the first `poll(_:)` opens the transport.
    /// - Parameters:
    ///   - network: The bound network both sides share.
    ///   - connector: Opens the transport.
    ///   - options: The limits, the transport of the connector and the client security.
    ///   - token: The token the server's host receives with `connecting`, at most 512 bytes.
    ///   - clock: The clock the end reads on every poll; the connection starts at its current time.
    /// - Throws: `GesApiError.invalidArgument` when the options use another transport than the connector, or are
    ///   invalid, or the token is too long.
    public init(network: GesBoundNetwork, connector: any GesNetworkConnector, options: GesNetworkOptions, token: [UInt8] = [], clock: any GesClock) throws {
        if options.transport != connector.transport { throw GesApiError.invalidArgument("The options must use the transport of the connector.") }
        self.connector = connector
        self.clock = clock
        connection = try GesNetworkConnection.makeClient(network: network, token: token, now: clock.elapsedMicroseconds, options: options)
        let routes = connection.routeCount
        latest = [[UInt8]](repeating: [], count: routes)
        latestLengths = [Int](repeating: 0, count: routes)
        latestSerials = [Int64](repeating: 0, count: routes)
        latestPublish = [Bool](repeating: false, count: routes)
        hasLatest = [Bool](repeating: false, count: routes)
        kept = [[UInt8]](repeating: [], count: routes)
        keptLengths = [Int](repeating: 0, count: routes)
        hasKept = [Bool](repeating: false, count: routes)
        stage = [[UInt8]](repeating: [], count: routes)
        stageLengths = [Int](repeating: 0, count: routes)
        staged = [Bool](repeating: false, count: routes)
        closed = [[UInt8]](repeating: [], count: routes)
        closedLengths = [Int](repeating: 0, count: routes)
        closedMembers = [Bool](repeating: false, count: routes)
    }

    /// The bound network of the end's connection.
    public var network: GesBoundNetwork { connection.network }

    /// The earliest time at which a poll has work without new input: now before the first poll and while a send,
    /// publish, tick or close outside a poll left packets for the next poll, else the connection's next deadline.
    public var nextDeadline: Int64 { (link == nil || transmitDue) && !released ? clock.elapsedMicroseconds : connection.nextDeadline }

    private var waiting: Bool { connection.state == .hello }

    /// Sends a payload on a route the client sends. Before the session is established the send waits in the end:
    /// reliable and bulk sends in order, a latest send as the newest of its route.
    /// - Parameters:
    ///   - route: The route index of a `sends` route of the client.
    ///   - payload: The encoded root value; the bytes are copied.
    /// - Returns: The send status; `queueFull` when the waiting reliable sends would exceed the connection's send
    ///   queue, or the waiting transfers its transfer limit or count.
    public func send(route: Int, payload: UnsafeRawBufferPointer) -> GesNetworkSendStatus {
        if !waiting { return after(connection.send(route: route, payload: payload)) }
        let status = connection.check(route, payload.count, publish: false)
        return status != .queued ? status : enqueue(route, payload, publish: false)
    }

    /// Sends a payload on a route the client sends, as `send(route:payload:)` with borrowed bytes.
    /// - Parameters:
    ///   - route: The route index of a `sends` route of the client.
    ///   - payload: The encoded root value; the bytes are copied.
    /// - Returns: The send status.
    public func send(route: Int, payload: [UInt8]) -> GesNetworkSendStatus {
        payload.withUnsafeBytes { send(route: route, payload: $0) }
    }

    /// Publishes a state on a topic the client publishes. Before the session is established the state waits in the
    /// end: a reliable topic's in order, a latest topic's as the newest of its route or as a member of the open tick.
    /// - Parameters:
    ///   - route: The route index of a `publishes` route of the client.
    ///   - payload: The full record of the topic's schema; the bytes are copied.
    /// - Returns: The send status.
    public func publish(route: Int, payload: UnsafeRawBufferPointer) -> GesNetworkSendStatus {
        if !waiting || tickOnConnection { return after(connection.publish(route: route, payload: payload)) }
        let status = connection.check(route, payload.count, publish: true)
        if status != .queued { return status }
        if !connection.isLatest(route) { return enqueue(route, payload, publish: true) }
        gesNetworkKeep(&kept[route], payload)
        keptLengths[route] = payload.count
        hasKept[route] = true
        if !tickOpen { return enqueue(route, payload, publish: true) }
        gesNetworkKeep(&stage[route], payload)
        stageLengths[route] = payload.count
        staged[route] = true
        return .queued
    }

    /// Publishes a state on a topic the client publishes, as `publish(route:payload:)` with borrowed bytes.
    /// - Parameters:
    ///   - route: The route index of a `publishes` route of the client.
    ///   - payload: The full record of the topic's schema; the bytes are copied.
    /// - Returns: The send status.
    public func publish(route: Int, payload: [UInt8]) -> GesNetworkSendStatus {
        payload.withUnsafeBytes { publish(route: route, payload: $0) }
    }

    /// Opens a tick, as `GesNetworkConnection.beginTick()`; before the handshake it waits in the end.
    /// - Throws: `GesApiError.invalidOperation` when a tick is open already.
    public func beginTick() throws {
        if tickOpen { throw GesApiError.invalidOperation("A tick is open already.") }
        if !waiting {
            try connection.beginTick()
            tickOnConnection = true
        }
        tickOpen = true
    }

    /// Makes a latest topic a member of the open tick with its last published state.
    /// - Parameter route: The route index of a latest topic the client publishes.
    /// - Throws: `GesApiError.invalidOperation` when no tick is open or the topic has no published state;
    ///   `GesApiError.invalidArgument` when the route is no latest topic the client publishes.
    public func include(route: Int) throws {
        if tickOnConnection {
            try connection.include(route: route)
            return
        }
        if !tickOpen { throw GesApiError.invalidOperation("No tick is open.") }
        if !connection.isLocalLatestTopic(route) { throw GesApiError.invalidArgument("The route is no latest topic this side publishes.") }
        if !hasKept[route] { throw GesApiError.invalidOperation("The topic has no published state.") }
        if staged[route] { return }
        kept[route].withUnsafeBytes { gesNetworkKeep(&stage[route], UnsafeRawBufferPointer(rebasing: $0[0..<keptLengths[route]])) }
        stageLengths[route] = keptLengths[route]
        staged[route] = true
    }

    /// Closes the open tick. Before the handshake it replaces the tick that waits.
    /// - Returns: `notConnected` once the connection closed; otherwise `queued`.
    /// - Throws: `GesApiError.invalidOperation` when no tick is open.
    @discardableResult
    public func endTick() throws -> GesNetworkSendStatus {
        if !tickOpen { throw GesApiError.invalidOperation("No tick is open.") }
        tickOpen = false
        if tickOnConnection {
            tickOnConnection = false
            return after(try connection.endTick())
        }
        if !waiting {
            // The connection closed while the tick waited.
            Self.clear(&staged)
            return .notConnected
        }
        if !staged.contains(true) { return .queued }
        if hasClosed { releaseClosed() }
        swap(&closed, &stage)
        swap(&closedLengths, &stageLengths)
        swap(&closedMembers, &staged)
        Self.clear(&staged)
        hasClosed = true
        serial += 1
        closedSerial = serial
        return .queued
    }

    /// Closes the connection with a detail and drops the sends that wait for the handshake. Does nothing once closed.
    /// - Parameter detail: The detail sent to the server.
    public func close(_ detail: String) {
        dropQueue()
        connection.close(detail)
        _ = after(.queued)
    }

    /// Opens the transport on the first call, passes received packets to the connection, hands every event to the
    /// handler and transmits every due packet. After the handler closes the connection, its remaining events up to the
    /// disconnect are skipped. Never blocks and never calls the handler outside this call.
    /// - Parameter handler: The handler of the events. It may send, publish, close and open or close ticks on the end;
    ///   an event's payload is valid only until it returns.
    /// - Throws: `GesApiError.invalidOperation` when the end is released or the handler polls the end again; an error
    ///   of the connector, the link or the handler.
    public func poll(_ handler: (GesNetworkEndEvent) throws -> Void) throws {
        try poll(handler, report: true)
    }

    func poll(_ handler: (GesNetworkEndEvent) throws -> Void, report: Bool) throws {
        if released { throw GesApiError.invalidOperation("The end is released.") }
        if polling { throw GesApiError.invalidOperation("The end is polled already.") }
        polling = true
        transmitDue = true
        do {
            defer { polling = false }
            let now = max(clock.elapsedMicroseconds, connection.now)
            let link: any GesNetworkLink
            if let existing = self.link {
                link = existing
            } else {
                link = try connector.connect(connection)
                self.link = link
            }
            try link.receive(now: now)
            var received = false
            if reportConnected && report {
                reportConnected = false
                try handler(GesNetworkEndEvent(.connected, client: -1, security: connection.security))
            }
            // After the handler closed the connection, its remaining events up to the disconnect are skipped.
            var skipping = false
            while true {
                let next = connection.nextEvent()
                if next.kind == .none { break }
                if skipping && next.kind != .closed { continue }
                if next.kind == .connected {
                    try passQueue()
                } else if next.kind == .closed {
                    dropQueue()
                }
                if !report {
                    if next.kind == .connected { reportConnected = true }
                    continue
                }
                let kind = GesNetworkEndEvent.kind(next.kind, client: true)
                if kind == .message || kind == .tick { received = true }
                let open = connection.state != .closed
                try handler(GesNetworkEndEvent(kind, client: -1, source: next, security: kind == .connected ? connection.security : .none))
                skipping = open && connection.state == .closed
            }
            if received { try handler(GesNetworkEndEvent(.received, client: -1, security: .none)) }
            try link.transmit()
            transmitDue = false
        }
        pair?.exchange()
    }

    /// Releases the transport once the packets already handed to it have left. Further polls throw. Does nothing when
    /// called again.
    public func release() {
        if released { return }
        released = true
        dropQueue()
        link?.close()
    }

    // Lets a synchronous pair deliver what a send outside a poll produced; another link transmits it with the next
    // poll, which the next deadline brings forward.
    private func after(_ status: GesNetworkSendStatus) -> GesNetworkSendStatus {
        if polling { return status }
        if let pair { pair.exchange() } else { transmitDue = true }
        return status
    }

    private func enqueue(_ route: Int, _ payload: UnsafeRawBufferPointer, publish: Bool) -> GesNetworkSendStatus {
        if connection.isLatest(route) {
            gesNetworkKeep(&latest[route], payload)
            latestLengths[route] = payload.count
            serial += 1
            latestSerials[route] = serial
            latestPublish[route] = publish
            hasLatest[route] = true
            return .queued
        }
        if connection.isReliable(route) {
            // As the connection counts it: a topic state with its mode byte, so passing the queue never fills it.
            let bytes = Int64(publish ? payload.count + 1 : payload.count)
            if queuedBytes + bytes > connection.maxSendQueueBytes { return .queueFull }
            queuedBytes += bytes
        } else {
            // As the connection bounds its started transfers, against this side's transfer limit; a transfer counts as
            // uncompressed, so the connection takes every queued one.
            if queuedTransfers >= GesNetworkSendQueue.maximumStarted || queuedTransferBytes + Int64(payload.count) > connection.maxTransfer { return .queueFull }
            queuedTransfers += 1
            queuedTransferBytes += GesNetworkConnection.queuedTransferBytes(payload.count)
        }
        queue.append(Queued(route: route, publish: publish, payload: Array(payload)))
        return .queued
    }

    // Passes everything that waited for the handshake to the connection: reliable and bulk sends in order, latest
    // states older than the waiting tick, the tick, and then newer latest states.
    private func passQueue() throws {
        for entry in queue {
            if entry.publish {
                _ = connection.publish(route: entry.route, payload: entry.payload)
            } else {
                _ = connection.send(route: entry.route, payload: entry.payload)
            }
        }
        queue.removeAll()
        queuedBytes = 0
        queuedTransferBytes = 0
        queuedTransfers = 0
        passLatest(older: true)
        if hasClosed {
            try connection.beginTick()
            for route in closedMembers.indices where closedMembers[route] {
                _ = gesNetworkWith(closed[route], closedLengths[route]) { connection.publish(route: route, payload: $0) }
            }
            try connection.endTick()
        }
        passLatest(older: false)
        hasClosed = false
        Self.clear(&closedMembers)
        Self.clear(&hasLatest)
        if !tickOpen { return }
        // An open tick moves to the connection with its members.
        try connection.beginTick()
        tickOnConnection = true
        for route in staged.indices where staged[route] {
            _ = gesNetworkWith(stage[route], stageLengths[route]) { connection.publish(route: route, payload: $0) }
        }
        Self.clear(&staged)
    }

    /// Turns the members of the replaced closed tick into latest states that newer states of their routes replace.
    private func releaseClosed() {
        for route in closedMembers.indices where closedMembers[route] && !(hasLatest[route] && latestSerials[route] > closedSerial) {
            let length = closedLengths[route]
            closed[route].withUnsafeBytes { gesNetworkKeep(&latest[route], UnsafeRawBufferPointer(rebasing: $0[0..<length])) }
            latestLengths[route] = length
            latestSerials[route] = closedSerial
            latestPublish[route] = true
            hasLatest[route] = true
        }
    }

    private func passLatest(older: Bool) {
        for route in hasLatest.indices {
            if !hasLatest[route] || (hasClosed && latestSerials[route] < closedSerial) != older { continue }
            // A tick member newer than the state replaces it.
            if older && hasClosed && closedMembers[route] { continue }
            let publish = latestPublish[route]
            _ = gesNetworkWith(latest[route], latestLengths[route]) {
                publish ? connection.publish(route: route, payload: $0) : connection.send(route: route, payload: $0)
            }
        }
    }

    private func dropQueue() {
        queue.removeAll()
        queuedBytes = 0
        queuedTransferBytes = 0
        queuedTransfers = 0
        Self.clear(&hasLatest)
        Self.clear(&staged)
        Self.clear(&closedMembers)
        hasClosed = false
    }

    private static func clear(_ flags: inout [Bool]) {
        for index in flags.indices { flags[index] = false }
    }
}
