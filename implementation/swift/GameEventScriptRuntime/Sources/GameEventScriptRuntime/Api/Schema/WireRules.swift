// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The value rules of the schema wire format over plain operands: Text and tag validity, the exact quantization of a step
/// `p / 10^k` and wrapping. Generated schema code calls them with constant operands, and the interpreted writer and reader
/// use the same rules, so both encode identical bytes.
public enum GesWireRules {
    // 2^50, the largest step count of a quantized value.
    private static let largestSteps = 1_125_899_906_842_624.0

    /// Returns whether bytes are well-formed UTF-8 without surrogates or overlong forms.
    /// - Parameter bytes: The bytes.
    public static func isValidUTF8(_ bytes: UnsafeRawBufferPointer) -> Bool {
        var index = 0
        while index < bytes.count {
            let first = bytes[index]
            if first < 0x80 {
                index += 1
                continue
            }
            var more = 0
            var least = 0
            var value = 0
            if first >= 0xC2 && first <= 0xDF {
                (more, least, value) = (1, 0x80, Int(first & 0x1F))
            } else if first >= 0xE0 && first <= 0xEF {
                (more, least, value) = (2, 0x800, Int(first & 0x0F))
            } else if first >= 0xF0 && first <= 0xF4 {
                (more, least, value) = (3, 0x10000, Int(first & 0x07))
            } else {
                return false
            }
            if index + more >= bytes.count { return false }
            var offset = 1
            while offset <= more {
                let next = bytes[index + offset]
                if next & 0xC0 != 0x80 { return false }
                value = (value << 6) | Int(next & 0x3F)
                offset += 1
            }
            if value < least || value > 0x10FFFF || (value >= 0xD800 && value <= 0xDFFF) { return false }
            index += more + 1
        }
        return true
    }

    /// Returns whether bytes are a tag name without `#`: a lowercase ASCII letter, then ASCII letters and digits.
    /// - Parameter bytes: The bytes.
    public static func isTagName(_ bytes: UnsafeRawBufferPointer) -> Bool {
        if bytes.isEmpty || !(0x61...0x7A).contains(bytes[0]) { return false }
        var index = 1
        while index < bytes.count {
            let byte = bytes[index]
            if !((0x30...0x39).contains(byte) || (0x41...0x5A).contains(byte) || (0x61...0x7A).contains(byte)) { return false }
            index += 1
        }
        return true
    }

    /// Returns the index of a value in a closed quantized range.
    /// - Parameters:
    ///   - value: The value.
    ///   - lowerValue: The decoded value of the lower bound.
    ///   - upperValue: The decoded value of the upper bound.
    ///   - tenPower: The binary64 value of `10^k` of the step `p / 10^k`.
    ///   - numerator: The binary64 value of `p` of the step.
    ///   - lower: The lower bound in steps.
    ///   - path: The field path for an error, evaluated only then.
    /// - Throws: `GesWireError` with `wire.outOfRange` when the value is not finite or lies outside the range.
    public static func quantizedIndex(
        _ value: Double,
        lowerValue: Double,
        upperValue: Double,
        tenPower: Double,
        numerator: Double,
        lower: Int64,
        path: @autoclosure () -> String
    ) throws(GesWireError) -> UInt64 {
        if !value.isFinite || value < lowerValue || value > upperValue { throw GesWireError(GesWireError.outOfRange, path()) }
        return UInt64(bitPattern: try steps(value, tenPower, numerator, path()) &- lower)
    }

    /// Returns the index of a value in a wrapping quantized range, which reduces it into its period.
    /// - Parameters:
    ///   - value: The value.
    ///   - tenPower: The binary64 value of `10^k` of the step `p / 10^k`.
    ///   - numerator: The binary64 value of `p` of the step.
    ///   - lower: The lower bound in steps.
    ///   - period: The number of steps in one period.
    ///   - path: The field path for an error, evaluated only then.
    /// - Throws: `GesWireError` with `wire.outOfRange` when the value is not finite or exceeds 2^50 steps.
    public static func wrappedQuantizedIndex(
        _ value: Double,
        tenPower: Double,
        numerator: Double,
        lower: Int64,
        period: UInt64,
        path: @autoclosure () -> String
    ) throws(GesWireError) -> UInt64 {
        if !value.isFinite { throw GesWireError(GesWireError.outOfRange, path()) }
        return wrappedIndex(try steps(value, tenPower, numerator, path()), lower: lower, period: period)
    }

    /// Returns the value of a quantized index, `(index + lower) * p / 10^k`, exact as the format requires.
    /// - Parameters:
    ///   - index: The index, below the size of the range.
    ///   - lower: The lower bound in steps.
    ///   - numerator: The integer `p` of the step `p / 10^k`.
    ///   - tenPower: The binary64 value of `10^k`.
    public static func quantizedValue(_ index: UInt64, lower: Int64, numerator: Int64, tenPower: Double) -> Double {
        Double((Int64(bitPattern: index) &+ lower) &* numerator) / tenPower
    }

