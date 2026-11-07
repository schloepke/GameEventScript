// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The sizes of a decoded delta.
public struct GesSchemaDeltaDecoded: Sendable {
    /// The bytes of the resulting full record in the buffer.
    public let byteCount: Int
    /// The bytes the delta occupied; further data may follow them.
    public let deltaByteCount: Int
}

/// Converts between full records of a schema and deltas against a baseline, the full record the receiver already holds.
/// Both directions compare wire values and allocate nothing on success. Data is untrusted: every limit is checked, and
/// invalid data fails with a `GesWireError`. The buffers must stay valid during a call.
public enum GesSchemaDelta {
    /// Writes what differs between a full record and its baseline.
    /// - Parameters:
    ///   - schema: The bound schema, of the kind `schema`.
    ///   - record: The full record to send.
    ///   - baseline: The full record the receiver holds.
    ///   - buffer: The buffer that receives the delta, separate from both records; `maxByteCount` of the schema always suffices.
    /// - Returns: The number of bytes written.
    /// - Throws: `GesWireError` when a record is invalid, their keys differ, the buffer overlaps a record or the schema is no `schema`.
    public static func encode(_ schema: GesBoundSchema, record: UnsafeRawBufferPointer, baseline: UnsafeRawBufferPointer, into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {
        try check(schema)
        try checkSeparate(buffer, record, baseline)
        var encoder = GesSchemaDeltaEncoder(schema, record, baseline, buffer)
        return try encoder.run()
    }

    /// Writes the full record that a delta produces against its baseline.
    /// - Parameters:
    ///   - schema: The bound schema, of the kind `schema`.
    ///   - delta: The delta; it may continue after it.
    ///   - baseline: The full record this side holds.
    ///   - buffer: The buffer that receives the record, separate from the delta and the baseline; `maxByteCount` of the schema always suffices.
    ///   - byteLimit: An optional limit on the bytes of this delta.
    /// - Returns: The bytes of the record and of the delta.
    /// - Throws: `GesWireError` when the data is no valid delta for this baseline, the buffer overlaps an input or the schema is no `schema`.
    public static func decode(_ schema: GesBoundSchema, delta: UnsafeRawBufferPointer, baseline: UnsafeRawBufferPointer, into buffer: UnsafeMutableRawBufferPointer, byteLimit: Int = .max) throws(GesWireError) -> GesSchemaDeltaDecoded {
        var bit = 0
        return try decode(schema, delta: delta, baseline: baseline, into: buffer, byteLimit: byteLimit, bit: &bit)
    }

    // `bit` holds the position in the delta when it fails, for the dump.
    static func decode(_ schema: GesBoundSchema, delta: UnsafeRawBufferPointer, baseline: UnsafeRawBufferPointer, into buffer: UnsafeMutableRawBufferPointer, byteLimit: Int, bit: inout Int) throws(GesWireError) -> GesSchemaDeltaDecoded {
        try check(schema)
        try checkSeparate(buffer, delta, baseline)
        var decoder = GesSchemaDeltaDecoder(schema, delta, baseline, buffer, byteLimit)
        do throws(GesWireError) {
            return try decoder.run()
        } catch {
            bit = decoder.bitPosition
            throw error
        }
    }

    private static func check(_ schema: GesBoundSchema) throws(GesWireError) {
        if schema.kind != .schema { throw GesWireError(GesWireError.typeMismatch) }
    }

    // A target that overlaps an input would overwrite bytes that are still to be read.
    private static func checkSeparate(_ buffer: UnsafeMutableRawBufferPointer, _ first: UnsafeRawBufferPointer, _ second: UnsafeRawBufferPointer) throws(GesWireError) {
        if overlap(buffer, first) || overlap(buffer, second) { throw GesWireError(GesWireError.overlappingBuffers) }
    }

    private static func overlap(_ buffer: UnsafeMutableRawBufferPointer, _ input: UnsafeRawBufferPointer) -> Bool {
        guard let target = buffer.baseAddress, let source = input.baseAddress, !buffer.isEmpty, !input.isEmpty else { return false }
        return UnsafeRawPointer(target) < source + input.count && source < UnsafeRawPointer(target) + buffer.count
    }

    // The difference of two indices without overflow; a wrapping field takes the shorter way, and an exact half period is negative.
    static func difference(_ node: GesSchemaNode, _ index: UInt64, _ baseline: UInt64) -> (magnitude: UInt64, negative: Bool) {
        if !node.wrapping { return index < baseline ? (baseline - index, true) : (index - baseline, false) }
        let period = node.maxIndex + 1
        let forward = index >= baseline ? index - baseline : period - (baseline - index)
        return forward >= period - period / 2 ? (period - forward, true) : (forward, false)
    }
}

// What the encoder and decoder share: one schema's nodes, read in the full encoding.
private struct GesSchemaFullCopy {
    let nodes: [GesSchemaNode]
    let infos: [GesSchemaNodeInfo]

