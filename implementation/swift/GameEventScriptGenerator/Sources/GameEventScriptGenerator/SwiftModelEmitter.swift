// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Writes Swift models: a Hashable, Sendable and Codable struct per schema with stored properties in declared order and a
// memberwise initializer, an enum per closed Tag and per union, and the runtime's GesPoint and GesVector for spatial fields.
enum GesSwiftModelEmitter {
    typealias Code = GesSchemaPlanner.Code
    typealias Names = [(name: String, origin: String)]

    static func emit(_ layout: GesModelLayout, _ options: GesSchemaGenerateOptions) throws(GesGeneratorError) -> GesCodeDocument {
        let access = options.access == .public ? "public " : ""
        let header = GesModelPlanner.header(layout)
        let suffix = GesSchemaPlanner.suffix(header, options)
        let values = "__GesModelValues" + suffix
        let support = "__GesModels" + suffix
        var prologue = Code()
        prologue.line("// swift-format-ignore-file")
        prologue.blank()
        prologue.line("import GameEventScriptRuntime")
        prologue.blank()
        try GesModelPlanner.checkProtocols(layout, swift: true)
        var document = GesCodeDocument(
            header: header,
            prologue: prologue.text,
            space: try GesSchemaPlanner.namespace(options.namespace, GesSwiftEmitter.keywords),
            swift: true,
            access: access,
            holds: "model"
        )
        let computed = GesModelPlanner.computed(layout, GesComputedEmitter.helper(options.namespace, suffix), swift: true)
        let documents = options.documents ? "__GesModelDocuments" + suffix : nil
        let documentType = options.documents ? try GesModelPlanner.documentType(layout, document.space) : ""
        for type in layout.types {
            let reads = layout.roots.contains(type.name) ? documents : nil
            document.units.append(
                GesCodeUnit(owner: type.name) { code in
                    try record(&code, layout, type, access, bindings: options.bindings, values: options.values ? values : nil, computed: computed, documents: reads)
                }
            )
        }
        for protocolType in layout.protocols {
            document.units.append(GesCodeUnit(owner: protocolType.name) { code in protocolDeclaration(&code, layout, protocolType, access) })
        }
        for set in GesModelPlanner.tagSets(layout) {
            document.units.append(
                GesCodeUnit(owner: set.name) { code in
                    tagEnum(&code, set.name, set.tags, access, "/// The tag set `:\(set.name)`, in declared order; the raw value is the tag's name.")
                }
            )
        }
        if options.bindings { document.units.append(GesCodeUnit(owner: "") { code in bindings(&code, layout, access, support, computed) }) }
        if options.values { document.units.append(GesCodeUnit(owner: "") { code in valueConversions(&code, layout, values, support, computed) }) }
        if let documents {
            document.units.append(GesCodeUnit(owner: "") { code in documentTypeDeclaration(&code, layout, documentType, documents, access) })
            document.units.append(GesCodeUnit(owner: "") { code in documentsHolder(&code, layout, documents) })
        }
        if options.bindings || options.values { document.units.append(GesCodeUnit(owner: "") { code in supportType(&code, support) }) }
        if layout.types.contains(where: { !$0.computed.isEmpty }) {
            let values = options.bindings || options.values || computed.needsValues(layout.types.map { ($0.name, $0.computed) })
            document.units.append(GesCodeUnit(owner: "") { code in GesComputedLibrary.write(&code, computed.helper, swift: true, values: values) })
        }
        return document
    }

    private static func name(_ name: String) -> String { GesSwiftEmitter.name(name) }

    // Reads a document of any root by its header, so that a host reading files of several kinds needs no switch on names.
    private static func documentTypeDeclaration(_ code: inout Code, _ layout: GesModelLayout, _ name: String, _ documents: String, _ access: String) {
        code.line("/// A GES document of one of the model roots of this file, chosen by its header `:Schema(:Name)`.")
        code.open("\(access)enum \(name): Hashable, Sendable {")
        for root in layout.roots {
            code.line("/// A document of `:\(root)`.")
            code.line("case \(member(root))(\(root))")
        }
        code.blank()
        code.line("/// Reads GES document text whose header names a model root of this file, as that root's `init(document:)` does.")
        code.line("/// - Throws: `GesDocumentError` when the text is no document, names no root of this file (`document.schemaMismatch`), or")
        code.line("///   breaks its schema, with the `document.*` or `wire.*` code, the line, the column and the field path.")
        code.open("\(access)init(document text: String) throws(GesDocumentError) {")
        code.line("let document = try \(documents).read(text, [\(layout.roots.map { GesSchemaPlanner.literal($0, swift: true) }.joined(separator: ", "))])")
        code.open("do {")
        code.line("switch document.schema {")
        for (index, root) in layout.roots.enumerated() {
            code.line(
                index + 1 < layout.roots.count
                    ? "case \(GesSchemaPlanner.literal(root, swift: true)): self = .\(member(root))(try \(root)(value: document.value))"
                    : "default: self = .\(member(root))(try \(root)(value: document.value))"
            )
        }
        code.line("}")
        code.close("} catch {")
        code.line("    throw document.error(for: error)")
        code.line("}")
        code.close()
        code.close()
    }

