// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// A FIFO of reliable or bulk messages to send. The bytes and entries are reused. A stream or message transport sends
// the head in fragments and removes it when sent; a datagram transport keeps each message until all its bytes are
// acknowledged, sends new fragments at a cursor and resends lost ranges first. A bulk queue numbers its transfers when
// they are queued and keeps a cancelled transfer as a placeholder until its cancel is sent in queue order.
final class GesNetworkSendQueue {
    // The most messages a datagram transport has started and still holds.
    static let maximumStarted = 8192

    private var bytes: [UInt8] = []
    private var head = 0
    private var tail = 0
    private var routes = [Int](repeating: 0, count: 8)
    private var starts = [Int](repeating: 0, count: 8)
    private var lengths = [Int](repeating: 0, count: 8)
    private var numbers = [Int](repeating: 0, count: 8)
    private var ids = [UInt16](repeating: 0, count: 8)
    private var acknowledged = [Int](repeating: 0, count: 8)
    private var done = [Bool](repeating: false, count: 8)
    private var cancelled = [Bool](repeating: false, count: 8)
    private var codes = [String](repeating: "", count: 8)
    private var first = 0
    private(set) var count = 0
    private var firstSerial: Int64 = 0

    // The send state of the head on a stream or message transport.
    var started = false
    var offset = 0
    var id: UInt16 = 0

    // On a datagram transport: the message, counted from the head, whose new fragments are sent next, whether it is
    // started, its offset, and the ranges of lost fragments to resend first.
    var cursor = 0
    var cursorStarted = false
    var cursorOffset = 0
    let lost = GesNetworkRanges()

    // Whether the ids were assigned when the messages were queued.
    let preassigned: Bool

    init(preassigned: Bool = false) {
        self.preassigned = preassigned
    }

    // Bytes of messages not yet completely sent, or on a datagram transport not yet acknowledged.
    var byteCount: Int64 { Int64(tail - head) }

    var currentRoute: Int { routes[first] }

    var currentLength: Int { lengths[first] }

    // Whether a datagram transport has new fragments to send.
    var hasNew: Bool { cursor < count && (cursorStarted || cursor < Self.maximumStarted) }

    private func slot(_ index: Int) -> Int { (first + index) & (routes.count - 1) }

    // Calls the body with the current message's bytes.
    func withCurrent<Result>(_ body: (UnsafeRawBufferPointer) -> Result) -> Result {
        withEntry(0, body)
    }

    // Calls the body with the bytes of the message the index counts from the head.
    func withEntry<Result>(_ index: Int, _ body: (UnsafeRawBufferPointer) -> Result) -> Result {
        let slot = slot(index)
        let start = starts[slot]
        let length = lengths[slot]
        return bytes.withUnsafeBytes { body(UnsafeRawBufferPointer(rebasing: $0[start..<start + length])) }
    }

    func entryLength(_ index: Int) -> Int { lengths[slot(index)] }

    func route(_ index: Int) -> Int { routes[slot(index)] }

    func entryId(_ index: Int) -> UInt16 { ids[slot(index)] }

    func setEntryId(_ index: Int, _ id: UInt16) { ids[slot(index)] = id }

    func serial(_ index: Int) -> Int64 { firstSerial + Int64(index) }

    func isCancelled(_ index: Int) -> Bool { cancelled[slot(index)] }

    func code(_ index: Int) -> String { codes[slot(index)] }

    // The acknowledged bytes of a held message on a datagram transport.
    func acknowledgedBytes(_ index: Int) -> Int { acknowledged[slot(index)] }

    // Turns a message into a cancelled placeholder; a datagram transport no longer sends or resends its fragments.
    func cancel(_ index: Int, _ code: String) {
        let slot = slot(index)
        cancelled[slot] = true
        done[slot] = true
        codes[slot] = code
    }

    // Finds the held, not cancelled transfer with the route and id.
    func findTransfer(_ route: Int, _ id: UInt16, _ index: inout Int) -> Bool {
        for candidate in 0..<count {
            let slot = slot(candidate)
            if routes[slot] == route && ids[slot] == id && !cancelled[slot] {
                index = candidate
                return true
            }
        }
        return false
    }