    func path(_ index: Int) -> String { infos[index].path }

    // One validated value of a fixed width, or the bits of a number.
    func wire(_ source: inout GesSchemaReader, _ index: Int) throws(GesWireError) -> UInt64 {
        let node = nodes[index]
        let value = try source.getBits(node.width, index)
        if node.kind != .float {
            if value > node.maxIndex { throw GesWireError(GesWireError.invalidIndex, path(index)) }
            return value
        }
        let number = Double(bitPattern: value)
        if number.isNaN || value == 1 << 63 || (node.hasRange && number.isInfinite) { throw GesWireError(GesWireError.invalidNumber, path(index)) }
        if node.hasRange && (number < node.lowerValue || number > node.upperValue) { throw GesWireError(GesWireError.outOfRange, path(index)) }
        return value
    }

    // A field in its full encoding with its presence bit, validated and written only when asked for.
    func fullField(_ source: inout GesSchemaReader, _ index: Int, _ target: inout GesSchemaWriter, write: Bool) throws(GesWireError) {
        if nodes[index].optional {
            let present = try source.getBits(1, index)
            if write { try target.putBits(present, 1) }
            if present == 0 { return }
        }
        try fullValue(&source, index, &target, write: write)
    }

    func fullValue(_ source: inout GesSchemaReader, _ index: Int, _ target: inout GesSchemaWriter, write: Bool) throws(GesWireError) {
        let node = nodes[index]
        switch node.kind {
        case .nothing:
            break
        case .boolean:
            let flag = try source.getBits(1, index)
            if write { try target.putBits(flag, 1) }
        case .text:
            try fullText(&source, index, &target, write: write)
        case .list:
            let count = try source.getBits(node.width, index)
            if count > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, path(index)) }
            if write { try target.putBits(count, node.width) }
            var item: UInt64 = 0
            while item < count {
                try fullField(&source, index + 1, &target, write: write)
                item += 1
            }
        case .group:
            var child = index + 1
            while child < node.next {
                try fullField(&source, child, &target, write: write)
                child = nodes[child].next
            }
        case .union:
            let position = try wire(&source, index)
            if write { try target.putBits(position, node.width) }
            try fullValue(&source, GesSchemaNumbers.alternative(nodes, index, Int(position)), &target, write: write)
        default:
            let value = try wire(&source, index)
            if write { try target.putBits(value, node.width) }
        }
    }

    // The bytes need not start at a byte boundary, so they are checked as they pass, without a buffer.
    private func fullText(_ source: inout GesSchemaReader, _ index: Int, _ target: inout GesSchemaWriter, write: Bool) throws(GesWireError) {
        let node = nodes[index]
        let count = try source.getBits(node.width, index)
        if count > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, path(index)) }
        try source.require(Int(count) * 8, index)
        if write { try target.putBits(count, node.width) }
        var text = GesSchemaUtf8Scanner()
        var valid = !node.isTagText || count > 0
        var position: UInt64 = 0
        while position < count {
            let value = UInt8(truncatingIfNeeded: try source.getBits(8, index))
            if write { try target.putBits(UInt64(value), 8) }
            if valid {
                if node.isTagText {
                    valid = (0x61...0x7A).contains(value) || (position > 0 && ((0x30...0x39).contains(value) || (0x41...0x5A).contains(value)))
                } else {
                    valid = text.next(value)
                }
            }
            position += 1
        }
        if !valid { throw GesWireError(node.isTagText ? GesWireError.invalidTag : GesWireError.invalidText, path(index)) }
        if !node.isTagText && !text.isComplete { throw GesWireError(GesWireError.invalidText, path(index)) }
    }

    // The sort value of a key: an integer without a range compares with its sign, every other key by its index.
    func keyOrder(_ index: Int, _ value: UInt64) -> UInt64 {
        let key = nodes[index]
        return key.kind == .integer && !key.hasRange ? value ^ (UInt64(1) << 63) : value
    }

    // A keyed list in its full encoding, validated and written only when asked for; returns whether its keys ascend strictly.
    func ascending(_ source: inout GesSchemaReader, _ index: Int, _ target: inout GesSchemaWriter, write: Bool) throws(GesWireError) -> Bool {
        let node = nodes[index]
        let count = try source.getBits(node.width, index)
        if count > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, path(index)) }
        if write { try target.putBits(count, node.width) }
        var ascending = true
        var previous: UInt64 = 0
        var item: UInt64 = 0
        while item < count {
            let value = try wire(&source, index + 2)
            if write { try target.putBits(value, nodes[index + 2].width) }
            let order = keyOrder(index + 2, value)
            if item > 0 && order <= previous { ascending = false }
            previous = order
            try rest(&source, index, &target, write: write)
            item += 1
        }
        return ascending
    }

    // Reads the next key of one list; returns whether it follows the previous one strictly.
    func advance(_ source: inout GesSchemaReader, _ key: Int, _ order: inout UInt64) throws(GesWireError) -> Bool {
        let next = keyOrder(key, try wire(&source, key))
        let follows = next > order
        order = next
        return follows
    }

    // The fields of a keyed list's element after its key.
    func rest(_ source: inout GesSchemaReader, _ index: Int, _ target: inout GesSchemaWriter, write: Bool) throws(GesWireError) {
        let end = nodes[index + 1].next
        var child = nodes[index + 2].next
        while child < end {
            try fullField(&source, child, &target, write: write)
            child = nodes[child].next
        }
    }

    static func equal(_ first: inout GesSchemaReader, _ firstStart: Int, _ firstEnd: Int, _ second: inout GesSchemaReader, _ secondStart: Int, _ secondEnd: Int) throws(GesWireError) -> Bool {
        if firstEnd - firstStart != secondEnd - secondStart { return false }
        var equal = true
        first.seek(firstStart)
        second.seek(secondStart)
        var left = firstEnd - firstStart
        while left > 0 && equal {
            let width = min(left, 64)
            equal = try first.getBits(width, -1) == second.getBits(width, -1)
            left -= width
        }
        first.seek(firstEnd)
        second.seek(secondEnd)
        return equal
    }
}

