// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) public enum GesNames {
    @_spi(Syntax) public static func lower(_ c: UInt8) -> Bool { (97...122).contains(c) }

    @_spi(Syntax) public static func upper(_ c: UInt8) -> Bool { (65...90).contains(c) }

    @_spi(Syntax) public static func digit(_ c: UInt8) -> Bool { (48...57).contains(c) }

    @_spi(Syntax) public static func alnum(_ c: UInt8) -> Bool { lower(c) || upper(c) || digit(c) }

    @_spi(Syntax) public static func plain(_ text: String) -> Bool {
        let bytes = Array(text.utf8)
        return bytes.first.map(lower) == true && bytes.dropFirst().allSatisfy(alnum)
    }

    @_spi(Syntax) public static func type(_ text: String) -> Bool {
        let bytes = Array(text.utf8)
        return bytes.first.map(upper) == true && bytes.dropFirst().allSatisfy(alnum)
    }

    @_spi(Syntax) public static func message(_ text: String) -> Bool { type(text) || text == "initialization" || text == "undeliverable" }

    @_spi(Syntax) public static func identifier(_ text: String) -> Bool {
        let parts = text.split(separator: "_", omittingEmptySubsequences: false)
        if parts.count == 1 { return plain(text) }
        guard parts.count == 2, plain(String(parts[0])), !parts[1].isEmpty else { return false }
        return (parts[1] == "0" || parts[1].utf8.first != 48) && parts[1].utf8.allSatisfy(digit)
    }

    @_spi(Syntax) public static func module(_ text: String) -> Bool {
        if text.isEmpty { return false }
        return text.split(separator: ".", omittingEmptySubsequences: false).allSatisfy { part in part.utf8.first.map(lower) == true && part.utf8.allSatisfy { lower($0) || digit($0) } }
    }

    @_spi(Syntax) public static func extensionName(_ text: String) -> Bool {
        let parts = text.split(separator: ".", omittingEmptySubsequences: false)
        return parts.count == 2 && parts.allSatisfy { plain(String($0)) }
    }
}
