// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Writes Swift models: a Hashable, Sendable and Codable struct per schema with stored properties in declared order and a
// memberwise initializer, an enum per closed Tag and per union, and the runtime's GesPoint and GesVector for spatial fields.
enum GesSwiftModelEmitter {
    typealias Code = GesSchemaPlanner.Code
    typealias Names = [(name: String, origin: String)]

    static func emit(_ layout: GesModelLayout, _ options: GesSchemaGenerateOptions) throws(GesGeneratorError) -> String {
        let access = options.access == .public ? "public " : ""
        let space = try GesSchemaPlanner.namespace(options.namespace, GesSwiftEmitter.keywords)
        var code = Code()
        code.raw(GesModelPlanner.header(layout))
        code.line("// swift-format-ignore-file")
        code.blank()
        code.line("import GameEventScriptRuntime")
        code.blank()
        for part in space {
            code.line("/// The namespace `\(part)` of generated model types.")
            code.open("\(access)enum \(part) {")
        }
        for (index, type) in layout.types.enumerated() {
            if index > 0 { code.blank() }
            try record(&code, type, access, bindings: options.bindings)
        }
        if options.bindings { bindings(&code, layout, access) }
        for _ in space { code.close() }
        return code.text
    }

    private static func name(_ name: String) -> String { GesSwiftEmitter.name(name) }

    private static func member(_ pascal: String) -> String { GesSwiftEmitter.member(pascal) }

    private static func record(_ code: inout Code, _ type: GesModelType, _ access: String, bindings: Bool) throws(GesGeneratorError) {
        let fields = GesModelPlanner.fields(type)
        let key = fields.first { $0.field.key }
        let identity = key.map { GesSchemaPlanner.camel($0.base) != "id" } ?? false
        var names: Names = []
        for field in fields {
            names.append((GesSchemaPlanner.camel(field.base), "the field " + field.field.name))
            nested(field.value, &names, field.field.name)
        }
        if let key, identity { names.append(("id", "the identity of the key " + key.field.name)) }
        if bindings { names.append(("gesValue", "the generated member gesValue")) }
        if bindings && fields.contains(where: { $0.field.name == "fields" }) {
            throw GesGeneratorError(GesGeneratorError.nameCollision, "In '\(type.name)', the field fields and the generated field fields of its bindings share a name.")
        }
        try GesSchemaPlanner.unique(type.name, names)

        code.line("/// The model of the schema `:\(type.name)`: a plain value that is never encoded.")
        code.open("\(access)struct \(type.name): Hashable, Sendable, Codable\(key == nil ? "" : ", Identifiable") {")
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
        code.blank()
        code.line("/// Creates a record from its fields; an absent value defaults to nil and a list to empty.")
        let parameters = fields.map { "\(member($0.base)): \(self.type($0.value))\(defaultValue($0.value))" }
        code.open("\(access)init(\(parameters.joined(separator: ", "))) {")
        for field in fields { code.line("self.\(member(field.base)) = \(member(field.base))") }
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
        for field in fields { try declarations(&code, field.value, access) }
        code.close()
    }

    private static func nested(_ value: GesModelPlanValue, _ names: inout Names, _ field: String) {
        if !value.nested.isEmpty { names.append((value.nested, "a type of the field " + field)) }
        if let element = value.element { nested(element, &names, field) }
        for alternative in value.alternatives { nested(alternative, &names, field) }
    }

