// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

private typealias Wire = GesNetworkWire

// Datagram delivery: packets may be lost, duplicated, delayed or reordered. The sender records its packets with the
// fragments they carried, detects losses as QUIC does and resends lost ranges; the receiver restores the order.
extension GesNetworkConnection {
    static let recordedPackets = 1024
    static let recordedFragments = 16384
    static let immediateAckAsks = 16
    static let initialRoundTrip: Int64 = 333_000
    static let initialVariation: Int64 = 166_500
    static let minimumDelay: Int64 = 1000
    static let helloRepeatInterval: Int64 = microsecondsPerSecond

    // Fragment lanes: cancel frames, reliable, bulk, then one per latest route.
    private static let laneCancel = -1
    private static let laneReliable = 0
    private static let laneBulk = 1
    private static let laneLatest = 2

    private var smoothedRoundTrip: Int64 { roundTripMicroseconds < 0 ? Self.initialRoundTrip : roundTripMicroseconds }

    private var probeDeadline: Int64 {
        if outstanding == 0 || lossTime != .max { return .max }
        let interval = smoothedRoundTrip + max(4 * variation, Self.minimumDelay) + ackDelay
        let shift = Int64(min(probeCount, 10))
        return interval > (Int64.max - lastAskSend) >> shift ? .max : lastAskSend + (interval << shift)
    }

    // The deadlines only a datagram connection has.
    func datagramDeadline() -> Int64 {
        var deadline = Int64.max
        if isClient && state == .hello && helloSent { deadline = helloNext }
        if state != .connected { return deadline }
        deadline = min(deadline, lossTime)
        if probeDue { return min(deadline, now) }
        return min(deadline, probeDeadline)
    }

    func advanceDatagram() {
        if isClient && state == .hello && helloSent && now >= helloNext { helloSent = false }
        if state != .connected { return }
        if now >= lossTime { detectLosses() }
        if !probeDue && now >= probeDeadline {
            probeDue = true
            probeCount += 1
        }
    }

    func hasPendingDatagramFrames() -> Bool {
        if cancels.pendingCount > 0 || sendReliable.lost.count > 0 || sendReliable.hasNew || sendBulk.lost.count > 0 || sendBulk.hasNew { return true }
        for slot in sendLatest where slot?.hasPending == true { return true }
        return false
    }

    func dropDatagramSending() {
        if !datagram { return }
        for index in owed.indices { owed[index] = false }
        probeDue = false
        outstanding = 0
        lossTime = .max
    }

    // MARK: Handshake

    func noteHelloSent() {
        helloWritten = true
        helloNext = helloInterval > Int64.max - now ? .max : now + helloInterval
        helloInterval = min(helloInterval, Int64.max / 2) * 2
    }

    // MARK: Receiving

    // Handles a datagram by type and state: repetitions and late packets of the handshake are ignored.
    func receiveDatagramPacket(_ packet: UnsafeRawBufferPointer) {
        // An encrypted session drops plain data, every plain packet on a client and every plain packet on a server once
        // a sealed one authenticated.
        if encrypted && packet[0] != Wire.sealed && (isClient || (keys?.largest ?? -1) >= 0 || packet[0] == Wire.data) { return }
        if packet[0] != Wire.sealed { lastReceive = now }
        switch packet[0] {
        case Wire.close:
            receiveClose(packet)
        case Wire.hello where !isClient && state == .awaitHello:
            receiveHello(packet)
        case Wire.hello where !isClient && state == .awaitAccept:
            break
        case Wire.hello where !isClient && state == .connected:
            welcomePending = true
        case Wire.retry where isClient && state == .hello && helloWritten:
            receiveRetry(packet)
        case Wire.retry where isClient:
            break
        case Wire.welcome where isClient && state == .hello && helloWritten:
            receiveWelcome(packet)
        case Wire.welcome where isClient && state == .connected:
            break
        case Wire.data where state == .connected && !encrypted:
            receiveDatagramData(packet)
        case Wire.data where isClient && state == .hello:
            break
        case Wire.sealed where state == .connected && encrypted:
            receiveSealed(packet)
        case Wire.sealed where isClient && state == .hello:
            break
        default:
            fail(GesNetworkCodes.malformed)
        }
    }

