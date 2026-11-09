// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Writes C# 9 for Unity and .NET: a struct per fixed-width schema with fields in wire order, Write and Read over the bit
// primitives, and constants inlined as literals or bit patterns.
enum GesCSharpEmitter {
    private static let keywords: Set<String> = [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
        "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while",
    ]

    // Members every generated type declares, and those every C# struct inherits.
    private static let reserved = ["SchemaHash", "BindingHash", "MaxByteCount", "Write", "Read", "ReadKey", "ToBytes", "Equals", "GetHashCode", "ToString", "GetType"]

    typealias Code = GesSchemaPlanner.Code

    static func emit(_ layout: GesSchemaLayout, _ types: [GesPlanType], _ options: GesSchemaGenerateOptions) throws(GesGeneratorError) -> String {
        let access = options.access == .public ? "public" : "internal"
        let space = try GesSchemaPlanner.namespace(options.namespace, keywords)
        var code = Code()
        code.raw(GesSchemaPlanner.header(layout))
        code.blank()
        code.line("#nullable enable")
        code.blank()
        code.line("using System;")
        code.line("using GameEventScript.Api;")
        code.blank()
        if !space.isEmpty {
            code.line("namespace " + space.joined(separator: "."))
            code.open("{")
        }
        for (index, type) in types.enumerated() {
            if index > 0 { code.blank() }
            try self.type(&code, type, access)
        }
        if !space.isEmpty { code.close() }
        return code.text
    }

    private static func type(_ code: inout Code, _ type: GesPlanType, _ access: String) throws(GesGeneratorError) {
        var names: [(name: String, origin: String)] = [(type.name, "the type itself")]
        for name in reserved { names.append((name, "the generated member " + name)) }
        for field in type.fields {
            names.append((field.base, "the field " + field.layout.name))
            if field.optional { names.append(("Has" + field.base, "the presence of " + field.layout.name)) }
            nested(field.value, &names, field.layout.name)
        }
        try GesSchemaPlanner.unique(type.name, names)

        code.line("/// <summary>The schema <c>:\(type.name)</c>, read and written without allocation.</summary>")
        code.line("\(access) partial struct \(type.name)")
        code.open("{")
        code.line("/// <summary>The schema hash, independent of parameter values.</summary>")
        code.line("public const ulong SchemaHash = 0x\(GesSchemaLayoutJson.hex(type.layout.hash))UL;")
        code.blank()
        code.line("/// <summary>The hash over the schema hash and the parameter values this code was generated with.</summary>")
        code.line("public const ulong BindingHash = 0x\(GesSchemaLayoutJson.hex(type.layout.bindingHash))UL;")
        code.blank()
        code.line("/// <summary>The size of the largest encoding in bytes.</summary>")
        code.line("public const int MaxByteCount = \(type.layout.maxByteCount);")
        code.blank()
        code.line("internal static readonly string[] __paths =")
        code.open("{")
        for path in type.paths { code.line(quote(path) + ",") }
        code.close("};")
        for field in type.fields { members(&code, field) }
        code.blank()
        methods(&code, type)
        for field in type.fields { declarations(&code, field.value, access) }
        code.close()
    }

    // The nested types a value declares, for the collision check.
    private static func nested(_ value: GesPlanValue, _ names: inout [(name: String, origin: String)], _ field: String) {
        let name = nestedName(value)
        if !name.isEmpty { names.append((name, "the type of " + field)) }
        if value.encoding.kind == .union {
            for alternative in value.alternatives {
                names.append((alternative.base, "the alternative \(alternative.label) of \(field)"))
                nested(alternative, &names, field)
            }
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
        case .boolean: "bool"
        case .integer: "long"
        case .quantized, .float: "double"
        case .schema: value.encoding.schema
        default: nestedName(value)
        }
    }