    // Adds a message; number is the topic state's number, or -1.
    func enqueue(_ route: Int, _ payload: UnsafeRawBufferPointer, _ number: Int = -1) {
        reserve(route, payload.count, number)
        if payload.count > 0 {
            let start = tail - payload.count
            bytes.withUnsafeMutableBytes { $0.baseAddress!.advanced(by: start).copyMemory(from: payload.baseAddress!, byteCount: payload.count) }
        }
    }

    // Calls the body with the bytes of the newest message to fill; shrink may shorten it before the next change.
    func withNewest<Result>(_ body: (UnsafeMutableRawBufferPointer) -> Result) -> Result {
        let slot = slot(count - 1)
        let start = starts[slot]
        let length = lengths[slot]
        return bytes.withUnsafeMutableBytes { body(UnsafeMutableRawBufferPointer(rebasing: $0[start..<start + length])) }
    }

    // Adds a message of the length whose bytes withNewest fills.
    func reserve(_ route: Int, _ length: Int, _ number: Int = -1, _ id: UInt16 = 0) {
        if count == routes.count { grow() }
        if bytes.count - tail < length {
            let used = tail - head
            let start = head
            if bytes.count - used >= length && head > 0 {
                // The ranges may overlap when the storage is kept.
                bytes.withUnsafeMutableBytes { $0.baseAddress!.copyMemory(from: $0.baseAddress! + start, byteCount: used) }
            } else {
                var grown = [UInt8](repeating: 0, count: max(bytes.count * 2, 256, used + length))
                if used > 0 {
                    bytes.withUnsafeBytes { source in
                        grown.withUnsafeMutableBytes { $0.baseAddress!.copyMemory(from: source.baseAddress! + start, byteCount: used) }
                    }
                }
                bytes = grown
            }
            for index in 0..<count { starts[slot(index)] -= start }
            head = 0
            tail = used
        }
        let slot = slot(count)
        routes[slot] = route
        starts[slot] = tail
        lengths[slot] = length
        numbers[slot] = number
        ids[slot] = id
        acknowledged[slot] = 0
        done[slot] = false
        cancelled[slot] = false
        codes[slot] = ""
        tail += length
        count += 1
    }

    // Shortens the newest message to the length.
    func shrink(_ length: Int) {
        let slot = slot(count - 1)
        tail -= lengths[slot] - length
        lengths[slot] = length
    }

    private func grow() {
        let size = routes.count * 2
        var grownRoutes = [Int](repeating: 0, count: size)
        var grownStarts = [Int](repeating: 0, count: size)
        var grownLengths = [Int](repeating: 0, count: size)
        var grownNumbers = [Int](repeating: 0, count: size)
        var grownIds = [UInt16](repeating: 0, count: size)
        var grownAcknowledged = [Int](repeating: 0, count: size)
        var grownDone = [Bool](repeating: false, count: size)
        var grownCancelled = [Bool](repeating: false, count: size)
        var grownCodes = [String](repeating: "", count: size)
        for index in 0..<count {
            let slot = slot(index)
            grownRoutes[index] = routes[slot]
            grownStarts[index] = starts[slot]
            grownLengths[index] = lengths[slot]
            grownNumbers[index] = numbers[slot]
            grownIds[index] = ids[slot]
            grownAcknowledged[index] = acknowledged[slot]
            grownDone[index] = done[slot]
            grownCancelled[index] = cancelled[slot]
            grownCodes[index] = codes[slot]
        }
        routes = grownRoutes
        starts = grownStarts
        lengths = grownLengths
        numbers = grownNumbers
        ids = grownIds
        acknowledged = grownAcknowledged
        done = grownDone
        cancelled = grownCancelled
        codes = grownCodes
        first = 0
    }

    // Removes the head message.
    func pop() {
        head += lengths[first]
        first = slot(1)
        count -= 1
        firstSerial += 1
        started = false
        offset = 0
        if cursor > 0 { cursor -= 1 }
        if count == 0 {
            head = 0
            tail = 0
        }
    }

    // Moves the datagram cursor past a message whose last new fragment is sent.
    func advance() {
        cursor += 1
        cursorStarted = false
        cursorOffset = 0
    }

