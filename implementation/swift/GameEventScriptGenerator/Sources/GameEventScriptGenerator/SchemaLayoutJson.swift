// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

// The canonical JSON form of a layout: members in a fixed order, two spaces of indentation, one value per line, integers
// as exact decimals and binary64 values as their bit patterns. Both ports write identical bytes.
enum GesSchemaLayoutJson {
    static func write(_ layout: GesSchemaLayout) -> String {
        var json = Writer()
        json.beginObject()
        json.number("layout", Int64(GesSchemaLayout.version))
        json.strings("roots", layout.roots)
        json.beginArray("parameters")
        for parameter in layout.parameters {
            json.beginObject()
            json.string("name", parameter.name)
            json.string("type", parameter.type)
            json.string("unit", parameter.unit)
            json.string("value", parameter.value)
            json.endObject()
        }
        json.endArray()
        json.beginArray("types")
        for type in layout.types { self.type(&json, type) }
        json.endArray()
        json.endObject()
        return json.text + "\n"
    }

    private static func type(_ json: inout Writer, _ type: GesLayoutType) {
        json.beginObject()
        json.string("name", type.name)
        json.string("kind", type.kind == .schema ? "schema" : type.kind == .message ? "message" : "messages")
        json.string("hash", hex(type.hash))
        json.string("bindingHash", hex(type.bindingHash))
        json.strings("bindingHashDepends", type.bindingHashDepends)
        json.number("maxByteCount", Int64(type.maxByteCount))
        json.strings("maxByteCountDepends", type.maxByteCountDepends)
        if type.kind == .messages {
            json.strings("members", type.members)
            json.number("memberWidth", Int64(type.memberWidth))
        } else {
            json.beginArray("fields")
            for field in type.fields { self.field(&json, field, member: true) }
            json.endArray()
            constants(&json, type.constants)
            if type.kind == .message { json.strings("tags", type.tags) }
        }
        json.endObject()
    }

    /// Only a schema with lets carries them.
    static func constants(_ json: inout Writer, _ constants: [GesLayoutConstant]) {
        if constants.isEmpty { return }
        json.beginArray("constants")
        for constant in constants {
            json.beginObject()
            json.string("name", constant.name)
            json.string("type", constant.type)
            json.string("unit", constant.unit)
            json.string("value", constant.value)
            json.boolean("integer", constant.integer)
            json.endObject()
        }
        json.endArray()
    }

    private static func field(_ json: inout Writer, _ field: GesLayoutField, member: Bool, value: Bool = false) {
        if value { json.beginValueObject() } else { json.beginObject() }
        json.string("name", field.name)
        json.string("path", field.path)
        if member {
            json.boolean("optional", field.optional)
            json.boolean("key", field.key)
            json.boolean("unlabeled", field.unlabeled)
            field.defaultValue?.write(&json)
        }
        json.key("encoding")
        encoding(&json, field.encoding)
        json.endObject()
    }

    private static func encoding(_ json: inout Writer, _ encoding: GesLayoutEncoding) {
        json.beginValueObject()
        json.string("kind", kind(encoding.kind))
        switch encoding.kind {
        case .integer, .quantized, .float:
            json.string("type", encoding.type)
            json.string("unit", encoding.unit)
            json.boolean("range", encoding.range)
            if encoding.kind != .float {
                json.boolean("wrapping", encoding.wrapping)
                json.number("lower", encoding.lower)
                json.unsigned("maxIndex", encoding.maxIndex)
            }
            json.number("width", Int64(encoding.width))
            if encoding.kind == .quantized {
                json.number("numerator", encoding.numerator)
                json.number("tenPower", encoding.tenPower)
            }
            if encoding.kind == .quantized || (encoding.kind == .float && encoding.range) {
                json.string("lowerValue", bits(encoding.lowerValue))
                json.string("upperValue", bits(encoding.upperValue))
            }
            json.unsigned("deltaRange", encoding.deltaRange)
            json.number("deltaWidth", Int64(encoding.deltaWidth))
            json.strings("depends", encoding.depends)
        case .tag, .flags:
            json.strings("tags", encoding.tags)
            if !encoding.tagSet.isEmpty { json.string("tagSet", encoding.tagSet) }
            json.number("width", Int64(encoding.width))
        case .text:
            json.boolean("tagName", encoding.tagName)
            json.number("maxCount", encoding.maxCount)
            json.number("width", Int64(encoding.width))
        case .bytes:
            json.number("maxCount", encoding.maxCount)
            json.number("width", Int64(encoding.width))
        case .list:
            json.boolean("dice", encoding.dice)
            json.number("maxCount", encoding.maxCount)
            json.number("width", Int64(encoding.width))
            json.boolean("keyed", encoding.keyed)
            json.number("positionWidth", Int64(encoding.positionWidth))
            json.key("element")
            field(&json, encoding.element!, member: false, value: true)
        case .point, .vector:
            json.string("unit", encoding.unit)
            json.beginArray("axes")
            for axis in encoding.axes { field(&json, axis, member: false) }
            json.endArray()
        case .schema:
            json.string("name", encoding.schema)
        case .union:
            json.number("width", Int64(encoding.width))
            json.beginArray("alternatives")
            for alternative in encoding.alternatives { field(&json, alternative, member: false) }
            json.endArray()
        case .boolean, .nothing:
            break
        }
        json.endObject()
    }

