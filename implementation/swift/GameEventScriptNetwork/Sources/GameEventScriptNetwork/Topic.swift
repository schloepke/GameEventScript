// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

// The states of one topic in one direction: their numbers and the ring of recent full records. Both sides apply the
// same ring rule, so the receiver's ring holds every state from a base on that the sender's ring holds.
final class GesNetworkTopic {
    static let maximumStates = 32

    let schema: GesBoundSchema
    private let maxRecord: Int
    private var offsets = [Int](repeating: 0, count: GesNetworkTopic.maximumStates)
    private var lengths = [Int](repeating: 0, count: GesNetworkTopic.maximumStates)
    private var numbers = [UInt16](repeating: 0, count: GesNetworkTopic.maximumStates)
    // The index of the packet that carried the state's last fragment; -1 while it is not completely sent. Stream and
    // message transports confirm a state through it.
    private var packets = [Int64](repeating: 0, count: GesNetworkTopic.maximumStates)
    // Whether the receiver confirmed the state; datagram transports confirm through it.
    private var confirmed = [Bool](repeating: false, count: GesNetworkTopic.maximumStates)
    private var bytes = UnsafeMutableRawBufferPointer(start: nil, count: 0)
    private var end = 0
    private var first = 0
    private var count = 0
    private var held: Int64 = 0

    // Whether the server's host subscribed the client to this topic; a client's own topics always are.
    var subscribed: Bool
    // The number of the next reliable state.
    var next: UInt16 = 0
    // The count of states added and of states whose last fragment is sent.
    private var added: Int64 = 0
    private var completed: Int64 = 0
    // The message being encoded or decoded: the mode byte and up to one record.
    let scratch: UnsafeMutableRawBufferPointer
    // The message of the latest state being sent and its length.
    var message = UnsafeMutableRawBufferPointer(start: nil, count: 0)
    var messageLength = 0

    init(_ schema: GesBoundSchema, subscribed: Bool) {
        self.schema = schema
        self.subscribed = subscribed
        maxRecord = schema.maxByteCount
        scratch = UnsafeMutableRawBufferPointer.allocate(byteCount: maxRecord + 1, alignment: 8)
    }

    deinit {
        scratch.deallocate()
        message.deallocate()
        bytes.deallocate()
    }

    // Adds a state with its number and full record; older states leave while the ring would exceed its limits.
    func add(_ record: UnsafeRawBufferPointer, _ limit: Int64, _ number: UInt16) {
        while count > 0 && (count + 1 > Self.maximumStates || held + Int64(record.count) > limit) {
            held -= Int64(lengths[first])
            first = (first + 1) % Self.maximumStates
            count -= 1
        }
        if count == 0 { end = 0 }
        if bytes.count - end < record.count { makeRoom(record.count, limit) }
        let slot = (first + count) % Self.maximumStates
        if record.count > 0 { (bytes.baseAddress! + end).copyMemory(from: record.baseAddress!, byteCount: record.count) }
        offsets[slot] = end
        lengths[slot] = record.count
        numbers[slot] = number
        packets[slot] = -1
        confirmed[slot] = false
        end += record.count
        held += Int64(record.count)
        count += 1
        added += 1
    }

    // Moves the held records to the start and grows the storage up to the most the ring can hold.
    private func makeRoom(_ length: Int, _ limit: Int64) {
        let start = count == 0 ? 0 : offsets[first]
        let used = end - start
        var target = bytes
        if bytes.count - used < length {
            let bound = max(min(limit, Int64(Self.maximumStates) * Int64(maxRecord)), Int64(maxRecord))
            target = .allocate(byteCount: Int(min(bound, max(Int64(bytes.count) * 2, Int64(used + length)))), alignment: 8)
        }
        if used > 0 {
            // The ranges may overlap when the storage is kept.
            target.baseAddress!.copyMemory(from: bytes.baseAddress! + start, byteCount: used)
        }
        if target.baseAddress != bytes.baseAddress {
            bytes.deallocate()
            bytes = target
        }
        for index in 0..<count { offsets[(first + index) % Self.maximumStates] -= start }
        end = used
    }

    // Records the packet that carried the last fragment of the next state; a state no longer held is skipped.
    func markSent(_ packet: Int64) {
        let index = completed
        completed += 1
        let oldest = added - Int64(count)
        if index >= oldest && index < added { packets[(first + Int(index - oldest)) % Self.maximumStates] = packet }
    }

    // Confirms the held state with the number; a state no longer held is skipped.
    func confirm(_ number: UInt16) {
        var index = count - 1
        while index >= 0 {
            let slot = (first + index) % Self.maximumStates
            if numbers[slot] == number {
                confirmed[slot] = true
                return
            }
            index -= 1
        }
    }

    // Finds the newest confirmed state: confirmed directly or carried completely by a packet up to the acknowledged
    // one. Returns the distance of its number to the given number of the new state, or 0 when none is confirmed. The
    // record stays valid until the next change of the ring.
    func base(_ acknowledged: Int64, _ number: UInt16, _ record: inout UnsafeRawBufferPointer) -> Int {
        var index = count - 1
        while index >= 0 {
            let slot = (first + index) % Self.maximumStates
            let packet = packets[slot]
            if confirmed[slot] || (packet >= 0 && packet <= acknowledged) {
                record = UnsafeRawBufferPointer(rebasing: bytes[offsets[slot]..<offsets[slot] + lengths[slot]])
                return Int(number &- numbers[slot])
            }
            index -= 1
        }
        return 0
    }

    // Finds the held state with the number; false when the ring does not hold it.
    func find(_ number: UInt16, _ record: inout UnsafeRawBufferPointer) -> Bool {
        var index = count - 1
        while index >= 0 {
            let slot = (first + index) % Self.maximumStates
            if numbers[slot] == number {
                record = UnsafeRawBufferPointer(rebasing: bytes[offsets[slot]..<offsets[slot] + lengths[slot]])
                return true
            }
            index -= 1
        }
        return false
    }

    // Empties the ring; the numbering continues.
    func clear() {
        first = 0
        count = 0
        end = 0
        held = 0
    }
}