private struct GesSchemaUtf8Scanner {
    private var more = 0
    private var least: UInt32 = 0
    private var value: UInt32 = 0

    var isComplete: Bool { more == 0 }

    mutating func next(_ next: UInt8) -> Bool {
        if more > 0 {
            if next & 0xC0 != 0x80 { return false }
            value = (value << 6) | UInt32(next & 0x3F)
            more -= 1
            return more > 0 || (value >= least && value <= 0x10FFFF && !(0xD800...0xDFFF).contains(value))
        }
        if next < 0x80 { return true }
        if (0xC2...0xDF).contains(next) {
            (more, least, value) = (1, 0x80, UInt32(next & 0x1F))
        } else if (0xE0...0xEF).contains(next) {
            (more, least, value) = (2, 0x800, UInt32(next & 0x0F))
        } else if (0xF0...0xF4).contains(next) {
            (more, least, value) = (3, 0x10000, UInt32(next & 0x07))
        } else {
            return false
        }
        return true
    }
}

private struct GesSchemaDeltaEncoder {
    private enum Section { case removed, added, changed }

    private let full: GesSchemaFullCopy
    private let fieldCount: Int
    private var record: GesSchemaReader
    private var baseline: GesSchemaReader
    private var writer: GesSchemaWriter

    init(_ schema: GesBoundSchema, _ record: UnsafeRawBufferPointer, _ baseline: UnsafeRawBufferPointer, _ buffer: UnsafeMutableRawBufferPointer) {
        full = GesSchemaFullCopy(nodes: schema.nodes, infos: schema.infos)
        fieldCount = schema.deltaFieldCount
        self.record = GesSchemaReader(schema, data: record)
        self.baseline = GesSchemaReader(schema, data: baseline)
        writer = GesSchemaWriter(schema, into: buffer)
    }

