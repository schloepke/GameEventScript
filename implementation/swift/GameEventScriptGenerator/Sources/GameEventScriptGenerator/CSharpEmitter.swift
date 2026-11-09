// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Writes C# 9 for Unity and .NET: a struct per fixed-width schema and a class per schema with Text or List storage, with
// fields in wire order, Write and Read over the bit primitives, and constants inlined as literals or bit patterns.
enum GesCSharpEmitter {
    static let keywords: Set<String> = [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
        "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while",
    ]

    // Members that converting to and from GES values adds.
    static let valueReserved = ["ToValue", "ReadValue", "ToMessage", "ReadMessage"]

    // Members every generated type declares, and those every C# object inherits.
    static let reserved = [
        "SchemaHash", "BindingHash", "MaxByteCount", "Write", "Read", "ReadKey", "ToBytes", "WriteDelta", "ToDeltaBytes", "ReadDelta", "WireEquals", "CopyFrom", "Quantize", "Equals",
        "GetHashCode", "ToString", "GetType",
    ]

    typealias Code = GesSchemaPlanner.Code
    typealias Names = [(name: String, origin: String)]

    static func emit(_ layout: GesSchemaLayout, _ types: [GesPlanType], _ options: GesSchemaGenerateOptions) throws(GesGeneratorError) -> String {
        let access = options.access == .public ? "public" : "internal"
        let space = try GesSchemaPlanner.namespace(options.namespace, keywords)
        var code = Code()
        code.raw(GesSchemaPlanner.header(layout))
        code.blank()
        code.line("#nullable enable")
        code.blank()
        code.line("using System;")
        code.line("using System.Collections.Generic;")
        code.line("using GameEventScript.Api;")
        code.blank()
        if !space.isEmpty {
            code.line("namespace " + space.joined(separator: "."))
            code.open("{")
        }
        let holder = "__GesSchemas" + GesSchemaPlanner.hash(GesSchemaPlanner.header(layout))
        let context = Context(elements: GesSchemaPlanner.keyedElements(types), global: "global::" + space.map { $0 + "." }.joined(), values: options.values, holder: holder)
        for (index, type) in types.enumerated() {
            if index > 0 { code.blank() }
            if type.kind == .messages { try messageSet(&code, type, access, context) } else { try record(&code, type, access, context) }
        }
        if options.values { valuesHolder(&code, layout, holder) }
        if !space.isEmpty { code.close() }
        return code.text
    }

    // The keyed-list elements and the qualifier of the top-level types, which a member of the same name cannot hide.
    struct Context {
        let elements: Set<String>
        let global: String
        // Whether records convert to and from GES values through the helper type that holds the embedded sources.
        let values: Bool
        let holder: String
    }

    static func kind(_ type: GesPlanType) -> String { type.kind == .message ? "message" : "schema" }

    static func record(_ code: inout Code, _ type: GesPlanType, _ access: String, _ context: Context) throws(GesGeneratorError) {
        var names: Names = [(type.name, "the type itself")]
        if !type.isItem {
            for name in reserved { names.append((name, "the generated member " + name)) }
        }
        if !type.isItem && context.values {
            for name in valueReserved { names.append((name, "the generated member " + name)) }
        }
        for field in type.fields {
            names.append((field.base, "the field " + field.layout.name))
            if field.optional { names.append(("Has" + field.base, "the presence of " + field.layout.name)) }
            if field.optional && plain(field.value) { names.append((field.base + "Optional", "the optional value of " + field.layout.name)) }
            nested(field.value, &names, field.layout.name)
        }
        for tag in type.tags { names.append((tag.base, "the tag #" + tag.tag)) }
        try GesSchemaPlanner.unique(type.name, names)

        let visibility = type.isItem ? "public" : access
        code.line(
            type.isItem
                ? "/// <summary>An element of the list.</summary>"
                : "/// <summary>The \(kind(type)) <c>:\(type.name)</c>, read and written without allocation\(type.isClass ? " once its storage has grown" : "").</summary>"
        )
        code.line(type.isClass ? "\(visibility) sealed partial class \(type.name)" : "\(visibility) partial struct \(type.name)")
        code.open("{")
        if !type.isItem {
            constants(&code, type.layout)
            code.blank()
            code.line("internal static readonly string[] __paths =")
            code.open("{")
            for path in type.paths { code.line(GesSchemaPlanner.quote(path) + ",") }
            code.close("};")
        }
        var first = type.isItem
        for field in type.fields {
            members(&code, field, first)
            first = false
        }
        for tag in type.tags {
            code.blank()
            code.line("/// <summary>Whether the tag <c>#\(tag.tag)</c> is set.</summary>")
            code.line("public bool \(tag.base);")
        }
        methods(&code, type)
        let deltas = type.kind == .schema && !type.isItem
        if deltas { sameHelpers(&code, type, context.global) }
        extras(&code, type, deltas, context.elements.contains(type.name))
        if context.values && !type.isItem { valueMembers(&code, type, context.holder) }
        for field in type.fields { try declarations(&code, field.value, access, context) }
        code.close()
    }

