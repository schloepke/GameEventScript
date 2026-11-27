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
    static let valueReserved = ["ToValue", "ReadValue", "ToMessage", "ReadMessage", "ReadMessageLenient"]

    // Members every generated type declares, and those every C# object inherits.
    static let reserved = [
        "SchemaHash", "BindingHash", "MaxByteCount", "Write", "Read", "ReadKey", "ToBytes", "ByteCount", "WriteDelta", "ToDeltaBytes", "DeltaByteCount", "ReadDelta", "WireEquals", "CopyFrom", "Quantize", "Equals",
        "GetHashCode", "ToString", "GetType",
    ]

    typealias Code = GesSchemaPlanner.Code
    typealias Names = [(name: String, origin: String)]

    static func emit(
        _ layout: GesSchemaLayout,
        _ types: [GesPlanType],
        _ options: GesSchemaGenerateOptions,
        _ packed: GesModelLayout?,
        _ modelLayout: GesModelLayout?
    ) throws(GesGeneratorError) -> GesCodeDocument {
        let access = options.access == .public ? "public" : "internal"
        let space = try GesSchemaPlanner.namespace(options.namespace, keywords)
        var prologue = Code()
        prologue.blank()
        prologue.line("#nullable enable")
        prologue.blank()
        prologue.line("using System;")
        prologue.line("using System.Collections.Generic;")
        prologue.line("using GameEventScript.Api;")
        if !layout.networks.isEmpty { prologue.line("using GameEventScript.Network;") }
        prologue.blank()
        let header = GesSchemaPlanner.header(layout)
        let schemaSet = try GesSchemaSetReference.from(options)
        let holder = schemaSet?.qualified ?? "__GesSchemas" + GesSchemaPlanner.suffix(header, options)
        let global = "global::" + space.map { $0 + "." }.joined()
        let computed = GesSchemaPlanner.computed(types, GesComputedEmitter.helper(options.namespace, GesSchemaPlanner.suffix(header, options)), swift: false)
        let sets = GesSchemaPlanner.tagSets(types, global)
        // Packed records are the model types over one word, the shared ones of the model namespace or declared here.
        var models = global
        if let shared = options.models { models = "global::" + (try GesSchemaPlanner.namespace(shared, keywords)).map { $0 + "." }.joined() }
        for type in types {
            for field in type.fields { GesSchemaPlanner.qualifyPacked(field.value, models) }
        }
        // Handle types are the models' where they share a namespace, else declared here.
        let handles = options.models != nil ? models : global
        for type in types {
            for field in type.fields { GesSchemaPlanner.qualifyHandles(field.value, handles) }
        }
        var topLevel = GesSchemaPlanner.topLevel(types, sets, layout.networks)
        if options.models == nil {
            for handle in layout.handles { topLevel.insert(GesModelPlanner.handleName(handle.name)) }
        }
        if options.models != nil { try GesSchemaPlanner.reserveSharedRoots(layout, types, &topLevel) }
        try schemaSet?.reserve(&topLevel)
        let context = Context(
            layout: layout,
            elements: GesSchemaPlanner.keyedElements(types),
            global: global,
            values: options.values,
            holder: holder,
            computed: computed,
            topLevel: topLevel,
            models: modelLayout.map { ModelConversion($0, models) }
        )
        var document = GesCodeDocument(header: header, prologue: prologue.text, space: space, parameters: !layout.parameters.isEmpty)
        if options.models == nil {
            let declared = layout.handles.map(\.model)
            for handle in layout.handles {
                document.units.append(GesCodeUnit(owner: GesModelPlanner.handleName(handle.name), write: { code in GesCSharpModelEmitter.handle(&code, declared, handle.model, access) }, handle: handle.name))
            }
        }
        for type in types {
            if type.layout.packedWidth > 0 {
                let root = layout.roots.contains(type.name)
                if let packed {
                    document.units.append(
                        GesCodeUnit(owner: type.name) { code in
                            try GesCSharpModelEmitter.packedType(&code, packed, type.name, access, context.global, root ? { members in packedRoot(&members, type, context) } : nil)
                        }
                    )
                } else if root {
                    document.units.append(
                        GesCodeUnit(owner: type.name) { code in
                            // A root shared with models gets its wire members as extension methods of the model's struct.
                            code.line("/// <summary>The wire members of the packed record <c>:\(type.layout.declared)</c>, a wire root whose type the models declare: its bytes are its word, little-endian.</summary>")
                            code.line("\(access) static class \(type.name)Wire")
                            code.open("{")
                            packedRoot(&code, type, context, models + type.name)
                            code.close()
                        }
                    )
                }
                continue
            }
            document.units.append(
                GesCodeUnit(owner: type.name) { code in
                    if type.kind == .messages { try messageSet(&code, type, access, context) } else { try record(&code, type, access, context) }
                }
            )
        }
        for set in sets {
            document.units.append(GesCodeUnit(owner: set.name) { code in tagSetDeclaration(&code, set.name, set.tags, access) })
        }
        for network in layout.networks {
            document.units.append(
                GesCodeUnit(owner: network.name) { code in
                    try self.network(&code, network, access, schemaSet?.qualified) { root in
                        // A packed root that the models declare is their struct; its wire members extend it.
                        let type = types.first { $0.name == root }!
                        let shared = type.layout.packedWidth > 0 && packed == nil
                        return NetworkRoot(type: (shared ? models : context.global) + root, owner: context.global + root + (shared ? "Wire" : ""), plan: type)
                    }
                }
            )
        }
        try GesSchemaPlanner.checkTopLevel(context.topLevel, swift: false)
        for network in layout.networks { try GesNetworkEndPlan.checkNested(network, context.topLevel, swift: false) }
        let shared = options.split != .none
        if let setName = schemaSet?.declared {
            document.units.append(GesCodeUnit(owner: "") { code in GesSchemaSetReference.writeCSharp(&code, setName, access, layout.sources, layout.parameters, networks: !layout.networks.isEmpty) })
        } else if options.values && schemaSet == nil {
            document.units.append(GesCodeUnit(owner: "") { code in valuesHolder(&code, layout, holder, shared: shared) })
        }
        if layout.types.contains(where: { !$0.computed.isEmpty }) {
            let computed = context.computed
            let values = computed.needsValues(types.map { ($0.name, $0.layout.computed) })
            let math = GesComputedEmitter.usesMath(types.map(\.layout.computed))
            let spatial = GesComputedEmitter.usesSpatial(types.map(\.layout.computed))
            document.units.append(GesCodeUnit(owner: "") { code in GesComputedLibrary.write(&code, computed.helper, swift: false, values: values, math: math, spatial: spatial) })
        }
        return document
    }

    // The keyed-list elements and the qualifier of the top-level types, which a member of the same name cannot hide.
    struct Context {
        let layout: GesSchemaLayout
        let elements: Set<String>
        let global: String
        // Whether records convert to and from GES values through the helper type that holds the embedded sources.
        let values: Bool
        let holder: String
        let computed: GesComputedEmitter
        // The records and tag sets declared beside each other, which a record's nested type must not hide.
        let topLevel: Set<String>
        // The models that records convert to and from directly, nil without `--models`.
        let models: ModelConversion?
    }

    static func kind(_ type: GesPlanType) -> String { type.kind == .message ? "message" : "schema" }

    // A message name stands without the colon that marks schemas.
    static func written(_ type: GesPlanType) -> String { type.kind == .message ? type.layout.declared : ":" + type.layout.declared }

    static func record(_ code: inout Code, _ type: GesPlanType, _ access: String, _ context: Context) throws(GesGeneratorError) {
        var names: Names = [(type.name, "the type itself")]
        if !type.isItem {
            for name in reserved { names.append((name, "the generated member " + name)) }
        }
        if !type.isItem && context.values {
            for name in valueReserved { names.append((name, "the generated member " + name)) }
        }
        fieldNames(type, &names)
        for tag in type.tags { names.append((tag.base, "the tag #" + tag.tag)) }
        if !type.isItem {
            for constant in type.layout.constants { names.append((GesSchemaPlanner.pascal(constant.name), "the let " + constant.name)) }
        }
        if !type.isItem { GesComputedEmitter.names(type.layout.computed, type.name, swift: false, &names) }
        let model = type.isItem ? nil : context.models?.type(type)
        if model != nil { names.append(("ToModel", "the generated member ToModel")) }
        try GesSchemaPlanner.unique(type.name, names, context.topLevel)

        let visibility = type.isItem ? "public" : access
        code.line(
            type.isItem
                ? "/// <summary>An element of the list.</summary>"
                : "/// <summary>The \(kind(type)) <c>\(written(type))</c>, read and written without allocation\(type.isClass ? " once its storage has grown" : "").</summary>"
        )
        code.line(type.isClass ? "\(visibility) sealed partial class \(type.name)" : "\(visibility) partial struct \(type.name)")
        code.open("{")
        if !type.isItem {
            constants(&code, type.layout)
            paths(&code, type)
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
        argumentInitializer(&code, type)
        for field in type.fields { listAccess(&code, field) }
        if !type.isItem { context.computed.members(&code, type.name, type.layout.computed, "public") }
        methods(&code, type)
        if type.isClass && (type.kind == .schema || type.isItem) { sharing(&code, type) }
        let deltas = type.kind == .schema && !type.isItem
        if deltas { sameHelpers(&code, type, context.global) }
        extras(&code, type, deltas, context.elements.contains(type.name))
        if context.values && !type.isItem { valueMembers(&code, type, context.holder, lenient: lenient(type.layout, context.layout)) }
        if let model { try modelMembers(&code, type, model, context.models!) }
        for field in type.fields { try declarations(&code, field.value, access, context) }
        code.close()
    }

    // The paths of a type's nodes in node order, which errors name.
    private static func paths(_ code: inout Code, _ type: GesPlanType) {
        code.blank()
        code.line("internal static readonly string[] __paths =")
        code.open("{")
        for path in type.paths { code.line(GesSchemaPlanner.quote(path) + ",") }
        code.close("};")
    }

    // The members that a type's fields declare, for the collision check.
    private static func fieldNames(_ type: GesPlanType, _ names: inout Names) {
        for field in type.fields {
            names.append((field.base, "the field " + field.layout.name))
            if field.optional { names.append(("Has" + field.base, "the presence of " + field.layout.name)) }
            if field.optional && plain(field.value) { names.append((field.base + "Optional", "the optional value of " + field.layout.name)) }
            nested(field.value, &names, field.layout.name)
            if isPooledList(field) { names.append(("Set" + field.base + "Count", "the element count of " + field.layout.name)) }
            if isTextList(field) { names.append((field.base + "Values", "the texts of " + field.layout.name)) }
        }
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
        lets(&code, layout.constants)
    }

    /// The lets of a schema, in base units; a Percentage holds its ratio.
    static func lets(_ code: inout Code, _ constants: [GesLayoutConstant]) {
        for constant in constants {
            code.blank()
            code.line("/// <summary><c>\(GesSchemaPlanner.constantSource(constant))</c></summary>")
            code.line("public const \(constant.integer ? "long" : "double") \(GesSchemaPlanner.pascal(constant.name)) = \(GesSchemaPlanner.constantValue(constant));")
        }
    }

    // Whether an optional value also has a nullable view: a value type without storage.
    static func plain(_ value: GesPlanValue) -> Bool { !value.isClass && value.encoding.kind != .union && value.encoding.kind != .text && value.encoding.kind != .bytes && value.encoding.kind != .list }

    // The nested types and members a value declares, for the collision check.
    static func nested(_ value: GesPlanValue, _ names: inout Names, _ field: String) {
        let name = nestedName(value)
        if !name.isEmpty && value.tagType.isEmpty { names.append((name, GesSchemaPlanner.nestedTypeOrigin + "of " + field)) }
        switch value.encoding.kind {
        case .text:
            names.append((value.base + "Utf8", "the UTF-8 bytes of " + field))
            names.append(("Set" + value.base + "Utf8", "the UTF-8 setter of " + field))
        case .bytes:
            names.append(("Set" + value.base, "the setter of " + field))
        case .list:
            if value.encoding.poly { names.append((value.base + "Poly", "the polygon view of " + field)) }
            if let item = value.item { names.append((item.name, GesSchemaPlanner.nestedTypeOrigin + "of the elements of " + field)) } else { nested(value.element!, &names, field) }
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
        case .tag: value.tagType.isEmpty ? value.base + "Tag" : value.tagType
        case .flags: value.base + "Flags"
        case .union: value.base + "Alternative"
        default: ""
        }
    }

    static func typeName(_ value: GesPlanValue) -> String {
        switch value.encoding.kind {
        case .boolean: "bool"
        case .integer: "long"
        case .quantized, .float: "double"
        case .schema: value.packed?.type ?? value.encoding.schema
        case .text: "string"
        case .bytes: "byte[]"
        case .point: "GesPoint"
        case .vector: "GesVector"
        case .box: "GesBox"
        case .disc: "GesDisc"
        case .handle: value.handleType
        case .list: "List<\(elementName(value))>"
        default: nestedName(value)
        }
    }

    static func elementName(_ list: GesPlanValue) -> String { list.item?.name ?? typeName(list.element!) }

    // A required list whose elements are classes the record reuses.
    static func isPooledList(_ field: GesPlanField) -> Bool { !field.optional && field.value.encoding.kind == .list && field.value.element!.isClass }

    // A pooled list of Text or open Tag elements, whose element type holds one text.
    static func isTextList(_ field: GesPlanField) -> Bool { isPooledList(field) && field.value.item != nil && field.value.element!.encoding.kind == .text }

    // Sets a pooled list to a count of reused elements, as a read does, and for texts offers them as strings.
    static func listAccess(_ code: inout Code, _ field: GesPlanField) {
        if !isPooledList(field) { return }
        let value = field.value
        let base = value.base
        code.blank()
        code.line("/// <summary>Sets <c>\(field.layout.name)</c> to <paramref name=\"count\"/> elements that the record reuses, as a read does; their fields keep earlier values until they are set.</summary>")
        code.line("/// <param name=\"count\">The element count.</param>")
        code.line("public void Set\(base)Count(int count)")
        code.open("{")
        code.line("if (count < 0) throw new global::System.ArgumentOutOfRangeException(nameof(count));")
        code.line("\(base).Clear();")
        code.line("for (var position = 0; position < count; position++)")
        code.open("{")
        code.line("if (position == __\(base)Pool.Count) __\(base)Pool.Add(new \(elementName(value))());")
        code.line("\(base).Add(__\(base)Pool[position]);")
        code.close()
        code.close()
        if !isTextList(field) { return }
        let text = value.item!.fields[0].base
        code.blank()
        code.line("/// <summary>The elements of <c>\(field.layout.name)</c> as strings; reading and setting allocate, and setting reuses the elements.</summary>")
        code.line("public string[] \(base)Values")
        code.open("{")
        code.line("get")
        code.open("{")
        code.line("var values = new string[\(base).Count];")
        code.line("for (var index = 0; index < values.Length; index++) values[index] = \(base)[index].\(text);")
        code.line("return values;")
        code.close()
        code.blank()
        code.line("set")
        code.open("{")
        code.line("if (value is null) throw new global::System.ArgumentNullException(nameof(value));")
        code.line("Set\(base)Count(value.Length);")
        code.line("for (var index = 0; index < value.Length; index++) \(base)[index].\(text) = value[index];")
        code.close()
        code.close()
    }

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

    // A Bytes value grows its storage as a Text does, without UTF-8 conveniences.
    static func bytesMember(_ code: inout Code, _ value: GesPlanValue, _ summary: String) {
        let storage = "__" + value.base
        let max = String(value.encoding.maxCount)
        code.line("private byte[] \(storage) = global::System.Array.Empty<byte>();")
        code.line("private int \(storage)Length;")
        code.blank()
        code.line("/// <summary>\(summary), at most \(max) bytes.</summary>")
        code.line("public ReadOnlySpan<byte> \(value.base) => new ReadOnlySpan<byte>(\(storage), 0, \(storage)Length);")
        code.blank()
        code.line("/// <summary>Sets \(summary.prefix(1).lowercased() + summary.dropFirst()); its length is checked when the record is written.</summary>")
        code.line("/// <param name=\"bytes\">The bytes.</param>")
        code.line("public void Set\(value.base)(ReadOnlySpan<byte> bytes)")
        code.open("{")
        code.line("if (\(storage).Length < bytes.Length) global::System.Array.Resize(ref \(storage), global::System.Math.Max(bytes.Length, global::System.Math.Min(\(max), \(storage).Length * 2)));")
        code.line("bytes.CopyTo(\(storage));")
        code.line("\(storage)Length = bytes.Length;")
        code.close()
    }

    // Declares the member that stores a value, with its storage and conveniences.
    static func member(_ code: inout Code, _ value: GesPlanValue, _ summary: String) {
        if value.encoding.kind == .bytes { return bytesMember(&code, value, summary) }
        if value.encoding.kind != .text {
            code.line("/// <summary>\(summary)\(description(value)).</summary>")
            code.line("public \(typeName(value)) \(value.base)\(initializer(value))")
            if !value.encoding.poly { return }
            code.blank()
            code.line("/// <summary>\(summary) as a polygon; reading it allocates.</summary>")
            code.line("public GesPoly \(value.base)Poly => new GesPoly(\(value.base));")
            return
        }
        let storage = "__" + value.base
        let max = String(value.encoding.maxCount)
        code.line("private byte[] \(storage) = global::System.Array.Empty<byte>();")
        code.line("private int \(storage)Length;")
        code.blank()
        code.line("/// <summary>\(summary) as UTF-8 bytes\(value.encoding.tagName ? ", a tag name without #" : "").</summary>")
        code.line("public ReadOnlySpan<byte> \(value.base)Utf8 => new ReadOnlySpan<byte>(\(storage), 0, \(storage)Length);")
        code.blank()
        code.line("/// <summary>Sets \(summary.prefix(1).lowercased() + summary.dropFirst()) from UTF-8 bytes, which are checked when the record is written.</summary>")
        code.line("/// <param name=\"utf8\">The bytes.</param>")
        code.line("public void Set\(value.base)Utf8(ReadOnlySpan<byte> utf8)")
        code.open("{")
        code.line("if (\(storage).Length < utf8.Length) global::System.Array.Resize(ref \(storage), global::System.Math.Max(utf8.Length, global::System.Math.Min(\(max), \(storage).Length * 2)));")
        code.line("utf8.CopyTo(\(storage));")
        code.line("\(storage)Length = utf8.Length;")
        code.close()
        code.blank()
        code.line("/// <summary>\(summary) as a string; reading and setting it allocate.</summary>")
        code.line("public string \(value.base)")
        code.open("{")
        code.line("get => global::System.Text.Encoding.UTF8.GetString(\(storage), 0, \(storage)Length);")
        code.line("set => Set\(value.base)Utf8(global::System.Text.Encoding.UTF8.GetBytes(value));")
        code.close()
    }

    static func description(_ value: GesPlanValue) -> String {
        let encoding = value.encoding
        switch encoding.kind {
        case .quantized, .float, .integer, .point, .vector, .box, .disc: return encoding.unit != "none" ? ", in \(encoding.unit)" : ""
        case .union: return ", the selected alternative"
        case .list where encoding.poly: return ", the points of a polygon\(encoding.unit != "none" ? " in " + encoding.unit : ""), at most \(encoding.maxCount)"
        case .list: return ", \(encoding.fixed ? "exactly" : "at most") \(encoding.maxCount) elements"
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
        fieldMethods(&code, type)
    }

    // Writes and reads a type's fields in wire order, at the paths that start at `at`.
    private static func fieldMethods(_ code: inout Code, _ type: GesPlanType) {
        let selfModifier = type.isClass || type.kind == .messages && type.membersAreClasses ? "" : "readonly "
        code.blank()
        code.line("internal \(selfModifier)void __WriteFields(ref GesBitWriter writer, string[] paths, int at\(levelParameter(type)))")
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
        code.line("internal void __ReadFields(ref GesBitReader reader, string[] paths, int at\(levelParameter(type)))")
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
        code.line("/// <summary>Counts the bytes that <see cref=\"Write\"/> writes for this \(what), without writing them.</summary>")
        code.line("/// <returns>The exact byte count.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>")
        code.line("public \(selfModifier)int ByteCount()")
        code.open("{")
        code.line("var writer = GesBitWriter.Counting();")
        for line in write { code.line(line) }
        code.line("return writer.Finish();")
        code.close()
        code.blank()
        code.line("/// <summary>Writes this \(what) into a new array of its exact size, which <see cref=\"ByteCount\"/> counts first.</summary>")
        code.line("/// <returns>The bytes.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>")
        code.line("public \(selfModifier)byte[] ToBytes()")
        code.open("{")
        code.line("var buffer = new byte[ByteCount()];")
        code.line("Write(buffer);")
        code.line("return buffer;")
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
        var names: Names = [
            (type.name, "the type itself"), ("Selected", "the selected member"), ("Member", GesSchemaPlanner.nestedTypeOrigin + "of the members"),
            ("Case", GesSchemaPlanner.nestedTypeOrigin + "of the cases"), ("ToCase", "the generated member ToCase"), ("Assign", "the generated member Assign"),
            ("Value", "the value of a case"),
        ]
        for name in reserved { names.append((name, "the generated member " + name)) }
        if context.values {
            for name in valueReserved { names.append((name, "the generated member " + name)) }
        }
        for member in type.memberNames { names.append((member, "the member " + member)) }
        fieldNames(type, &names)
        try GesSchemaPlanner.unique(type.name, names, context.topLevel)
        let width = String(type.layout.memberWidth)
        let last = unsigned(UInt64(type.members.count) - 1)
        let parameters = !type.fields.isEmpty
        code.line(
            "/// <summary>The message set <c>\(type.layout.declared)</c>: \(parameters ? "its parameters and " : "")one of its messages, read and written without allocation\(type.membersAreClasses ? " once its storage has grown" : "").</summary>"
        )
        code.line(type.membersAreClasses ? "\(access) sealed partial class \(type.name)" : "\(access) partial struct \(type.name)")
        code.open("{")
        constants(&code, type.layout)
        if parameters {
            paths(&code, type)
            for field in type.fields { members(&code, field, false) }
            for field in type.fields { listAccess(&code, field) }
        }
        code.blank()
        code.line("/// <summary>The message this record holds.</summary>")
        code.line("public Member Selected;")
        for (member, record) in type.memberPairs {
            code.blank()
            code.line("/// <summary>The message when <see cref=\"Selected\"/> is <c>\(member)</c>.</summary>")
            code.line("public \(record) \(member)\(type.membersAreClasses ? " = new \(record)();" : ";")")
        }
        var write = ["if ((ulong)Selected > \(last)) throw new GesWireException(GesWireException.OutOfRange, \"\");"]
        var read: [String] = []
        if parameters {
            write.append("__WriteFields(ref writer, __paths, 0);")
            read.append("__ReadFields(ref reader, __paths, 0);")
        }
        write += ["writer.Write((ulong)Selected, \(width));", "switch (Selected)", "{"]
        read.append("var index = reader.Read(\(width), \"\");")
        if UInt64(type.members.count) - 1 < full(type.layout.memberWidth) { read.append("if (index > \(last)) throw new GesWireException(GesWireException.InvalidIndex, \"\");") }
        read += ["Selected = (Member)index;", "switch (Selected)", "{"]
        // The paths are static members of the message type, whose name differs from the member's with a message suffix.
        for (member, record) in type.memberPairs {
            write += ["    case Member.\(member):", "        \(member).__WriteFields(ref writer, \(record).__paths, 0);", "        break;"]
            read += ["    case Member.\(member):", "        \(member).__ReadFields(ref reader, \(record).__paths, 0);", "        break;"]
        }
        write.append("}")
        read.append("}")
        code.blank()
        publicMethods(&code, type.membersAreClasses ? "" : "readonly ", write, read, "message")
        if parameters { fieldMethods(&code, type) }
        if context.values { valueMembers(&code, type, context.holder, lenient: lenient(type.layout, context.layout)) }
        if let model = context.models?.type(type) { try modelMembers(&code, type, model, context.models!) }
        code.blank()
        messageCases(&code, type, context)
        code.blank()
        code.line("/// <summary>The messages of the set, in declared order.</summary>")
        code.line("public enum Member : \(underlying(type.members.count))")
        code.open("{")
        for member in type.memberNames {
            code.line("/// <summary>The message <c>\(member)</c>.</summary>")
            code.line(member + ",")
        }
        code.close()
        for field in type.fields { try declarations(&code, field.value, access, context) }
        code.close()
    }

    // The cases of a message set, an abstract class with one sealed subclass per message for pattern matching, and the
    // conversions between them and the record. A typed end hands its cached cases, so receiving allocates nothing.
    static func messageCases(_ code: inout Code, _ type: GesPlanType, _ context: Context) {
        let selfModifier = type.membersAreClasses ? "" : "readonly "
        code.blank()
        code.line("/// <summary>Returns the selected message as a new case that holds the member record.</summary>")
        code.line("/// <returns>The case.</returns>")
        code.line("/// <exception cref=\"GesWireException\"><see cref=\"Selected\"/> names no message of the set.</exception>")
        code.line("public \(selfModifier)Case ToCase() => Selected switch")
        code.open("{")
        for member in type.memberNames { code.line("Member.\(member) => new Case.\(member)(\(member)),") }
        code.line("_ => throw new GesWireException(GesWireException.OutOfRange, \"\"),")
        code.close("};")
        code.blank()
        code.line("/// <summary>Selects the message of a case and takes its member record.</summary>")
        code.line("/// <param name=\"value\">The case.</param>")
        code.line("/// <exception cref=\"global::System.ArgumentNullException\"><paramref name=\"value\"/> is null.</exception>")
        code.line("public void Assign(Case value)")
        code.open("{")
        code.line("switch (value)")
        code.open("{")
        for member in type.memberNames {
            let local = GesSchemaPlanner.camel(member)
            code.line("case Case.\(member) @\(local):")
            code.line("    Selected = Member.\(member);")
            code.line("    \(member) = @\(local).Value;")
            code.line("    break;")
        }
        code.line("default:")
        code.line("    throw new global::System.ArgumentNullException(nameof(value));")
        code.close()
        code.close()
        code.blank()
        code.line("/// <summary>One message of the set as a case: match <c>case \(type.name).Case.\(type.memberNames[0]) value</c>.</summary>")
        code.line("public abstract class Case")
        code.open("{")
        code.line("private protected Case()")
        code.open("{")
        code.close()
        code.blank()
        code.line("/// <summary>Gets the message of the case.</summary>")
        code.line("public abstract Member Selected { get; }")
        for (member, generated) in type.memberPairs {
            let record = context.global + generated
            code.blank()
            code.line("/// <summary>The message <c>\(member)</c>.</summary>")
            code.line("public sealed class \(member) : Case")
            code.open("{")
            code.line("/// <summary>Creates the case with an empty record.</summary>")
            code.line("public \(member)()")
            code.open("{")
            code.close()
            code.blank()
            code.line("/// <summary>Creates the case with a record.</summary>")
            code.line("/// <param name=\"value\">The record.</param>")
            code.line("public \(member)(\(record) value) => Value = value;")
            code.blank()
            code.line("/// <summary>The record of the message.</summary>")
            code.line("public \(record) Value\(type.membersAreClasses ? " = new \(record)();" : ";")")
            code.blank()
            code.line("/// <inheritdoc/>")
            code.line("public override Member Selected => Member.\(member);")
            code.close()
        }
        code.close()
    }

    static func path(_ value: GesPlanValue) -> String { "paths[at + \(value.node)]" }

    // A recursive schema's methods take the level of the record, 1 where a caller enters the recursion.
    static func levelParameter(_ type: GesPlanType) -> String { type.depth > 0 ? ", int level = 1" : "" }

    // The level argument of a call into a record: one deeper for the element of a recursive List.
    static func nextLevel(_ value: GesPlanValue) -> String { value.recursive ? ", level + 1" : "" }

    // The level that the item of a recursive union takes: that of the record holding the List.
    static func itemLevel(_ element: GesPlanValue) -> String { element.passesLevel ? ", level" : "" }

    // Opens the levels of a recursive List that hold elements; at its depth the List holds nothing, so data there is deeper.
    static func openLevels(_ code: inout Code, _ value: GesPlanValue, _ count: String) {
        code.line("if (level >= \(value.depth))")
        code.open("{")
        code.line("if (\(count) > 0) throw new GesWireException(GesWireException.TooDeep, \(path(value)), \"deeper than depth \(value.depth)\");")
        code.close()
        code.line("else")
        code.open("{")
    }

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
            code.line("writer.Write(unchecked((ulong)global::System.BitConverter.DoubleToInt64Bits(\(target) == 0 ? 0d : \(target))), 64);")
        case .tag:
            code.line("if ((ulong)\(target) > \(unsigned(UInt64(encoding.tags.count) - 1))) \(fail("UnknownTag", value))")
            code.line("writer.Write((ulong)\(target), \(width));")
        case .flags:
            if encoding.width < 64 { code.line("if ((ulong)\(target) > \(unsigned(full(encoding.width)))) \(fail("UnknownTag", value))") }
            code.line("writer.Write((ulong)\(target), \(width));")
        case .text, .bytes:
            code.open("{")
            code.line("var text = new ReadOnlySpan<byte>(__\(value.base), 0, __\(value.base)Length);")
            code.line("if (text.Length > \(max)) \(fail("TooLong", value))")
            if encoding.kind == .text {
                code.line(encoding.tagName ? "if (!GesWireRules.IsTagName(text)) \(fail("InvalidTag", value))" : "if (!GesWireRules.IsValidUtf8(text)) \(fail("InvalidText", value))")
            }
            code.line("writer.Write((ulong)text.Length, \(width));")
            code.line("writer.WriteBytes(text);")
            code.close()
        case .list:
            if value.depth > 0 { openLevels(&code, value, target + ".Count") }
            code.line("if (\(target).Count > \(max)) \(fail("TooLong", value))")
            if value.encoding.fixed {
                code.line("if (\(target).Count < \(max)) \(fail("TooShort", value))")
            } else {
                code.line("writer.Write((ulong)\(target).Count, \(width));")
            }
            if value.handleMap {
                code.line(
                    "for (var position = 1; position < \(target).Count; position++) if (\(target)[position].Key.CompareTo(\(target)[position - 1].Key) <= 0) throw new GesWireException(GesWireException.InvalidKeyOrder, \(handleKeyPath(value)));"
                )
            }
            code.line("foreach (var item in \(target))")
            code.open("{")
            if value.item != nil {
                code.line("item.__WriteFields(ref writer, paths, at + \(value.element!.node)\(itemLevel(value.element!)));")
            } else {
                write(&code, value.element!, "item")
            }
            code.close()
            if value.depth > 0 { code.close() }
        case .handle:
            writeHandle(&code, value, target)
        case .point, .vector:
            for axis in value.axes { write(&code, axis, target + "." + axis.base) }
            for axis in GesSchemaPlanner.omitted(value) { code.line("if (\(target).\(axis) != 0) \(fail("TypeMismatch", value))") }
        case .box:
            for corner in value.corners { write(&code, corner, target + "." + corner.base) }
        case .disc:
            for part in value.corners { write(&code, part, target + "." + part.base) }
            if value.corners.count == 1 { code.line("if (\(target).Radius != 0) \(fail("TypeMismatch", value))") }
        case .schema where value.packed != nil:
            packedWrite(&code, value, target)
        case .schema:
            code.line("\(target).__WriteFields(ref writer, paths, at + \(value.node + 1)\(nextLevel(value)));")
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
        case .nothing, .handleMap:
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
        case .flags:
            code.line("\(target) = (\(nestedName(value)))reader.Read(\(width), \(path(value)));")
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
            code.line("var value = global::System.BitConverter.Int64BitsToDouble(unchecked((long)bits));")
            code.line("if (double.IsNaN(value) || bits == 0x8000000000000000UL\(encoding.range ? " || double.IsInfinity(value)" : "")) \(fail("InvalidNumber", value))")
            if encoding.range { code.line("if (value < \(bits(encoding.lowerValue)) || value > \(bits(encoding.upperValue))) \(fail("OutOfRange", value))") }
            code.line("\(target) = value;")
            code.close()
        case .text, .bytes:
            let storage = "__" + value.base
            code.open("{")
            code.line("var count = (int)reader.Read(\(width), \(path(value)));")
            code.line("if (count > \(max)) \(fail("InvalidCount", value))")
            code.line("reader.Require(count * 8L, \(path(value)));")
            code.line("if (\(storage).Length < count) global::System.Array.Resize(ref \(storage), global::System.Math.Max(count, global::System.Math.Min(\(max), \(storage).Length * 2)));")
            code.line("var text = new Span<byte>(\(storage), 0, count);")
            code.line("reader.ReadBytes(text, \(path(value)));")
            if encoding.kind == .text {
                code.line(encoding.tagName ? "if (!GesWireRules.IsTagName(text)) \(fail("InvalidTag", value))" : "if (!GesWireRules.IsValidUtf8(text)) \(fail("InvalidText", value))")
            }
            code.line("\(storage)Length = count;")
            code.close()
        case .list:
            let element = value.element!
            let name = elementName(value)
            if value.depth > 0 {
                // The deepest level reads no count, since its List takes no bits.
                code.line("if (level >= \(value.depth)) \(target).Clear();")
                code.line("else")
            }
            code.open("{")
            if value.encoding.fixed {
                code.line("const int count = \(max);")
            } else {
                code.line("var count = (int)reader.Read(\(width), \(path(value)));")
                code.line("if (count > \(max)) \(fail("InvalidCount", value))")
            }
            code.line("\(target).Clear();")
            if element.packed != nil {
                packedList(&code, value, target, "count")
                code.close()
                break
            }
            code.line("for (var position = 0; position < count; position++)")
            code.open("{")
            if element.isClass {
                code.line("if (position == __\(value.base)Pool.Count) __\(value.base)Pool.Add(new \(name)());")
                code.line("var item = __\(value.base)Pool[position];")
            } else if value.item != nil || element.encoding.kind == .point || element.encoding.kind == .vector || element.encoding.kind == .schema || element.encoding.kind == .handle {
                // A handle element starts empty, because its read passes the current id along.
                code.line("var item = new \(name)();")
            } else {
                code.line("\(name) item;")
            }
            if value.item != nil {
                code.line("item.__ReadFields(ref reader, paths, at + \(element.node)\(itemLevel(element)));")
            } else {
                read(&code, element, "item")
            }
            if value.handleMap {
                code.line("if (position > 0 && item.Key.CompareTo(\(target)[position - 1].Key) <= 0) throw new GesWireException(GesWireException.InvalidKeyOrder, \(handleKeyPath(value)));")
            }
            code.line("\(target).Add(item);")
            code.close()
            code.close()
        case .handle:
            readHandle(&code, value, target)
        case .point, .vector:
            for axis in value.axes { read(&code, axis, target + "." + axis.base) }
            for axis in GesSchemaPlanner.omitted(value) { code.line("\(target).\(axis) = 0;") }
        case .box:
            // The corners arrive as two Points; the box orders them.
            code.open("{")
            for corner in value.corners {
                code.line("var \(corner.label)\(value.node) = new GesPoint();")
                read(&code, corner, corner.label + String(value.node))
            }
            code.line("\(target) = new GesBox(lower\(value.node), upper\(value.node));")
            code.close()
        case .disc:
            // The centre and the radius, which a block can leave out; the disc takes the radius's magnitude.
            code.open("{")
            code.line("var center\(value.node) = new GesPoint();")
            code.line("var radius\(value.node) = 0d;")
            for part in value.corners { read(&code, part, part.label + String(value.node)) }
            code.line("\(target) = new GesDisc(center\(value.node), radius\(value.node));")
            code.close()
        case .schema where value.packed != nil:
            packedRead(&code, value, target)
        case .schema:
            code.line("\(target).__ReadFields(ref reader, paths, at + \(value.node + 1)\(nextLevel(value)));")
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
        case .nothing, .handleMap:
            break
        }
    }

    // The path of a handle map's key: the node after its entry's group.
    private static func handleKeyPath(_ map: GesPlanValue) -> String { "paths[at + \(map.element!.node + 1)]" }

    // The plan values of a handle's id, its version where the field has one, and a composite's part, in node order.
    static func handleParts(_ value: GesPlanValue) -> (id: GesPlanValue, version: GesPlanValue?, part: GesPlanValue?) {
        let handle = value.handle!
        let owner = value.handleRoot ?? handle
        var node = value.node + 1
        let id = GesPlanValue(base: "Id", node: node, encoding: owner.id!)
        node += 1
        var version: GesPlanValue?
        if value.encoding.versioned {
            var encoding = owner.revision
            if owner.version != .revision {
                let hash = GesLayoutEncoding()
                hash.kind = .bytes
                hash.maxCount = owner.version == .sha1 ? 20 : 32
                encoding = hash
            }
            version = GesPlanValue(base: "Version", node: node, encoding: encoding!)
            node += 1
        }
        let part = handle.isComposite ? GesPlanValue(base: "Part", node: node, encoding: handle.part!) : nil
        return (id, version, part)
    }

    // The type of a composite's root, beside the handle's own type.
    static func handleRootType(_ value: GesPlanValue) -> String {
        String(value.handleType.dropLast((value.handle!.name + "Handle").count)) + value.handleRoot!.name + "Handle"
    }

    // The version type of a hash: GesHash20 or GesHash32.
    static func hashType(_ version: GesPlanValue) -> String { version.encoding.maxCount == 20 ? "GesHash20" : "GesHash32" }

    // Writes a handle's id, version and part; a field without a version leaves the handle's out.
    private static func writeHandle(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let (id, version, part) = handleParts(value)
        code.open("{")
        writeHandleKey(&code, id, target + (value.handleRoot != nil ? ".Root.Id" : ".Id"))
        if let version {
            code.line("if (\(target).Version is not { } version) \(fail("MissingVersion", version))")
            if version.encoding.kind == .bytes {
                code.line("Span<byte> hash = stackalloc byte[\(hashType(version)).Length];")
                code.line("version.CopyTo(hash);")
                code.line("for (var index = 0; index < hash.Length; index++) writer.Write(hash[index], 8);")
            } else {
                write(&code, version, "version")
            }
        }
        if let part { writeHandleKey(&code, part, target + ".Part") }
        code.close()
    }

    private static func writeHandleKey(_ code: inout Code, _ key: GesPlanValue, _ target: String) {
        if key.encoding.kind != .text {
            write(&code, key, target)
            return
        }
        code.line("if (\(target) is null) \(fail("TypeMismatch", key))")
        code.line("GesWireRules.WriteText(ref writer, \(target), \(key.encoding.width), \(key.encoding.maxCount), \(path(key)));")
    }

    // Reads a handle into locals and builds it; a text id that equals the held one keeps its string.
    private static func readHandle(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let (id, version, part) = handleParts(value)
        code.open("{")
        readHandleKey(&code, id, "id", target + (value.handleRoot != nil ? ".Root.Id" : ".Id"))
        if let version {
            if version.encoding.kind == .bytes {
                code.line("Span<byte> hash = stackalloc byte[\(hashType(version)).Length];")
                code.line("reader.Require(hash.Length * 8L, \(path(version)));")
                code.line("for (var index = 0; index < hash.Length; index++) hash[index] = (byte)reader.Read(8, \(path(version)));")
                code.line("var version = \(hashType(version)).FromBytes(hash);")
            } else {
                code.line("long version;")
                read(&code, version, "version")
            }
        }
        if let part { readHandleKey(&code, part, "part", target + ".Part") }
        let arguments = "id" + (version != nil ? ", version" : "")
        code.line(value.handleRoot != nil ? "\(target) = new \(value.handleType)(new \(handleRootType(value))(\(arguments)), part);" : "\(target) = new \(value.handleType)(\(arguments));")
        code.close()
    }

    private static func readHandleKey(_ code: inout Code, _ key: GesPlanValue, _ local: String, _ current: String) {
        if key.encoding.kind == .text {
            code.line("var \(local) = GesWireRules.ReadText(ref reader, \(key.encoding.width), \(key.encoding.maxCount), \(current), \(path(key)));")
            return
        }
        code.line("long \(local);")
        read(&code, key, local)
    }

    static func declarations(_ code: inout Code, _ value: GesPlanValue, _ access: String, _ context: Context) throws(GesGeneratorError) {
        let name = nestedName(value)
        switch value.encoding.kind {
        case .tag:
            if !value.tagType.isEmpty { break }
            code.blank()
            code.line("/// <summary>The tags of the field, in declared order.</summary>")
            code.line("public enum \(name) : \(underlying(value.encoding.tags.count))")
            code.open("{")
            for tag in value.encoding.tags {
                code.line("/// <summary>The tag <c>#\(tag)</c>.</summary>")
                code.line(GesSchemaPlanner.pascal(tag) + ",")
            }
            code.close()
        case .flags:
            flagsDeclaration(&code, name, value.encoding.tags)
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

    // A route root as a network sees it: its type, the type that holds its MaxByteCount and its plan.
    struct NetworkRoot {
        let type: String
        let owner: String
        let plan: GesPlanType

        // A message set without parameters, whose routes take and hand its cases; one with parameters is sent as its record,
        // which holds them.
        var isSet: Bool { plan.kind == .messages && plan.fields.isEmpty }
    }

    // The constants of a network, its hash, the index of each route and the rate of each topic that has one, a function
    // per route that encodes its root and sends or publishes it, and the typed ends with their handler interfaces.
    static func network(_ code: inout Code, _ network: GesLayoutNetwork, _ access: String, _ schemaSet: String?, _ roots: (String) -> NetworkRoot) throws(GesGeneratorError) {
        let client = try GesNetworkEndPlan.plan(network, client: true)
        let server = try GesNetworkEndPlan.plan(network, client: false)
        code.line("/// <summary>The network <c>:\(network.name)</c>: its hash, the index of each route, the rate of each topic that has one, a typed function per route and the typed ends.</summary>")
        code.line("\(access) static class \(network.name)")
        code.open("{")
        code.line("/// <summary>The network hash over the routes and their bound roots, which the handshake compares.</summary>")
        code.line("public const ulong Hash = 0x\(GesSchemaLayoutJson.hex(network.hash))UL;")
        if let schemaSet {
            code.blank()
            code.line("private static GesBoundNetwork? __bound;")
            code.blank()
            code.line("/// <summary>Gets the network bound with the parameters of the schema set, checked against <see cref=\"Hash\"/> on first access.</summary>")
            code.line("/// <exception cref=\"GesSchemaException\">The sources no longer parse or the network no longer binds.</exception>")
            code.line("/// <exception cref=\"global::System.InvalidOperationException\">The bound network hash differs from <see cref=\"Hash\"/>.</exception>")
            code.line("public static GesBoundNetwork Bound => __bound ??= \(schemaSet).BindNetwork(\(GesSchemaPlanner.literal(network.name, swift: false)), Hash);")
        }
        for route in network.routes {
            code.blank()
            code.line("/// <summary>The index of the route <c>\(route.side) \(route.verb) \(route.written) \(route.deliveryClass)</c>.</summary>")
            code.line("public const int \(route.name) = \(route.index);")
            if route.rateMicroseconds == 0 { continue }
            code.blank()
            code.line("/// <summary>The shortest interval between two updates of the topic <c>\(route.written)</c> in microseconds.</summary>")
            code.line("public const long \(route.name)EveryMicroseconds = \(route.rateMicroseconds);")
        }
        for route in network.routes {
            let publishes = route.verb == "publishes"
            let verb = publishes ? "Publish" : "Send"
            code.blank()
            code.line("/// <summary>Encodes <paramref name=\"value\"/> into <paramref name=\"buffer\"/> and \(publishes ? "publishes" : "sends") it on the route <c>\(route.side) \(route.verb) \(route.written) \(route.deliveryClass)</c>.</summary>")
            code.line("/// <param name=\"connection\">The \(route.side)'s connection.</param>")
            code.line("/// <param name=\"value\">The record.</param>")
            code.line("/// <param name=\"buffer\">The buffer the record is encoded into; its <c>MaxByteCount</c> bytes always suffice.</param>")
            code.line("/// <returns>The status of <see cref=\"GesNetworkConnection.\(verb)(int, ReadOnlySpan{byte})\"/>.</returns>")
            code.line("/// <exception cref=\"GesWireException\">A value does not fit its field, or the buffer is too small.</exception>")
            code.line("public static GesNetworkSendStatus \(GesSchemaPlanner.pascal(route.side))\(verb)\(route.root)(GesNetworkConnection connection, in \(roots(route.type).type) value, Span<byte> buffer)")
            code.line("    => connection.\(verb)(\(route.name), buffer.Slice(0, value.Write(buffer)));")
        }
        for end in [client, server] {
            typedEnd(&code, network, end, roots)
            endHandler(&code, network, end, roots)
        }
        code.close()
    }

    // The receiving of a route: the type its handler method takes.
    static func eventParameter(_ root: NetworkRoot) -> String { root.isSet ? root.type + ".Case" : "in " + root.type }

    // A typed end: it encodes the roots of its side into one buffer, reads the roots it receives into one record per route
    // and calls the handler's typed methods.
    static func typedEnd(_ code: inout Code, _ network: GesLayoutNetwork, _ end: GesNetworkEndPlan, _ roots: (String) -> NetworkRoot) {
        let isClient = end.client
        let endType = isClient ? "GesNetworkClientEnd" : "GesNetworkServerEnd"
        let handler = "I\(end.name)Handler"
        let side = isClient ? "client" : "server"

        // Distinct prefixes keep the scratch record of an outgoing message set, the cached cases and the internal fields
        // (__send_buffer, __tick_count, __tick_open: an underscore before a lowercase letter, which no root name has)
        // apart from the record of each received route, whatever its root is called.
        func field(_ name: String) -> String { "__" + GesSchemaPlanner.camel(name) }

        func outgoing(_ route: GesLayoutRoute) -> String { "__out_" + GesSchemaPlanner.camel((route.verb == "sends" ? "Send" : "Publish") + route.root) }

        func caseField(_ name: String, _ member: String) -> String { "__case_" + GesSchemaPlanner.camel(name) + "__" + member }

        code.blank()
        code.line("/// <summary>")
        code.line("/// The typed \(side) end of <c>:\(network.name)</c>: it encodes the records the \(side) sends and publishes and hands the records it")
        code.line("/// receives to an <see cref=\"\(handler)\"/>. Received records are borrowed: they stay valid until the handler method returns.")
        code.line("/// </summary>")
        code.line("public sealed class \(end.name)")
        code.open("{")
        code.line("private readonly byte[] __send_buffer;")
        for route in end.outgoing {
            let root = roots(route.type)
            if !root.isSet { continue }
            code.line("private \(root.type) \(outgoing(route))\(root.plan.membersAreClasses ? " = new();" : ";")")
        }
        for (route, name) in end.incoming {
            let root = roots(route.type)
            code.line("private \(root.type) \(field(name))\(root.plan.isClass || root.plan.membersAreClasses ? " = new();" : ";")")
            if !root.isSet { continue }
            for member in root.plan.memberNames { code.line("private readonly \(root.type).Case.\(member) \(caseField(name, member)) = new();") }
        }
        code.blank()
        code.line("/// <summary>Wraps an end of the network.</summary>")
        code.line("/// <param name=\"end\">The end; its bound network must have the hash <see cref=\"Hash\"/>.</param>")
        code.line("/// <exception cref=\"global::System.ArgumentNullException\"><paramref name=\"end\"/> is null.</exception>")
        code.line("/// <exception cref=\"global::System.ArgumentException\">The end's network has another hash.</exception>")
        code.line("public \(end.name)(\(endType) end)")
        code.open("{")
        code.line("End = end ?? throw new global::System.ArgumentNullException(nameof(end));")
        code.line("if (end.Network.Hash != Hash) throw new global::System.ArgumentException(\"The end's network is no :\(network.name).\", nameof(end));")
        var sizes: [String] = []
        for route in end.outgoing {
            let owner = roots(route.type).owner + ".MaxByteCount"
            if !sizes.contains(owner) { sizes.append(owner) }
        }
        var size = sizes.first ?? "0"
        for index in sizes.indices.dropFirst() { size = "global::System.Math.Max(\(size), \(sizes[index]))" }
        code.line("__send_buffer = new byte[\(size)];")
        code.close()
        code.blank()
        code.line("/// <summary>Gets the wrapped end.</summary>")
        code.line("public \(endType) End { get; }")
        if isClient {
            code.blank()
            code.line("/// <summary>Gets the end's connection.</summary>")
            code.line("public GesNetworkConnection Connection => End.Connection;")
        } else {
            code.blank()
            code.line("/// <summary>Gets the number of connecting and connected clients.</summary>")
            code.line("public int ClientCount => End.ClientCount;")
            code.blank()
            code.line("/// <summary>Gets the connection of a client.</summary>")
            code.line("/// <param name=\"client\">The client.</param>")
            code.line("/// <returns>The connection.</returns>")
            code.line("/// <exception cref=\"global::System.ArgumentException\">The client is not connected to the end.</exception>")
            code.line("public GesNetworkConnection Connection(int client) => End.Connection(client);")
        }
        for route in end.outgoing {
            let root = roots(route.type)
            let publishes = route.verb == "publishes"
            let verb = publishes ? "Publish" : "Send"
            let what = "<c>\(route.side) \(route.verb) \(route.written) \(route.deliveryClass)</c>"
            var targets: [(parameters: String, arguments: String, doc: String)] = [("", "", publishes ? "to the subscribed clients" : isClient ? "to the server" : "to every client")]
            if !isClient && !publishes { targets.append((", int client", ", client", "to one client")) }
            for (parameters, arguments, doc) in targets {
                code.blank()
                code.line("/// <summary>Encodes a record and \(publishes ? "publishes" : "sends") it on the route \(what)\(isClient ? "" : ", " + doc).</summary>")
                code.line("/// <param name=\"value\">The record.</param>")
                if !arguments.isEmpty { code.line("/// <param name=\"client\">The client.</param>") }
                code.line("/// <returns>The status of <see cref=\"\(endType).\(verb)(int, ReadOnlySpan{byte}\(arguments.isEmpty ? "" : ", int"))\"/>.</returns>")
                code.line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>")
                code.line("public GesNetworkSendStatus \(verb)(in \(root.type) value\(parameters)) => End.\(verb)(\(route.name), __send_buffer.AsSpan(0, value.Write(__send_buffer))\(arguments));")
                if !root.isSet { continue }
                let scratch = outgoing(route)
                code.blank()
                code.line("/// <summary>Encodes a message and \(publishes ? "publishes" : "sends") it on the route \(what)\(isClient ? "" : ", " + doc).</summary>")
                code.line("/// <param name=\"value\">The message.</param>")
                if !arguments.isEmpty { code.line("/// <param name=\"client\">The client.</param>") }
                code.line("/// <returns>The status of <see cref=\"\(endType).\(verb)(int, ReadOnlySpan{byte}\(arguments.isEmpty ? "" : ", int"))\"/>.</returns>")
                code.line("/// <exception cref=\"global::System.ArgumentNullException\"><paramref name=\"value\"/> is null.</exception>")
                code.line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>")
                code.line("public GesNetworkSendStatus \(verb)(\(root.type).Case value\(parameters))")
                code.open("{")
                code.line("\(scratch).Assign(value);")
                code.line("return \(verb)(in \(scratch)\(arguments));")
                code.close()
            }
        }
        if !isClient {
            code.blank()
            code.line("/// <summary>Refuses a connecting client; call it while the handler handles its <c>OnConnecting</c>.</summary>")
            code.line("/// <param name=\"client\">The client.</param>")
            code.line("/// <param name=\"detail\">The detail sent to the client.</param>")
            code.line("public void Refuse(int client, string detail) => End.Refuse(client, detail);")
            code.blank()
            code.line("/// <summary>Closes the connection of one client with a detail.</summary>")
            code.line("/// <param name=\"client\">The client.</param>")
            code.line("/// <param name=\"detail\">The detail sent to the client.</param>")
            code.line("public void Close(int client, string detail) => End.Close(client, detail);")
        }
        code.blank()
        code.line(isClient ? "/// <summary>Closes the connection with a detail.</summary>" : "/// <summary>Closes every current connection with a detail; the end still accepts new clients.</summary>")
        code.line("/// <param name=\"detail\">The detail sent to the \(isClient ? "server" : "clients").</param>")
        code.line("public void Close(string detail) => End.Close(detail);")
        if end.hasTopics { tickMembers(&code, end, roots) }
        code.blank()
        code.line("/// <summary>Polls the end and hands every event to the handler's typed methods. Never blocks.</summary>")
        code.line("/// <typeparam name=\"THandler\">The handler type; a struct that implements every method is called without allocating.</typeparam>")
        code.line("/// <param name=\"handler\">The handler.</param>")
        code.line("/// <exception cref=\"global::System.ObjectDisposedException\">The end is disposed.</exception>")
        code.line("public void Poll<THandler>(ref THandler handler) where THandler : \(handler)")
        code.open("{")
        code.line("var adapter = new __Adapter<THandler>(this, handler);")
        code.line("try")
        code.open("{")
        code.line("End.Poll(ref adapter);")
        code.close()
        code.line("finally")
        code.open("{")
        code.line("handler = adapter.Handler;")
        code.close()
        code.close()
        code.blank()
        code.line("private void __Handle<THandler>(ref THandler handler, GesNetworkEndEvent networkEvent) where THandler : \(handler)")
        code.open("{")
        let client = isClient ? "" : "networkEvent.Client, "
        code.line("switch (networkEvent.Kind)")
        code.open("{")
        if isClient {
            code.line("case GesNetworkEndEventKind.Connected:")
            code.line("    handler.OnConnected(networkEvent.Security);")
            code.line("    break;")
            code.line("case GesNetworkEndEventKind.Disconnected:")
            code.line("    handler.OnDisconnected(networkEvent.Code, networkEvent.Detail, networkEvent.ByPeer);")
            code.line("    break;")
        } else {
            code.line("case GesNetworkEndEventKind.Connecting:")
            code.line("    handler.OnConnecting(networkEvent.Client, networkEvent.Payload);")
            code.line("    break;")
            code.line("case GesNetworkEndEventKind.ClientConnected:")
            code.line("    handler.OnClientConnected(networkEvent.Client, networkEvent.Security);")
            code.line("    break;")
            code.line("case GesNetworkEndEventKind.ClientDisconnected:")
            code.line("    handler.OnClientDisconnected(networkEvent.Client, networkEvent.Code, networkEvent.Detail, networkEvent.ByPeer);")
            code.line("    break;")
        }
        if !end.incoming.isEmpty {
            code.line("case GesNetworkEndEventKind.Message:")
            code.line("    switch (networkEvent.Route)")
            code.line("    {")
            for (route, name) in end.incoming {
                let root = roots(route.type)
                let record = field(name)
                code.line("        case \(route.name):")
                code.line("            try")
                code.line("            {")
                code.line("                \(record).Read(networkEvent.Payload);")
                code.line("            }")
                code.line("            catch (GesWireException)")
                code.line("            {")
                code.line("                End.Close(\(client)\"undecodable \(route.written)\");")
                code.line("                break;")
                code.line("            }")
                code.line("")
                if !root.isSet {
                    code.line("            handler.On\(name)(\(client)in \(record));")
                } else {
                    code.line("            switch (\(record).Selected)")
                    code.line("            {")
                    for member in root.plan.memberNames {
                        code.line("                case \(root.type).Member.\(member):")
                        code.line("                    \(caseField(name, member)).Value = \(record).\(member);")
                        code.line("                    handler.On\(name)(\(client)\(caseField(name, member)));")
                        code.line("                    break;")
                    }
                    code.line("            }")
                    code.line("")
                }
                code.line("            break;")
            }
            code.line("    }")
            code.line("")
            code.line("    break;")
        }
        code.line("case GesNetworkEndEventKind.Tick:")
        code.line("    handler.OnTick(\(client)networkEvent.TickNumber);")
        code.line("    break;")
        code.line("case GesNetworkEndEventKind.TransferProgress:")
        code.line("    handler.OnTransferProgress(\(client)networkEvent.Route, networkEvent.TransferId, networkEvent.Bytes, networkEvent.Total);")
        code.line("    break;")
        code.line("case GesNetworkEndEventKind.TransferCancelled:")
        code.line("    handler.OnTransferCancelled(\(client)networkEvent.Route, networkEvent.TransferId, networkEvent.Code);")
        code.line("    break;")
        code.line("case GesNetworkEndEventKind.Received:")
        code.line("    handler.OnReceived();")
        code.line("    break;")
        code.close()
        code.close()
        code.blank()
        code.line("private struct __Adapter<THandler> : IGesNetworkEndHandler where THandler : \(handler)")
        code.open("{")
        code.line("private readonly \(end.name) __end;")
        code.line("internal THandler Handler;")
        code.blank()
        code.line("internal __Adapter(\(end.name) end, THandler handler)")
        code.open("{")
        code.line("__end = end;")
        code.line("Handler = handler;")
        code.close()
        code.blank()
        code.line("public void Handle(GesNetworkEndEvent networkEvent) => __end.__Handle(ref Handler, networkEvent);")
        code.close()
        code.close()
    }

    // The topic members of a typed end that publishes: subscriptions on the server, the tick and the topic enum.
    static func tickMembers(_ code: inout Code, _ end: GesNetworkEndPlan, _ roots: (String) -> NetworkRoot) {
        let side = end.client ? "client" : "server"
        if !end.client {
            code.blank()
            code.line("/// <summary>Subscribes a client to a topic.</summary>")
            code.line("/// <param name=\"client\">The client.</param>")
            code.line("/// <param name=\"topic\">The topic.</param>")
            code.line("public void Subscribe(int client, Topic topic) => End.Subscribe(client, (int)topic);")
            code.blank()
            code.line("/// <summary>Unsubscribes a client from a topic.</summary>")
            code.line("/// <param name=\"client\">The client.</param>")
            code.line("/// <param name=\"topic\">The topic.</param>")
            code.line("public void Unsubscribe(int client, Topic topic) => End.Unsubscribe(client, (int)topic);")
        }
        code.blank()
        code.line("private int __tick_count;")
        code.line("private bool __tick_open;")
        code.blank()
        code.line("/// <summary>Opens a tick; disposing the returned tick closes it.</summary>")
        code.line("/// <returns>The open tick.</returns>")
        code.line("/// <exception cref=\"global::System.InvalidOperationException\">A tick is open already.</exception>")
        code.line("public Tick BeginTick()")
        code.open("{")
        code.line("End.BeginTick();")
        code.line("__tick_open = true;")
        code.line("return new Tick(this, unchecked(++__tick_count));")
        code.close()
        code.blank()
        code.line("/// <summary>The topics the \(side) publishes.</summary>")
        code.line("public enum Topic")
        code.open("{")
        for route in end.topics {
            code.line("/// <summary>The topic <c>\(route.side) \(route.verb) \(route.written) \(route.deliveryClass)</c>.</summary>")
            code.line("\(route.root) = \(route.name),")
        }
        code.close()
        code.blank()
        code.line("/// <summary>An open tick: its publications arrive together. <see cref=\"Dispose\"/> closes it.</summary>")
        code.line("public readonly struct Tick : global::System.IDisposable")
        code.open("{")
        code.line("private readonly \(end.name) __end;")
        code.line("private readonly int __number;")
        code.blank()
        code.line("internal Tick(\(end.name) end, int number)")
        code.open("{")
        code.line("__end = end;")
        code.line("__number = number;")
        code.close()
        for route in end.topics {
            let root = roots(route.type)
            for parameter in root.isSet ? ["in " + root.type, root.type + ".Case"] : ["in " + root.type] {
                let record = parameter.hasPrefix("in ")
                code.blank()
                code.line("/// <summary>Publishes a \(record ? "record" : "message") on the topic <c>\(route.written)</c> as a member of the tick.</summary>")
                code.line("/// <param name=\"value\">The value.</param>")
                code.line("/// <returns>The status of the publication.</returns>")
                code.line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>")
                code.line("public GesNetworkSendStatus Publish(\(parameter) value) => __end.Publish(\(record ? "in " : "")value);")
            }
        }
        code.blank()
        code.line("/// <summary>Makes a latest topic a member of the tick with its last published state.</summary>")
        code.line("/// <param name=\"topic\">The topic.</param>")
        code.line("/// <exception cref=\"global::System.InvalidOperationException\">The topic has no published state.</exception>")
        code.line("/// <exception cref=\"global::System.ArgumentException\">The topic is no latest topic.</exception>")
        code.line("public void Include(Topic topic) => __end.End.Include((int)topic);")
        code.blank()
        code.line("/// <summary>Closes the tick. A further call, also on a copy, does nothing.</summary>")
        code.line("public void Dispose()")
        code.open("{")
        code.line("if (__end is null || !__end.__tick_open || __end.__tick_count != __number) return;")
        code.line("__end.__tick_open = false;")
        code.line("__end.End.EndTick();")
        code.close()
        code.close()
    }

    // The handler interface of a typed end: one method per event, each empty by default.
    static func endHandler(_ code: inout Code, _ network: GesLayoutNetwork, _ end: GesNetworkEndPlan, _ roots: (String) -> NetworkRoot) {
        let isClient = end.client
        let client = isClient ? "" : "int client, "

        func method(_ summary: String, _ signature: String, _ parameters: String...) {
            code.blank()
            code.line("/// <summary>\(summary)</summary>")
            // The client parameter, where a method has one, comes first.
            if !isClient && signature.drop(while: { $0 != "(" }).dropFirst().hasPrefix("int client") { code.line("/// <param name=\"client\">The client.</param>") }
            for parameter in parameters { code.line(parameter) }
            code.line("void \(signature)")
            code.open("{")
            code.close()
        }

        code.blank()
        code.line("/// <summary>")
        code.line("/// The events of the typed \(isClient ? "client" : "server") end <see cref=\"\(end.name)\"/>; every method does nothing by default. A struct handler that")
        code.line("/// leaves a method to its default is boxed when that method is called.")
        code.line("/// </summary>")
        code.line("public interface I\(end.name)Handler")
        code.open("{")
        if isClient {
            method("The session is established.", "OnConnected(GesNetworkSecurity security)", "/// <param name=\"security\">The reached security.</param>")
            method(
                "The connection closed.",
                "OnDisconnected(string code, string detail, bool byPeer)",
                "/// <param name=\"code\">The close code.</param>",
                "/// <param name=\"detail\">The detail.</param>",
                "/// <param name=\"byPeer\">Whether the server closed it.</param>"
            )
        } else {
            method(
                "A client asks to connect; refuse it with <see cref=\"Server.Refuse\"/> before the method returns, or it is accepted.",
                "OnConnecting(int client, ReadOnlySpan<byte> token)",
                "/// <param name=\"token\">The client's token, valid until the method returns.</param>"
            )
            method("A client is connected.", "OnClientConnected(int client, GesNetworkSecurity security)", "/// <param name=\"security\">The reached security.</param>")
            method(
                "A client's connection closed; the client leaves the end.",
                "OnClientDisconnected(int client, string code, string detail, bool byPeer)",
                "/// <param name=\"code\">The close code.</param>",
                "/// <param name=\"detail\">The detail.</param>",
                "/// <param name=\"byPeer\">Whether the client closed it.</param>"
            )
        }
        for (route, name) in end.incoming {
            let root = roots(route.type)
            method(
                "A \(root.isSet ? "message" : "record") arrived on the route <c>\(route.side) \(route.verb) \(route.written) \(route.deliveryClass)</c>.",
                "On\(name)(\(client)\(eventParameter(root)) value)",
                "/// <param name=\"value\">The \(root.isSet ? "message, whose record" : "record, which") is valid until the method returns.</param>"
            )
        }
        method("A tick is complete; the events of its changed members precede it.", "OnTick(\(client)int number)", "/// <param name=\"number\">The tick number.</param>")
        method(
            "A transfer on a bulk route advanced.",
            "OnTransferProgress(\(client)int route, int transferId, long bytes, long total)",
            "/// <param name=\"route\">The route index.</param>",
            "/// <param name=\"transferId\">The transfer.</param>",
            "/// <param name=\"bytes\">The bytes received so far.</param>",
            "/// <param name=\"total\">The total bytes.</param>"
        )
        method(
            "A transfer was cancelled; the session continues.",
            "OnTransferCancelled(\(client)int route, int transferId, string code)",
            "/// <param name=\"route\">The route index.</param>",
            "/// <param name=\"transferId\">The transfer.</param>",
            "/// <param name=\"code\">The cancel code.</param>"
        )
        method("The last event of a poll that reported at least one message or tick.", "OnReceived()")
        code.close()
    }

    // A named tag set, declared once beside the records that use it.
    static func tagSetDeclaration(_ code: inout Code, _ name: String, _ tags: [String], _ access: String) {
        code.blank()
        code.line("/// <summary>The tag set <c>:\(name)</c>, in declared order.</summary>")
        code.line("\(access) enum \(name) : \(underlying(tags.count))")
        code.open("{")
        for tag in tags {
            code.line("/// <summary>The tag <c>#\(tag)</c>.</summary>")
            code.line(GesSchemaPlanner.pascal(tag) + ",")
        }
        code.close()
    }

    // One bit per member in declared order, so the enum value is the wire mask; the empty value needs no name.
    static func flagsDeclaration(_ code: inout Code, _ name: String, _ members: [String]) {
        code.blank()
        code.line("/// <summary>The members of the field, one bit each in declared order.</summary>")
        code.line("[global::System.Flags]")
        code.line("public enum \(name) : ulong")
        code.open("{")
        for (index, member) in members.enumerated() {
            code.line("/// <summary>The member <c>#\(member)</c>.</summary>")
            code.line("\(GesSchemaPlanner.pascal(member)) = 1UL << \(index),")
        }
        code.close()
    }

    static func full(_ width: Int) -> UInt64 { width >= 64 ? .max : (1 << UInt64(width)) - 1 }

    static func long(_ value: Int64) -> String { value == .min ? "long.MinValue" : "\(value)L" }

    static func unsigned(_ value: UInt64) -> String { "\(value)UL" }

    // An integral binary64 such as 10^k or p, exact as a literal.
    static func double(_ value: Int64) -> String { "\(value)d" }

    static func bits(_ value: Double) -> String { "global::System.BitConverter.Int64BitsToDouble(unchecked((long)0x\(GesSchemaLayoutJson.hex(value.bitPattern))UL))" }
}
