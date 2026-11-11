// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

// The running lists of a writer or reader, without allocation. Lists nest at most four levels for encoding.
struct GesListFrames {
    private var ends: (Int, Int, Int, Int) = (0, 0, 0, 0)
    private var elements: (Int, Int, Int, Int) = (0, 0, 0, 0)
    private var lefts: (Int64, Int64, Int64, Int64) = (0, 0, 0, 0)
    private var depth = 0

    mutating func push(end: Int, element: Int, count: Int64) {
        switch depth {
        case 0:
            ends.0 = end
            elements.0 = element
            lefts.0 = count
        case 1:
            ends.1 = end
            elements.1 = element
            lefts.1 = count
        case 2:
            ends.2 = end
            elements.2 = element
            lefts.2 = count
        default:
            ends.3 = end
            elements.3 = element
            lefts.3 = count
        }
        depth += 1
    }

    // The node that follows `next`: the same element again, or on past finished lists.
    mutating func advance(_ next: Int) -> Int {
        while depth > 0 {
            switch depth {
            case 1:
                if next != ends.0 { return next }
                lefts.0 -= 1
                if lefts.0 > 0 { return elements.0 }
            case 2:
                if next != ends.1 { return next }
                lefts.1 -= 1
                if lefts.1 > 0 { return elements.1 }
            case 3:
                if next != ends.2 { return next }
                lefts.2 -= 1
                if lefts.2 > 0 { return elements.2 }
            default:
                if next != ends.3 { return next }
                lefts.3 -= 1
                if lefts.3 > 0 { return elements.3 }
            }
            depth -= 1
        }
        return next
    }
}

// The node-based forms of the wire rules, shared by writer, reader, deltas and values.
enum GesSchemaNumbers {
    static func index(_ node: GesSchemaNode, _ value: Double, _ path: @autoclosure () -> String) throws(GesWireError) -> UInt64 {
        if node.wrapping {
            return try GesWireRules.wrappedQuantizedIndex(value, tenPower: node.sBinary, numerator: node.pBinary, lower: node.lower, period: node.maxIndex &+ 1, path: path())
        }
        return try GesWireRules.quantizedIndex(value, lowerValue: node.lowerValue, upperValue: node.upperValue, tenPower: node.sBinary, numerator: node.pBinary, lower: node.lower, path: path())
    }

    static func value(_ node: GesSchemaNode, _ index: UInt64) -> (value: Double, multiple: Int64) {
        let multiple = (Int64(index) + node.lower) * node.p
        return (Double(multiple) / node.sBinary, multiple)
    }

    static func alternative(_ nodes: [GesSchemaNode], _ union: Int, _ alternative: Int) -> Int {
        var index = union + 1
        var skipped = 0
        while skipped < alternative {
            index = nodes[index].next
            skipped += 1
        }
        return index
    }

    // The error of Text bytes on the wire, if any.
    static func textError(_ node: GesSchemaNode, _ bytes: UnsafeRawBufferPointer) -> String? {
        if node.isBytes { return nil }
        if node.isTagText { return GesWireRules.isTagName(bytes) ? nil : GesWireError.invalidTag }
        return GesWireRules.isValidUTF8(bytes) ? nil : GesWireError.invalidText
    }
}

/// Encodes one top-level record or message into a caller's buffer without allocating on success. Fields are written in
/// declaration order; a nested schema, Point or Vector is entered with `beginNested`. The buffer must stay valid while the
/// writer is used.
public struct GesSchemaWriter {
    private var bits: GesBitWriter
    private var schema: GesBoundSchema
    private var lists = GesListFrames()
    private var cursor = 0

    /// Starts a record or message in a buffer.
    /// - Parameters:
    ///   - schema: The bound schema; for a message set, select a member first.
    ///   - buffer: The buffer that receives the encoding.
    public init(_ schema: GesBoundSchema, into buffer: UnsafeMutableRawBufferPointer) {
        self.schema = schema
        bits = GesBitWriter(into: buffer)
    }