    // The schema sources of this file, parsed when a document is first read.
    private static func documentsHolder(_ code: inout Code, _ layout: GesModelLayout, _ holder: String) {
        code.line("/// The schema sources of this file, which reading documents checks them against.")
        code.open("enum \(holder) {")
        code.open("static let set = Result<GesSchemaSet, any Error> {")
        code.open("try GesSchemaSet(sources: [")
        for source in layout.sources {
            code.open("GesSchemaSource(name: \(GesSchemaPlanner.literal(source.name, swift: true)), text: [")
            for line in GesSchemaPlanner.lines(source.text) { code.line(GesSchemaPlanner.literal(line, swift: true) + ",") }
            code.close("].joined(separator: \"\\n\")),")
        }
        code.close("])")
        code.close()
        code.blank()
        code.open("static func read(_ text: String, _ roots: [String]) throws(GesDocumentError) -> GesSchemaDocument {")
        code.open("guard let set = try? Self.set.get() else {")
        code.line("throw GesDocumentError(code: GesSchemaDocument.schemaMismatch, line: 1, column: 1, message: \"The embedded schema sources no longer parse.\")")
        code.close()
        code.line("return try GesSchemaDocument(text, schemas: set, expected: roots)")
        code.close()
        code.close()
    }

    private static func member(_ pascal: String) -> String { GesSwiftEmitter.member(pascal) }

    // The argument label of a declaration: Swift needs backticks there only for inout, var and let; a call needs none.
    private static func label(_ pascal: String) -> String {
        let name = GesSchemaPlanner.camel(pascal)
        return name == "inout" || name == "var" || name == "let" ? "`" + name + "`" : name
    }

    /// The read-only properties of the fields a schema lends with `with`.
    private static func protocolDeclaration(_ code: inout Code, _ layout: GesModelLayout, _ protocolType: GesModelProtocol, _ access: String) {
        let refines = protocolType.takes.map { GesModelPlanner.protocolName(layout, $0, swift: true) }
        code.line("/// The fields of the schema `:\(protocolType.name)`, which every model type that takes them unchanged with `with` has.")
        code.open("\(access)protocol \(GesModelPlanner.protocolName(layout, protocolType.name, swift: true))\(refines.isEmpty ? "" : ": " + refines.joined(separator: ", ")) {")
        for (index, field) in GesModelPlanner.fields(GesModelType(name: protocolType.name, fields: protocolType.fields)).enumerated() {
            if index > 0 { code.blank() }
            code.line("/// `\(field.field.name): \(GesModelPlanner.describe(field.field))`.")
            code.line("var \(member(field.base)): \(self.type(field.value)) { get }")
        }
        code.close()
    }

    private static func record(
        _ code: inout Code,
        _ layout: GesModelLayout,
        _ type: GesModelType,
        _ access: String,
        bindings: Bool,
        values: String?,
        computed: GesComputedEmitter,
        documents: String?
    ) throws(GesGeneratorError) {
        let fields = GesModelPlanner.fields(type)
        let key = fields.first { $0.field.key }
        let identity = key.map { GesSchemaPlanner.camel($0.base) != "id" } ?? false
        var names: Names = []
        for field in fields {
            names.append((GesSchemaPlanner.camel(field.base), "the field " + field.field.name))
            nested(field.value, &names, field.field.name)
        }
        if let key, identity { names.append(("id", "the identity of the key " + key.field.name)) }
        names.append(("schemaHash", "the generated member schemaHash"))
        for constant in type.constants { names.append((constant.name, "the let " + constant.name)) }
        GesComputedEmitter.names(type.computed, type.name, swift: true, &names)
        if bindings { names.append(("gesValue", "the generated member gesValue")) }
        if values != nil { names.append(("toValue", "the generated member toValue")) }
        if bindings && fields.contains(where: { $0.field.name == "fields" }) {
            throw GesGeneratorError(GesGeneratorError.nameCollision, "In '\(type.name)', the field fields and the generated field fields of its bindings share a name.")
        }
        try GesSchemaPlanner.unique(type.name, names)

        code.line("/// The model of the schema `:\(type.name)`: a plain value that is never encoded.")
        let adopted = GesModelPlanner.adopted(layout, type, swift: true).map { ", " + $0 }.joined()
        code.open("\(access)struct \(type.name): Hashable, Sendable, Codable\(key == nil ? "" : ", Identifiable")\(adopted) {")
        code.line("/// The hash of the schema `:\(type.name)`, which wire records of it carry too; a test that compares it notices stale generated code.")
        code.line("\(access)static let schemaHash: UInt64 = 0x\(GesSchemaLayoutJson.hex(type.hash))")
        GesSwiftEmitter.lets(&code, type.constants, access, "Int")
        if !fields.isEmpty { code.blank() }
        for (index, field) in fields.enumerated() {
            if index > 0 { code.blank() }
            code.line("/// `\(field.field.name): \(GesModelPlanner.describe(field.field))`.")
            code.line("\(access)var \(member(field.base)): \(self.type(field.value))")
        }
        if let key, identity {
            code.blank()
            code.line("/// The key `\(key.field.name)`, which identifies the record in a keyed list.")
            code.line("\(access)var id: \(self.type(key.value)) { \(member(key.base)) }")
        }
        computed.members(&code, type.name, type.computed, String(access.dropLast(access.hasSuffix(" ") ? 1 : 0)))
        code.blank()
        let siblings = fields.contains { GesModelPlanner.sibling(fields, $0) != nil }
        code.line("/// Creates a record from its fields; an absent value defaults to nil, a list to empty and a field with a default to it\(siblings ? ", where nil takes the field it names" : "").")
        let parameters = fields.map { field in
            if GesModelPlanner.sibling(fields, field) != nil { return "\(label(field.base)): \(plain(field.value))? = nil" }
            if let fallback = field.field.defaultValue { return "\(label(field.base)): \(self.type(field.value)) = \(native(field.value, fallback))" }
            return "\(label(field.base)): \(self.type(field.value))\(defaultValue(field.value))"
        }
        code.open("\(access)init(\(parameters.joined(separator: ", "))) {")
        for index in GesModelPlanner.readOrder(fields) {
            let field = fields[index]
            let fallback = GesModelPlanner.sibling(fields, field).map { " ?? self." + member(fields[$0].base) } ?? ""
            code.line("self.\(member(field.base)) = \(member(field.base))\(fallback)")
        }
        code.close()
        if bindings {
            code.blank()
            code.line("/// The record as the external value `:\(type.name)` of ``GesTypes``; scripts read its fields as this copy holds them.")
            code.line("\(access)func gesValue() -> GesValue { .external(GesTypes.\(type.name)Value(self)) }")
            code.blank()
            code.line("/// Reads a `:\(type.name)` that a script holds, or fails for another value.")
            code.open("\(access)init?(gesValue value: GesValue) {")
            code.line("guard let held = value.externalValue as? GesTypes.\(type.name)Value else { return nil }")
            code.line("self = held.record")
            code.close()
        }
        if let values {
            code.blank()
            code.line("/// The record as a plain GES Record of its fields, as `GesSchemaValues` decodes the bytes of the record.")
            code.line("\(access)func toValue() -> GesValue { \(values).value(self) }")
            code.blank()
            code.line("/// Reads a Record of this schema, or a Map of only its fields, as `GesSchemaValues` encodes it.")
            code.line("/// - Throws: `GesWireError` with `wire.typeMismatch` at the path of a field that does not convert.")
            code.line("\(access)init(value: GesValue) throws(GesWireError) { self = try \(values).read\(type.name)(value, \"\") }")
        }
        if let documents {
            code.blank()
            code.line("/// Reads a record from GES document text with the header `:Schema(:\(type.name))`: the document is checked against the schema")
            code.line("/// sources of this file, and fields it leaves out take their defaults. Reading allocates and does no file I/O.")
            code.line("/// - Throws: `GesDocumentError` when the text is no document, names another schema (`document.schemaMismatch`), or breaks the")
            code.line("///   schema, with the `document.*` or `wire.*` code, the line, the column and the field path.")
            code.open("\(access)init(document text: String) throws(GesDocumentError) {")
            code.line("let document = try \(documents).read(text, [\(GesSchemaPlanner.literal(type.name, swift: true))])")
            code.open("do {")
            code.line("self = try Self(value: document.value)")
            code.close("} catch {")
            code.line("    throw document.error(for: error)")
            code.line("}")
            code.close()
        }
        for field in fields { try declarations(&code, field.value, access) }
        code.close()
    }