    mutating func run() throws(GesWireError) -> Int {
        var start = 0
        if full.nodes.first?.key == true {
            let value = try full.wire(&record, 0)
            if try value != full.wire(&baseline, 0) { throw GesWireError(GesWireError.keyMismatch, full.path(0)) }
            try writer.putBits(value, full.nodes[0].width)
            start = full.nodes[0].next
        }
        _ = try group(start, full.nodes.count, fieldCount)
        _ = try record.finishBits()
        _ = try baseline.finishBits()
        return try writer.finishBits()
    }

    // The change bits come first and are set as the fields turn out to differ.
    private mutating func group(_ start: Int, _ end: Int, _ count: Int) throws(GesWireError) -> Bool {
        let first = writer.bitPosition
        var left = count
        while left > 0 {
            try writer.putBits(0, min(left, 64))
            left -= 64
        }
        var changed = false
        var ordinal = 0
        var index = start
        while index < end {
            if try field(index) {
                writer.setBit(first + ordinal)
                changed = true
            }
            ordinal += 1
            index = full.nodes[index].next
        }
        return changed
    }

    // Writes the payload of a changed field and nothing for an unchanged one.
    private mutating func field(_ index: Int) throws(GesWireError) -> Bool {
        if !full.nodes[index].optional { return try payload(index) }
        let present = try record.getBits(1, index) == 1
        let held = try baseline.getBits(1, index) == 1
        if !present && !held { return false }
        if present != held {
            try writer.putBits(present ? 1 : 0, 1)
            if present {
                try full.fullValue(&record, index, &writer, write: true)
            } else {
                try full.fullValue(&baseline, index, &writer, write: false)
            }
            return true
        }
        let entry = writer.bitPosition
        try writer.putBits(1, 1)
        if try payload(index) { return true }
        writer.rewind(entry)
        return false
    }

    private mutating func payload(_ index: Int) throws(GesWireError) -> Bool {
        let node = full.nodes[index]
        switch node.kind {
        case .nothing:
            return false
        case .boolean:
            return try record.getBits(1, index) != baseline.getBits(1, index)
        case .list where node.keyedElements:
            return try keyedList(index)
        case .text, .list:
            return try whole(index)
        case .group:
            let start = writer.bitPosition
            if try group(index + 1, node.next, node.childCount) { return true }
            writer.rewind(start)
            return false
        case .union:
            return try union(index)
        default:
            return try number(index)
        }
    }

    private mutating func number(_ index: Int) throws(GesWireError) -> Bool {
        let node = full.nodes[index]
        let value = try full.wire(&record, index)
        let held = try full.wire(&baseline, index)
        if value == held { return false }
        if node.deltaRange == 0 {
            try writer.putBits(value, node.width)
            return true
        }
        let difference = GesSchemaDelta.difference(node, value, held)
        if difference.magnitude > node.deltaRange {
            try writer.putBits(1, 1)
            try writer.putBits(value, node.width)
            return true
        }
        try writer.putBits(0, 1)
        try writer.putBits(difference.negative ? node.deltaRange - difference.magnitude : node.deltaRange + difference.magnitude, node.deltaWidth)
        return true
    }

    // A Text or List is compared and replaced as a whole.
    private mutating func whole(_ index: Int) throws(GesWireError) -> Bool {
        let recordStart = record.bitPosition
        try full.fullValue(&record, index, &writer, write: false)
        let recordEnd = record.bitPosition
        let baselineStart = baseline.bitPosition
        try full.fullValue(&baseline, index, &writer, write: false)
        if try GesSchemaFullCopy.equal(&record, recordStart, recordEnd, &baseline, baselineStart, baseline.bitPosition) { return false }
        record.seek(recordStart)
        try full.fullValue(&record, index, &writer, write: true)
        return true
    }