    /// Selects a member of a message set and writes its index.
    /// - Throws: `GesWireError` when the schema is no message set or a member was already selected.
    public mutating func selectMember(_ member: Int) throws(GesWireError) {
        if schema.kind != .messages { throw GesWireError(GesWireError.typeMismatch) }
        if member < 0 || member >= schema.members.count { throw GesWireError(GesWireError.outOfRange) }
        try put(UInt64(member), schema.memberWidth)
        schema = schema.members[member]
    }

    /// Writes a Boolean field or a message tag.
    /// - Throws: `GesWireError` when the field does not fit.
    public mutating func writeBoolean(_ field: GesSchemaField, _ value: Bool) throws(GesWireError) {
        try enter(field, .boolean)
        try put(value ? 1 : 0, 1)
        leave(schema.nodes[field.index].after)
    }

    /// Writes an Int64 to a numeric field.
    /// - Throws: `GesWireError` when the value does not fit.
    public mutating func writeInteger(_ field: GesSchemaField, _ value: Int64) throws(GesWireError) {
        try current(field)
        let node = schema.nodes[field.index]
        switch node.kind {
        case .integer:
            let index: UInt64
            if !node.hasRange {
                index = UInt64(bitPattern: value)
            } else if node.wrapping {
                index = GesWireRules.wrappedIndex(value, lower: node.lower, period: node.maxIndex &+ 1)
            } else {
                if value < node.lower || UInt64(bitPattern: value) &- UInt64(bitPattern: node.lower) > node.maxIndex { throw GesWireError(GesWireError.outOfRange, field.path) }
                index = UInt64(bitPattern: value) &- UInt64(bitPattern: node.lower)
            }
            try presence(node)
            try put(index, node.width)
            leave(node.after)
        case .quantized:
            if value >= 1 << 53 || value <= -(1 << 53) { throw GesWireError(GesWireError.outOfRange, field.path) }
            try writeNumber(field, Double(value))
        case .float:
            let converted = Double(value)
            if converted >= 9_223_372_036_854_775_808 || Int64(converted) != value { throw GesWireError(GesWireError.notRepresentable, field.path) }
            try writeNumber(field, converted)
        default:
            throw GesWireError(GesWireError.typeMismatch, field.path)
        }
    }

    /// Writes a Binary64 to a numeric field. A quantized field rounds it to its step.
    /// - Throws: `GesWireError` when the value does not fit.
    public mutating func writeNumber(_ field: GesSchemaField, _ value: Double) throws(GesWireError) {
        try current(field)
        let node = schema.nodes[field.index]
        switch node.kind {
        case .quantized:
            let index = try GesSchemaNumbers.index(node, value, field.path)
            try presence(node)
            try put(index, node.width)
            leave(node.after)
        case .float:
            if value.isNaN { throw GesWireError(GesWireError.notRepresentable, field.path) }
            if node.hasRange && (value < node.lowerValue || value > node.upperValue) { throw GesWireError(GesWireError.outOfRange, field.path) }
            try presence(node)
            try put((value == 0 ? 0.0 : value).bitPattern, 64)
            leave(node.after)
        case .integer:
            if value.isNaN || value != value.rounded(.down) || value >= 9_223_372_036_854_775_808 || value < -9_223_372_036_854_775_808 { throw GesWireError(GesWireError.notRepresentable, field.path) }
            try writeInteger(field, Int64(value))
        default:
            throw GesWireError(GesWireError.typeMismatch, field.path)
        }
    }

    /// Writes the raw wire index of a ranged field, for callers that store quantized values.
    /// - Throws: `GesWireError` when the index does not fit.
    public mutating func writeIndex(_ field: GesSchemaField, _ index: UInt64) throws(GesWireError) {
        try current(field)
        let node = schema.nodes[field.index]
        if node.kind != .integer && node.kind != .quantized && node.kind != .tag { throw GesWireError(GesWireError.typeMismatch, field.path) }
        if index > node.maxIndex { throw GesWireError(GesWireError.outOfRange, field.path) }
        try presence(node)
        try put(index, node.width)
        leave(node.after)
    }

    /// Writes a tag of a closed set by its index in declared order.
    /// - Throws: `GesWireError` when the set has no such tag.
    public mutating func writeTag(_ field: GesSchemaField, _ tagIndex: Int) throws(GesWireError) {
        try current(field)
        let node = schema.nodes[field.index]
        if node.kind != .tag { throw GesWireError(GesWireError.typeMismatch, field.path) }
        if tagIndex < 0 || UInt64(tagIndex) > node.maxIndex { throw GesWireError(GesWireError.unknownTag, field.path) }
        try presence(node)
        try put(UInt64(tagIndex), node.width)
        leave(node.after)
    }

