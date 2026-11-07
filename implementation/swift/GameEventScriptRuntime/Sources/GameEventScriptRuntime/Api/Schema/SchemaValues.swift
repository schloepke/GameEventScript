// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// A decoded top-level value and the bytes it occupied.
public struct GesSchemaDecoded {
    /// The Record of a schema or the Message of a message or message set.
    public let value: GesValue
    /// The bytes the encoding occupied; further records may follow them.
    public let byteCount: Int
}

/// The convenient path between GES values and the wire, for rare data. A Record or Map encodes by field name, and fields
/// without a schema entry, such as computed fields, are not transmitted. Only a Map that selects a union alternative must
/// have a field for each of its keys. Time-critical code uses `GesSchemaWriter` and
/// `GesSchemaReader`.
public enum GesSchemaValues {
    /// Encodes a Record or Map through a schema, or a Message through a message.
    /// - Parameters:
    ///   - schema: The bound schema.
    ///   - value: The value.
    ///   - bufferSize: The size of the buffer that receives the encoding; by default the largest encoding.
    /// - Returns: The encoded bytes.
    /// - Throws: `GesWireError` when the value does not fit the schema; `GesFatalRuntimeError` when an external field getter fails.
    public static func encode(_ schema: GesBoundSchema, _ value: GesValue, bufferSize: Int? = nil) throws -> [UInt8] {
        var buffer = [UInt8](repeating: 0, count: bufferSize ?? schema.maxByteCount)
        let length = try buffer.withUnsafeMutableBytes { bytes -> Int in
            var writer = GesSchemaWriter(schema, into: bytes)
            var current = schema
            if schema.kind == .messages {
                guard let message = value.messageValue, let member = schema.memberIndex(message.name) else { throw GesWireError(code: GesWireError.typeMismatch) }
                try writer.selectMember(member)
                current = schema.members[member]
            }
            if current.kind == .message {
                try encodeMessage(&writer, current, value)
            } else {
                if let name = value.customTypeName, name != current.name { throw GesWireError(code: GesWireError.typeMismatch) }
                guard let fields = try value.materializedMap() else { throw GesWireError(code: GesWireError.typeMismatch) }
                try encodeFields(&writer, current, 0, current.nodes.count, fields, strict: false)
            }
            return try writer.finish()
        }
        return Array(buffer[0..<length])
    }

    /// Encodes a Record or Map as a delta against a baseline value of the same schema.
    /// - Parameters:
    ///   - schema: The bound schema.
    ///   - value: The value.
    ///   - baseline: The value that the receiver holds.
    /// - Returns: The encoded delta.
    /// - Throws: `GesWireError` when a value does not fit the schema or their keys differ; `GesFatalRuntimeError` when an external field getter fails.
    public static func encode(_ schema: GesBoundSchema, _ value: GesValue, baseline: GesValue) throws -> [UInt8] {
        let record = try encode(schema, value)
        let previous = try encode(schema, baseline)
        var buffer = [UInt8](repeating: 0, count: schema.maxByteCount)
        let length = try buffer.withUnsafeMutableBytes { target in
            try record.withUnsafeBytes { first in
                try previous.withUnsafeBytes { second in
                    try GesSchemaDelta.encode(schema, record: first, baseline: second, into: target)
                }
            }
        }
        return Array(buffer[0..<length])
    }

    /// Decodes a delta against the baseline value it was encoded with.
    /// - Parameters:
    ///   - schema: The bound schema.
    ///   - data: The delta; it may continue after it.
    ///   - baseline: The value that this side holds.
    ///   - byteLimit: An optional limit on the bytes of this delta.
    /// - Returns: The value and the bytes the delta occupied.
    /// - Throws: `GesWireError` when the data is not a valid delta for this baseline; `GesFatalRuntimeError` when an external field getter of the baseline fails.
    public static func decode(_ schema: GesBoundSchema, _ data: [UInt8], baseline: GesValue, byteLimit: Int = .max) throws -> GesSchemaDecoded {
        let previous = try encode(schema, baseline)
        var buffer = [UInt8](repeating: 0, count: schema.maxByteCount)
        let decoded = try buffer.withUnsafeMutableBytes { target in
            try data.withUnsafeBytes { first in
                try previous.withUnsafeBytes { second in
                    try GesSchemaDelta.decode(schema, delta: first, baseline: second, into: target, byteLimit: byteLimit)
                }
            }
        }
        let value = try decode(schema, Array(buffer[0..<decoded.byteCount])).value
        return GesSchemaDecoded(value: value, byteCount: decoded.deltaByteCount)
    }

