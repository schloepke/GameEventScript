// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

/// What a path reaches: the member text, the presence flags of the optional fields on it (wire records), whether the text
/// gives an optional value (models), a computed field it ends at, and what the field holds as declared.
final class GesComputedReach {
    var text = ""
    /// A presence flag per optional field on the path, with the index of that field.
    var conditions: [(step: Int, condition: String)] = []
    /// Whether the text gives an optional value: an optional field, or a field behind an optional one.
    var nullable = false
    /// The schema and name of a computed field at the end of the path, where `text` names the record that holds it.
    var computedOwner = ""
    var computedName = ""
    /// Whether the field holds an Int64, and the closed range it declares, unbounded without one.
    var integer = false
    var lower = -Double.infinity
    var upper = Double.infinity
    /// The largest element count of a list, unbounded without a max.
    var maxCount = Double.infinity

    init(text: String) { self.text = text }
}

/// How generated code reaches the fields that computed expressions read. Paths start at the record (an empty root, `self`
/// or `this`) or at a list element (`item`, whose owner is its schema, or empty for a number).
protocol GesComputedAccess {
    /// What a path from a record of the owner reaches.
    func reach(_ root: String, _ owner: String, _ path: [String]) -> GesComputedReach

    /// A list element that is a number, of the list at the path.
    func element(_ owner: String, _ path: [String]) -> GesComputedReach

    /// The schema of a list's elements, or empty for numbers.
    func elementOwner(_ owner: String, _ path: [String]) -> String

    /// The computed fields of a schema.
    func computed(_ owner: String) -> [GesSchemaComputedFieldInfo]
}

/// Translates the checked expressions of computed fields into C# or Swift. A field whose operands binding proves to give the
/// bits of the GES virtual machine in plain binary64 becomes one property in the language's own operators (static code); any
/// other field keeps the dynamic form, a property over a method that returns the exact GES value with the rules of the
/// virtual machine. The helpers of both forms live in one type per generation, `GesComputed`.
final class GesComputedEmitter {
    typealias Code = GesSchemaPlanner.Code

    let swift: Bool
    let helper: String
    let access: any GesComputedAccess
    var sums: [String] = []
    var owner = ""
    var field = ""
    var element = ""
    var facts: [ObjectIdentifier: GesComputedFacts] = [:]
    var fields: [String: GesComputedFacts] = [:]
    var analyzed: Set<String> = []

    init(swift: Bool, helper: String, access: any GesComputedAccess) {
        self.swift = swift
        self.helper = helper
        self.access = access
    }

    /// The helper type: `GesComputed` in a namespace, otherwise with the suffix of the generation, so that two generations
    /// without namespaces can share a module.
    static func helper(_ space: String, _ suffix: String) -> String { space.isEmpty ? "GesComputed" + suffix : "GesComputed" }

    /// The member names of a computed field: the property and, in the dynamic form, the method with the exact value.
    static func property(_ name: String, _ type: String, swift: Bool) -> String {
        var pascal = GesSchemaPlanner.pascal(name)
        if pascal == type { pascal += "Value" }
        return swift ? GesSchemaPlanner.camel(pascal) : pascal
    }

    static func method(_ name: String, _ type: String, swift: Bool) -> String { "__" + property(name, type, swift: swift) }

    /// The names a type's computed fields add, for the collision check of its members.
    static func names(_ computed: [GesSchemaComputedFieldInfo], _ type: String, swift: Bool, _ names: inout [(name: String, origin: String)]) {
        for field in computed {
            names.append((property(field.name, type, swift: swift), "the computed field " + field.name))
            names.append((method(field.name, type, swift: swift), "the value of the computed field " + field.name))
        }
    }

    /// The type of a computed field's property: binary64 for numbers and percentage ratios, optional when the field is.
    static func propertyType(_ field: GesSchemaComputedFieldInfo, swift: Bool) -> String {
        if field.kind == .boolean { return swift ? "Bool" : "bool" }
        return (swift ? "Double" : "double") + (field.isOptional ? "?" : "")
    }