    private static func members(_ code: inout Code, _ field: GesPlanField) {
        code.blank()
        if field.optional {
            code.line("/// <summary>Whether <c>\(field.layout.name)</c> is present.</summary>")
            code.line("public bool Has\(field.base);")
            code.blank()
        }
        code.line("/// <summary>The field <c>\(field.layout.name)</c>\(description(field.value)).</summary>")
        code.line("public \(typeName(field.value)) \(field.base);")
        if field.value.encoding.kind != .union { return }
        for alternative in field.value.alternatives where alternative.encoding.kind != .nothing {
            code.blank()
            code.line("/// <summary>The value of <c>\(field.layout.name)</c> when its alternative is <c>\(alternative.label)</c>\(description(alternative)).</summary>")
            code.line("public \(typeName(alternative)) \(alternative.base);")
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

    private static func methods(_ code: inout Code, _ type: GesPlanType) {
        code.line("/// <summary>Writes this record into a buffer; <see cref=\"MaxByteCount\"/> bytes always suffice.</summary>")
        code.line("/// <param name=\"buffer\">The buffer.</param>")
        code.line("/// <returns>The number of bytes written.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A value does not fit its field, or the buffer is too small.</exception>")
        code.line("public readonly int Write(Span<byte> buffer)")
        code.open("{")
        code.line("var writer = new GesBitWriter(buffer);")
        code.line("__WriteFields(ref writer, __paths, 0);")
        code.line("return writer.Finish();")
        code.close()
        code.blank()
        code.line("/// <summary>Writes this record into a new array of its exact size.</summary>")
        code.line("/// <returns>The bytes.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>")
        code.line("public readonly byte[] ToBytes()")
        code.open("{")
        code.line("var buffer = new byte[MaxByteCount];")
        code.line("return buffer.AsSpan(0, Write(buffer)).ToArray();")
        code.close()
        code.blank()
        code.line("/// <summary>Reads a record from untrusted data into this instance. After an error its values are unspecified.</summary>")
        code.line("/// <param name=\"data\">The data; it may continue after the record.</param>")
        code.line("/// <param name=\"byteLimit\">An optional limit on the bytes of this record.</param>")
        code.line("/// <returns>The number of bytes the record occupies.</returns>")
        code.line("/// <exception cref=\"GesWireException\">The data is no valid record.</exception>")
        code.line("public int Read(ReadOnlySpan<byte> data, long byteLimit = long.MaxValue)")
        code.open("{")
        code.line("var reader = new GesBitReader(data, byteLimit);")
        code.line("__ReadFields(ref reader, __paths, 0);")
        code.line("return reader.Finish();")
        code.close()
        if let key = type.fields.first, key.key {
            code.blank()
            code.line("/// <summary>Reads the key, which comes first, before a caller chooses a baseline.</summary>")
            code.line("/// <param name=\"data\">The data.</param>")
            code.line("/// <returns>The key.</returns>")
            code.line("/// <exception cref=\"GesWireException\">The data ends before the key does or holds an invalid key.</exception>")
            code.line("public static \(typeName(key.value)) ReadKey(ReadOnlySpan<byte> data)")
            code.open("{")
            code.line("var reader = new GesBitReader(data);")
            code.line("var paths = __paths;")
            code.line("const int at = 0;")
            code.line("\(typeName(key.value)) key;")
            read(&code, key.value, "key")
            code.line("return key;")
            code.close()
        }
        code.blank()
        code.line("internal readonly void __WriteFields(ref GesBitWriter writer, string[] paths, int at)")
        code.open("{")
        for field in type.fields {
            if field.optional {
                code.line("if (Has\(field.base))")
                code.open("{")
                code.line("writer.Write(1, 1);")
                write(&code, field.value, field.base)
                code.close()
                code.line("else writer.Write(0, 1);")
            } else {
                write(&code, field.value, field.base)
            }
        }
        code.close()
        code.blank()
        code.line("internal void __ReadFields(ref GesBitReader reader, string[] paths, int at)")
        code.open("{")
        for field in type.fields {
            if field.optional {
                code.line("Has\(field.base) = reader.Read(1, \(path(field.value))) != 0;")
                code.line("if (!Has\(field.base)) \(field.base) = default;")
                code.line("else")
                code.open("{")
                read(&code, field.value, field.base)
                code.close()
            } else {
                read(&code, field.value, field.base)
            }
        }
        code.close()
    }

    private static func path(_ value: GesPlanValue) -> String { "paths[at + \(value.node)]" }

    private static func fail(_ code: String, _ value: GesPlanValue) -> String { "throw new GesWireException(GesWireException.\(code), \(path(value)));" }

    // Writes the value in `target`, which the caller has validated as present.
    private static func write(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let encoding = value.encoding
        let width = String(encoding.width)
        switch encoding.kind {
        case .boolean:
            code.line("writer.Write(\(target) ? 1UL : 0UL, 1);")
        case .integer where !encoding.range:
            code.line("writer.Write(unchecked((ulong)\(target)), 64);")
        case .integer where encoding.wrapping:
            code.line("writer.Write(GesWireRules.WrappedIndex(\(target), \(long(encoding.lower)), \(unsigned(encoding.maxIndex &+ 1))), \(width));")
        case .integer:
            let lower = unsigned(UInt64(bitPattern: encoding.lower))
            code.line("if (\(target) < \(long(encoding.lower)) || unchecked((ulong)\(target) - \(lower)) > \(unsigned(encoding.maxIndex))) \(fail("OutOfRange", value))")
            code.line("writer.Write(unchecked((ulong)\(target) - \(lower)), \(width));")
        case .quantized where encoding.wrapping:
            code.line(
                "writer.Write(GesWireRules.WrappedQuantizedIndex(\(target), \(double(encoding.tenPower)), \(double(encoding.numerator)), \(long(encoding.lower)), \(unsigned(encoding.maxIndex &+ 1)), \(path(value))), \(width));"
            )
        case .quantized:
            code.line(
                "writer.Write(GesWireRules.QuantizedIndex(\(target), \(bits(encoding.lowerValue)), \(bits(encoding.upperValue)), \(double(encoding.tenPower)), \(double(encoding.numerator)), \(long(encoding.lower)), \(path(value))), \(width));"
            )
        case .float:
            code.line("if (double.IsNaN(\(target))) \(fail("NotRepresentable", value))")
            if encoding.range { code.line("if (\(target) < \(bits(encoding.lowerValue)) || \(target) > \(bits(encoding.upperValue))) \(fail("OutOfRange", value))") }
            code.line("writer.Write(unchecked((ulong)BitConverter.DoubleToInt64Bits(\(target) == 0 ? 0d : \(target))), 64);")
        case .tag:
            code.line("if ((ulong)\(target) > \(unsigned(UInt64(encoding.tags.count) - 1))) \(fail("UnknownTag", value))")
            code.line("writer.Write((ulong)\(target), \(width));")
        case .point, .vector:
            for axis in value.axes { write(&code, axis, target + "." + axis.base) }
        case .schema:
            code.line("\(target).__WriteFields(ref writer, paths, at + \(value.node + 1));")
        case .union:
            code.line("switch (\(target))")
            code.open("{")
            for (index, alternative) in value.alternatives.enumerated() {
                code.line("case \(nestedName(value)).\(alternative.label):")
                code.open("{")
                code.line("writer.Write(\(index), \(width));")
                if alternative.encoding.kind != .nothing { write(&code, alternative, alternative.base) }
                code.line("break;")
                code.close()
            }
            code.line("default:")
            code.open("{")
            code.line(fail("OutOfRange", value))
            code.close()
            code.close()
        default:
            break
        }
    }

    // Reads into `target`, after the caller has read a presence bit.
    private static func read(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let encoding = value.encoding
        let width = String(encoding.width)
        switch encoding.kind {
        case .boolean:
            code.line("\(target) = reader.Read(1, \(path(value))) != 0;")
        case .integer where !encoding.range:
            code.line("\(target) = unchecked((long)reader.Read(64, \(path(value))));")
        case .integer, .quantized, .tag:
            code.open("{")
            code.line("var index = reader.Read(\(width), \(path(value)));")
            let largest = encoding.kind == .tag ? UInt64(encoding.tags.count) - 1 : encoding.maxIndex
            if largest < full(encoding.width) { code.line("if (index > \(unsigned(largest))) \(fail("InvalidIndex", value))") }
            switch encoding.kind {
            case .integer: code.line("\(target) = unchecked((long)(\(unsigned(UInt64(bitPattern: encoding.lower))) + index));")
            case .quantized: code.line("\(target) = GesWireRules.QuantizedValue(index, \(long(encoding.lower)), \(long(encoding.numerator)), \(double(encoding.tenPower)));")
            default: code.line("\(target) = (\(nestedName(value)))index;")
            }
            code.close()
        case .float:
            code.open("{")
            code.line("var bits = reader.Read(64, \(path(value)));")
            code.line("var value = BitConverter.Int64BitsToDouble(unchecked((long)bits));")
            code.line("if (double.IsNaN(value) || bits == 0x8000000000000000UL\(encoding.range ? " || double.IsInfinity(value)" : "")) \(fail("InvalidNumber", value))")
            if encoding.range { code.line("if (value < \(bits(encoding.lowerValue)) || value > \(bits(encoding.upperValue))) \(fail("OutOfRange", value))") }
            code.line("\(target) = value;")
            code.close()
        case .point, .vector:
            for axis in value.axes { read(&code, axis, target + "." + axis.base) }
        case .schema:
            code.line("\(target).__ReadFields(ref reader, paths, at + \(value.node + 1));")
        case .union:
            code.open("{")
            code.line("var choice = reader.Read(\(width), \(path(value)));")
            let largest = UInt64(value.alternatives.count) - 1
            if largest < full(encoding.width) { code.line("if (choice > \(unsigned(largest))) \(fail("InvalidIndex", value))") }
            code.line("\(target) = (\(nestedName(value)))choice;")
            code.line("switch (choice)")
            code.open("{")
            for (index, alternative) in value.alternatives.enumerated() where alternative.encoding.kind != .nothing {
                code.line("case \(index):")
                code.open("{")
                read(&code, alternative, alternative.base)
                code.line("break;")
                code.close()
            }
            code.close()
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
            code.line("/// <summary>The tags of the field, in declared order.</summary>")
            code.line("public enum \(name) : \(underlying(value.encoding.tags.count))")
            code.open("{")
            for tag in value.encoding.tags {
                code.line("/// <summary>The tag <c>#\(tag)</c>.</summary>")
                code.line(GesSchemaPlanner.pascal(tag) + ",")
            }
            code.close()
        case .point, .vector:
            code.blank()
            code.line("/// <summary>The transmitted axes of the \(value.encoding.kind == .point ? "Point" : "Vector").</summary>")
            code.line("public struct \(name)")
            code.open("{")
            for (index, axis) in value.axes.enumerated() {
                if index > 0 { code.blank() }
                code.line("/// <summary>The \(axis.label) axis.</summary>")
                code.line("public double \(axis.base);")
            }
            code.close()
        case .union:
            code.blank()
            code.line("/// <summary>The alternatives of the field, in declared order.</summary>")
            code.line("public enum \(name) : \(underlying(value.alternatives.count))")
            code.open("{")
            for alternative in value.alternatives {
                code.line("/// <summary>The alternative <c>:\(alternative.label)</c>.</summary>")
                code.line(alternative.label + ",")
            }
            code.close()
            for alternative in value.alternatives { declarations(&code, alternative, access) }
        default:
            break
        }
    }

    private static func underlying(_ count: Int) -> String { count <= 256 ? "byte" : count <= 65536 ? "ushort" : "uint" }

    private static func full(_ width: Int) -> UInt64 { width >= 64 ? .max : (1 << UInt64(width)) - 1 }

    private static func long(_ value: Int64) -> String { value == .min ? "long.MinValue" : "\(value)L" }

    private static func unsigned(_ value: UInt64) -> String { "\(value)UL" }

    // An integral binary64 such as 10^k or p, exact as a literal.
    private static func double(_ value: Int64) -> String { "\(value)d" }

    private static func bits(_ value: Double) -> String { "BitConverter.Int64BitsToDouble(unchecked((long)0x\(GesSchemaLayoutJson.hex(value.bitPattern))UL))" }

    private static func quote(_ text: String) -> String { GesSchemaPlanner.quote(text) }
}