    /// Decodes a Record or Message.
    /// - Parameters:
    ///   - schema: The bound schema.
    ///   - data: The data; it may continue after the record.
    ///   - byteLimit: An optional limit on the bytes of this record.
    /// - Returns: The value and the bytes it occupied.
    /// - Throws: `GesWireError` when the data is not a valid encoding.
    public static func decode(_ schema: GesBoundSchema, _ data: [UInt8], byteLimit: Int = .max) throws -> GesSchemaDecoded {
        try data.withUnsafeBytes { bytes in
            var reader = GesSchemaReader(schema, data: bytes, byteLimit: byteLimit)
            var current = schema
            if schema.kind == .messages { current = schema.members[try reader.readMember()] }
            let value = current.kind == .message ? try decodeMessage(&reader, current) : try decodeRecord(&reader, current, current.name, 0, current.nodes.count)
            return GesSchemaDecoded(value: value, byteCount: try reader.finish())
        }
    }

    /// Reads the key of an encoded record without decoding the rest.
    /// - Returns: The key as an Int64, or the tag index of a Tag key.
    /// - Throws: `GesWireError` when the schema has no key or the data is invalid.
    public static func readKey(_ schema: GesBoundSchema, _ data: [UInt8]) throws -> Int64 {
        try data.withUnsafeBytes { bytes in
            var reader = GesSchemaReader(schema, data: bytes)
            return try reader.readKey()
        }
    }

    private static func encodeMessage(_ writer: inout GesSchemaWriter, _ schema: GesBoundSchema, _ value: GesValue) throws {
        guard let message = value.messageValue, message.name == schema.name else { throw GesWireError(code: GesWireError.typeMismatch) }
        for tag in message.tags where !schema.infos.enumerated().contains(where: { schema.nodes[$0.offset].isTagBit && $0.element.name == tag }) {
            throw GesWireError(code: GesWireError.unknownTag)
        }
        let arguments = Array(message.arguments)
        var argument = 0
        var index = 0
        while index < schema.nodes.count {
            if schema.nodes[index].isTagBit {
                try writer.writeBoolean(GesSchemaField(owner: schema, index: index), message.tags.contains(schema.infos[index].name))
            } else {
                if argument >= arguments.count { throw GesWireError(code: GesWireError.missingField, path: schema.infos[index].path) }
                if arguments[argument].name != schema.labels[argument] { throw GesWireError(code: GesWireError.typeMismatch, path: schema.infos[index].path) }
                try encodeNode(&writer, schema, index, arguments[argument].value, strict: false)
                argument += 1
            }
            index = schema.nodes[index].next
        }
        if argument != arguments.count { throw GesWireError(code: GesWireError.typeMismatch) }
    }

    private static func decodeMessage(_ reader: inout GesSchemaReader, _ schema: GesBoundSchema) throws -> GesValue {
        var arguments: [GesMessageArgument] = []
        var tags: [String] = []
        var index = 0
        while index < schema.nodes.count {
            if schema.nodes[index].isTagBit {
                if try reader.readBoolean(GesSchemaField(owner: schema, index: index)) { tags.append(schema.infos[index].name) }
            } else {
                arguments.append(try GesMessageArgument(name: schema.labels[arguments.count], value: try decodeNode(&reader, schema, index)))
            }
            index = schema.nodes[index].next
        }
        return .message(try GameEventScriptMessage(name: schema.name, arguments: arguments, tags: tags))
    }

    private static func encodeFields(_ writer: inout GesSchemaWriter, _ schema: GesBoundSchema, _ start: Int, _ end: Int, _ fields: GesValueMap, strict: Bool) throws {
        var index = start
        while index < end {
            var value = GesValue.nothing
            var found = false
            var key = 0
            while key < fields.length {
                if fields.key(at: key).unicodeScalars.elementsEqual(schema.infos[index].name.unicodeScalars) {
                    value = fields.value(at: key)
                    found = true
                }
                key += 1
            }
            if !found && !acceptsNothing(schema, index) { throw GesWireError(code: GesWireError.missingField, path: schema.infos[index].path) }
            try encodeNode(&writer, schema, index, value, strict: strict)
            index = schema.nodes[index].next
        }
    }

    private static func acceptsNothing(_ schema: GesBoundSchema, _ index: Int) -> Bool {
        let node = schema.nodes[index]
        if node.optional { return true }
        if node.kind != .union { return false }
        var alternative = index + 1
        while alternative < node.next {
            if schema.nodes[alternative].kind == .nothing { return true }
            alternative = schema.nodes[alternative].next
        }
        return false
    }

