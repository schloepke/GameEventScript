// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// One side of a network connection: a synchronous state machine without threads, clocks or I/O.
///
/// The host feeds received bytes with `receive(_:now:)`, takes packets to send with `transmit(into:)`, advances the
/// time with `advance(_:)` and takes events with `nextEvent()`. Times are microseconds of one monotonic clock. A
/// connection is serial: one caller at a time; it may move between threads with a happens-before handoff.
public final class GesNetworkConnection {
    static let microsecondsPerSecond: Int64 = 1_000_000
    static let sendTimeWindow = 256

    /// The bound network both sides must share.
    public let network: GesBoundNetwork
    /// Whether this side is the client.
    public let isClient: Bool
    /// How the transport delimits packets.
    public let transport: GesNetworkTransport
    /// The largest packet this side sends or receives.
    public let maxPacketBytes: Int
    /// The state of the connection.
    public internal(set) var state: GesNetworkState
    /// The current time in microseconds.
    public internal(set) var now: Int64
    /// The smoothed round-trip time in microseconds; -1 before the first sample.
    public internal(set) var roundTripMicroseconds: Int64 = -1

    let maxPending: Int64
    let framesPerSecond: Int64
    let maxSendQueue: Int64
    let timeout: Int64
    let keepalive: Int64
    let ackDelay: Int64

    let routeCount: Int
    let classes: [GesNetworkClass]
    let localSends: [Bool]
    let maxBytes: [Int64]

    let token: [UInt8]
    var helloSent = false
    var welcomePending = false
    var peerPacketLimit = GesNetworkWire.minimumPacketBytes

    var closePending = false
    var closeCode = ""
    var closeDetail: [UInt8] = []

    var lastReceive: Int64
    var lastSend: Int64

    let sendReliable = GesNetworkSendQueue()
    let sendBulk = GesNetworkSendQueue()
    let sendLatest: [GesNetworkLatestSlot?]
    var nextReliableId: UInt16 = 0
    var nextBulkId: UInt16 = 0

    let receiveReliable = GesNetworkReassembly()
    let receiveBulk = GesNetworkReassembly()
    let receiveLatest: [GesNetworkReassembly?]

    var sentCount: Int64 = 0
    var sendTimes = [Int64](repeating: 0, count: GesNetworkConnection.sendTimeWindow)
    // Whether the sent packet asked for acknowledgement; only those give a round-trip sample.
    var sendAsks = [Bool](repeating: false, count: GesNetworkConnection.sendTimeWindow)
    var ackedIndex: Int64 = -1
    var receivedCount: Int64 = 0
    var ackDue = false
    var ackDeadline: Int64 = 0

    var frameUnits: Int64
    var budget: Int64
    var budgetUnits: Int64 = 0

    private var events: [EventEntry] = []
    private var eventHead = 0
    // The event bytes. A grown arena is retired, not freed, while the payload of the last returned event may point
    // into it; nextEvent() frees it.
    private var arena: UnsafeMutableRawPointer?
    private var arenaCapacity = 0
    private var arenaLength = 0
    private var retiredArena: UnsafeMutableRawPointer?
    var pending: Int64 = 0

    // The bytes of an incomplete stream packet: at most a prefix and a packet.
    var stream: UnsafeMutableRawBufferPointer?
    var streamLength = 0

