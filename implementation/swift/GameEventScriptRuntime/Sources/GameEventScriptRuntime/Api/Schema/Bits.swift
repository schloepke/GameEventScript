// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Writes values of 0 to 64 bits into a caller's buffer in the bit order of the schema wire format: least significant bit
/// first, bytes in Little Endian order. It knows no schema; generated schema code and `GesSchemaWriter` build on it.
/// Writing allocates nothing.
///
/// Bits are collected in a 64-bit word that is stored once it is full, so a buffer of the record's byte count suffices; a
/// full buffer fails with `wire.bufferTooSmall`. A ``counting()`` writer stores nothing and only counts, so that a caller
/// learns the exact byte count of a record before it writes it. The buffer must stay valid while the writer is used, and a
/// copy of the writer holds its own pending bits, so only one copy may continue writing.
public struct GesBitWriter {
    private let buffer: UnsafeMutableRawBufferPointer
    private let isCounting: Bool
    private var required = 0
    private var position = 0
    private var pending: UInt64 = 0
    private var pendingBits = 0

    /// Starts writing at the beginning of a buffer.
    /// - Parameter buffer: The buffer that receives the bits.
    public init(into buffer: UnsafeMutableRawBufferPointer) {
        self.buffer = buffer
        isCounting = false
    }

    private init(counting: Bool) {
        buffer = UnsafeMutableRawBufferPointer(start: nil, count: 0)
        isCounting = counting
    }

    /// Creates a writer that stores nothing: it accepts every write, and ``finish()`` returns the byte count that a buffer
    /// needs for the same writes.
    public static func counting() -> GesBitWriter { GesBitWriter(counting: true) }

    /// For a ``counting()`` writer, the bytes a buffer needs for the writes so far, including bits that ``rewind(to:)`` took
    /// back; it is at least the count ``finish()`` returns. A writer over a buffer reports 0.
    public var requiredByteCount: Int { required }

    /// The number of bits written so far.
    public var bitPosition: Int { position * 8 + pendingBits }

    /// Writes the low `width` bits of a value.
    /// - Parameters:
    ///   - value: The value; its bits above `width` must be zero.
    ///   - width: The number of bits, from 0 to 64.
    /// - Throws: `GesWireError` with `wire.bufferTooSmall` when the buffer is full.
    public mutating func write(_ value: UInt64, width: Int) throws(GesWireError) {
        if width == 0 { return }
        pending |= value << UInt64(pendingBits)
        let total = pendingBits + width
        if total < 64 {
            pendingBits = total
            return
        }
        // A counting writer has no buffer, so only this branch, which a full buffer takes anyway, asks for it.
        if position + 8 <= buffer.count {
            buffer.storeBytes(of: pending.littleEndian, toByteOffset: position, as: UInt64.self)
        } else if !isCounting {
            throw GesWireError(GesWireError.bufferTooSmall)
        } else if position + 8 > required {
            required = position + 8
        }
        position += 8
        pendingBits = total - 64
        pending = pendingBits == 0 ? 0 : value >> UInt64(width - pendingBits)
    }

    /// Writes bytes of eight bits each, as the bytes of a Text are written.
    /// - Parameter bytes: The bytes.
    /// - Throws: `GesWireError` with `wire.bufferTooSmall` when the buffer is full.
    public mutating func writeBytes(_ bytes: UnsafeRawBufferPointer) throws(GesWireError) {
        if isCounting { return skip(bytes.count) }
        var index = 0
        while index < bytes.count {
            try write(UInt64(bytes[index]), width: 8)
            index += 1
        }
    }

    /// Writes the bytes of an array, eight bits each, as generated code writes the stored bytes of a Text or Bytes field.
    /// - Parameter bytes: The bytes.
    /// - Throws: `GesWireError` with `wire.bufferTooSmall` when the buffer is full.
    public mutating func writeBytes(_ bytes: [UInt8]) throws(GesWireError) {
        if isCounting { return skip(bytes.count) }
        for byte in bytes { try write(UInt64(byte), width: 8) }
    }

