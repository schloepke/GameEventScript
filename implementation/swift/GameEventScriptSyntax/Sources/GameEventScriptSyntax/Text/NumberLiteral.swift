// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The kinds of a recognized number text.
@_spi(Syntax) public enum GesNumberLiteralKind: UInt8, Sendable {
    case invalid
    // NaN, or a percentage outside the binary64 range: recognized, but the value nothing.
    case nothing
    case integer
    case float
    case percentage
}

/// A recognized number text such as `1_000`, `2.5km` or `90%`: exact Int64 values, binary64 values rounded once from
/// their decimal digits, and percentage ratios. GES grammar, exact Int64 conversion and decimal percentage scaling are
/// GES rules; Swift's decimal conversion is used only after grammar validation for binary64 rounding. Runtime turns it
/// into a value; documents only test it.
@_spi(Syntax) public struct GesNumberLiteral: Sendable {
    @_spi(Syntax) public let kind: GesNumberLiteralKind
    @_spi(Syntax) public let integerValue: Int64
    /// The binary64 value, or the ratio of a percentage.
    @_spi(Syntax) public let floatValue: Double
    @_spi(Syntax) public let unit: GesQuantityUnit

    static let invalid = Self(kind: .invalid, integerValue: 0, floatValue: 0, unit: .none)
    static let nothingValue = Self(kind: .nothing, integerValue: 0, floatValue: 0, unit: .none)

    @_spi(Syntax) public var isValid: Bool { kind != .invalid }

    private static func integer(_ value: Int64, unit: GesQuantityUnit) -> Self { Self(kind: .integer, integerValue: value, floatValue: 0, unit: unit) }

    private static func float(_ value: Double, unit: GesQuantityUnit) -> Self { Self(kind: .float, integerValue: 0, floatValue: value, unit: unit) }

    private static func percentage(_ ratio: Double) -> Self { Self(kind: .percentage, integerValue: 0, floatValue: ratio, unit: .none) }

    private static func trim(_ input: String) -> String {
        let scalars = input.unicodeScalars
        var start = scalars.startIndex
        var end = scalars.endIndex
        while start < end && (scalars[start] == " " || scalars[start] == "\t") { scalars.formIndex(after: &start) }
        while start < end {
            let previous = scalars.index(before: end)
            if scalars[previous] != " " && scalars[previous] != "\t" { break }
            end = previous
        }
        return String(scalars[start..<end])
    }

