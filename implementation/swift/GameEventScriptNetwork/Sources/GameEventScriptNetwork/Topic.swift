// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

// The states of one topic in one direction: their numbering and the ring of recent full records. Both sides apply the
// same ring rule to the same states, so the receiver's ring holds every state the sender's ring holds.
final class GesNetworkTopic {
    static let maximumStates = 32

    let schema: GesBoundSchema
    private let maxRecord: Int
    private var offsets = [Int](repeating: 0, count: GesNetworkTopic.maximumStates)
    private var lengths = [Int](repeating: 0, count: GesNetworkTopic.maximumStates)
    // The index of the packet that carried the state's last fragment; -1 while it is not completely sent.
    private var packets = [Int64](repeating: 0, count: GesNetworkTopic.maximumStates)
    private var bytes = UnsafeMutableRawBufferPointer(start: nil, count: 0)
    private var end = 0
    private var first = 0
    private var count = 0
    private var held: Int64 = 0

    // Whether the server's host subscribed the client to this topic; a client's own topics always are.
    var subscribed: Bool
    // The number of the next state.
    var next: Int64 = 0
    // The number of the next state whose last fragment is sent.
    var completed: Int64 = 0
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

    // Numbers a new state and adds its full record; older states leave while the ring would exceed its limits.
    func add(_ record: UnsafeRawBufferPointer, _ limit: Int64) {
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
        packets[slot] = -1
        end += record.count
        held += Int64(record.count)
        count += 1
        next += 1
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
        let number = completed
        completed += 1
        let oldest = next - Int64(count)
        if number >= oldest && number < next { packets[(first + Int(number - oldest)) % Self.maximumStates] = packet }
    }

    // Finds the newest state whose last fragment is acknowledged; the distance to the next state, or 0 when none is.
    // The record stays valid until the next change of the ring.
    func base(_ acknowledged: Int64, _ record: inout UnsafeRawBufferPointer) -> Int {
        var index = count - 1
        while index >= 0 {
            let slot = (first + index) % Self.maximumStates
            let packet = packets[slot]
            if packet >= 0 && packet <= acknowledged {
                record = UnsafeRawBufferPointer(rebasing: bytes[offsets[slot]..<offsets[slot] + lengths[slot]])
                return count - index
            }
            index -= 1
        }
        return 0
    }

    // Finds the state the given distance before the next one; false when the ring does not hold it.
    func find(_ distance: Int, _ record: inout UnsafeRawBufferPointer) -> Bool {
        if distance > count { return false }
        let slot = (first + count - distance) % Self.maximumStates
        record = UnsafeRawBufferPointer(rebasing: bytes[offsets[slot]..<offsets[slot] + lengths[slot]])
        return true
    }

    // Empties the ring; the numbering continues.
    func clear() {
        first = 0
        count = 0
        end = 0
        held = 0
    }
}
