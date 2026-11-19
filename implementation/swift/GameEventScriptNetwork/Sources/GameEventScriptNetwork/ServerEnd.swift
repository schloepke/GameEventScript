// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// The server end of a session: it accepts clients from an acceptor, owns one server connection per client and drives
/// all of them from `poll(_:)`.
///
/// Clients have stable end-local ids. Publishes and ticks reach every client. One caller at a time; not thread-affine.
public final class GesNetworkServerEnd {
    private final class Client {
        let id: Int
        let link: any GesNetworkLink
        var left = false
        // The handler closed the connection while it handled the late ClientConnected event.
        var skipping = false

        init(id: Int, link: any GesNetworkLink) {
            self.id = id
            self.link = link
        }
    }

    /// The bound network the end's connections use.
    public let network: GesBoundNetwork
    /// The acceptor of the clients' transports; the end closes it when released.
    public let acceptor: any GesNetworkAcceptor
    /// The clock the end reads the time from.
    public let clock: any GesClock

    private let subscribeAll: Bool
    // Answers the checks of sends and publishes without a client.
    private let checker: GesNetworkConnection
    private var clients: [Client] = []
    private var byId: [Int: Client] = [:]
    private var nextId = 0
    private var released = false
    private(set) var polling = false
    // Whether a send, publish, tick or close outside a poll left packets that only the next poll transmits.
    private var transmitDue = false
    private var tickOpen = false
    // The last published state of each latest topic, which a client connected during a tick and include receive.
    private var kept: [[UInt8]]
    private var keptLengths: [Int]
    private var hasKept: [Bool]
    // The members of the open tick, which a client connected during it receives.
    private var members: [Bool]
    // Set by a synchronous pair, which runs both ends after every send.
    var pair: GesNetworkPair.Core?
    // Clients whose connected event a synchronous pair's handshake took and the next poll reports.
    private var reportConnected: [Int] = []

    /// Creates a server end. Accepts nothing until the first `poll(_:)`.
    /// - Parameters:
    ///   - network: The bound network both sides share.
    ///   - acceptor: Accepts the transports of clients; the end closes it when released.
    ///   - clock: The clock the end reads on every poll.
    ///   - subscribeAll: Whether every connected client is subscribed to every topic the server publishes.
    /// - Throws: `GesApiError.invalidArgument` when the network cannot form a server connection.
    public init(network: GesBoundNetwork, acceptor: any GesNetworkAcceptor, clock: any GesClock, subscribeAll: Bool = true) throws {
        self.network = network
        self.acceptor = acceptor
        self.clock = clock
        self.subscribeAll = subscribeAll
        checker = try GesNetworkConnection.makeServer(network: network, now: 0, options: .inProcess())
        let routes = checker.routeCount
        kept = [[UInt8]](repeating: [], count: routes)
        keptLengths = [Int](repeating: 0, count: routes)
        hasKept = [Bool](repeating: false, count: routes)
        members = [Bool](repeating: false, count: routes)
    }

    /// The number of clients the end holds, connecting or connected.
    public var clientCount: Int { clients.count }

    /// The earliest deadline of all clients, now while a send, publish, tick or close outside a poll left packets for
    /// the next poll; `Int64.max` without clients. An acceptor has no deadline: a server end that waits for clients
    /// polls at its own pace.
    public var nextDeadline: Int64 {
        if transmitDue && !clients.isEmpty { return clock.elapsedMicroseconds }
        var deadline = Int64.max
        for client in clients { deadline = min(deadline, client.link.connection.nextDeadline) }
        return deadline
    }

    /// The connection of a client.
    /// - Parameter client: The end-local client id.
    /// - Returns: The server connection of the client.
    /// - Throws: `GesApiError.invalidArgument` when no client has the id.
    public func connection(client: Int) throws -> GesNetworkConnection { try find(client).link.connection }

