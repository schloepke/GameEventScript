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
        try withScratch(bufferSize ?? schema.maxByteCount) { bytes in
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
            return Array(UnsafeRawBufferPointer(rebasing: bytes[0..<(try writer.finish())]))
        }
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
        return try withScratch(schema.maxByteCount) { target in
            let length = try record.withUnsafeBytes { first in
                try previous.withUnsafeBytes { second in
                    try GesSchemaDelta.encode(schema, record: first, baseline: second, into: target)
                }
            }
            return Array(UnsafeRawBufferPointer(rebasing: target[0..<length]))
        }
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
        return try withScratch(schema.maxByteCount) { target in
            let decoded = try data.withUnsafeBytes { first in
                try previous.withUnsafeBytes { second in
                    try GesSchemaDelta.decode(schema, delta: first, baseline: second, into: target, byteLimit: byteLimit)
                }
            }
            let value = try decode(schema, Array(UnsafeRawBufferPointer(rebasing: target[0..<decoded.byteCount]))).value
            return GesSchemaDecoded(value: value, byteCount: decoded.deltaByteCount)
        }
    }

    // Scratch space for one record, released on return. The largest encoding may reach 10 MiB, so it is neither cleared
    // nor kept; the writer writes whole words before it reads any back.
    static func withScratch<Result>(_ count: Int, _ body: (UnsafeMutableRawBufferPointer) throws -> Result) rethrows -> Result {
        let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: count, alignment: 8)
        defer { buffer.deallocate() }
        return try body(buffer)
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
            if let last = try held(schema, start, end, fields, index, &value), !acceptsNothing(schema, last) {
                throw GesWireError(code: GesWireError.missingField, path: schema.infos[last].path)
            }
            try encodeNode(&writer, schema, index, value, strict: strict)
            index = schema.nodes[index].next
        }
    }

    /// The value of a field, or else of its default: a literal, or the value of the sibling it names, whose own default
    /// applies in turn. Returns nil, or the field at the end of the chain that has no value, which reads as nothing.
    private static func held(_ schema: GesBoundSchema, _ start: Int, _ end: Int, _ fields: GesValueMap, _ index: Int, _ value: inout GesValue) throws(GesWireError) -> Int? {
        let info = schema.infos[index]
        var key = 0
        while key < fields.length {
            if fields.key(at: key).unicodeScalars.elementsEqual(info.name.unicodeScalars) {
                value = fields.value(at: key)
                return nil
            }
            key += 1
        }
        guard let fallback = info.defaultValue else { return index }
        switch fallback.kind {
        case .field:
            var sibling = start
            while sibling < end {
                if schema.infos[sibling].name == fallback.name { return try held(schema, start, end, fields, sibling, &value) }
                sibling = schema.nodes[sibling].next
            }
            return index
        default:
            value = literal(fallback, integer: schema.nodes[index].kind == .integer, percentage: info.valueKind == .percentage, unit: info.unit)
        }
        return nil
    }

    /// The value of a literal default that names no sibling: a Boolean, a tag, the Map of a record literal, or a number of
    /// the field's kind and unit.
    static func literal(_ fallback: GesResolvedDefault, integer: Bool, percentage: Bool, unit: GesQuantityUnit) -> GesValue {
        switch fallback.kind {
        case .boolean:
            return .boolean(fallback.boolean)
        case .tag:
            return (try? .tag(fallback.name)) ?? .nothing
        case .record:
            return fallback.record?.value ?? .map(keys: [], values: [])
        case .number where integer:
            var whole = fallback.value.significand
            for _ in 0..<max(fallback.value.exponent, 0) { whole *= 10 }
            return .integer(whole, unit: unit)
        default:
            let number = Double(fallback.value.canonical) ?? 0
            return percentage ? .percentage(number) : .float(number, unit: unit)
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
        case .flags:
            let flags = flagMask(info, value)
            if let rejected = flags.rejected { throw GesWireError(code: rejected, path: info.path) }
            try writer.writeFlags(field, flags.mask)
        case .text where node.isBytes:
            guard let bytes = value.bytesValue else { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            try bytes.withUnsafeBytes { try writer.writeBytes(field, $0) }
        case .text:
            if value.valueKind != (node.isTagText ? .tag : .text) { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            let bytes = Array(value.asText.utf8)
            try bytes.withUnsafeBytes { try writer.writeText(field, $0) }
        case .list where info.valueKind == .dice:
            guard value.valueKind == .dice else { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            let rolls = value.asDice
            try writer.beginList(field, count: rolls.count)
            for roll in rolls { try writer.writeInteger(GesSchemaField(owner: schema, index: index + 1), Int64(roll)) }
        case .list where info.valueKind == .poly:
            guard let poly = GesPoly(value, unit: info.unit) else { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            try writer.beginList(field, count: poly.points.count)
            for point in poly.points { try encodePoint(&writer, schema, index + 1, point.x, point.y, point.z) }
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

    // A flag Map sets a member for true and leaves it unset for false or an absent key. Returns the mask, or the wire
    // code that rejects the value; a union alternative holds exactly the Maps that encoding accepts.
    private static func flagMask(_ info: GesSchemaNodeInfo, _ value: GesValue) -> (mask: UInt64, rejected: String?) {
        guard value.valueKind == .map, let map = value.asMap else { return (0, GesWireError.typeMismatch) }
        var mask: UInt64 = 0
        for entry in 0..<map.length {
            guard let member = info.tags.firstIndex(of: map.key(at: entry)) else { return (0, GesWireError.unknownTag) }
            let set = map.value(at: entry)
            if set.valueKind != .boolean { return (0, GesWireError.typeMismatch) }
            if set.asBoolean { mask |= 1 << UInt64(member) }
        }
        return (mask, nil)
    }

    // The Map of the set members, each true.
    private static func flagMap(_ members: [String], _ mask: UInt64) -> GesValue {
        let keys = members.indices.filter { mask >> UInt64($0) & 1 != 0 }.map { members[$0] }
        return .map(keys: keys, values: keys.map { _ in .boolean(true) })
    }

    private static func selects(_ candidate: GesSchemaNodeInfo, _ value: GesValue) -> Bool {
        switch candidate.valueKind {
        case .nothing: value.isNothing
        case .boolean: value.valueKind == .boolean
        case .tag: value.valueKind == .tag
        case .text: value.valueKind == .text
        case .bytes: value.valueKind == .bytes
        case .flags: flagMask(candidate, value).rejected == nil
        case .list: value.valueKind == .list
        case .dice: value.valueKind == .dice
        case .point: value.valueKind == .point
        case .vector: value.valueKind == .vector
        case .box: value.externalValue is GesGeometryLibrary.BoxValue
        case .poly: value.externalValue is GesGeometryLibrary.PolyValue
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
            try encodePoint(&writer, schema, index, value.x, value.y, value.z)
            return
        }
        if info.valueKind == .box {
            guard let box = GesBox(value, unit: info.unit) else { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
            try writer.beginNested(field)
            try encodePoint(&writer, schema, index + 1, box.min.x, box.min.y, box.min.z)
            try encodePoint(&writer, schema, schema.nodes[index + 1].next, box.max.x, box.max.y, box.max.z)
            return
        }
        if let name = value.customTypeName, name != info.schemaName { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
        guard let fields = try value.materializedMap() else { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
        if strict && value.customTypeName == nil && !hasEveryKey(schema, index, fields) { throw GesWireError(code: GesWireError.typeMismatch, path: info.path) }
        try writer.beginNested(field)
        try encodeFields(&writer, schema, index + 1, node.next, fields, strict: strict)
    }

    // A checked document writes a Box as the Map of its corners and a Poly as the List of its points, since documents have no
    // geometry literal; this gives them the geometry library's values, as the schema's fields and their alternatives expect.
    static func documentShapes(_ schema: GesResolvedDeclaration, _ value: GesValue) -> GesValue {
        guard let map = value.asMap else { return value }
        var keys: [String] = []
        var values: [GesValue] = []
        for index in 0..<map.length {
            let key = map.key(at: index)
            let entry = map.value(at: index)
            keys.append(key)
            values.append(schema.fields.first { $0.name == key }.map { documentShape($0.alternatives, entry) } ?? entry)
        }
        if let name = value.customTypeName { return .record(typeName: name, keys: keys, values: values) }
        return .map(keys: keys, values: values)
    }

    private static func documentShape(_ alternatives: [GesResolvedType], _ value: GesValue) -> GesValue {
        for type in alternatives {
            switch type.kind {
            case .box:
                if let corners = value.asMap, corners.length == 2, let lower = corners.get("lower").flatMap({ GesPoint($0, unit: type.unit) }),
                    let upper = corners.get("upper").flatMap({ GesPoint($0, unit: type.unit) })
                {
                    return GesBox(lower, upper).value(unit: type.unit)
                }
            case .poly:
                guard value.valueKind == .list else { continue }
                let points = value.asList.compactMap { GesPoint($0, unit: type.unit) }
                if points.count == value.asList.count { return GesPoly(points).value(unit: type.unit) }
            case .list:
                if let element = type.element, value.valueKind == .list { return .list(value.asList.map { documentShape(element, $0) }) }
            case .reference:
                if value.asMap != nil, value.customTypeName == nil || value.customTypeName == type.referenceName, let reference = type.reference {
                    return documentShapes(reference, value)
                }
            default:
                continue
            }
        }
        return value
    }

    // The components of a Point, Vector, Box corner or Poly point; an axis that the block leaves out must be zero.
    private static func encodePoint(_ writer: inout GesSchemaWriter, _ schema: GesBoundSchema, _ index: Int, _ x: Double, _ y: Double, _ z: Double) throws {
        let node = schema.nodes[index]
        var seen: Set<String> = []
        try writer.beginNested(GesSchemaField(owner: schema, index: index))
        var axis = index + 1
        while axis < node.next {
            let name = schema.infos[axis].name
            seen.insert(name)
            try writer.writeNumber(GesSchemaField(owner: schema, index: axis), name == "x" ? x : name == "y" ? y : z)
            axis += 1
        }
        if (!seen.contains("x") && x != 0) || (!seen.contains("y") && y != 0) || (!seen.contains("z") && z != 0) {
            throw GesWireError(code: GesWireError.typeMismatch, path: schema.infos[index].path)
        }
    }

    // The components of a Point, Vector, Box corner or Poly point; an axis that the block leaves out is zero.
    private static func decodePoint(_ reader: inout GesSchemaReader, _ schema: GesBoundSchema, _ index: Int) throws -> GesPoint {
        let node = schema.nodes[index]
        try reader.beginNested(GesSchemaField(owner: schema, index: index))
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
        return GesPoint(x: x, y: y, z: z)
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
        if let computed = schema.computed[name] { derive(computed, &keys, &values) }
        return .record(typeName: name, keys: keys, values: values)
    }

    /// Adds the computed fields in order, each reading the fields and the computed fields before it, as a script record does.
    private static func derive(_ computed: [GesSchemaComputedFieldInfo], _ keys: inout [String], _ values: inout [GesValue]) {
        for field in computed {
            let type: GesComputedType = field.kind == .boolean ? .boolean : field.kind == .percentage ? .percentage : .number
            let value = GesComputedMath.cast(GesComputedEvaluation.evaluate(field.tree, GesRecordScope(keys: keys, values: values), nil), type)
            keys.append(field.name)
            switch value.kind {
            case .integer: values.append(.integer(value.integer, unit: field.unit))
            case .float: values.append(.float(value.number, unit: field.unit))
            case .percentage: values.append(.percentage(value.number))
            case .true, .false: values.append(.boolean(value.kind == .true))
            case .nothing: values.append(.nothing)
            }
        }
    }

    private static func decodeNode(_ reader: inout GesSchemaReader, _ schema: GesBoundSchema, _ index: Int) throws -> GesValue {
        let node = schema.nodes[index]
        let info = schema.infos[index]
        let field = GesSchemaField(owner: schema, index: index)
        if node.optional, !(try reader.readPresence(field)) { return .nothing }
        switch node.kind {
        case .boolean: return .boolean(try reader.readBoolean(field))
        case .tag: return try .tag(info.tags[try reader.readTag(field)])
        case .flags: return flagMap(info.tags, try reader.readFlags(field))
        case .text where node.isBytes: return .bytes(try reader.readBytesValue(field))
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
        case .list where info.valueKind == .poly:
            let count = try reader.beginList(field)
            var points: [GesPoint] = []
            // The count is untrusted until the points are read, so the capacity grows with the data as for a List.
            points.reserveCapacity(min(count, 1024))
            for _ in 0..<count { points.append(try decodePoint(&reader, schema, index + 1)) }
            return GesPoly(points).value(unit: info.unit)
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
        case .group where info.valueKind == .point || info.valueKind == .vector:
            let spatial = try decodePoint(&reader, schema, index)
            return info.valueKind == .point ? .point(x: spatial.x, y: spatial.y, z: spatial.z, unit: info.unit) : .vector(x: spatial.x, y: spatial.y, z: spatial.z, unit: info.unit)
        case .group where info.valueKind == .box:
            // The corners are two Points on the wire; the box orders them.
            try reader.beginNested(field)
            let lower = try decodePoint(&reader, schema, index + 1)
            let upper = try decodePoint(&reader, schema, schema.nodes[index + 1].next)
            return GesBox(lower, upper).value(unit: info.unit)
        case .group:
            try reader.beginNested(field)
            return try decodeRecord(&reader, schema, info.schemaName, index + 1, node.next)
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

/// The decoded fields of a record, or a value of a list element, as computed fields read them.
private struct GesRecordScope: GesComputedScope {
    var keys: [String]?
    var values: [GesValue]?
    var value = GesValue.nothing

    init(keys: [String], values: [GesValue]) {
        self.keys = keys
        self.values = values
    }

    init(value: GesValue) { self.value = value }

    func read(_ path: [String], percentage: Bool) -> GesComputedValue {
        let found = walk(path)
        switch found.valueKind {
        case .integer: return .of(integer: found.integerValue!)
        case .float: return .of(float: found.floatValue!)
        case .percentage: return .of(percentage: found.floatValue!)
        case .boolean: return .of(boolean: found.asBoolean)
        default: return .nothing
        }
    }

    func has(_ path: [String]) -> Bool { !walk(path).isNothing }

    func count(_ path: [String]) -> Int { walk(path).listValue?.count ?? 0 }

    func element(_ path: [String], _ index: Int) -> any GesComputedScope { GesRecordScope(value: walk(path).listValue![index]) }

    private func walk(_ path: [String]) -> GesValue {
        var current = value
        for (segment, name) in path.enumerated() {
            if segment == 0, let keys, let values {
                current = keys.firstIndex(of: name).map { values[$0] } ?? .nothing
                continue
            }
            guard let fields = current.asMap else { return .nothing }
            current = fields.get(name) ?? .nothing
        }
        return current
    }
}

/// The named fields of a record literal in the field order of its schema, filled once every schema is composed and immutable
/// after loading. Setting them also builds the Map that a left-out field takes, once, so that reading and encoding share it;
/// encoding gives every field the Map leaves out its own default.
final class GesResolvedRecord: @unchecked Sendable {
    var entries: [GesResolvedRecordEntry] = [] {
        didSet {
            value = .map(
                keys: entries.map(\.field.name),
                values: entries.map { entry in
                    let type = entry.field.alternatives[0]
                    return GesSchemaValues.literal(entry.value, integer: type.constraints.integer, percentage: type.kind == .percentage, unit: type.unit)
                }
            )
        }
    }
    private(set) var value: GesValue = .map(keys: [], values: [])
}