    func receiveDatagramData(_ packet: UnsafeRawBufferPointer) {
        if packet.count < 4 || (packet[1] & ~3) != 0 {
            fail(GesNetworkCodes.malformed)
            return
        }

        if !placeSequence(Wire.readUInt16(packet, 2)) { return }
        var position = 4
        if (packet[1] & 1) != 0 {
            if packet.count - position < 6 || !acknowledgeDatagram(Wire.readUInt16(packet, position), Wire.readUInt32(packet, position + 2)) {
                fail(GesNetworkCodes.malformed)
                return
            }
            // Progress the acknowledgement reports may exceed the pending events.
            if state == .closed { return }
            position += 6
        }

        let asks = (packet[1] & 2) != 0
        while position < packet.count {
            if !receiveDatagramFrame(packet, &position, asks) { return }
        }

        if !asks { return }
        asksSinceSend += 1
        dueAck()
        if asksSinceSend >= Self.immediateAckAsks { ackDeadline = min(ackDeadline, now) }
    }

    private func dueAck() {
        if ackDue { return }
        ackDue = true
        ackDeadline = now + ackDelay
    }

    // Places a received sequence; false for a duplicate or a sequence too old.
    private func placeSequence(_ sequence: UInt16) -> Bool {
        if !receivedAny {
            receivedAny = true
            newestSequence = sequence
            sequenceBits = 0
            return true
        }

        let ahead = sequence &- newestSequence
        if ahead > 0 && ahead < 32768 {
            sequenceBits = ahead > 32 ? 0 : UInt32(truncatingIfNeeded: (UInt64(sequenceBits) << UInt64(ahead)) | (UInt64(1) << UInt64(ahead - 1)))
            newestSequence = sequence
            return true
        }

        let behind = newestSequence &- sequence
        if behind == 0 || behind > 32 { return false }
        let bit = UInt32(1) << UInt32(behind - 1)
        if (sequenceBits & bit) != 0 { return false }
        sequenceBits |= bit
        return true
    }

    // Applies an acknowledgement; false when it names a packet before the first or none was sent.
    private func acknowledgeDatagram(_ sequence: UInt16, _ bits: UInt32) -> Bool {
        let sent = sentCount
        if sent == 0 { return false }
        let named = sent - 1 - Int64(UInt16(truncatingIfNeeded: sent - 1) &- sequence)
        if named < 0 { return false }
        var acknowledged = false
        if named > largestAcked {
            if isRecorded(named) && recordAsks[Int(named % Int64(Self.recordedPackets))] {
                sample(now - recordTimes[Int(named % Int64(Self.recordedPackets))])
            }
            largestAcked = named
        }

        acknowledged = acknowledgePacket(named) || acknowledged
        for bit in 0..<32 where (bits & (UInt32(1) << UInt32(bit))) != 0 && state != .closed {
            acknowledged = acknowledgePacket(named - 1 - Int64(bit)) || acknowledged
        }
        if state == .closed { return true }

        sendReliable.popDone()
        sendBulk.popDone()
        if acknowledged { probeCount = 0 }
        detectLosses()
        return true
    }

    private func isRecorded(_ packet: Int64) -> Bool {
        packet >= 0 && packet < sentCount && sentCount - packet <= Int64(Self.recordedPackets)
    }

    private func sample(_ sample: Int64) {
        if roundTripMicroseconds < 0 {
            roundTripMicroseconds = sample
            variation = sample / 2
        } else {
            variation += (abs(roundTripMicroseconds - sample) - variation) / 4
            roundTripMicroseconds += (sample - roundTripMicroseconds) / 8
        }
        latestSample = sample
    }

    // Acknowledges a recorded outstanding packet and its fragments; false when it was not outstanding.
    private func acknowledgePacket(_ packet: Int64) -> Bool {
        if !isRecorded(packet) || !recordOutstanding[Int(packet % Int64(Self.recordedPackets))] { return false }
        let slot = Int(packet % Int64(Self.recordedPackets))
        recordOutstanding[slot] = false
        outstanding -= 1
        for index in 0..<recordFragmentCounts[slot] {
            let fragment = Int((recordFragments[slot] + Int64(index)) % Int64(Self.recordedFragments))
            let lane = fragmentLanes[fragment]
            let key = fragmentKeys[fragment]
            if lane == Self.laneCancel {
                cancels.confirm(key)
                continue
            }
            if lane >= Self.laneLatest { continue }
            if lane == Self.laneBulk {
                if !acknowledgeBulk(key, fragmentLengths[fragment]) { return true }
                continue
            }
            var route = 0
            var number = 0
            if sendReliable.acknowledge(key, fragmentLengths[fragment], &route, &number) && number >= 0 {
                topics[route]?.confirm(UInt16(truncatingIfNeeded: number))
            }
        }
        return true
    }

