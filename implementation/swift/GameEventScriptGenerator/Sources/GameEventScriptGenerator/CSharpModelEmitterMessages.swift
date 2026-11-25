// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime

// The model messages and model message sets: a message is a record of its arguments and a Boolean per tag, a set holds its
// parameters and the carried message as a case of its members, and both convert to and from GES messages.
extension GesCSharpModelEmitter {
    // The members that converting to and from messages adds to a model message or set.
    static let messageMembers = ["ToMessage", "FromMessage", "FromMessageLenient"]

    // The names a member of a set's Case cannot take: its own members and those of a subclass.
    private static let caseMembers = ["Case", "Selected", "From", "Value", "Equals", "GetHashCode", "ToString", "GetType"]

    // The layout restricted to schemas, which bindings and documents concern.
    static func schemasOnly(_ layout: GesModelLayout) -> GesModelLayout {
        let types = layout.types.filter { $0.kind == .schema }
        var plain = GesModelLayout(roots: layout.roots.filter { root in types.contains { $0.name == root } }, types: types)
        plain.sources = layout.sources
        plain.protocols = layout.protocols
        return plain
    }

    // The documentation, parameters and assignments of a constructor that takes fields in declared order. Optional parameters
    // end the list, so a default becomes one only where every later field has a default too; an optional argument of a message
    // or set defaults to absence.
    static func constructor(_ layout: GesModelLayout, _ fields: [GesModelPlanField], _ order: [Int], absent: Bool) -> (documentation: [String], parameters: [String], assignments: [String]) {
        let isAbsent = { (field: GesModelPlanField) in absent && field.field.optional && field.field.defaultValue == nil }
        var trailing = fields.count
        while trailing > 0 && (fields[trailing - 1].field.defaultValue != nil || isAbsent(fields[trailing - 1])) { trailing -= 1 }
        // A record default or an empty typed List is no constant, so a trailing parameter of one is null for it; free
        // data is a struct whose default value is empty.
        let record = { (index: Int) in
            index >= trailing && (fields[index].field.defaultValue?.kind == .record || fields[index].field.defaultValue?.kind == .empty) && !isData(fields[index].value)
        }
        var documentation: [String] = []
        var parameters: [String] = []
        for (index, field) in fields.enumerated() {
            let note = GesModelPlanner.sibling(fields, field).map { "; null takes <c>\(fields[$0].field.name)</c>" } ?? (record(index) ? "; null takes its default" : "")
            documentation.append("/// <param name=\"\(GesSchemaPlanner.camel(field.base))\">The \(field.tag ? "tag" : "field") <c>\(field.field.name)</c>\(note).</param>")
            let optional = index >= trailing
            if GesModelPlanner.sibling(fields, field) == nil && !record(index) {
                let fallback = !optional ? "" : isAbsent(field) ? " = null" : " = " + native(layout, field.value, field.field.defaultValue!)
                parameters.append("\(type(field.value)) \(parameter(field.base))\(fallback)")
            } else {
                parameters.append("\(plain(field.value))? \(parameter(field.base))\(optional ? " = null" : "")")
            }
        }
        var assignments: [String] = []
        for index in order {
            let field = fields[index]
            let fallback = GesModelPlanner.sibling(fields, field).map { " ?? " + fields[$0].base } ?? (record(index) ? " ?? " + native(layout, field.value, field.field.defaultValue!) : "")
            assignments.append("\(field.base) = \(parameter(field.base))\(fallback);")
        }
        return (documentation, parameters, assignments)
    }

