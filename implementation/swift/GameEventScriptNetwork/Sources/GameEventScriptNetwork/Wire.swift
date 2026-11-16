// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Constants and value codecs of the network wire format.
enum GesNetworkWire {
    static let minimumPacketBytes = 1200
    static let maximumPacketBytes = 65535
    static let maximumTokenBytes = 512
    static let maximumDetailBytes = 512
    static let maximumCodeBytes = 64
    static let maximumRoutes = 64
    static let version: UInt32 = 1

    static let hello: UInt8 = 1
    static let welcome: UInt8 = 3
    static let close: UInt8 = 4
    static let data: UInt8 = 5

    // The magic of a hello: "GESN".
    static let magic: UInt32 = 0x4e53_4547

    static func varintSize(_ value: UInt32) -> Int {
        var value = value
        var size = 1
        while value >= 0x80 {
            value >>= 7
            size += 1
        }
        return size
    }

    static func writeVarint(_ target: UnsafeMutableRawBufferPointer, _ position: Int, _ value: UInt32) -> Int {
        var value = value
        var position = position
        while value >= 0x80 {
            target[position] = UInt8(truncatingIfNeeded: value | 0x80)
            position += 1
            value >>= 7
        }
        target[position] = UInt8(truncatingIfNeeded: value)
        return position + 1
    }

    // Reads a canonical varint. Returns 1 on success, 0 when the bytes end first, -1 when it is not canonical or too long.
    static func readVarint(_ source: UnsafeRawBufferPointer, _ position: inout Int, _ value: inout UInt32) -> Int {
        value = 0
        let start = position
        for index in 0..<5 {
            if start + index >= source.count { return 0 }
            let current = source[start + index]
            if index == 4 && current > 0x0f { return -1 }
            value |= UInt32(current & 0x7f) << UInt32(7 * index)
            if current & 0x80 == 0 {
                if current == 0 && index > 0 { return -1 }
                position = start + index + 1
                return 1
            }
        }
        return -1
    }

    static func writeUInt16(_ target: UnsafeMutableRawBufferPointer, _ position: Int, _ value: UInt16) {
        target[position] = UInt8(truncatingIfNeeded: value)
        target[position + 1] = UInt8(truncatingIfNeeded: value >> 8)
    }

    static func readUInt16(_ source: UnsafeRawBufferPointer, _ position: Int) -> UInt16 {
        UInt16(source[position]) | UInt16(source[position + 1]) << 8
    }

    static func writeUInt32(_ target: UnsafeMutableRawBufferPointer, _ position: Int, _ value: UInt32) {
        target[position] = UInt8(truncatingIfNeeded: value)
        target[position + 1] = UInt8(truncatingIfNeeded: value >> 8)
        target[position + 2] = UInt8(truncatingIfNeeded: value >> 16)
        target[position + 3] = UInt8(truncatingIfNeeded: value >> 24)
    }

    static func readUInt32(_ source: UnsafeRawBufferPointer, _ position: Int) -> UInt32 {
        UInt32(source[position]) | UInt32(source[position + 1]) << 8 | UInt32(source[position + 2]) << 16 | UInt32(source[position + 3]) << 24
    }

    static func writeUInt64(_ target: UnsafeMutableRawBufferPointer, _ position: Int, _ value: UInt64) {
        writeUInt32(target, position, UInt32(truncatingIfNeeded: value))
        writeUInt32(target, position + 4, UInt32(truncatingIfNeeded: value >> 32))
    }

    static func readUInt64(_ source: UnsafeRawBufferPointer, _ position: Int) -> UInt64 {
        UInt64(readUInt32(source, position)) | UInt64(readUInt32(source, position + 4)) << 32
    }

    // The UTF-8 bytes of a detail, shortened to whole scalars of at most 512 bytes.
    static func detailBytes(_ detail: String) -> [UInt8] {
        let bytes = Array(detail.utf8)
        if bytes.count <= maximumDetailBytes { return bytes }
        var length = maximumDetailBytes
        while bytes[length] & 0xc0 == 0x80 { length -= 1 }
        return Array(bytes[0..<length])
    }

    // Whether the bytes are well-formed UTF-8: no overlong forms, surrogates or scalars above U+10FFFF.
    static func isValidUtf8(_ bytes: UnsafeRawBufferPointer) -> Bool {
        var index = 0
        while index < bytes.count {
            let lead = bytes[index]
            if lead < 0x80 {
                index += 1
                continue
            }
            let count: Int
            var low: UInt8 = 0x80
            var high: UInt8 = 0xbf
            if lead >= 0xc2 && lead <= 0xdf {
                count = 1
            } else if lead >= 0xe0 && lead <= 0xef {
                count = 2
                if lead == 0xe0 { low = 0xa0 } else if lead == 0xed { high = 0x9f }
            } else if lead >= 0xf0 && lead <= 0xf4 {
                count = 3
                if lead == 0xf0 { low = 0x90 } else if lead == 0xf4 { high = 0x8f }
            } else {
                return false
            }
            if index + count >= bytes.count { return false }
            let second = bytes[index + 1]
            if second < low || second > high { return false }
            var next = 2
            while next <= count {
                if bytes[index + next] & 0xc0 != 0x80 { return false }
                next += 1
            }
            index += count + 1
        }
        return true
    }
}
