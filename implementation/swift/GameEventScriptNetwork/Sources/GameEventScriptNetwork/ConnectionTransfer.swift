// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

private typealias Wire = GesNetworkWire

// Bulk transfers: a message on a bulk route carries a header with its compression, uncompressed size and SHA-256 hash.
// Either side may cancel a transfer with a control frame on channel 0 without closing the session.
extension GesNetworkConnection {
    static let maximumCancels = 16384
    static let maximumWrittenTransfers = 8192

    // The pending limit of bulk bytes: a transfer being received and a delivered one.
    var maxPendingBulk: Int64 { 2 * (maxTransfer + Int64(Wire.transferHeaderBytes)) }

    // The largest data of a transfer on a bulk route: the route's root limit and the transfer limit.
    func transferLimit(_ route: Int) -> Int64 { min(maxBytes[route], maxTransfer) }

    /// Cancels a transfer this side sends, as long as not all of its bytes were sent once. The peer is told with a
    /// cancel frame, also when the transfer was not started, and the connection reports
    /// ``GesNetworkEventKind/transferCancelled``; the session continues.
    /// - Parameters:
    ///   - route: The route index of a bulk `sends` route of this side.
    ///   - transferId: The id ``lastTransferId`` gave the transfer.
    ///   - code: The code the peer receives, 1 to 64 characters from `!` to `~`; `network.transferCancelled` when nil.
    /// - Returns: True when the transfer was cancelled; false when the connection is not connected or the transfer is
    ///   unknown or sent completely.
    /// - Throws: `GesApiError.invalidArgument` when the route is no bulk route this side sends or the code is invalid.
    @discardableResult
    public func cancel(route: Int, transferId: Int, code: String? = nil) throws -> Bool {
        if route < 0 || route >= routeCount || !localSends[route] || classes[route] != .bulk {
            throw GesApiError.invalidArgument("The route is no bulk route this side sends.")
        }
        let code = code ?? GesNetworkCodes.transferCancelled
        if !Self.isValidCode(code) { throw GesApiError.invalidArgument("A code holds 1 to 64 characters from '!' to '~'.") }
        if state != .connected || transferId < 0 || transferId > Int(UInt16.max) { return false }
        var index = 0
        if !sendBulk.findTransfer(route, UInt16(transferId), &index) || (datagram && index < sendBulk.cursor) { return false }
        cancelAt(route, UInt16(transferId), index, code)
        return true
    }

    // Cancels the queued transfer at the index of the send queue: the caller checked the connection, the id and the code.
    private func cancelAt(_ route: Int, _ transferId: UInt16, _ index: Int, _ code: String) {
        // On a stream the started transfer is the head; its written bytes are no longer reported. The stream writes the
        // cancel frame when the placeholder reaches the head; a datagram transport sends it at once.
        if !datagram && index == 0 && sendBulk.started { written.cancelNewest() }
        cancelQueued(index, code)
        if datagram && !queueCancel(route, transferId, code) { return }
        if !addEvent(.transferCancelled, route, UnsafeRawBufferPointer(start: nil, count: 0), .none, code, "", false, Int(transferId)) { fail(GesNetworkCodes.queueFull) }
    }

    // Turns a queued transfer into a cancelled placeholder; a datagram transport moves its cursor past it and removes it
    // once it reaches the head.
    func cancelQueued(_ index: Int, _ code: String) {
        sendBulk.cancel(index, code)
        if !datagram { return }
        if index == sendBulk.cursor { sendBulk.advance() }
        sendBulk.popDone()
    }

    private static func isValidCode(_ code: String) -> Bool {
        let scalars = code.unicodeScalars
        if scalars.isEmpty || scalars.count > Wire.maximumCodeBytes { return false }
        return scalars.allSatisfy { $0.value >= 0x21 && $0.value <= 0x7E }
    }

    // Whether a transfer of this length on the bulk route exceeds a limit of this connected connection; a server end asks
    // every client before it sends to any of them.
    func rejectsTransfer(_ route: Int, _ length: Int) -> Bool {
        state == .connected && Int64(length) > min(transferLimit(route), peerTransferLimit)
    }