    /// Publishes a state on a topic the server publishes to every subscribed client. In an open tick a latest state is
    /// a member of it.
    /// - Parameters:
    ///   - route: The route index of a `publishes` route of the server.
    ///   - payload: The full record of the topic's schema; the bytes are copied.
    /// - Returns: `queued`; `messageTooLarge` when the payload exceeds the limit of some client, and then no client
    ///   receives it; `queueFull` when the queue of some client is full and the others received it; or the status of an invalid route or payload. Clients that are not connected or not subscribed are skipped.
    public func publish(route: Int, payload: UnsafeRawBufferPointer) -> GesNetworkSendStatus {
        var status = checker.check(route, payload.count, publish: true)
        if status != .queued { return status }
        if checker.isLatest(route) {
            gesNetworkKeep(&kept[route], payload)
            keptLengths[route] = payload.count
            hasKept[route] = true
            if tickOpen { members[route] = true }
        }
        for client in clients { status = Self.merge(status, client.link.connection.publish(route: route, payload: payload)) }
        return after(status)
    }

    /// Publishes a state on a topic the server publishes to every subscribed client, as `publish(route:payload:)` with
    /// borrowed bytes.
    /// - Parameters:
    ///   - route: The route index of a `publishes` route of the server.
    ///   - payload: The full record of the topic's schema; the bytes are copied.
    /// - Returns: The send status.
    public func publish(route: Int, payload: [UInt8]) -> GesNetworkSendStatus {
        payload.withUnsafeBytes { publish(route: route, payload: $0) }
    }

    /// Sends a payload on a route the server sends to every connected client.
    /// - Parameters:
    ///   - route: The route index of a `sends` route of the server.
    ///   - payload: The encoded root value; the bytes are copied.
    /// - Returns: As `publish(route:payload:)`.
    public func send(route: Int, payload: UnsafeRawBufferPointer) -> GesNetworkSendStatus {
        var status = checker.check(route, payload.count, publish: false)
        if status != .queued { return status }
        // A transfer limit differs per client; a transfer some client rejects goes to none of them.
        if checker.isBulk(route) && clients.contains(where: { $0.link.connection.rejectsTransfer(route, payload.count) }) { return .messageTooLarge }
        for client in clients { status = Self.merge(status, client.link.connection.send(route: route, payload: payload)) }
        return after(status)
    }

    // The status of a send to every client: messageTooLarge when the payload exceeds the limit of some client, else
    // queueFull when the queue of some client is full; clients that are not connected or not subscribed are skipped.
    private static func merge(_ status: GesNetworkSendStatus, _ client: GesNetworkSendStatus) -> GesNetworkSendStatus {
        switch client {
        case .messageTooLarge: return client
        case .queueFull where status == .queued: return client
        default: return status
        }
    }

    /// Sends a payload on a route the server sends to every connected client, as `send(route:payload:)` with borrowed
    /// bytes.
    /// - Parameters:
    ///   - route: The route index of a `sends` route of the server.
    ///   - payload: The encoded root value; the bytes are copied.
    /// - Returns: The send status.
    public func send(route: Int, payload: [UInt8]) -> GesNetworkSendStatus {
        payload.withUnsafeBytes { send(route: route, payload: $0) }
    }

    /// Sends a payload on a route the server sends to one client.
    /// - Parameters:
    ///   - route: The route index of a `sends` route of the server.
    ///   - payload: The encoded root value; the bytes are copied.
    ///   - client: The end-local client id.
    /// - Returns: The send status of the client's connection.
    /// - Throws: `GesApiError.invalidArgument` when no client has the id.
    public func send(route: Int, payload: UnsafeRawBufferPointer, client: Int) throws -> GesNetworkSendStatus {
        after(try find(client).link.connection.send(route: route, payload: payload))
    }

    /// Sends a payload on a route the server sends to one client, as `send(route:payload:client:)` with borrowed bytes.
    /// - Parameters:
    ///   - route: The route index of a `sends` route of the server.
    ///   - payload: The encoded root value; the bytes are copied.
    ///   - client: The end-local client id.
    /// - Returns: The send status of the client's connection.
    /// - Throws: `GesApiError.invalidArgument` when no client has the id.
    public func send(route: Int, payload: [UInt8], client: Int) throws -> GesNetworkSendStatus {
        try payload.withUnsafeBytes { try send(route: route, payload: $0, client: client) }
    }

    /// Opens a tick on every client, as `GesNetworkConnection.beginTick()`; a client connected during it joins it.
    /// - Throws: `GesApiError.invalidOperation` when a tick is open already.
    public func beginTick() throws {
        if tickOpen { throw GesApiError.invalidOperation("A tick is open already.") }
        tickOpen = true
        for client in clients where client.link.connection.state == .connected { try client.link.connection.beginTick() }
    }

