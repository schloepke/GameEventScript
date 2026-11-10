// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Writes C# 9 models for Unity: a sealed class per schema with value equality, settable properties in declared order, a
// constructor that sets every field and one that leaves every field at its zero value, an enum per closed Tag, an abstract
// class with one sealed subclass per alternative for a union, and the runtime's GesPoint and GesVector for spatial fields.
// Classes avoid struct copies that would share a list; no `record`, `init` accessor or Reflection is used.
enum GesCSharpModelEmitter {
    typealias Code = GesSchemaPlanner.Code
    typealias Names = [(name: String, origin: String)]

    // Members every model type declares or inherits.
    private static let reserved = ["Equals", "GetHashCode", "ToString", "GetType"]

    static func emit(_ layout: GesModelLayout, _ options: GesSchemaGenerateOptions) throws(GesGeneratorError) -> String {
        let access = options.access == .public ? "public" : "internal"
        let space = try GesSchemaPlanner.namespace(options.namespace, GesCSharpEmitter.keywords)
        var code = Code()
        code.raw(GesModelPlanner.header(layout))
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
        let support = "__GesModels" + GesSchemaPlanner.hash(GesModelPlanner.header(layout))
        let qualifier = "global::" + space.map { $0 + "." }.joined()
        for type in layout.types {
            try record(&code, type, access, support, qualifier)
            code.blank()
        }
        supportType(&code, support)
        if !space.isEmpty { code.close() }
        return code.text
    }

    private static func parameter(_ pascal: String) -> String {
        let name = GesSchemaPlanner.camel(pascal)
        return GesCSharpEmitter.keywords.contains(name) ? "@" + name : name
    }

    private static func record(_ code: inout Code, _ type: GesModelType, _ access: String, _ support: String, _ qualifier: String) throws(GesGeneratorError) {
        let fields = GesModelPlanner.fields(type)
        var names: Names = reserved.map { ($0, "the inherited member " + $0) }
        for field in fields {
            names.append((field.base, "the field " + field.field.name))
            nested(field.value, &names, field.field.name)
        }
        try GesSchemaPlanner.unique(type.name, names)

        code.line("/// <summary>The model of the schema <c>:\(type.name)</c>: a plain value that is never encoded and compares by value.</summary>")
        code.line("\(access) sealed class \(type.name) : IEquatable<\(type.name)>")
        code.open("{")
        code.line("/// <summary>Creates a record with every field at its zero value: absent, empty or the first alternative.</summary>")
        code.line("public \(type.name)()")
        code.open("{")
        code.close()
        if !fields.isEmpty {
            code.blank()
            code.line("/// <summary>Creates a record from its fields.</summary>")
            for field in fields { code.line("/// <param name=\"\(GesSchemaPlanner.camel(field.base))\">The field <c>\(field.field.name)</c>.</param>") }
            code.line("public \(type.name)(\(fields.map { "\(self.type($0.value)) \(parameter($0.base))" }.joined(separator: ", ")))")
            code.open("{")
            for field in fields { code.line("\(field.base) = \(parameter(field.base));") }
            code.close()
        }
        for field in fields {
            code.blank()
            code.line("/// <summary><c>\(escape(field.field.name + ": " + GesModelPlanner.describe(field.field)))</c></summary>")
            let initial = self.initial(field.value)
            code.line("public \(self.type(field.value)) \(field.base) { get; set; }\(initial.isEmpty ? "" : " = " + initial + ";")")
        }
        code.blank()
        code.line("/// <summary>Compares every field by value.</summary>")
        code.line("/// <param name=\"other\">The other record.</param>")
        code.line("/// <returns>Whether both hold equal values.</returns>")
        code.line("public bool Equals(\(type.name)? other)")
        code.open("{")
        code.line("if (other is null) return false;")
        code.line("if (ReferenceEquals(this, other)) return true;")
        if fields.isEmpty { code.line("return true;") }
        for (index, field) in fields.enumerated() {
            let test = equal(field.value, field.base, "other." + field.base, support)
            let end = index == fields.count - 1 ? ";" : ""
            code.line(index == 0 ? "return " + test + end : "    && " + test + end)
        }
        code.close()
        code.blank()
        code.line("/// <inheritdoc />")
        code.line("public override bool Equals(object? obj) => obj is \(type.name) other && Equals(other);")
        code.blank()
        code.line("/// <inheritdoc />")
        code.line("public override int GetHashCode()")
        code.open("{")
        code.line("unchecked")
        code.open("{")
        code.line("var hash = 17;")
        for field in fields { code.line("hash = (hash * 31) + \(hash(field.value, field.base, support));") }
        code.line("return hash;")
        code.close()
        code.close()
        code.blank()
        operators(&code, type.name, "record")
        for field in fields { try declarations(&code, field.value, support, qualifier) }
        code.close()
    }