    // Queues a transfer: the header and the data, compressed when that is shorter and both sides allow it.
    func sendTransfer(_ route: Int, _ data: UnsafeRawBufferPointer) -> GesNetworkSendStatus {
        let limit = min(transferLimit(route), peerTransferLimit)
        if Int64(data.count) > limit { return .messageTooLarge }
        if sendBulk.count >= GesNetworkSendQueue.maximumStarted || sendBulk.byteCount + Int64(data.count) > min(maxTransfer, peerTransferLimit) {
            return .queueFull
        }
        // On a datagram transport the receiver resolves ids in order; a cancel it has not confirmed holds its id open, so
        // the new id stays within the receive window of the oldest open one.
        if datagram && cancels.count > 0 && cancels.ownSpan(nextBulkId, localSends) >= GesNetworkSendQueue.maximumStarted { return .queueFull }
        queueTransfer(route, data)
        return .queued
    }

    // Passes a transfer a client end kept before the handshake. The end bounded its kept transfers by this side's
    // limits, so the queue bounds do not apply; a transfer larger than the peer's transfer limit keeps its id and is
    // cancelled at once with network.transferTooLarge.
    func passTransfer(_ route: Int, _ data: UnsafeRawBufferPointer) {
        if state != .connected { return }
        if Int64(data.count) <= peerTransferLimit {
            queueTransfer(route, data)
            return
        }
        // The placeholder is the newest entry and not yet sent, so it is found behind the datagram cursor.
        queueTransfer(route, UnsafeRawBufferPointer(start: nil, count: 0))
        var index = 0
        _ = sendBulk.findTransfer(route, UInt16(lastTransferId), &index)
        cancelAt(route, UInt16(lastTransferId), index, GesNetworkCodes.transferTooLarge)
    }

    private func queueTransfer(_ route: Int, _ data: UnsafeRawBufferPointer) {
        let id = nextBulkId
        nextBulkId &+= 1
        let header = 1 + Wire.varintSize64(UInt64(data.count)) + Wire.hashBytes
        sendBulk.reserve(route, header + data.count, -1, id)
        var length = -1
        if deflate && peerDeflate && data.count > 0 {
            if deflater == nil { deflater = GesNetworkDeflater() }
            let deflater = deflater!
            length = sendBulk.withNewest { message in
                deflater.compress(data, into: UnsafeMutableRawBufferPointer(rebasing: message[header..<header + data.count - 1]))
            }
        }

        if length >= 0 {
            sendBulk.withNewest { $0[0] = Wire.compressionDeflate }
            sendBulk.shrink(header + length)
        } else {
            sendBulk.withNewest { message in
                message[0] = Wire.compressionNone
                if data.count > 0 { (message.baseAddress! + header).copyMemory(from: data.baseAddress!, byteCount: data.count) }
            }
        }

        sendBulk.withNewest { message in
            let position = Wire.writeVarint64(message, 1, UInt64(data.count))
            hash(data, UnsafeMutableRawBufferPointer(rebasing: message[position..<position + Wire.hashBytes]))
        }
        peerCancelled[Int(id >> 6)] &= ~(UInt64(1) << UInt64(id & 63))
        lastTransferId = Int(id)
    }

    private func hash(_ data: UnsafeRawBufferPointer, _ target: UnsafeMutableRawBufferPointer) {
        if sha256 == nil { sha256 = GesNetworkSha256() }
        let sha256 = sha256!
        sha256.reset()
        sha256.update(data)
        sha256.finish(into: target)
    }

    func dropTransfers() {
        cancels.clear()
        written.clear()
    }

    // Queues a cancel frame for the peer; false when it closed the connection because too many are pending.
    func queueCancel(_ route: Int, _ id: UInt16, _ code: String) -> Bool {
        if cancels.count >= Self.maximumCancels { return fail(GesNetworkCodes.queueFull) }
        cancels.add(route, id, code)
        return true
    }

    // The bytes of a cancel frame.
    static func cancelFrameSize(_ route: Int, _ code: String) -> Int {
        let length = code.utf8.count
        return Wire.varintSize(0) + 1 + Wire.varintSize(UInt32(route + 1)) + 2 + Wire.varintSize(UInt32(length)) + length
    }

    static func writeCancelFrame(_ packet: UnsafeMutableRawBufferPointer, _ position: Int, _ route: Int, _ id: UInt16, _ code: String) -> Int {
        var position = Wire.writeVarint(packet, position, 0)
        packet[position] = Wire.controlCancel
        position = Wire.writeVarint(packet, position + 1, UInt32(route + 1))
        Wire.writeUInt16(packet, position, id)
        position = Wire.writeVarint(packet, position + 2, UInt32(code.utf8.count))
        for byte in code.utf8 {
            packet[position] = byte
            position += 1
        }
        return position
    }

