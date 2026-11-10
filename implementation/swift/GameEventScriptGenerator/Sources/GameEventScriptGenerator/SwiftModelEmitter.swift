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
            try record(&code, type, access)
        }
        for _ in space { code.close() }
        return code.text
    }

    private static func name(_ name: String) -> String { GesSwiftEmitter.name(name) }

    private static func member(_ pascal: String) -> String { GesSwiftEmitter.member(pascal) }

    private static func record(_ code: inout Code, _ type: GesModelType, _ access: String) throws(GesGeneratorError) {
        let fields = GesModelPlanner.fields(type)
        let key = fields.first { $0.field.key }
        let identity = key.map { GesSchemaPlanner.camel($0.base) != "id" } ?? false
        var names: Names = []
        for field in fields {
            names.append((GesSchemaPlanner.camel(field.base), "the field " + field.field.name))
            nested(field.value, &names, field.field.name)
        }
        if let key, identity { names.append(("id", "the identity of the key " + key.field.name)) }
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
        for field in fields { try declarations(&code, field.value, access) }
        code.close()
    }

    private static func nested(_ value: GesModelPlanValue, _ names: inout Names, _ field: String) {
        if !value.nested.isEmpty { names.append((value.nested, "a type of the field " + field)) }
        if let element = value.element { nested(element, &names, field) }
        for alternative in value.alternatives { nested(alternative, &names, field) }
    }

    private static func defaultValue(_ value: GesModelPlanValue) -> String {
        value.nullable ? " = nil" : value.kind == .list || value.kind == .dice ? " = []" : ""
    }

    static func type(_ value: GesModelPlanValue) -> String { plain(value) + (value.nullable ? "?" : "") }

    private static func plain(_ value: GesModelPlanValue) -> String {
        switch value.kind {
        case .boolean: "Bool"
        case .number: value.model.integer ? "Int" : "Double"
        case .tag: value.closedTag ? value.nested : "String"
        case .text: "String"
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
}
