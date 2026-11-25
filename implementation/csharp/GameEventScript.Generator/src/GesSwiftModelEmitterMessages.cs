// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The Swift model messages and model message sets: a message is a struct of its arguments and a Boolean per tag, a set holds
// its parameters and the carried message as a case of its members, and both convert to and from GES messages.
internal static partial class GesSwiftModelEmitter
{
    // toMessage, init(message:) and init(lenientMessage:) of a model message or set.
    private static void MessageConversions(GesSchemaPlanner.Code code, GesModelType type, string access, string values, string schemas)
    {
        var set = type.Kind == GesSchemaKind.Messages;
        var what = set ? "message set" : "message";
        code.Blank();
        code.Line(set && type.Fields.Count > 0
            ? $"/// Converts the set into its GES message `{type.Name}`: its parameters, then the carried message as `message`."
            : set
                ? "/// Converts the set into the GES message it carries."
                : "/// Converts the record into its GES message: its arguments in declared order and the tags that are true.");
        code.Line($"{access}func toMessage() -> GameEventScriptMessage {{ {values}.message(self) }}");
        code.Blank();
        code.Line($"/// Reads a GES message of the {what} `{type.Name}` as `GesSchemaValues` encodes it: every argument in declared order with");
        code.Line("/// its label.");
        code.Line("/// - Throws: `GesWireError` with the code and path of the value path; another message is `wire.typeMismatch` at the empty path.");
        code.Line($"{access}init(message: GameEventScriptMessage) throws(GesWireError) {{ self = try {values}.read{type.Name}Message(message, \"\") }}");
        code.Blank();
        code.Line($"/// Reads a GES message of the {what} `{type.Name}` after completing it with the lenient read: arguments it leaves out");
        code.Line("/// take their defaults or absence.");
        code.Line("/// - Throws: `GesWireError` with the code and path of the value path, or `GesSchemaError` when the runtime no longer");
        code.Line("///   parses the embedded schema sources.");
        code.Line($"{access}init(lenientMessage message: GameEventScriptMessage) throws {{ self = try Self(message: GesSchemaValues.completeMessage({schemas}.{Member(type.Name)}(), message)) }}");
    }

    // The model of a message set: its parameters, the carried message as a case of its members and its conversions.
    private static void MessageSet(GesSchemaPlanner.Code code, GesModelLayout layout, GesModelType type, string access, string values, string schemas)
    {
        var fields = GesModelPlanner.Fields(type);
        var names = new List<(string, string)>();
        foreach (var field in fields)
        {
            names.Add((GesSchemaPlanner.Camel(field.Base), "the parameter " + field.Field.Name));
            Nested(field.Value, names, field.Field.Name);
        }

        names.Add(("schemaHash", "the generated member schemaHash"));
        names.Add(("hashValue", "the Hashable member hashValue"));
        names.Add(("hash", "the Hashable member hash(into:)"));
        names.Add(("toMessage", "the generated member toMessage"));
        names.Add(("message", "the generated member message"));
        names.Add(("Case", GesSchemaPlanner.NestedTypeOrigin + "Case"));
        names.Add(("Member", GesSchemaPlanner.NestedTypeOrigin + "Member"));
        GesSchemaPlanner.Unique(type.Name, names, GesModelPlanner.TopLevel(layout, swift: true));
        var cases = new List<(string, string)> { ("selected", "the generated member selected"), ("hashValue", "the Hashable member hashValue"), ("hash", "the Hashable member hash(into:)") };
        foreach (var member in type.Members) cases.Add((GesSchemaPlanner.Camel(member), "the case of the member " + member));
        GesSchemaPlanner.Unique(type.Name + ".Case", cases);

        code.Line($"/// The model of the message set `{type.Name}`: {(fields.Count > 0 ? "its parameters and " : "")}the message it carries, a plain value that is never encoded.");
        code.Open($"{access}struct {type.Name}: Hashable, Sendable, Codable {{");
        code.Line($"/// The hash of the message set `{type.Name}`, which wire code of it carries too; a test that compares it notices stale generated code.");
        code.Line($"{access}static let schemaHash: UInt64 = 0x{GesSchemaLayoutJson.Hex(type.Hash)}");
        foreach (var field in fields)
        {
            code.Blank();
            code.Line($"/// `{field.Field.Name}: {GesModelPlanner.Describe(layout, field.Field)}`.");
            code.Line($"{access}var {Member(field.Base)}: {Type(field.Value)}");
        }

        code.Blank();
        code.Line("/// The carried message.");
        code.Line($"{access}var message: Case");
        code.Blank();
        var siblings = fields.Exists(field => GesModelPlanner.Sibling(fields, field) is not null);
        code.Line(fields.Count == 0
            ? "/// Creates a set that carries a message."
            : $"/// Creates a set that carries a message, with its parameters; an absent value defaults to nil, a list to empty and a parameter with a default to it{(siblings ? ", where nil takes the parameter it names" : "")}.");
        var parameters = new List<string>();
        foreach (var field in fields)
            parameters.Add(GesModelPlanner.Sibling(fields, field) is null
                ? $"{Label(field.Base)}: {Type(field.Value)}{(field.Field.Default is { } fallback ? " = " + Native(layout, field.Value, fallback) : Default(field.Value))}"
                : $"{Label(field.Base)}: {Plain(field.Value)}? = nil");
        parameters.Add("_ message: Case");
        code.Open($"{access}init({string.Join(", ", parameters)}) {{");
        foreach (var index in GesModelPlanner.ReadOrder(fields))
        {
            var field = fields[index];
            var sibling = GesModelPlanner.Sibling(fields, field);
            code.Line($"self.{Member(field.Base)} = {Member(field.Base)}{(sibling is { } named ? " ?? self." + Member(fields[named].Base) : "")}");
        }

        code.Line("self.message = message");
        code.Close();
        MessageConversions(code, type, access, values, schemas);
        code.Blank();
        code.Line("/// The members of the set, in declared order; the raw value is the message's name.");
        code.Open($"{access}enum Member: String, Hashable, Sendable, Codable, CaseIterable {{");
        foreach (var member in type.Members)
        {
            code.Line($"/// The message `{member}`.");
            code.Line($"case {Member(member)} = {GesSchemaPlanner.Literal(member, true)}");
        }

        code.Close();
        code.Blank();
        Case(code, layout, type, access);
        foreach (var field in fields) Declarations(code, field.Value, access);
        code.Close();
    }