    // Acknowledges a fragment of the message with the serial. Returns true when that completes the message, with its
    // route and topic state number.
    func acknowledge(_ serial: Int64, _ length: Int, _ route: inout Int, _ number: inout Int) -> Bool {
        route = -1
        number = -1
        let index = serial - firstSerial
        if index < 0 || index >= Int64(count) { return false }
        let slot = slot(Int(index))
        if done[slot] { return false }
        acknowledged[slot] += length
        if acknowledged[slot] != lengths[slot] { return false }
        done[slot] = true
        route = routes[slot]
        number = numbers[slot]
        return true
    }

    // Removes the acknowledged messages at the head.
    func popDone() {
        while count > 0 && done[first] { pop() }
    }

    // Queues a lost fragment of a held message to be resent.
    func lose(_ serial: Int64, _ offset: Int, _ length: Int) {
        let index = serial - firstSerial
        if index >= 0 && index < Int64(count) { lost.add(serial, offset, length) }
    }

    // Finds the held message with the serial; false when it is no longer held.
    func find(_ serial: Int64, _ index: inout Int) -> Bool {
        let relative = serial - firstSerial
        if relative < 0 || relative >= Int64(count) { return false }
        index = Int(relative)
        return true
    }

    func clear() {
        head = 0
        tail = 0
        first = 0
        count = 0
        started = false
        offset = 0
        cursor = 0
        cursorStarted = false
        cursorOffset = 0
        lost.clear()
    }
}

// A FIFO of byte ranges of messages to resend: the message key, the offset and the length.
final class GesNetworkRanges {
    private var messages = [Int64](repeating: 0, count: 8)
    private var offsets = [Int](repeating: 0, count: 8)
    private var lengths = [Int](repeating: 0, count: 8)
    private var first = 0
    private(set) var count = 0

    var message: Int64 { messages[first] }

    var offset: Int { offsets[first] }

    var length: Int { lengths[first] }

    func add(_ message: Int64, _ offset: Int, _ length: Int) {
        if count == messages.count {
            let size = messages.count * 2
            var grownMessages = [Int64](repeating: 0, count: size)
            var grownOffsets = [Int](repeating: 0, count: size)
            var grownLengths = [Int](repeating: 0, count: size)
            for index in 0..<count {
                let slot = (first + index) & (messages.count - 1)
                grownMessages[index] = messages[slot]
                grownOffsets[index] = offsets[slot]
                grownLengths[index] = lengths[slot]
            }
            messages = grownMessages
            offsets = grownOffsets
            lengths = grownLengths
            first = 0
        }
        let target = (first + count) & (messages.count - 1)
        messages[target] = message
        offsets[target] = offset
        lengths[target] = length
        count += 1
    }

    // Removes the first bytes of the head range, and the range once none remain; a range of length 0 is removed.
    func consume(_ length: Int) {
        offsets[first] += length
        lengths[first] -= length
        if lengths[first] > 0 { return }
        first = (first + 1) & (messages.count - 1)
        count -= 1
    }

    func clear() {
        first = 0
        count = 0
    }
}

// The message of a latest route being sent and the newer one waiting in its place. On a datagram transport the
// current message stays after it is sent once, so its lost ranges can be resent until a newer message replaces it.
final class GesNetworkLatestSlot {
    private let datagram: Bool
    private var current: [UInt8] = []
    private var waiting: [UInt8] = []

    var currentLength = 0
    var hasCurrent = false
    var started = false
    var offset = 0
    var id: UInt16 = 0
    var waitingLength = 0
    var hasWaiting = false
    var nextId: UInt16 = 0

    // On a datagram transport: whether every fragment of the current message is sent once, and its lost ranges.
    var sent = false
    let lost = GesNetworkRanges()

    init(datagram: Bool) {
        self.datagram = datagram
    }

    // Whether the current message has fragments to send, new or lost.
    var hasPending: Bool { (hasCurrent && !sent) || lost.count > 0 }

    // Calls the body with the current message's bytes.
    func withCurrent<Result>(_ body: (UnsafeRawBufferPointer) -> Result) -> Result {
        let length = currentLength
        return current.withUnsafeBytes { body(UnsafeRawBufferPointer(rebasing: $0[0..<length])) }
    }