    private static func defaultValue(_ value: GesModelPlanValue) -> String {
        value.nullable ? " = nil" : value.kind == .list || value.kind == .dice || value.kind == .bytes ? " = []" : ""
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
        case .schema: value.model.schema
        case .union, .nothing: value.nested
        }
    }

    private static func declarations(_ code: inout Code, _ value: GesModelPlanValue, _ access: String) throws(GesGeneratorError) {
        if value.closedTag {
            code.blank()
            code.line("/// The tags, in declared order; the raw value is the tag's name.")
            code.open("\(access)enum \(value.nested): String, Hashable, Sendable, Codable, CaseIterable {")
            for tag in value.model.tags {
                code.line("/// The tag `#\(tag)`.")
                code.line("case \(name(tag))")
            }
            code.close()
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

    // Converts a native value to a GES value.
    private static func encode(_ value: GesModelPlanValue, _ owner: String, _ expression: String, nullable: Bool = true) -> String {
        if nullable && value.nullable { return "GesTypes.present(\(expression), { \(encode(value, owner, "$0", nullable: false)) })" }
        switch value.kind {
        case .boolean: return ".boolean(\(expression))"
        case .number where value.model.type == "Percentage": return ".percentage(\(expression))"
        case .number where value.model.integer: return ".integer(Int64(\(expression)), unit: \(unit(value.model.unit)))"
        case .number: return ".float(\(expression), unit: \(unit(value.model.unit)))"
        case .text: return ".text(\(expression))"
        case .bytes: return ".bytes(\(expression))"
        case .tag where value.closedTag: return "GesTypes.tag(\(expression).rawValue)"
        case .tag: return "GesTypes.tag(\(expression))"
        case .list: return ".list(\(expression).map { \(encode(value.element!, owner, "$0")) })"
        case .dice: return "GesTypes.dice(\(expression))"
        case .point, .vector: return "\(expression).value(unit: \(unit(value.model.unit)))"
        case .schema: return "\(expression).gesValue()"
        case .union, .nothing: return "GesTypes.value(\(expression))"
        }
    }

    // Converts a GES value to a native value, or nil when it does not convert.
    private static func decode(_ value: GesModelPlanValue, _ owner: String, _ expression: String, nullable: Bool = true) -> String {
        if nullable && value.nullable { return "GesTypes.optional(\(expression), { \(decode(value, owner, "$0", nullable: false)) })" }
        switch value.kind {
        case .boolean: return "GesTypes.boolean(\(expression))"
        case .number where value.model.type == "Percentage": return "GesTypes.percentage(\(expression))"
        case .number where value.model.integer: return "GesTypes.integer(\(expression), \(unit(value.model.unit)))"
        case .number: return "GesTypes.number(\(expression), \(unit(value.model.unit)))"
        case .text: return "GesTypes.text(\(expression))"
        case .bytes: return "GesTypes.bytes(\(expression))"
        case .tag where value.closedTag: return "GesTypes.tag(\(expression)).flatMap(\(owner).\(value.nested).init(rawValue:))"
        case .tag: return "GesTypes.tag(\(expression))"
        case .list: return "GesTypes.list(\(expression), { \(decode(value.element!, owner, "$0")) })"
        case .dice: return "GesTypes.dice(\(expression))"
        case .point: return "GesPoint(\(expression), unit: \(unit(value.model.unit)))"
        case .vector: return "GesVector(\(expression), unit: \(unit(value.model.unit)))"
        case .schema: return "\(value.model.schema)(gesValue: \(expression))"
        case .union, .nothing: return "GesTypes.decode\(owner)\(value.nested)(\(expression))"
        }
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
        if value.kind == .union { found.append(value) }
        if let element = value.element { unions(element, &found) }
        for alternative in value.alternatives { unions(alternative, &found) }
    }

    // The catalog and constructor registry of the models, with an external value and a constructor per type.
    private static func bindings(_ code: inout Code, _ layout: GesModelLayout, _ access: String) {
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
            code.line("    fields: [\(fields.map { definition($0.field.name, $0.value) }.joined(separator: ", "))\(fields.isEmpty ? "" : ", ")try! .init(name: \"fields\", kind: .map)],")
            code.line("    constructors: [try! .init(typeName: \(literal(type.name)), parameters: [try! .init(name: \"fields\", kind: .map)])]")
            code.line(")")
            code.line("let record: \(type.name)")
            code.blank()
            code.line("init(_ record: \(type.name)) { self.record = record }")
            code.blank()
            code.line("var definition: GesExternalTypeDefinition { Self.definition }")
            code.blank()
            code.open("func field(_ name: String) throws -> GesValue? {")
            code.line("switch name {")
            for field in fields { code.line("case \(literal(field.field.name)): \(encode(field.value, type.name, "record." + member(field.base)))") }
            code.line("case \"fields\": .map(keys: [\(names)], values: [\(fields.map { "try field(\(literal($0.field.name)))!" }.joined(separator: ", "))])")
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
            code.line("let fields = call.arguments[0].asMap, GesTypes.known(fields, [\(names)])\(fields.isEmpty ? "" : ",")")
            for (index, field) in fields.enumerated() {
                code.line("let \(member(field.base)) = \(decode(field.value, type.name, "fields.get(\(literal(field.field.name))) ?? .nothing"))\(index == fields.count - 1 ? "" : ",")")
            }
            code.dedent()
            code.open("else {")
            code.line("call.setNothing()")
            code.line("return")
            code.close()
            code.line("try call.setExternalValue(\(type.name)Value(\(type.name)(\(fields.map { "\(member($0.base)): \(member($0.base))" }.joined(separator: ", ")))))")
            code.close()
            code.close()
            var found: [GesModelPlanValue] = []
            for field in fields { unions(field.value, &found) }
            for union in found {
                let name = type.name + "." + union.nested
                code.blank()
                code.line("static func value(_ value: \(name)) -> GesValue {")
                code.line("    switch value {")
                for alternative in union.alternatives { code.line("    case .\(member(alternative.label))(let alternative): \(encode(alternative, type.name, "alternative"))") }
                code.line("    }")
                code.line("}")
                code.blank()
                code.open("static func decode\(type.name)\(union.nested)(_ value: GesValue) -> \(name)? {")
                for alternative in union.alternatives {
                    code.line("if let alternative = \(decode(alternative, type.name, "value")) { return .\(member(alternative.label))(alternative) }")
                }
                code.line("return nil")
                code.close()
            }
        }
        helpers(&code)
        code.close()
    }

    private static func helpers(_ code: inout Code) {
        code.blank()
        code.line("static func known(_ fields: GesValueMap, _ names: Set<String>) -> Bool { (0..<fields.length).allSatisfy { names.contains(fields.key(at: $0)) } }")
        code.blank()
        code.line("static func present<T>(_ value: T?, _ encode: (T) -> GesValue) -> GesValue { value.map(encode) ?? .nothing }")
        code.blank()
        code.line("static func optional<T>(_ value: GesValue, _ decode: (GesValue) -> T?) -> T?? { value.isNothing ? .some(nil) : decode(value).map { .some($0) } }")
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
