// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// GES grammar, exact Int64 conversion, and decimal percentage scaling.
/// Swift's decimal conversion is used only after grammar validation for binary64 rounding.
enum TextNumberCast {
    static func read(
        _ input: String, percentage: Bool = false, allowGrouping: Bool = true, percentageMagnitude: Bool = false
    ) -> GesValue? {
        var text = MessageNames.trim(input)
        guard !text.isEmpty else { return nil }
        var unit: GesUnit = .none
        let scaled = percentageMagnitude || text.hasSuffix("%")
        if text.hasSuffix("%") {
            text.removeLast()
        } else if text.hasSuffix("m") {
            unit = .meter
            text.removeLast()
        } else if text.hasSuffix("s") {
            unit = .second
            text.removeLast()
        } else if text.hasSuffix("°") {
            unit = .degree
            text.removeLast()
        }
        if percentage && unit != .none || text.isEmpty { return nil }
        if text == "NaN" { return .nothing }
        let bytes = Array(text.utf8)
        var position = 0
        let negative = bytes[0] == 45
        if bytes[0] == 43 || bytes[0] == 45 { position += 1 }
        if position == bytes.count { return nil }
        if bytes[position...].elementsEqual("Infinity".utf8) {
            return percentage ? .nothing : .float(negative ? -.infinity : .infinity, unit: unit)
        }
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
        if integerDigits == 0 { return nil }
        let grouping = position < bytes.count && bytes[position] == 44
        if grouping {
            if !allowGrouping || integerDigits > 3 { return nil }
            while position < bytes.count && bytes[position] == 44 {
                position += 1
                if readDigits() != 3 { return nil }
            }
        }
        var fractionalDigits = 0
        if position < bytes.count && bytes[position] == 46 {
            position += 1
            fractionalDigits = readDigits()
            if fractionalDigits == 0 { return nil }
        }
        let mantissaEnd = position
        var exponent: Int64 = 0
        if position < bytes.count && (bytes[position] == 101 || bytes[position] == 69) {
            position += 1
            let exponentNegative = position < bytes.count && bytes[position] == 45
            if position < bytes.count && (bytes[position] == 45 || bytes[position] == 43) { position += 1 }
            let start = position
            if readDigits() == 0 { return nil }
            for byte in bytes[start..<position] where digit(byte) {
                exponent = min(1 << 50, exponent * 10 + Int64(byte - 48))
            }
            if exponentNegative { exponent = -exponent }
        }
        if position != bytes.count || grouping && underscores { return nil }
        let digits = bytes[digitsStart..<mantissaEnd].filter(digit)
        guard let first = digits.firstIndex(where: { $0 != 48 }), let last = digits.lastIndex(where: { $0 != 48 })
        else { return percentage ? .percentage(0) : .integer(0, unit: unit) }
        let power = exponent - Int64(fractionalDigits) + Int64(digits.count - last - 1) - (scaled ? 2 : 0)
        if !percentage && power >= 0 && Int64(last - first + 1) + power <= 19 {
            var magnitude: UInt64 = 0
            for byte in digits[first...last] { magnitude = magnitude * 10 + UInt64(byte - 48) }
            for _ in 0..<Int(power) { magnitude *= 10 }
            if magnitude <= (negative ? UInt64(1) << 63 : UInt64(Int64.max)) {
                return .integer(Int64(bitPattern: negative ? 0 &- magnitude : magnitude), unit: unit)
            }
        }
        let decimal: String
        if !underscores && !grouping && !scaled {
            decimal = text
        } else {
            decimal =
                String(decoding: bytes[..<mantissaEnd].filter { $0 != 95 && $0 != 44 }, as: UTF8.self) + "e"
                + String(exponent - (scaled ? 2 : 0))
        }
        guard let number = Double(decimal) else { return nil }
        return percentage ? number.isFinite ? .percentage(number) : .nothing : .float(number, unit: unit)
    }
    static func percentage(_ value: GesValue) -> GesValue {
        if value.kind == .text { return read(value.textValue!, percentage: true) ?? .nothing }
        if value.kind == .percentage { return value }
        return !value.hasUnit && value.asNumber.isFinite ? .percentage(value.asNumber) : .nothing
    }
}