    // Unchanged, whole when one side's keys do not ascend strictly, and otherwise by element. The first pass validates both
    // lists, finds whether they ascend and already writes the removed section, which is dropped again when the list is
    // unchanged or goes whole.
    private mutating func keyedList(_ index: Int) throws(GesWireError) -> Bool {
        let entry = writer.bitPosition
        let recordStart = record.bitPosition
        let baselineStart = baseline.bitPosition
        try writer.putBits(0, 1)
        let ascending = try section(index, recordStart, baselineStart, .removed)
        let recordEnd = record.bitPosition
        let baselineEnd = baseline.bitPosition
        if try GesSchemaFullCopy.equal(&record, recordStart, recordEnd, &baseline, baselineStart, baselineEnd) {
            writer.rewind(entry)
            return false
        }
        if !ascending {
            writer.rewind(entry)
            try writer.putBits(1, 1)
            record.seek(recordStart)
            try full.fullValue(&record, index, &writer, write: true)
            return true
        }
        _ = try section(index, recordStart, baselineStart, .added)
        _ = try section(index, recordStart, baselineStart, .changed)
        record.seek(recordEnd)
        baseline.seek(baselineEnd)
        return true
    }

    // One merge pass over both lists writes one section; its count is set afterwards, since only the nested deltas tell
    // whether a kept element changed. Returns whether the keys of both lists ascend strictly; the merge still ends when
    // they do not, and the result is then discarded.
    private mutating func section(_ index: Int, _ recordStart: Int, _ baselineStart: Int, _ section: Section) throws(GesWireError) -> Bool {
        let node = full.nodes[index]
        let key = index + 2
        let keyWidth = full.nodes[key].width
        record.seek(recordStart)
        baseline.seek(baselineStart)
        var recordLeft = try record.getBits(node.width, index)
        if recordLeft > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, full.path(index)) }
        let baselineCount = try baseline.getBits(node.width, index)
        if baselineCount > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, full.path(index)) }
        let countBit = writer.bitPosition
        try writer.putBits(0, node.width)
        var count: UInt64 = 0
        var position: UInt64 = 0
        var ascending = true
        var recordOrder = recordLeft > 0 ? full.keyOrder(key, try full.wire(&record, key)) : 0
        var baselineOrder = baselineCount > 0 ? full.keyOrder(key, try full.wire(&baseline, key)) : 0
        while recordLeft > 0 || position < baselineCount {
            let holdsRecord = recordLeft > 0
            let holdsBaseline = position < baselineCount
            let useRecord = holdsRecord && (!holdsBaseline || recordOrder <= baselineOrder)
            let useBaseline = holdsBaseline && (!holdsRecord || baselineOrder <= recordOrder)
            if useRecord && useBaseline {
                if section == .changed {
                    let entry = writer.bitPosition
                    try writer.putBits(position, node.positionWidth)
                    if try group(full.nodes[key].next, full.nodes[index + 1].next, full.nodes[index + 1].childCount - 1) {
                        count += 1
                    } else {
                        writer.rewind(entry)
                    }
                } else {
                    try full.rest(&record, index, &writer, write: false)
                    try full.rest(&baseline, index, &writer, write: false)
                }
            } else if useRecord {
                if section == .added {
                    try writer.putBits(full.keyOrder(key, recordOrder), keyWidth)
                    count += 1
                }
                try full.rest(&record, index, &writer, write: section == .added)
            } else {
                if section == .removed {
                    try writer.putBits(position, node.positionWidth)
                    count += 1
                }
                try full.rest(&baseline, index, &writer, write: false)
            }
            if useRecord {
                recordLeft -= 1
                if recordLeft > 0, !(try full.advance(&record, key, &recordOrder)) { ascending = false }
            }
            if useBaseline {
                position += 1
                if position < baselineCount, !(try full.advance(&baseline, key, &baselineOrder)) { ascending = false }
            }
        }
        var bit = 0
        while bit < node.width {
            if (count >> UInt64(bit)) & 1 != 0 { writer.setBit(countBit + bit) }
            bit += 1
        }
        return ascending
    }

    private mutating func union(_ index: Int) throws(GesWireError) -> Bool {
        let node = full.nodes[index]
        let position = try full.wire(&record, index)
        let held = try full.wire(&baseline, index)
        let alternative = GesSchemaNumbers.alternative(full.nodes, index, Int(position))
        if position != held {
            try writer.putBits(position, node.width)
            try full.fullValue(&record, alternative, &writer, write: true)
            try full.fullValue(&baseline, GesSchemaNumbers.alternative(full.nodes, index, Int(held)), &writer, write: false)
            return true
        }
        let entry = writer.bitPosition
        try writer.putBits(position, node.width)
        if try payload(alternative) { return true }
        writer.rewind(entry)
        return false
    }
}