    /// Returns the index of an integer in a wrapping range, reduced into its period.
    /// - Parameters:
    ///   - value: The value.
    ///   - lower: The lower bound.
    ///   - period: The number of values in one period.
    public static func wrappedIndex(_ value: Int64, lower: Int64, period: UInt64) -> UInt64 {
        if value >= lower { return (UInt64(bitPattern: value) &- UInt64(bitPattern: lower)) % period }
        let remainder = (UInt64(bitPattern: lower) &- UInt64(bitPattern: value)) % period
        return remainder == 0 ? 0 : period - remainder
    }

    /// Writes a changed index of a field with `delta within`: its difference from the baseline's index in the range form when
    /// that is at most `deltaRange`, otherwise the full index after a fallback bit. A wrapping field takes the shorter way
    /// around its period.
    /// - Parameters:
    ///   - writer: The writer.
    ///   - index: The record's index, different from `held`.
    ///   - held: The baseline's index.
    ///   - maxIndex: The largest index; a wrapping field's period is one more.
    ///   - wrapping: Whether the field wraps.
    ///   - deltaRange: The largest difference of the range form.
    ///   - deltaWidth: The width of the range form.
    ///   - width: The width of a full index.
    /// - Throws: `GesWireError` with `wire.bufferTooSmall` when the buffer is full.
    public static func writeDeltaRange(
        _ writer: inout GesBitWriter,
        index: UInt64,
        held: UInt64,
        maxIndex: UInt64,
        wrapping: Bool,
        deltaRange: UInt64,
        deltaWidth: Int,
        width: Int
    ) throws(GesWireError) {
        let (difference, negative) = self.difference(index, held, maxIndex, wrapping)
        if difference > deltaRange {
            try writer.write(1, width: 1)
            try writer.write(index, width: width)
            return
        }
        try writer.write(0, width: 1)
        try writer.write(negative ? deltaRange - difference : deltaRange + difference, width: deltaWidth)
    }

    /// Reads a changed index of a field with `delta within` against the baseline's index, and rejects every form that is not
    /// the canonical one.
    /// - Parameters:
    ///   - reader: The reader.
    ///   - held: The baseline's index.
    ///   - maxIndex: The largest index; a wrapping field's period is one more.
    ///   - wrapping: Whether the field wraps.
    ///   - deltaRange: The largest difference of the range form.
    ///   - deltaWidth: The width of the range form.
    ///   - width: The width of a full index.
    ///   - path: The field path for an error, evaluated only then.
    /// - Returns: The record's index.
    /// - Throws: `GesWireError` when the data ends early, a full index is invalid (`wire.invalidIndex`), or the form is not
    ///   canonical or leaves the range (`wire.invalidDelta`).
    public static func readDeltaRange(
        _ reader: inout GesBitReader,
        held: UInt64,
        maxIndex: UInt64,
        wrapping: Bool,
        deltaRange: UInt64,
        deltaWidth: Int,
        width: Int,
        path: @autoclosure () -> String
    ) throws(GesWireError) -> UInt64 {
        if try reader.read(1, path: path()) == 1 {
            let value = try reader.read(width, path: path())
            if value > maxIndex { throw GesWireError(GesWireError.invalidIndex, path()) }
            if value == held || difference(value, held, maxIndex, wrapping).magnitude <= deltaRange { throw GesWireError(GesWireError.invalidDelta, path()) }
            return value
        }
        let range = try reader.read(deltaWidth, path: path())
        if range > deltaRange * 2 || range == deltaRange { throw GesWireError(GesWireError.invalidDelta, path()) }
        if range > deltaRange {
            let forward = range - deltaRange
            if wrapping { return forward >= maxIndex + 1 - held ? held - (maxIndex + 1 - forward) : held + forward }
            if forward > maxIndex - held { throw GesWireError(GesWireError.invalidDelta, path()) }
            return held + forward
        }
        let backward = deltaRange - range
        if backward <= held { return held - backward }
        if !wrapping { throw GesWireError(GesWireError.invalidDelta, path()) }
        return maxIndex + 1 - (backward - held)
    }

    // The difference of two indices without overflow; a wrapping field takes the shorter way, and an exact half period is negative.
    static func difference(_ index: UInt64, _ baseline: UInt64, _ maxIndex: UInt64, _ wrapping: Bool) -> (magnitude: UInt64, negative: Bool) {
        if !wrapping { return index < baseline ? (baseline - index, true) : (index - baseline, false) }
        let period = maxIndex + 1
        let forward = index >= baseline ? index - baseline : period - (baseline - index)
        return forward >= period - period / 2 ? (period - forward, true) : (forward, false)
    }

    private static func steps(_ value: Double, _ tenPower: Double, _ numerator: Double, _ path: @autoclosure () -> String) throws(GesWireError) -> Int64 {
        let quotient = value * tenPower / numerator
        if quotient > largestSteps || quotient < -largestSteps { throw GesWireError(GesWireError.outOfRange, path()) }
        return Int64(quotient.rounded(.toNearestOrEven))
    }
}
