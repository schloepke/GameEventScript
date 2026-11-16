// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The hexadecimal text of Bytes values, as in `:Bytes("a0b7 f3")`: pairs of hexadecimal digits in either case,
/// optionally separated by single ASCII spaces. The canonical form writes lowercase pairs without spaces.
@_spi(Syntax) public enum GesBytesText {
    private static let digits = Array("0123456789abcdef".utf8)

    /// The canonical source and literal text of a Bytes value.
    @_spi(Syntax) public static func format(_ bytes: [UInt8]) -> String {
        var text = Array(":Bytes(\"".utf8)
        text.reserveCapacity(bytes.count * 2 + 10)
        for byte in bytes {
            text.append(digits[Int(byte >> 4)])
            text.append(digits[Int(byte & 0xF)])
        }
        text.append(contentsOf: "\")".utf8)
        return String(decoding: text, as: UTF8.self)
    }

    /// The bytes of hexadecimal text, or nil when a pair is incomplete, a digit is invalid or a space does not separate
    /// two pairs.
    @_spi(Syntax) public static func parse(_ text: String) -> [UInt8]? {
        let scalars = Array(text.unicodeScalars)
        var bytes: [UInt8] = []
        var index = 0
        while index < scalars.count {
            if scalars[index] == " " {
                if index == 0 || index + 1 == scalars.count || scalars[index + 1] == " " { return nil }
                index += 1
                continue
            }
            guard index + 1 < scalars.count, let high = GesNumberLiteral.hexDigit(scalars[index]), let low = GesNumberLiteral.hexDigit(scalars[index + 1]) else { return nil }
            bytes.append(UInt8(high << 4 | low))
            index += 2
        }
        return bytes
    }
}