    /// Makes a latest topic a member of the open tick with its last published state: a client that has received the
    /// state names it, a subscribed client that has not receives it.
    /// - Parameter route: The route index of a latest topic the server publishes.
    /// - Throws: `GesApiError.invalidOperation` when no tick is open or the topic has no published state;
    ///   `GesApiError.invalidArgument` when the route is no latest topic the server publishes.
    public func include(route: Int) throws {
        if !tickOpen { throw GesApiError.invalidOperation("No tick is open.") }
        if !checker.isLocalLatestTopic(route) { throw GesApiError.invalidArgument("The route is no latest topic this side publishes.") }
        if !hasKept[route] { throw GesApiError.invalidOperation("The topic has no published state.") }
        members[route] = true
        for client in clients where client.link.connection.tickOpen { try includeKept(client.link.connection, route) }
    }

    /// Closes the open tick on every client.
    /// - Returns: `queued`.
    /// - Throws: `GesApiError.invalidOperation` when no tick is open.
    @discardableResult
    public func endTick() throws -> GesNetworkSendStatus {
        if !tickOpen { throw GesApiError.invalidOperation("No tick is open.") }
        tickOpen = false
        for index in members.indices { members[index] = false }
        for client in clients where client.link.connection.tickOpen { try client.link.connection.endTick() }
        return after(.queued)
    }

    /// Subscribes a client to a topic the server publishes.
    /// - Parameters:
    ///   - client: The end-local client id.
    ///   - route: The route index of a topic the server publishes.
    /// - Throws: `GesApiError.invalidArgument` when no client has the id or the route is no topic the server publishes.
    public func subscribe(client: Int, route: Int) throws { try find(client).link.connection.subscribe(route: route) }

    /// Unsubscribes a client from a topic the server publishes, as `GesNetworkConnection.unsubscribe(route:)`.
    /// - Parameters:
    ///   - client: The end-local client id.
    ///   - route: The route index of a topic the server publishes.
    /// - Throws: `GesApiError.invalidArgument` when no client has the id or the route is no topic the server publishes.
    public func unsubscribe(client: Int, route: Int) throws { try find(client).link.connection.unsubscribe(route: route) }

    /// Refuses a client during its `GesNetworkEndEventKind.connecting` event.
    /// - Parameters:
    ///   - client: The end-local client id.
    ///   - detail: The detail sent to the client.
    /// - Throws: `GesApiError.invalidArgument` when no client has the id; `GesApiError.invalidOperation` when the client
    ///   is not connecting.
    public func refuse(client: Int, detail: String) throws {
        try find(client).link.connection.refuse(detail)
        _ = after(.queued)
    }

    /// Closes the connection of a client; it leaves the end with its disconnect event. Does nothing once closed.
    /// - Parameters:
    ///   - client: The end-local client id.
    ///   - detail: The detail sent to the client.
    /// - Throws: `GesApiError.invalidArgument` when no client has the id.
    public func close(client: Int, detail: String) throws {
        try find(client).link.connection.close(detail)
        _ = after(.queued)
    }

    /// Closes the connections of all clients.
    /// - Parameter detail: The detail sent to the clients.
    public func close(_ detail: String) {
        for client in clients { client.link.connection.close(detail) }
        _ = after(.queued)
    }