    // The carried message of a set: an enum with a case per member and the conversions from the cases of other sets that share
    // members.
    private static void Case(GesSchemaPlanner.Code code, GesModelLayout layout, GesModelType type, string access)
    {
        code.Line("/// The message a set carries: one case per member, holding the member's model.");
        code.Open($"{access}enum Case: Hashable, Sendable, Codable {{");
        foreach (var member in type.Members)
        {
            code.Line($"/// The message `{member}`.");
            code.Line($"case {Member(member)}({member})");
        }

        code.Blank();
        code.Line("/// The member the case holds.");
        code.Open($"{access}var selected: Member {{");
        code.Line("switch self {");
        foreach (var member in type.Members) code.Line($"case .{Member(member)}: return .{Member(member)}");
        code.Line("}");
        code.Close();
        foreach (var other in layout.Types)
        {
            if (other.Kind != GesSchemaKind.Messages || other == type) continue;
            var shared = other.Members.FindAll(type.Members.Contains);
            if (shared.Count == 0) continue;
            code.Blank();
            code.Line($"/// Takes the message of a case of `{other.Name}.Case` when this set has it as a member, else nil; the parameters of the");
            code.Line("/// sets stay behind.");
            code.Open($"{access}init?(_ other: {other.Name}.Case) {{");
            code.Line("switch other {");
            foreach (var member in shared) code.Line($"case .{Member(member)}(let held): self = .{Member(member)}(held)");
            if (shared.Count < other.Members.Count) code.Line("default: return nil");
            code.Line("}");
            code.Close();
        }

        code.Close();
    }

