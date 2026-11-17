// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// A FIFO of reliable or bulk messages to send. The bytes and entries are reused; the current message is
// the head and is sent in fragments.
final class GesNetworkSendQueue {
    private var bytes: [UInt8] = []
    private var head = 0
    private var tail = 0
    private var routes = [Int](repeating: 0, count: 8)
    private var lengths = [Int](repeating: 0, count: 8)
    private var first = 0
    private(set) var count = 0

    var started = false
    var offset = 0
    var id: UInt16 = 0

    // Bytes of messages not yet completely sent.
    var byteCount: Int64 { Int64(tail - head) }

    var currentRoute: Int { routes[first] }

    var currentLength: Int { lengths[first] }

    // Calls the body with the current message's bytes.
    func withCurrent<Result>(_ body: (UnsafeRawBufferPointer) -> Result) -> Result {
        let start = head
        let length = lengths[first]
        return bytes.withUnsafeBytes { body(UnsafeRawBufferPointer(rebasing: $0[start..<start + length])) }
    }

    func enqueue(_ route: Int, _ payload: UnsafeRawBufferPointer) {
        if count == routes.count {
            var grownRoutes = [Int](repeating: 0, count: routes.count * 2)
            var grownLengths = [Int](repeating: 0, count: routes.count * 2)
            for index in 0..<count {
                grownRoutes[index] = routes[(first + index) % routes.count]
                grownLengths[index] = lengths[(first + index) % routes.count]
            }
            routes = grownRoutes
            lengths = grownLengths
            first = 0
        }
        if bytes.count - tail < payload.count {
            let used = tail - head
            if bytes.count - used >= payload.count && head > 0 {
                let start = head
                bytes.withUnsafeMutableBytes { buffer in
                    buffer.baseAddress!.copyMemory(from: buffer.baseAddress! + start, byteCount: used)
                }
            } else {
                var grown = [UInt8](repeating: 0, count: max(bytes.count * 2, 256, used + payload.count))
                let start = head
                bytes.withUnsafeBytes { source in
                    grown.withUnsafeMutableBytes { target in
                        if used > 0 { target.baseAddress!.copyMemory(from: source.baseAddress! + start, byteCount: used) }
                    }
                }
                bytes = grown
            }
            head = 0
            tail = used
        }
        let start = tail
        if payload.count > 0 {
            bytes.withUnsafeMutableBytes { $0.baseAddress!.advanced(by: start).copyMemory(from: payload.baseAddress!, byteCount: payload.count) }
        }
        tail += payload.count
        let slot = (first + count) % routes.count
        routes[slot] = route
        lengths[slot] = payload.count
        count += 1
    }

    // Removes the completely sent head message.
    func pop() {
        head += lengths[first]
        first = (first + 1) % routes.count
        count -= 1
        started = false
        offset = 0
        if count == 0 {
            head = 0
            tail = 0
        }
    }

    func clear() {
        head = 0
        tail = 0
        first = 0
        count = 0
        started = false
        offset = 0
    }
}

// The message of a latest route being sent and the newer one waiting in its place.
final class GesNetworkLatestSlot {
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

    // Calls the body with the current message's bytes.
    func withCurrent<Result>(_ body: (UnsafeRawBufferPointer) -> Result) -> Result {
        let length = currentLength
        return current.withUnsafeBytes { body(UnsafeRawBufferPointer(rebasing: $0[0..<length])) }
    }

    func set(_ payload: UnsafeRawBufferPointer) {
        if hasCurrent && started {
            Self.copy(payload, into: &waiting)
            waitingLength = payload.count
            hasWaiting = true
            return
        }
        Self.copy(payload, into: &current)
        currentLength = payload.count
        hasCurrent = true
    }

    // Ends the completed current message and moves the waiting one in its place.
    func complete() {
        hasCurrent = false
        started = false
        offset = 0
        if !hasWaiting { return }
        swap(&current, &waiting)
        currentLength = waitingLength
        hasCurrent = true
        hasWaiting = false
    }

    func clear() {
        hasCurrent = false
        hasWaiting = false
        started = false
        offset = 0
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