    private init(network: GesBoundNetwork, client: Bool, token: [UInt8], now: Int64, options: GesNetworkOptions) throws {
        try options.validate()
        if token.count > GesNetworkWire.maximumTokenBytes { throw GesApiError.invalidArgument("A token holds at most 512 bytes.") }
        self.network = network
        isClient = client
        transport = options.transport
        maxPacketBytes = options.maxPacketBytes
        maxPending = options.maxPendingReceiveBytes
        framesPerSecond = Int64(options.maxFramesPerSecond)
        maxSendQueue = options.maxSendQueueBytes
        timeout = options.timeoutMicroseconds
        keepalive = options.keepaliveMicroseconds
        ackDelay = options.ackDelayMicroseconds
        budget = options.sendBudgetBytesPerSecond

        let routes = network.network.routes
        routeCount = routes.count
        var classes: [GesNetworkClass] = []
        var localSends: [Bool] = []
        var maxBytes: [Int64] = []
        var sendLatest: [GesNetworkLatestSlot?] = []
        var receiveLatest: [GesNetworkReassembly?] = []
        for (index, route) in routes.enumerated() {
            let local = (route.side == .client) == client
            classes.append(route.deliveryClass)
            localSends.append(local)
            maxBytes.append(Int64(network.roots[index].maxByteCount))
            let latest = route.deliveryClass == .latest
            sendLatest.append(latest && local ? GesNetworkLatestSlot() : nil)
            receiveLatest.append(latest && !local ? GesNetworkReassembly() : nil)
        }
        self.classes = classes
        self.localSends = localSends
        self.maxBytes = maxBytes
        self.sendLatest = sendLatest
        self.receiveLatest = receiveLatest
        self.token = token
        self.now = now
        lastReceive = now
        lastSend = now
        frameUnits = framesPerSecond * Self.microsecondsPerSecond
        state = client ? .hello : .awaitHello
        budgetUnits = budgetCapacity
    }

    deinit {
        arena?.deallocate()
        retiredArena?.deallocate()
        stream?.deallocate()
    }

    /// Creates the client side of a connection.
    /// - Parameters:
    ///   - network: The bound network the server must share.
    ///   - token: At most 512 bytes the server receives with the client's hello, such as a ticket.
    ///   - now: The current time in microseconds, from which the timeout runs.
    ///   - options: The limits and timings.
    /// - Returns: The connection in the `hello` state; its first transmitted packet is the hello.
    /// - Throws: `GesApiError.invalidArgument` when the token or an option is out of range.
    public static func makeClient(network: GesBoundNetwork, token: [UInt8] = [], now: Int64, options: GesNetworkOptions = GesNetworkOptions()) throws -> GesNetworkConnection {
        try GesNetworkConnection(network: network, client: true, token: token, now: now, options: options)
    }

    /// Creates the server side of a connection.
    /// - Parameters:
    ///   - network: The bound network the client must share.
    ///   - now: The current time in microseconds, from which the timeout runs.
    ///   - options: The limits and timings.
    /// - Returns: The connection in the `awaitHello` state.
    /// - Throws: `GesApiError.invalidArgument` when an option is out of range.
    public static func makeServer(network: GesBoundNetwork, now: Int64, options: GesNetworkOptions = GesNetworkOptions()) throws -> GesNetworkConnection {
        try GesNetworkConnection(network: network, client: false, token: [], now: now, options: options)
    }

    /// The received message bytes not yet taken as events.
    public var pendingReceiveBytes: Int64 { pending }

    /// The reliable and bulk bytes not yet completely sent.
    public var queuedSendBytes: Int64 { sendReliable.byteCount + sendBulk.byteCount }

    /// The bytes per second this side sends; 0 sends without a budget. Change it with
    /// `setSendBudgetBytesPerSecond(_:)`, for example from a congestion controller.
    public var sendBudgetBytesPerSecond: Int64 { budget }

    /// Changes the send budget.
    /// - Parameter value: The bytes per second this side sends; 0 sends without a budget.
    /// - Throws: `GesApiError.invalidArgument` when the value is negative.
    public func setSendBudgetBytesPerSecond(_ value: Int64) throws {
        if value < 0 { throw GesApiError.invalidArgument("The send budget is not negative.") }
        if budget == 0 { budgetUnits = budgetCapacity }
        budget = value
    }