    /// Writes a `:Flags` field as a mask whose bit `i` sets the member at index `i` in declared order.
    /// - Throws: `GesWireError` when the mask sets a bit beyond the last member.
    public mutating func writeFlags(_ field: GesSchemaField, _ mask: UInt64) throws(GesWireError) {
        try current(field)
        let node = schema.nodes[field.index]
        if node.kind != .flags { throw GesWireError(GesWireError.typeMismatch, field.path) }
        if mask > node.maxIndex { throw GesWireError(GesWireError.unknownTag, field.path) }
        try presence(node)
        try put(mask, node.width)
        leave(node.after)
    }

    /// Writes a Text from its UTF-8 bytes, or a `:Tag max n` from its name without `#`.
    /// - Throws: `GesWireError` when the bytes are too long, not valid UTF-8 or no valid tag name.
    public mutating func writeText(_ field: GesSchemaField, _ utf8: UnsafeRawBufferPointer) throws(GesWireError) {
        try current(field)
        let node = schema.nodes[field.index]
        if node.kind != .text || node.isBytes { throw GesWireError(GesWireError.typeMismatch, field.path) }
        if Int64(utf8.count) > node.maxCount { throw GesWireError(GesWireError.tooLong, field.path) }
        if let code = GesSchemaNumbers.textError(node, utf8) { throw GesWireError(code, field.path) }
        try presence(node)
        try put(UInt64(utf8.count), node.width)
        try bits.writeBytes(utf8)
        leave(node.after)
    }

    /// Writes a `:Bytes max n` field.
    /// - Throws: `GesWireError` when the field is no Bytes or the bytes exceed its maximum.
    public mutating func writeBytes(_ field: GesSchemaField, _ bytes: UnsafeRawBufferPointer) throws(GesWireError) {
        try current(field)
        let node = schema.nodes[field.index]
        if node.kind != .text || !node.isBytes { throw GesWireError(GesWireError.typeMismatch, field.path) }
        if Int64(bytes.count) > node.maxCount { throw GesWireError(GesWireError.tooLong, field.path) }
        try presence(node)
        try put(UInt64(bytes.count), node.width)
        try bits.writeBytes(bytes)
        leave(node.after)
    }

    /// Starts a list with its element count; the elements follow.
    /// - Throws: `GesWireError` when the count exceeds the maximum.
    public mutating func beginList(_ field: GesSchemaField, count: Int) throws(GesWireError) {
        try current(field)
        let node = schema.nodes[field.index]
        if node.kind != .list { throw GesWireError(GesWireError.typeMismatch, field.path) }
        if count < 0 || Int64(count) > node.maxCount { throw GesWireError(GesWireError.tooLong, field.path) }
        try presence(node)
        try put(UInt64(count), node.width)
        if count == 0 { return leave(node.after) }
        lists.push(end: node.after, element: field.index + 1, count: Int64(count))
        cursor = field.index + 1
    }

    /// Enters a nested schema, Point or Vector; its fields follow.
    /// - Throws: `GesWireError` when the field is used out of order.
    public mutating func beginNested(_ field: GesSchemaField) throws(GesWireError) {
        try enter(field, .group)
        let node = schema.nodes[field.index]
        leave(node.next == field.index + 1 ? node.after : field.index + 1)
    }

    /// Selects an alternative of a union and writes its index; the alternative follows, unless it is `:Nothing`.
    /// - Parameters:
    ///   - field: The union's handle.
    ///   - alternative: The alternative's index in declared order.
    /// - Throws: `GesWireError` when the field is no union or the index does not exist.
    public mutating func selectAlternative(_ field: GesSchemaField, _ alternative: Int) throws(GesWireError) {
        try current(field)
        let node = schema.nodes[field.index]
        if node.kind != .union { throw GesWireError(GesWireError.typeMismatch, field.path) }
        if alternative < 0 || UInt64(alternative) > node.maxIndex { throw GesWireError(GesWireError.outOfRange, field.path) }
        try presence(node)
        try put(UInt64(alternative), node.width)
        let selected = GesSchemaNumbers.alternative(schema.nodes, field.index, alternative)
        if schema.nodes[selected].kind == .nothing {
            leave(schema.nodes[selected].after)
        } else {
            cursor = selected
        }
    }