private struct GesSchemaDeltaDecoder {
    private let full: GesSchemaFullCopy
    private let fieldCount: Int
    private var delta: GesSchemaReader
    private var baseline: GesSchemaReader
    private var writer: GesSchemaWriter

    init(_ schema: GesBoundSchema, _ delta: UnsafeRawBufferPointer, _ baseline: UnsafeRawBufferPointer, _ buffer: UnsafeMutableRawBufferPointer, _ byteLimit: Int) {
        full = GesSchemaFullCopy(nodes: schema.nodes, infos: schema.infos)
        fieldCount = schema.deltaFieldCount
        self.delta = GesSchemaReader(schema, data: delta, byteLimit: byteLimit)
        self.baseline = GesSchemaReader(schema, data: baseline)
        writer = GesSchemaWriter(schema, into: buffer)
    }

    var bitPosition: Int { delta.bitPosition }

    mutating func run() throws(GesWireError) -> GesSchemaDeltaDecoded {
        var start = 0
        if full.nodes.first?.key == true {
            let value = try full.wire(&delta, 0)
            if try value != full.wire(&baseline, 0) { throw GesWireError(GesWireError.invalidDelta, full.path(0)) }
            try writer.putBits(value, full.nodes[0].width)
            start = full.nodes[0].next
        }
        _ = try group(start, full.nodes.count, fieldCount, -1)
        let deltaBytes = try delta.finishBits()
        _ = try baseline.finishBits()
        return GesSchemaDeltaDecoded(byteCount: try writer.finishBits(), deltaByteCount: deltaBytes)
    }

    private mutating func group(_ start: Int, _ end: Int, _ count: Int, _ owner: Int) throws(GesWireError) -> Bool {
        try delta.require(count, owner)
        var bit = delta.bitPosition
        delta.seek(bit + count)
        var changed = false
        var index = start
        while index < end {
            let payload = delta.bitPosition
            delta.seek(bit)
            let set = try delta.getBits(1, index) == 1
            delta.seek(payload)
            if set {
                try field(index)
                changed = true
            } else {
                try full.fullField(&baseline, index, &writer, write: true)
            }
            bit += 1
            index = full.nodes[index].next
        }
        return changed
    }

    private mutating func field(_ index: Int) throws(GesWireError) {
        if !full.nodes[index].optional {
            try payload(index)
            return
        }
        let present = try delta.getBits(1, index) == 1
        let held = try baseline.getBits(1, index) == 1
        if !present && !held { throw GesWireError(GesWireError.invalidDelta, full.path(index)) }
        try writer.putBits(present ? 1 : 0, 1)
        if present && held {
            try payload(index)
        } else if present {
            try full.fullValue(&delta, index, &writer, write: true)
        } else {
            try full.fullValue(&baseline, index, &writer, write: false)
        }
    }

    // The payload of a field whose change bit is set, which must be a change.
    private mutating func payload(_ index: Int) throws(GesWireError) {
        let node = full.nodes[index]
        switch node.kind {
        case .nothing:
            throw GesWireError(GesWireError.invalidDelta, full.path(index))
        case .boolean:
            try writer.putBits(try baseline.getBits(1, index) ^ 1, 1)
        case .list where node.keyedElements:
            if try delta.getBits(1, index) == 1 {
                try wholeKeyedList(index)
            } else {
                try elementKeyedList(index)
            }
        case .text, .list:
            let deltaStart = delta.bitPosition
            try full.fullValue(&delta, index, &writer, write: true)
            let deltaEnd = delta.bitPosition
            let baselineStart = baseline.bitPosition
            try full.fullValue(&baseline, index, &writer, write: false)
            if try GesSchemaFullCopy.equal(&delta, deltaStart, deltaEnd, &baseline, baselineStart, baseline.bitPosition) { throw GesWireError(GesWireError.invalidDelta, full.path(index)) }
        case .group:
            if !(try group(index + 1, node.next, node.childCount, index)) { throw GesWireError(GesWireError.invalidDelta, full.path(index)) }
        case .union:
            try union(index)
        default:
            try writer.putBits(try number(index), node.width)
        }
    }