    // Writes the pending cancel frames of a stream or message transport; they need no confirmation.
    func writeCancels(_ packet: UnsafeMutableRawBufferPointer, _ position: Int, _ limit: Int) -> Int {
        var position = position
        while cancels.pendingCount > 0 {
            let serial = cancels.firstPending
            let route = cancels.route(serial)
            let code = cancels.code(serial)
            if limit - position < Self.cancelFrameSize(route, code) { break }
            position = Self.writeCancelFrame(packet, position, route, cancels.id(serial), code)
            cancels.markSent(serial)
            cancels.confirm(serial)
        }
        return position
    }

    // Reads a cancel frame after its kind byte; false when it closed the connection.
    func receiveCancel(_ packet: UnsafeRawBufferPointer, _ position: inout Int) -> Bool {
        var channel: UInt32 = 0
        if Wire.readVarint(packet, &position, &channel) != 1 || channel == 0 || Int(channel) > routeCount || packet.count - position < 2 {
            return fail(GesNetworkCodes.malformed)
        }
        let route = Int(channel) - 1
        let id = Wire.readUInt16(packet, position)
        position += 2
        var codeLength: UInt32 = 0
        if Wire.readVarint(packet, &position, &codeLength) != 1 || codeLength < 1 || codeLength > UInt32(Wire.maximumCodeBytes) || packet.count - position < Int(codeLength) {
            return fail(GesNetworkCodes.malformed)
        }
        let codeBytes = UnsafeRawBufferPointer(rebasing: packet[position..<position + Int(codeLength)])
        position += codeBytes.count
        for byte in codeBytes where byte < 0x21 || byte > 0x7E { return fail(GesNetworkCodes.malformed) }

        if classes[route] != .bulk { return fail(GesNetworkCodes.malformed) }
        if localSends[route] {
            // The receiver refused a transfer of ours: stop sending it and report it once.
            var index = 0
            if sendBulk.findTransfer(route, id, &index) { cancelQueued(index, "") }
            if isPeerCancelled(id) { return true }
            peerCancelled[Int(id >> 6)] |= UInt64(1) << UInt64(id & 63)
            return addCancelled(route, id, codeBytes)
        }

        if datagram {
            let ordered = orderedBulk!
            if Int(id &- ordered.nextId) >= GesNetworkOrderedReceive.window {
                return Int(ordered.nextId &- id) <= GesNetworkOrderedReceive.window || fail(GesNetworkCodes.malformed)
            }
            let message = ordered.message(id)
            if message.active && message.id == id {
                if message.cancelled { return true }
                pendingBulk -= Int64(message.total)
            }
            message.cancel(channel, id)
            return addCancelled(route, id, codeBytes) && deliverOrdered(ordered)
        }

        let assembly = receiveBulk
        if assembly.active && assembly.id == id && assembly.channel == channel {
            assembly.active = false
            pendingBulk -= Int64(assembly.received)
        } else if !assembly.active && id == assembly.nextId {
            assembly.nextId &+= 1
        } else {
            return fail(GesNetworkCodes.malformed)
        }
        return addCancelled(route, id, codeBytes)
    }

    func isPeerCancelled(_ id: UInt16) -> Bool { peerCancelled[Int(id >> 6)] & (UInt64(1) << UInt64(id & 63)) != 0 }

    private func addCancelled(_ route: Int, _ id: UInt16, _ code: UnsafeRawBufferPointer) -> Bool {
        addEvent(.transferCancelled, route, UnsafeRawBufferPointer(start: nil, count: 0), .none, String(decoding: code, as: UTF8.self), "", true, Int(id))
            || fail(GesNetworkCodes.queueFull)
    }

    // Checks a received transfer and reports its data as a message; a transfer that fails a check is cancelled. False
    // when it closed the connection.
    func deliverTransfer(_ route: Int, _ id: UInt16, _ message: UnsafeRawBufferPointer) -> Bool {
        var position = 1
        let code = GesNetworkCodes.transferCorrupt
        var size: UInt64 = 0
        if message.isEmpty || Wire.readVarint64(message, &position, &size) != 1 { return refuseTransfer(route, id, code) }
        let compression = message[0]
        if size > UInt64(transferLimit(route)) { return refuseTransfer(route, id, GesNetworkCodes.transferTooLarge) }
        if compression > Wire.compressionDeflate || (compression == Wire.compressionDeflate && !deflate) { return refuseTransfer(route, id, code) }
        if message.count - position < Wire.hashBytes { return refuseTransfer(route, id, code) }
        let expected = UnsafeRawBufferPointer(rebasing: message[position..<position + Wire.hashBytes])
        let data = UnsafeRawBufferPointer(rebasing: message[(position + Wire.hashBytes)...])
        if compression == Wire.compressionNone ? UInt64(data.count) != size : UInt64(data.count) >= size { return refuseTransfer(route, id, code) }

        let length = Int(size)
        guard let target = reserveEvent(.message, route, length, .bulk, "", "", false, Int(id), 0, 0) else { return fail(GesNetworkCodes.queueFull) }
        if compression == Wire.compressionNone {
            if length > 0 { target.baseAddress!.copyMemory(from: data.baseAddress!, byteCount: length) }
        } else {
            if inflater == nil { inflater = GesNetworkInflater() }
            if !inflater!.inflate(data, into: target) { return dropEvent(length) && refuseTransfer(route, id, code) }
        }

        hash(UnsafeRawBufferPointer(target), hash)
        return expected.elementsEqual(UnsafeRawBufferPointer(hash)) || (dropEvent(length) && refuseTransfer(route, id, code))
    }