    static func constants(_ code: inout Code, _ layout: GesLayoutType) {
        code.line("/// <summary>The schema hash, independent of parameter values.</summary>")
        code.line("public const ulong SchemaHash = 0x\(GesSchemaLayoutJson.hex(layout.hash))UL;")
        code.blank()
        code.line("/// <summary>The hash over the schema hash and the parameter values this code was generated with.</summary>")
        code.line("public const ulong BindingHash = 0x\(GesSchemaLayoutJson.hex(layout.bindingHash))UL;")
        code.blank()
        code.line("/// <summary>The size of the largest encoding in bytes.</summary>")
        code.line("public const int MaxByteCount = \(layout.maxByteCount);")
    }

    // Whether an optional value also has a nullable view: a value type without storage.
    static func plain(_ value: GesPlanValue) -> Bool { !value.isClass && value.encoding.kind != .union && value.encoding.kind != .text && value.encoding.kind != .list }

    // The nested types and members a value declares, for the collision check.
    static func nested(_ value: GesPlanValue, _ names: inout Names, _ field: String) {
        let name = nestedName(value)
        if !name.isEmpty { names.append((name, "the type of " + field)) }
        switch value.encoding.kind {
        case .text:
            names.append((value.base + "Utf8", "the UTF-8 bytes of " + field))
            names.append(("Set" + value.base + "Utf8", "the UTF-8 setter of " + field))
        case .list:
            if let item = value.item { names.append((item.name, "the element type of " + field)) } else { nested(value.element!, &names, field) }
        case .union:
            for alternative in value.alternatives {
                if alternative.encoding.kind != .nothing { names.append((alternative.base, "the alternative \(alternative.label) of \(field)")) }
                nested(alternative, &names, field)
            }
        default:
            break
        }
    }

    static func nestedName(_ value: GesPlanValue) -> String {
        switch value.encoding.kind {
        case .tag: value.base + "Tag"
        case .point: value.base + "Point"
        case .vector: value.base + "Vector"
        case .union: value.base + "Alternative"
        default: ""
        }
    }

    static func typeName(_ value: GesPlanValue) -> String {
        switch value.encoding.kind {
        case .boolean: "bool"
        case .integer: "long"
        case .quantized, .float: "double"
        case .schema: value.encoding.schema
        case .text: "string"
        case .list: "List<\(elementName(value))>"
        default: nestedName(value)
        }
    }

    static func elementName(_ list: GesPlanValue) -> String { list.item?.name ?? typeName(list.element!) }

    // A class reference starts as an instance, so that a record can be filled without allocating later.
    static func initializer(_ value: GesPlanValue) -> String {
        switch value.encoding.kind {
        case .schema where value.isClass: " = new " + value.encoding.schema + "();"
        case .list: " = new List<\(elementName(value))>();"
        default: ";"
        }
    }

    static func members(_ code: inout Code, _ field: GesPlanField, _ first: Bool) {
        if !first { code.blank() }
        if field.optional {
            code.line("/// <summary>Whether <c>\(field.layout.name)</c> is present.</summary>")
            code.line("public bool Has\(field.base);")
            code.blank()
        }
        member(&code, field.value, "The field <c>\(field.layout.name)</c>")
        if field.optional && plain(field.value) {
            let type = typeName(field.value)
            code.blank()
            code.line("/// <summary>The field <c>\(field.layout.name)</c> as a nullable value, null when absent.</summary>")
            code.line("public \(type)? \(field.base)Optional")
            code.open("{")
            code.line("get => Has\(field.base) ? \(field.base) : (\(type)?)null;")
            code.line("set")
            code.open("{")
            code.line("Has\(field.base) = value.HasValue;")
            code.line("\(field.base) = value.GetValueOrDefault();")
            code.close()
            code.close()
        }
        if field.value.encoding.kind != .union { return }
        for alternative in field.value.alternatives where alternative.encoding.kind != .nothing {
            code.blank()
            member(&code, alternative, "The value of <c>\(field.layout.name)</c> when its alternative is <c>\(alternative.label)</c>")
        }
    }

