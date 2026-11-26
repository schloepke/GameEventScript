// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A SHA-1 digest of 20 bytes, such as a handle's `#sha1` version; equality and order follow the bytes.
public struct GesHash20: Hashable, Comparable, Sendable, Codable, CustomStringConvertible {
    // Big-endian words, so that comparing the words compares the bytes in order.
    private let first: UInt64
    private let second: UInt64
    private let third: UInt32

    /// The number of bytes of the digest.
    public static let length = 20

    /// Creates a digest from exactly 20 bytes; nil for another length.
    public init?(bytes: [UInt8]) {
        guard bytes.count == Self.length else { return nil }
        first = GesHashBytes.read(bytes, 0, 8)
        second = GesHashBytes.read(bytes, 8, 8)
        third = UInt32(GesHashBytes.read(bytes, 16, 4))
    }

    /// Reads a digest from 40 hexadecimal digits in either case; nil for anything else.
    public init?(hex: String) {
        guard let bytes = GesHashBytes.parseHex(hex, count: Self.length) else { return nil }
        self.init(bytes: bytes)
    }

    /// The 20 bytes of the digest.
    public var bytes: [UInt8] {
        var result: [UInt8] = []
        result.reserveCapacity(Self.length)
        GesHashBytes.write(first, 8, into: &result)
        GesHashBytes.write(second, 8, into: &result)
        GesHashBytes.write(UInt64(third), 4, into: &result)
        return result
    }

    /// The digest as 40 lowercase hexadecimal digits.
    public var description: String { GesHashBytes.hex(bytes) }

    /// Orders digests by their bytes.
    public static func < (left: Self, right: Self) -> Bool { (left.first, left.second, left.third) < (right.first, right.second, right.third) }

    /// Decodes a digest from its lowercase or uppercase hexadecimal text.
    ///
    /// - Throws: `DecodingError.dataCorrupted` for text that is not 40 hexadecimal digits.
    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        guard let hash = Self(hex: try container.decode(String.self)) else { throw DecodingError.dataCorruptedError(in: container, debugDescription: "A GesHash20 has 40 hexadecimal digits.") }
        self = hash
    }

    /// Encodes the digest as its lowercase hexadecimal text.
    public func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(description)
    }
}

/// A SHA-256 digest of 32 bytes, such as a handle's `#sha256` or content-hash version; equality and order follow the
/// bytes.
public struct GesHash32: Hashable, Comparable, Sendable, Codable, CustomStringConvertible {
    // Big-endian words, so that comparing the words compares the bytes in order.
    private let first: UInt64
    private let second: UInt64
    private let third: UInt64
    private let fourth: UInt64

    /// The number of bytes of the digest.
    public static let length = 32

    /// Creates a digest from exactly 32 bytes; nil for another length.
    public init?(bytes: [UInt8]) {
        guard bytes.count == Self.length else { return nil }
        first = GesHashBytes.read(bytes, 0, 8)
        second = GesHashBytes.read(bytes, 8, 8)
        third = GesHashBytes.read(bytes, 16, 8)
        fourth = GesHashBytes.read(bytes, 24, 8)
    }

    /// Reads a digest from 64 hexadecimal digits in either case; nil for anything else.
    public init?(hex: String) {
        guard let bytes = GesHashBytes.parseHex(hex, count: Self.length) else { return nil }
        self.init(bytes: bytes)
    }

    /// The 32 bytes of the digest.
    public var bytes: [UInt8] {
        var result: [UInt8] = []
        result.reserveCapacity(Self.length)
        GesHashBytes.write(first, 8, into: &result)
        GesHashBytes.write(second, 8, into: &result)
        GesHashBytes.write(third, 8, into: &result)
        GesHashBytes.write(fourth, 8, into: &result)
        return result
    }

    /// The digest as 64 lowercase hexadecimal digits.
    public var description: String { GesHashBytes.hex(bytes) }

    /// Orders digests by their bytes.
    public static func < (left: Self, right: Self) -> Bool {
        (left.first, left.second, left.third, left.fourth) < (right.first, right.second, right.third, right.fourth)
    }

    /// Decodes a digest from its lowercase or uppercase hexadecimal text.
    ///
    /// - Throws: `DecodingError.dataCorrupted` for text that is not 64 hexadecimal digits.
    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        guard let hash = Self(hex: try container.decode(String.self)) else { throw DecodingError.dataCorruptedError(in: container, debugDescription: "A GesHash32 has 64 hexadecimal digits.") }
        self = hash
    }

    /// Encodes the digest as its lowercase hexadecimal text.
    public func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(description)
    }
}

enum GesHashBytes {
    static func read(_ bytes: [UInt8], _ offset: Int, _ count: Int) -> UInt64 {
        var value: UInt64 = 0
        for index in 0..<count { value = (value << 8) | UInt64(bytes[offset + index]) }
        return value
    }

    static func write(_ value: UInt64, _ count: Int, into bytes: inout [UInt8]) {
        for index in stride(from: count - 1, through: 0, by: -1) { bytes.append(UInt8(truncatingIfNeeded: value >> UInt64(8 * index))) }
    }

    static func parseHex(_ hex: String, count: Int) -> [UInt8]? {
        let digits = Array(hex.utf8)
        guard digits.count == count * 2 else { return nil }
        var bytes: [UInt8] = []
        bytes.reserveCapacity(count)
        for index in 0..<count {
            guard let high = digit(digits[2 * index]), let low = digit(digits[2 * index + 1]) else { return nil }
            bytes.append(high << 4 | low)
        }
        return bytes
    }

    static func hex(_ bytes: [UInt8]) -> String {
        var text = ""
        appendHex(bytes, to: &text)
        return text
    }

    static func appendHex(_ bytes: [UInt8], to text: inout String) {
        let digits = Array("0123456789abcdef".unicodeScalars)
        for byte in bytes {
            text.unicodeScalars.append(digits[Int(byte >> 4)])
            text.unicodeScalars.append(digits[Int(byte & 15)])
        }
    }

    private static func digit(_ byte: UInt8) -> UInt8? {
        switch byte {
        case 48...57: byte - 48
        case 97...102: byte - 87
        case 65...70: byte - 55
        default: nil
        }
    }
}
