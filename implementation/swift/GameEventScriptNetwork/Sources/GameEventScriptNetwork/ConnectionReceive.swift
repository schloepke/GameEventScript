// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

private typealias Wire = GesNetworkWire

extension GesNetworkConnection {
    /// Passes received bytes: one packet on a message transport, any chunk of the byte stream on a stream transport.
    ///
    /// The connection first advances to `now`. Errors close the connection with their code; once closed, received
    /// bytes are ignored.
    /// - Parameters:
    ///   - data: The received bytes; they are not retained.
    ///   - now: The current time in microseconds, not earlier than `now`.
    /// - Throws: `GesApiError.invalidArgument` when the time goes back.
    public func receive(_ data: UnsafeRawBufferPointer, now: Int64) throws {
        try advance(now)
        if state == .closed { return }
        if transport != .stream {
            receivePacket(data)
            return
        }

        var data = data
        while !data.isEmpty && state != .closed {
            if streamLength == 0 {
                var position = 0
                var length: UInt32 = 0
                let result = Wire.readVarint(data, &position, &length)
                if result > 0 && !checkStreamLength(length) { return }
                if result < 0 {
                    fail(GesNetworkCodes.malformed)
                    return
                }
                if result > 0 && data.count - position >= Int(length) {
                    let end = position + Int(length)
                    receivePacket(UnsafeRawBufferPointer(rebasing: data[position..<end]))
                    data = UnsafeRawBufferPointer(rebasing: data[end...])
                    continue
                }

                // The prefix or the packet is incomplete: keep the bytes, which are at most a prefix and a packet.
                if stream == nil {
                    stream = UnsafeMutableRawBufferPointer.allocate(byteCount: maxPacketBytes + 5, alignment: 1)
                }
                stream!.baseAddress!.copyMemory(from: data.baseAddress!, byteCount: data.count)
                streamLength = data.count
                return
            }

            let buffer = stream!
            var buffered = 0
            var packetLength: UInt32 = 0
            var prefix = Wire.readVarint(UnsafeRawBufferPointer(rebasing: buffer[0..<streamLength]), &buffered, &packetLength)
            if prefix == 0 {
                buffer[streamLength] = data[0]
                streamLength += 1
                data = UnsafeRawBufferPointer(rebasing: data[1...])
                prefix = Wire.readVarint(UnsafeRawBufferPointer(rebasing: buffer[0..<streamLength]), &buffered, &packetLength)
                if prefix < 0 {
                    fail(GesNetworkCodes.malformed)
                    return
                }
                if prefix == 0 || !checkStreamLength(packetLength) { continue }
            }

            let end = buffered + Int(packetLength)
            let take = min(end - streamLength, data.count)
            if take > 0 { (buffer.baseAddress! + streamLength).copyMemory(from: data.baseAddress!, byteCount: take) }
            streamLength += take
            data = UnsafeRawBufferPointer(rebasing: data[take...])
            if streamLength < end { return }
            streamLength = 0
            receivePacket(UnsafeRawBufferPointer(rebasing: buffer[buffered..<end]))
        }
    }

    /// Passes received bytes: one packet on a message transport, any chunk of the byte stream on a stream transport.
    /// - Parameters:
    ///   - data: The received bytes; they are not retained.
    ///   - now: The current time in microseconds, not earlier than `now`.
    /// - Throws: `GesApiError.invalidArgument` when the time goes back.
    public func receive(_ data: [UInt8], now: Int64) throws {
        try data.withUnsafeBytes { try receive($0, now: now) }
    }

    // Checks a stream length prefix; closes the connection and returns false when it is not allowed.
    private func checkStreamLength(_ length: UInt32) -> Bool {
        if length == 0 {
            fail(GesNetworkCodes.malformed)
        } else if Int(length) > maxPacketBytes {
            fail(GesNetworkCodes.packetTooLarge)
        } else {
            return true
        }
        return false
    }

    private func receivePacket(_ packet: UnsafeRawBufferPointer) {
        if packet.isEmpty {
            fail(GesNetworkCodes.malformed)
            return
        }
        if packet.count > maxPacketBytes {
            fail(GesNetworkCodes.packetTooLarge)
            return
        }

        if datagram {
            receiveDatagramPacket(packet)
            return
        }

        lastReceive = now
        switch packet[0] {
        case Wire.close where (keys?.largest ?? -1) < 0:
            receiveClose(packet)
        case Wire.retry where isClient && state == .hello && helloSent:
            receiveRetry(packet)
        case Wire.hello where !isClient && state == .awaitHello:
            receiveHello(packet)
        case Wire.welcome where isClient && state == .hello && helloSent:
            receiveWelcome(packet)
        case Wire.data where state == .connected && !encrypted:
            receiveData(packet)
        case Wire.sealed where state == .connected && encrypted:
            receiveSealed(packet)
        default:
            fail(GesNetworkCodes.malformed)
        }
    }