    private static func operators(_ code: inout Code, _ name: String, _ what: String) {
        code.line("/// <summary>Compares two \(what)s by value.</summary>")
        code.line("/// <param name=\"left\">The first value.</param>")
        code.line("/// <param name=\"right\">The second value.</param>")
        code.line("/// <returns>Whether both are null or equal.</returns>")
        code.line("public static bool operator ==(\(name)? left, \(name)? right) => left is null ? right is null : left.Equals(right);")
        code.blank()
        code.line("/// <summary>Compares two \(what)s by value.</summary>")
        code.line("/// <param name=\"left\">The first value.</param>")
        code.line("/// <param name=\"right\">The second value.</param>")
        code.line("/// <returns>Whether they differ.</returns>")
        code.line("public static bool operator !=(\(name)? left, \(name)? right) => !(left == right);")
    }

    private static func nested(_ value: GesModelPlanValue, _ names: inout Names, _ field: String) {
        if !value.nested.isEmpty { names.append((value.nested, "a type of the field " + field)) }
        if let element = value.element { nested(element, &names, field) }
        for alternative in value.alternatives { nested(alternative, &names, field) }
    }

    private static func escape(_ text: String) -> String {
        var result = ""
        for character in text {
            switch character {
            case "&": result += "&amp;"
            case "<": result += "&lt;"
            case ">": result += "&gt;"
            default: result.append(character)
            }
        }
        return result
    }

    // A union subclass named after a schema hides that schema's type, so its members name schema types with `global::`.
    static func type(_ value: GesModelPlanValue, _ qualifier: String = "") -> String { plain(value, qualifier) + (value.nullable ? "?" : "") }

    private static func plain(_ value: GesModelPlanValue, _ qualifier: String = "") -> String {
        switch value.kind {
        case .boolean: "bool"
        case .number: value.model.integer ? "long" : "double"
        case .tag: value.closedTag ? value.nested : "string"
        case .text: "string"
        case .list: "List<\(type(value.element!, qualifier))>"
        case .dice: "List<int>"
        case .point: "GesPoint"
        case .vector: "GesVector"
        case .schema: qualifier + value.model.schema
        case .union, .nothing: value.nested
        }
    }

    private static func isReference(_ value: GesModelPlanValue) -> Bool {
        switch value.kind {
        case .text, .list, .dice, .schema, .union: true
        case .tag: !value.closedTag
        default: false
        }
    }

    // The zero value of a property that the type's default would not give: empty texts and lists, new records, the first
    // alternative of a union.
    private static func initial(_ value: GesModelPlanValue) -> String {
        if value.nullable { return "" }
        switch value.kind {
        case .text: return "\"\""
        case .tag where !value.closedTag: return "\"\""
        case .list, .dice: return "new \(plain(value))()"
        case .schema: return "new \(value.model.schema)()"
        case .union: return "new \(value.nested).\(value.alternatives[0].label)(\(zero(value.alternatives[0])))"
        default: return ""
        }
    }

    private static func zero(_ value: GesModelPlanValue) -> String {
        if value.nullable { return "null" }
        switch value.kind {
        case .boolean: return "false"
        case .number: return value.model.integer ? "0L" : "0d"
        case .tag where value.closedTag: return "default(\(value.nested))"
        case .point, .vector: return "default(\(plain(value)))"
        default: return initial(value)
        }
    }

    // Nested lists compare and hash through lambdas whose parameters are numbered by depth: x0 and y0, then x1 and y1.
    private static func equal(_ value: GesModelPlanValue, _ left: String, _ right: String, _ support: String, _ depth: Int = 0, _ qualifier: String = "") -> String {
        if value.kind == .list || value.kind == .dice { return "\(support).ListEquals(\(left), \(right), \(comparer(value, support, depth, qualifier)))" }
        return "EqualityComparer<\(type(value, qualifier))>.Default.Equals(\(left), \(right))"
    }

    private static func comparer(_ list: GesModelPlanValue, _ support: String, _ depth: Int, _ qualifier: String) -> String {
        let (x, y) = ("x\(depth)", "y\(depth)")
        if list.kind == .dice { return "(\(x), \(y)) => \(x) == \(y)" }
        return "(\(x), \(y)) => \(equal(list.element!, x, y, support, depth + 1, qualifier))"
    }

    private static func hash(_ value: GesModelPlanValue, _ expression: String, _ support: String, _ depth: Int = 0) -> String {
        if value.kind == .list || value.kind == .dice { return "\(support).ListHash(\(expression), \(elementHash(value, support, depth)))" }
        if value.kind == .number && !value.model.integer { return "\(support).Hash(\(expression))" }
        if isReference(value) || value.nullable { return "(\(expression)?.GetHashCode() ?? 0)" }
        return "\(expression).GetHashCode()"
    }

    private static func elementHash(_ list: GesModelPlanValue, _ support: String, _ depth: Int) -> String {
        let x = "x\(depth)"
        return list.kind == .dice ? "\(x) => \(x)" : "\(x) => " + hash(list.element!, x, support, depth + 1)
    }