    private static func nested(_ value: GesModelPlanValue, _ names: inout Names, _ field: String) {
        if !value.nested.isEmpty && value.tagSet.isEmpty { names.append((value.nested, "a type of the field " + field)) }
        if let element = value.element { nested(element, &names, field) }
        for alternative in value.alternatives { nested(alternative, &names, field) }
    }

    /// Returns the record read from `field<n>`, setting afterwards the optional fields whose initializer would take a sibling.
    private static func construct(_ code: inout GesSchemaPlanner.Code, _ type: String, _ arguments: String, _ fields: [GesModelPlanField]) {
        let kept = GesModelPlanner.kept(fields)
        if kept.isEmpty {
            code.line("return \(type)(\(arguments))")
            return
        }
        code.line("var record = \(type)(\(arguments))")
        for index in kept { code.line("record.\(member(fields[index].base)) = field\(index)") }
        code.line("return record")
    }

    /// A literal default as a native value.
    private static func native(_ value: GesModelPlanValue, _ fallback: GesLayoutDefault) -> String {
        switch fallback.kind {
        case .boolean: fallback.boolean ? "true" : "false"
        case .tag where value.closedTag: "." + name(fallback.value)
        case .tag: literal(fallback.value)
        default: GesModelPlanner.number(value, fallback)
        }
    }

    /// The GES value that a field left out of a Record or Map takes: its literal default, the value read for the sibling it
    /// names, or else `missing`.
    private static func held(_ fields: [GesModelPlanField], _ index: Int, _ owner: String, _ support: String, _ sibling: (Int) -> String, _ missing: String) -> String {
        let field = fields[index]
        let key = "fields.get(\(literal(field.field.name)))"
        guard let fallback = field.field.defaultValue else { return missing }
        switch fallback.kind {
        case .field: return "\(key) ?? \(sibling(GesModelPlanner.sibling(fields, field)!))"
        case .tag: return "\(key) ?? \(support).tag(\(literal(fallback.value)))"
        default: return "\(key) ?? \(encode(field.value, owner, native(field.value, fallback), "", support, nullable: false, plain: true))"
        }
    }

    /// A field's GES value inline, following defaults to siblings; `.nothing` when no value or default is left.
    private static func inline(_ fields: [GesModelPlanField], _ index: Int, _ owner: String, _ support: String) -> String {
        held(fields, index, owner, support, { inline(fields, $0, owner, support) }, "fields.get(\(literal(fields[index].field.name))) ?? .nothing")
    }

    private static func defaultValue(_ value: GesModelPlanValue) -> String {
        value.nullable ? " = nil" : value.kind == .list || value.kind == .dice || value.kind == .bytes || value.kind == .flags ? " = []" : ""
    }

