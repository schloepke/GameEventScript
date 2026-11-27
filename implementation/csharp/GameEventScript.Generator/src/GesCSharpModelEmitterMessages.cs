// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The model messages and model message sets: a message is a record of its arguments and a Boolean per tag, a set holds its
// parameters and the carried message as a case of its members, and both convert to and from GES messages.
internal static partial class GesCSharpModelEmitter
{
    // The members that converting to and from messages adds to a model message or set.
    private static readonly string[] MessageMembers = ["ToMessage", "FromMessage", "FromMessageLenient"];

    // The names a member of a set's Case cannot take: its own members and those of a subclass.
    private static readonly string[] CaseMembers = ["Case", "Selected", "From", "Value", "Equals", "GetHashCode", "ToString", "GetType"];

    // The layout restricted to schemas, which bindings and documents concern.
    internal static GesModelLayout Schemas(GesModelLayout layout)
    {
        var types = layout.Types.FindAll(type => type.Kind == GesSchemaKind.Schema);
        var plain = new GesModelLayout(layout.Roots.FindAll(root => types.Exists(type => type.Name == root)), types);
        plain.Sources.AddRange(layout.Sources);
        plain.Protocols.AddRange(layout.Protocols);
        return plain;
    }

    // The documentation, parameters and assignments of a constructor that takes fields in declared order. Optional parameters
    // end the list, so a default becomes one only where every later field has a default too; an optional argument of a message
    // or set defaults to absence.
    private static (List<string> Documentation, List<string> Parameters, List<string> Assignments) Constructor(GesModelLayout layout, List<GesModelPlanField> fields, List<int> order, bool absent)
    {
        bool Absent(GesModelPlanField field) => absent && field.Field.Optional && field.Field.Default is null;
        var trailing = fields.Count;
        while (trailing > 0 && (fields[trailing - 1].Field.Default is not null || Absent(fields[trailing - 1]))) trailing--;
        // A record default or an empty typed List is no constant, so a trailing parameter of one is null for it; free
        // data is a struct whose default value is empty.
        bool RecordDefault(int index) => index >= trailing && (fields[index].Field.Default is { Kind: GesSchemaDefaultKind.Record or GesSchemaDefaultKind.Empty } || GesModelPlanner.Fill(fields[index].Value, fields[index].Field.Default)) && !IsData(fields[index].Value);
        var documentation = new List<string>();
        var parameters = new List<string>();
        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            var note = GesModelPlanner.Sibling(fields, field) is { } named ? $"; null takes <c>{fields[named].Field.Name}</c>" : RecordDefault(index) ? "; null takes its default" : "";
            documentation.Add($"/// <param name=\"{GesSchemaPlanner.Camel(field.Base)}\">The {(field.Tag ? "tag" : "field")} <c>{field.Field.Name}</c>{note}.</param>");
            var optional = index >= trailing;
            parameters.Add(GesModelPlanner.Sibling(fields, field) is null && !RecordDefault(index)
                ? $"{Type(field.Value)} {Parameter(field.Base)}{(!optional ? "" : Absent(field) ? " = null" : " = " + Native(layout, field.Value, field.Field.Default!))}"
                : $"{Plain(field.Value)}? {Parameter(field.Base)}{(optional ? " = null" : "")}");
        }

        var assignments = new List<string>();
        foreach (var index in order)
        {
            var field = fields[index];
            var fallback = GesModelPlanner.Sibling(fields, field) is { } named ? " ?? " + fields[named].Base : RecordDefault(index) ? " ?? " + Native(layout, field.Value, field.Field.Default!) : "";
            assignments.Add($"{field.Base} = {Parameter(field.Base)}{fallback};");
        }

