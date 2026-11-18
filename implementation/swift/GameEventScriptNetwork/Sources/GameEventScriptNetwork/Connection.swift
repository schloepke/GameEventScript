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
    static let maxPendingEvents = 65536

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
    let maxBaseline: Int64
    var baselineLimit: Int64 = 0
    let maxTransfer: Int64
    let deflate: Bool
    var peerTransferLimit: Int64 = 0
    var peerDeflate = false

    let routeCount: Int
    let classes: [GesNetworkClass]
    let localSends: [Bool]
    let maxBytes: [Int64]
    let topics: [GesNetworkTopic?]

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
    let sendBulk = GesNetworkSendQueue(preassigned: true)
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

    var events: [EventEntry] = []
    var eventHead = 0
    // The event bytes. A grown arena is retired, not freed, while the payload of the last returned event may point
    // into it; nextEvent() frees it.
    private var arena: UnsafeMutableRawPointer?
    private var arenaCapacity = 0
    var arenaLength = 0
    private var retiredArena: UnsafeMutableRawPointer?
    var pending: Int64 = 0
    var pendingBulk: Int64 = 0

    // The bytes of an incomplete stream packet: at most a prefix and a packet.
    var stream: UnsafeMutableRawBufferPointer?
    var streamLength = 0

    // Datagram delivery, see ConnectionDatagram.swift.
    let datagram: Bool

    // Receiving: the newest sequence, a bit for each of the 32 before it, and the asking packets since the last send.
    var receivedAny = false
    var newestSequence: UInt16 = 0
    var sequenceBits: UInt32 = 0
    var asksSinceSend = 0
    let orderedReliable: GesNetworkOrderedReceive?
    let orderedBulk: GesNetworkOrderedReceive?
    let partialLatest: [GesNetworkPartial?]

    // Owed confirmation frames per route.
    var owed: [Bool]
    var owedNumbers: [UInt16]

    // Sent packets: time, whether it asked, whether it is outstanding, and its fragments in the fragment ring.
    var recordTimes: [Int64]
    var recordAsks: [Bool]
    var recordOutstanding: [Bool]
    var recordFragments: [Int64]
    var recordFragmentCounts: [Int]
    var fragmentLanes: [Int]
    var fragmentKeys: [Int64]
    var fragmentOffsets: [Int]
    var fragmentLengths: [Int]
    var fragmentTail: Int64 = 0
    var fragmentRoom = 0
    var outstanding = 0
    var oldestOutstanding: Int64 = 0

    var largestAcked: Int64 = -1
    var variation: Int64 = GesNetworkConnection.initialVariation
    var latestSample: Int64 = GesNetworkConnection.initialRoundTrip
    var lossTime: Int64 = .max
    var probeCount = 0
    var probeDue = false
    var lastAskSend: Int64 = 0

    // The client's hello repetition and the cookie of the latest retry.
    var helloWritten = false
    var helloNext: Int64 = 0
    var helloInterval: Int64 = GesNetworkConnection.helloRepeatInterval
    var cookie: [UInt8] = []

    // Encryption, see ConnectionSecurity.swift.
    let minimumSecurity: GesNetworkSecurity
    let key: GesNetworkKey?
    let pinned: Bool
    let testKeys: Bool
    let testKeySeed: UInt64
    var ephemeralCount: UInt64 = 0
    // The server's public key: the pinned one or the one a retry gave the client.
    var serverKey: [UInt8]?
    var keys: Keys?
    // The session is encrypted: the client read a sealed welcome, the server opened a sealed hello.
    var encrypted = false
    // This side sends sealed packets: from the server's welcome or the client's reading of it on.
    var sealing = false
    var retryPending = false
    var retryAccepted = false
    // The hello the client holds is sealed; its next one needs a fresh ephemeral key.
    var helloSealed = false
    var ephemeralStale = true

    /// The security level the session reached: `GesNetworkSecurity.none` until it is connected; a server reports
    /// `GesNetworkSecurity.encrypted` for every encrypted session.
    public internal(set) var security: GesNetworkSecurity = .none

    /// Whether the connection derives its ephemeral keys from `GesNetworkOptions.insecureTestKeySeed`.
    public var usesInsecureTestKeys: Bool { testKeys }

    // Bulk transfers, see ConnectionTransfer.swift: the codecs and hash, created when first needed.
    var deflater: GesNetworkDeflater?
    var inflater: GesNetworkInflater?
    let hash = UnsafeMutableRawBufferPointer.allocate(byteCount: GesNetworkWire.hashBytes, alignment: 1)
    var sha256: GesNetworkSha256?

    // Cancel frames to send: the route, the id, the code and whether it is sent and unconfirmed or confirmed.
    let cancels = GesNetworkCancels()

    // The ids of local transfers the peer cancelled, so a repeated cancel is reported once.
    var peerCancelled = [UInt64](repeating: 0, count: 65536 / 64)

    // Stream sender progress: written bulk bytes, their count at each sent packet, and the written transfers not yet
    // confirmed completely.
    var bulkWritten: Int64 = 0
    var bulkMarks = [Int64](repeating: 0, count: GesNetworkConnection.sendTimeWindow)
    let written = GesNetworkWrittenTransfers()

    /// The id of the transfer the latest successful `send(route:payload:)` on a bulk route queued; -1 before the first.
    public internal(set) var lastTransferId = -1

    private init(network: GesBoundNetwork, client: Bool, token: [UInt8], now: Int64, options: GesNetworkOptions) throws {
        try options.validate()
        if token.count > GesNetworkWire.maximumTokenBytes { throw GesApiError.invalidArgument("A token holds at most 512 bytes.") }
        self.network = network
        isClient = client
        transport = options.transport
        datagram = options.transport == .datagram
        maxPacketBytes = options.packetLimit
        maxPending = options.maxPendingReceiveBytes
        framesPerSecond = Int64(options.maxFramesPerSecond)
        maxSendQueue = options.maxSendQueueBytes
        timeout = options.timeoutMicroseconds
        keepalive = options.keepaliveMicroseconds
        ackDelay = options.ackDelayMicroseconds
        budget = options.sendBudgetBytesPerSecond
        maxBaseline = options.maxBaselineBytes
        maxTransfer = options.maxTransferBytes
        deflate = options.deflateTransfers
        minimumSecurity = options.minimumSecurity
        if client {
            serverKey = options.serverPublicKey
            pinned = options.serverPublicKey != nil
            key = nil
        } else {
            key = options.key
            pinned = false
            if key == nil && minimumSecurity != .none {
                throw GesApiError.invalidArgument("A server whose lowest security level is above none needs a key.")
            }
        }
        testKeys = options.insecureTestKeySeed != nil
        testKeySeed = options.insecureTestKeySeed ?? 0

        let routes = network.network.routes
        routeCount = routes.count
        var classes: [GesNetworkClass] = []
        var localSends: [Bool] = []
        var maxBytes: [Int64] = []
        var sendLatest: [GesNetworkLatestSlot?] = []
        var receiveLatest: [GesNetworkReassembly?] = []
        var partialLatest: [GesNetworkPartial?] = []
        var topics: [GesNetworkTopic?] = []
        for (index, route) in routes.enumerated() {
            let local = (route.side == .client) == client
            classes.append(route.deliveryClass)
            localSends.append(local)
            let root = network.roots[index]
            if route.verb == .publishes {
                // A topic message adds its mode byte; a client's own topics are always subscribed.
                maxBytes.append(Int64(root.maxByteCount) + 1)
                topics.append(GesNetworkTopic(root, subscribed: client))
            } else {
                maxBytes.append(Int64(root.maxByteCount))
                topics.append(nil)
            }
            let latest = route.deliveryClass == .latest
            sendLatest.append(latest && local ? GesNetworkLatestSlot(datagram: datagram) : nil)
            receiveLatest.append(latest && !local && !datagram ? GesNetworkReassembly() : nil)
            if datagram { partialLatest.append(latest && !local ? GesNetworkPartial() : nil) }
        }
        self.classes = classes
        self.localSends = localSends
        self.maxBytes = maxBytes
        self.topics = topics
        self.sendLatest = sendLatest
        self.receiveLatest = receiveLatest
        self.partialLatest = partialLatest
        // Only a datagram connection records its packets and restores the order of what it receives.
        let datagramRoutes = datagram ? routeCount : 0
        let records = datagram ? Self.recordedPackets : 0
        let fragments = datagram ? Self.recordedFragments : 0
        owed = [Bool](repeating: false, count: datagramRoutes)
        owedNumbers = [UInt16](repeating: 0, count: datagramRoutes)
        recordTimes = [Int64](repeating: 0, count: records)
        recordAsks = [Bool](repeating: false, count: records)
        recordOutstanding = [Bool](repeating: false, count: records)
        recordFragments = [Int64](repeating: 0, count: records)
        recordFragmentCounts = [Int](repeating: 0, count: records)
        fragmentLanes = [Int](repeating: 0, count: fragments)
        fragmentKeys = [Int64](repeating: 0, count: fragments)
        fragmentOffsets = [Int](repeating: 0, count: fragments)
        fragmentLengths = [Int](repeating: 0, count: fragments)
        orderedReliable = datagram ? GesNetworkOrderedReceive(retention: options.maxPendingReceiveBytes) : nil
        orderedBulk = datagram ? GesNetworkOrderedReceive(retention: 2 * (options.maxTransferBytes + Int64(GesNetworkWire.transferHeaderBytes))) : nil
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
        hash.deallocate()
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

    /// The received bytes of incomplete messages and transfers and of events not yet taken.
    public var pendingReceiveBytes: Int64 { pending + pendingBulk }

    /// The reliable and bulk bytes not yet completely sent, on a datagram transport not yet completely acknowledged.
    public var queuedSendBytes: Int64 { sendReliable.byteCount + sendBulk.byteCount }

    /// The bytes per second this side sends; 0 sends without a budget. Change it with
    /// `setSendBudgetBytesPerSecond(_:)`, for example from a congestion controller.
    public var sendBudgetBytesPerSecond: Int64 { budget }

    /// Changes the send budget.
    /// - Parameter value: The bytes per second this side sends; 0 sends without a budget.
    /// - Throws: `GesApiError.invalidArgument` when the value is negative or above 1073741824.
    public func setSendBudgetBytesPerSecond(_ value: Int64) throws {
        if !(0...GesNetworkOptions.maximumBytes).contains(value) { throw GesApiError.invalidArgument("The send budget is 0 to 1073741824 bytes per second.") }
        if budget == 0 { budgetUnits = budgetCapacity }
        budget = value
    }

    /// The time in microseconds at which the connection next needs `advance(_:)` or `transmit(into:)`: the timeout,
    /// keepalive, acknowledgement or budget refill, and on a datagram transport also the repetition of the hello, loss
    /// detection or probe timeout; `Int64.max` once closed.
    public var nextDeadline: Int64 {
        if state == .closed { return .max }
        var deadline = lastReceive + timeout
        if state == .connected { deadline = min(deadline, lastSend + keepalive) }
        if ackDue { deadline = min(deadline, ackDeadline) }
        if budget > 0 && budgetUnits < 0 && hasPendingFrames() {
            deadline = min(deadline, now + (-budgetUnits + budget - 1) / budget)
        }
        if datagram { deadline = min(deadline, datagramDeadline()) }
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
        if state != .closed && now - lastReceive >= timeout {
            closeLocal(GesNetworkCodes.timeout, "", true)
        } else if datagram {
            advanceDatagram()
        }
    }

    /// Queues a message on a route this side sends. On a bulk route the message is a transfer: it is hashed, compressed
    /// when that is shorter and both sides allow it, and gets its id at once (`lastTransferId`).
    /// - Parameters:
    ///   - route: The route index of a `sends` route of this side.
    ///   - payload: The encoded message; the bytes are copied.
    /// - Returns: Whether the message is queued, and why not.
    public func send(route: Int, payload: UnsafeRawBufferPointer) -> GesNetworkSendStatus {
        if route < 0 || route >= routeCount || !localSends[route] || topics[route] != nil { return .invalidRoute }
        if state != .connected { return .notConnected }
        if classes[route] == .bulk { return sendTransfer(route, payload) }
        if Int64(payload.count) > maxBytes[route] { return .messageTooLarge }
        if let slot = sendLatest[route] {
            slot.set(payload)
            return .queued
        }
        if sendReliable.byteCount + Int64(payload.count) > maxSendQueue { return .queueFull }
        sendReliable.enqueue(route, payload)
        return .queued
    }

    /// Queues a message on a route this side sends. On a bulk route the message is a transfer: it is hashed, compressed
    /// when that is shorter and both sides allow it, and gets its id at once (`lastTransferId`).
    /// - Parameters:
    ///   - route: The route index of a `sends` route of this side.
    ///   - payload: The encoded message; the bytes are copied.
    /// - Returns: Whether the message is queued, and why not.
    public func send(route: Int, payload: [UInt8]) -> GesNetworkSendStatus {
        payload.withUnsafeBytes { send(route: route, payload: $0) }
    }

    /// Publishes a state on a topic this side publishes. The receiver gets the full record; on the wire it travels as
    /// a delta against a confirmed earlier state when that is shorter.
    /// - Parameters:
    ///   - route: The route index of a `publishes` route of this side.
    ///   - payload: The full record of the topic's schema; the bytes are copied and not validated.
    /// - Returns: Whether the state is queued, and why not; `notSubscribed` for a server topic the client is not
    ///   subscribed to.
    public func publish(route: Int, payload: UnsafeRawBufferPointer) -> GesNetworkSendStatus {
        if route < 0 || route >= routeCount || !localSends[route] { return .invalidRoute }
        guard let topic = topics[route] else { return .invalidRoute }
        if state != .connected { return .notConnected }
        if !topic.subscribed { return .notSubscribed }
        if Int64(payload.count) >= maxBytes[route] { return .messageTooLarge }
        if let slot = sendLatest[route] {
            slot.set(payload)
            return .queued
        }
        let length = encodeState(topic, payload, topic.next)
        if sendReliable.byteCount + Int64(length) > maxSendQueue { return .queueFull }
        sendReliable.enqueue(route, UnsafeRawBufferPointer(rebasing: topic.scratch[0..<length]), Int(topic.next))
        topic.add(payload, baselineLimit, topic.next)
        topic.next &+= 1
        return .queued
    }

    /// Publishes a state on a topic this side publishes. The receiver gets the full record; on the wire it travels as
    /// a delta against a confirmed earlier state when that is shorter.
    /// - Parameters:
    ///   - route: The route index of a `publishes` route of this side.
    ///   - payload: The full record of the topic's schema; the bytes are copied and not validated.
    /// - Returns: Whether the state is queued, and why not; `notSubscribed` for a server topic the client is not
    ///   subscribed to.
    public func publish(route: Int, payload: [UInt8]) -> GesNetworkSendStatus {
        payload.withUnsafeBytes { publish(route: route, payload: $0) }
    }

    /// Subscribes the client to a topic the server publishes. Sends nothing by itself; does nothing when subscribed.
    /// - Parameter route: The route index of a topic the server publishes.
    /// - Throws: `GesApiError.invalidOperation` on a client connection; `GesApiError.invalidArgument` when the route is
    ///   no topic the server publishes.
    public func subscribe(route: Int) throws {
        try serverTopic(route).subscribed = true
    }

    /// Unsubscribes the client from a topic the server publishes: drops its waiting latest state and the ring of sent
    /// states. A latest state already started and queued reliable states are still sent. Does nothing when not
    /// subscribed.
    /// - Parameter route: The route index of a topic the server publishes.
    /// - Throws: `GesApiError.invalidOperation` on a client connection; `GesApiError.invalidArgument` when the route is
    ///   no topic the server publishes.
    public func unsubscribe(route: Int) throws {
        let topic = try serverTopic(route)
        if !topic.subscribed { return }
        topic.subscribed = false
        topic.clear()
        sendLatest[route]?.dropUnstarted()
    }

    private func serverTopic(_ route: Int) throws -> GesNetworkTopic {
        if isClient { throw GesApiError.invalidOperation("Only the server subscribes clients to topics.") }
        if route < 0 || route >= routeCount || !localSends[route] { throw GesApiError.invalidArgument("The route is no topic the server publishes.") }
        guard let topic = topics[route] else { throw GesApiError.invalidArgument("The route is no topic the server publishes.") }
        return topic
    }

    // Writes the message of a new state into the topic's scratch: a delta against the newest confirmed state when it
    // is shorter than the full record, else the full record. The number is the new state's. Returns the message length.
    func encodeState(_ topic: GesNetworkTopic, _ record: UnsafeRawBufferPointer, _ number: UInt16) -> Int {
        let message = topic.scratch
        var baseline = UnsafeRawBufferPointer(start: nil, count: 0)
        let distance = topic.base(datagram ? -1 : ackedIndex, number, &baseline)
        if distance > 0 {
            // A record without a delta, such as one with another key or an invalid one, travels in full.
            if let length = try? GesSchemaDelta.encode(topic.schema, record: record, baseline: baseline, into: UnsafeMutableRawBufferPointer(rebasing: message[1...])),
                length < record.count
            {
                message[0] = UInt8(distance)
                return length + 1
            }
        }
        message[0] = 0
        if record.count > 0 { (message.baseAddress! + 1).copyMemory(from: record.baseAddress!, byteCount: record.count) }
        return record.count + 1
    }

    /// Accepts a connecting client; the server sends its welcome next.
    /// - Throws: `GesApiError.invalidOperation` unless the state is `awaitAccept`.
    public func accept() throws {
        if state != .awaitAccept { throw GesApiError.invalidOperation("Only a connecting client can be accepted.") }
        state = .connected
        security = encrypted ? .encrypted : .none
        welcomePending = true
        _ = addEvent(.connected, -1, UnsafeRawBufferPointer(start: nil, count: 0), .none, "", "", false)
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
            return GesNetworkEvent(
                kind: .none,
                route: -1,
                payload: UnsafeRawBufferPointer(start: nil, count: 0),
                code: "",
                detail: "",
                byPeer: false,
                transferId: -1,
                bytes: 0,
                total: 0
            )
        }
        compactEvents()
        let entry = events[eventHead]
        eventHead += 1
        if entry.counter == .pending {
            pending -= Int64(entry.length)
        } else if entry.counter == .bulk {
            pendingBulk -= Int64(entry.length)
        }
        let payload = UnsafeRawBufferPointer(start: entry.length == 0 ? nil : arena! + entry.offset, count: entry.length)
        return GesNetworkEvent(
            kind: entry.kind,
            route: entry.route,
            payload: payload,
            code: entry.code,
            detail: entry.detail,
            byPeer: entry.byPeer,
            transferId: entry.transferId,
            bytes: entry.bytes,
            total: entry.total
        )
    }

    // Moves the pending events and their payloads to the front once the taken ones occupy at least as much as the pending
    // ones, so the queue and its arena stay within twice their pending size while a host never empties them.
    private func compactEvents() {
        let taken = events[eventHead].offset
        let count = events.count - eventHead
        if eventHead < 16 || eventHead < count || taken < arenaLength - taken { return }
        if arenaLength > taken { arena!.copyMemory(from: arena! + taken, byteCount: arenaLength - taken) }
        arenaLength -= taken
        for index in 0..<count {
            let entry = events[eventHead + index]
            events[index] = EventEntry(
                kind: entry.kind,
                route: entry.route,
                offset: entry.offset - taken,
                length: entry.length,
                counter: entry.counter,
                code: entry.code,
                detail: entry.detail,
                byPeer: entry.byPeer,
                transferId: entry.transferId,
                bytes: entry.bytes,
                total: entry.total
            )
        }
        events.removeLast(eventHead)
        eventHead = 0
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
        _ = addEvent(.closed, -1, UnsafeRawBufferPointer(start: nil, count: 0), .none, code, reported, false)
    }

    func dropSending() {
        sendReliable.clear()
        sendBulk.clear()
        dropTransfers()
        for slot in sendLatest { slot?.clear() }
        ackDue = false
        welcomePending = false
        retryPending = false
        dropDatagramSending()
    }

    func hasPendingFrames() -> Bool {
        if datagram { return hasPendingDatagramFrames() }
        if sendReliable.count > 0 || sendBulk.count > 0 || cancels.pendingCount > 0 { return true }
        for slot in sendLatest where slot?.hasCurrent == true { return true }
        return false
    }

    // Adds an event; a counted payload adds to its pending received bytes. Returns false when they exceed their limit or
    // the pending events their count; a closed event always fits.
    func addEvent(
        _ kind: GesNetworkEventKind,
        _ route: Int,
        _ payload: UnsafeRawBufferPointer,
        _ counter: Counter,
        _ code: String,
        _ detail: String,
        _ byPeer: Bool,
        _ transferId: Int = -1
    ) -> Bool {
        guard let reserved = reserveEvent(kind, route, payload.count, counter, code, detail, byPeer, transferId, 0, 0) else { return false }
        if payload.count > 0 { reserved.baseAddress!.copyMemory(from: payload.baseAddress!, byteCount: payload.count) }
        return true
    }

    // Adds an event with a payload of the length and returns its bytes to fill; nil when the counted bytes exceed their
    // limit.
    func reserveEvent(
        _ kind: GesNetworkEventKind,
        _ route: Int,
        _ length: Int,
        _ counter: Counter,
        _ code: String,
        _ detail: String,
        _ byPeer: Bool,
        _ transferId: Int,
        _ bytes: Int64,
        _ total: Int64
    ) -> UnsafeMutableRawBufferPointer? {
        if events.count - eventHead >= GesNetworkConnection.maxPendingEvents && kind != .closed { return nil }
        if counter == .pending {
            pending += Int64(length)
            if pending > maxPending { return nil }
        } else if counter == .bulk {
            pendingBulk += Int64(length)
            if pendingBulk > maxPendingBulk { return nil }
        }
        if arenaCapacity - arenaLength < length {
            let capacity = max(arenaCapacity * 2, 256, arenaLength + length)
            let grown = UnsafeMutableRawPointer.allocate(byteCount: capacity, alignment: 8)
            if let old = arena {
                if arenaLength > 0 { grown.copyMemory(from: old, byteCount: arenaLength) }
                if retiredArena == nil { retiredArena = old } else { old.deallocate() }
            }
            arena = grown
            arenaCapacity = capacity
        }
        events.append(
            EventEntry(
                kind: kind,
                route: route,
                offset: arenaLength,
                length: length,
                counter: counter,
                code: code,
                detail: detail,
                byPeer: byPeer,
                transferId: transferId,
                bytes: bytes,
                total: total
            )
        )
        let reserved = UnsafeMutableRawBufferPointer(start: length == 0 ? nil : arena! + arenaLength, count: length)
        arenaLength += length
        return reserved
    }

    // Reports the progress of a transfer; it replaces the newest event when that reports the same transfer. False when
    // it closed the connection because too many events are pending.
    func addProgress(_ route: Int, _ id: UInt16, _ bytes: Int64, _ total: Int64) -> Bool {
        if events.count > eventHead {
            let last = events[events.count - 1]
            if last.kind == .transferProgress && last.route == route && last.transferId == Int(id) {
                events[events.count - 1] = EventEntry(
                    kind: last.kind,
                    route: route,
                    offset: last.offset,
                    length: 0,
                    counter: .none,
                    code: "",
                    detail: "",
                    byPeer: false,
                    transferId: Int(id),
                    bytes: bytes,
                    total: total
                )
                return true
            }
        }
        return reserveEvent(.transferProgress, route, 0, .none, "", "", false, Int(id), bytes, total) != nil || fail(GesNetworkCodes.queueFull)
    }

    // Refills a token bucket of units per microsecond up to its capacity, without overflow.
    static func refill(_ units: Int64, _ capacity: Int64, _ elapsed: Int64, _ rate: Int64) -> Int64 {
        if units >= capacity { return capacity }
        let room = capacity - units
        if elapsed >= room / rate + 1 { return capacity }
        return units + elapsed * rate
    }

    // Which pending count an event's payload adds to.
    enum Counter {
        case none
        case pending
        case bulk
    }

    struct EventEntry {
        let kind: GesNetworkEventKind
        let route: Int
        let offset: Int
        let length: Int
        let counter: Counter
        let code: String
        let detail: String
        let byPeer: Bool
        let transferId: Int
        let bytes: Int64
        let total: Int64
    }
}