    // The values enum's conversion of a model message to and from a GES message.
    private static void MessageValues(GesSchemaPlanner.Code code, GesModelType type, List<GesModelPlanField> fields, string helper, string support)
    {
        var arguments = fields.FindAll(field => !field.Tag);
        var tags = fields.FindAll(field => field.Tag);
        var name = GesSchemaPlanner.Literal(type.Name, true);
        var encoded = arguments.ConvertAll(field => $"({GesSchemaPlanner.Literal(field.Field.Unlabeled ? "_" : field.Field.Name, true)}, {Encode(field.Value, type.Name, "record." + Member(field.Base), helper, support, plain: true)})");
        code.Blank();
        code.Open($"static func message(_ record: {type.Name}) -> GameEventScriptMessage {{");
        if (tags.Count == 0) code.Line($"message({name}, [{string.Join(", ", encoded)}], [])");
        else
        {
            code.Line("var tags: [String] = []");
            foreach (var tag in tags) code.Line($"if record.{Member(tag.Base)} {{ tags.append({GesSchemaPlanner.Literal(tag.Field.Name, true)}) }}");
            code.Line($"return message({name}, [{string.Join(", ", encoded)}], tags)");
        }

        code.Close();
        code.Blank();
        // The arguments in declared order with their labels and only declared tags, as encoding a message requires, read as the
        // Record of the model's fields.
        code.Open($"static func read{type.Name}Message(_ message: GameEventScriptMessage, _ path: String) throws(GesWireError) -> {type.Name} {{");
        code.Line($"guard same(message.name, {name}) else {{ throw GesWireError(code: GesWireError.typeMismatch, path: path) }}");
        if (tags.Count == 0) code.Line("guard message.tags.isEmpty else { throw GesWireError(code: GesWireError.unknownTag, path: path) }");
        else
            code.Line($"for tag in message.tags where {string.Join(" && ", tags.ConvertAll(tag => $"!same(tag, {GesSchemaPlanner.Literal(tag.Field.Name, true)})"))} {{ throw GesWireError(code: GesWireError.unknownTag, path: path) }}");
        code.Line("let arguments = message.arguments");
        var values = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
            values.Add(Argument(arguments[index], index, "arguments.count"));
        foreach (var tag in tags) values.Add($".boolean(hasTag(message, {GesSchemaPlanner.Literal(tag.Field.Name, true)}))");
        code.Line($"let values: [GesValue] = [{string.Join(", ", values)}]");
        code.Line($"guard arguments.count == {arguments.Count.ToString(CultureInfo.InvariantCulture)} else {{ throw GesWireError(code: GesWireError.typeMismatch, path: path) }}");
        code.Line($"return try read{type.Name}(.record(typeName: {name}, keys: {type.Name}Fields, values: values), path)");
        code.Close();
    }

    // The value of the argument at an index, checked for its label; the arguments before `count` are the ones to read.
    private static string Argument(GesModelPlanField field, int index, string count)
        => $"try argument(arguments, {index.ToString(CultureInfo.InvariantCulture)}, {count}, {GesSchemaPlanner.Literal(field.Field.Unlabeled ? "_" : field.Field.Name, true)}, fieldPath(path, {GesSchemaPlanner.Literal(field.Field.Name, true)}))";

    // The values enum's conversion of a model set to and from the GES message it carries or its envelope.
    private static void SetValues(GesSchemaPlanner.Code code, GesModelType type, List<GesModelPlanField> fields, string helper, string support)
    {
        var name = GesSchemaPlanner.Literal(type.Name, true);
        // Without parameters nothing precedes these functions in the type's section.
        if (fields.Count > 0) code.Blank();
        code.Open($"static func message(_ value: {type.Name}.Case) -> GameEventScriptMessage {{");
        code.Line("switch value {");
        foreach (var member in type.Members) code.Line($"case .{Member(member)}(let held): return message(held)");
        code.Line("}");
        code.Close();
        code.Blank();
        code.Open($"static func message(_ set: {type.Name}) -> GameEventScriptMessage {{");
        if (fields.Count == 0) code.Line("message(set.message)");
        else
        {
            var encoded = fields.ConvertAll(field => $"({GesSchemaPlanner.Literal(field.Field.Unlabeled ? "_" : field.Field.Name, true)}, {Encode(field.Value, type.Name, "set." + Member(field.Base), helper, support, plain: true)})");
            encoded.Add("(\"message\", .message(message(set.message)))");
            code.Line($"message({name}, [{string.Join(", ", encoded)}], [])");
        }

        code.Close();
        code.Blank();
        code.Open($"static func read{type.Name}Message(_ message: GameEventScriptMessage, _ path: String) throws(GesWireError) -> {type.Name} {{");
        if (fields.Count == 0) code.Line($"{type.Name}(try read{type.Name}Case(message, path))");
        else
        {
            // The envelope: the parameters in declared order, then the carried message as `message`.
            code.Line($"guard same(message.name, {name}) else {{ throw GesWireError(code: GesWireError.typeMismatch, path: path) }}");
            // The envelope carries no tags of its own; the carried message keeps its tags.
            code.Line("guard message.tags.isEmpty else { throw GesWireError(code: GesWireError.unknownTag, path: path) }");
            code.Line("let arguments = message.arguments");
            code.Line("let last = arguments.count - 1");
            code.Open("guard last >= 0, same(arguments.nameAt(last), \"message\") else {");
            code.Line("for index in 0..<arguments.count where same(arguments.nameAt(index), \"message\") { throw GesWireError(code: GesWireError.typeMismatch, path: fieldPath(path, \"message\")) }");
            code.Line("throw GesWireError(code: GesWireError.missingField, path: fieldPath(path, \"message\"))");
            code.Close();
            code.Line("guard let carried = arguments.valueAt(last).messageValue else { throw GesWireError(code: GesWireError.typeMismatch, path: fieldPath(path, \"message\")) }");
            var values = new List<string>();
            for (var index = 0; index < fields.Count; index++) values.Add(Argument(fields[index], index, "last"));
            code.Line($"let values: [GesValue] = [{string.Join(", ", values)}]");
            code.Line($"guard last == {fields.Count.ToString(CultureInfo.InvariantCulture)} else {{ throw GesWireError(code: GesWireError.typeMismatch, path: path) }}");
            code.Line($"let held = try read{type.Name}Case(carried, fieldPath(path, \"message\"))");
            code.Line($"return try read{type.Name}(.record(typeName: {name}, keys: {type.Name}Fields, values: values), path, held)");
        }

        code.Close();
        code.Blank();
        code.Open($"static func read{type.Name}Case(_ message: GameEventScriptMessage, _ path: String) throws(GesWireError) -> {type.Name}.Case {{");
        foreach (var member in type.Members)
            code.Line($"if same(message.name, {GesSchemaPlanner.Literal(member, true)}) {{ return .{Member(member)}(try read{member}Message(message, path)) }}");
        code.Line("throw GesWireError(code: GesWireError.typeMismatch, path: path)");
        code.Close();
    }