    @_spi(Syntax) public static func read(_ input: String, percentage: Bool = false, allowGrouping: Bool = true, percentageMagnitude: Bool = false) -> GesNumberLiteral {
        var text = trim(input)
        guard !text.isEmpty else { return .invalid }
        var unit: GesQuantityUnit = .none
        // A unit suffix scales the decimal value by factor × 10^exponent; a percentage by 10^-2.
        var shift = percentageMagnitude || text.hasSuffix("%") ? -2 : 0
        var factor = 1
        if text.hasSuffix("%") {
            text.removeLast()
        } else if let suffix = possibleSuffixEnd(text) ? trailingSuffix(text) : nil {
            unit = suffix.unit
            shift = suffix.exponent
            factor = suffix.factor
            text = String(String.UnicodeScalarView(text.unicodeScalars.dropLast(suffix.scalars.count)))
        }
        if percentage && unit != .none || text.isEmpty { return .invalid }
        if text == "NaN" { return .nothingValue }
        let bytes = Array(text.utf8)
        var position = 0
        let negative = bytes[0] == 45
        if bytes[0] == 43 || bytes[0] == 45 { position += 1 }
        if position == bytes.count { return .invalid }
        if bytes[position...].elementsEqual("Infinity".utf8) { return percentage ? .nothingValue : float(negative ? -.infinity : .infinity, unit: unit) }
        let digitsStart = position
        var underscores = false

        func digit(_ byte: UInt8) -> Bool { (48...57).contains(byte) }

        func readDigits() -> Int {
            var count = 0
            while position < bytes.count && digit(bytes[position]) {
                count += 1
                position += 1
                if position + 1 < bytes.count && bytes[position] == 95 && digit(bytes[position + 1]) {
                    underscores = true
                    position += 1
                }
            }
            return count
        }

        let integerDigits = readDigits()
        if integerDigits == 0 { return .invalid }
        let grouping = position < bytes.count && bytes[position] == 44
        if grouping {
            if !allowGrouping || integerDigits > 3 { return .invalid }
            while position < bytes.count && bytes[position] == 44 {
                position += 1
                if readDigits() != 3 { return .invalid }
            }
        }
        var fractionalDigits = 0
        if position < bytes.count && bytes[position] == 46 {
            position += 1
            fractionalDigits = readDigits()
            if fractionalDigits == 0 { return .invalid }
        }
        let mantissaEnd = position
        var exponent: Int64 = 0
        if position < bytes.count && (bytes[position] == 101 || bytes[position] == 69) {
            position += 1
            let exponentNegative = position < bytes.count && bytes[position] == 45
            if position < bytes.count && (bytes[position] == 45 || bytes[position] == 43) { position += 1 }
            let start = position
            if readDigits() == 0 { return .invalid }
            for byte in bytes[start..<position] where digit(byte) { exponent = min(1 << 50, exponent * 10 + Int64(byte - 48)) }
            if exponentNegative { exponent = -exponent }
        }
        if position != bytes.count || grouping && underscores { return .invalid }
        // Collect the mantissa digits; a non-decimal factor multiplies them exactly before any rounding.
        var digits = bytes[digitsStart..<mantissaEnd].filter(digit)
        if factor != 1 { multiplyDecimal(&digits, by: factor) }
        guard let first = digits.firstIndex(where: { $0 != 48 }), let last = digits.lastIndex(where: { $0 != 48 }) else { return percentage ? Self.percentage(0) : integer(0, unit: unit) }
        let power = exponent - Int64(fractionalDigits) + Int64(digits.count - last - 1) + Int64(shift)
        if !percentage && power >= 0 && Int64(last - first + 1) + power <= 19 {
            var magnitude: UInt64 = 0
            for byte in digits[first...last] { magnitude = magnitude * 10 + UInt64(byte - 48) }
            for _ in 0..<Int(power) { magnitude *= 10 }
            if magnitude <= (negative ? UInt64(1) << 63 : UInt64(Int64.max)) { return integer(Int64(bitPattern: negative ? 0 &- magnitude : magnitude), unit: unit) }
        }
        // Scaling adjusts the decimal exponent before the single binary64 rounding.
        let decimal: String
        if !underscores && !grouping && shift == 0 && factor == 1 { decimal = text } else { decimal = (negative ? "-" : "") + String(decoding: digits, as: UTF8.self) + "e" + String(exponent - Int64(fractionalDigits) + Int64(shift)) }
        guard let number = Double(decimal) else { return .invalid }
        return percentage ? number.isFinite ? Self.percentage(number) : .nothingValue : float(number, unit: unit)
    }

    // The longest unit suffix spelling ending the text; it belongs to the number only when it directly follows a digit
    // or the special values, so `NaN` keeps its trailing N.
    // Only these scalars end a unit spelling, so plain numbers and other text skip the suffix table. A native test keeps
    // the switch in step with the suffix table.
    @_spi(Syntax) public static func possibleSuffixEnd(_ text: String) -> Bool {
        let scalars = text.unicodeScalars
        switch scalars.last {
        case "s", "m", "h", "z", "g", "N", "J", "W", "V", "A", "K", "°", "²": return true
        case "2": return scalars.dropLast().last == "^"
        default: return false
        }
    }

    // Compares backwards over the scalar view, so matching allocates nothing.
    private static func trailingSuffix(_ text: String) -> GesQuantityUnit.Suffix? {
        let scalars = text.unicodeScalars
        for suffix in GesQuantityUnit.suffixes {
            var index = scalars.endIndex
            var matched = true
            for expected in suffix.scalars.reversed() {
                guard index > scalars.startIndex else {
                    matched = false
                    break
                }
                index = scalars.index(before: index)
                if scalars[index] != expected {
                    matched = false
                    break
                }
            }
            guard matched, index > scalars.startIndex else { continue }
            let remainder = scalars[..<index]
            if let last = remainder.last, ("0"..."9").contains(last) { return suffix }
            let unsigned = remainder.first == "+" || remainder.first == "-" ? remainder.dropFirst() : remainder[...]
            if unsigned.elementsEqual("Infinity".unicodeScalars) || remainder.elementsEqual("NaN".unicodeScalars) { return suffix }
            return nil
        }
        return nil
    }

    // Multiplies ASCII decimal digits in place by a small positive factor.
    private static func multiplyDecimal(_ digits: inout [UInt8], by factor: Int) {
        var carry = 0
        for index in digits.indices.reversed() {
            let product = Int(digits[index] - 48) * factor + carry
            digits[index] = UInt8(48 + product % 10)
            carry = product / 10
        }
        var prefix: [UInt8] = []
        while carry > 0 {
            prefix.insert(UInt8(48 + carry % 10), at: 0)
            carry /= 10
        }
        digits.insert(contentsOf: prefix, at: 0)
    }
}