    // Declares the member that stores a value, with its storage and conveniences.
    static func member(_ code: inout Code, _ value: GesPlanValue, _ summary: String) {
        if value.encoding.kind != .text {
            code.line("/// <summary>\(summary)\(description(value)).</summary>")
            code.line("public \(typeName(value)) \(value.base)\(initializer(value))")
            return
        }
        let storage = "__" + value.base
        let max = String(value.encoding.maxCount)
        code.line("private byte[] \(storage) = Array.Empty<byte>();")
        code.line("private int \(storage)Length;")
        code.blank()
        code.line("/// <summary>\(summary) as UTF-8 bytes\(value.encoding.tagName ? ", a tag name without #" : "").</summary>")
        code.line("public ReadOnlySpan<byte> \(value.base)Utf8 => new ReadOnlySpan<byte>(\(storage), 0, \(storage)Length);")
        code.blank()
        code.line("/// <summary>Sets \(summary.prefix(1).lowercased() + summary.dropFirst()) from UTF-8 bytes, which are checked when the record is written.</summary>")
        code.line("/// <param name=\"utf8\">The bytes.</param>")
        code.line("public void Set\(value.base)Utf8(ReadOnlySpan<byte> utf8)")
        code.open("{")
        code.line("if (\(storage).Length < utf8.Length) Array.Resize(ref \(storage), Math.Max(utf8.Length, Math.Min(\(max), \(storage).Length * 2)));")
        code.line("utf8.CopyTo(\(storage));")
        code.line("\(storage)Length = utf8.Length;")
        code.close()
        code.blank()
        code.line("/// <summary>\(summary) as a string; reading and setting it allocate.</summary>")
        code.line("public string \(value.base)")
        code.open("{")
        code.line("get => System.Text.Encoding.UTF8.GetString(\(storage), 0, \(storage)Length);")
        code.line("set => Set\(value.base)Utf8(System.Text.Encoding.UTF8.GetBytes(value));")
        code.close()
    }

    static func description(_ value: GesPlanValue) -> String {
        let encoding = value.encoding
        switch encoding.kind {
        case .quantized, .float, .integer, .point, .vector: return encoding.unit != "none" ? ", in \(encoding.unit)" : ""
        case .union: return ", the selected alternative"
        case .list: return ", at most \(encoding.maxCount) elements"
        default: return ""
        }
    }

    static func methods(_ code: inout Code, _ type: GesPlanType) {
        let selfModifier = type.isClass ? "" : "readonly "
        if !type.isItem {
            code.blank()
            publicMethods(&code, selfModifier, ["__WriteFields(ref writer, __paths, 0);"], ["__ReadFields(ref reader, __paths, 0);"], "record")
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
        }
        code.blank()
        code.line("internal \(selfModifier)void __WriteFields(ref GesBitWriter writer, string[] paths, int at)")
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
        for tag in type.tags { code.line("writer.Write(\(tag.base) ? 1UL : 0UL, 1);") }
        code.close()
        code.blank()
        code.line("internal void __ReadFields(ref GesBitReader reader, string[] paths, int at)")
        code.open("{")
        for field in type.fields {
            if field.optional {
                code.line("Has\(field.base) = reader.Read(1, \(path(field.value))) != 0;")
                code.line("if (Has\(field.base))")
                code.open("{")
                read(&code, field.value, field.base)
                code.close()
                if plain(field.value) { code.line("else \(field.base) = default;") }
            } else {
                read(&code, field.value, field.base)
            }
        }
        for tag in type.tags { code.line("\(tag.base) = reader.Read(1, paths[at + \(tag.node)]) != 0;") }
        code.close()
    }

    static func publicMethods(_ code: inout Code, _ selfModifier: String, _ write: [String], _ read: [String], _ what: String) {
        code.line("/// <summary>Writes this \(what) into a buffer; <see cref=\"MaxByteCount\"/> bytes always suffice.</summary>")
        code.line("/// <param name=\"buffer\">The buffer.</param>")
        code.line("/// <returns>The number of bytes written.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A value does not fit its field, or the buffer is too small.</exception>")
        code.line("public \(selfModifier)int Write(Span<byte> buffer)")
        code.open("{")
        code.line("var writer = new GesBitWriter(buffer);")
        for line in write { code.line(line) }
        code.line("return writer.Finish();")
        code.close()
        code.blank()
        code.line("/// <summary>Writes this \(what) into a new array of its exact size.</summary>")
        code.line("/// <returns>The bytes.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>")
        code.line("public \(selfModifier)byte[] ToBytes()")
        code.open("{")
        code.line("var buffer = new byte[MaxByteCount];")
        code.line("return buffer.AsSpan(0, Write(buffer)).ToArray();")
        code.close()
        code.blank()
        code.line("/// <summary>Reads a \(what) from untrusted data into this instance. After an error its values are unspecified.</summary>")
        code.line("/// <param name=\"data\">The data; it may continue after the record.</param>")
        code.line("/// <param name=\"byteLimit\">An optional limit on the bytes of this record.</param>")
        code.line("/// <returns>The number of bytes the record occupies.</returns>")
        code.line("/// <exception cref=\"GesWireException\">The data is no valid \(what).</exception>")
        code.line("public int Read(ReadOnlySpan<byte> data, long byteLimit = long.MaxValue)")
        code.open("{")
        code.line("var reader = new GesBitReader(data, byteLimit);")
        for line in read { code.line(line) }
        code.line("return reader.Finish();")
        code.close()
    }

    static func messageSet(_ code: inout Code, _ type: GesPlanType, _ access: String, _ context: Context) throws(GesGeneratorError) {
        var names: Names = [(type.name, "the type itself"), ("Selected", "the selected member"), ("Member", "the member type")]
        for name in reserved { names.append((name, "the generated member " + name)) }
        if context.values {
            for name in valueReserved { names.append((name, "the generated member " + name)) }
        }
        for member in type.members { names.append((member, "the member " + member)) }
        try GesSchemaPlanner.unique(type.name, names)
        let width = String(type.layout.memberWidth)
        let last = unsigned(UInt64(type.members.count) - 1)
        code.line(
            "/// <summary>The message set <c>:\(type.name)</c>: one of its messages, read and written without allocation\(type.membersAreClasses ? " once its storage has grown" : "").</summary>"
        )
        code.line(type.membersAreClasses ? "\(access) sealed partial class \(type.name)" : "\(access) partial struct \(type.name)")
        code.open("{")
        constants(&code, type.layout)
        code.blank()
        code.line("/// <summary>The message this record holds.</summary>")
        code.line("public Member Selected;")
        for member in type.members {
            code.blank()
            code.line("/// <summary>The message when <see cref=\"Selected\"/> is <c>\(member)</c>.</summary>")
            code.line("public \(member) \(member)\(type.membersAreClasses ? " = new \(member)();" : ";")")
        }
        var write = [
            "if ((ulong)Selected > \(last)) throw new GesWireException(GesWireException.OutOfRange, \"\");", "writer.Write((ulong)Selected, \(width));", "switch (Selected)", "{",
        ]
        var read = ["var index = reader.Read(\(width), \"\");"]
        if UInt64(type.members.count) - 1 < full(type.layout.memberWidth) { read.append("if (index > \(last)) throw new GesWireException(GesWireException.InvalidIndex, \"\");") }
        read += ["Selected = (Member)index;", "switch (Selected)", "{"]
        for member in type.members {
            write += ["    case Member.\(member):", "        \(member).__WriteFields(ref writer, \(member).__paths, 0);", "        break;"]
            read += ["    case Member.\(member):", "        \(member).__ReadFields(ref reader, \(member).__paths, 0);", "        break;"]
        }
        write.append("}")
        read.append("}")
        code.blank()
        publicMethods(&code, type.membersAreClasses ? "" : "readonly ", write, read, "message")
        if context.values { valueMembers(&code, type, context.holder) }
        code.blank()
        code.line("/// <summary>The messages of the set, in declared order.</summary>")
        code.line("public enum Member : \(underlying(type.members.count))")
        code.open("{")
        for member in type.members {
            code.line("/// <summary>The message <c>\(member)</c>.</summary>")
            code.line(member + ",")
        }
        code.close()
        code.close()
    }

    static func path(_ value: GesPlanValue) -> String { "paths[at + \(value.node)]" }

    static func fail(_ code: String, _ value: GesPlanValue) -> String { "throw new GesWireException(GesWireException.\(code), \(path(value)));" }

    // Writes the value in `target`, which the caller has validated as present.
    static func write(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let encoding = value.encoding
        let width = String(encoding.width)
        let max = String(encoding.maxCount)
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
        case .text:
            code.open("{")
            code.line("var text = new ReadOnlySpan<byte>(__\(value.base), 0, __\(value.base)Length);")
            code.line("if (text.Length > \(max)) \(fail("TooLong", value))")
            code.line(encoding.tagName ? "if (!GesWireRules.IsTagName(text)) \(fail("InvalidTag", value))" : "if (!GesWireRules.IsValidUtf8(text)) \(fail("InvalidText", value))")
            code.line("writer.Write((ulong)text.Length, \(width));")
            code.line("writer.WriteBytes(text);")
            code.close()
        case .list:
            code.line("if (\(target).Count > \(max)) \(fail("TooLong", value))")
            code.line("writer.Write((ulong)\(target).Count, \(width));")
            code.line("foreach (var item in \(target))")
            code.open("{")
            if value.item != nil {
                code.line("item.__WriteFields(ref writer, paths, at + \(value.element!.node));")
            } else {
                write(&code, value.element!, "item")
            }
            code.close()
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
        case .nothing:
            break
        }
    }

