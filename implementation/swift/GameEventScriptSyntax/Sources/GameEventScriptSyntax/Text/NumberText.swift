// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Values of decimal number tokens, such as `1_000.5`, for the syntax rules that test a literal's value. Number tokens
// have no sign and no exponent; digit separators carry no value.
enum GesNumberText {
    // Whether a percentage token, such as `5%`, stays within the binary64 range as a ratio.
    static func isFinitePercentage(_ text: String) -> Bool {
        var digits = digitsOnly(text)
        if digits.last == "%" { digits.removeLast() }
        return Double(digits + "e-2")?.isFinite ?? false
    }

    // The integer of a token that is exactly an integer from 1 to Int32.max, such as `6` or `6.0`.
    static func positiveInt32(_ text: String) -> Int? {
        var digits = Substring(digitsOnly(text))
        if let point = digits.firstIndex(of: ".") {
            guard digits[digits.index(after: point)...].allSatisfy({ $0 == "0" }) else { return nil }
            digits = digits[..<point]
        }
        digits = digits.drop { $0 == "0" }
        guard !digits.isEmpty, digits.count <= 10, let value = Int(digits), value <= Int(Int32.max) else { return nil }
        return value
    }

    private static func digitsOnly(_ text: String) -> String { text.contains("_") ? String(text.filter { $0 != "_" }) : text }
}
