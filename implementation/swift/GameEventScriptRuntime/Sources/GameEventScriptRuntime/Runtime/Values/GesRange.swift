// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An immutable signed-64 range descriptor. Terms are computed lazily without binary64 conversion.
public struct GesIntegerRange: Hashable {
    /// First term of the range.
    public let from: Int64
    /// Inclusive endpoint bound; it need not itself be a generated term.
    public let to: Int64
    /// Signed increment; zero or a direction incompatible with the bounds gives an empty range.
    public let step: Int64
    private let cachedCount: Int64

    /// Creates a lazy range descriptor without enumerating its terms. The default step is one.
    public init(from: Int64, to: Int64, step: Int64 = 1) {
        self.from = from
        self.to = to
        self.step = step
        cachedCount = Self.computeCount(from: from, to: to, step: step)
    }

    /// Inclusive length, saturated at Int64.max.
    public var count: Int64 { cachedCount }

    private static func computeCount(from: Int64, to: Int64, step: Int64) -> Int64 {
        guard step != 0, step > 0 ? from <= to : from >= to else { return 0 }
        let distance = step > 0 ? UInt64(bitPattern: to) &- UInt64(bitPattern: from) : UInt64(bitPattern: from) &- UInt64(bitPattern: to)
        let magnitude = step > 0 ? UInt64(step) : 0 &- UInt64(bitPattern: step)
        let zeroBasedCount = distance / magnitude
        return zeroBasedCount >= UInt64(Int64.max) ? .max : Int64(zeroBasedCount + 1)
    }

    /// Returns the one-based language range term, or absence for an invalid index.
    public func term(at oneBasedIndex: Int64) -> Int64? {
        guard oneBasedIndex > 0, oneBasedIndex <= count else { return nil }
        let magnitude = step > 0 ? UInt64(step) : 0 &- UInt64(bitPattern: step)
        let product = UInt64(oneBasedIndex - 1).multipliedReportingOverflow(by: magnitude)
        guard !product.overflow else { return nil }
        let bits = step > 0 ? UInt64(bitPattern: from) &+ product.partialValue : UInt64(bitPattern: from) &- product.partialValue
        let result = Int64(bitPattern: bits)
        return (step > 0 ? result >= from && result <= to : result <= from && result >= to) ? result : nil
    }

    /// Exact membership without materializing the range.
    public func contains(_ value: Int64) -> Bool {
        guard step != 0 else { return false }
        if step > 0 { return value >= from && value <= to && (UInt64(bitPattern: value) &- UInt64(bitPattern: from)) % UInt64(step) == 0 }
        return value <= from && value >= to && (UInt64(bitPattern: from) &- UInt64(bitPattern: value)) % (0 &- UInt64(bitPattern: step)) == 0
    }
}

/// An immutable binary64 range descriptor; multiplication and addition define each lazy term separately.
public struct GesFloatRange: Hashable {
    /// First term of the range.
    public let from: Double
    /// Inclusive endpoint bound; it need not itself be a generated term.
    public let to: Double
    /// Signed increment; zero or a direction incompatible with the bounds gives an empty range.
    public let step: Double
    private let cachedCount: Int64

    /// Creates a lazy range descriptor without enumerating its terms. The default step is one.
    public init(from: Double, to: Double, step: Double = 1) {
        self.from = GesNumber.canonicalZero(from)
        self.to = GesNumber.canonicalZero(to)
        self.step = GesNumber.canonicalZero(step)
        cachedCount = Self.computeCount(from: self.from, to: self.to, step: self.step)
    }

    /// Inclusive length, saturated at Int64.max; invalid descriptors have length zero.
    public var count: Int64 { cachedCount }

    private static func computeCount(from: Double, to: Double, step: Double) -> Int64 {
        guard from.isFinite, to.isFinite, step.isFinite, step != 0, step > 0 ? from <= to : from >= to else { return 0 }
        let distance = step > 0 ? (to - from) / step : (from - to) / -step
        if distance >= 9223372036854775808.0 { return .max }
        return Int64(distance.rounded(.down)) + 1
    }

    /// Returns the one-based language range term, or absence for an invalid index.
    public func term(at oneBasedIndex: Int64) -> Double? {
        guard oneBasedIndex > 0, oneBasedIndex <= count else { return nil }
        let offset = step * Double(oneBasedIndex - 1)
        let value = from + offset
        return GesNumber.canonicalZero(step > 0 ? min(value, to) : max(value, to))
    }

    /// Exact membership in the generated binary64 sequence, without a tolerance or enumeration.
    public func contains(_ value: Double) -> Bool {
        let length = count
        guard value.isFinite, length > 0, step > 0 ? value >= from && value <= to : value <= from && value >= to else { return false }
        var low: Int64 = 0
        var high = length - 1
        while low <= high {
            let index = low + (high - low) / 2
            guard let term = term(at: index + 1) else { return false }
            if term == value { return true }
            if step > 0 ? term < value : term > value { low = index + 1 } else { high = index - 1 }
        }
        return false
    }
}

extension GesFloatRange {
    // Whether `slice` has exactly the terms [start, start + count) of this non-empty Range, last first when reversed,
    // as IsExactFloatSlice of C#. A prefix keeps `from` and `step`, so its terms are computed identically and only its
    // length needs checking; other slices also require that every term of this Range is exact (isExactGrid), so that
    // terms counted from another origin cannot round differently.
    func isExactSlice(start: Int64, count: Int64, reversed: Bool, _ slice: GesFloatRange) -> Bool {
        if slice.count != count { return false }
        return start == 0 && !reversed || isExactGrid
    }

    // Every term from + step * i is exact and unclamped: `from` and `step` are integer multiples of a common power of
    // two whose largest multiple among the terms stays below 2^53, and the last term does not pass `to`.
    private var isExactGrid: Bool {
        let significandLimit: Int64 = 1 << 53
        let (stepOdd, stepExponent) = Self.oddSignificand(step)
        let (fromOdd, fromExponent) = from == 0 ? (0, stepExponent) : Self.oddSignificand(from)
        let exponent = min(stepExponent, fromExponent)
        guard let stepUnits = Self.scale(stepOdd, stepExponent - exponent), let fromUnits = Self.scale(fromOdd, fromExponent - exponent) else { return false }
        if count - 1 > (significandLimit - 1 - fromUnits) / stepUnits { return false }
        let last = from + step * Double(count - 1)
        return step > 0 ? last <= to : last >= to
    }

    // The odd integer significand of a finite non-zero binary64 magnitude and the exponent of its lowest set bit.
    private static func oddSignificand(_ value: Double) -> (Int64, Int) {
        let bits = value.bitPattern & 0x7FFF_FFFF_FFFF_FFFF
        let biased = Int(bits >> 52)
        var significand = Int64(bits & 0x000F_FFFF_FFFF_FFFF)
        if biased != 0 { significand |= 1 << 52 }
        var exponent = (biased == 0 ? 1 : biased) - 1075
        while significand & 1 == 0 {
            significand >>= 1
            exponent += 1
        }
        return (significand, exponent)
    }

    // odd * 2^shift as an integer below 2^53.
    private static func scale(_ odd: Int64, _ shift: Int) -> Int64? {
        if shift >= 53 || odd >> (53 - shift) != 0 { return nil }
        return odd << shift
    }
}