    // Removes the newest event, a delivered transfer whose data failed a check.
    private func dropEvent(_ length: Int) -> Bool {
        events.removeLast()
        arenaLength -= length
        pendingBulk -= Int64(length)
        return true
    }

    // Cancels a received transfer that failed a check and tells the sender.
    private func refuseTransfer(_ route: Int, _ id: UInt16, _ code: String) -> Bool {
        if !addEvent(.transferCancelled, route, UnsafeRawBufferPointer(start: nil, count: 0), .none, code, "", false, Int(id)) { return fail(GesNetworkCodes.queueFull) }
        return queueCancel(route, id, code)
    }

    // ---- Stream sender progress ----

    // Notes the first written bytes of a transfer, which start at the written count before them.
    func noteTransferStarted(_ route: Int, _ id: UInt16, _ length: Int) {
        if written.count == Self.maximumWrittenTransfers { written.pop() }
        written.add(route, id, bulkWritten, Int64(length))
    }

    // Reports the progress of the written transfers the peer confirmed up to the written count of the packet.
    func confirmWritten(_ index: Int64) {
        if sentCount - index > Int64(Self.sendTimeWindow) { return }
        let confirmed = bulkMarks[Int(index % Int64(Self.sendTimeWindow))]
        while written.count > 0 {
            if written.cancelled || isPeerCancelled(written.id) {
                written.pop()
                continue
            }
            let bytes = min(confirmed - written.start, written.total)
            if bytes <= written.reported { return }
            if !addProgress(written.route, written.id, bytes, written.total) { return }
            written.reported = bytes
            if bytes < written.total { return }
            written.pop()
        }
    }
}

// The cancel frames a connection sends, in order. Each has a serial; a frame is pending, sent and unconfirmed, or
// confirmed. Confirmed frames leave from the head.
final class GesNetworkCancels {
    private static let pending: UInt8 = 0
    private static let sent: UInt8 = 1
    private static let confirmed: UInt8 = 2

    private var routes = [Int](repeating: 0, count: 8)
    private var ids = [UInt16](repeating: 0, count: 8)
    private var codes = [String](repeating: "", count: 8)
    private var states = [UInt8](repeating: 0, count: 8)
    private var first = 0
    private var firstSerial: Int64 = 0
    private var pendingSerial: Int64 = 0

    private(set) var count = 0
    private(set) var pendingCount = 0

    // The serial of the first pending frame; valid while one is pending.
    var firstPending: Int64 {
        while states[slot(pendingSerial)] != Self.pending { pendingSerial += 1 }
        return pendingSerial
    }

    private func slot(_ serial: Int64) -> Int { (first + Int(serial - firstSerial)) & (routes.count - 1) }

    private func holds(_ serial: Int64) -> Bool { serial >= firstSerial && serial < firstSerial + Int64(count) }

    func route(_ serial: Int64) -> Int { routes[slot(serial)] }

    func id(_ serial: Int64) -> UInt16 { ids[slot(serial)] }

    func code(_ serial: Int64) -> String { codes[slot(serial)] }

    func add(_ route: Int, _ id: UInt16, _ code: String) {
        if count == routes.count { grow() }
        let slot = (first + count) & (routes.count - 1)
        routes[slot] = route
        ids[slot] = id
        codes[slot] = code
        states[slot] = Self.pending
        if pendingCount == 0 { pendingSerial = firstSerial + Int64(count) }
        count += 1
        pendingCount += 1
    }

    func markSent(_ serial: Int64) {
        states[slot(serial)] = Self.sent
        pendingCount -= 1
    }

