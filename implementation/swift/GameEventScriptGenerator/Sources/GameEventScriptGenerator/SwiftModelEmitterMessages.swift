// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The Swift model messages and model message sets: a message is a struct of its arguments and a Boolean per tag, a set holds
// its parameters and the carried message as a case of its members, and both convert to and from GES messages.
extension GesSwiftModelEmitter {
    // toMessage, init(message:) and init(lenientMessage:) of a model message or set.
    static func messageConversions(_ code: inout Code, _ type: GesModelType, _ access: String, _ values: String, _ schemas: String) {
        let set = type.kind == .messages
        let what = set ? "message set" : "message"
        code.blank()
        code.line(
            set && !type.fields.isEmpty
                ? "/// Converts the set into its GES message `\(type.name)`: its parameters, then the carried message as `message`."
                : set
                    ? "/// Converts the set into the GES message it carries."
                    : "/// Converts the record into its GES message: its arguments in declared order and the tags that are true."
        )
        code.line("\(access)func toMessage() -> GameEventScriptMessage { \(values).message(self) }")
        code.blank()
        code.line("/// Reads a GES message of the \(what) `\(type.name)` as `GesSchemaValues` encodes it: every argument in declared order with")
        code.line("/// its label.")
        code.line("/// - Throws: `GesWireError` with the code and path of the value path; another message is `wire.typeMismatch` at the empty path.")
        code.line("\(access)init(message: GameEventScriptMessage) throws(GesWireError) { self = try \(values).read\(type.name)Message(message, \"\") }")
        code.blank()
        code.line("/// Reads a GES message of the \(what) `\(type.name)` after completing it with the lenient read: arguments it leaves out")
        code.line("/// take their defaults or absence.")
        code.line("/// - Throws: `GesWireError` with the code and path of the value path, or `GesSchemaError` when the runtime no longer")
        code.line("///   parses the embedded schema sources.")
        code.line(
            "\(access)init(lenientMessage message: GameEventScriptMessage) throws { self = try Self(message: GesSchemaValues.completeMessage(\(schemas).\(member(type.name))(), message)) }"
        )
    }

    // The model of a message set: its parameters, the carried message as a case of its members and its conversions.
    static func messageSet(_ code: inout Code, _ layout: GesModelLayout, _ type: GesModelType, _ access: String, _ values: String, _ schemas: String) throws(GesGeneratorError) {
        let fields = GesModelPlanner.fields(type)
        var names: Names = []
        for field in fields {
            names.append((GesSchemaPlanner.camel(field.base), "the parameter " + field.field.name))
            nested(field.value, &names, field.field.name)
        }
        names.append(("schemaHash", "the generated member schemaHash"))
        names.append(("hashValue", "the Hashable member hashValue"))
        names.append(("hash", "the Hashable member hash(into:)"))
        names.append(("toMessage", "the generated member toMessage"))
        names.append(("message", "the generated member message"))
        names.append(("Case", GesSchemaPlanner.nestedTypeOrigin + "Case"))
        names.append(("Member", GesSchemaPlanner.nestedTypeOrigin + "Member"))
        try GesSchemaPlanner.unique(type.name, names, GesModelPlanner.topLevel(layout, swift: true))
        var cases: Names = [("selected", "the generated member selected"), ("hashValue", "the Hashable member hashValue"), ("hash", "the Hashable member hash(into:)")]
        for name in type.members { cases.append((GesSchemaPlanner.camel(name), "the case of the member " + name)) }
        try GesSchemaPlanner.unique(type.name + ".Case", cases)

        code.line("/// The model of the message set `\(type.name)`: \(!fields.isEmpty ? "its parameters and " : "")the message it carries, a plain value that is never encoded.")
        code.open("\(access)struct \(type.name): Hashable, Sendable, Codable {")
        code.line("/// The hash of the message set `\(type.name)`, which wire code of it carries too; a test that compares it notices stale generated code.")
        code.line("\(access)static let schemaHash: UInt64 = 0x\(GesSchemaLayoutJson.hex(type.hash))")
        for field in fields {
            code.blank()
            code.line("/// `\(field.field.name): \(GesModelPlanner.describe(layout, field.field))`.")
            code.line("\(access)var \(member(field.base)): \(self.type(field.value))")
        }
        code.blank()
        code.line("/// The carried message.")
        code.line("\(access)var message: Case")
        code.blank()
        let siblings = fields.contains { GesModelPlanner.sibling(fields, $0) != nil }
        code.line(
            fields.isEmpty
                ? "/// Creates a set that carries a message."
                : "/// Creates a set that carries a message, with its parameters; an absent value defaults to nil, a list to empty and a parameter with a default to it\(siblings ? ", where nil takes the parameter it names" : "")."
        )
        var parameters = fields.map { field in
            if GesModelPlanner.sibling(fields, field) != nil { return "\(label(field.base)): \(plain(field.value))? = nil" }
            if let fallback = field.field.defaultValue { return "\(label(field.base)): \(self.type(field.value)) = \(native(layout, field.value, fallback))" }
            return "\(label(field.base)): \(self.type(field.value))\(defaultValue(field.value))"
        }
        parameters.append("_ message: Case")
        code.open("\(access)init(\(parameters.joined(separator: ", "))) {")
        for index in GesModelPlanner.readOrder(fields) {
            let field = fields[index]
            let fallback = GesModelPlanner.sibling(fields, field).map { " ?? self." + member(fields[$0].base) } ?? ""
            code.line("self.\(member(field.base)) = \(member(field.base))\(fallback)")
        }
        code.line("self.message = message")
        code.close()
        messageConversions(&code, type, access, values, schemas)
        code.blank()
        code.line("/// The members of the set, in declared order; the raw value is the message's name.")
        code.open("\(access)enum Member: String, Hashable, Sendable, Codable, CaseIterable {")
        for name in type.members {
            code.line("/// The message `\(name)`.")
            code.line("case \(member(name)) = \(GesSchemaPlanner.literal(name, swift: true))")
        }
        code.close()
        code.blank()
        caseDeclaration(&code, layout, type, access)
        for field in fields { try declarations(&code, field.value, access) }
        code.close()
    }