    private static func declarations(_ code: inout Code, _ value: GesModelPlanValue, _ support: String, _ qualifier: String) throws(GesGeneratorError) {
        if value.closedTag {
            try GesSchemaPlanner.unique(value.nested, value.model.tags.map { (GesSchemaPlanner.pascal($0), "the tag #" + $0) })
            code.blank()
            code.line("/// <summary>The tags, in declared order.</summary>")
            code.line("public enum \(value.nested)")
            code.open("{")
            for tag in value.model.tags {
                code.line("/// <summary>The tag <c>#\(tag)</c>.</summary>")
                code.line(GesSchemaPlanner.pascal(tag) + ",")
            }
            code.close()
        } else if value.kind == .union {
            try union(&code, value, support, qualifier)
        } else if let element = value.element {
            try declarations(&code, element, support, qualifier)
        }
    }

    private static func union(_ code: inout Code, _ value: GesModelPlanValue, _ support: String, _ qualifier: String) throws(GesGeneratorError) {
        var names: Names = reserved.map { ($0, "the inherited member " + $0) }
        for alternative in value.alternatives { names.append((alternative.label, "the alternative " + alternative.label)) }
        try GesSchemaPlanner.unique(value.nested, names)
        code.blank()
        code.line("/// <summary>The alternatives, one sealed subclass each, in declared order.</summary>")
        code.line("public abstract class \(value.nested) : IEquatable<\(value.nested)>")
        code.open("{")
        code.line("private \(value.nested)()")
        code.open("{")
        code.close()
        code.blank()
        code.line("/// <summary>Compares the alternative and its value.</summary>")
        code.line("/// <param name=\"other\">The other value.</param>")
        code.line("/// <returns>Whether both are the same alternative with equal values.</returns>")
        code.line("public abstract bool Equals(\(value.nested)? other);")
        code.blank()
        code.line("/// <inheritdoc />")
        code.line("public override bool Equals(object? obj) => obj is \(value.nested) other && Equals(other);")
        code.blank()
        code.line("/// <inheritdoc />")
        code.line("public abstract override int GetHashCode();")
        code.blank()
        operators(&code, value.nested, "value")
        for (index, alternative) in value.alternatives.enumerated() {
            let type = self.type(alternative, qualifier)
            code.blank()
            code.line("/// <summary>The alternative <c>:\(alternative.label)</c>.</summary>")
            code.line("public sealed class \(alternative.label) : \(value.nested)")
            code.open("{")
            code.line("/// <summary>Creates the alternative.</summary>")
            code.line("/// <param name=\"value\">Its value.</param>")
            code.line("public \(alternative.label)(\(type) value)")
            code.open("{")
            code.line("Value = value;")
            code.close()
            code.blank()
            code.line("/// <summary>Gets or sets the value.</summary>")
            code.line("public \(type) Value { get; set; }")
            code.blank()
            code.line("/// <inheritdoc />")
            code.line("public override bool Equals(\(value.nested)? other) => other is \(alternative.label) alternative && \(equal(alternative, "Value", "alternative.Value", support, 0, qualifier));")
            code.blank()
            code.line("/// <inheritdoc />")
            code.line("public override int GetHashCode() => unchecked((\(index) * 397) ^ \(hash(alternative, "Value", support)));")
            code.close()
        }
        code.close()
        for alternative in value.alternatives { try declarations(&code, alternative, support, qualifier) }
    }

    // Equality and hashing helpers of the file, named after its header so that two generated files can share a namespace.
    private static func supportType(_ code: inout Code, _ name: String) {
        code.line("/// <summary>Value equality of lists and numbers for the models of this file.</summary>")
        code.line("internal static class \(name)")
        code.open("{")
        code.line("/// <summary>Hashes a number so that 0 and -0, which compare equal, hash alike.</summary>")
        code.line("internal static int Hash(double value) => value == 0 ? 0 : value.GetHashCode();")
        code.blank()
        code.line("/// <summary>Hashes an optional number.</summary>")
        code.line("internal static int Hash(double? value) => value is { } present ? Hash(present) : 0;")
        code.blank()
        code.line("/// <summary>Compares two lists element by element.</summary>")
        code.line("internal static bool ListEquals<T>(List<T>? left, List<T>? right, Func<T, T, bool> equals)")
        code.open("{")
        code.line("if (ReferenceEquals(left, right)) return true;")
        code.line("if (left is null || right is null || left.Count != right.Count) return false;")
        code.line("for (var index = 0; index < left.Count; index++)")
        code.line("    if (!equals(left[index], right[index])) return false;")
        code.line("return true;")
        code.close()
        code.blank()
        code.line("/// <summary>Hashes a list from its elements.</summary>")
        code.line("internal static int ListHash<T>(List<T>? list, Func<T, int> hash)")
        code.open("{")
        code.line("if (list is null) return 0;")
        code.line("unchecked")
        code.open("{")
        code.line("var result = 19;")
        code.line("foreach (var item in list) result = (result * 31) + hash(item);")
        code.line("return result;")
        code.close()
        code.close()
        code.close()
    }
}
