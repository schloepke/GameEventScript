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

    @_spi(Syntax) public static func type(_ text: String) -> Bool { GesText.isUpperName(text) }

    // The end of an uppercase name, from the end of its first segment. Any other `_` is not part of the name.
    static func upperNameEnd(_ bytes: [UInt8], _ start: Int) -> Int {
        var position = start
        while position + 1 < bytes.count && bytes[position] == 95 && upper(bytes[position + 1]) {
            position += 2
            while position < bytes.count && alnum(bytes[position]) { position += 1 }
        }
        return position
    }

    @_spi(Syntax) public static func message(_ text: String) -> Bool { type(text) || systemEndpoint(text) }

    /// The system endpoints `initialization` and `undeliverable`, which only the host delivers.
    @_spi(Syntax) public static func systemEndpoint(_ text: String) -> Bool { text == "initialization" || text == "undeliverable" }

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