    /// The time in microseconds at which the connection next needs `advance(_:)` or `transmit(into:)`;
    /// `Int64.max` once closed.
    public var nextDeadline: Int64 {
        if state == .closed { return .max }
        var deadline = lastReceive + timeout
        if state == .connected { deadline = min(deadline, lastSend + keepalive) }
        if ackDue { deadline = min(deadline, ackDeadline) }
        if budget > 0 && budgetUnits < 0 && hasPendingFrames() {
            deadline = min(deadline, now + (-budgetUnits + budget - 1) / budget)
        }
        return deadline
    }

    var sendPacketLimit: Int64 { Int64(min(maxPacketBytes, peerPacketLimit)) }

    var budgetCapacity: Int64 { sendPacketLimit * Self.microsecondsPerSecond }

    /// Advances the time; closes the connection with `network.timeout` when nothing arrived within the timeout.
    /// - Parameter now: The current time in microseconds.
    /// - Throws: `GesApiError.invalidArgument` when the time goes back.
    public func advance(_ now: Int64) throws {
        if now < self.now { throw GesApiError.invalidArgument("Time does not go back.") }
        let elapsed = now - self.now
        self.now = now
        frameUnits = Self.refill(frameUnits, framesPerSecond * Self.microsecondsPerSecond, elapsed, framesPerSecond)
        budgetUnits = budget == 0 ? budgetCapacity : Self.refill(budgetUnits, budgetCapacity, elapsed, budget)
        if state != .closed && now - lastReceive >= timeout { closeLocal(GesNetworkCodes.timeout, "", true) }
    }

    /// Queues a message on a route this side sends.
    /// - Parameters:
    ///   - route: The route index.
    ///   - payload: The encoded message; the bytes are copied.
    /// - Returns: Whether the message is queued, and why not.
    public func send(route: Int, payload: UnsafeRawBufferPointer) -> GesNetworkSendStatus {
        if route < 0 || route >= routeCount || !localSends[route] { return .invalidRoute }
        if state != .connected { return .notConnected }
        if Int64(payload.count) > maxBytes[route] { return .messageTooLarge }
        if let slot = sendLatest[route] {
            slot.set(payload)
            return .queued
        }
        if queuedSendBytes + Int64(payload.count) > maxSendQueue { return .queueFull }
        (classes[route] == .bulk ? sendBulk : sendReliable).enqueue(route, payload)
        return .queued
    }

    /// Queues a message on a route this side sends.
    /// - Parameters:
    ///   - route: The route index.
    ///   - payload: The encoded message; the bytes are copied.
    /// - Returns: Whether the message is queued, and why not.
    public func send(route: Int, payload: [UInt8]) -> GesNetworkSendStatus {
        payload.withUnsafeBytes { send(route: route, payload: $0) }
    }

    /// Accepts a connecting client; the server sends its welcome next.
    /// - Throws: `GesApiError.invalidOperation` unless the state is `awaitAccept`.
    public func accept() throws {
        if state != .awaitAccept { throw GesApiError.invalidOperation("Only a connecting client can be accepted.") }
        state = .connected
        welcomePending = true
        _ = addEvent(.connected, -1, UnsafeRawBufferPointer(start: nil, count: 0), false, "", "", false)
    }

    /// Refuses a connecting client with `network.refused`.
    /// - Parameter detail: The reason the client receives, shortened to 512 UTF-8 bytes.
    /// - Throws: `GesApiError.invalidOperation` unless the state is `awaitAccept`.
    public func refuse(_ detail: String) throws {
        if state != .awaitAccept { throw GesApiError.invalidOperation("Only a connecting client can be refused.") }
        closeLocal(GesNetworkCodes.refused, detail, true)
    }

    /// Closes the connection with `network.closed`; the close packet is transmitted next. Does nothing once closed.
    /// - Parameter detail: The reason the peer receives, shortened to 512 UTF-8 bytes.
    public func close(_ detail: String) {
        if state != .closed { closeLocal(GesNetworkCodes.closed, detail, true) }
    }

