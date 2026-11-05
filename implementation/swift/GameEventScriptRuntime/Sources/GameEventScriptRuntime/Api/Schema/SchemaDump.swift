// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// The readable form of encoded data.
public struct GesSchemaDumpResult: Sendable {
    /// One line per field. The first invalid field ends the text with its error, field path and bit offset.
    public let text: String
    /// Whether the data was a valid encoding.
    public let isValid: Bool
}

/// Makes binary data readable through a bound schema.
public enum GesSchemaDump {
    /// Lists each field of an encoded record with its path, bit offset, width and decoded value.
    /// - Returns: The text and whether the data was valid.
    public static func read(_ schema: GesBoundSchema, data: [UInt8]) -> GesSchemaDumpResult {
        data.withUnsafeBytes { bytes in
            var output = ""
            var reader = GesSchemaReader(schema, data: bytes)
            do throws(GesWireError) {
                var current = schema
                if schema.kind == .messages {
                    let member = try reader.readMember()
                    current = schema.members[member]
                    output += "message \(current.name) index=\(member)\n"
                }
                try walk(&reader, current, 0, current.nodes.count, &output)
                let count = try reader.finish()
                output += "bytes \(count)\n"
                return GesSchemaDumpResult(text: output, isValid: true)
            } catch {
                output += "error \(error.code) path=\(error.path) bit=\(reader.bitPosition)\n"
                return GesSchemaDumpResult(text: output, isValid: false)
            }
        }
    }

    private static func walk(_ reader: inout GesSchemaReader, _ schema: GesBoundSchema, _ start: Int, _ end: Int, _ output: inout String) throws(GesWireError) {
        var index = start
        while index < end {
            try field(&reader, schema, index, &output)
            index = schema.nodes[index].next
        }
    }

    private static func field(_ reader: inout GesSchemaReader, _ schema: GesBoundSchema, _ index: Int, _ output: inout String) throws(GesWireError) {
        let node = schema.nodes[index]
        let info = schema.infos[index]
        let field = GesSchemaField(owner: schema, index: index)
        let offset = reader.bitPosition

        func line(_ value: String) { output += "\(info.path) bit=\(offset) width=\(reader.bitPosition - offset) value=\(value)\n" }

        if node.optional, !(try reader.readPresence(field)) { return line("absent") }
        switch node.kind {
        case .boolean:
            let flag = try reader.readBoolean(field)
            line(flag ? "true" : "false")
        case .tag:
            let tag = try reader.readTag(field)
            line("#\(info.tags[tag]) index=\(tag)")
        case .text:
            line(quoted(try reader.readTextValue(field)))
        case .list:
            let length = try reader.beginList(field)
            line("count=\(length)")
            var item = 0
            while item < length {
                try self.field(&reader, schema, index + 1, &output)
                item += 1
            }
        case .group:
            try reader.beginNested(field)
            try walk(&reader, schema, index + 1, node.next, &output)
        case .integer:
            let integer = try reader.readInteger(field)
            line(String(integer))
        default:
            let number = try reader.readNumber(field)
            line(spelled(number))
        }
    }

    // A Text stays on its line, whatever the data holds.
    private static func quoted(_ text: String) -> String {
        var output = "'"
        for scalar in text.unicodeScalars {
            switch scalar {
            case "\\": output += "\\\\"
            case "'": output += "\\'"
            case "\n": output += "\\n"
            case "\r": output += "\\r"
            case "\t": output += "\\t"
            default:
                if scalar.value < 32 || scalar.value == 127 { output += "\\u{\(String(scalar.value, radix: 16))}" } else { output.unicodeScalars.append(scalar) }
            }
        }
        return output + "'"
    }

    // The same spelling in every port, from the shortest digits of a roundtrip text.
    private static func spelled(_ value: Double) -> String {
        if value.isInfinite { return value > 0 ? "Infinity" : "-Infinity" }
        if value == 0 { return "0" }
        let text = Array(value.magnitude.description.utf8)
        let sign = value < 0 ? "-" : ""
        let mark = text.firstIndex { $0 == 101 || $0 == 69 } ?? text.count
        let mantissa = text[0..<mark]
        let exponent = mark < text.count ? Int(String(decoding: text[(mark + 1)...], as: UTF8.self)) ?? 0 : 0
        var point = (mantissa.firstIndex(of: 46) ?? mark) + exponent
        var digits = mantissa.filter { $0 != 46 }
        while digits.first == 48 {
            digits.removeFirst()
            point -= 1
        }
        while digits.last == 48 { digits.removeLast() }
        let all = String(decoding: digits, as: UTF8.self)
        if point > 0 && point <= 21 {
            if digits.count <= point { return sign + all + String(repeating: "0", count: point - digits.count) }
            return sign + String(all.prefix(point)) + "." + String(all.dropFirst(point))
        }
        if point <= 0 && point > -4 { return sign + "0." + String(repeating: "0", count: -point) + all }
        let written = point - 1
        return sign + String(all.prefix(1)) + (digits.count > 1 ? "." + String(all.dropFirst()) : "") + (written < 0 ? "e-" : "e+") + String(abs(written))
    }
}
