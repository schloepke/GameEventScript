// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesJson {
    static func error(_ code: String = "message.invalidValue") -> GameEventScriptMessageFormatError { .init(code: code) }

    static func object(_ fields: [(String, GesValue)]) -> GesValue { .map(fields.map { .init(key: $0.0, value: $0.1) }) }

    static func write(_ value: GesValue) throws -> String {
        var output = ""
        var items = 0
        var length = 0

        func append(_ text: String) throws {
            length += text.utf16.count
            if length > 4_194_304 { throw error("message.resourceLimit") }
            output += text
        }

        func quoted(_ text: String) throws {
            if text.utf16.count > 4_194_304 { throw error("message.resourceLimit") }
            try append("\"")
            for scalar in text.unicodeScalars {
                switch scalar.value {
                case 34, 92: try append("\\" + String(scalar))
                case 0..<32:
                    let hex = String(scalar.value, radix: 16)
                    try append("\\u" + String(repeating: "0", count: 4 - hex.count) + hex)
                default: try append(String(scalar))
                }
            }
            try append("\"")
        }

        func emit(_ value: GesValue, _ depth: Int) throws {
            items += 1
            if depth > 256 || items > 524288 { throw error("message.resourceLimit") }
            switch value.kind {
            case .nothing: try append("null")
            case .boolean: try append(value.asBoolean ? "true" : "false")
            case .integer: try append(String(value.asInteger))
            case .text: try quoted(value.textValue!)
            case .list:
                try append("[")
                for (index, value) in value.asList.enumerated() {
                    if index > 0 { try append(",") }
                    try emit(value, depth + 1)
                }
                try append("]")
            case .map:
                try append("{")
                for (index, entry) in value.mapEntries!.enumerated() {
                    if index > 0 { try append(",") }
                    try quoted(entry.key)
                    try append(":")
                    try emit(entry.value, depth + 1)
                }
                try append("}")
            default: throw error()
            }
        }

        try emit(value, 0)
        return output
    }

    static func read(_ text: String) throws -> GesValue {
        if text.utf16.count > 4_194_304 { throw error("message.resourceLimit") }
        var parser = Parser(bytes: Array(text.utf8))
        let result = try parser.value(0)
        parser.space()
        if parser.position != parser.bytes.count { throw error("message.invalidJson") }
        return result
    }

    private struct Parser {
        let bytes: [UInt8]
        var position = 0, items = 0

        mutating func space() { while position < bytes.count && [9, 10, 13, 32].contains(bytes[position]) { position += 1 } }

        mutating func take(_ byte: UInt8) -> Bool {
            if position == bytes.count || bytes[position] != byte { return false }
            position += 1
            return true
        }

        mutating func value(_ depth: Int) throws -> GesValue {
            items += 1
            if depth > 256 || items > 524288 { throw error("message.resourceLimit") }
            space()
            if position == bytes.count { throw error("message.invalidJson") }
            if bytes[position] == 34 { return .text(try string()) }
            if take(91) {
                var values: [GesValue] = []
                space()
                if take(93) { return .list([]) }
                repeat {
                    values.append(try value(depth + 1))
                    space()
                    if take(93) { return .list(values) }
                } while take(44)
                throw error("message.invalidJson")
            }
            if take(123) {
                var entries: [GesMapEntry] = []
                var names = Set<GesValue>()
                space()
                if take(125) { return .map([]) }
                repeat {
                    space()
                    let name = try string()
                    if !names.insert(.text(name)).inserted { throw error("message.invalidJson") }
                    space()
                    if !take(58) { throw error("message.invalidJson") }
                    entries.append(.init(key: name, value: try value(depth + 1)))
                    space()
                    if take(125) { return .map(entries) }
                } while take(44)
                throw error("message.invalidJson")
            }
            for token in ["true", "false", "null"] where bytes[position...].starts(with: token.utf8) {
                position += token.utf8.count
                return token == "null" ? .nothing : .boolean(token == "true")
            }
            let start = position
            _ = take(45)
            if !take(48) {
                if position == bytes.count || !(49...57).contains(bytes[position]) { throw error("message.invalidJson") }
                while position < bytes.count && (48...57).contains(bytes[position]) { position += 1 }
            }
            guard let number = Int64(String(decoding: bytes[start..<position], as: UTF8.self)) else { throw error("message.invalidJson") }
            return .integer(number)
        }

        mutating func hex() throws -> UInt32 {
            if position + 4 > bytes.count { throw error("message.invalidJson") }
            guard let value = UInt32(String(decoding: bytes[position..<(position + 4)], as: UTF8.self), radix: 16) else { throw error("message.invalidJson") }
            position += 4
            return value
        }

        mutating func string() throws -> String {
            if !take(34) { throw error("message.invalidJson") }
            var output: [UInt8] = []
            while position < bytes.count {
                let byte = bytes[position]
                position += 1
                if byte == 34 { return String(decoding: output, as: UTF8.self) }
                if byte < 32 { throw error("message.invalidJson") }
                if byte != 92 {
                    output.append(byte)
                    continue
                }
                if position == bytes.count { throw error("message.invalidJson") }
                let escaped = bytes[position]
                position += 1
                switch escaped {
                case 34, 92, 47: output.append(escaped)
                case 98: output.append(8)
                case 102: output.append(12)
                case 110: output.append(10)
                case 114: output.append(13)
                case 116: output.append(9)
                case 117:
                    var code = try hex()
                    if (0xD800...0xDBFF).contains(code) {
                        guard take(92), take(117) else { throw error("message.invalidJson") }
                        let low = try hex()
                        if !(0xDC00...0xDFFF).contains(low) { throw error("message.invalidJson") }
                        code = 0x10000 + ((code - 0xD800) << 10) + low - 0xDC00
                    }
                    guard let scalar = Unicode.Scalar(code) else { throw error("message.invalidJson") }
                    output.append(contentsOf: String(scalar).utf8)
                default: throw error("message.invalidJson")
                }
            }
            throw error("message.invalidJson")
        }
    }
}