    /// Whether any computed field of the types needs the exact values of the dynamic form.
    func needsValues(_ types: [(owner: String, computed: [GesSchemaComputedFieldInfo])]) -> Bool {
        for (owner, computed) in types {
            for field in computed where analyze(owner, field) == nil { return true }
        }
        return false
    }

    /// The exact GES value of a computed field of a record, which conversions to GES values read.
    func valueOf(_ record: String, _ owner: String, _ field: GesSchemaComputedFieldInfo) -> String {
        if analyze(owner, field) == nil { return "\(record).\(Self.method(field.name, owner, swift: swift))()" }
        let property = "\(record).\(member(Self.property(field.name, owner, swift: swift)))"
        return call(field.kind == .percentage ? "ReadPercentage" : "Read", property)
    }

    /// Writes the properties and helper methods of a type's computed fields.
    func members(_ code: inout Code, _ owner: String, _ computed: [GesSchemaComputedFieldInfo], _ access: String) {
        for field in computed {
            self.owner = owner
            self.field = Self.property(field.name, owner, swift: swift)
            element = ""
            sums = []
            code.blank()
            if let known = analyze(owner, field) {
                staticMembers(&code, field, known, access)
            } else {
                dynamicMembers(&code, owner, field, access)
            }
            for body in sums {
                code.blank()
                for line in body.split(separator: "\n", omittingEmptySubsequences: false) { code.line(String(line)) }
            }
        }
    }

    static func absent(_ field: GesSchemaComputedFieldInfo) -> String {
        field.kind == .boolean
            ? "false where the expression gives nothing, as a script record reads it"
            : field.isOptional ? "absent where the expression gives nothing" : "NaN where the expression gives nothing"
    }

    static func escape(_ text: String) -> String {
        replace(replace(replace(text, "&", "&amp;"), "<", "&lt;"), ">", "&gt;")
    }

    /// Replaces every occurrence of a text, without the newer standard library or Foundation.
    static func replace(_ text: String, _ target: String, _ replacement: String) -> String {
        let source = Array(text.unicodeScalars)
        let pattern = Array(target.unicodeScalars)
        var result = String.UnicodeScalarView()
        var index = 0
        while index < source.count {
            if index + pattern.count <= source.count && source[index..<(index + pattern.count)].elementsEqual(pattern) {
                result.append(contentsOf: replacement.unicodeScalars)
                index += pattern.count
            } else {
                result.append(source[index])
                index += 1
            }
        }
        return String(result)
    }

    func summary(_ code: inout Code, _ field: GesSchemaComputedFieldInfo) {
        code.line(
            swift
                ? "/// `\(field.name)` computed by `\(field.spelling)`; \(Self.absent(field))."
                : "/// <summary><c>\(Self.escape(field.name))</c> computed by <c>\(Self.escape(field.spelling))</c>; \(Self.absent(field)).</summary>"
        )
    }

    func camel(_ name: String) -> String { swift ? GesSchemaPlanner.camel(name) : name }

    func call(_ name: String, _ arguments: String...) -> String { "\(helper).\(camel(name))(\(arguments.joined(separator: ", ")))" }

    /// A member name as the language spells it, with Swift keywords escaped.
    func member(_ name: String) -> String { swift && GesSwiftEmitter.keywords.contains(name) ? "`" + name + "`" : name }

    /// A read of a path from a record (empty root for the record itself in a property) or from a list element.
    func root(_ node: GesComputedNode, _ root: String) -> String { node.op == .item ? "item" : root }

    func owner(_ node: GesComputedNode) -> String { node.op == .item ? element : owner }

    static func join(_ root: String, _ member: String) -> String { root.isEmpty ? member : root + "." + member }

    func target(_ reach: GesComputedReach) -> GesSchemaComputedFieldInfo {
        access.computed(reach.computedOwner).first { $0.name == reach.computedName }!
    }
}