    static func type(_ value: GesModelPlanValue) -> String { plain(value) + (value.nullable ? "?" : "") }

    private static func plain(_ value: GesModelPlanValue) -> String {
        switch value.kind {
        case .boolean: "Bool"
        case .number: value.model.integer ? "Int" : "Double"
        case .tag: value.closedTag ? value.nested : "String"
        case .text: "String"
        case .bytes: "[UInt8]"
        case .list: "[\(type(value.element!))]"
        case .dice: "[Int]"
        case .point: "GesPoint"
        case .vector: "GesVector"
        case .box: "GesBox"
        case .poly: "GesPoly"
        case .schema: value.model.schema
        case .union, .nothing, .flags: value.nested
        }
    }

    // A closed Tag's enum, nested in its record or, for a named tag set, declared once beside the records.
    private static func tagEnum(_ code: inout Code, _ name: String, _ tags: [String], _ access: String, _ summary: String) {
        code.line(summary)
        code.open("\(access)enum \(name): String, Hashable, Sendable, Codable, CaseIterable {")
        for tag in tags {
            code.line("/// The tag `#\(tag)`.")
            code.line("case \(self.name(tag))")
        }
        code.close()
    }

    private static func declarations(_ code: inout Code, _ value: GesModelPlanValue, _ access: String) throws(GesGeneratorError) {
        if value.closedTag {
            if !value.tagSet.isEmpty { return }
            code.blank()
            tagEnum(&code, value.nested, value.model.tags, access, "/// The tags, in declared order; the raw value is the tag's name.")
        } else if value.kind == .flags {
            try GesSwiftEmitter.flagsDeclaration(&code, value.nested, value.model.tags, access)
        } else if value.kind == .union {
            try GesSchemaPlanner.unique(value.nested, value.alternatives.map { (GesSchemaPlanner.camel($0.label), "the alternative " + $0.label) })
            code.blank()
            code.line("/// The alternatives, in declared order.")
            code.open("\(access)enum \(value.nested): Hashable, Sendable, Codable {")
            for alternative in value.alternatives {
                code.line("/// The alternative `:\(alternative.label)`.")
                code.line("case \(member(alternative.label))(\(type(alternative)))")
            }
            code.close()
            for alternative in value.alternatives { try declarations(&code, alternative, access) }
        } else if let element = value.element {
            try declarations(&code, element, access)
        }
    }

    private static func unit(_ unit: String) -> String { "." + unit }

    private static func literal(_ text: String) -> String { GesSchemaPlanner.literal(text, swift: true) }

    // Converts a native value to a GES value; a plain value holds nested records as Records rather than as external values.
    // `helper` declares the conversions of the file's unions and Flags, `support` the conversions that both share.
    private static func encode(_ value: GesModelPlanValue, _ owner: String, _ expression: String, _ helper: String, _ support: String, nullable: Bool = true, plain: Bool = false) -> String {
        if nullable && value.nullable { return "\(support).present(\(expression), { \(encode(value, owner, "$0", helper, support, nullable: false, plain: plain)) })" }
        switch value.kind {
        case .boolean: return ".boolean(\(expression))"
        case .number where value.model.type == "Percentage": return ".percentage(\(expression))"
        case .number where value.model.integer: return ".integer(Int64(\(expression)), unit: \(unit(value.model.unit)))"
        case .number: return ".float(\(expression), unit: \(unit(value.model.unit)))"
        case .text: return ".text(\(expression))"
        case .bytes: return ".bytes(\(expression))"
        case .tag where value.closedTag: return "\(support).tag(\(expression).rawValue)"
        case .tag: return "\(support).tag(\(expression))"
        case .list: return ".list(\(expression).map { \(encode(value.element!, owner, "$0", helper, support, plain: plain)) })"
        case .dice: return "\(support).dice(\(expression))"
        case .point, .vector, .box, .poly: return "\(expression).value(unit: \(unit(value.model.unit)))"
        case .schema: return plain ? "\(expression).toValue()" : "\(expression).gesValue()"
        case .union, .nothing, .flags: return "\(helper).value(\(expression))"
        }
    }

    // Converts a GES value to a native value, or nil when it does not convert.
    private static func decode(_ value: GesModelPlanValue, _ owner: String, _ expression: String, _ helper: String, _ support: String, nullable: Bool = true, plain: Bool = false) -> String {
        if nullable && value.nullable { return "\(support).optional(\(expression), { \(decode(value, owner, "$0", helper, support, nullable: false, plain: plain)) })" }
        switch value.kind {
        case .boolean: return "\(support).boolean(\(expression))"
        case .number where value.model.type == "Percentage": return "\(support).percentage(\(expression))"
        case .number where value.model.integer: return "\(support).integer(\(expression), \(unit(value.model.unit)))"
        case .number: return "\(support).number(\(expression), \(unit(value.model.unit)))"
        case .text: return "\(support).text(\(expression))"
        case .bytes: return "\(support).bytes(\(expression))"
        case .tag where value.closedTag:
            return "\(support).tag(\(expression)).flatMap(\(value.tagSet.isEmpty ? owner + "." + value.nested : value.tagSet).init(rawValue:))"
        case .tag: return "\(support).tag(\(expression))"
        case .list: return "\(support).list(\(expression), { \(decode(value.element!, owner, "$0", helper, support, plain: plain)) })"
        case .dice: return "\(support).dice(\(expression))"
        case .point: return "GesPoint(\(expression), unit: \(unit(value.model.unit)))"
        case .vector: return "GesVector(\(expression), unit: \(unit(value.model.unit)))"
        case .box: return "GesBox(\(expression), unit: \(unit(value.model.unit)))"
        case .poly: return "GesPoly(\(expression), unit: \(unit(value.model.unit)))"
        case .schema: return plain ? "\(helper).decode\(value.model.schema)(\(expression))" : "\(value.model.schema)(gesValue: \(expression))"
        case .union, .nothing, .flags: return "\(helper).decode\(owner)\(value.nested)(\(expression))"
        }
    }