    // ToMessage, FromMessage and FromMessageLenient of a model message or set.
    static func messageConversions(_ code: inout Code, _ type: GesModelType, _ values: String, _ schemas: String) {
        let set = type.kind == .messages
        let what = set ? "message set" : "message"
        code.blank()
        if set && !type.fields.isEmpty {
            code.line("/// <summary>Converts the set into its GES message <c>\(type.name)</c>: its parameters, then the carried message as <c>message</c>.</summary>")
        } else if set {
            code.line("/// <summary>Converts the set into the GES message it carries.</summary>")
        } else {
            code.line("/// <summary>Converts the record into its GES message: its arguments in declared order and the tags that are true.</summary>")
        }
        code.line("/// <returns>The message.</returns>")
        code.line("public GameEventScriptMessage ToMessage() => \(values).Message(this);")
        code.blank()
        code.line("/// <summary>Reads a GES message of the \(what) <c>\(type.name)</c> as <see cref=\"GesSchemaValues\"/> encodes it: every argument in")
        code.line("/// declared order with its label.</summary>")
        code.line("/// <param name=\"message\">The message.</param>")
        code.line("/// <returns>The \(set ? "set" : "record").</returns>")
        code.line("/// <exception cref=\"GesWireException\">The message does not convert, with the code and path of the value path; another message is")
        code.line("/// <c>wire.typeMismatch</c> at the empty path.</exception>")
        code.line(
            "public static \(type.name) FromMessage(GameEventScriptMessage message) => \(values).Read\(type.name)Message(message ?? throw new global::System.ArgumentNullException(nameof(message)), \"\");"
        )
        code.blank()
        code.line("/// <summary>Reads a GES message of the \(what) <c>\(type.name)</c> after completing it with the lenient read: arguments it")
        code.line("/// leaves out take their defaults or absence.</summary>")
        code.line("/// <param name=\"message\">The message.</param>")
        code.line("/// <returns>The \(set ? "set" : "record").</returns>")
        code.line("/// <exception cref=\"GesWireException\">The message does not convert, with the code and path of the value path.</exception>")
        code.line("/// <exception cref=\"GesSchemaException\">The runtime no longer parses the embedded schema sources.</exception>")
        code.line(
            "public static \(type.name) FromMessageLenient(GameEventScriptMessage message) => FromMessage(GesSchemaValues.CompleteMessage(\(schemas).\(type.name), message ?? throw new global::System.ArgumentNullException(nameof(message))));"
        )
    }

