// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Unicode-scalar operations used by the portable value and identifier models.
/// Swift's canonical-equivalence string comparison is deliberately not used.
public enum GesText {
    /// Compares the exact scalar sequence, without Unicode normalization.
    public static func scalarEqual(_ left: String, _ right: String) -> Bool {
        left.unicodeScalars.elementsEqual(right.unicodeScalars)
    }

    /// Orders text lexicographically by Unicode scalar value.
    public static func scalarLess(_ left: String, _ right: String) -> Bool {
        left.unicodeScalars.lexicographicallyPrecedes(right.unicodeScalars) { $0.value < $1.value }
    }

    /// Hashes the exact scalar sequence; the hash is process-local, not transport data.
    public static func hashScalars(_ text: String, into hasher: inout Hasher) {
        hasher.combine(text.unicodeScalars.count)
        for scalar in text.unicodeScalars { hasher.combine(scalar.value) }
    }

    /// Whether text is a lowercase-starting ASCII field, tag, or argument label.
    public static func isLowerName(_ text: String) -> Bool {
        var bytes = text.utf8.makeIterator()
        guard let first = bytes.next(), first >= 97, first <= 122 else { return false }
        while let byte = bytes.next() {
            if !(byte >= 65 && byte <= 90 || byte >= 97 && byte <= 122 || byte >= 48 && byte <= 57) {
                return false
            }
        }
        return true
    }

    /// Whether text is a PascalCase-starting ASCII type or message name.
    public static func isUpperName(_ text: String) -> Bool {
        var bytes = text.utf8.makeIterator()
        guard let first = bytes.next(), first >= 65, first <= 90 else { return false }
        while let byte = bytes.next() {
            if !(byte >= 65 && byte <= 90 || byte >= 97 && byte <= 122 || byte >= 48 && byte <= 57) {
                return false
            }
        }
        return true
    }

    /// Quotes a GES text literal by doubling embedded double quotes.
    public static func quoted(_ text: String) -> String {
        var result = "\""
        for scalar in text.unicodeScalars {
            if scalar == "\"" { result.append("\"") }
            result.unicodeScalars.append(scalar)
        }
        result.append("\"")
        return result
    }
}
