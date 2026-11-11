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
        if options.bindings || options.values { code.line("using GameEventScript.Runtime.Values;") }
        code.blank()
        if !space.isEmpty {
            code.line("namespace " + space.joined(separator: "."))
            code.open("{")
        }
        let support = "__GesModels" + GesSchemaPlanner.hash(GesModelPlanner.header(layout))
        let values = "__GesModelValues" + GesSchemaPlanner.hash(GesModelPlanner.header(layout))
        let qualifier = "global::" + space.map { $0 + "." }.joined()
        for type in layout.types {
            try record(&code, type, access, support, qualifier, bindings: options.bindings, values: options.values ? values : nil)
            code.blank()
        }
        if options.bindings {
            bindings(&code, layout, access, qualifier, support)
            code.blank()
        }
        if options.values {
            valueConversions(&code, layout, values, qualifier, support)
            code.blank()
        }
        supportType(&code, support, conversions: options.bindings || options.values)
        if !space.isEmpty { code.close() }
        return code.text
    }

    private static func parameter(_ pascal: String) -> String {
        let name = GesSchemaPlanner.camel(pascal)
        return GesCSharpEmitter.keywords.contains(name) ? "@" + name : name
    }

    private static func record(_ code: inout Code, _ type: GesModelType, _ access: String, _ support: String, _ qualifier: String, bindings: Bool, values: String?)
        throws(GesGeneratorError)
    {
        let fields = GesModelPlanner.fields(type)
        var names: Names = reserved.map { ($0, "the inherited member " + $0) }
        if bindings { names += ["ToGesValue", "FromGesValue"].map { ($0, "the generated member " + $0) } }
        if values != nil { names += ["ToValue", "FromValue"].map { ($0, "the generated member " + $0) } }
        if bindings && fields.contains(where: { $0.field.name == "fields" }) {
            throw GesGeneratorError(GesGeneratorError.nameCollision, "In '\(type.name)', the field fields and the generated field fields of its bindings share a name.")
        }
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
        if bindings {
            code.blank()
            code.line("/// <summary>Wraps the record as the external value <c>:\(type.name)</c> of <see cref=\"GesTypes\"/>; scripts read its fields when they read them.</summary>")
            code.line("/// <returns>The GES value.</returns>")
            code.line("public GesValue ToGesValue() => GesValue.GesExternal(new GesTypes.\(type.name)Value(this));")
            code.blank()
            code.line("/// <summary>Reads a <c>:\(type.name)</c> that a script holds.</summary>")
            code.line("/// <param name=\"value\">The value.</param>")
            code.line("/// <returns>The record, or null for another value.</returns>")
            code.line("public static \(type.name)? FromGesValue(in GesValue value) => value.ExternalValue is GesTypes.\(type.name)Value held ? held.Record : null;")
        }
        if let values {
            code.blank()
            code.line("/// <summary>Converts the record into a plain GES Record of its fields, as <see cref=\"GesSchemaValues\"/> decodes the bytes of the record.</summary>")
            code.line("/// <returns>The Record.</returns>")
            code.line("public GesValue ToValue() => \(values).Value(this);")
            code.blank()
            code.line("/// <summary>Reads a Record of this schema, or a Map of only its fields, as <see cref=\"GesSchemaValues\"/> encodes it.</summary>")
            code.line("/// <param name=\"value\">The value.</param>")
            code.line("/// <returns>The record.</returns>")
            code.line("/// <exception cref=\"GesWireException\">A field does not convert: <c>wire.typeMismatch</c> at its path.</exception>")
            code.line("public static \(type.name) FromValue(GesValue value) => \(values).Read\(type.name)(value, \"\");")
        }
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
    // Outside its owner, as in the generated type set, a nested type also names the owner.
    static func type(_ value: GesModelPlanValue, _ qualifier: String = "", _ owner: String? = nil) -> String { plain(value, qualifier, owner) + (value.nullable ? "?" : "") }

    private static func nested(_ value: GesModelPlanValue, _ qualifier: String, _ owner: String?) -> String { owner.map { qualifier + $0 + "." + value.nested } ?? value.nested }

    private static func plain(_ value: GesModelPlanValue, _ qualifier: String = "", _ owner: String? = nil) -> String {
        switch value.kind {
        case .boolean: "bool"
        case .number: value.model.integer ? "long" : "double"
        case .tag: value.closedTag ? nested(value, qualifier, owner) : "string"
        case .text: "string"
        case .bytes: "byte[]"
        case .list: "List<\(type(value.element!, qualifier, owner))>"
        case .dice: "List<int>"
        case .point: "GesPoint"
        case .vector: "GesVector"
        case .schema: qualifier + value.model.schema
        case .union, .nothing, .flags: nested(value, qualifier, owner)
        }
    }

    private static func isReference(_ value: GesModelPlanValue) -> Bool {
        switch value.kind {
        case .text, .bytes, .list, .dice, .schema, .union: true
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
        case .bytes: return "Array.Empty<byte>()"
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
        case .flags: return "default(\(value.nested))"
        case .point, .vector: return "default(\(plain(value)))"
        default: return initial(value)
        }
    }

    // Nested lists compare and hash through lambdas whose parameters are numbered by depth: x0 and y0, then x1 and y1.
    private static func equal(_ value: GesModelPlanValue, _ left: String, _ right: String, _ support: String, _ depth: Int = 0, _ qualifier: String = "") -> String {
        if value.kind == .list || value.kind == .dice { return "\(support).ListEquals(\(left), \(right), \(comparer(value, support, depth, qualifier)))" }
        if value.kind == .bytes { return "\(support).BytesEquals(\(left), \(right))" }
        return "EqualityComparer<\(type(value, qualifier))>.Default.Equals(\(left), \(right))"
    }

    private static func comparer(_ list: GesModelPlanValue, _ support: String, _ depth: Int, _ qualifier: String) -> String {
        let (x, y) = ("x\(depth)", "y\(depth)")
        if list.kind == .dice { return "(\(x), \(y)) => \(x) == \(y)" }
        return "(\(x), \(y)) => \(equal(list.element!, x, y, support, depth + 1, qualifier))"
    }

    private static func hash(_ value: GesModelPlanValue, _ expression: String, _ support: String, _ depth: Int = 0) -> String {
        if value.kind == .list || value.kind == .dice { return "\(support).ListHash(\(expression), \(elementHash(value, support, depth)))" }
        if value.kind == .bytes { return "\(support).BytesHash(\(expression))" }
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
        } else if value.kind == .flags {
            try GesSchemaPlanner.unique(value.nested, value.model.tags.map { (GesSchemaPlanner.pascal($0), "the member #" + $0) })
            GesCSharpEmitter.flagsDeclaration(&code, value.nested, value.model.tags)
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

    // Equality and hashing helpers of the file, named after its header so that two generated files can share a namespace, and
    // the conversions that the script bindings and the plain values share.
    private static func supportType(_ code: inout Code, _ name: String, conversions: Bool) {
        code.line("/// <summary>Value equality of lists and numbers\(conversions ? " and GES value conversions" : "") for the models of this file.</summary>")
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
        code.blank()
        code.line("/// <summary>Compares two byte arrays by their bytes.</summary>")
        code.line("internal static bool BytesEquals(byte[]? left, byte[]? right)")
        code.line("    => ReferenceEquals(left, right) || left is not null && right is not null && new ReadOnlySpan<byte>(left).SequenceEqual(right);")
        code.blank()
        code.line("/// <summary>Hashes a byte array from its bytes.</summary>")
        code.line("internal static int BytesHash(byte[]? bytes)")
        code.open("{")
        code.line("if (bytes is null) return 0;")
        code.line("unchecked")
        code.open("{")
        code.line("var result = 19;")
        code.line("foreach (var item in bytes) result = (result * 31) + item;")
        code.line("return result;")
        code.close()
        code.close()
        if conversions { helpers(&code) }
        code.close()
    }

    private static func unitName(_ unit: String) -> String { "GesQuantityUnit.Unit" + GesSchemaPlanner.pascal(unit) }

    private static func isValueType(_ value: GesModelPlanValue) -> Bool {
        switch value.kind {
        case .boolean, .number, .point, .vector, .flags: true
        default: value.closedTag
        }
    }

    private static func literal(_ text: String) -> String { GesSchemaPlanner.literal(text, swift: false) }

    // Converts a native value to a GES value; nullable values pattern-match a numbered variable.
    // A plain value holds nested records as Records rather than as the external values of the bindings. Pattern variables
    // start with `pattern`, so that statements in one scope keep them apart.
    private static func encode(
        _ value: GesModelPlanValue,
        _ owner: String,
        _ expression: String,
        _ qualifier: String,
        _ support: String,
        _ depth: Int = 0,
        nullable: Bool = true,
        plain: Bool = false,
        pattern: String = "v"
    ) -> String {
        if nullable && value.nullable {
            return "\(expression) is { } \(pattern)\(depth) ? \(encode(value, owner, "\(pattern)\(depth)", qualifier, support, depth + 1, nullable: false, plain: plain, pattern: pattern)) : GesValue.GesNothing()"
        }
        switch value.kind {
        case .boolean: return "GesValue.GesBoolean(\(expression))"
        case .number where value.model.type == "Percentage": return "GesValue.GesPercentage(\(expression))"
        case .number where value.model.integer: return "GesValue.GesInteger(\(expression), \(unitName(value.model.unit)))"
        case .number: return "GesValue.GesFloat(\(expression), \(unitName(value.model.unit)))"
        case .text: return "GesValue.GesText(\(expression))"
        case .bytes: return "GesValue.GesBytes(\(expression))"
        case .tag where value.closedTag: return "\(support).Tag(Name(\(expression)))"
        case .tag: return "\(support).Tag(\(expression))"
        case .list: return "\(support).List(\(expression), x\(depth) => \(encode(value.element!, owner, "x\(depth)", qualifier, support, depth + 1, plain: plain, pattern: pattern)))"
        case .dice: return "\(support).Dice(\(expression))"
        case .point, .vector: return "\(expression).ToValue(\(unitName(value.model.unit)))"
        case .schema: return plain ? "\(expression).ToValue()" : "\(expression).ToGesValue()"
        case .union, .nothing, .flags: return "Value(\(expression))"
        }
    }

    // A condition that converts a GES value into the `out` target, false when it does not convert.
    private static func decode(
        _ value: GesModelPlanValue,
        _ owner: String,
        _ expression: String,
        _ target: String,
        _ qualifier: String,
        _ support: String,
        _ depth: Int = 0,
        nullable: Bool = true
    ) -> String {
        if nullable && value.nullable {
            let inner = "(in GesValue x\(depth), out \(plain(value, qualifier, owner)) y\(depth)) => \(decode(value, owner, "x\(depth)", "out y\(depth)", qualifier, support, depth + 1, nullable: false))"
            return "\(support).\(isValueType(value) ? "OptionalValue" : "OptionalObject")(\(expression), \(inner), \(target))"
        }
        switch value.kind {
        case .boolean: return "\(support).Boolean(\(expression), \(target))"
        case .number where value.model.type == "Percentage": return "\(support).Percentage(\(expression), \(target))"
        case .number where value.model.integer: return "\(support).Integer(\(expression), \(unitName(value.model.unit)), \(target))"
        case .number: return "\(support).Number(\(expression), \(unitName(value.model.unit)), \(target))"
        case .text: return "\(support).Text(\(expression), \(target))"
        case .bytes: return "\(support).Bytes(\(expression), \(target))"
        case .tag where value.closedTag: return "Decode\(owner)\(value.nested)(\(expression), \(target))"
        case .tag: return "\(support).Tag(\(expression), \(target))"
        case .list:
            let element = value.element!
            return "\(support).List(\(expression), (in GesValue x\(depth), out \(type(element, qualifier, owner)) y\(depth)) => \(decode(element, owner, "x\(depth)", "out y\(depth)", qualifier, support, depth + 1)), \(target))"
        case .dice: return "\(support).Dice(\(expression), \(target))"
        case .point: return "\(support).Point(\(expression), \(unitName(value.model.unit)), \(target))"
        case .vector: return "\(support).Vector(\(expression), \(unitName(value.model.unit)), \(target))"
        case .schema: return "Decode\(value.model.schema)(\(expression), \(target))"
        case .union, .nothing, .flags: return "Decode\(owner)\(value.nested)(\(expression), \(target))"
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
        _ qualifier: String,
        _ support: String,
        _ depth: Int = 0,
        nullable: Bool = true
    ) -> String {
        let x = "x\(depth)"
        let p = "p\(depth)"
        if nullable && value.nullable {
            return "ReadOptional(\(expression), \(path), (in GesValue \(x), string \(p)) => \(read(value, owner, x, p, qualifier, support, depth + 1, nullable: false)))"
        }
        if value.kind == .schema { return "Read\(value.model.schema)(\(expression), \(path))" }
        let element =
            descends(value.element!)
            ? read(value.element!, owner, x, p, qualifier, support, depth + 1)
            : "\(decode(value.element!, owner, x, "out var y\(depth)", qualifier, support, depth + 1)) ? y\(depth) : throw Mismatch(\(p))"
        return "ReadList(\(expression), \(path), (in GesValue \(x), string \(p)) => \(element))"
    }

    private static func definition(_ name: String, _ value: GesModelPlanValue, _ kind: String) -> String {
        let declared = GesModelPlanner.declared(value)
        guard let declaredKind = declared.kind else { return "new GesExternalType\(kind)Definition(\(literal(name)), \(literal(declared.typeName!)))" }
        let kindName = "GesBytecodeTypeKind." + declaredKind
        return declared.unit == "none"
            ? "new GesExternalType\(kind)Definition(\(literal(name)), \(kindName))" : "new GesExternalType\(kind)Definition(\(literal(name)), \(kindName), \(unitName(declared.unit)))"
    }

    private static func conversions(_ value: GesModelPlanValue, _ found: inout [GesModelPlanValue]) {
        if value.kind == .union || value.kind == .flags || value.closedTag { found.append(value) }
        if let element = value.element { conversions(element, &found) }
        for alternative in value.alternatives { conversions(alternative, &found) }
    }

    // The plain values of the models: a Record of each schema with its fields in declared order, as the value path decodes the
    // bytes of a record, so that wire records read them and give them back.
    private static func valueConversions(_ code: inout Code, _ layout: GesModelLayout, _ name: String, _ qualifier: String, _ support: String) {
        code.line("/// <summary>The plain GES values of the models of this file, as <see cref=\"GesSchemaValues\"/> reads and writes them.</summary>")
        code.line("internal static class \(name)")
        code.open("{")
        for (typeIndex, type) in layout.types.enumerated() {
            let fields = GesModelPlanner.fields(type)
            let record = qualifier + type.name
            let names = type.name + "Fields"
            let typeLiteral = literal(type.name)
            if typeIndex > 0 { code.blank() }
            code.line("private static readonly string[] \(names) = { \(fields.map { literal($0.field.name) }.joined(separator: ", ")) };")
            code.blank()
            // One statement per field, so that the pattern variables of optional fields stay apart.
            code.line("internal static GesValue Value(\(record) record)")
            code.open("{")
            code.line("var values = new GesValue[\(fields.count)];")
            for (index, field) in fields.enumerated() { code.line("values[\(index)] = \(encode(field.value, type.name, "record." + field.base, qualifier, support, plain: true, pattern: "value\(index)_"));") }
            code.line("return GesValue.GesRecord(\(typeLiteral), \(names), values);")
            code.close()
            code.blank()
            code.line("internal static \(record) Read\(type.name)(in GesValue value, string path)")
            code.open("{")
            code.line("var fields = RecordFields(value, \(typeLiteral), \(names)) ?? throw Mismatch(path);")
            for (index, field) in fields.enumerated() {
                let fieldLiteral = literal(field.field.name)
                if descends(field.value) {
                    code.line("var field\(index) = \(read(field.value, type.name, "\(support).Field(fields, \(fieldLiteral))", "Path(path, \(fieldLiteral))", qualifier, support));")
                } else {
                    code.line("if (!\(decode(field.value, type.name, "\(support).Field(fields, \(fieldLiteral))", "out var field\(index)", qualifier, support))) throw Mismatch(Path(path, \(fieldLiteral)));")
                }
            }
            code.line("return new \(record)(\(fields.indices.map { "field\($0)" }.joined(separator: ", ")));")
            code.close()
            code.blank()
            code.line("private static bool Decode\(type.name)(in GesValue value, out \(record) result)")
            code.open("{")
            code.line("result = null!;")
            code.line("var fields = RecordFields(value, \(typeLiteral), \(names));")
            var conditions = ["fields is null"]
            for (index, field) in fields.enumerated() {
                conditions.append("!" + decode(field.value, type.name, "\(support).Field(fields, \(literal(field.field.name)))", "out var field\(index)", qualifier, support))
            }
            for (index, condition) in conditions.enumerated() {
                code.line((index == 0 ? "if (" : "    || ") + condition + (index == conditions.count - 1 ? ") return false;" : ""))
            }
            code.line("result = new \(record)(\(fields.indices.map { "field\($0)" }.joined(separator: ", ")));")
            code.line("return true;")
            code.close()
            var found: [GesModelPlanValue] = []
            for field in fields { conversions(field.value, &found) }
            for conversion in found { self.conversion(&code, type.name, qualifier, support, conversion, plain: true) }
        }
        code.blank()
        code.line("// The fields of a Record of the schema, or of a Map whose keys are all fields of it.")
        code.line("private static GesValueMap? RecordFields(in GesValue value, string name, string[] names)")
        code.open("{")
        code.line("var fields = value.AsMap();")
        code.line("if (fields is null) return null;")
        code.line("if (value.CustomTypeName is { } type) return type == name ? fields : null;")
        code.line("return \(support).Known(fields, names) ? fields : null;")
        code.close()
        code.blank()
        code.line("private static string Path(string path, string field) => path.Length == 0 ? field : path + \".\" + field;")
        code.blank()
        code.line("private static GesWireException Mismatch(string path) => new GesWireException(GesWireException.TypeMismatch, path);")
        code.blank()
        code.line("private delegate T Reader<T>(in GesValue value, string path);")
        code.blank()
        code.line("// The elements of a List, each read at the list's path with `[]`.")
        code.line("private static List<T> ReadList<T>(in GesValue value, string path, Reader<T> element)")
        code.open("{")
        code.line("if (value.ValueKind != GesBytecodeTypeKind.List) throw Mismatch(path);")
        code.line("var items = value.AsList();")
        code.line("var list = new List<T>(items.Length);")
        code.line("var item = path + \"[]\";")
        code.line("for (var index = 0; index < items.Length; index++) list.Add(element(items[index], item));")
        code.line("return list;")
        code.close()
        code.blank()
        code.line("private static T? ReadOptional<T>(in GesValue value, string path, Reader<T> read) where T : class => value.IsNothing ? null : read(value, path);")
        code.close()
    }

    // The catalog and constructor registry of the models, with an external value and a constructor per type.
    private static func bindings(_ code: inout Code, _ layout: GesModelLayout, _ access: String, _ qualifier: String, _ support: String) {
        let ordered = GesModelPlanner.dependencyOrder(layout)
        code.line("/// <summary>")
        code.line("/// The model types for scripts as external types: pass one instance to the compiler as its catalog and to the host as its")
        code.line("/// external-type registry. Each type has its fields and one constructor that takes every field by name; a value that does not")
        code.line("/// convert, such as another unit or kind, constructs <c>nothing</c>. Types and constructors that this set does not declare")
        code.line("/// resolve through the next catalog and registry.")
        code.line("/// </summary>")
        code.line("\(access) sealed class GesTypes : IGesExternalTypeCatalog, IGesExternalTypeRegistry")
        code.open("{")
        code.line("private static readonly GesExternalTypeDefinition[] Own = { \(ordered.map { $0.name + "Value.Declaration" }.joined(separator: ", ")) };")
        code.line("private readonly IGesExternalTypeCatalog? _nextCatalog;")
        code.line("private readonly IGesExternalTypeRegistry? _nextRegistry;")
        code.line("private readonly List<GesExternalTypeDefinition> _types;")
        code.blank()
        code.line("/// <summary>Creates the set in front of another catalog and registry.</summary>")
        code.line("/// <param name=\"nextCatalog\">The catalog that declares every other type, or null.</param>")
        code.line("/// <param name=\"nextRegistry\">The registry that resolves every other constructor, or null.</param>")
        code.line("/// <exception cref=\"ArgumentException\">The next catalog already declares a type of this set.</exception>")
        code.line("public GesTypes(IGesExternalTypeCatalog? nextCatalog = null, IGesExternalTypeRegistry? nextRegistry = null)")
        code.open("{")
        code.line("_types = new List<GesExternalTypeDefinition>(Own);")
        code.line("if (nextCatalog is not null)")
        code.open("{")
        code.line("foreach (var type in nextCatalog.Types)")
        code.open("{")
        code.line("foreach (var own in Own)")
        code.line("    if (own.Name == type.Name) throw new ArgumentException(\"The type :\" + type.Name + \" is already declared.\", nameof(nextCatalog));")
        code.line("_types.Add(type);")
        code.close()
        code.close()
        code.blank()
        code.line("_nextCatalog = nextCatalog;")
        code.line("_nextRegistry = nextRegistry;")
        code.close()
        code.blank()
        code.line("/// <summary>Gets the declarations of the generated types, each after the types it holds, then those of the next catalog.</summary>")
        code.line("public IReadOnlyList<GesExternalTypeDefinition> Types => _types;")
        code.blank()
        code.line("/// <summary>Resolves a generated type, or asks the next catalog.</summary>")
        code.line("/// <param name=\"typeName\">The type name.</param>")
        code.line("/// <returns>The declaration, or null.</returns>")
        code.line("public GesExternalTypeDefinition? Resolve(string typeName)")
        code.open("{")
        code.line("foreach (var own in Own)")
        code.line("    if (own.Name == typeName) return own;")
        code.line("return _nextCatalog?.Resolve(typeName);")
        code.close()
        code.blank()
        code.line("/// <summary>Resolves the constructor of a generated type, or asks the next registry.</summary>")
        code.line("/// <param name=\"reference\">The constructor reference.</param>")
        code.line("/// <returns>The constructor, or null.</returns>")
        code.line("public IGesExternalTypeConstructor? Resolve(GesExternalTypeConstructorReference reference)")
        code.open("{")
        code.line("if (reference is null) throw new ArgumentNullException(nameof(reference));")
        for type in ordered { code.line("if (reference.SignatureId == \(type.name)Value.Declaration.Constructors[0].SignatureId) return new \(type.name)Constructor();") }
        code.line("return _nextRegistry?.Resolve(reference);")
        code.close()
        for type in ordered {
            let fields = GesModelPlanner.fields(type)
            let name = qualifier + type.name
            code.blank()
            code.line("/// <summary>A <c>:\(type.name)</c> held by a script.</summary>")
            code.line("internal sealed class \(type.name)Value : IGesExternalValue")
            code.open("{")
            code.line("/// <summary>The declaration that every value of the type shares.</summary>")
            code.line("internal static readonly GesExternalTypeDefinition Declaration = new GesExternalTypeDefinition(")
            code.line("    \(literal(type.name)),")
            code.line("    new[] { \(fields.map { definition($0.field.name, $0.value, "Field") }.joined(separator: ", "))\(fields.isEmpty ? "" : ", ")new GesExternalTypeFieldDefinition(\"fields\", GesBytecodeTypeKind.Map) },")
            code.line("    new[] { new GesExternalTypeConstructorDefinition(\(literal(type.name)), new[] { new GesExternalTypeParameterDefinition(\"fields\", GesBytecodeTypeKind.Map) }) });")
            code.blank()
            code.line("internal static readonly string[] Names = { \(fields.map { literal($0.field.name) }.joined(separator: ", ")) };")
            code.blank()
            code.line("internal \(type.name)Value(\(name) record) => Record = record;")
            code.blank()
            code.line("internal \(name) Record { get; }")
            code.blank()
            code.line("/// <inheritdoc />")
            code.line("public GesExternalTypeDefinition Definition => Declaration;")
            code.blank()
            code.line("/// <inheritdoc />")
            code.line("public GesValue? GetField(string fieldName) => fieldName switch")
            code.open("{")
            for field in fields { code.line("\(literal(field.field.name)) => \(encode(field.value, type.name, "Record." + field.base, qualifier, support)),") }
            code.line("\"fields\" => \(support).Fields(Names, this),")
            code.line("_ => null")
            code.close("};")
            code.close()
            code.blank()
            code.line("/// <summary>Constructs a <c>:\(type.name)</c> from a Map or Record of its fields; an absent optional field may be left out.</summary>")
            code.line("private sealed class \(type.name)Constructor : IGesExternalTypeConstructor")
            code.open("{")
            code.line("/// <inheritdoc />")
            code.line("public GesExternalTypeConstructorDefinition Definition => \(type.name)Value.Declaration.Constructors[0];")
            code.blank()
            code.line("/// <inheritdoc />")
            code.line("public void Invoke(GesExternalTypeConstructorCall call)")
            code.open("{")
            code.line("var fields = call.Arguments[0].AsMap();")
            var conditions = ["fields is null", "!\(support).Known(fields, \(type.name)Value.Names)"]
            for field in fields {
                conditions.append("!" + decode(field.value, type.name, "\(support).Field(fields, \(literal(field.field.name)))", "out var " + parameter(field.base), qualifier, support))
            }
            for (index, condition) in conditions.enumerated() { code.line((index == 0 ? "if (" : "    || ") + condition + (index == conditions.count - 1 ? ")" : "")) }
            code.open("{")
            code.line("call.SetNothing();")
            code.line("return;")
            code.close()
            code.blank()
            code.line("call.SetExternalValue(new \(type.name)Value(new \(name)(\(fields.map { parameter($0.base) }.joined(separator: ", ")))));")
            code.close()
            code.close()
            code.blank()
            code.line("private static bool Decode\(type.name)(in GesValue value, out \(name) result)")
            code.open("{")
            code.line("result = \(name).FromGesValue(value)!;")
            code.line("return result is not null;")
            code.close()
            var found: [GesModelPlanValue] = []
            for field in fields { conversions(field.value, &found) }
            for conversion in found { self.conversion(&code, type.name, qualifier, support, conversion) }
        }
        code.close()
    }

    private static func conversion(_ code: inout Code, _ owner: String, _ qualifier: String, _ support: String, _ value: GesModelPlanValue, plain: Bool = false) {
        let name = qualifier + owner + "." + value.nested
        code.blank()
        if value.closedTag {
            code.line("private static string Name(\(name) value) => value switch")
            code.open("{")
            for tag in value.model.tags { code.line("\(name).\(GesSchemaPlanner.pascal(tag)) => \(literal(tag)),") }
            code.line("_ => \"\"")
            code.close("};")
            code.blank()
            code.line("private static bool Decode\(owner)\(value.nested)(in GesValue value, out \(name) result)")
            code.open("{")
            code.line("result = default;")
            code.line("if (!\(support).Tag(value, out var text)) return false;")
            code.line("switch (text)")
            code.open("{")
            for tag in value.model.tags { code.line("case \(literal(tag)): result = \(name).\(GesSchemaPlanner.pascal(tag)); return true;") }
            code.line("default: return false;")
            code.close()
            code.close()
            return
        }
        if value.kind == .flags {
            let names = "\(owner)\(value.nested)Names"
            code.line("private static readonly string[] \(names) = { \(value.model.tags.map(literal).joined(separator: ", ")) };")
            code.blank()
            code.line("private static GesValue Value(\(name) value) => \(support).Flags(\(names), (ulong)value);")
            code.blank()
            code.line("private static bool Decode\(owner)\(value.nested)(in GesValue value, out \(name) result)")
            code.open("{")
            code.line("var valid = \(support).Flags(value, \(names), out var mask);")
            code.line("result = (\(name))mask;")
            code.line("return valid;")
            code.close()
            return
        }
        code.line("private static GesValue Value(\(name) value) => value switch")
        code.open("{")
        for alternative in value.alternatives { code.line("\(name).\(alternative.label) alternative => \(encode(alternative, owner, "alternative.Value", qualifier, support, plain: plain)),") }
        code.line("_ => GesValue.GesNothing()")
        code.close("};")
        code.blank()
        code.line("private static bool Decode\(owner)\(value.nested)(in GesValue value, out \(name) result)")
        code.open("{")
        for (index, alternative) in value.alternatives.enumerated() {
            code.line("if (\(decode(alternative, owner, "value", "out var alternative\(index)", qualifier, support)))")
            code.open("{")
            code.line("result = new \(name).\(alternative.label)(alternative\(index));")
            code.line("return true;")
            code.close()
            code.blank()
        }
        code.line("result = null!;")
        code.line("return false;")
        code.close()
    }

    private static func helpers(_ code: inout Code) {
        code.blank()
        code.line("internal delegate bool Decoder<T>(in GesValue value, out T result);")
        code.blank()
        code.line("internal static GesValue Field(GesValueMap fields, string name) => fields.Get(name) ?? GesValue.GesNothing();")
        code.blank()
        code.line("internal static bool Known(GesValueMap fields, string[] names)")
        code.open("{")
        code.line("for (var index = 0; index < fields.Length; index++)")
        code.line("    if (Array.IndexOf(names, fields.KeyAt(index)) < 0) return false;")
        code.line("return true;")
        code.close()
        code.blank()
        code.line("internal static GesValue Fields(string[] names, IGesExternalValue value)")
        code.open("{")
        code.line("var values = new GesValue[names.Length];")
        code.line("for (var index = 0; index < names.Length; index++) values[index] = value.GetField(names[index]) ?? GesValue.GesNothing();")
        code.line("return GesValue.GesMap(names, values);")
        code.close()
        code.blank()
        code.line("internal static GesValue Flags(string[] names, ulong mask)")
        code.open("{")
        code.line("var keys = new List<string>();")
        code.line("for (var index = 0; index < names.Length; index++)")
        code.line("    if ((mask >> index & 1) != 0) keys.Add(names[index]);")
        code.line("var values = new GesValue[keys.Count];")
        code.line("for (var index = 0; index < values.Length; index++) values[index] = GesValue.GesBoolean(true);")
        code.line("return GesValue.GesMap(keys.ToArray(), values);")
        code.close()
        code.blank()
        code.line("internal static bool Flags(in GesValue value, string[] names, out ulong result)")
        code.open("{")
        code.line("result = 0;")
        code.line("if (value.ValueKind != GesBytecodeTypeKind.Map || value.CustomTypeName is not null || value.MaterializedMap() is not { } map) return false;")
        code.line("for (var index = 0; index < map.Length; index++)")
        code.open("{")
        code.line("var member = Array.IndexOf(names, map.KeyAt(index));")
        code.line("var set = map.ValueAt(index);")
        code.line("if (member < 0 || set.ValueKind != GesBytecodeTypeKind.Boolean) return false;")
        code.line("if (set.AsBoolean()) result |= 1UL << member;")
        code.close()
        code.line("return true;")
        code.close()
        code.blank()
        code.line("internal static GesValue Tag(string name)")
        code.open("{")
        code.line("try")
        code.open("{")
        code.line("return GesValue.GesTag(name);")
        code.close()
        code.line("catch (ArgumentException)")
        code.open("{")
        code.line("return GesValue.GesNothing();")
        code.close()
        code.close()
        code.blank()
        code.line("internal static bool Tag(in GesValue value, out string result) => Read(value.ValueKind is GesBytecodeTypeKind.Tag or GesBytecodeTypeKind.Text, value.AsText(), out result);")
        code.blank()
        code.line("internal static bool Text(in GesValue value, out string result) => Read(value.ValueKind == GesBytecodeTypeKind.Text, value.AsText(), out result);")
        code.blank()
        code.line("internal static bool Bytes(in GesValue value, out byte[] result) => Read(value.ValueKind == GesBytecodeTypeKind.Bytes, value.AsBytes().ToArray(), out result);")
        code.blank()
        code.line("internal static bool Boolean(in GesValue value, out bool result) => Read(value.ValueKind == GesBytecodeTypeKind.Boolean, value.AsBoolean(), out result);")
        code.blank()
        code.line("internal static bool Percentage(in GesValue value, out double result) => Read(value.ValueKind == GesBytecodeTypeKind.Percentage, value.AsNumber(), out result);")
        code.blank()
        code.line(
            "internal static bool Number(in GesValue value, GesQuantityUnit unit, out double result)"
                + " => Read(value.ValueKind is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float && value.ValueUnit == unit, value.AsNumber(), out result);"
        )
        code.blank()
        code.line("internal static bool Point(in GesValue value, GesQuantityUnit unit, out GesPoint result) => Read(GesPoint.FromValue(value, unit), out result);")
        code.blank()
        code.line("internal static bool Vector(in GesValue value, GesQuantityUnit unit, out GesVector result) => Read(GesVector.FromValue(value, unit), out result);")
        code.blank()
        code.line("internal static bool Integer(in GesValue value, GesQuantityUnit unit, out long result)")
        code.open("{")
        code.line("result = 0;")
        code.line("if (value.ValueUnit != unit) return false;")
        code.line("if (value.ValueKind == GesBytecodeTypeKind.Integer) result = value.AsInteger();")
        code.line("else if (value.ValueKind == GesBytecodeTypeKind.Float && value.AsNumber() is var number && number >= -9.2233720368547758E18 && number < 9.2233720368547758E18 && Math.Floor(number) == number) result = (long)number;")
        code.line("else return false;")
        code.line("return true;")
        code.close()
        code.blank()
        code.line("internal static bool Read<T>(bool valid, T value, out T result)")
        code.open("{")
        code.line("result = valid ? value : default!;")
        code.line("return valid;")
        code.close()
        code.blank()
        code.line("internal static bool Read<T>(T? value, out T result) where T : struct")
        code.open("{")
        code.line("result = value.GetValueOrDefault();")
        code.line("return value.HasValue;")
        code.close()
        code.blank()
        code.line("internal static bool OptionalValue<T>(in GesValue value, Decoder<T> decode, out T? result) where T : struct")
        code.open("{")
        code.line("result = null;")
        code.line("if (value.IsNothing) return true;")
        code.line("if (!decode(value, out var decoded)) return false;")
        code.line("result = decoded;")
        code.line("return true;")
        code.close()
        code.blank()
        code.line("internal static bool OptionalObject<T>(in GesValue value, Decoder<T> decode, out T? result) where T : class")
        code.open("{")
        code.line("result = null;")
        code.line("if (value.IsNothing) return true;")
        code.line("if (!decode(value, out var decoded)) return false;")
        code.line("result = decoded;")
        code.line("return true;")
        code.close()
        code.blank()
        code.line("internal static bool List<T>(in GesValue value, Decoder<T> element, out List<T> result)")
        code.open("{")
        code.line("result = null!;")
        code.line("if (value.ValueKind != GesBytecodeTypeKind.List) return false;")
        code.line("var items = value.AsList();")
        code.line("var list = new List<T>(items.Length);")
        code.line("for (var index = 0; index < items.Length; index++)")
        code.open("{")
        code.line("if (!element(items[index], out var item)) return false;")
        code.line("list.Add(item);")
        code.close()
        code.blank()
        code.line("result = list;")
        code.line("return true;")
        code.close()
        code.blank()
        code.line("internal static GesValue List<T>(List<T> list, Func<T, GesValue> element)")
        code.open("{")
        code.line("var items = new GesValue[list.Count];")
        code.line("for (var index = 0; index < items.Length; index++) items[index] = element(list[index]);")
        code.line("return GesValue.GesList(items);")
        code.close()
        code.blank()
        code.line("internal static bool Dice(in GesValue value, out List<int> result)")
        code.open("{")
        code.line("result = null!;")
        code.line("if (value.ValueKind != GesBytecodeTypeKind.Dice) return false;")
        code.line("result = new List<int>(value.AsDice());")
        code.line("return true;")
        code.close()
        code.blank()
        code.line("internal static GesValue Dice(List<int> rolls)")
        code.open("{")
        code.line("foreach (var roll in rolls)")
        code.line("    if (roll <= 0) return GesValue.GesNothing();")
        code.line("return GesValue.GesDice(rolls.ToArray());")
        code.close()
    }
}