    // Acknowledges a fragment of a transfer and reports the transfer's acknowledged bytes; false when it closed the
    // connection.
    private func acknowledgeBulk(_ key: Int64, _ length: Int) -> Bool {
        var index = 0
        if !sendBulk.find(key, &index) || sendBulk.isCancelled(index) { return true }
        let before = sendBulk.acknowledgedBytes(index)
        var route = 0
        var number = 0
        _ = sendBulk.acknowledge(key, length, &route, &number)
        let after = sendBulk.acknowledgedBytes(index)
        return after == before || addProgress(sendBulk.route(index), sendBulk.entryId(index), Int64(after), Int64(sendBulk.entryLength(index)))
    }

    // Declares lost the outstanding packets older than the largest acknowledged one whose order or delay ran out.
    private func detectLosses() {
        lossTime = .max
        if largestAcked < 0 { return }
        let delay = max(9 * max(smoothedRoundTrip, latestSample) / 8, Self.minimumDelay)
        let first = max(oldestOutstanding, sentCount - Int64(Self.recordedPackets))
        var packet = max(first, 0)
        while packet < largestAcked {
            defer { packet += 1 }
            let slot = Int(packet % Int64(Self.recordedPackets))
            if !recordOutstanding[slot] { continue }
            let expiry = recordTimes[slot] + delay
            if largestAcked - packet >= 3 || now >= expiry {
                losePacket(packet)
            } else {
                lossTime = min(lossTime, expiry)
            }
        }

        while oldestOutstanding < sentCount && !(isRecorded(oldestOutstanding) && recordOutstanding[Int(oldestOutstanding % Int64(Self.recordedPackets))]) {
            oldestOutstanding += 1
        }
    }

    // Declares an outstanding packet lost and queues its fragments to be resent.
    private func losePacket(_ packet: Int64) {
        let slot = Int(packet % Int64(Self.recordedPackets))
        recordOutstanding[slot] = false
        outstanding -= 1
        for index in 0..<recordFragmentCounts[slot] {
            let fragment = Int((recordFragments[slot] + Int64(index)) % Int64(Self.recordedFragments))
            let lane = fragmentLanes[fragment]
            let key = fragmentKeys[fragment]
            let offset = fragmentOffsets[fragment]
            let length = fragmentLengths[fragment]
            if lane == Self.laneCancel {
                cancels.lose(key)
            } else if lane == Self.laneReliable {
                sendReliable.lose(key, offset, length)
            } else if lane == Self.laneBulk {
                sendBulk.lose(key, offset, length)
            } else {
                sendLatest[lane - Self.laneLatest]?.lose(UInt16(truncatingIfNeeded: key), offset, length)
            }
        }
    }