    private mutating func wholeKeyedList(_ index: Int) throws(GesWireError) {
        let deltaStart = delta.bitPosition
        let deltaAscending = try full.ascending(&delta, index, &writer, write: true)
        let deltaEnd = delta.bitPosition
        let baselineStart = baseline.bitPosition
        let baselineAscending = try full.ascending(&baseline, index, &writer, write: false)
        if deltaAscending && baselineAscending { throw GesWireError(GesWireError.invalidDelta, full.path(index)) }
        if try GesSchemaFullCopy.equal(&delta, deltaStart, deltaEnd, &baseline, baselineStart, baseline.bitPosition) { throw GesWireError(GesWireError.invalidDelta, full.path(index)) }
    }

    private mutating func sectionCount(_ index: Int) throws(GesWireError) -> UInt64 {
        let node = full.nodes[index]
        let count = try delta.getBits(node.width, index)
        if count > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, full.path(index)) }
        return count
    }

    // The removed positions ascending inside the baseline.
    private mutating func positions(_ index: Int, _ count: UInt64, _ baselineCount: UInt64) throws(GesWireError) {
        var previous: UInt64 = 0
        var item: UInt64 = 0
        while item < count {
            let position = try delta.getBits(full.nodes[index].positionWidth, index)
            if position >= baselineCount || (item > 0 && position <= previous) { throw GesWireError(GesWireError.invalidDelta, full.path(index)) }
            previous = position
            item += 1
        }
    }

    // Checks run in wire order; one that needs the baseline belongs to the item that triggers it. The baseline's order
    // belongs to the form bit, so it is checked first, and an added key that the baseline holds to that added element.
    // The sections are then read side by side through their own cursors while the baseline is walked once more.
    private mutating func elementKeyedList(_ index: Int) throws(GesWireError) {
        let node = full.nodes[index]
        let key = index + 2
        let keyWidth = full.nodes[key].width
        let invalid = GesWireError(GesWireError.invalidDelta, full.path(index))
        let baselineStart = baseline.bitPosition
        if !(try full.ascending(&baseline, index, &writer, write: false)) { throw invalid }
        baseline.seek(baselineStart)
        let baselineCount = try baseline.getBits(node.width, index)
        let elementsStart = baseline.bitPosition

        var removedLeft = try sectionCount(index)
        var removedCursor = delta.bitPosition
        try positions(index, removedLeft, baselineCount)
        var addedLeft = try sectionCount(index)
        var addedCursor = delta.bitPosition
        var previous: UInt64 = 0
        var heldLeft = baselineCount
        var heldOrder: UInt64 = 0
        var holds = false
        var item: UInt64 = 0
        while item < addedLeft {
            let order = full.keyOrder(key, try full.wire(&delta, key))
            if item > 0 && order <= previous { throw invalid }
            previous = order
            while true {
                if !holds {
                    if heldLeft == 0 { break }
                    heldOrder = full.keyOrder(key, try full.wire(&baseline, key))
                    try full.rest(&baseline, index, &writer, write: false)
                    heldLeft -= 1
                    holds = true
                }
                if heldOrder == order { throw invalid }
                if heldOrder > order { break }
                holds = false
            }
            try full.rest(&delta, index, &writer, write: false)
            item += 1
        }

        baseline.seek(elementsStart)

        var changedLeft = try sectionCount(index)
        var changedCursor = delta.bitPosition
        if removedLeft == 0 && addedLeft == 0 && changedLeft == 0 { throw invalid }
        let resultCount = baselineCount - removedLeft + addedLeft
        if resultCount > UInt64(node.maxCount) { throw invalid }
        try writer.putBits(resultCount, node.width)

        var nextRemoved = try nextPosition(index, &removedCursor, &removedLeft)
        var nextChanged = try nextPosition(index, &changedCursor, &changedLeft)
        var addedKey: UInt64 = addedLeft > 0 ? try self.addedKey(key, &addedCursor) : 0
        var position: UInt64 = 0
        while position < baselineCount {
            let held = try full.wire(&baseline, key)
            let order = full.keyOrder(key, held)
            while addedLeft > 0 && full.keyOrder(key, addedKey) < order { try writeAdded(index, &addedCursor, &addedLeft, &addedKey) }
            if position == nextRemoved {
                if position == nextChanged { throw invalid }
                try full.rest(&baseline, index, &writer, write: false)
                nextRemoved = try nextPosition(index, &removedCursor, &removedLeft)
            } else if position == nextChanged {
                try writer.putBits(held, keyWidth)
                delta.seek(changedCursor)
                if !(try group(full.nodes[key].next, full.nodes[index + 1].next, full.nodes[index + 1].childCount - 1, index + 1)) {
                    throw GesWireError(GesWireError.invalidDelta, full.path(index + 1))
                }
                changedCursor = delta.bitPosition
                let before = nextChanged
                nextChanged = try nextPosition(index, &changedCursor, &changedLeft)
                if nextChanged <= before { throw invalid }
            } else {
                try writer.putBits(held, keyWidth)
                try full.rest(&baseline, index, &writer, write: true)
            }
            position += 1
        }

        // A changed position beyond the baseline was never reached.
        if nextChanged != .max { throw invalid }
        while addedLeft > 0 { try writeAdded(index, &addedCursor, &addedLeft, &addedKey) }
        delta.seek(changedCursor)
    }

    // The next position of a section, or the largest number when it has ended.
    private mutating func nextPosition(_ index: Int, _ cursor: inout Int, _ left: inout UInt64) throws(GesWireError) -> UInt64 {
        if left == 0 { return .max }
        left -= 1
        delta.seek(cursor)
        let position = try delta.getBits(full.nodes[index].positionWidth, index)
        cursor = delta.bitPosition
        return position
    }

    private mutating func addedKey(_ key: Int, _ cursor: inout Int) throws(GesWireError) -> UInt64 {
        delta.seek(cursor)
        let value = try full.wire(&delta, key)
        cursor = delta.bitPosition
        return value
    }

    private mutating func writeAdded(_ index: Int, _ cursor: inout Int, _ left: inout UInt64, _ key: inout UInt64) throws(GesWireError) {
        try writer.putBits(key, full.nodes[index + 2].width)
        delta.seek(cursor)
        try full.rest(&delta, index, &writer, write: true)
        cursor = delta.bitPosition
        left -= 1
        if left > 0 { key = try addedKey(index + 2, &cursor) }
    }

    private mutating func number(_ index: Int) throws(GesWireError) -> UInt64 {
        let node = full.nodes[index]
        let held = try full.wire(&baseline, index)
        let fallback = node.deltaRange == 0 ? true : try delta.getBits(1, index) == 1
        if fallback {
            let value = try full.wire(&delta, index)
            if value == held || (node.deltaRange > 0 && GesSchemaDelta.difference(node, value, held).magnitude <= node.deltaRange) {
                throw GesWireError(GesWireError.invalidDelta, full.path(index))
            }
            return value
        }
        let range = try delta.getBits(node.deltaWidth, index)
        if range > node.deltaRange * 2 || range == node.deltaRange { throw GesWireError(GesWireError.invalidDelta, full.path(index)) }
        return range > node.deltaRange ? try forward(index, held, range - node.deltaRange) : try backward(index, held, node.deltaRange - range)
    }

    private func forward(_ index: Int, _ held: UInt64, _ difference: UInt64) throws(GesWireError) -> UInt64 {
        let node = full.nodes[index]
        if node.wrapping { return difference >= node.maxIndex + 1 - held ? held - (node.maxIndex + 1 - difference) : held + difference }
        if difference > node.maxIndex - held { throw GesWireError(GesWireError.invalidDelta, full.path(index)) }
        return held + difference
    }

    private func backward(_ index: Int, _ held: UInt64, _ difference: UInt64) throws(GesWireError) -> UInt64 {
        if difference <= held { return held - difference }
        let node = full.nodes[index]
        if !node.wrapping { throw GesWireError(GesWireError.invalidDelta, full.path(index)) }
        return node.maxIndex + 1 - (difference - held)
    }

    private mutating func union(_ index: Int) throws(GesWireError) {
        let node = full.nodes[index]
        let position = try full.wire(&delta, index)
        let held = try full.wire(&baseline, index)
        try writer.putBits(position, node.width)
        let alternative = GesSchemaNumbers.alternative(full.nodes, index, Int(position))
        if position == held {
            try payload(alternative)
            return
        }
        try full.fullValue(&delta, alternative, &writer, write: true)
        try full.fullValue(&baseline, GesSchemaNumbers.alternative(full.nodes, index, Int(held)), &writer, write: false)
    }
}