    // Strict encoding is the trial of a Map against a union alternative: a key without a field means that the alternative does not hold.
    private static func encodeNode(_ writer: inout GesSchemaWriter, _ schema: GesBoundSchema, _ index: Int, _ value: GesValue, strict: Bool) throws {
        let node = schema.nodes[index]
        let info = schema.infos[index]
        let field = GesSchemaField(owner: schema, index: index)
        if value.isNothing && (node.optional || node.kind != .union) {
            if !node.optional { throw GesWireError(code: GesWireError.nothingValue, path: info.path) }
            try writer.writeAbsent(field)
            return
        }
        switch node.kind {
        case .boolean:
            if value.valueKind != .boolean { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            try writer.writeBoolean(field, value.asBoolean)
        case .tag:
            if value.valueKind != .tag { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            guard let tag = info.tags.firstIndex(of: value.asText) else { throw GesWireError(code: GesWireError.unknownTag, path: info.path) }
            try writer.writeTag(field, tag)
        case .text:
            if value.valueKind != (node.isTagText ? .tag : .text) { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            let bytes = Array(value.asText.utf8)
            try bytes.withUnsafeBytes { try writer.writeText(field, $0) }
        case .list where info.valueKind == .dice:
            guard value.valueKind == .dice else { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            let rolls = value.asDice
            try writer.beginList(field, count: rolls.count)
            for roll in rolls { try writer.writeInteger(GesSchemaField(owner: schema, index: index + 1), Int64(roll)) }
        case .list:
            guard value.valueKind == .list else { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            let items = value.asList
            try writer.beginList(field, count: items.count)
            for item in items { try encodeNode(&writer, schema, index + 1, item, strict: strict) }
        case .group:
            try encodeGroup(&writer, schema, index, value, strict: strict)
        case .union:
            try encodeUnion(&writer, schema, index, value, strict: strict)
        default:
            // Boolean and Dice are numeric in the language, but a field takes them only after an explicit conversion.
            let percent = value.valueKind == .percentage
            if !(value.valueKind == .integer || value.valueKind == .float || percent) || percent != (info.valueKind == .percentage) || value.unit != info.unit { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            if let integer = value.integerValue {
                try writer.writeInteger(field, integer)
            } else {
                try writer.writeNumber(field, value.floatValue ?? value.asNumber)
            }
        }
    }

    // The kind and type name of a value select one alternative. Only a Map can fit several schemas, and the first that holds decides.
    private static func encodeUnion(_ writer: inout GesSchemaWriter, _ schema: GesBoundSchema, _ index: Int, _ value: GesValue, strict: Bool) throws {
        let node = schema.nodes[index]
        let field = GesSchemaField(owner: schema, index: index)
        var position = 0
        var failure: GesWireError?
        var alternative = index + 1
        while alternative < node.next {
            let candidate = schema.nodes[alternative]
            let info = schema.infos[alternative]
            if candidate.kind == .group && !info.schemaName.isEmpty && value.valueKind == .map && value.customTypeName == nil {
                var attempt = writer
                do {
                    try attempt.selectAlternative(field, position)
                    try encodeNode(&attempt, schema, alternative, value, strict: true)
                    writer = attempt
                    return
                } catch let error as GesWireError where error.code != GesWireError.bufferTooSmall {
                    // The next schema may hold; otherwise the last one reports why not.
                    failure = error
                }
            } else if selects(info, value) {
                try writer.selectAlternative(field, position)
                if candidate.kind != .nothing { try encodeNode(&writer, schema, alternative, value, strict: strict) }
                return
            }
            alternative = candidate.next
            position += 1
        }
        throw failure ?? GesWireError(code: value.isNothing ? GesWireError.nothingValue : GesWireError.typeMismatch, path: schema.infos[index].path)
    }

    private static func selects(_ candidate: GesSchemaNodeInfo, _ value: GesValue) -> Bool {
        switch candidate.valueKind {
        case .nothing: value.isNothing
        case .boolean: value.valueKind == .boolean
        case .tag: value.valueKind == .tag
        case .text: value.valueKind == .text
        case .list: value.valueKind == .list
        case .dice: value.valueKind == .dice
        case .point: value.valueKind == .point
        case .vector: value.valueKind == .vector
        case .percentage: value.valueKind == .percentage
        case .reference: value.customTypeName == candidate.schemaName
        default: (value.valueKind == .integer || value.valueKind == .float) && value.unit == candidate.unit
        }
    }

    private static func encodeGroup(_ writer: inout GesSchemaWriter, _ schema: GesBoundSchema, _ index: Int, _ value: GesValue, strict: Bool) throws {
        let node = schema.nodes[index]
        let info = schema.infos[index]
        let field = GesSchemaField(owner: schema, index: index)
        if info.valueKind == .point || info.valueKind == .vector {
            let kind: GesBytecodeTypeKind = info.valueKind == .point ? .point : .vector
            if value.valueKind != kind || value.unit != info.unit { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            var seen: Set<String> = []
            try writer.beginNested(field)
            var axis = index + 1
            while axis < node.next {
                let name = schema.infos[axis].name
                seen.insert(name)
                try writer.writeNumber(GesSchemaField(owner: schema, index: axis), name == "x" ? value.x : name == "y" ? value.y : value.z)
                axis += 1
            }
            if (!seen.contains("x") && value.x != 0) || (!seen.contains("y") && value.y != 0) || (!seen.contains("z") && value.z != 0) {
                throw GesWireError(code: GesWireError.typeMismatch, path: info.path)
            }
            return
        }
        if let name = value.customTypeName, name != info.schemaName { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
        guard let fields = try value.materializedMap() else { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
        if strict && value.customTypeName == nil && !hasEveryKey(schema, index, fields) { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
        try writer.beginNested(field)
        try encodeFields(&writer, schema, index + 1, node.next, fields, strict: strict)
    }

    private static func hasEveryKey(_ schema: GesBoundSchema, _ index: Int, _ fields: GesValueMap) -> Bool {
        var key = 0
        while key < fields.length {
            var found = false
            var child = index + 1
            while child < schema.nodes[index].next && !found {
                found = fields.key(at: key).unicodeScalars.elementsEqual(schema.infos[child].name.unicodeScalars)
                child = schema.nodes[child].next
            }
            if !found { return false }
            key += 1
        }
        return true
    }

    private static func decodeRecord(_ reader: inout GesSchemaReader, _ schema: GesBoundSchema, _ name: String, _ start: Int, _ end: Int) throws -> GesValue {
        var keys: [String] = []
        var values: [GesValue] = []
        var index = start
        while index < end {
            keys.append(schema.infos[index].name)
            values.append(try decodeNode(&reader, schema, index))
            index = schema.nodes[index].next
        }
        return .record(typeName: name, keys: keys, values: values)
    }

    private static func decodeNode(_ reader: inout GesSchemaReader, _ schema: GesBoundSchema, _ index: Int) throws -> GesValue {
        let node = schema.nodes[index]
        let info = schema.infos[index]
        let field = GesSchemaField(owner: schema, index: index)
        if node.optional, !(try reader.readPresence(field)) { return .nothing }
        switch node.kind {
        case .boolean: return .boolean(try reader.readBoolean(field))
        case .tag: return try .tag(info.tags[try reader.readTag(field)])
        case .text: return node.isTagText ? try .tag(try reader.readTextValue(field)) : .text(try reader.readTextValue(field))
        case .list where info.valueKind == .dice:
            let length = try reader.beginList(field)
            var rolls: [Int32] = []
            rolls.reserveCapacity(min(length, 1024))
            var roll = 0
            while roll < length {
                rolls.append(Int32(try reader.readInteger(GesSchemaField(owner: schema, index: index + 1))))
                roll += 1
            }
            return .dice(rolls)
        case .union:
            let selected = GesSchemaNumbers.alternative(schema.nodes, index, try reader.readAlternative(field))
            return schema.nodes[selected].kind == .nothing ? .nothing : try decodeNode(&reader, schema, selected)
        case .list:
            let length = try reader.beginList(field)
            var items: [GesValue] = []
            items.reserveCapacity(min(length, 1024))
            var item = 0
            while item < length {
                items.append(try decodeNode(&reader, schema, index + 1))
                item += 1
            }
            return .list(items)
        case .group:
            try reader.beginNested(field)
            if info.valueKind != .point && info.valueKind != .vector { return try decodeRecord(&reader, schema, info.schemaName, index + 1, node.next) }
            var (x, y, z) = (0.0, 0.0, 0.0)
            var axis = index + 1
            while axis < node.next {
                let component = try reader.readNumber(GesSchemaField(owner: schema, index: axis))
                switch schema.infos[axis].name {
                case "x": x = component
                case "y": y = component
                default: z = component
                }
                axis += 1
            }
            return info.valueKind == .point ? .point(x: x, y: y, z: z, unit: info.unit) : .vector(x: x, y: y, z: z, unit: info.unit)
        case .integer: return .integer(try reader.readInteger(field), unit: info.unit)
        case .quantized:
            let (value, integer) = try reader.readExactNumber(field)
            if info.valueKind == .percentage { return .percentage(value) }
            if let integer { return .integer(integer, unit: info.unit) }
            return .float(value, unit: info.unit)
        default:
            let value = try reader.readNumber(field)
            if info.valueKind == .percentage { return .percentage(value) }
            if value == value.rounded(.down), value < 9_223_372_036_854_775_808, value >= -9_223_372_036_854_775_808 { return .integer(Int64(value), unit: info.unit) }
            return .float(value, unit: info.unit)
        }
    }
}

extension GesWireError {
    fileprivate init(code: String, path: String = "") {
        self = GesWireError.make(code, path)
    }
}