    /// Marks an `optional` field as absent.
    /// - Throws: `GesWireError` when the field is not optional.
    public mutating func writeAbsent(_ field: GesSchemaField) throws(GesWireError) {
        try current(field)
        let node = schema.nodes[field.index]
        if !node.optional { throw GesWireError(GesWireError.missingField, field.path) }
        try put(0, 1)
        leave(node.after)
    }

    /// Ends the record with padding to a byte boundary.
    /// - Returns: The number of bytes written.
    /// - Throws: `GesWireError` when a field is missing or the buffer is too small.
    public mutating func finish() throws(GesWireError) -> Int {
        if schema.kind == .messages { throw GesWireError(GesWireError.missingField) }
        if cursor != schema.nodes.count { throw GesWireError(GesWireError.missingField, schema.infos[cursor].path) }
        return try finishBits()
    }

    var bitPosition: Int { bits.bitPosition }

    mutating func putBits(_ value: UInt64, _ width: Int) throws(GesWireError) { try bits.write(value, width: width) }

    mutating func setBit(_ bit: Int) { bits.setBit(bit) }

    mutating func rewind(_ bit: Int) { bits.rewind(to: bit) }

    mutating func finishBits() throws(GesWireError) -> Int { try bits.finish() }

    private func current(_ field: GesSchemaField) throws(GesWireError) {
        if field.owner !== schema || field.index != cursor || cursor >= schema.nodes.count { throw GesWireError(GesWireError.fieldOrder, field.path) }
    }

    private mutating func enter(_ field: GesSchemaField, _ kind: GesSchemaNodeKind) throws(GesWireError) {
        try current(field)
        if schema.nodes[field.index].kind != kind { throw GesWireError(GesWireError.typeMismatch, field.path) }
        try presence(schema.nodes[field.index])
    }

    private mutating func presence(_ node: GesSchemaNode) throws(GesWireError) {
        if node.optional { try put(1, 1) }
    }

    private mutating func leave(_ next: Int) { cursor = lists.advance(next) }

    private mutating func put(_ value: UInt64, _ width: Int) throws(GesWireError) { try bits.write(value, width: width) }
}

/// Decodes one top-level record or message without allocating on success. Data is untrusted: every limit is checked, and
/// invalid data fails with a `GesWireError`. Fields are read in declaration order. The data must stay valid while the
/// reader is used.
public struct GesSchemaReader {
    private var bits: GesBitReader
    private var schema: GesBoundSchema
    private var lists = GesListFrames()
    private var cursor = 0
    private var present = false

    /// Starts reading a record or message.
    /// - Parameters:
    ///   - schema: The bound schema; for a message set, read the member first.
    ///   - data: The data; it may continue after the record.
    ///   - byteLimit: An optional limit on the bytes of this record.
    public init(_ schema: GesBoundSchema, data: UnsafeRawBufferPointer, byteLimit: Int = .max) {
        self.schema = schema
        bits = GesBitReader(data: data, byteLimit: byteLimit)
    }

    /// Reads which member of a message set follows, and selects it.
    /// - Returns: The member's index in declared order.
    /// - Throws: `GesWireError` when the data is invalid.
    public mutating func readMember() throws(GesWireError) -> Int {
        if schema.kind != .messages { throw GesWireError(GesWireError.typeMismatch) }
        let index = try get(schema.memberWidth, -1)
        if index >= UInt64(schema.members.count) { throw GesWireError(GesWireError.invalidIndex) }
        schema = schema.members[Int(index)]
        return Int(index)
    }

    /// Reads the key, which comes first, before the caller chooses a baseline.
    /// - Returns: The key as an Int64, or the tag index of a Tag key.
    /// - Throws: `GesWireError` when the schema has no key or the data is invalid.
    public mutating func readKey() throws(GesWireError) -> Int64 {
        if cursor != 0 || schema.nodes.isEmpty || !schema.nodes[0].key { throw GesWireError(GesWireError.fieldOrder) }
        let field = GesSchemaField(owner: schema, index: 0)
        return schema.nodes[0].kind == .tag ? Int64(try readTag(field)) : try readInteger(field)
    }

