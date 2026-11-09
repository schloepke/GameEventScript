// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Writes Swift 6 without Foundation: a Sendable struct per fixed-width schema with properties in wire order, write and read
// over the bit primitives, and constants inlined as literals or bit patterns.
enum GesSwiftEmitter {
    private static let keywords: Set<String> = [
        "Any", "Protocol", "Self", "Type", "as", "associatedtype", "break", "case", "catch", "class", "continue", "default",
        "defer", "deinit", "do", "else", "enum", "extension", "fallthrough", "false", "fileprivate", "for", "func", "guard",
        "if", "import", "in", "init", "inout", "internal", "is", "let", "nil", "open", "operator", "private", "precedencegroup",
        "protocol", "public", "repeat", "rethrows", "return", "self", "static", "struct", "subscript", "super", "switch",
        "throw", "throws", "true", "try", "typealias", "var", "where", "while",
    ]

    private static let reserved = ["schemaHash", "bindingHash", "maxByteCount", "write", "read", "readKey", "encoded"]

    typealias Code = GesSchemaPlanner.Code

    static func emit(_ layout: GesSchemaLayout, _ types: [GesPlanType], _ options: GesSchemaGenerateOptions) throws(GesGeneratorError) -> String {
        let access = options.access == .public ? "public " : ""
        let space = try GesSchemaPlanner.namespace(options.namespace, keywords)
        var code = Code()
        code.raw(GesSchemaPlanner.header(layout))
        code.blank()
        code.line("import GameEventScriptRuntime")
        code.blank()
        for part in space {
            code.line("/// The namespace `\(part)` of generated schema types.")
            code.open("\(access)enum \(part) {")
        }
        for (index, type) in types.enumerated() {
            if index > 0 { code.blank() }
            try self.type(&code, type, access)
        }
        for _ in space { code.close() }
        return code.text
    }

    private static func name(_ name: String) -> String { keywords.contains(name) ? "`" + name + "`" : name }

    private static func member(_ pascal: String) -> String { name(GesSchemaPlanner.camel(pascal)) }

    private static func type(_ code: inout Code, _ type: GesPlanType, _ access: String) throws(GesGeneratorError) {
        var names: [(name: String, origin: String)] = []
        for name in reserved { names.append((name, "the generated member " + name)) }
        for field in type.fields {
            names.append((GesSchemaPlanner.camel(field.base), "the field " + field.layout.name))
            if field.optional { names.append(("has" + field.base, "the presence of " + field.layout.name)) }
            nested(field.value, &names, field.layout.name)
        }
        try GesSchemaPlanner.unique(type.name, names)

        code.line("/// The schema `:\(type.name)`, read and written without allocation.")
        code.open("\(access)struct \(type.name): Sendable {")
        code.line("/// The schema hash, independent of parameter values.")
        code.line("\(access)static let schemaHash: UInt64 = 0x\(GesSchemaLayoutJson.hex(type.layout.hash))")
        code.line("/// The hash over the schema hash and the parameter values this code was generated with.")
        code.line("\(access)static let bindingHash: UInt64 = 0x\(GesSchemaLayoutJson.hex(type.layout.bindingHash))")
        code.line("/// The size of the largest encoding in bytes.")
        code.line("\(access)static let maxByteCount = \(type.layout.maxByteCount)")
        code.line("static let __paths: [String] = [")
        for path in type.paths { code.line("    " + GesSchemaPlanner.quote(path) + ",") }
        code.line("]")
        for field in type.fields { members(&code, field, access) }
        code.blank()
        code.line("/// Creates a record with every field at its zero value.")
        code.line("\(access)init() {}")
        code.blank()
        methods(&code, type, access)
        for field in type.fields { declarations(&code, field.value, access) }
        code.close()
    }

    private static func nested(_ value: GesPlanValue, _ names: inout [(name: String, origin: String)], _ field: String) {
        let name = nestedName(value)
        if !name.isEmpty { names.append((name, "the type of " + field)) }
        if value.encoding.kind != .union { return }
        for alternative in value.alternatives {
            names.append((GesSchemaPlanner.camel(alternative.base), "the alternative \(alternative.label) of \(field)"))
            nested(alternative, &names, field)
        }
    }

    private static func nestedName(_ value: GesPlanValue) -> String {
        switch value.encoding.kind {
        case .tag: value.base + "Tag"
        case .point: value.base + "Point"
        case .vector: value.base + "Vector"
        case .union: value.base + "Alternative"
        default: ""
        }
    }

    private static func typeName(_ value: GesPlanValue) -> String {
        switch value.encoding.kind {
        case .boolean: "Bool"
        case .integer: "Int64"
        case .quantized, .float: "Double"
        case .schema: value.encoding.schema
        default: nestedName(value)
        }
    }