    // Whether reading a value descends into it, so that a failure inside reports its own path: a nested record or a list.
    private static func descends(_ value: GesModelPlanValue) -> Bool { value.kind == .schema || value.kind == .list }

    // An expression that reads a value that descends, or throws wire.typeMismatch at the path of the part that does not convert:
    // a nested record's field, or a list element with `[]`, as the value path names them.
    private static func read(
        _ value: GesModelPlanValue,
        _ owner: String,
        _ expression: String,
        _ path: String,
        _ helper: String,
        _ support: String,
        _ depth: Int = 0,
        nullable: Bool = true
    ) -> String {
        let x = "x\(depth)"
        let p = "p\(depth)"
        if nullable && value.nullable {
            return "try readOptional(\(expression), \(path)) { (\(x): GesValue, \(p): String) throws(GesWireError) in \(read(value, owner, x, p, helper, support, depth + 1, nullable: false)) }"
        }
        if value.kind == .schema { return "try read\(value.model.schema)(\(expression), \(path))" }
        let element =
            descends(value.element!)
            ? read(value.element!, owner, x, p, helper, support, depth + 1)
            : "try require(\(decode(value.element!, owner, x, helper, support, plain: true)), \(x), \(p))"
        return "try readList(\(expression), \(path)) { (\(x): GesValue, \(p): String) throws(GesWireError) in \(element) }"
    }

    private static func definition(_ name: String, _ value: GesModelPlanValue) -> String {
        let declared = GesModelPlanner.declared(value)
        guard let kind = declared.kind else { return "try! .init(name: \(literal(name)), typeName: \(literal(declared.typeName!)))" }
        let kindName = GesSchemaPlanner.camel(kind)
        return declared.unit == "none"
            ? "try! .init(name: \(literal(name)), kind: .\(kindName))" : "try! .init(name: \(literal(name)), kind: .\(kindName), unit: \(unit(declared.unit)))"
    }

    // The unions of a type, including those in lists and alternatives, for their conversion functions.
    private static func unions(_ value: GesModelPlanValue, _ found: inout [GesModelPlanValue]) {
        if value.kind == .union || value.kind == .flags { found.append(value) }
        if let element = value.element { unions(element, &found) }
        for alternative in value.alternatives { unions(alternative, &found) }
    }