    /// Reads whether an `optional` field is present. An absent field is complete.
    /// - Returns: Whether a value follows.
    /// - Throws: `GesWireError` when the data is invalid.
    public mutating func readPresence(_ field: GesSchemaField) throws(GesWireError) -> Bool {
        try current(field)
        let node = schema.nodes[field.index]
        if !node.optional || present { return true }
        if try get(1, field.index) == 1 {
            present = true
            return true
        }
        leave(node.after)
        return false
    }

    /// Reads a Boolean field or a message tag.
    /// - Throws: `GesWireError` when the data is invalid.
    public mutating func readBoolean(_ field: GesSchemaField) throws(GesWireError) -> Bool {
        try enter(field, .boolean)
        let value = try get(1, field.index) == 1
        leave(schema.nodes[field.index].after)
        return value
    }

    /// Reads an `integer` field.
    /// - Throws: `GesWireError` when the data is invalid.
    public mutating func readInteger(_ field: GesSchemaField) throws(GesWireError) -> Int64 {
        try enter(field, .integer)
        let node = schema.nodes[field.index]
        let index = try get(node.width, field.index)
        if index > node.maxIndex { throw GesWireError(GesWireError.invalidIndex, field.path) }
        leave(node.after)
        return node.hasRange ? Int64(bitPattern: UInt64(bitPattern: node.lower) &+ index) : Int64(bitPattern: index)
    }

    /// Reads a numeric field as a Binary64, without boxing.
    /// - Throws: `GesWireError` when the data is invalid.
    public mutating func readNumber(_ field: GesSchemaField) throws(GesWireError) -> Double {
        try current(field)
        let node = schema.nodes[field.index]
        switch node.kind {
        case .quantized:
            let index = try enteredIndex(field)
            leave(node.after)
            return GesSchemaNumbers.value(node, index).value
        case .float:
            try requirePresence(field)
            let bits = try get(64, field.index)
            let value = Double(bitPattern: bits)
            if value.isNaN || bits == 1 << 63 || (node.hasRange && value.isInfinite) { throw GesWireError(GesWireError.invalidNumber, field.path) }
            if node.hasRange && (value < node.lowerValue || value > node.upperValue) { throw GesWireError(GesWireError.outOfRange, field.path) }
            present = false
            leave(node.after)
            return value
        case .integer:
            return Double(try readInteger(field))
        default:
            throw GesWireError(GesWireError.typeMismatch, field.path)
        }
    }

    /// Reads a quantized field as the multiple of its step's fraction, for the value path: whether its value is an exact
    /// integer, which the Number storage rule keeps as an Int64.
    mutating func readExactNumber(_ field: GesSchemaField) throws(GesWireError) -> (value: Double, integer: Int64?) {
        let node = schema.nodes[field.index]
        let index = try enteredIndex(field)
        leave(node.after)
        let (value, multiple) = GesSchemaNumbers.value(node, index)
        return (value, multiple % node.tenPower == 0 ? multiple / node.tenPower : nil)
    }

    /// Reads the raw wire index of a ranged field, for callers that store quantized values.
    /// - Throws: `GesWireError` when the data is invalid.
    public mutating func readIndex(_ field: GesSchemaField) throws(GesWireError) -> UInt64 {
        try current(field)
        let node = schema.nodes[field.index]
        if node.kind != .integer && node.kind != .quantized && node.kind != .tag { throw GesWireError(GesWireError.typeMismatch, field.path) }
        let index = try enteredIndex(field)
        leave(node.after)
        return index
    }

    /// Reads a tag of a closed set as its index in declared order.
    /// - Throws: `GesWireError` when the data is invalid.
    public mutating func readTag(_ field: GesSchemaField) throws(GesWireError) -> Int {
        try enter(field, .tag)
        let node = schema.nodes[field.index]
        let index = try get(node.width, field.index)
        if index > node.maxIndex { throw GesWireError(GesWireError.invalidIndex, field.path) }
        leave(node.after)
        return Int(index)
    }