    // The model of a message set: its parameters, the carried message as a case of its members and its conversions.
    static func messageSet(
        _ code: inout Code,
        _ layout: GesModelLayout,
        _ type: GesModelType,
        _ access: String,
        _ support: String,
        _ qualifier: String,
        _ values: String,
        _ schemas: String
    ) throws(GesGeneratorError) {
        let fields = GesModelPlanner.fields(type)
        var names: Names = [(type.name, "the type itself")] + reserved.map { ($0, "the inherited member " + $0) }
        names.append(("SchemaHash", "the generated member SchemaHash"))
        names += messageMembers.map { ($0, "the generated member " + $0) }
        names.append(("Message", "the generated member Message"))
        names.append(("Case", GesSchemaPlanner.nestedTypeOrigin + "Case"))
        names.append(("Member", GesSchemaPlanner.nestedTypeOrigin + "Member"))
        for field in fields {
            names.append((field.base, "the parameter " + field.field.name))
            nested(field.value, &names, field.field.name)
        }
        try GesSchemaPlanner.unique(type.name, names, GesModelPlanner.topLevel(layout, swift: false))
        let cases: Names = caseMembers.map { ($0, "the generated member " + $0) } + type.members.map { ($0, "the case of the member " + $0) }
        try GesSchemaPlanner.unique(type.name + ".Case", cases)

        let order = GesModelPlanner.readOrder(fields)
        code.line("/// <summary>The model of the message set <c>\(type.name)</c>: \(fields.isEmpty ? "" : "its parameters and ")the message it carries, a plain value that is never")
        code.line("/// encoded and compares by value.</summary>")
        code.line("\(access) sealed class \(type.name) : IEquatable<\(type.name)>")
        code.open("{")
        code.line("/// <summary>The hash of the message set <c>\(type.name)</c>, which wire code of it carries too; a test that compares it notices stale generated code.</summary>")
        code.line("public const ulong SchemaHash = 0x\(GesSchemaLayoutJson.hex(type.hash))UL;")
        code.blank()
        code.line("/// <summary>Creates a set that carries a message\(fields.isEmpty ? "" : ", with its parameters").</summary>")
        code.line("/// <param name=\"message\">The carried message.</param>")
        let (documentation, parameters, assignments) = constructor(layout, fields, order, absent: true)
        for line in documentation { code.line(line.replacingAll("The field <c>", with: "The parameter <c>")) }
        code.line("public \(type.name)(\((["Case message"] + parameters).joined(separator: ", ")))")
        code.open("{")
        code.line("Message = message ?? throw new global::System.ArgumentNullException(nameof(message));")
        for line in assignments { code.line(line) }
        code.close()
        code.blank()
        code.line("/// <summary>The carried message.</summary>")
        code.line("public Case Message { get; set; }")
        for field in fields {
            code.blank()
            code.line("/// <summary><c>\(escape(field.field.name + ": " + GesModelPlanner.describe(layout, field.field)))</c></summary>")
            code.line("public \(self.type(field.value)) \(field.base) { get; set; }")
        }
        code.blank()
        code.line("/// <summary>Compares the carried message and every parameter by value.</summary>")
        code.line("/// <param name=\"other\">The other set.</param>")
        code.line("/// <returns>Whether both hold equal values.</returns>")
        code.line("public bool Equals(\(type.name)? other)")
        code.open("{")
        code.line("if (other is null) return false;")
        code.line("if (ReferenceEquals(this, other)) return true;")
        code.line("return Message.Equals(other.Message)" + (fields.isEmpty ? ";" : ""))
        for (index, field) in fields.enumerated() {
            code.line("    && " + equal(field.value, field.base, "other." + field.base, support) + (index == fields.count - 1 ? ";" : ""))
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
        code.line("var hash = (17 * 31) + Message.GetHashCode();")
        for field in fields { code.line("hash = (hash * 31) + \(hash(field.value, field.base, support));") }
        code.line("return hash;")
        code.close()
        code.close()
        code.blank()
        operators(&code, type.name, "set")
        messageConversions(&code, type, values, schemas)
        code.blank()
        try tagEnum(&code, "Member", type.members, "public", "/// <summary>The members of the set, in declared order.</summary>")
        code.blank()
        caseType(&code, layout, type, qualifier)
        for field in fields { try declarations(&code, field.value, support, qualifier) }
        code.close()
    }

    // The carried message of a set: an abstract class with a sealed subclass per member and the conversions from the cases of
    // other sets that share members.
    private static func caseType(_ code: inout Code, _ layout: GesModelLayout, _ type: GesModelType, _ qualifier: String) {
        code.line("/// <summary>The message a set carries: one sealed subclass per member, holding the member's model.</summary>")
        code.line("public abstract class Case : IEquatable<Case>")
        code.open("{")
        code.line("private protected Case()")
        code.open("{")
        code.close()
        code.blank()
        code.line("/// <summary>Gets the member the case holds.</summary>")
        code.line("public abstract Member Selected { get; }")
        code.blank()
        code.line("/// <summary>Compares the member and its message by value.</summary>")
        code.line("/// <param name=\"other\">The other case.</param>")
        code.line("/// <returns>Whether both hold the same member with an equal message.</returns>")
        code.line("public abstract bool Equals(Case? other);")
        code.blank()
        code.line("/// <inheritdoc />")
        code.line("public override bool Equals(object? obj) => obj is Case other && Equals(other);")
        code.blank()
        code.line("/// <inheritdoc />")
        code.line("public abstract override int GetHashCode();")
        code.blank()
        operators(&code, "Case", "case")
        for other in layout.types {
            if other.kind != .messages || other.name == type.name { continue }
            let shared = other.members.filter { type.members.contains($0) }
            if shared.isEmpty { continue }
            let source = qualifier + other.name + ".Case"
            code.blank()
            code.line("/// <summary>Takes the message of a case of <see cref=\"\(source)\"/> when this set has it as a member.</summary>")
            code.line("/// <param name=\"other\">The other case.</param>")
            code.line("/// <returns>The case of the same message, or null when this set has no such member.</returns>")
            code.line("public static Case? From(\(source) other) => other switch")
            code.open("{")
            for member in shared { code.line("\(source).\(member) held => new \(member)(held.Value),") }
            code.line("_ => null")
            code.close("};")
        }
        for (index, member) in type.members.enumerated() {
            let model = qualifier + member
            code.blank()
            code.line("/// <summary>The message <c>\(member)</c>.</summary>")
            code.line("public sealed class \(member) : Case")
            code.open("{")
            code.line("/// <summary>Holds a message.</summary>")
            code.line("/// <param name=\"value\">The message.</param>")
            code.line("public \(member)(\(model) value)")
            code.open("{")
            code.line("Value = value ?? throw new global::System.ArgumentNullException(nameof(value));")
            code.close()
            code.blank()
            code.line("/// <summary>Gets the message.</summary>")
            code.line("public \(model) Value { get; }")
            code.blank()
            code.line("/// <inheritdoc />")
            code.line("public override Member Selected => Member.\(GesSchemaPlanner.pascal(member));")
            code.blank()
            code.line("/// <inheritdoc />")
            code.line("public override bool Equals(Case? other) => other is \(member) held && Value.Equals(held.Value);")
            code.blank()
            code.line("/// <inheritdoc />")
            code.line("public override int GetHashCode() => unchecked((\(index) * 31) + Value.GetHashCode());")
            code.close()
        }
        code.close()
    }

    // The values class's conversion of a model message to and from a GES message.
    static func messageValues(_ code: inout Code, _ type: GesModelType, _ fields: [GesModelPlanField], _ record: String, _ qualifier: String, _ support: String) {
        let arguments = fields.filter { !$0.tag }
        code.blank()
        code.line("internal static GameEventScriptMessage Message(\(record) record)")
        code.open("{")
        code.line("var arguments = new GesMessageArgument[\(arguments.count)];")
        for (index, field) in arguments.enumerated() {
            let label = literal(field.field.unlabeled ? "_" : field.field.name)
            code.line(
                "arguments[\(index)] = new GesMessageArgument(\(label), \(encode(field.value, type.name, "record." + field.base, qualifier, support, plain: true, pattern: "value\(index)_")));"
            )
        }
        let tags = fields.filter(\.tag)
        code.line("var tags = new List<string>(\(tags.count));")
        for tag in tags { code.line("if (record.\(tag.base)) tags.Add(\(literal(tag.field.name)));") }
        code.line("return GameEventScriptMessage.Create(\(literal(type.name)), arguments, tags);")
        code.close()
        code.blank()
        // The arguments in declared order with their labels and only declared tags, as encoding a message requires, read as the
        // Record of the model's fields.
        code.line("internal static \(record) Read\(type.name)Message(GameEventScriptMessage message, string path)")
        code.open("{")
        code.line("if (message.Name != \(literal(type.name))) throw new GesWireException(GesWireException.TypeMismatch, path);")
        if tags.isEmpty {
            code.line("if (message.Tags.Count > 0) throw new GesWireException(GesWireException.UnknownTag, path);")
        } else {
            code.line("foreach (var tag in message.Tags)")
            let unknown = tags.map { "tag != " + literal($0.field.name) }.joined(separator: " && ")
            code.line("    if (\(unknown)) throw new GesWireException(GesWireException.UnknownTag, path);")
        }
        code.line("var values = new GesValue[\(fields.count)];")
        argumentValues(&code, arguments)
        code.line("if (message.Arguments.Count != \(arguments.count)) throw new GesWireException(GesWireException.TypeMismatch, path);")
        for (index, tag) in tags.enumerated() {
            code.line("values[\(arguments.count + index)] = GesValue.GesBoolean(HasTag(message, \(literal(tag.field.name))));")
        }
        code.line("return Read\(type.name)(GesValue.GesRecord(\(literal(type.name)), \(type.name)Fields, values), path);")
        code.close()
    }

    // Takes the arguments of a message into `values`, each with its declared label at its index.
    private static func argumentValues(_ code: inout Code, _ arguments: [GesModelPlanField]) {
        for (at, field) in arguments.enumerated() {
            let path = "Path(path, \(literal(field.field.name)))"
            code.line("if (message.Arguments.Count <= \(at)) throw new GesWireException(GesWireException.MissingField, \(path));")
            code.line("if (message.Arguments.NameAt(\(at)) != \(literal(field.field.unlabeled ? "_" : field.field.name))) throw new GesWireException(GesWireException.TypeMismatch, \(path));")
            code.line("values[\(at)] = message.Arguments.ValueAt(\(at));")
        }
    }

    // The values class's conversion of a model set to and from the GES message it carries or its envelope.
    static func setValues(_ code: inout Code, _ layout: GesModelLayout, _ type: GesModelType, _ fields: [GesModelPlanField], _ record: String, _ qualifier: String, _ support: String) {
        code.blank()
        code.line("internal static GameEventScriptMessage Message(\(record) set)")
        code.open("{")
        code.line("var carried = set.Message switch")
        code.open("{")
        for member in type.members { code.line("\(record).Case.\(member) held => Message(held.Value),") }
        code.line("_ => throw new global::System.ArgumentException(\"The set carries no message.\", nameof(set))")
        code.close("};")
        if fields.isEmpty {
            code.line("return carried;")
        } else {
            code.line("var arguments = new GesMessageArgument[\(fields.count + 1)];")
            for (index, field) in fields.enumerated() {
                let label = literal(field.field.unlabeled ? "_" : field.field.name)
                code.line(
                    "arguments[\(index)] = new GesMessageArgument(\(label), \(encode(field.value, type.name, "set." + field.base, qualifier, support, plain: true, pattern: "value\(index)_")));"
                )
            }
            code.line("arguments[\(fields.count)] = new GesMessageArgument(\"message\", GesValue.GesMessage(carried));")
            code.line("return GameEventScriptMessage.Create(\(literal(type.name)), arguments, global::System.Array.Empty<string>());")
        }
        code.close()
        code.blank()
        code.line("internal static \(record) Read\(type.name)Message(GameEventScriptMessage message, string path)")
        code.open("{")
        if fields.isEmpty {
            code.line("return new \(record)(Read\(type.name)Case(message, path));")
        } else {
            // The envelope: the parameters in declared order, then the carried message as `message`.
            code.line("if (message.Name != \(literal(type.name))) throw new GesWireException(GesWireException.TypeMismatch, path);")
            // The envelope carries no tags of its own; the carried message keeps its tags.
            code.line("if (message.Tags.Count > 0) throw new GesWireException(GesWireException.UnknownTag, path);")
            code.line("var last = message.Arguments.Count - 1;")
            code.line("if (last < 0 || message.Arguments.NameAt(last) != \"message\")")
            code.open("{")
            code.line("for (var index = 0; index < message.Arguments.Count; index++)")
            code.line("    if (message.Arguments.NameAt(index) == \"message\") throw new GesWireException(GesWireException.TypeMismatch, Path(path, \"message\"));")
            code.line("throw new GesWireException(GesWireException.MissingField, Path(path, \"message\"));")
            code.close()
            code.line("var carried = message.Arguments.ValueAt(last).Message ?? throw new GesWireException(GesWireException.TypeMismatch, Path(path, \"message\"));")
            code.line("var values = new GesValue[\(fields.count)];")
            for (index, field) in fields.enumerated() {
                let path = "Path(path, \(literal(field.field.name)))"
                code.line("if (last <= \(index)) throw new GesWireException(GesWireException.MissingField, \(path));")
                code.line("if (message.Arguments.NameAt(\(index)) != \(literal(field.field.unlabeled ? "_" : field.field.name))) throw new GesWireException(GesWireException.TypeMismatch, \(path));")
                code.line("values[\(index)] = message.Arguments.ValueAt(\(index));")
            }
            code.line("if (last != \(fields.count)) throw new GesWireException(GesWireException.TypeMismatch, path);")
            code.line("var held = Read\(type.name)Case(carried, Path(path, \"message\"));")
            code.line("return Read\(type.name)(GesValue.GesRecord(\(literal(type.name)), \(type.name)Fields, values), path, held);")
        }
        code.close()
        code.blank()
        code.line("private static \(record).Case Read\(type.name)Case(GameEventScriptMessage message, string path)")
        code.open("{")
        code.line("switch (message.Name)")
        code.open("{")
        for member in type.members {
            code.line("case \(literal(member)):")
            code.line("    return new \(record).Case.\(member)(Read\(member)Message(message, path));")
        }
        code.line("default:")
        code.line("    throw new GesWireException(GesWireException.TypeMismatch, path);")
        code.close()
        code.close()
    }

    // The helpers of the values class that model messages and sets use.
    static func messageHelpers(_ code: inout Code) {
        code.blank()
        code.line("private static bool HasTag(GameEventScriptMessage message, string tag)")
        code.open("{")
        code.line("foreach (var candidate in message.Tags)")
        code.line("    if (candidate == tag) return true;")
        code.line("return false;")
        code.close()
    }

    // The schemas of the model messages and sets, which the lenient read completes messages against without binding them.
    static func schemasHolder(_ code: inout Code, _ layout: GesModelLayout, _ holder: String, _ schemaSet: GesSchemaSetReference?) {
        let types = layout.types.filter { $0.kind != .schema }
        code.line("/// <summary>The schemas of the model messages and sets of this file, which the lenient read completes messages against.</summary>")
        code.line("internal static class \(holder)")
        code.open("{")
        for type in types { code.line("private static GesSchema? __\(type.name);") }
        if schemaSet == nil {
            code.line("private static GesSchemaSet? __set;")
            code.blank()
            code.line("private static GesSchemaSet Set => __set ??= GesSchemaSet.Parse(")
            code.indent()
            for (index, source) in layout.sources.enumerated() {
                code.line("new GesSchemaSource(\(literal(source.name)), string.Join(\"\\n\", new[]")
                code.open("{")
                for line in GesSchemaPlanner.lines(source.text) { code.line(literal(line) + ",") }
                code.close(index + 1 < layout.sources.count ? "}))," : "})));")
            }
            code.dedent()
        }
        let set = schemaSet.map { $0.qualified + ".Set" } ?? "Set"
        for type in types {
            code.blank()
            code.line("internal static GesSchema \(type.name) => __\(type.name) ??= \(set).Schema(\(literal(type.lookup)));")
        }
        code.close()
    }
}

extension String {
    // The text with every occurrence of a part replaced, as C#'s Replace does, by Unicode scalars and without Foundation.
    fileprivate func replacingAll(_ part: String, with replacement: String) -> String {
        let scalars = Array(unicodeScalars)
        let pattern = Array(part.unicodeScalars)
        var result = String.UnicodeScalarView()
        var index = 0
        while index < scalars.count {
            if index + pattern.count <= scalars.count && Array(scalars[index..<index + pattern.count]) == pattern {
                result.append(contentsOf: replacement.unicodeScalars)
                index += pattern.count
            } else {
                result.append(scalars[index])
                index += 1
            }
        }
        return String(result)
    }
}