    // The carried message of a set: an enum with a case per member and the conversions from the cases of other sets that share
    // members.
    private static func caseDeclaration(_ code: inout Code, _ layout: GesModelLayout, _ type: GesModelType, _ access: String) {
        code.line("/// The message a set carries: one case per member, holding the member's model.")
        code.open("\(access)enum Case: Hashable, Sendable, Codable {")
        for name in type.members {
            code.line("/// The message `\(name)`.")
            code.line("case \(member(name))(\(name))")
        }
        code.blank()
        code.line("/// The member the case holds.")
        code.open("\(access)var selected: Member {")
        code.line("switch self {")
        for name in type.members { code.line("case .\(member(name)): return .\(member(name))") }
        code.line("}")
        code.close()
        for other in layout.types {
            if other.kind != .messages || other.name == type.name { continue }
            let shared = other.members.filter { type.members.contains($0) }
            if shared.isEmpty { continue }
            code.blank()
            code.line("/// Takes the message of a case of `\(other.name).Case` when this set has it as a member, else nil; the parameters of the")
            code.line("/// sets stay behind.")
            code.open("\(access)init?(_ other: \(other.name).Case) {")
            code.line("switch other {")
            for name in shared { code.line("case .\(member(name))(let held): self = .\(member(name))(held)") }
            if shared.count < other.members.count { code.line("default: return nil") }
            code.line("}")
            code.close()
        }
        code.close()
    }