    // Reads one confirmation or message frame; false when it closed the connection.
    private func receiveDatagramFrame(_ packet: UnsafeRawBufferPointer, _ position: inout Int, _ asks: Bool) -> Bool {
        if frameUnits < Self.microsecondsPerSecond { return fail(GesNetworkCodes.rateExceeded) }
        frameUnits -= Self.microsecondsPerSecond
        var channel: UInt32 = 0
        if Wire.readVarint(packet, &position, &channel) != 1 || Int(channel) > routeCount { return fail(GesNetworkCodes.malformed) }
        if channel == 0 {
            if position == packet.count { return fail(GesNetworkCodes.malformed) }
            let kind = packet[position]
            position += 1
            if kind == Wire.controlConfirmation { return receiveConfirmation(packet, &position) }
            // A cancel frame is resent until acknowledged, so its packet asks.
            if kind != Wire.controlCancel || !asks { return fail(GesNetworkCodes.malformed) }
            return receiveCancel(packet, &position)
        }
        if !asks { return fail(GesNetworkCodes.malformed) }
        let route = Int(channel) - 1
        if localSends[route] { return fail(GesNetworkCodes.unexpectedRoute) }
        if packet.count - position < 2 { return fail(GesNetworkCodes.malformed) }
        let id = Wire.readUInt16(packet, position)
        position += 2
        var offset: UInt32 = 0
        var total: UInt32 = 0
        if Wire.readVarint(packet, &position, &offset) != 1 || Wire.readVarint(packet, &position, &total) != 1 { return fail(GesNetworkCodes.malformed) }
        if Int64(total) > messageLimit(route) { return fail(GesNetworkCodes.messageTooLarge) }
        var length: UInt32 = 0
        if Wire.readVarint(packet, &position, &length) != 1 || packet.count - position < Int(length) { return fail(GesNetworkCodes.malformed) }
        let chunk = UnsafeRawBufferPointer(rebasing: packet[position..<position + Int(length)])
        position += chunk.count
        if (length == 0 && total != 0) || UInt64(offset) + UInt64(length) > UInt64(total) { return fail(GesNetworkCodes.malformed) }

        if classes[route] == .latest { return receiveLatestFragment(route, channel, id, Int(offset), total, chunk) }
        let bulk = classes[route] == .bulk
        let ordered = bulk ? orderedBulk! : orderedReliable!
        if Int(id &- ordered.nextId) >= GesNetworkOrderedReceive.window {
            return Int(ordered.nextId &- id) <= GesNetworkOrderedReceive.window || fail(GesNetworkCodes.malformed)
        }
        let message = ordered.message(id)
        if !message.active || message.id != id {
            // The total counts as pending before the message's storage grows to it.
            if bulk {
                pendingBulk += Int64(total)
                if pendingBulk > maxPendingBulk { return fail(GesNetworkCodes.queueFull) }
            } else {
                pending += Int64(total)
                if pending > maxPending { return fail(GesNetworkCodes.queueFull) }
            }
            message.start(channel, id, total)
        } else if message.cancelled {
            // Fragments of a transfer the sender cancelled are still in flight.
            return true
        } else if message.channel != channel || message.total != total {
            return fail(GesNetworkCodes.malformed)
        }

        let received = message.received
        message.write(Int(offset), chunk)
        if bulk && message.received != received && !addProgress(route, id, message.received, Int64(total)) { return false }
        return deliverOrdered(ordered)
    }

    // Reports the messages that are complete in order and skips cancelled ones; false when it closed the connection.
    func deliverOrdered(_ ordered: GesNetworkOrderedReceive) -> Bool {
        while let ready = ordered.ready {
            ordered.nextId &+= 1
            var delivered = true
            if !ready.cancelled {
                let route = Int(ready.channel) - 1
                // The message's bytes are pending already; the event counts them again.
                if classes[route] == .bulk {
                    pendingBulk -= Int64(ready.total)
                } else {
                    pending -= Int64(ready.total)
                }
                delivered = ready.withMessage { deliver(route, ready.id, $0) }
            }
            ordered.release(ready)
            if !delivered { return false }
        }
        return true
    }

    // Continues, starts or ignores the message of a latest route; false when it closed the connection.
    private func receiveLatestFragment(_ route: Int, _ channel: UInt32, _ id: UInt16, _ offset: Int, _ total: UInt32, _ chunk: UnsafeRawBufferPointer) -> Bool {
        let message = partialLatest[route]!
        if message.active && message.id == id {
            if message.total != total { return fail(GesNetworkCodes.malformed) }
        } else {
            let reference = message.active ? message.id : message.reported
            let ahead = id &- reference
            if (message.active || message.hasReported) && (ahead == 0 || ahead >= 32768) { return true }
            if message.active { pending -= Int64(message.total) }
            message.active = false
            pending += Int64(total)
            if pending > maxPending { return fail(GesNetworkCodes.queueFull) }
            message.start(channel, id, total)
        }

        message.write(offset, chunk)
        if message.received != Int64(message.total) { return true }
        message.active = false
        message.reported = id
        message.hasReported = true
        pending -= Int64(total)
        return message.withMessage { deliver(route, id, $0) }
    }

    private func receiveConfirmation(_ packet: UnsafeRawBufferPointer, _ position: inout Int) -> Bool {
        var channel: UInt32 = 0
        if Wire.readVarint(packet, &position, &channel) != 1 || channel == 0 || Int(channel) > routeCount || packet.count - position < 2 {
            return fail(GesNetworkCodes.malformed)
        }
        let route = Int(channel) - 1
        guard localSends[route], classes[route] == .latest, let topic = topics[route] else { return fail(GesNetworkCodes.malformed) }
        topic.confirm(Wire.readUInt16(packet, position))
        position += 2
        return true
    }

    // Owes the sender a confirmation of a latest state; it replaces one not yet sent.
    func oweConfirmation(_ route: Int, _ number: UInt16) {
        owed[route] = true
        owedNumbers[route] = number
        dueAck()
    }