    // Only the count matters, so a counting writer skips the bytes at once.
    private mutating func skip(_ count: Int) {
        let total = pendingBits + count * 8
        position += (total >> 6) * 8
        if position > required { required = position }
        pendingBits = total & 63
        pending = 0
    }

    /// Sets a bit that was already written as zero, for example a flag that is known only later.
    /// - Parameter bit: The bit's position, below `bitPosition`.
    /// - Precondition: `bit` is not negative; raw buffer access checks no bounds in release builds, so a negative position traps.
    public mutating func setBit(_ bit: Int) {
        precondition(bit >= 0, "A bit position is not negative.")
        let flushed = position * 8
        if bit >= flushed {
            pending |= 1 << UInt64(bit - flushed)
        } else if !isCounting {
            buffer[bit >> 3] |= UInt8(1) << UInt8(bit & 7)
        }
    }

    /// Goes back to an earlier position; the bits from there on are discarded and written again.
    /// - Parameter bit: The position, at most `bitPosition`.
    /// - Precondition: `bit` is not negative; a negative position traps.
    public mutating func rewind(to bit: Int) {
        precondition(bit >= 0, "A bit position is not negative.")
        // Whole words are flushed, so the word that holds the position can be read back.
        let word = (bit >> 6) << 3
        if word < position { pending = isCounting ? 0 : UInt64(littleEndian: buffer.loadUnaligned(fromByteOffset: word, as: UInt64.self)) }
        position = word
        pendingBits = bit & 63
        pending = pendingBits == 0 ? 0 : pending & ((UInt64(1) << UInt64(pendingBits)) - 1)
    }

    /// Pads the bits with zeros to a byte boundary and stores them.
    /// - Returns: The number of bytes written.
    /// - Throws: `GesWireError` with `wire.bufferTooSmall` when the buffer is full.
    public mutating func finish() throws(GesWireError) -> Int {
        let bytes = (pendingBits + 7) / 8
        if isCounting {
            position += bytes
            if position > required { required = position }
            pendingBits = 0
            return position
        }
        if position + bytes > buffer.count { throw GesWireError(GesWireError.bufferTooSmall) }
        var index = 0
        while index < bytes {
            buffer[position] = UInt8(truncatingIfNeeded: pending)
            position += 1
            pending >>= 8
            index += 1
        }
        pendingBits = 0
        return position
    }
}

/// Reads values of 0 to 64 bits from untrusted data in the bit order of the schema wire format: least significant bit
/// first, bytes in Little Endian order. It knows no schema; generated schema code and `GesSchemaReader` build on it.
/// Reading allocates nothing.
///
/// Every read checks the end of the data. Reading past it fails with `wire.truncated`, or with `wire.limitExceeded` when the
/// byte limit ends before the data does. The path passed to a read names the field in that error and is evaluated only
/// then. The data must stay valid while the reader is used.
public struct GesBitReader {
    private let data: UnsafeRawBufferPointer
    private let bitCount: Int
    private let limited: Bool

    /// The position of the next bit.
    public private(set) var bitPosition = 0

    /// How many bits remain before the data or its byte limit ends, for example to read many values without checking each one.
    public var remainingBits: Int { bitCount - bitPosition }

    /// Starts reading at the beginning of the data.
    /// - Parameters:
    ///   - data: The data; it may continue after the record.
    ///   - byteLimit: An optional limit on the bytes that may be read.
    public init(data: UnsafeRawBufferPointer, byteLimit: Int = .max) {
        self.data = data
        bitCount = min(data.count, byteLimit) * 8
        limited = data.count > byteLimit
    }