    func receiveHello(_ packet: UnsafeRawBufferPointer) {
        if packet.count < 5 || Wire.readUInt32(packet, 1) != Wire.magic {
            fail(GesNetworkCodes.protocolMismatch)
            return
        }

        var position = 5
        if !readVersion(packet, &position) { return }
        if packet.count - position < 8 {
            fail(GesNetworkCodes.malformed)
            return
        }

        let hash = Wire.readUInt64(packet, position)
        position += 8
        var count: UInt32 = 0
        if Wire.readVarint(packet, &position, &count) != 1 || count < 1 || count > UInt32(Wire.maximumRoutes) || packet.count - position < 8 * Int(count) {
            fail(GesNetworkCodes.malformed)
            return
        }

        let hashes = UnsafeRawBufferPointer(rebasing: packet[position..<position + 8 * Int(count)])
        position += hashes.count
        var limit = 0
        var baseline: Int64 = 0
        if !readPacketLimit(packet, &position, &limit) || !readBaselineLimit(packet, &position, &baseline) || !readTransferLimits(packet, &position) { return }
        // The cookie: up to 64 bytes on a datagram transport, which the gate checks, and none on the others.
        var cookie: UInt32 = 0
        if Wire.readVarint(packet, &position, &cookie) != 1 || cookie > UInt32(datagram ? Wire.maximumCookieBytes : 0) || packet.count - position < Int(cookie) {
            fail(GesNetworkCodes.malformed)
            return
        }

        position += Int(cookie)
        if Int(count) != routeCount || hash != network.hash {
            fail(GesNetworkCodes.schemaMismatch, mismatchDetail(hashes))
            return
        }

        peerPacketLimit = limit
        baselineLimit = min(maxBaseline, baseline)
        receiveHelloSecurity(packet, position)
    }

    // The roots of the local routes the client lacks or binds differently; the network name when none differs.
    private func mismatchDetail(_ hashes: UnsafeRawBufferPointer) -> String {
        var text = ""
        let clientCount = hashes.count / 8
        for index in 0..<routeCount {
            if index < clientCount && Wire.readUInt64(hashes, 8 * index) == network.roots[index].bindingHash { continue }
            if !text.isEmpty { text += ", " }
            text += network.network.routes[index].root.name
        }
        return text.isEmpty ? network.network.name : text
    }

    func receiveWelcome(_ packet: UnsafeRawBufferPointer) {
        if helloSealed {
            if !receiveSealedWelcome(packet) && !datagram { fail(GesNetworkCodes.authenticationFailed) }
            return
        }

        // A key request asks for a retry; only a plain hello is answered by a plain welcome.
        if minimumSecurity != .none {
            fail(GesNetworkCodes.malformed)
            return
        }

        acceptWelcome(packet, 1)
    }

    // Reads the welcome fields from the position to the end and connects.
    func acceptWelcome(_ packet: UnsafeRawBufferPointer, _ start: Int) {
        var position = start
        var limit = 0
        var baseline: Int64 = 0
        if !readVersion(packet, &position) || !readPacketLimit(packet, &position, &limit) || !readBaselineLimit(packet, &position, &baseline)
            || !readTransferLimits(packet, &position)
        {
            return
        }
        if position != packet.count {
            fail(GesNetworkCodes.malformed)
            return
        }

        peerPacketLimit = limit
        baselineLimit = min(maxBaseline, baseline)
        state = .connected
        security = !encrypted ? .none : pinned ? .verified : .encrypted
        _ = addEvent(.connected, -1, UnsafeRawBufferPointer(start: nil, count: 0), .none, "", "", false)
    }

    private func readVersion(_ packet: UnsafeRawBufferPointer, _ position: inout Int) -> Bool {
        var version: UInt32 = 0
        if Wire.readVarint(packet, &position, &version) != 1 {
            fail(GesNetworkCodes.malformed)
        } else if version != Wire.version {
            fail(GesNetworkCodes.protocolMismatch)
        } else {
            return true
        }
        return false
    }

    private func readPacketLimit(_ packet: UnsafeRawBufferPointer, _ position: inout Int, _ limit: inout Int) -> Bool {
        var value: UInt32 = 0
        if Wire.readVarint(packet, &position, &value) != 1 || value < UInt32(Wire.minimumPacketBytes) || value > UInt32(Wire.maximumPacketBytes) {
            fail(GesNetworkCodes.malformed)
            return false
        }
        limit = Int(value)
        return true
    }