    private static func kind(_ kind: GesLayoutKind) -> String {
        switch kind {
        case .boolean: "boolean"
        case .integer: "integer"
        case .quantized: "quantized"
        case .float: "float"
        case .tag: "tag"
        case .flags: "flags"
        case .text: "text"
        case .bytes: "bytes"
        case .list: "list"
        case .point: "point"
        case .vector: "vector"
        case .schema: "schema"
        case .union: "union"
        case .nothing: "nothing"
        }
    }

    static func hex(_ value: UInt64) -> String {
        let digits = String(value, radix: 16)
        return String(repeating: "0", count: 16 - digits.count) + digits
    }

    private static func bits(_ value: Double) -> String { "0x" + hex(value.bitPattern) }

    // Writes members in call order: every member or element on its own line, a container closing on its own line.
    struct Writer {
        var text = ""
        private var filled: [Bool] = []

        // An object as the whole document or as an array element.
        mutating func beginObject() {
            if !filled.isEmpty { item() }
            open("{")
        }

        // An object as the value of the member just started with key.
        mutating func beginValueObject() { open("{") }

        mutating func endObject() { close("}") }

        mutating func beginArray(_ key: String) {
            self.key(key)
            open("[")
        }

        mutating func endArray() { close("]") }

        // A member whose value is the object that follows.
        mutating func key(_ key: String) {
            item()
            quote(key)
            text += ": "
        }

        mutating func string(_ key: String, _ value: String) {
            self.key(key)
            quote(value)
        }

        mutating func number(_ key: String, _ value: Int64) {
            self.key(key)
            text += String(value)
        }

        mutating func unsigned(_ key: String, _ value: UInt64) {
            self.key(key)
            text += String(value)
        }

        mutating func null(_ key: String) {
            self.key(key)
            text += "null"
        }

        mutating func boolean(_ key: String, _ value: Bool) {
            self.key(key)
            text += value ? "true" : "false"
        }

        mutating func strings(_ key: String, _ values: [String]) {
            self.key(key)
            open("[")
            for value in values {
                item()
                quote(value)
            }
            close("]")
        }

        // Starts a member or element on its own line, after a comma unless it is the first.
        private mutating func item() {
            if filled[filled.count - 1] { text += "," }
            filled[filled.count - 1] = true
            text += "\n" + String(repeating: " ", count: filled.count * 2)
        }

        private mutating func open(_ bracket: String) {
            text += bracket
            filled.append(false)
        }

        private mutating func close(_ bracket: String) {
            if filled.removeLast() { text += "\n" + String(repeating: " ", count: filled.count * 2) }
            text += bracket
        }

        // Escapes like the C# port, which works on UTF-16 code units: only quotes, backslashes and control characters.
        private mutating func quote(_ value: String) {
            text += "\""
            for scalar in value.unicodeScalars {
                switch scalar {
                case "\"": text += "\\\""
                case "\\": text += "\\\\"
                case _ where scalar.value < 0x20:
                    let digits = String(scalar.value, radix: 16)
                    text += "\\u" + String(repeating: "0", count: 4 - digits.count) + digits
                default: text.unicodeScalars.append(scalar)
                }
            }
            text += "\""
        }
    }
}