    // Reads into `target`, after the caller has read a presence bit.
    static func read(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let encoding = value.encoding
        let width = String(encoding.width)
        let max = String(encoding.maxCount)
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
        case .text:
            let storage = "__" + value.base
            code.open("{")
            code.line("var count = (int)reader.Read(\(width), \(path(value)));")
            code.line("if (count > \(max)) \(fail("InvalidCount", value))")
            code.line("reader.Require(count * 8L, \(path(value)));")
            code.line("if (\(storage).Length < count) Array.Resize(ref \(storage), Math.Max(count, Math.Min(\(max), \(storage).Length * 2)));")
            code.line("var text = new Span<byte>(\(storage), 0, count);")
            code.line("reader.ReadBytes(text, \(path(value)));")
            code.line(encoding.tagName ? "if (!GesWireRules.IsTagName(text)) \(fail("InvalidTag", value))" : "if (!GesWireRules.IsValidUtf8(text)) \(fail("InvalidText", value))")
            code.line("\(storage)Length = count;")
            code.close()
        case .list:
            let element = value.element!
            let name = elementName(value)
            code.open("{")
            code.line("var count = (int)reader.Read(\(width), \(path(value)));")
            code.line("if (count > \(max)) \(fail("InvalidCount", value))")
            code.line("\(target).Clear();")
            code.line("for (var position = 0; position < count; position++)")
            code.open("{")
            if element.isClass {
                code.line("if (position == __\(value.base)Pool.Count) __\(value.base)Pool.Add(new \(name)());")
                code.line("var item = __\(value.base)Pool[position];")
            } else if value.item != nil || element.encoding.kind == .point || element.encoding.kind == .vector || element.encoding.kind == .schema {
                code.line("var item = new \(name)();")
            } else {
                code.line("\(name) item;")
            }
            if value.item != nil {
                code.line("item.__ReadFields(ref reader, paths, at + \(element.node));")
            } else {
                read(&code, element, "item")
            }
            code.line("\(target).Add(item);")
            code.close()
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
        case .nothing:
            break
        }
    }

    static func declarations(_ code: inout Code, _ value: GesPlanValue, _ access: String, _ context: Context) throws(GesGeneratorError) {
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
            for alternative in value.alternatives { try declarations(&code, alternative, access, context) }
        case .list:
            if value.element!.isClass {
                code.blank()
                code.line("private readonly List<\(elementName(value))> __\(value.base)Pool = new List<\(elementName(value))>();")
            }
            if let item = value.item {
                code.blank()
                try record(&code, item, access, context)
            } else {
                try declarations(&code, value.element!, access, context)
            }
        default:
            break
        }
    }

    static func underlying(_ count: Int) -> String { count <= 256 ? "byte" : count <= 65536 ? "ushort" : "uint" }

    static func full(_ width: Int) -> UInt64 { width >= 64 ? .max : (1 << UInt64(width)) - 1 }

    static func long(_ value: Int64) -> String { value == .min ? "long.MinValue" : "\(value)L" }

    static func unsigned(_ value: UInt64) -> String { "\(value)UL" }

    // An integral binary64 such as 10^k or p, exact as a literal.
    static func double(_ value: Int64) -> String { "\(value)d" }

    static func bits(_ value: Double) -> String { "BitConverter.Int64BitsToDouble(unchecked((long)0x\(GesSchemaLayoutJson.hex(value.bitPattern))UL))" }
}