    // The catalog and constructor registry of the models, with an external value and a constructor per type.
    private static func bindings(_ code: inout Code, _ layout: GesModelLayout, _ access: String, _ support: String, _ computed: GesComputedEmitter) {
        let ordered = GesModelPlanner.dependencyOrder(layout)
        code.blank()
        code.line("/// The model types for scripts as external types: pass one instance to the compiler as its catalog and to the host as")
        code.line("/// its external-type registry. Each type has its fields and one constructor that takes every field by name; a value that")
        code.line("/// does not convert, such as another unit or kind, constructs `nothing`. Types and constructors that this set does not")
        code.line("/// declare resolve through the next catalog and registry.")
        code.open("\(access)final class GesTypes: GesExternalTypeCatalog, GesExternalTypeRegistry {")
        code.line("/// The declarations of the generated types, each after the types it holds, then those of the next catalog.")
        code.line("\(access)let types: [GesExternalTypeDefinition]")
        code.line("private let nextCatalog: (any GesExternalTypeCatalog)?")
        code.line("private let nextRegistry: (any GesExternalTypeRegistry)?")
        code.line("nonisolated(unsafe) private static let own: [GesExternalTypeDefinition] = [\(ordered.map { $0.name + "Value.definition" }.joined(separator: ", "))]")
        code.blank()
        code.line("/// Creates the set in front of another catalog and registry.")
        code.line("/// - Throws: `GesApiError.invalidArgument` when the next catalog already declares a type of this set.")
        code.open("\(access)init(nextCatalog: (any GesExternalTypeCatalog)? = nil, nextRegistry: (any GesExternalTypeRegistry)? = nil) throws {")
        code.line("let others = nextCatalog?.types ?? []")
        code.open("if let clash = others.first(where: { other in Self.own.contains { $0.name == other.name } }) {")
        code.line("throw GesApiError.invalidArgument(\"The type :\\(clash.name) is already declared.\")")
        code.close()
        code.line("types = Self.own + others")
        code.line("self.nextCatalog = nextCatalog")
        code.line("self.nextRegistry = nextRegistry")
        code.close()
        code.blank()
        code.line("/// Resolves a generated type, or asks the next catalog.")
        code.open("\(access)func resolve(_ typeName: String) throws -> GesExternalTypeDefinition? {")
        code.line("if let own = Self.own.first(where: { $0.name == typeName }) { return own }")
        code.line("return try nextCatalog?.resolve(typeName)")
        code.close()
        code.blank()
        code.line("/// Resolves the constructor of a generated type, or asks the next registry.")
        code.open("\(access)func resolve(_ reference: GesExternalTypeConstructorReference) throws -> (any GesExternalTypeConstructor)? {")
        code.line("switch reference.signatureID {")
        for type in ordered { code.line("case \(type.name)Value.definition.constructors[0].signatureID: return \(type.name)Constructor()") }
        code.line("default: return try nextRegistry?.resolve(reference)")
        code.line("}")
        code.close()
        for type in ordered {
            let fields = GesModelPlanner.fields(type)
            let names = fields.map { literal($0.field.name) }.joined(separator: ", ")
            code.blank()
            code.line("/// A `:\(type.name)` held by a script.")
            code.open("final class \(type.name)Value: GesExternalValue {")
            code.line("nonisolated(unsafe) static let definition = try! GesExternalTypeDefinition(")
            code.line("    name: \(literal(type.name)),")
            var definitions = fields.map { definition($0.field.name, $0.value) }
            for field in type.computed {
                let name = literal(field.name)
                switch field.kind {
                case .boolean: definitions.append("try! .init(name: \(name), kind: .boolean)")
                case .percentage: definitions.append("try! .init(name: \(name), kind: .percentage)")
                case .quantity: definitions.append("try! .init(name: \(name), kind: .float, unit: .\(field.unit.rawValue))")
                default: definitions.append("try! .init(name: \(name), kind: .float)")
                }
            }
            code.line("    fields: [\(definitions.joined(separator: ", "))\(definitions.isEmpty ? "" : ", ")try! .init(name: \"fields\", kind: .map)],")
            code.line("    constructors: [try! .init(typeName: \(literal(type.name)), parameters: [try! .init(name: \"fields\", kind: .map)])]")
            code.line(")")
            code.line("static let names = [\(names)]")
            code.line("let record: \(type.name)")
            code.blank()
            code.line("init(_ record: \(type.name)) { self.record = record }")
            code.blank()
            code.line("var definition: GesExternalTypeDefinition { Self.definition }")
            code.blank()
            code.open("func field(_ name: String) throws -> GesValue? {")
            code.line("switch name {")
            for field in fields { code.line("case \(literal(field.field.name)): \(encode(field.value, type.name, "record." + member(field.base), "GesTypes", support))") }
            for field in type.computed {
                let unit = field.kind == .quantity ? field.unit.rawValue : "none"
                code.line("case \(literal(field.name)): \(computed.helper).gesValue(\(computed.valueOf("record", type.name, field)), .\(unit))")
            }
            code.line("case \"fields\": .map(keys: Self.names, values: [\(fields.map { "try field(\(literal($0.field.name)))!" }.joined(separator: ", "))])")
            code.line("default: nil")
            code.line("}")
            code.close()
            code.close()
            code.blank()
            code.line("/// Constructs a `:\(type.name)` from a Map or Record of its fields; an absent optional field may be left out.")
            code.open("private struct \(type.name)Constructor: GesExternalTypeConstructor {")
            code.line("var definition: GesExternalTypeConstructorDefinition { \(type.name)Value.definition.constructors[0] }")
            code.blank()
            code.open("func invoke(_ call: GesExternalTypeConstructorCall) throws {")
            code.open("guard")
            code.line("let fields = call.arguments[0].asMap, \(support).known(fields, \(type.name)Value.names)\(fields.isEmpty ? "" : ",")")
            for (index, field) in fields.enumerated() {
                code.line("let \(member(field.base)) = \(decode(field.value, type.name, inline(fields, index, type.name, support), "GesTypes", support))\(index == fields.count - 1 ? "" : ",")")
            }
            code.dedent()
            code.open("else {")
            code.line("call.setNothing()")
            code.line("return")
            code.close()
            let created = "\(type.name)(\(fields.map { "\(GesSchemaPlanner.camel($0.base)): \(member($0.base))" }.joined(separator: ", ")))"
            let kept = GesModelPlanner.kept(fields)
            if kept.isEmpty {
                code.line("try call.setExternalValue(\(type.name)Value(\(created)))")
            } else {
                code.line("var record = \(created)")
                for index in kept { code.line("record.\(member(fields[index].base)) = \(member(fields[index].base))") }
                code.line("try call.setExternalValue(\(type.name)Value(record))")
            }
            code.close()
            code.close()
            conversions(&code, type.name, fields, helper: "GesTypes", support: support, plain: false)
        }
        code.close()
    }

    // The conversions of the unions and Flags of a type, including those in lists and alternatives.
    private static func conversions(_ code: inout Code, _ owner: String, _ fields: [GesModelPlanField], helper: String, support: String, plain: Bool) {
        var found: [GesModelPlanValue] = []
        for field in fields { unions(field.value, &found) }
        for union in found {
            let name = owner + "." + union.nested
            code.blank()
            if union.kind == .flags {
                let names = "\(owner)\(union.nested)Names"
                code.line("static let \(names) = [\(union.model.tags.map(literal).joined(separator: ", "))]")
                code.blank()
                code.line("static func value(_ value: \(name)) -> GesValue { \(support).flags(\(names), value.rawValue) }")
                code.blank()
                code.line("static func decode\(owner)\(union.nested)(_ value: GesValue) -> \(name)? { \(support).flags(value, \(names)).map(\(name).init(rawValue:)) }")
                continue
            }
            code.line("static func value(_ value: \(name)) -> GesValue {")
            code.line("    switch value {")
            for alternative in union.alternatives {
                code.line("    case .\(member(alternative.label))(let alternative): \(encode(alternative, owner, "alternative", helper, support, plain: plain))")
            }
            code.line("    }")
            code.line("}")
            code.blank()
            code.open("static func decode\(owner)\(union.nested)(_ value: GesValue) -> \(name)? {")
            for alternative in union.alternatives {
                code.line("if let alternative = \(decode(alternative, owner, "value", helper, support, plain: plain)) { return .\(member(alternative.label))(alternative) }")
            }
            code.line("return nil")
            code.close()
        }
    }