    /// Reads a `:Flags` field as a mask whose bit `i` sets the member at index `i` in declared order.
    /// - Throws: `GesWireError` when the data ends early.
    public mutating func readFlags(_ field: GesSchemaField) throws(GesWireError) -> UInt64 {
        try enter(field, .flags)
        let node = schema.nodes[field.index]
        let mask = try get(node.width, field.index)
        leave(node.after)
        return mask
    }

    /// Reads a Text into a caller's buffer as validated UTF-8, or a `:Tag max n` as a validated name without `#`.
    /// - Parameter destination: The buffer; its maximum is the field's `max`.
    /// - Returns: The number of bytes.
    /// - Throws: `GesWireError` when the data is invalid or the buffer too small.
    public mutating func readText(_ field: GesSchemaField, into destination: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {
        try enter(field, .text)
        let node = schema.nodes[field.index]
        if node.isBytes { throw GesWireError(GesWireError.typeMismatch, field.path) }
        let count = try get(node.width, field.index)
        if count > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, field.path) }
        if count > UInt64(destination.count) { throw GesWireError(GesWireError.bufferTooSmall, field.path) }
        let bytes = UnsafeMutableRawBufferPointer(rebasing: destination[0..<Int(count)])
        try bits.readBytes(into: bytes, path: field.path)
        if let code = GesSchemaNumbers.textError(node, UnsafeRawBufferPointer(rebasing: destination[0..<Int(count)])) { throw GesWireError(code, field.path) }
        leave(node.after)
        return Int(count)
    }

    /// Reads a `:Bytes max n` field into a caller's buffer.
    /// - Returns: The number of bytes.
    /// - Throws: `GesWireError` when the field is no Bytes, the data is invalid or the buffer is too small.
    public mutating func readBytes(_ field: GesSchemaField, into destination: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {
        try enter(field, .text)
        let node = schema.nodes[field.index]
        if !node.isBytes { throw GesWireError(GesWireError.typeMismatch, field.path) }
        let count = try get(node.width, field.index)
        if count > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, field.path) }
        if count > UInt64(destination.count) { throw GesWireError(GesWireError.bufferTooSmall, field.path) }
        try bits.readBytes(into: UnsafeMutableRawBufferPointer(rebasing: destination[0..<Int(count)]), path: field.path)
        leave(node.after)
        return Int(count)
    }

    /// Reads Bytes and allocates only what the data holds, for the value path.
    mutating func readBytesValue(_ field: GesSchemaField) throws(GesWireError) -> [UInt8] {
        try enter(field, .text)
        let node = schema.nodes[field.index]
        let count = try get(node.width, field.index)
        if count > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, field.path) }
        try bits.require(Int(count) * 8, path: field.path)
        var bytes = [UInt8](repeating: 0, count: Int(count))
        var index = 0
        while index < bytes.count {
            bytes[index] = UInt8(truncatingIfNeeded: try get(8, field.index))
            index += 1
        }
        leave(node.after)
        return bytes
    }

    /// Reads a Text and allocates only what the data holds, for the rare paths that build a string.
    mutating func readTextValue(_ field: GesSchemaField) throws(GesWireError) -> String {
        try enter(field, .text)
        let node = schema.nodes[field.index]
        let count = try get(node.width, field.index)
        if count > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, field.path) }
        try bits.require(Int(count) * 8, path: field.path)
        var bytes = [UInt8](repeating: 0, count: Int(count))
        var index = 0
        while index < bytes.count {
            bytes[index] = UInt8(truncatingIfNeeded: try get(8, field.index))
            index += 1
        }
        if let code = bytes.withUnsafeBytes({ GesSchemaNumbers.textError(node, $0) }) { throw GesWireError(code, field.path) }
        leave(node.after)
        return String(decoding: bytes, as: UTF8.self)
    }

    /// Starts a list; its elements follow.
    /// - Returns: The element count.
    /// - Throws: `GesWireError` when the data is invalid.
    public mutating func beginList(_ field: GesSchemaField) throws(GesWireError) -> Int {
        try enter(field, .list)
        let node = schema.nodes[field.index]
        let count = try get(node.width, field.index)
        if count > UInt64(node.maxCount) { throw GesWireError(GesWireError.invalidCount, field.path) }
        if count == 0 {
            leave(node.after)
            return 0
        }
        lists.push(end: node.after, element: field.index + 1, count: Int64(count))
        cursor = field.index + 1
        return Int(count)
    }

    /// Enters a nested schema, Point or Vector; its fields follow.
    /// - Throws: `GesWireError` when the data is invalid.
    public mutating func beginNested(_ field: GesSchemaField) throws(GesWireError) {
        try enter(field, .group)
        let node = schema.nodes[field.index]
        leave(node.next == field.index + 1 ? node.after : field.index + 1)
    }

    /// Reads which alternative of a union follows, and selects it. A `:Nothing` alternative is complete.
    /// - Returns: The alternative's index in declared order.
    /// - Throws: `GesWireError` when the field is no union or the data is invalid.
    public mutating func readAlternative(_ field: GesSchemaField) throws(GesWireError) -> Int {
        try enter(field, .union)
        let node = schema.nodes[field.index]
        let index = try get(node.width, field.index)
        if index > node.maxIndex { throw GesWireError(GesWireError.invalidIndex, field.path) }
        let selected = GesSchemaNumbers.alternative(schema.nodes, field.index, Int(index))
        if schema.nodes[selected].kind == .nothing {
            leave(schema.nodes[selected].after)
        } else {
            cursor = selected
        }
        return Int(index)
    }

    /// Ends the record and checks its padding.
    /// - Returns: The bytes the record occupies; further records may follow them.
    /// - Throws: `GesWireError` when a field was not read or the padding is invalid.
    public mutating func finish() throws(GesWireError) -> Int {
        if schema.kind == .messages { throw GesWireError(GesWireError.fieldOrder) }
        if cursor != schema.nodes.count { throw GesWireError(GesWireError.fieldOrder, schema.infos[cursor].path) }
        return try finishBits()
    }

    /// The bit position of the next value, for diagnostics.
    var bitPosition: Int { bits.bitPosition }

    mutating func getBits(_ width: Int, _ nodeIndex: Int) throws(GesWireError) -> UInt64 { try get(width, nodeIndex) }

    // Fails like reading when the data ends before these bits do.
    func require(_ bits: Int, _ nodeIndex: Int) throws(GesWireError) {
        try self.bits.require(bits, path: nodeIndex >= 0 ? schema.infos[nodeIndex].path : "")
    }

    mutating func seek(_ bit: Int) { bits.seek(to: bit) }

    mutating func finishBits() throws(GesWireError) -> Int { try bits.finish() }

    private mutating func enteredIndex(_ field: GesSchemaField) throws(GesWireError) -> UInt64 {
        try requirePresence(field)
        present = false
        let node = schema.nodes[field.index]
        let index = try get(node.width, field.index)
        if index > node.maxIndex { throw GesWireError(GesWireError.invalidIndex, field.path) }
        return index
    }

    private func requirePresence(_ field: GesSchemaField) throws(GesWireError) {
        if schema.nodes[field.index].optional && !present { throw GesWireError(GesWireError.fieldOrder, field.path) }
    }

    private func current(_ field: GesSchemaField) throws(GesWireError) {
        if field.owner !== schema || field.index != cursor || cursor >= schema.nodes.count { throw GesWireError(GesWireError.fieldOrder, field.path) }
    }

    private mutating func enter(_ field: GesSchemaField, _ kind: GesSchemaNodeKind) throws(GesWireError) {
        try current(field)
        if schema.nodes[field.index].kind != kind { throw GesWireError(GesWireError.typeMismatch, field.path) }
        try requirePresence(field)
        present = false
    }

    private mutating func leave(_ next: Int) { cursor = lists.advance(next) }

    // The path is looked up only for an error. A local reference to the schema avoids an overlapping access to `self`
    // while `bits` is mutated; a `do`/`catch` that adds the path instead measured 40 % slower on deltas.
    private mutating func get(_ width: Int, _ nodeIndex: Int) throws(GesWireError) -> UInt64 {
        let schema = self.schema
        return try bits.read(width, path: nodeIndex >= 0 ? schema.infos[nodeIndex].path : "")
    }
}