        return (documentation, parameters, assignments);
    }

    // ToMessage, FromMessage and FromMessageLenient of a model message or set.
    private static void MessageConversions(GesSchemaPlanner.Code code, GesModelType type, string values, string schemas)
    {
        var what = type.Kind == GesSchemaKind.Messages ? "message set" : "message";
        code.Blank();
        code.Line(type.Kind == GesSchemaKind.Messages && type.Fields.Count > 0
            ? $"/// <summary>Converts the set into its GES message <c>{type.Name}</c>: its parameters, then the carried message as <c>message</c>.</summary>"
            : type.Kind == GesSchemaKind.Messages
                ? "/// <summary>Converts the set into the GES message it carries.</summary>"
                : "/// <summary>Converts the record into its GES message: its arguments in declared order and the tags that are true.</summary>");
        code.Line("/// <returns>The message.</returns>");
        code.Line($"public GameEventScriptMessage ToMessage() => {values}.Message(this);");
        code.Blank();
        code.Line($"/// <summary>Reads a GES message of the {what} <c>{type.Name}</c> as <see cref=\"GesSchemaValues\"/> encodes it: every argument in");
        code.Line("/// declared order with its label.</summary>");
        code.Line("/// <param name=\"message\">The message.</param>");
        code.Line($"/// <returns>The {(type.Kind == GesSchemaKind.Messages ? "set" : "record")}.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">The message does not convert, with the code and path of the value path; another message is");
        code.Line("/// <c>wire.typeMismatch</c> at the empty path.</exception>");
        code.Line($"public static {type.Name} FromMessage(GameEventScriptMessage message) => {values}.Read{type.Name}Message(message ?? throw new global::System.ArgumentNullException(nameof(message)), \"\");");
        code.Blank();
        code.Line($"/// <summary>Reads a GES message of the {what} <c>{type.Name}</c> after completing it with the lenient read: arguments it");
        code.Line("/// leaves out take their defaults or absence.</summary>");
        code.Line("/// <param name=\"message\">The message.</param>");
        code.Line($"/// <returns>The {(type.Kind == GesSchemaKind.Messages ? "set" : "record")}.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">The message does not convert, with the code and path of the value path.</exception>");
        code.Line("/// <exception cref=\"GesSchemaException\">The runtime no longer parses the embedded schema sources.</exception>");
        code.Line($"public static {type.Name} FromMessageLenient(GameEventScriptMessage message) => FromMessage(GesSchemaValues.CompleteMessage({schemas}.{type.Name}, message ?? throw new global::System.ArgumentNullException(nameof(message))));");
    }

    // The model of a message set: its parameters, the carried message as a case of its members and its conversions.
    private static void MessageSet(GesSchemaPlanner.Code code, GesModelLayout layout, GesModelType type, string access, string support, string qualifier, string values, string schemas)
    {
        var fields = GesModelPlanner.Fields(type);
        var names = new List<(string, string)> { (type.Name, "the type itself") };
        foreach (var reserved in Reserved) names.Add((reserved, "the inherited member " + reserved));
        names.Add(("SchemaHash", "the generated member SchemaHash"));
        foreach (var reserved in MessageMembers) names.Add((reserved, "the generated member " + reserved));
        names.Add(("Message", "the generated member Message"));
        names.Add(("Case", GesSchemaPlanner.NestedTypeOrigin + "Case"));
        names.Add(("Member", GesSchemaPlanner.NestedTypeOrigin + "Member"));
        foreach (var field in fields)
        {
            names.Add((field.Base, "the parameter " + field.Field.Name));
            Nested(field.Value, names, field.Field.Name);
        }

        GesSchemaPlanner.Unique(type.Name, names, GesModelPlanner.TopLevel(layout, swift: false));
        var cases = new List<(string, string)>();
        foreach (var reserved in CaseMembers) cases.Add((reserved, "the generated member " + reserved));
        foreach (var member in type.Members) cases.Add((member, "the case of the member " + member));
        GesSchemaPlanner.Unique(type.Name + ".Case", cases);

        var order = GesModelPlanner.ReadOrder(fields);
        code.Line($"/// <summary>The model of the message set <c>{type.Name}</c>: {(fields.Count > 0 ? "its parameters and " : "")}the message it carries, a plain value that is never");
        code.Line("/// encoded and compares by value.</summary>");
        code.Line($"{access} sealed class {type.Name} : IEquatable<{type.Name}>");
        code.Open("{");
        code.Line($"/// <summary>The hash of the message set <c>{type.Name}</c>, which wire code of it carries too; a test that compares it notices stale generated code.</summary>");
        code.Line($"public const ulong SchemaHash = 0x{GesSchemaLayoutJson.Hex(type.Hash)}UL;");
        code.Blank();
        code.Line($"/// <summary>Creates a set that carries a message{(fields.Count > 0 ? ", with its parameters" : "")}.</summary>");
        code.Line("/// <param name=\"message\">The carried message.</param>");
        var (documentation, parameters, assignments) = Constructor(layout, fields, order, true);
        foreach (var line in documentation) code.Line(line.Replace("The field <c>", "The parameter <c>"));
        parameters.Insert(0, "Case message");
        code.Line($"public {type.Name}({string.Join(", ", parameters)})");
        code.Open("{");
        code.Line("Message = message ?? throw new global::System.ArgumentNullException(nameof(message));");
        foreach (var line in assignments) code.Line(line);
        code.Close();
        code.Blank();
        code.Line("/// <summary>The carried message.</summary>");
        code.Line("public Case Message { get; set; }");
        foreach (var field in fields)
        {
            code.Blank();
            code.Line($"/// <summary><c>{Escape(field.Field.Name + ": " + GesModelPlanner.Describe(layout, field.Field))}</c></summary>");
            code.Line($"public {Type(field.Value)} {field.Base} {{ get; set; }}");
        }

        code.Blank();
        code.Line("/// <summary>Compares the carried message and every parameter by value.</summary>");
        code.Line("/// <param name=\"other\">The other set.</param>");
        code.Line("/// <returns>Whether both hold equal values.</returns>");
        code.Line($"public bool Equals({type.Name}? other)");
        code.Open("{");
        code.Line("if (other is null) return false;");
        code.Line("if (ReferenceEquals(this, other)) return true;");
        code.Line("return Message.Equals(other.Message)" + (fields.Count == 0 ? ";" : ""));
        for (var index = 0; index < fields.Count; index++)
            code.Line("    && " + Equal(fields[index].Value, fields[index].Base, "other." + fields[index].Base, support) + (index == fields.Count - 1 ? ";" : ""));
        code.Close();
        code.Blank();
        code.Line("/// <inheritdoc />");
        code.Line($"public override bool Equals(object? obj) => obj is {type.Name} other && Equals(other);");
        code.Blank();
        code.Line("/// <inheritdoc />");
        code.Line("public override int GetHashCode()");
        code.Open("{");
        code.Line("unchecked");
        code.Open("{");
        code.Line("var hash = (17 * 31) + Message.GetHashCode();");
        foreach (var field in fields) code.Line($"hash = (hash * 31) + {Hash(field.Value, field.Base, support)};");
        code.Line("return hash;");
        code.Close();
        code.Close();
        code.Blank();
        Operators(code, type.Name, "set");
        MessageConversions(code, type, values, schemas);
        code.Blank();
        TagEnum(code, "Member", type.Members, "public", "/// <summary>The members of the set, in declared order.</summary>");
        code.Blank();
        Case(code, layout, type, qualifier);
        foreach (var field in fields) Declarations(code, field.Value, support, qualifier);
        code.Close();
    }

    // The carried message of a set: an abstract class with a sealed subclass per member and the conversions from the cases of
    // other sets that share members.
    private static void Case(GesSchemaPlanner.Code code, GesModelLayout layout, GesModelType type, string qualifier)
    {
        code.Line("/// <summary>The message a set carries: one sealed subclass per member, holding the member's model.</summary>");
        code.Line("public abstract class Case : IEquatable<Case>");
        code.Open("{");
        code.Line("private protected Case()");
        code.Open("{");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Gets the member the case holds.</summary>");
        code.Line("public abstract Member Selected { get; }");
        code.Blank();
        code.Line("/// <summary>Compares the member and its message by value.</summary>");
        code.Line("/// <param name=\"other\">The other case.</param>");
        code.Line("/// <returns>Whether both hold the same member with an equal message.</returns>");
        code.Line("public abstract bool Equals(Case? other);");
        code.Blank();
        code.Line("/// <inheritdoc />");
        code.Line("public override bool Equals(object? obj) => obj is Case other && Equals(other);");
        code.Blank();
        code.Line("/// <inheritdoc />");
        code.Line("public abstract override int GetHashCode();");
        code.Blank();
        Operators(code, "Case", "case");
        foreach (var other in layout.Types)
        {
            if (other.Kind != GesSchemaKind.Messages || other == type) continue;
            var shared = other.Members.FindAll(type.Members.Contains);
            if (shared.Count == 0) continue;
            var source = qualifier + other.Name + ".Case";
            code.Blank();
            code.Line($"/// <summary>Takes the message of a case of <see cref=\"{source}\"/> when this set has it as a member.</summary>");
            code.Line("/// <param name=\"other\">The other case.</param>");
            code.Line("/// <returns>The case of the same message, or null when this set has no such member.</returns>");
            code.Line($"public static Case? From({source} other) => other switch");
            code.Open("{");
            foreach (var member in shared) code.Line($"{source}.{member} held => new {member}(held.Value),");
            code.Line("_ => null");
            code.Close("};");
        }

        for (var index = 0; index < type.Members.Count; index++)
        {
            var member = type.Members[index];
            var model = qualifier + member;
            code.Blank();
            code.Line($"/// <summary>The message <c>{member}</c>.</summary>");
            code.Line($"public sealed class {member} : Case");
            code.Open("{");
            code.Line("/// <summary>Holds a message.</summary>");
            code.Line("/// <param name=\"value\">The message.</param>");
            code.Line($"public {member}({model} value)");
            code.Open("{");
            code.Line("Value = value ?? throw new global::System.ArgumentNullException(nameof(value));");
            code.Close();
            code.Blank();
            code.Line("/// <summary>Gets the message.</summary>");
            code.Line($"public {model} Value {{ get; }}");
            code.Blank();
            code.Line("/// <inheritdoc />");
            code.Line($"public override Member Selected => Member.{GesSchemaPlanner.Pascal(member)};");
            code.Blank();
            code.Line("/// <inheritdoc />");
            code.Line($"public override bool Equals(Case? other) => other is {member} held && Value.Equals(held.Value);");
            code.Blank();
            code.Line("/// <inheritdoc />");
            code.Line($"public override int GetHashCode() => unchecked(({index.ToString(CultureInfo.InvariantCulture)} * 31) + Value.GetHashCode());");
            code.Close();
        }

        code.Close();
    }

    // The values class's conversion of a model message to and from a GES message.
    private static void MessageValues(GesSchemaPlanner.Code code, GesModelType type, List<GesModelPlanField> fields, string record, string qualifier, string support)
    {
        var arguments = fields.FindAll(field => !field.Tag);
        code.Blank();
        code.Line($"internal static GameEventScriptMessage Message({record} record)");
        code.Open("{");
        code.Line($"var arguments = new GesMessageArgument[{Depth(arguments.Count)}];");
        for (var index = 0; index < arguments.Count; index++)
        {
            var field = arguments[index];
            var label = GesSchemaPlanner.Literal(field.Field.Unlabeled ? "_" : field.Field.Name, false);
            code.Line($"arguments[{Depth(index)}] = new GesMessageArgument({label}, {Encode(field.Value, type.Name, "record." + field.Base, qualifier, support, plain: true, pattern: "value" + Depth(index) + "_")});");
        }

        var tags = fields.FindAll(field => field.Tag);
        code.Line($"var tags = new List<string>({Depth(tags.Count)});");
        foreach (var tag in tags) code.Line($"if (record.{tag.Base}) tags.Add({GesSchemaPlanner.Literal(tag.Field.Name, false)});");
        code.Line($"return GameEventScriptMessage.Create({GesSchemaPlanner.Literal(type.Name, false)}, arguments, tags);");
        code.Close();
        code.Blank();
        // The arguments in declared order with their labels and only declared tags, as encoding a message requires, read as the
        // Record of the model's fields.
        code.Line($"internal static {record} Read{type.Name}Message(GameEventScriptMessage message, string path)");
        code.Open("{");
        code.Line($"if (message.Name != {GesSchemaPlanner.Literal(type.Name, false)}) throw new GesWireException(GesWireException.TypeMismatch, path);");
        if (tags.Count == 0) code.Line("if (message.Tags.Count > 0) throw new GesWireException(GesWireException.UnknownTag, path);");
        else
        {
            code.Line("foreach (var tag in message.Tags)");
            code.Line($"    if ({string.Join(" && ", tags.ConvertAll(tag => "tag != " + GesSchemaPlanner.Literal(tag.Field.Name, false)))}) throw new GesWireException(GesWireException.UnknownTag, path);");
        }

        code.Line($"var values = new GesValue[{Depth(fields.Count)}];");
        Arguments(code, arguments);
        code.Line($"if (message.Arguments.Count != {Depth(arguments.Count)}) throw new GesWireException(GesWireException.TypeMismatch, path);");
        for (var index = 0; index < tags.Count; index++)
            code.Line($"values[{Depth(arguments.Count + index)}] = GesValue.GesBoolean(HasTag(message, {GesSchemaPlanner.Literal(tags[index].Field.Name, false)}));");
        code.Line($"return Read{type.Name}(GesValue.GesRecord({GesSchemaPlanner.Literal(type.Name, false)}, {type.Name}Fields, values), path);");
        code.Close();
    }

    // Takes the arguments of a message into `values`, each with its declared label at its index.
    private static void Arguments(GesSchemaPlanner.Code code, List<GesModelPlanField> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var field = arguments[index];
            var at = Depth(index);
            var path = $"Path(path, {GesSchemaPlanner.Literal(field.Field.Name, false)})";
            code.Line($"if (message.Arguments.Count <= {at}) throw new GesWireException(GesWireException.MissingField, {path});");
            code.Line($"if (message.Arguments.NameAt({at}) != {GesSchemaPlanner.Literal(field.Field.Unlabeled ? "_" : field.Field.Name, false)}) throw new GesWireException(GesWireException.TypeMismatch, {path});");
            code.Line($"values[{at}] = message.Arguments.ValueAt({at});");
        }
    }

    // The values class's conversion of a model set to and from the GES message it carries or its envelope.
    private static void SetValues(GesSchemaPlanner.Code code, GesModelLayout layout, GesModelType type, List<GesModelPlanField> fields, string record, string qualifier, string support)
    {
        code.Blank();
        code.Line($"internal static GameEventScriptMessage Message({record} set)");
        code.Open("{");
        code.Line("var carried = set.Message switch");
        code.Open("{");
        foreach (var member in type.Members) code.Line($"{record}.Case.{member} held => Message(held.Value),");
        code.Line("_ => throw new global::System.ArgumentException(\"The set carries no message.\", nameof(set))");
        code.Close("};");
        if (fields.Count == 0) code.Line("return carried;");
        else
        {
            code.Line($"var arguments = new GesMessageArgument[{Depth(fields.Count + 1)}];");
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                var label = GesSchemaPlanner.Literal(field.Field.Unlabeled ? "_" : field.Field.Name, false);
                code.Line($"arguments[{Depth(index)}] = new GesMessageArgument({label}, {Encode(field.Value, type.Name, "set." + field.Base, qualifier, support, plain: true, pattern: "value" + Depth(index) + "_")});");
            }

            code.Line($"arguments[{Depth(fields.Count)}] = new GesMessageArgument(\"message\", GesValue.GesMessage(carried));");
            code.Line($"return GameEventScriptMessage.Create({GesSchemaPlanner.Literal(type.Name, false)}, arguments, global::System.Array.Empty<string>());");
        }

        code.Close();
        code.Blank();
        code.Line($"internal static {record} Read{type.Name}Message(GameEventScriptMessage message, string path)");
        code.Open("{");
        if (fields.Count == 0) code.Line($"return new {record}(Read{type.Name}Case(message, path));");
        else
        {
            // The envelope: the parameters in declared order, then the carried message as `message`.
            code.Line($"if (message.Name != {GesSchemaPlanner.Literal(type.Name, false)}) throw new GesWireException(GesWireException.TypeMismatch, path);");
            // The envelope carries no tags of its own; the carried message keeps its tags.
            code.Line("if (message.Tags.Count > 0) throw new GesWireException(GesWireException.UnknownTag, path);");
            code.Line("var last = message.Arguments.Count - 1;");
            code.Line("if (last < 0 || message.Arguments.NameAt(last) != \"message\")");
            code.Open("{");
            code.Line("for (var index = 0; index < message.Arguments.Count; index++)");
            code.Line("    if (message.Arguments.NameAt(index) == \"message\") throw new GesWireException(GesWireException.TypeMismatch, Path(path, \"message\"));");
            code.Line("throw new GesWireException(GesWireException.MissingField, Path(path, \"message\"));");
            code.Close();
            code.Line("var carried = message.Arguments.ValueAt(last).Message ?? throw new GesWireException(GesWireException.TypeMismatch, Path(path, \"message\"));");
            code.Line($"var values = new GesValue[{Depth(fields.Count)}];");
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                var path = $"Path(path, {GesSchemaPlanner.Literal(field.Field.Name, false)})";
                code.Line($"if (last <= {Depth(index)}) throw new GesWireException(GesWireException.MissingField, {path});");
                code.Line($"if (message.Arguments.NameAt({Depth(index)}) != {GesSchemaPlanner.Literal(field.Field.Unlabeled ? "_" : field.Field.Name, false)}) throw new GesWireException(GesWireException.TypeMismatch, {path});");
                code.Line($"values[{Depth(index)}] = message.Arguments.ValueAt({Depth(index)});");
            }

            code.Line($"if (last != {Depth(fields.Count)}) throw new GesWireException(GesWireException.TypeMismatch, path);");
            code.Line($"var held = Read{type.Name}Case(carried, Path(path, \"message\"));");
            code.Line($"return Read{type.Name}(GesValue.GesRecord({GesSchemaPlanner.Literal(type.Name, false)}, {type.Name}Fields, values), path, held);");
        }

        code.Close();
        code.Blank();
        code.Line($"private static {record}.Case Read{type.Name}Case(GameEventScriptMessage message, string path)");
        code.Open("{");
        code.Line("switch (message.Name)");
        code.Open("{");
        foreach (var member in type.Members)
        {
            code.Line($"case {GesSchemaPlanner.Literal(member, false)}:");
            code.Line($"    return new {record}.Case.{member}(Read{member}Message(message, path));");
        }

        code.Line("default:");
        code.Line("    throw new GesWireException(GesWireException.TypeMismatch, path);");
        code.Close();
        code.Close();
    }

    // The helpers of the values class that model messages and sets use.
    private static void MessageHelpers(GesSchemaPlanner.Code code)
    {
        code.Blank();
        code.Line("private static bool HasTag(GameEventScriptMessage message, string tag)");
        code.Open("{");
        code.Line("foreach (var candidate in message.Tags)");
        code.Line("    if (candidate == tag) return true;");
        code.Line("return false;");
        code.Close();
    }

    // The schemas of the model messages and sets, which the lenient read completes messages against without binding them.
    private static void SchemasHolder(GesSchemaPlanner.Code code, GesModelLayout layout, string holder, GesSchemaSetReference? schemaSet)
    {
        var types = layout.Types.FindAll(type => type.Kind != GesSchemaKind.Schema);
        code.Line("/// <summary>The schemas of the model messages and sets of this file, which the lenient read completes messages against.</summary>");
        code.Line($"internal static class {holder}");
        code.Open("{");
        foreach (var type in types) code.Line($"private static GesSchema? __{type.Name};");
        if (schemaSet is null)
        {
            code.Line("private static GesSchemaSet? __set;");
            code.Blank();
            code.Line("private static GesSchemaSet Set => __set ??= GesSchemaSet.Parse(");
            code.Indent();
            for (var index = 0; index < layout.Sources.Count; index++)
            {
                var source = layout.Sources[index];
                code.Line($"new GesSchemaSource({GesSchemaPlanner.Literal(source.Name, false)}, string.Join(\"\\n\", new[]");
                code.Open("{");
                foreach (var line in source.Text.Split('\n')) code.Line(GesSchemaPlanner.Literal(line, false) + ",");
                code.Close(index + 1 < layout.Sources.Count ? "}))," : "})));");
            }

            code.Dedent();
        }

        var set = schemaSet is null ? "Set" : schemaSet.Qualified + ".Set";
        foreach (var type in types)
        {
            code.Blank();
            code.Line($"internal static GesSchema {type.Name} => __{type.Name} ??= {set}.Schema({GesSchemaPlanner.Literal(type.Lookup, false)});");
        }

        code.Close();
    }
}