    private func readBaselineLimit(_ packet: UnsafeRawBufferPointer, _ position: inout Int, _ limit: inout Int64) -> Bool {
        var value: UInt32 = 0
        if Wire.readVarint(packet, &position, &value) != 1 || value > UInt32(Wire.maximumBaselineBytes) {
            fail(GesNetworkCodes.malformed)
            return false
        }
        limit = Int64(value)
        return true
    }

    // Reads the peer's transfer limit and the mask of the compressions it accepts; unknown compressions are malformed.
    private func readTransferLimits(_ packet: UnsafeRawBufferPointer, _ position: inout Int) -> Bool {
        var limit: UInt32 = 0
        var mask: UInt32 = 0
        if Wire.readVarint(packet, &position, &limit) != 1 || Int64(limit) > Int64(Wire.maximumTransferBytes) || Wire.readVarint(packet, &position, &mask) != 1
            || (mask & ~Wire.compressionMaskDeflate) != 0
        {
            fail(GesNetworkCodes.malformed)
            return false
        }
        peerTransferLimit = Int64(limit)
        peerDeflate = (mask & Wire.compressionMaskDeflate) != 0
        return true
    }

    func receiveClose(_ packet: UnsafeRawBufferPointer) {
        var position = 1
        var codeLength: UInt32 = 0
        if Wire.readVarint(packet, &position, &codeLength) != 1 || codeLength < 1 || codeLength > UInt32(Wire.maximumCodeBytes) || packet.count - position < Int(codeLength) {
            fail(GesNetworkCodes.malformed)
            return
        }

        let code = UnsafeRawBufferPointer(rebasing: packet[position..<position + Int(codeLength)])
        position += code.count
        for character in code where character < UInt8(ascii: "!") || character > UInt8(ascii: "~") {
            fail(GesNetworkCodes.malformed)
            return
        }

        var detailLength: UInt32 = 0
        if Wire.readVarint(packet, &position, &detailLength) != 1 || detailLength > UInt32(Wire.maximumDetailBytes) || packet.count - position != Int(detailLength) {
            fail(GesNetworkCodes.malformed)
            return
        }

        let detail = UnsafeRawBufferPointer(rebasing: packet[position..<packet.count])
        if !Wire.isValidUtf8(detail) {
            fail(GesNetworkCodes.malformed)
            return
        }

        state = .closed
        dropSending()
        closePending = false
        _ = addEvent(.closed, -1, UnsafeRawBufferPointer(start: nil, count: 0), .none, String(decoding: code, as: UTF8.self), String(decoding: detail, as: UTF8.self), true)
    }

    func receiveData(_ packet: UnsafeRawBufferPointer) {
        if packet.count < 4 || (packet[1] & ~3) != 0 || Wire.readUInt16(packet, 2) != UInt16(truncatingIfNeeded: receivedCount) {
            fail(GesNetworkCodes.malformed)
            return
        }

        var position = 4
        if (packet[1] & 1) != 0 {
            if packet.count - position < 6 || !acknowledge(Wire.readUInt16(packet, position)) {
                fail(GesNetworkCodes.malformed)
                return
            }
            position += 6
        }

        // A packet with frames asks for acknowledgement.
        let asks = (packet[1] & 2) != 0
        if !asks && position < packet.count {
            fail(GesNetworkCodes.malformed)
            return
        }

        receivedCount += 1
        while position < packet.count {
            if !receiveFrame(packet, &position) { return }
        }

        if asks && !ackDue {
            ackDue = true
            ackDeadline = now + ackDelay
        }
    }

    // Applies an acknowledgement; false when it names a sequence not sent or older than the previous one.
    private func acknowledge(_ sequence: UInt16) -> Bool {
        if sentCount == 0 { return false }
        let start = max(ackedIndex, 0)
        let index = start + Int64(sequence &- UInt16(truncatingIfNeeded: start))
        if index >= sentCount { return false }
        if index <= ackedIndex { return true }
        if sentCount - index <= Int64(Self.sendTimeWindow) && sendAsks[Int(index % Int64(Self.sendTimeWindow))] {
            let sample = now - sendTimes[Int(index % Int64(Self.sendTimeWindow))]
            roundTripMicroseconds = roundTripMicroseconds < 0 ? sample : roundTripMicroseconds + (sample - roundTripMicroseconds) / 8
        }
        ackedIndex = index
        confirmWritten(index)
        return true
    }