    /// Accepts every waiting client, passes received packets to every connection, hands every event to the handler and
    /// transmits every due packet. A connecting client is accepted after its event unless the handler accepted or
    /// refused it, also when the handler throws; after the handler closes or refuses a client, that client's remaining
    /// events up to its disconnect are skipped, and a closed client leaves the end after its event. Never blocks and
    /// never calls the handler outside this call.
    /// - Parameter handler: The handler of the events. It may send, publish, close, refuse and open or close ticks on
    ///   the end; an event's payload is valid only until it returns.
    /// - Throws: `GesApiError.invalidOperation` when the end is released or the handler polls the end again; an error
    ///   of the acceptor, a link or the handler.
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
            let now = clock.elapsedMicroseconds
            while let link = try acceptor.accept(now: now) {
                let client = Client(id: nextId, link: link)
                nextId += 1
                clients.append(client)
                byId[client.id] = client
            }
            var received = false
            if report {
                for id in reportConnected {
                    guard let client = byId[id] else { continue }
                    let open = client.link.connection.state != .closed
                    try handler(GesNetworkEndEvent(.clientConnected, client: id, security: client.link.connection.security))
                    client.skipping = open && client.link.connection.state == .closed
                }
                reportConnected.removeAll()
            }
            // Clients that join during the poll wait for the next one; a client that leaves is removed after its events.
            let count = clients.count
            for index in 0..<count {
                let client = clients[index]
                let connection = client.link.connection
                try client.link.receive(now: max(now, connection.now))
                // After the handler closed or refused the connection, its remaining events up to the disconnect are skipped, also
                // after the ClientConnected event that a synchronous pair reports late.
                var skipping = client.skipping
                client.skipping = false
                while true {
                    let next = connection.nextEvent()
                    if next.kind == .none { break }
                    if skipping && next.kind != .closed { continue }
                    let kind = GesNetworkEndEvent.kind(next.kind, client: false)
                    if kind == .clientConnected {
                        try join(connection)
                    } else if kind == .clientDisconnected {
                        client.left = true
                    }
                    if !report {
                        if kind == .clientConnected { reportConnected.append(client.id) }
                    } else if kind == .connecting {
                        do {
                            try handler(GesNetworkEndEvent(kind, client: client.id, source: next, security: .none))
                        } catch {
                            // A client the handler neither accepted nor refused is accepted, also when the handler throws.
                            if connection.state == .awaitAccept { try connection.accept() }
                            throw error
                        }
                        if connection.state == .awaitAccept { try connection.accept() }
                        skipping = connection.state == .closed
                        continue
                    } else {
                        if kind == .message || kind == .tick { received = true }
                        let open = connection.state != .closed
                        try handler(GesNetworkEndEvent(kind, client: client.id, source: next, security: kind == .clientConnected ? connection.security : .none))
                        skipping = open && connection.state == .closed
                    }
                    // A client is accepted when no handler sees its request.
                    if kind == .connecting && connection.state == .awaitAccept { try connection.accept() }
                }
            }
            for index in clients.indices.reversed() where clients[index].left {
                let client = clients.remove(at: index)
                byId[client.id] = nil
                try client.link.transmit()
                client.link.close()
            }
            if received { try handler(GesNetworkEndEvent(.received, client: -1, security: .none)) }
            for client in clients { try client.link.transmit() }
            transmitDue = false
        }
        pair?.exchange()
    }

    /// Releases every client's transport once the packets already handed to it have left, and closes the acceptor.
    /// Further polls throw. Does nothing when called again.
    public func release() {
        if released { return }
        released = true
        for client in clients { client.link.close() }
        clients.removeAll()
        byId.removeAll()
        acceptor.close()
    }

    // Prepares a client that just connected: its subscriptions and the open tick with the members so far.
    private func join(_ connection: GesNetworkConnection) throws {
        if subscribeAll {
            for route in 0..<checker.routeCount where checker.check(route, 0, publish: true) != .invalidRoute { try connection.subscribe(route: route) }
        }
        if !tickOpen || connection.tickOpen { return }
        try connection.beginTick()
        for route in members.indices where members[route] { try includeKept(connection, route) }
    }

    /// A connection whose last state of the topic is the kept one names it; another one, which missed a later publish
    /// while it was not subscribed, receives the kept state.
    private func includeKept(_ connection: GesNetworkConnection, _ route: Int) throws {
        if connection.lastIs(route, kept[route], keptLengths[route]) {
            try connection.include(route: route)
        } else if connection.isSubscribed(route) {
            _ = gesNetworkWith(kept[route], keptLengths[route]) { connection.publish(route: route, payload: $0) }
        }
    }

    private func find(_ client: Int) throws -> Client {
        guard let found = byId[client] else { throw GesApiError.invalidArgument("No client has the id.") }
        return found
    }

    // Lets a synchronous pair deliver what a send outside a poll produced; another link transmits it with the next
    // poll, which the next deadline brings forward.
    private func after(_ status: GesNetworkSendStatus) -> GesNetworkSendStatus {
        if polling { return status }
        if let pair { pair.exchange() } else { transmitDue = true }
        return status
    }
}
