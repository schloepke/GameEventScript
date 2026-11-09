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

    private static func steps(_ value: Double, _ tenPower: Double, _ numerator: Double, _ path: @autoclosure () -> String) throws(GesWireError) -> Int64 {
        let quotient = value * tenPower / numerator
        if quotient > largestSteps || quotient < -largestSteps { throw GesWireError(GesWireError.outOfRange, path()) }
        return Int64(quotient.rounded(.toNearestOrEven))
    }
}
