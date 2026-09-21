// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Explicit signed-64 and binary64 conversion rules shared by runtime values.
public enum GesNumber {
    /// Returns an exact signed-64 value only when the finite input is integral and in range.
    public static func exactInteger(_ value: Double) -> Int64? {
        guard value.isFinite, value >= -9223372036854775808.0, value < 9223372036854775808.0, value.rounded(.towardZero) == value else { return nil }
        return Int64(value)
    }

    /// Truncates toward zero and saturates instead of trapping on overflow or special values.
    public static func saturatedInteger(_ value: Double) -> Int64 {
        if value.isNaN { return 0 }
        if value >= 9223372036854775808.0 { return .max }
        if value < -9223372036854775808.0 { return .min }
        return Int64(value)
    }

    /// Canonicalizes both IEEE zero encodings to positive zero.
    public static func canonicalZero(_ value: Double) -> Double { value == 0 ? 0 : value }

    /// Writes a shortest round-trippable decimal with the portable exponent and special-value spellings.
    public static func format(_ value: Double) -> String {
        if value.isNaN { return "NaN" }
        if value == .infinity { return "Infinity" }
        if value == -.infinity { return "-Infinity" }
        if value == 0 { return "0" }
        let text = String(value)
        let parts = text.split(separator: "e", omittingEmptySubsequences: false)
        var significand = String(parts[0])
        if significand.hasSuffix(".0") { significand.removeLast(2) }
        guard parts.count == 2, let exponent = Int(parts[1]) else { return significand }
        return significand + "e" + String(exponent)
    }

    /// Formats a ratio as Percentage without performing a rounded or overflowing multiplication by 100.
    /// Nonfinite inputs follow value normalization: NaN becomes Nothing and infinities become Numbers.
    public static func formatPercentage(_ ratio: Double) -> String {
        if ratio.isNaN { return "nothing" }
        if !ratio.isFinite { return format(ratio) }
        if ratio == 0 { return "0%" }
        let text = exactInteger(ratio).map(String.init) ?? format(ratio)
        let exponentParts = text.split(separator: "e")
        if exponentParts.count == 2, let exponent = Int(exponentParts[1]) { return String(exponentParts[0]) + "e" + String(exponent + 2) + "%" }
        let negative = text.first == "-"
        let magnitude = negative ? String(text.dropFirst()) : text
        let parts = magnitude.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 2 else { return text + "00%" }
        var digits = Array((String(parts[0]) + String(parts[1])).utf8)
        let point = parts[0].utf8.count + 2
        while digits.count < point { digits.append(48) }
        var integer = Array(digits.prefix(point))
        while integer.count > 1 && integer.first == 48 { integer.removeFirst() }
        var fraction = Array(digits.dropFirst(point))
        while fraction.last == 48 { fraction.removeLast() }
        return (negative ? "-" : "") + String(decoding: integer, as: UTF8.self) + (fraction.isEmpty ? "" : "." + String(decoding: fraction, as: UTF8.self)) + "%"
    }
}