    /// Reports that the transport ended; closes with `network.transportClosed` without sending anything.
    public func transportClosed() {
        closePending = false
        if state != .closed { closeLocal(GesNetworkCodes.transportClosed, "", false) }
    }

    /// Takes the next event.
    /// - Returns: The event, or an event of kind `none` when no event waits. Its payload stays valid until the next
    ///   call of this method.
    public func nextEvent() -> GesNetworkEvent {
        if let retired = retiredArena {
            retired.deallocate()
            retiredArena = nil
        }
        if eventHead == events.count {
            eventHead = 0
            events.removeAll(keepingCapacity: true)
            arenaLength = 0
            return GesNetworkEvent(kind: .none, route: -1, payload: UnsafeRawBufferPointer(start: nil, count: 0), code: "", detail: "", byPeer: false)
        }
        let entry = events[eventHead]
        eventHead += 1
        if entry.counted { pending -= Int64(entry.length) }
        let payload = UnsafeRawBufferPointer(start: entry.length == 0 ? nil : arena! + entry.offset, count: entry.length)
        return GesNetworkEvent(kind: entry.kind, route: entry.route, payload: payload, code: entry.code, detail: entry.detail, byPeer: entry.byPeer)
    }

    func closeLocal(_ code: String, _ detail: String, _ sendPacket: Bool) {
        state = .closed
        dropSending()
        var reported = ""
        if sendPacket {
            closePending = true
            closeCode = code
            closeDetail = GesNetworkWire.detailBytes(detail)
            reported = closeDetail.isEmpty ? "" : String(decoding: closeDetail, as: UTF8.self)
        }
        _ = addEvent(.closed, -1, UnsafeRawBufferPointer(start: nil, count: 0), false, code, reported, false)
    }

    func dropSending() {
        sendReliable.clear()
        sendBulk.clear()
        for slot in sendLatest { slot?.clear() }
        ackDue = false
        welcomePending = false
    }

    func hasPendingFrames() -> Bool {
        if sendReliable.count > 0 || sendBulk.count > 0 { return true }
        for slot in sendLatest where slot?.hasCurrent == true { return true }
        return false
    }

    // Adds an event; a counted payload adds to the pending received bytes. Returns false when they exceed their limit.
    func addEvent(_ kind: GesNetworkEventKind, _ route: Int, _ payload: UnsafeRawBufferPointer, _ counted: Bool, _ code: String, _ detail: String, _ byPeer: Bool) -> Bool {
        if counted {
            pending += Int64(payload.count)
            if pending > maxPending { return false }
        }
        if arenaCapacity - arenaLength < payload.count {
            let capacity = max(arenaCapacity * 2, 256, arenaLength + payload.count)
            let grown = UnsafeMutableRawPointer.allocate(byteCount: capacity, alignment: 8)
            if let old = arena {
                if arenaLength > 0 { grown.copyMemory(from: old, byteCount: arenaLength) }
                if retiredArena == nil { retiredArena = old } else { old.deallocate() }
            }
            arena = grown
            arenaCapacity = capacity
        }
        if payload.count > 0 { (arena! + arenaLength).copyMemory(from: payload.baseAddress!, byteCount: payload.count) }
        events.append(EventEntry(kind: kind, route: route, offset: arenaLength, length: payload.count, counted: counted, code: code, detail: detail, byPeer: byPeer))
        arenaLength += payload.count
        return true
    }

    // Refills a token bucket of units per microsecond up to its capacity, without overflow.
    static func refill(_ units: Int64, _ capacity: Int64, _ elapsed: Int64, _ rate: Int64) -> Int64 {
        if units >= capacity { return capacity }
        let room = capacity - units
        if elapsed >= room / rate + 1 { return capacity }
        return units + elapsed * rate
    }

    private struct EventEntry {
        let kind: GesNetworkEventKind
        let route: Int
        let offset: Int
        let length: Int
        let counted: Bool
        let code: String
        let detail: String
        let byPeer: Bool
    }
}
