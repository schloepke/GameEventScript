// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

private typealias Wire = GesNetworkWire

// Ticks: sets of latest topic states that the receiver reports together. One tick is in transmission at a time; a
// closed tick waits behind it and is replaced by a newer one.
extension GesNetworkConnection {
    static let stageNone: UInt8 = 0
    static let stagePublished: UInt8 = 1
    static let stageIncluded: UInt8 = 2

    /// Opens a tick: until `endTick()`, every publish on a latest topic this side publishes makes the state a member of
    /// the tick instead of sending it alone. Reliable topics, sends and transfers are never members.
    /// - Throws: `GesApiError.invalidOperation` when a tick is open already.
    public func beginTick() throws {
        if tickOpen { throw GesApiError.invalidOperation("A tick is open already.") }
        tickOpen = true
        // A closed tick that still waits is replaced by the new one: its published members become latest states.
        if tickWaiting { releaseStage() }
        tickWaiting = false
    }

    /// Makes a latest topic a member of the open tick with its last published state, without publishing it again. Does
    /// nothing when the topic is a member already, and on a server when the client is not subscribed to it.
    /// - Parameter route: The route index of a latest topic this side publishes.
    /// - Throws: `GesApiError.invalidOperation` when no tick is open or the topic has no published state;
    ///   `GesApiError.invalidArgument` when the route is no latest topic this side publishes.
    public func include(route: Int) throws {
        if !tickOpen { throw GesApiError.invalidOperation("No tick is open.") }
        guard route >= 0, route < routeCount, localSends[route], let topic = topics[route], sendLatest[route] != nil else {
            throw GesApiError.invalidArgument("The route is no latest topic this side publishes.")
        }
        if !hasLast[route] { throw GesApiError.invalidOperation("The topic has no published state.") }
        if !topic.subscribed || stageKinds[route] != Self.stageNone { return }
        stageKinds[route] = Self.stageIncluded
        stageCount += 1
    }

    /// Closes the open tick. Its members are sent once no earlier tick is in transmission; an empty tick sends nothing.
    /// - Returns: `notConnected` when the connection is not connected, which drops the tick; otherwise `queued`.
    /// - Throws: `GesApiError.invalidOperation` when no tick is open.
    @discardableResult
    public func endTick() throws -> GesNetworkSendStatus {
        if !tickOpen { throw GesApiError.invalidOperation("No tick is open.") }
        tickOpen = false
        if state != .connected {
            clearStage()
            return .notConnected
        }
        if stageCount == 0 { return .queued }
        tickWaiting = true
        if !tickSending { startTick() }
        return .queued
    }

    // Publishes a state of a latest topic as a member of the open tick: it waits in the stage until the tick starts.
    func stageState(_ route: Int, _ payload: UnsafeRawBufferPointer) {
        gesNetworkKeep(&stageRecords[route], payload)
        stageLengths[route] = payload.count
        if stageKinds[route] == Self.stageNone { stageCount += 1 }
        stageKinds[route] = Self.stagePublished
    }

    // Keeps the last published record of a latest topic.
    func keepLast(_ route: Int, _ payload: UnsafeRawBufferPointer) {
        gesNetworkKeep(&lastRecords[route], payload)
        lastLengths[route] = payload.count
        hasLast[route] = true
    }

    /// Turns the published members of the waiting tick into latest states of their routes and clears the stage.
    private func releaseStage() {
        for route in 0..<routeCount where stageKinds[route] == Self.stagePublished && topics[route]!.subscribed {
            let length = stageLengths[route]
            stageRecords[route].withUnsafeBytes { sendLatest[route]!.set(UnsafeRawBufferPointer(rebasing: $0[0..<length])) }
        }
        clearStage()
    }

    private func clearStage() {
        for route in 0..<routeCount { stageKinds[route] = Self.stageNone }
        stageCount = 0
    }