    /// Reads a value of `width` bits.
    /// - Parameters:
    ///   - width: The number of bits, from 0 to 64.
    ///   - path: The field path for an error, such as `position.x`; empty for the record as a whole.
    /// - Returns: The value, with all bits above `width` zero.
    /// - Throws: `GesWireError` when the data ends before the value does.
    public mutating func read(_ width: Int, path: @autoclosure () -> String) throws(GesWireError) -> UInt64 {
        if width == 0 { return 0 }
        if bitPosition + width > bitCount { throw GesWireError(end, path()) }
        let start = bitPosition >> 3
        let shift = bitPosition & 7
        bitPosition += width
        var value: UInt64
        if start + 8 <= data.count {
            value = UInt64(littleEndian: data.loadUnaligned(fromByteOffset: start, as: UInt64.self)) >> UInt64(shift)
            if shift + width > 64 { value |= UInt64(data[start + 8]) << UInt64(64 - shift) }
        } else {
            value = 0
            let bytes = (shift + width + 7) / 8
            var index = 0
            while index < bytes && index < 8 {
                value |= UInt64(data[start + index]) << UInt64(8 * index)
                index += 1
            }
            value >>= UInt64(shift)
            if bytes > 8 { value |= UInt64(data[start + 8]) << UInt64(64 - shift) }
        }
        return width == 64 ? value : value & ((1 << UInt64(width)) - 1)
    }

    /// Reads bytes of eight bits each, as the bytes of a Text are read.
    /// - Parameters:
    ///   - destination: The buffer that receives exactly its count in bytes.
    ///   - path: The field path for an error.
    /// - Throws: `GesWireError` when the data ends before the bytes do; nothing is read then.
    public mutating func readBytes(into destination: UnsafeMutableRawBufferPointer, path: @autoclosure () -> String) throws(GesWireError) {
        try require(destination.count * 8, path: path())
        var index = 0
        while index < destination.count {
            destination[index] = UInt8(truncatingIfNeeded: try read(8, path: ""))
            index += 1
        }
    }

    /// Reads bytes of eight bits each into an array, which keeps its capacity, as generated code reads a Text or Bytes field.
    /// - Parameters:
    ///   - count: The number of bytes; the array holds exactly them afterwards.
    ///   - storage: The array that receives the bytes.
    ///   - path: The field path for an error.
    /// - Throws: `GesWireError` when the data ends before the bytes do; nothing is read and the array is unchanged then.
    public mutating func readBytes(_ count: Int, into storage: inout [UInt8], path: @autoclosure () -> String) throws(GesWireError) {
        try require(count * 8, path: path())
        storage.removeAll(keepingCapacity: true)
        let start = bitPosition >> 3
        if bitPosition & 7 == 0 {
            // Byte-aligned bytes are copied at once.
            storage.append(contentsOf: UnsafeRawBufferPointer(rebasing: data[start..<(start + count)]))
            bitPosition += count * 8
            return
        }
        storage.reserveCapacity(count)
        var index = 0
        while index < count {
            storage.append(UInt8(truncatingIfNeeded: try read(8, path: "")))
            index += 1
        }
    }

    /// Fails like a read when fewer than `bits` bits remain, without reading them.
    /// - Parameters:
    ///   - bits: The number of bits that must remain.
    ///   - path: The field path for an error.
    /// - Throws: `GesWireError` when the data ends before these bits do.
    public func require(_ bits: Int, path: @autoclosure () -> String) throws(GesWireError) {
        if bitPosition + bits > bitCount { throw GesWireError(end, path()) }
    }

    /// Moves to a position, for example back to a value that was read before.
    /// - Parameter bit: The position, from 0 to the end of the data.
    /// - Precondition: `bit` is not negative; a negative position traps, since reads check only the end of the data.
    public mutating func seek(to bit: Int) {
        precondition(bit >= 0, "A bit position is not negative.")
        bitPosition = bit
    }

    /// Reads the padding to the next byte boundary, which must be zero.
    /// - Returns: The number of bytes read; further records may follow them.
    /// - Throws: `GesWireError` with `wire.invalidPadding` when a padding bit is set.
    public mutating func finish() throws(GesWireError) -> Int {
        let rest = (8 - bitPosition % 8) % 8
        if rest > 0, try read(rest, path: "") != 0 { throw GesWireError(GesWireError.invalidPadding) }
        return bitPosition / 8
    }

    private var end: String { limited ? GesWireError.limitExceeded : GesWireError.truncated }
}