    // MARK: Sending

    // Writes a data packet when frames, an acknowledgement, the keepalive or a probe are due; 0 otherwise.
    func writeDatagramData(_ packet: UnsafeMutableRawBufferPointer, _ limit: Int) -> Int {
        let sent = sentCount
        let recorded = Int64(Self.recordedPackets)
        let slot = Int(sent % recorded)
        // The record of the packet 1024 before leaves; it is lost when still outstanding.
        let evicted = sent >= recorded && recordOutstanding[slot]
        let oldest = sent - recorded + 1
        let used: Int64
        if oldest >= 0 && oldest < sent {
            used = fragmentTail - recordFragments[Int(oldest % recorded)]
        } else if sent > 0 && oldest < 0 {
            used = fragmentTail - recordFragments[0]
        } else {
            used = 0
        }
        let room = Int(Int64(Self.recordedFragments) - used)
        let frames = (evicted || hasPendingDatagramFrames()) && (budget == 0 || budgetUnits >= 0) && room > 0
        let keepaliveDue = now - lastSend >= keepalive
        if !frames && !(ackDue && now >= ackDeadline) && !keepaliveDue && !probeDue { return 0 }
        if evicted { losePacket(sent - recorded) }

        packet[0] = Wire.data
        packet[1] = 0
        Wire.writeUInt16(packet, 2, UInt16(truncatingIfNeeded: sent))
        var position = 4
        if receivedAny {
            packet[1] = 1
            Wire.writeUInt16(packet, position, newestSequence)
            Wire.writeUInt32(packet, position + 2, sequenceBits)
            position += 6
        }

        for route in 0..<routeCount where owed[route] {
            owed[route] = false
            position = Wire.writeVarint(packet, position, 0)
            packet[position] = Wire.controlConfirmation
            position = Wire.writeVarint(packet, position + 1, UInt32(route) + 1)
            Wire.writeUInt16(packet, position, owedNumbers[route])
            position += 2
        }

        recordFragments[slot] = fragmentTail
        fragmentRoom = room
        let start = position
        if frames {
            position = writeDatagramCancels(packet, position, limit)
            position = writeDatagramQueue(sendReliable, &nextReliableId, Self.laneReliable, packet, position, limit)
            for route in 0..<routeCount {
                if let latest = sendLatest[route] { position = writeDatagramLatest(route, latest, packet, position, limit) }
            }
            position = writeDatagramQueue(sendBulk, &nextBulkId, Self.laneBulk, packet, position, limit)
        }

        let asks = position > start || keepaliveDue || probeDue
        if asks { packet[1] |= 2 }
        recordTimes[slot] = now
        recordAsks[slot] = asks
        recordOutstanding[slot] = asks
        recordFragmentCounts[slot] = Int(fragmentTail - recordFragments[slot])
        if asks {
            if outstanding == 0 { oldestOutstanding = sent }
            outstanding += 1
            lastAskSend = now
        }

        sentCount += 1
        ackDue = false
        probeDue = false
        asksSinceSend = 0
        if position > start && budget > 0 { budgetUnits -= Int64(position) * Self.microsecondsPerSecond }
        return position
    }

    // Writes pending cancel frames; each is resent until a packet holding it is acknowledged.
    private func writeDatagramCancels(_ packet: UnsafeMutableRawBufferPointer, _ position: Int, _ limit: Int) -> Int {
        var position = position
        while cancels.pendingCount > 0 && fragmentRoom > 0 {
            let serial = cancels.firstPending
            let route = cancels.route(serial)
            let code = cancels.code(serial)
            if limit - position < Self.cancelFrameSize(route, code) { break }
            position = Self.writeCancelFrame(packet, position, route, cancels.id(serial), code)
            cancels.markSent(serial)
            recordFragment(Self.laneCancel, serial, 0, 0)
        }
        return position
    }

    private func recordFragment(_ lane: Int, _ key: Int64, _ offset: Int, _ length: Int) {
        let fragment = Int(fragmentTail % Int64(Self.recordedFragments))
        fragmentLanes[fragment] = lane
        fragmentKeys[fragment] = key
        fragmentOffsets[fragment] = offset
        fragmentLengths[fragment] = length
        fragmentTail += 1
        fragmentRoom -= 1
    }