    // Reads one frame; false when it closed the connection.
    private func receiveFrame(_ packet: UnsafeRawBufferPointer, _ position: inout Int) -> Bool {
        if frameUnits < Self.microsecondsPerSecond { return fail(GesNetworkCodes.rateExceeded) }
        frameUnits -= Self.microsecondsPerSecond
        var channel: UInt32 = 0
        if Wire.readVarint(packet, &position, &channel) != 1 || Int(channel) > routeCount { return fail(GesNetworkCodes.malformed) }
        if channel == 0 {
            // Control frames: a stream or message transport carries only cancels.
            if position == packet.count || packet[position] != Wire.controlCancel { return fail(GesNetworkCodes.malformed) }
            position += 1
            return receiveCancel(packet, &position)
        }

        let route = Int(channel) - 1
        if localSends[route] { return fail(GesNetworkCodes.unexpectedRoute) }
        if packet.count - position < 2 { return fail(GesNetworkCodes.malformed) }
        let id = Wire.readUInt16(packet, position)
        position += 2
        var offset: UInt32 = 0
        var total: UInt32 = 0
        if Wire.readVarint(packet, &position, &offset) != 1 || Wire.readVarint(packet, &position, &total) != 1 { return fail(GesNetworkCodes.malformed) }
        let bulk = classes[route] == .bulk
        if Int64(total) > messageLimit(route) { return fail(GesNetworkCodes.messageTooLarge) }
        var length: UInt32 = 0
        if Wire.readVarint(packet, &position, &length) != 1 || packet.count - position < Int(length) { return fail(GesNetworkCodes.malformed) }
        let chunk = UnsafeRawBufferPointer(rebasing: packet[position..<position + Int(length)])
        position += chunk.count
        if (length == 0 && total != 0) || UInt64(offset) + UInt64(length) > UInt64(total) { return fail(GesNetworkCodes.malformed) }

        let assembly: GesNetworkReassembly
        switch classes[route] {
        case .reliable: assembly = receiveReliable
        case .bulk: assembly = receiveBulk
        case .latest: assembly = receiveLatest[route]!
        }
        if offset == 0 {
            if assembly.active || id != assembly.nextId { return fail(GesNetworkCodes.malformed) }
            assembly.nextId &+= 1
            if length == total {
                if bulk { addProgress(route, id, Int64(total), Int64(total)) }
                return deliver(route, id, chunk)
            }
            assembly.start(channel, id, total)
        } else if !assembly.active || assembly.channel != channel || assembly.id != id || assembly.total != total || assembly.received != Int(offset) {
            return fail(GesNetworkCodes.malformed)
        }

        if bulk {
            pendingBulk += Int64(length)
            if pendingBulk > maxPendingBulk { return fail(GesNetworkCodes.queueFull) }
        } else {
            pending += Int64(length)
            if pending > maxPending { return fail(GesNetworkCodes.queueFull) }
        }

        assembly.append(chunk)
        if bulk { addProgress(route, id, Int64(assembly.received), Int64(total)) }
        if assembly.received != Int(total) { return true }
        assembly.active = false
        // The message's bytes are pending already; the event counts them again.
        if bulk {
            pendingBulk -= Int64(total)
        } else {
            pending -= Int64(total)
        }
        return assembly.withMessage { deliver(route, id, $0) }
    }

    // Reports a complete message with its id; a topic message becomes its state's full record, numbered by the id of a
    // latest message or the topic's count of reliable states. False when it closed the connection.
    func deliver(_ route: Int, _ id: UInt16, _ message: UnsafeRawBufferPointer) -> Bool {
        if classes[route] == .bulk { return deliverTransfer(route, id, message) }
        var message = message
        if let topic = topics[route] {
            if message.isEmpty || Int(message[0]) > GesNetworkTopic.maximumStates { return fail(GesNetworkCodes.malformed) }
            let number: UInt16
            if classes[route] == .latest {
                number = id
            } else {
                number = topic.next
                topic.next &+= 1
            }
            var rest = UnsafeRawBufferPointer(rebasing: message[1...])
            if message[0] != 0 {
                var baseline = UnsafeRawBufferPointer(start: nil, count: 0)
                if !topic.find(number &- UInt16(message[0]), &baseline) { return fail(GesNetworkCodes.malformed) }
                guard let decoded = try? GesSchemaDelta.decode(topic.schema, delta: rest, baseline: baseline, into: topic.scratch),
                    decoded.deltaByteCount == rest.count
                else { return fail(GesNetworkCodes.malformed) }
                rest = UnsafeRawBufferPointer(rebasing: topic.scratch[0..<decoded.byteCount])
            }
            topic.add(rest, baselineLimit, number)
            if datagram && classes[route] == .latest { oweConfirmation(route, number) }
            message = rest
        }
        return addEvent(.message, route, message, .pending, "", "", false) || fail(GesNetworkCodes.queueFull)
    }

    // The largest message of a received route: on a bulk route a transfer's header and data.
    func messageLimit(_ route: Int) -> Int64 { classes[route] == .bulk ? transferLimit(route) + Int64(Wire.transferHeaderBytes) : maxBytes[route] }

    // Closes the connection with an error found in received data and returns false.
    @discardableResult
    func fail(_ code: String, _ detail: String = "") -> Bool {
        closeLocal(code, detail, true)
        return false
    }
}