    // The plain values of the models: a Record of each schema with its fields in declared order, as the value path decodes the
    // bytes of a record, so that wire records read them and give them back.
    private static func valueConversions(_ code: inout Code, _ layout: GesModelLayout, _ name: String, _ support: String, _ computed: GesComputedEmitter) {
        code.blank()
        code.line("/// The plain GES values of the models of this file, as `GesSchemaValues` reads and writes them.")
        code.open("enum \(name) {")
        for (typeIndex, type) in layout.types.enumerated() {
            let fields = GesModelPlanner.fields(type)
            let names = type.name + "Fields"
            let typeLiteral = literal(type.name)
            let arguments = fields.enumerated().map { "\(GesSchemaPlanner.camel($0.element.base)): field\($0.offset)" }.joined(separator: ", ")
            if typeIndex > 0 { code.blank() }
            code.line("static let \(names) = [\(fields.map { literal($0.field.name) }.joined(separator: ", "))]")
            code.blank()
            // A value also carries the computed fields, as decoding the record's bytes gives them; reading ignores them.
            var keys = names
            if !type.computed.isEmpty {
                keys = type.name + "Keys"
                code.line("static let \(keys) = \(names) + [\(type.computed.map { literal($0.name) }.joined(separator: ", "))]")
                code.blank()
            }
            code.open("static func value(_ record: \(type.name)) -> GesValue {")
            code.line(".record(typeName: \(typeLiteral), keys: \(keys), values: [")
            for field in fields { code.line("    " + encode(field.value, type.name, "record." + member(field.base), name, support, plain: true) + ",") }
            for field in type.computed {
                code.line("    \(computed.helper).gesValue(\(computed.valueOf("record", type.name, field)), .\(field.kind == .quantity ? field.unit.rawValue : "none")),")
            }
            code.line("])")
            code.close()
            code.blank()
            code.open("static func read\(type.name)(_ value: GesValue, _ path: String) throws(GesWireError) -> \(type.name) {")
            if fields.isEmpty {
                code.line("guard recordFields(value, \(typeLiteral), \(names)) != nil else { throw failure(value, path) }")
                code.line("return \(type.name)()")
            } else {
                code.line("guard let fields = recordFields(value, \(typeLiteral), \(names)) else { throw failure(value, path) }")
                for index in GesModelPlanner.readOrder(fields) {
                    let field = fields[index]
                    let fieldLiteral = literal(field.field.name)
                    let held = "value\(index)"
                    let missing = field.value.nullable ? "fields.get(\(fieldLiteral)) ?? .nothing" : "try required(fields, \(fieldLiteral), path)"
                    code.line("let \(held) = \(self.held(fields, index, type.name, support, { "value\($0)" }, missing))")
                    if descends(field.value) {
                        code.line("let field\(index) = \(read(field.value, type.name, held, "fieldPath(path, \(fieldLiteral))", name, support))")
                    } else {
                        code.line("guard let field\(index) = \(decode(field.value, type.name, held, name, support, plain: true)) else { throw failure(\(held), fieldPath(path, \(fieldLiteral))) }")
                    }
                }
                construct(&code, type.name, arguments, fields)
            }
            code.close()
            code.blank()
            code.open("static func decode\(type.name)(_ value: GesValue) -> \(type.name)? {")
            if fields.isEmpty {
                code.line("guard recordFields(value, \(typeLiteral), \(names)) != nil else { return nil }")
            } else {
                code.open("guard")
                code.line("let fields = recordFields(value, \(typeLiteral), \(names)),")
                for (index, field) in fields.enumerated() {
                    code.line("let field\(index) = \(decode(field.value, type.name, inline(fields, index, type.name, support), name, support, plain: true))\(index == fields.count - 1 ? "" : ",")")
                }
                code.dedent()
                code.line("else { return nil }")
            }
            construct(&code, type.name, arguments, fields)
            code.close()
            conversions(&code, type.name, fields, helper: name, support: support, plain: true)
        }
        code.blank()
        code.line("// The fields of a Record of the schema, or of a Map whose keys are all fields of it.")
        code.open("static func recordFields(_ value: GesValue, _ name: String, _ names: [String]) -> GesValueMap? {")
        code.line("guard let fields = value.asMap else { return nil }")
        code.line("if let type = value.customTypeName { return type == name ? fields : nil }")
        code.line("return \(support).known(fields, names) ? fields : nil")
        code.close()
        code.blank()
        code.line("static func fieldPath(_ path: String, _ field: String) -> String { path.isEmpty ? field : path + \".\" + field }")
        code.blank()
        code.line("// A value that does not convert: nothing where a value is required, or a value of another type.")
        code.open("static func failure(_ value: GesValue, _ path: String) -> GesWireError {")
        code.line("GesWireError(code: value.isNothing ? GesWireError.nothingValue : GesWireError.typeMismatch, path: path)")
        code.close()
        code.blank()
        code.open("static func required(_ fields: GesValueMap, _ name: String, _ path: String) throws(GesWireError) -> GesValue {")
        code.line("guard let value = fields.get(name) else { throw GesWireError(code: GesWireError.missingField, path: fieldPath(path, name)) }")
        code.line("return value")
        code.close()
        code.blank()
        code.open("static func require<T>(_ converted: T?, _ value: GesValue, _ path: String) throws(GesWireError) -> T {")
        code.line("guard let converted else { throw failure(value, path) }")
        code.line("return converted")
        code.close()
        code.blank()
        code.line("// The elements of a List, each read at the list's path with `[]`.")
        code.open("static func readList<T>(_ value: GesValue, _ path: String, _ element: (GesValue, String) throws(GesWireError) -> T) throws(GesWireError) -> [T] {")
        code.line("guard let items = value.listValue else { throw failure(value, path) }")
        code.line("let item = path + \"[]\"")
        code.line("var result: [T] = []")
        code.line("result.reserveCapacity(items.count)")
        code.line("for value in items { result.append(try element(value, item)) }")
        code.line("return result")
        code.close()
        code.blank()
        code.open("static func readOptional<T>(_ value: GesValue, _ path: String, _ read: (GesValue, String) throws(GesWireError) -> T) throws(GesWireError) -> T? {")
        code.line("value.isNothing ? nil : try read(value, path)")
        code.close()
        code.close()
    }