    // Writes the lost ranges, then new fragments of reliable or bulk messages until the packet is full.
    private func writeDatagramQueue(
        _ queue: GesNetworkSendQueue,
        _ nextId: inout UInt16,
        _ lane: Int,
        _ packet: UnsafeMutableRawBufferPointer,
        _ position: Int,
        _ limit: Int
    ) -> Int {
        var position = position
        let lost = queue.lost
        while lost.count > 0 && fragmentRoom > 0 {
            var index = 0
            if !queue.find(lost.message, &index) || queue.isCancelled(index) {
                lost.consume(lost.length)
                continue
            }

            let offset = lost.offset
            let end = offset + lost.length
            var chunk = 0
            let channel = UInt32(queue.route(index)) + 1
            let id = queue.entryId(index)
            let next = queue.withEntry(index) { Self.writeFrame(packet, position, limit, channel, id, offset, end, $0, &chunk) }
            if next < 0 { return position }
            position = next
            recordFragment(lane, lost.message, offset, chunk)
            lost.consume(chunk)
        }

        while queue.hasNew && fragmentRoom > 0 {
            let index = queue.cursor
            if queue.isCancelled(index) {
                queue.advance()
                continue
            }

            let length = queue.entryLength(index)
            let id = queue.cursorStarted || queue.preassigned ? queue.entryId(index) : nextId
            let offset = queue.cursorOffset
            let channel = UInt32(queue.route(index)) + 1
            var chunk = 0
            let next = queue.withEntry(index) { Self.writeFrame(packet, position, limit, channel, id, offset, length, $0, &chunk) }
            if next < 0 { break }
            position = next
            if !queue.cursorStarted {
                queue.cursorStarted = true
                if !queue.preassigned {
                    queue.setEntryId(index, nextId)
                    nextId &+= 1
                }
            }
            recordFragment(lane, queue.serial(index), offset, chunk)
            queue.cursorOffset += chunk
            if queue.cursorOffset < length { break }
            queue.advance()
        }
        return position
    }

    // Writes the lost ranges of a latest route's current message, then its new fragments and those of a newer message.
    private func writeDatagramLatest(_ route: Int, _ slot: GesNetworkLatestSlot, _ packet: UnsafeMutableRawBufferPointer, _ position: Int, _ limit: Int) -> Int {
        let topic = topics[route]
        let lane = Self.laneLatest + route
        let channel = UInt32(route) + 1
        var position = position
        let lost = slot.lost
        while lost.count > 0 && fragmentRoom > 0 {
            let offset = lost.offset
            let end = offset + lost.length
            let id = slot.id
            var chunk = 0
            let next: Int
            if let topic {
                next = Self.writeFrame(packet, position, limit, channel, id, offset, end, UnsafeRawBufferPointer(rebasing: topic.message[0..<topic.messageLength]), &chunk)
            } else {
                next = slot.withCurrent { Self.writeFrame(packet, position, limit, channel, id, offset, end, $0, &chunk) }
            }
            if next < 0 { return position }
            position = next
            recordFragment(lane, Int64(id), offset, chunk)
            lost.consume(chunk)
        }

        while slot.hasCurrent && !slot.sent && fragmentRoom > 0 {
            let id = slot.started ? slot.id : slot.nextId
            let offset = slot.offset
            var chunk = 0
            let next: Int
            let length: Int
            if let topic {
                if !slot.started {
                    topic.messageLength = slot.withCurrent { encodeState(topic, $0, slot.nextId) }
                    if topic.message.baseAddress == nil { topic.message = .allocate(byteCount: topic.scratch.count, alignment: 8) }
                    topic.message.baseAddress!.copyMemory(from: topic.scratch.baseAddress!, byteCount: topic.messageLength)
                }
                length = topic.messageLength
                next = Self.writeFrame(packet, position, limit, channel, id, offset, length, UnsafeRawBufferPointer(rebasing: topic.message[0..<length]), &chunk)
            } else {
                length = slot.currentLength
                next = slot.withCurrent { Self.writeFrame(packet, position, limit, channel, id, offset, length, $0, &chunk) }
            }
            if next < 0 { break }
            position = next
            if !slot.started {
                slot.started = true
                slot.id = slot.nextId
                slot.nextId &+= 1
                if let topic { slot.withCurrent { topic.add($0, baselineLimit, slot.id) } }
            }
            recordFragment(lane, Int64(slot.id), offset, chunk)
            slot.offset += chunk
            if slot.offset < length { break }
            slot.complete()
        }
        return position
    }
}