    // The values enum's conversion of a model message to and from a GES message.
    static func messageValues(_ code: inout Code, _ type: GesModelType, _ fields: [GesModelPlanField], _ helper: String, _ support: String) {
        let arguments = fields.filter { !$0.tag }
        let tags = fields.filter(\.tag)
        let name = literal(type.name)
        let encoded = arguments.map { field in
            "(\(literal(field.field.unlabeled ? "_" : field.field.name)), \(encode(field.value, type.name, "record." + member(field.base), helper, support, plain: true)))"
        }
        code.blank()
        code.open("static func message(_ record: \(type.name)) -> GameEventScriptMessage {")
        if tags.isEmpty {
            code.line("message(\(name), [\(encoded.joined(separator: ", "))], [])")
        } else {
            code.line("var tags: [String] = []")
            for tag in tags { code.line("if record.\(member(tag.base)) { tags.append(\(literal(tag.field.name))) }") }
            code.line("return message(\(name), [\(encoded.joined(separator: ", "))], tags)")
        }
        code.close()
        code.blank()
        // The arguments in declared order with their labels and only declared tags, as encoding a message requires, read as the
        // Record of the model's fields.
        code.open("static func read\(type.name)Message(_ message: GameEventScriptMessage, _ path: String) throws(GesWireError) -> \(type.name) {")
        code.line("guard same(message.name, \(name)) else { throw GesWireError(code: GesWireError.typeMismatch, path: path) }")
        if tags.isEmpty {
            code.line("guard message.tags.isEmpty else { throw GesWireError(code: GesWireError.unknownTag, path: path) }")
        } else {
            code.line(
                "for tag in message.tags where \(tags.map { "!same(tag, \(literal($0.field.name)))" }.joined(separator: " && ")) { throw GesWireError(code: GesWireError.unknownTag, path: path) }"
            )
        }
        code.line("let arguments = message.arguments")
        var values: [String] = []
        for (index, field) in arguments.enumerated() { values.append(argument(field, index, "arguments.count")) }
        for tag in tags { values.append(".boolean(hasTag(message, \(literal(tag.field.name))))") }
        code.line("let values: [GesValue] = [\(values.joined(separator: ", "))]")
        code.line("guard arguments.count == \(arguments.count) else { throw GesWireError(code: GesWireError.typeMismatch, path: path) }")
        code.line("return try read\(type.name)(.record(typeName: \(name), keys: \(type.name)Fields, values: values), path)")
        code.close()
    }

    // The value of the argument at an index, checked for its label; the arguments before `count` are the ones to read.
    private static func argument(_ field: GesModelPlanField, _ index: Int, _ count: String) -> String {
        "try argument(arguments, \(index), \(count), \(literal(field.field.unlabeled ? "_" : field.field.name)), fieldPath(path, \(literal(field.field.name))))"
    }

    // The values enum's conversion of a model set to and from the GES message it carries or its envelope.
    static func setValues(_ code: inout Code, _ type: GesModelType, _ fields: [GesModelPlanField], _ helper: String, _ support: String) {
        let name = literal(type.name)
        // Without parameters nothing precedes these functions in the type's section.
        if !fields.isEmpty { code.blank() }
        code.open("static func message(_ value: \(type.name).Case) -> GameEventScriptMessage {")
        code.line("switch value {")
        for member in type.members { code.line("case .\(Self.member(member))(let held): return message(held)") }
        code.line("}")
        code.close()
        code.blank()
        code.open("static func message(_ set: \(type.name)) -> GameEventScriptMessage {")
        if fields.isEmpty {
            code.line("message(set.message)")
        } else {
            var encoded = fields.map { field in
                "(\(literal(field.field.unlabeled ? "_" : field.field.name)), \(encode(field.value, type.name, "set." + member(field.base), helper, support, plain: true)))"
            }
            encoded.append("(\"message\", .message(message(set.message)))")
            code.line("message(\(name), [\(encoded.joined(separator: ", "))], [])")
        }
        code.close()
        code.blank()
        code.open("static func read\(type.name)Message(_ message: GameEventScriptMessage, _ path: String) throws(GesWireError) -> \(type.name) {")
        if fields.isEmpty {
            code.line("\(type.name)(try read\(type.name)Case(message, path))")
        } else {
            // The envelope: the parameters in declared order, then the carried message as `message`.
            code.line("guard same(message.name, \(name)) else { throw GesWireError(code: GesWireError.typeMismatch, path: path) }")
            // The envelope carries no tags of its own; the carried message keeps its tags.
            code.line("guard message.tags.isEmpty else { throw GesWireError(code: GesWireError.unknownTag, path: path) }")
            code.line("let arguments = message.arguments")
            code.line("let last = arguments.count - 1")
            code.open("guard last >= 0, same(arguments.nameAt(last), \"message\") else {")
            code.line(
                "for index in 0..<arguments.count where same(arguments.nameAt(index), \"message\") { throw GesWireError(code: GesWireError.typeMismatch, path: fieldPath(path, \"message\")) }"
            )
            code.line("throw GesWireError(code: GesWireError.missingField, path: fieldPath(path, \"message\"))")
            code.close()
            code.line("guard let carried = arguments.valueAt(last).messageValue else { throw GesWireError(code: GesWireError.typeMismatch, path: fieldPath(path, \"message\")) }")
            var values: [String] = []
            for (index, field) in fields.enumerated() { values.append(argument(field, index, "last")) }
            code.line("let values: [GesValue] = [\(values.joined(separator: ", "))]")
            code.line("guard last == \(fields.count) else { throw GesWireError(code: GesWireError.typeMismatch, path: path) }")
            code.line("let held = try read\(type.name)Case(carried, fieldPath(path, \"message\"))")
            code.line("return try read\(type.name)(.record(typeName: \(name), keys: \(type.name)Fields, values: values), path, held)")
        }
        code.close()
        code.blank()
        code.open("static func read\(type.name)Case(_ message: GameEventScriptMessage, _ path: String) throws(GesWireError) -> \(type.name).Case {")
        for member in type.members {
            code.line("if same(message.name, \(literal(member))) { return .\(Self.member(member))(try read\(member)Message(message, path)) }")
        }
        code.line("throw GesWireError(code: GesWireError.typeMismatch, path: path)")
        code.close()
    }