    // The conversions that the script bindings and the plain values share, named after the header so that two generated
    // files can share a module.
    private static func supportType(_ code: inout Code, _ name: String) {
        code.blank()
        code.line("/// The GES value conversions of the models of this file.")
        code.open("enum \(name) {")
        helpers(&code)
        code.close()
    }

    private static func helpers(_ code: inout Code) {
        code.line("static func known(_ fields: GesValueMap, _ names: [String]) -> Bool { (0..<fields.length).allSatisfy { names.contains(fields.key(at: $0)) } }")
        code.blank()
        code.line("static func present<T>(_ value: T?, _ encode: (T) -> GesValue) -> GesValue { value.map(encode) ?? .nothing }")
        code.blank()
        code.line("static func optional<T>(_ value: GesValue, _ decode: (GesValue) -> T?) -> T?? { value.isNothing ? .some(nil) : decode(value).map { .some($0) } }")
        code.blank()
        code.open("static func flags(_ names: [String], _ mask: UInt64) -> GesValue {")
        code.line("let keys = names.indices.filter { mask >> UInt64($0) & 1 != 0 }.map { names[$0] }")
        code.line("return .map(keys: keys, values: keys.map { _ in .boolean(true) })")
        code.close()
        code.blank()
        code.open("static func flags(_ value: GesValue, _ names: [String]) -> UInt64? {")
        code.line("guard value.valueKind == .map, let fields = value.asMap else { return nil }")
        code.line("var mask: UInt64 = 0")
        code.open("for index in 0..<fields.length {")
        code.line("let set = fields.value(at: index)")
        code.line("guard let member = names.firstIndex(of: fields.key(at: index)), set.valueKind == .boolean else { return nil }")
        code.line("if set.asBoolean { mask |= 1 << UInt64(member) }")
        code.close()
        code.line("return mask")
        code.close()
        code.blank()
        code.line("static func tag(_ name: String) -> GesValue { (try? GesValue.tag(name)) ?? .nothing }")
        code.blank()
        code.line("static func tag(_ value: GesValue) -> String? { value.valueKind == .tag || value.valueKind == .text ? value.textValue : nil }")
        code.blank()
        code.line("static func text(_ value: GesValue) -> String? { value.valueKind == .text ? value.textValue : nil }")
        code.blank()
        code.line("static func bytes(_ value: GesValue) -> [UInt8]? { value.bytesValue }")
        code.blank()
        code.line("static func boolean(_ value: GesValue) -> Bool? { value.valueKind == .boolean ? value.asBoolean : nil }")
        code.blank()
        code.line("static func percentage(_ value: GesValue) -> Double? { value.valueKind == .percentage ? value.floatValue : nil }")
        code.blank()
        code.open("static func number(_ value: GesValue, _ unit: GesQuantityUnit) -> Double? {")
        code.line("guard value.unit == unit else { return nil }")
        code.line("if let integer = value.integerValue { return Double(exactly: integer) }")
        code.line("return value.valueKind == .float ? value.floatValue : nil")
        code.close()
        code.blank()
        code.open("static func integer(_ value: GesValue, _ unit: GesQuantityUnit) -> Int? {")
        code.line("guard value.unit == unit else { return nil }")
        code.line("if let integer = value.integerValue { return Int(exactly: integer) }")
        code.line("return value.valueKind == .float ? value.floatValue.flatMap { Int(exactly: $0) } : nil")
        code.close()
        code.blank()
        code.open("static func list<T>(_ value: GesValue, _ element: (GesValue) -> T?) -> [T]? {")
        code.line("guard let items = value.listValue else { return nil }")
        code.line("var result: [T] = []")
        code.line("result.reserveCapacity(items.count)")
        code.open("for item in items {")
        code.line("guard let decoded = element(item) else { return nil }")
        code.line("result.append(decoded)")
        code.close()
        code.line("return result")
        code.close()
        code.blank()
        code.line("static func dice(_ value: GesValue) -> [Int]? { value.diceRolls?.map { Int($0) } }")
        code.blank()
        code.line("static func dice(_ rolls: [Int]) -> GesValue { rolls.allSatisfy { $0 > 0 && $0 <= Int(Int32.max) } ? .dice(rolls.map { Int32($0) }) : .nothing }")
    }
}
