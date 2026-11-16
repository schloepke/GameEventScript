// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

private typealias Wire = GesNetworkWire

extension GesNetworkConnection {
    /// Writes the next packet to transmit into the buffer: on a stream transport with its length prefix. Call it
    /// until it returns 0.
    /// - Parameter buffer: The target, at least 1200 bytes.
    /// - Returns: The number of bytes written; 0 when nothing is due.
    /// - Throws: `GesApiError.invalidArgument` when the buffer holds fewer than 1200 bytes.
    public func transmit(into buffer: UnsafeMutableRawBufferPointer) throws -> Int {
        if buffer.count < Wire.minimumPacketBytes { throw GesApiError.invalidArgument("The buffer holds at least 1200 bytes.") }
        // A stream packet is written behind room for the longest prefix of a 65535-byte packet and then moved.
        let prefix = transport == .stream ? 3 : 0
        let packet = UnsafeMutableRawBufferPointer(rebasing: buffer[prefix...])
        let length: Int
        if closePending {
            length = writeClose(packet)
            closePending = false
        } else if state == .closed {
            return 0
        } else if isClient && !helloSent {
            length = writeHello(packet)
            helloSent = true
        } else if welcomePending {
            length = writeWelcome(packet)
            welcomePending = false
        } else if state == .connected {
            length = writeData(packet, Int(min(sendPacketLimit, Int64(packet.count))))
            if length == 0 { return 0 }
        } else {
            return 0
        }

        lastSend = now
        if prefix == 0 { return length }
        let size = Wire.varintSize(UInt32(length))
        (buffer.baseAddress! + size).copyMemory(from: buffer.baseAddress! + prefix, byteCount: length)
        _ = Wire.writeVarint(buffer, 0, UInt32(length))
        return size + length
    }

    /// Writes the next packet to transmit into the buffer: on a stream transport with its length prefix. Call it
    /// until it returns 0.
    /// - Parameter buffer: The target, at least 1200 bytes.
    /// - Returns: The number of bytes written; 0 when nothing is due.
    /// - Throws: `GesApiError.invalidArgument` when the buffer holds fewer than 1200 bytes.
    public func transmit(into buffer: inout [UInt8]) throws -> Int {
        try buffer.withUnsafeMutableBytes { try transmit(into: $0) }
    }

    private func writeHello(_ packet: UnsafeMutableRawBufferPointer) -> Int {
        packet[0] = Wire.hello
        Wire.writeUInt32(packet, 1, Wire.magic)
        var position = Wire.writeVarint(packet, 5, Wire.version)
        Wire.writeUInt64(packet, position, network.hash)
        position = Wire.writeVarint(packet, position + 8, UInt32(routeCount))
        for index in 0..<routeCount {
            Wire.writeUInt64(packet, position, network.roots[index].bindingHash)
            position += 8
        }
        position = Wire.writeVarint(packet, position, UInt32(maxPacketBytes))
        position = Wire.writeVarint(packet, position, UInt32(token.count))
        for byte in token {
            packet[position] = byte
            position += 1
        }
        return Wire.writeVarint(packet, position, 0)
    }

    private func writeWelcome(_ packet: UnsafeMutableRawBufferPointer) -> Int {
        packet[0] = Wire.welcome
        let position = Wire.writeVarint(packet, 1, Wire.version)
        return Wire.writeVarint(packet, position, UInt32(maxPacketBytes))
    }

    private func writeClose(_ packet: UnsafeMutableRawBufferPointer) -> Int {
        packet[0] = Wire.close
        var position = Wire.writeVarint(packet, 1, UInt32(closeCode.utf8.count))
        for character in closeCode.utf8 {
            packet[position] = character
            position += 1
        }
        position = Wire.writeVarint(packet, position, UInt32(closeDetail.count))
        for byte in closeDetail {
            packet[position] = byte
            position += 1
        }
        return position
    }