    func set(_ payload: UnsafeRawBufferPointer) {
        if hasCurrent && started && !sent {
            Self.copy(payload, into: &waiting)
            waitingLength = payload.count
            hasWaiting = true
            lost.clear()
            return
        }
        Self.copy(payload, into: &current)
        currentLength = payload.count
        hasCurrent = true
        started = false
        sent = false
        offset = 0
        lost.clear()
    }

    // Ends the current message once every fragment is sent and moves the waiting one in its place. On a datagram
    // transport a message without a waiting successor stays current for resends.
    func complete() {
        started = false
        offset = 0
        if hasWaiting {
            swap(&current, &waiting)
            currentLength = waitingLength
            hasWaiting = false
            lost.clear()
            return
        }
        if datagram {
            started = true
            sent = true
            return
        }
        hasCurrent = false
    }

    // Queues a lost fragment for resending while its message is still current and no newer one waits.
    func lose(_ id: UInt16, _ offset: Int, _ length: Int) {
        if hasCurrent && started && self.id == id && !hasWaiting { lost.add(Int64(id), offset, length) }
    }

    func clear() {
        hasCurrent = false
        hasWaiting = false
        started = false
        sent = false
        offset = 0
        lost.clear()
    }

    // Drops the messages not yet started; a started one is completed.
    func dropUnstarted() {
        hasWaiting = false
        if !started { hasCurrent = false }
    }

    private static func copy(_ payload: UnsafeRawBufferPointer, into target: inout [UInt8]) {
        if target.count < payload.count { target = [UInt8](repeating: 0, count: max(payload.count, target.count * 2)) }
        if payload.count > 0 {
            target.withUnsafeMutableBytes { $0.baseAddress!.copyMemory(from: payload.baseAddress!, byteCount: payload.count) }
        }
    }
}

// A message being received in fragments.
final class GesNetworkReassembly {
    private var buffer: [UInt8] = []

    var active = false
    var channel: UInt32 = 0
    var id: UInt16 = 0
    var total: UInt32 = 0
    var received = 0
    var nextId: UInt16 = 0

    // Calls the body with the received bytes.
    func withMessage<Result>(_ body: (UnsafeRawBufferPointer) -> Result) -> Result {
        let length = received
        return buffer.withUnsafeBytes { body(UnsafeRawBufferPointer(rebasing: $0[0..<length])) }
    }

    func start(_ channel: UInt32, _ id: UInt16, _ total: UInt32) {
        active = true
        self.channel = channel
        self.id = id
        self.total = total
        received = 0
    }

    func append(_ chunk: UnsafeRawBufferPointer) {
        let needed = received + chunk.count
        if buffer.count < needed {
            var grown = [UInt8](repeating: 0, count: min(Int(total), max(needed, buffer.count * 2)))
            let length = received
            buffer.withUnsafeBytes { source in
                grown.withUnsafeMutableBytes { target in
                    if length > 0 { target.baseAddress!.copyMemory(from: source.baseAddress!, byteCount: length) }
                }
            }
            buffer = grown
        }
        let start = received
        if chunk.count > 0 {
            buffer.withUnsafeMutableBytes { $0.baseAddress!.advanced(by: start).copyMemory(from: chunk.baseAddress!, byteCount: chunk.count) }
        }
        received = needed
    }
}

// A message received in fragments in any order on a datagram transport: its bytes and which of them arrived. A latest
// route also keeps the id of the message it reported last.
final class GesNetworkPartial {
    private var buffer: [UInt8] = []
    private var arrived: [UInt8] = []

    var active = false
    var channel: UInt32 = 0
    var id: UInt16 = 0
    var total: UInt32 = 0
    var received: Int64 = 0
    var reported: UInt16 = 0
    var hasReported = false

    // Whether the sender cancelled the bulk message before it was reported.
    var cancelled = false

    // Calls the body with the message's bytes.
    func withMessage<Result>(_ body: (UnsafeRawBufferPointer) -> Result) -> Result {
        let length = Int(total)
        return buffer.withUnsafeBytes { body(UnsafeRawBufferPointer(rebasing: $0[0..<length])) }
    }

    // The bytes the message's storage holds.
    var capacity: Int64 { Int64(buffer.count + arrived.count) }