    // The helpers of the values enum that model messages and sets use. Names compare by their UTF-8 bytes, as GES compares
    // text by Unicode scalars rather than by canonical equivalence.
    private static void MessageHelpers(GesSchemaPlanner.Code code)
    {
        code.Blank();
        code.Line("static func same(_ name: String, _ expected: String) -> Bool { name.utf8.elementsEqual(expected.utf8) }");
        code.Blank();
        code.Line("static func hasTag(_ message: GameEventScriptMessage, _ tag: String) -> Bool { message.tags.contains { same($0, tag) } }");
        code.Blank();
        code.Line("// A GES message of generated values, whose names, labels and tags are valid by construction.");
        code.Open("static func message(_ name: String, _ arguments: [(String, GesValue)], _ tags: [String]) -> GameEventScriptMessage {");
        code.Line("try! GameEventScriptMessage(name: name, arguments: arguments.map { try! GesMessageArgument(name: $0.0, value: $0.1) }, tags: tags)");
        code.Close();
        code.Blank();
        code.Line("// The value of the argument at an index with its declared label; only the arguments before `count` take part.");
        code.Open("static func argument(_ arguments: GesMessageArguments, _ index: Int, _ count: Int, _ label: String, _ path: String) throws(GesWireError) -> GesValue {");
        code.Line("guard index < count else { throw GesWireError(code: GesWireError.missingField, path: path) }");
        code.Line("guard same(arguments.nameAt(index), label) else { throw GesWireError(code: GesWireError.typeMismatch, path: path) }");
        code.Line("return arguments.valueAt(index)");
        code.Close();
    }

    // The schemas of the model messages and sets, which the lenient read completes messages against without binding them.
    private static void SchemasHolder(GesSchemaPlanner.Code code, GesModelLayout layout, string holder, GesSchemaSetReference? schemaSet)
    {
        var types = layout.Types.FindAll(type => type.Kind != GesSchemaKind.Schema);
        code.Blank();
        code.Line("/// The schemas of the model messages and sets of this file, which the lenient read completes messages against.");
        code.Open($"enum {holder} {{");
        if (schemaSet is null)
        {
            code.Open("static let set = Result<GesSchemaSet, any Error> {");
            code.Open("try GesSchemaSet(sources: [");
            foreach (var source in layout.Sources)
            {
                code.Open($"GesSchemaSource(name: {GesSchemaPlanner.Literal(source.Name, true)}, text: [");
                foreach (var line in source.Text.Split('\n')) code.Line(GesSchemaPlanner.Literal(line, true) + ",");
                code.Close("].joined(separator: \"\\n\")),");
            }

            code.Close("])");
            code.Close();
        }

        var set = schemaSet is null ? "set.get()" : schemaSet.Qualified + ".set";
        foreach (var type in types)
        {
            code.Blank();
            code.Line($"private static let __{type.Name} = Result<GesSchema, any Error> {{ try {set}.schema({GesSchemaPlanner.Literal(type.Lookup, true)}) }}");
            code.Blank();
            code.Line($"static func {Member(type.Name)}() throws -> GesSchema {{ try __{type.Name}.get() }}");
        }

        code.Close();
    }
}