    // The helpers of the values enum that model messages and sets use. Names compare by their UTF-8 bytes, as GES compares
    // text by Unicode scalars rather than by canonical equivalence.
    static func messageHelpers(_ code: inout Code) {
        code.blank()
        code.line("static func same(_ name: String, _ expected: String) -> Bool { name.utf8.elementsEqual(expected.utf8) }")
        code.blank()
        code.line("static func hasTag(_ message: GameEventScriptMessage, _ tag: String) -> Bool { message.tags.contains { same($0, tag) } }")
        code.blank()
        code.line("// A GES message of generated values, whose names, labels and tags are valid by construction.")
        code.open("static func message(_ name: String, _ arguments: [(String, GesValue)], _ tags: [String]) -> GameEventScriptMessage {")
        code.line("try! GameEventScriptMessage(name: name, arguments: arguments.map { try! GesMessageArgument(name: $0.0, value: $0.1) }, tags: tags)")
        code.close()
        code.blank()
        code.line("// The value of the argument at an index with its declared label; only the arguments before `count` take part.")
        code.open(
            "static func argument(_ arguments: GesMessageArguments, _ index: Int, _ count: Int, _ label: String, _ path: String) throws(GesWireError) -> GesValue {"
        )
        code.line("guard index < count else { throw GesWireError(code: GesWireError.missingField, path: path) }")
        code.line("guard same(arguments.nameAt(index), label) else { throw GesWireError(code: GesWireError.typeMismatch, path: path) }")
        code.line("return arguments.valueAt(index)")
        code.close()
    }

    // The schemas of the model messages and sets, which the lenient read completes messages against without binding them.
    static func schemasHolder(_ code: inout Code, _ layout: GesModelLayout, _ holder: String, _ schemaSet: GesSchemaSetReference?) {
        let types = layout.types.filter { $0.kind != .schema }
        code.blank()
        code.line("/// The schemas of the model messages and sets of this file, which the lenient read completes messages against.")
        code.open("enum \(holder) {")
        if schemaSet == nil {
            code.open("static let set = Result<GesSchemaSet, any Error> {")
            code.open("try GesSchemaSet(sources: [")
            for source in layout.sources {
                code.open("GesSchemaSource(name: \(literal(source.name)), text: [")
                for line in GesSchemaPlanner.lines(source.text) { code.line(literal(line) + ",") }
                code.close("].joined(separator: \"\\n\")),")
            }
            code.close("])")
            code.close()
        }
        let set = schemaSet.map { $0.qualified + ".set" } ?? "set.get()"
        for type in types {
            code.blank()
            code.line("private static let __\(type.name) = Result<GesSchema, any Error> { try \(set).schema(\(literal(type.lookup))) }")
            code.blank()
            code.line("static func \(member(type.name))() throws -> GesSchema { try __\(type.name).get() }")
        }
        code.close()
    }
}