    // Starts a message; its storage grows to exactly the total, which the caller counted as pending before.
    func start(_ channel: UInt32, _ id: UInt16, _ total: UInt32) {
        if buffer.count < Int(total) { buffer = [UInt8](repeating: 0, count: Int(total)) }
        let bits = Int((total + 7) / 8)
        if arrived.count < bits {
            arrived = [UInt8](repeating: 0, count: bits)
        } else if bits > 0 {
            arrived.withUnsafeMutableBytes { _ = $0.baseAddress!.initializeMemory(as: UInt8.self, repeating: 0, count: bits) }
        }
        active = true
        cancelled = false
        self.channel = channel
        self.id = id
        self.total = total
        received = 0
    }

    // Marks the message with the id cancelled and frees its storage; its fragments are ignored and it is skipped in
    // order.
    func cancel(_ channel: UInt32, _ id: UInt16) {
        freeStorage()
        active = true
        cancelled = true
        self.channel = channel
        self.id = id
        total = 0
        received = 0
    }

    func freeStorage() {
        buffer = []
        arrived = []
    }

    // Writes the bytes of a chunk that did not arrive before; bytes that did keep their first value. Runs of eight
    // bytes that share a bitmap byte are copied or skipped together.
    func write(_ offset: Int, _ chunk: UnsafeRawBufferPointer) {
        var added: Int64 = 0
        buffer.withUnsafeMutableBytes { buffer in
            arrived.withUnsafeMutableBytes { arrived in
                let end = offset + chunk.count
                var position = offset
                while position < end {
                    if position & 7 == 0 && end - position >= 8 {
                        var run = position
                        while end - run >= 8 && arrived[run >> 3] == 0 {
                            arrived[run >> 3] = 0xff
                            run += 8
                        }
                        if run != position {
                            UnsafeMutableRawBufferPointer(rebasing: buffer[position..<run]).copyMemory(
                                from: UnsafeRawBufferPointer(rebasing: chunk[position - offset..<run - offset])
                            )
                            added += Int64(run - position)
                            position = run
                            continue
                        }
                        if arrived[position >> 3] == 0xff {
                            position += 8
                            continue
                        }
                    }
                    let mask = UInt8(1) << UInt8(position & 7)
                    let bits = arrived[position >> 3]
                    if bits & mask == 0 {
                        arrived[position >> 3] = bits | mask
                        buffer[position] = chunk[position - offset]
                        added += 1
                    }
                    position += 1
                }
            }
        }
        received += added
    }
}

// The reliable or bulk messages being received on a datagram transport: the window of ids from the next to report.
final class GesNetworkOrderedReceive {
    static let window = 8192

    private var messages = [GesNetworkPartial?](repeating: nil, count: GesNetworkOrderedReceive.window)

    // Delivered messages wait here for reuse, so a warmed session receives without allocating. The storage they keep
    // stays within the retention limit: a message whose storage would exceed it is kept without.
    private var free: [GesNetworkPartial] = []
    private let retention: Int64
    // The storage the messages kept for reuse hold.
    private(set) var retained: Int64 = 0

    var nextId: UInt16 = 0

    init(retention: Int64) {
        self.retention = retention
    }

    func message(_ id: UInt16) -> GesNetworkPartial {
        let slot = Int(id) % Self.window
        if let message = messages[slot] { return message }
        let message: GesNetworkPartial
        if let reused = free.popLast() {
            retained -= reused.capacity
            message = reused
        } else {
            message = GesNetworkPartial()
        }
        messages[slot] = message
        return message
    }

    // Whether a message with the id is being received or was cancelled.
    func holds(_ id: UInt16) -> Bool { messages[Int(id) % Self.window]?.active ?? false }

    // Frees the slot of a delivered message and keeps the message for reuse.
    func release(_ message: GesNetworkPartial) {
        message.active = false
        messages[Int(message.id) % Self.window] = nil
        if retained + message.capacity > retention { message.freeStorage() }
        retained += message.capacity
        free.append(message)
    }

    // The next message to report when it is complete or cancelled; nil otherwise.
    var ready: GesNetworkPartial? {
        guard let message = messages[Int(nextId) % Self.window], message.active, message.cancelled || message.received == Int64(message.total) else {
            return nil
        }
        return message
    }
}