    // Starts the waiting tick: published members become latest states of their routes, and an included member names the
    // topic's newest state when the receiver holds it, else publishes that state again.
    private func startTick() {
        tickWaiting = false
        tickSending = true
        tickCurrent = false
        tickFrameDue = false
        tickPending = 0
        var members = 0
        for route in 0..<routeCount {
            tickMembers[route] = false
            tickUnsent[route] = false
            let kind = stageKinds[route]
            if kind == Self.stageNone { continue }
            stageKinds[route] = Self.stageNone
            if !topics[route]!.subscribed { continue }
            let slot = sendLatest[route]!
            members += 1
            tickMembers[route] = true
            if kind == Self.stageIncluded, let named = namedState(slot, route) {
                tickNames[route] = named
                continue
            }
            let length = kind == Self.stagePublished ? stageLengths[route] : lastLengths[route]
            let record = kind == Self.stagePublished ? stageRecords[route] : lastRecords[route]
            record.withUnsafeBytes { slot.set(UnsafeRawBufferPointer(rebasing: $0[0..<length]), member: true) }
            tickUnsent[route] = true
            tickPending += 1
        }
        stageCount = 0
        if members == 0 { tickSending = false } else { tickFrameDue = tickPending == 0 }
    }

    // The newest state of a topic when the receiver holds it and no newer state waits: on a stream or message transport
    // the newest completely sent state, on a datagram transport a confirmed one.
    private func namedState(_ slot: GesNetworkLatestSlot, _ route: Int) -> UInt16? {
        if !slot.hasSent || slot.hasWaiting { return nil }
        if !datagram { return slot.hasCurrent ? nil : slot.id }
        return slot.hasCurrent && slot.sent && slot.lost.count == 0 && topics[route]!.isConfirmed(slot.id) ? slot.id : nil
    }

    // Notes that the member state of a route is completely sent with its number.
    func completeMember(_ route: Int, _ number: UInt16) {
        if !tickSending || !tickUnsent[route] { return }
        tickUnsent[route] = false
        tickNames[route] = number
        tickPending -= 1
        if tickPending == 0 { tickFrameDue = true }
    }

    // Removes the member of a route whose state was dropped before it started; a tick left without members ends.
    func dropMember(_ route: Int) {
        if !tickSending || !tickUnsent[route] { return }
        tickUnsent[route] = false
        tickMembers[route] = false
        tickPending -= 1
        if !tickMembers.contains(true) {
            tickSending = false
            if tickWaiting { startTick() }
        } else if tickPending == 0 {
            tickFrameDue = true
        }
    }

    func dropTicks() {
        clearStage()
        tickWaiting = false
        tickSending = false
        tickCurrent = false
        tickFrameDue = false
    }

    private func tickFrameSize() -> Int {
        var size = 4
        var count = 0
        for route in 0..<routeCount where tickMembers[route] {
            count += 1
            size += Wire.varintSize(UInt32(route + 1)) + 2
        }
        return size + Wire.varintSize(UInt32(count))
    }

    // Writes the due tick frame when it fits; the first write numbers the tick and starts a waiting one.
    func writeTickFrame(_ packet: UnsafeMutableRawBufferPointer, _ position: Int, _ limit: Int) -> Int {
        if !tickFrameDue || limit - position < tickFrameSize() || (datagram && fragmentRoom <= 0) { return position }
        if tickSending {
            tickNumber = nextTick
            nextTick &+= 1
        }
        var count = 0
        for route in 0..<routeCount where tickMembers[route] { count += 1 }
        var position = Wire.writeVarint(packet, position, 0)
        packet[position] = Wire.controlTick
        Wire.writeUInt16(packet, position + 1, tickNumber)
        position = Wire.writeVarint(packet, position + 3, UInt32(count))
        for route in 0..<routeCount where tickMembers[route] {
            position = Wire.writeVarint(packet, position, UInt32(route + 1))
            Wire.writeUInt16(packet, position, tickNames[route])
            position += 2
        }
        if datagram { recordFragment(Self.laneTick, Int64(tickNumber), 0, 0) }
        tickFrameDue = false
        if !tickSending { return position }
        tickSending = false
        tickCurrent = true
        if tickWaiting { startTick() }
        return position
    }

    // Resends a lost tick frame while its tick is current and no newer one started.
    func loseTickFrame(_ number: UInt16) {
        if tickCurrent && !tickSending && number == tickNumber { tickFrameDue = true }
    }

    // MARK: Receiving

    // Holds a received member state until its tick is complete; it replaces an older held state of the route.
    func holdMember(_ route: Int, _ number: UInt16, _ record: UnsafeRawBufferPointer) -> Bool {
        if held[route] { pending -= Int64(heldLengths[route]) }
        gesNetworkKeep(&heldRecords[route], record)
        heldLengths[route] = record.count
        heldNumbers[route] = number
        held[route] = true
        pending += Int64(record.count)
        if pending > maxPending { return fail(GesNetworkCodes.queueFull) }
        return completeTick()
    }