    // Confirms a sent frame and removes the confirmed frames at the head.
    func confirm(_ serial: Int64) {
        if !holds(serial) || states[slot(serial)] != Self.sent { return }
        states[slot(serial)] = Self.confirmed
        while count > 0 && states[first] == Self.confirmed {
            first = (first + 1) & (routes.count - 1)
            firstSerial += 1
            count -= 1
        }
        if pendingSerial < firstSerial { pendingSerial = firstSerial }
    }

    // Makes a sent frame whose packet was lost pending again.
    func lose(_ serial: Int64) {
        if !holds(serial) || states[slot(serial)] != Self.sent { return }
        states[slot(serial)] = Self.pending
        if pendingCount == 0 || serial < pendingSerial { pendingSerial = serial }
        pendingCount += 1
    }

    // The largest distance from the next transfer id back to an unconfirmed cancel of a transfer of this side; 0 when
    // there is none.
    func ownSpan(_ next: UInt16, _ localSends: [Bool]) -> Int {
        var span = 0
        for index in 0..<count {
            let slot = (first + index) & (routes.count - 1)
            if states[slot] != Self.confirmed && localSends[routes[slot]] { span = max(span, Int(next &- ids[slot])) }
        }
        return span
    }

    private func grow() {
        let size = routes.count * 2
        var grownRoutes = [Int](repeating: 0, count: size)
        var grownIds = [UInt16](repeating: 0, count: size)
        var grownCodes = [String](repeating: "", count: size)
        var grownStates = [UInt8](repeating: 0, count: size)
        for index in 0..<count {
            let slot = (first + index) & (routes.count - 1)
            grownRoutes[index] = routes[slot]
            grownIds[index] = ids[slot]
            grownCodes[index] = codes[slot]
            grownStates[index] = states[slot]
        }
        routes = grownRoutes
        ids = grownIds
        codes = grownCodes
        states = grownStates
        first = 0
    }

    func clear() {
        firstSerial += Int64(count)
        pendingSerial = firstSerial
        first = 0
        count = 0
        pendingCount = 0
    }
}

// The transfers a stream connection wrote, oldest first, with the written count before their first byte and the
// confirmed bytes already reported.
final class GesNetworkWrittenTransfers {
    private var routes = [Int](repeating: 0, count: 8)
    private var ids = [UInt16](repeating: 0, count: 8)
    private var starts = [Int64](repeating: 0, count: 8)
    private var totals = [Int64](repeating: 0, count: 8)
    private var reports = [Int64](repeating: 0, count: 8)
    private var cancels = [Bool](repeating: false, count: 8)
    private var first = 0

    private(set) var count = 0

    var route: Int { routes[first] }

    var id: UInt16 { ids[first] }

    var start: Int64 { starts[first] }

    var total: Int64 { totals[first] }

    var cancelled: Bool { cancels[first] }

    var reported: Int64 {
        get { reports[first] }
        set { reports[first] = newValue }
    }

    func add(_ route: Int, _ id: UInt16, _ start: Int64, _ total: Int64) {
        if count == routes.count { grow() }
        let slot = (first + count) & (routes.count - 1)
        routes[slot] = route
        ids[slot] = id
        starts[slot] = start
        totals[slot] = total
        reports[slot] = 0
        cancels[slot] = false
        count += 1
    }

    // Marks the newest transfer cancelled: it is no longer reported.
    func cancelNewest() { cancels[(first + count - 1) & (routes.count - 1)] = true }

    func pop() {
        first = (first + 1) & (routes.count - 1)
        count -= 1
    }

    private func grow() {
        let size = routes.count * 2
        var grownRoutes = [Int](repeating: 0, count: size)
        var grownIds = [UInt16](repeating: 0, count: size)
        var grownStarts = [Int64](repeating: 0, count: size)
        var grownTotals = [Int64](repeating: 0, count: size)
        var grownReports = [Int64](repeating: 0, count: size)
        var grownCancels = [Bool](repeating: false, count: size)
        for index in 0..<count {
            let slot = (first + index) & (routes.count - 1)
            grownRoutes[index] = routes[slot]
            grownIds[index] = ids[slot]
            grownStarts[index] = starts[slot]
            grownTotals[index] = totals[slot]
            grownReports[index] = reports[slot]
            grownCancels[index] = cancels[slot]
        }
        routes = grownRoutes
        ids = grownIds
        starts = grownStarts
        totals = grownTotals
        reports = grownReports
        cancels = grownCancels
        first = 0
    }

    func clear() {
        first = 0
        count = 0
    }
}