    private static func zero(_ value: GesPlanValue) -> String {
        switch value.encoding.kind {
        case .boolean: "false"
        case .integer: "0"
        case .quantized, .float: "0.0"
        case .tag: "." + name(value.encoding.tags[0])
        case .union: "." + member(value.alternatives[0].label)
        default: typeName(value) + "()"
        }
    }

    private static func members(_ code: inout Code, _ field: GesPlanField, _ access: String) {
        code.blank()
        if field.optional {
            code.line("/// Whether `\(field.layout.name)` is present.")
            code.line("\(access)var has\(field.base) = false")
        }
        code.line("/// The field `\(field.layout.name)`\(description(field.value)).")
        code.line("\(access)var \(member(field.base)): \(typeName(field.value)) = \(zero(field.value))")
        if field.value.encoding.kind != .union { return }
        for alternative in field.value.alternatives where alternative.encoding.kind != .nothing {
            code.line("/// The value of `\(field.layout.name)` when its alternative is `\(alternative.label)`\(description(alternative)).")
            code.line("\(access)var \(member(alternative.base)): \(typeName(alternative)) = \(zero(alternative))")
        }
    }

    private static func description(_ value: GesPlanValue) -> String {
        let encoding = value.encoding
        switch encoding.kind {
        case .quantized, .float, .integer, .point, .vector: return encoding.unit != "none" ? ", in \(encoding.unit)" : ""
        case .union: return ", the selected alternative"
        default: return ""
        }
    }