    // Reads a tick frame after its kind byte; false when it closed the connection.
    func receiveTick(_ packet: UnsafeRawBufferPointer, _ position: inout Int) -> Bool {
        if packet.count - position < 2 { return fail(GesNetworkCodes.malformed) }
        let number = Wire.readUInt16(packet, position)
        position += 2
        var count: UInt32 = 0
        if Wire.readVarint(packet, &position, &count) != 1 || count == 0 || Int(count) > routeCount { return fail(GesNetworkCodes.malformed) }
        let start = position
        var previous: UInt32 = 0
        for _ in 0..<Int(count) {
            var channel: UInt32 = 0
            if Wire.readVarint(packet, &position, &channel) != 1 || channel <= previous || Int(channel) > routeCount || packet.count - position < 2 {
                return fail(GesNetworkCodes.malformed)
            }
            let route = Int(channel) - 1
            if localSends[route] || classes[route] != .latest || topics[route] == nil { return fail(GesNetworkCodes.malformed) }
            position += 2
            previous = channel
        }

        // A tick 1 to 32767 ahead of the held one, or of the last reported one when none is held, replaces it.
        if hasHeldTick || hasReportedTick {
            let ahead = number &- (hasHeldTick ? heldTick : reportedTick)
            if ahead == 0 || ahead >= 32768 { return true }
        }

        let end = position
        position = start
        for index in 0..<Int(count) {
            var channel: UInt32 = 0
            _ = Wire.readVarint(packet, &position, &channel)
            heldTickRoutes[index] = Int(channel) - 1
            heldTickStates[index] = Wire.readUInt16(packet, position)
            position += 2
        }
        position = end
        hasHeldTick = true
        heldTick = number
        heldTickCount = Int(count)
        return completeTick()
    }

    // Whether the receiver holds the state of a route with the number: held, last reported or in the ring.
    private func holds(_ route: Int, _ number: UInt16) -> Bool {
        if held[route] && heldNumbers[route] == number { return true }
        if reported[route] && reportedNumbers[route] == number { return true }
        var record = UnsafeRawBufferPointer(start: nil, count: 0)
        return topics[route]!.find(number, &record)
    }

    // Reports the held tick once every state it names is held: a message for each member whose state differs from the
    // last reported one, then the tick. False when it closed the connection.
    func completeTick() -> Bool {
        if !hasHeldTick { return true }
        for index in 0..<heldTickCount where !holds(heldTickRoutes[index], heldTickStates[index]) { return true }
        hasHeldTick = false
        for index in 0..<heldTickCount {
            let route = heldTickRoutes[index]
            let number = heldTickStates[index]
            let fromHeld = held[route] && heldNumbers[route] == number
            releaseHeld(route, number)
            // A state no newer than the last reported one of its topic is not reported again.
            if reported[route] {
                let ahead = number &- reportedNumbers[route]
                if ahead == 0 || ahead >= 32768 { continue }
            }
            let added: Bool
            if fromHeld {
                let length = heldLengths[route]
                added = heldRecords[route].withUnsafeBytes { addEvent(.message, route, UnsafeRawBufferPointer(rebasing: $0[0..<length]), .pending, "", "", false) }
            } else {
                var record = UnsafeRawBufferPointer(start: nil, count: 0)
                _ = topics[route]!.find(number, &record)
                added = addEvent(.message, route, record, .pending, "", "", false)
            }
            if !added { return fail(GesNetworkCodes.queueFull) }
            reported[route] = true
            reportedNumbers[route] = number
        }
        hasReportedTick = true
        reportedTick = heldTick
        return reserveEvent(.tick, -1, 0, .none, "", "", false, Int(heldTick), 0, 0) != nil || fail(GesNetworkCodes.queueFull)
    }

    // Drops the held state of a route unless it is newer than the reported number.
    func releaseHeld(_ route: Int, _ number: UInt16) {
        if !held[route] { return }
        let ahead = heldNumbers[route] &- number
        if ahead > 0 && ahead < 32768 { return }
        held[route] = false
        pending -= Int64(heldLengths[route])
    }

    // Notes a latest topic state reported alone, which counts as the last reported state of its topic.
    func noteReported(_ route: Int, _ number: UInt16) {
        reported[route] = true
        reportedNumbers[route] = number
    }
}