    // Writes a data packet when frames, an acknowledgement or the keepalive are due; 0 otherwise.
    private func writeData(_ packet: UnsafeMutableRawBufferPointer, _ limit: Int) -> Int {
        let frames = hasPendingFrames() && (budget == 0 || budgetUnits >= 0)
        let keepaliveDue = now - lastSend >= keepalive
        if !frames && !(ackDue && now >= ackDeadline) && !keepaliveDue { return 0 }
        packet[0] = Wire.data
        packet[1] = 0
        Wire.writeUInt16(packet, 2, UInt16(truncatingIfNeeded: sentCount))
        var position = 4
        if receivedCount > 0 {
            packet[1] = 1
            Wire.writeUInt16(packet, position, UInt16(truncatingIfNeeded: receivedCount - 1))
            let earlier = Int(min(32, receivedCount - 1))
            Wire.writeUInt32(packet, position + 2, earlier == 32 ? UInt32.max : (UInt32(1) << UInt32(earlier)) - 1)
            position += 6
        }

        let start = position
        if frames {
            position = writeQueue(sendReliable, &nextReliableId, packet, position, limit)
            for route in 0..<routeCount {
                if let slot = sendLatest[route] { position = Self.writeLatest(route, slot, packet, position, limit) }
            }
            position = writeQueue(sendBulk, &nextBulkId, packet, position, limit)
        }

        // Frames and keepalives ask for acknowledgement; a packet that only acknowledges does not.
        let asks = position > start || keepaliveDue
        if asks { packet[1] |= 2 }
        sendTimes[Int(sentCount % Int64(Self.sendTimeWindow))] = now
        sendAsks[Int(sentCount % Int64(Self.sendTimeWindow))] = asks
        sentCount += 1
        ackDue = false
        if position > start && budget > 0 { budgetUnits -= Int64(position) * Self.microsecondsPerSecond }
        return position
    }

    // Writes fragments of reliable or bulk messages until the packet is full or the queue is empty.
    private func writeQueue(_ queue: GesNetworkSendQueue, _ nextId: inout UInt16, _ packet: UnsafeMutableRawBufferPointer, _ position: Int, _ limit: Int) -> Int {
        var position = position
        while queue.count > 0 {
            let id = queue.started ? queue.id : nextId
            let offset = queue.offset
            let route = queue.currentRoute
            var chunk = 0
            let next = queue.withCurrent { Self.writeFrame(packet, position, limit, UInt32(route) + 1, id, offset, $0, &chunk) }
            if next < 0 { break }
            position = next
            if !queue.started {
                queue.started = true
                queue.id = nextId
                nextId &+= 1
            }
            queue.offset += chunk
            if queue.offset < queue.currentLength { break }
            queue.pop()
        }
        return position
    }

    // Writes fragments of a latest route's current message and of the newer one that then takes its place.
    private static func writeLatest(_ route: Int, _ slot: GesNetworkLatestSlot, _ packet: UnsafeMutableRawBufferPointer, _ position: Int, _ limit: Int) -> Int {
        var position = position
        while slot.hasCurrent {
            let id = slot.started ? slot.id : slot.nextId
            let offset = slot.offset
            var chunk = 0
            let next = slot.withCurrent { writeFrame(packet, position, limit, UInt32(route) + 1, id, offset, $0, &chunk) }
            if next < 0 { break }
            position = next
            if !slot.started {
                slot.started = true
                slot.id = slot.nextId
                slot.nextId &+= 1
            }
            slot.offset += chunk
            if slot.offset < slot.currentLength { break }
            slot.complete()
        }
        return position
    }

    // Writes the largest fragment of a message at its offset that fits; -1 when none fits.
    private static func writeFrame(
        _ packet: UnsafeMutableRawBufferPointer,
        _ position: Int,
        _ limit: Int,
        _ channel: UInt32,
        _ id: UInt16,
        _ offset: Int,
        _ message: UnsafeRawBufferPointer,
        _ chunk: inout Int
    ) -> Int {
        chunk = 0
        let total = message.count
        let space = limit - position - Wire.varintSize(channel) - 2 - Wire.varintSize(UInt32(offset)) - Wire.varintSize(UInt32(total))
        if space < 1 { return -1 }
        let remaining = total - offset
        var length = min(remaining, space - 1)
        while Wire.varintSize(UInt32(length)) + length > space { length -= 1 }
        if length == 0 && remaining > 0 { return -1 }
        var position = Wire.writeVarint(packet, position, channel)
        Wire.writeUInt16(packet, position, id)
        position = Wire.writeVarint(packet, position + 2, UInt32(offset))
        position = Wire.writeVarint(packet, position, UInt32(total))
        position = Wire.writeVarint(packet, position, UInt32(length))
        if length > 0 { (packet.baseAddress! + position).copyMemory(from: message.baseAddress! + offset, byteCount: length) }
        chunk = length
        return position + length
    }
}