    private static func methods(_ code: inout Code, _ type: GesPlanType, _ access: String) {
        code.line("/// Writes this record into a buffer; `maxByteCount` bytes always suffice.")
        code.line("/// - Returns: The number of bytes written.")
        code.line("/// - Throws: `GesWireError` when a value does not fit its field or the buffer is too small.")
        code.open("\(access)func write(into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {")
        code.line("var writer = GesBitWriter(into: buffer)")
        code.line("try __writeFields(&writer, Self.__paths, 0)")
        code.line("return try writer.finish()")
        code.close()
        code.blank()
        code.line("/// Writes this record into a new array of its exact size.")
        code.line("/// - Throws: `GesWireError` when a value does not fit its field.")
        code.open("\(access)func encoded() throws(GesWireError) -> [UInt8] {")
        code.line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Swift.max(Self.maxByteCount, 1), alignment: 8)")
        code.line("defer { buffer.deallocate() }")
        code.line("let count = try write(into: buffer)")
        code.line("return Array(buffer[0..<count])")
        code.close()
        code.blank()
        code.line("/// Reads a record from untrusted data into this instance. After an error its values are unspecified.")
        code.line("/// - Parameters:")
        code.line("///   - data: The data; it may continue after the record.")
        code.line("///   - byteLimit: An optional limit on the bytes of this record.")
        code.line("/// - Returns: The number of bytes the record occupies.")
        code.line("/// - Throws: `GesWireError` when the data is no valid record.")
        code.line("@discardableResult")
        code.open("\(access)mutating func read(from data: UnsafeRawBufferPointer, byteLimit: Int = .max) throws(GesWireError) -> Int {")
        code.line("var reader = GesBitReader(data: data, byteLimit: byteLimit)")
        code.line("try __readFields(&reader, Self.__paths, 0)")
        code.line("return try reader.finish()")
        code.close()
        code.blank()
        code.line("/// Reads a record from bytes into this instance. After an error its values are unspecified.")
        code.line("/// - Returns: The number of bytes the record occupies.")
        code.line("/// - Throws: `GesWireError` when the bytes are no valid record.")
        code.line("@discardableResult")
        code.open("\(access)mutating func read(from bytes: [UInt8], byteLimit: Int = .max) throws(GesWireError) -> Int {")
        code.line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Swift.max(bytes.count, 1), alignment: 8)")
        code.line("defer { buffer.deallocate() }")
        code.line("buffer.copyBytes(from: bytes)")
        code.line("return try read(from: UnsafeRawBufferPointer(rebasing: buffer[0..<bytes.count]), byteLimit: byteLimit)")
        code.close()
        if let key = type.fields.first, key.key {
            code.blank()
            code.line("/// Reads the key, which comes first, before a caller chooses a baseline.")
            code.line("/// - Throws: `GesWireError` when the data ends before the key does or holds an invalid key.")
            code.open("\(access)static func readKey(from data: UnsafeRawBufferPointer) throws(GesWireError) -> \(typeName(key.value)) {")
            code.line("var reader = GesBitReader(data: data)")
            code.line("let paths = Self.__paths")
            code.line("let at = 0")
            code.line("var key: \(typeName(key.value)) = \(zero(key.value))")
            read(&code, key.value, "key")
            code.line("return key")
            code.close()
        }
        code.blank()
        code.open("func __writeFields(_ writer: inout GesBitWriter, _ paths: [String], _ at: Int) throws(GesWireError) {")
        for field in type.fields {
            let target = "self." + member(field.base)
            if field.optional {
                code.open("if self.has\(field.base) {")
                code.line("try writer.write(1, width: 1)")
                write(&code, field.value, target)
                code.close("} else {")
                code.line("    try writer.write(0, width: 1)")
                code.line("}")
            } else {
                write(&code, field.value, target)
            }
        }
        code.close()
        code.blank()
        code.open("mutating func __readFields(_ reader: inout GesBitReader, _ paths: [String], _ at: Int) throws(GesWireError) {")
        for field in type.fields {
            let target = "self." + member(field.base)
            if field.optional {
                code.line("self.has\(field.base) = try reader.read(1, path: \(path(field.value))) != 0")
                code.open("if self.has\(field.base) {")
                read(&code, field.value, target)
                code.close("} else {")
                code.line("    \(target) = \(zero(field.value))")
                code.line("}")
            } else {
                read(&code, field.value, target)
            }
        }
        code.close()
    }

    private static func path(_ value: GesPlanValue) -> String { "paths[at + \(value.node)]" }

    private static func fail(_ code: String, _ value: GesPlanValue) -> String { "throw GesWireError(code: GesWireError.\(code), path: \(path(value)))" }

    private static func write(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let encoding = value.encoding
        let width = String(encoding.width)
        switch encoding.kind {
        case .boolean:
            code.line("try writer.write(\(target) ? 1 : 0, width: 1)")
        case .integer where !encoding.range:
            code.line("try writer.write(UInt64(bitPattern: \(target)), width: 64)")
        case .integer where encoding.wrapping:
            code.line("try writer.write(GesWireRules.wrappedIndex(\(target), lower: \(long(encoding.lower)), period: \(encoding.maxIndex &+ 1)), width: \(width))")
        case .integer:
            let lower = String(UInt64(bitPattern: encoding.lower))
            code.line("if \(target) < \(long(encoding.lower)) || UInt64(bitPattern: \(target)) &- \(lower) > \(encoding.maxIndex) { \(fail("outOfRange", value)) }")
            code.line("try writer.write(UInt64(bitPattern: \(target)) &- \(lower), width: \(width))")
        case .quantized where encoding.wrapping:
            code.line("try writer.write(")
            code.line(
                "    try GesWireRules.wrappedQuantizedIndex(\(target), tenPower: \(double(encoding.tenPower)), numerator: \(double(encoding.numerator)), lower: \(long(encoding.lower)), period: \(encoding.maxIndex &+ 1), path: \(path(value))),"
            )
            code.line("    width: \(width)")
            code.line(")")
        case .quantized:
            code.line("try writer.write(")
            code.line(
                "    try GesWireRules.quantizedIndex(\(target), lowerValue: \(bits(encoding.lowerValue)), upperValue: \(bits(encoding.upperValue)), tenPower: \(double(encoding.tenPower)), numerator: \(double(encoding.numerator)), lower: \(long(encoding.lower)), path: \(path(value))),"
            )
            code.line("    width: \(width)")
            code.line(")")
        case .float:
            code.line("if \(target).isNaN { \(fail("notRepresentable", value)) }")
            if encoding.range { code.line("if \(target) < \(bits(encoding.lowerValue)) || \(target) > \(bits(encoding.upperValue)) { \(fail("outOfRange", value)) }") }
            code.line("try writer.write((\(target) == 0 ? 0.0 : \(target)).bitPattern, width: 64)")
        case .tag:
            code.line("try writer.write(UInt64(\(target).rawValue), width: \(width))")
        case .point, .vector:
            for axis in value.axes { write(&code, axis, target + "." + member(axis.base)) }
        case .schema:
            code.line("try \(target).__writeFields(&writer, paths, at + \(value.node + 1))")
        case .union:
            code.line("switch \(target) {")
            for (index, alternative) in value.alternatives.enumerated() {
                code.open("case .\(member(alternative.label)):")
                code.line("try writer.write(\(index), width: \(width))")
                if alternative.encoding.kind != .nothing { write(&code, alternative, "self." + member(alternative.base)) }
                code.close("")
            }
            code.line("}")
        default:
            break
        }
    }

    private static func read(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let encoding = value.encoding
        let width = String(encoding.width)
        switch encoding.kind {
        case .boolean:
            code.line("\(target) = try reader.read(1, path: \(path(value))) != 0")
        case .integer where !encoding.range:
            code.line("\(target) = Int64(bitPattern: try reader.read(64, path: \(path(value))))")
        case .integer, .quantized, .tag:
            code.open("do {")
            code.line("let index = try reader.read(\(width), path: \(path(value)))")
            let largest = encoding.kind == .tag ? UInt64(encoding.tags.count) - 1 : encoding.maxIndex
            if largest < full(encoding.width) { code.line("if index > \(largest) { \(fail("invalidIndex", value)) }") }
            switch encoding.kind {
            case .integer: code.line("\(target) = Int64(bitPattern: \(UInt64(bitPattern: encoding.lower)) &+ index)")
            case .quantized:
                code.line("\(target) = GesWireRules.quantizedValue(index, lower: \(long(encoding.lower)), numerator: \(long(encoding.numerator)), tenPower: \(double(encoding.tenPower)))")
            default: code.line("\(target) = \(nestedName(value))(rawValue: \(underlying(encoding.tags.count))(index))!")
            }
            code.close()
        case .float:
            code.open("do {")
            code.line("let bits = try reader.read(64, path: \(path(value)))")
            code.line("let value = Double(bitPattern: bits)")
            code.line("if value.isNaN || bits == 0x8000_0000_0000_0000\(encoding.range ? " || value.isInfinite" : "") { \(fail("invalidNumber", value)) }")
            if encoding.range { code.line("if value < \(bits(encoding.lowerValue)) || value > \(bits(encoding.upperValue)) { \(fail("outOfRange", value)) }") }
            code.line("\(target) = value")
            code.close()
        case .point, .vector:
            for axis in value.axes { read(&code, axis, target + "." + member(axis.base)) }
        case .schema:
            code.line("try \(target).__readFields(&reader, paths, at + \(value.node + 1))")
        case .union:
            code.open("do {")
            code.line("let choice = try reader.read(\(width), path: \(path(value)))")
            let largest = UInt64(value.alternatives.count) - 1
            if largest < full(encoding.width) { code.line("if choice > \(largest) { \(fail("invalidIndex", value)) }") }
            code.line("\(target) = \(nestedName(value))(rawValue: \(underlying(value.alternatives.count))(choice))!")
            code.line("switch choice {")
            for (index, alternative) in value.alternatives.enumerated() where alternative.encoding.kind != .nothing {
                code.open("case \(index):")
                read(&code, alternative, "self." + member(alternative.base))
                code.close("")
            }
            code.open("default:")
            code.line("break")
            code.close("}")
            code.close()
        default:
            break
        }
    }

    private static func declarations(_ code: inout Code, _ value: GesPlanValue, _ access: String) {
        let name = nestedName(value)
        switch value.encoding.kind {
        case .tag:
            code.blank()
            code.line("/// The tags of the field, in declared order.")
            code.open("\(access)enum \(name): \(underlying(value.encoding.tags.count)), Sendable {")
            for tag in value.encoding.tags {
                code.line("/// The tag `#\(tag)`.")
                code.line("case \(self.name(tag))")
            }
            code.close()
        case .point, .vector:
            code.blank()
            code.line("/// The transmitted axes of the \(value.encoding.kind == .point ? "Point" : "Vector").")
            code.open("\(access)struct \(name): Sendable {")
            for axis in value.axes {
                code.line("/// The \(axis.label) axis.")
                code.line("\(access)var \(member(axis.base)) = 0.0")
            }
            code.blank()
            code.line("/// Creates a value with every axis at zero.")
            code.line("\(access)init() {}")
            code.close()
        case .union:
            code.blank()
            code.line("/// The alternatives of the field, in declared order.")
            code.open("\(access)enum \(name): \(underlying(value.alternatives.count)), Sendable {")
            for alternative in value.alternatives {
                code.line("/// The alternative `:\(alternative.label)`.")
                code.line("case \(member(alternative.label))")
            }
            code.close()
            for alternative in value.alternatives { declarations(&code, alternative, access) }
        default:
            break
        }
    }

    private static func underlying(_ count: Int) -> String { count <= 256 ? "UInt8" : count <= 65536 ? "UInt16" : "UInt32" }

    private static func full(_ width: Int) -> UInt64 { width >= 64 ? .max : (1 << UInt64(width)) - 1 }

    private static func long(_ value: Int64) -> String { value == .min ? "Int64.min" : String(value) }

    private static func double(_ value: Int64) -> String { "\(value).0" }

    private static func bits(_ value: Double) -> String { "Double(bitPattern: 0x\(GesSchemaLayoutJson.hex(value.bitPattern)))" }
}
