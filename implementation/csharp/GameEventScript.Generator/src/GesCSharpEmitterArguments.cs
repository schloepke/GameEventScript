// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The members of a message with argument defaults: an initializer whose parameters take the defaults, and with values the
// lenient read. A message without an argument default generates neither, so its code stays as it was.
internal static partial class GesCSharpEmitter
{
    // Whether a message declares an argument default.
    internal static bool HasArgumentDefaults(GesLayoutType layout) => layout.Kind == GesSchemaKind.Message && layout.Fields.Exists(field => field.Default is not null);

    // Whether a message, a message set's parameter or a member of a message set declares an argument default.
    internal static bool Lenient(GesLayoutType type, GesSchemaLayout layout) => type.Kind == GesSchemaKind.Messages
        ? type.Fields.Exists(field => field.Default is not null) || type.Members.Exists(member => layout.Types.Find(candidate => candidate.Name == member) is { } found && HasArgumentDefaults(found))
        : HasArgumentDefaults(type);

    // The lenient read, which fills left-out arguments as GesSchemaValues.CompleteMessage does and then reads the message.
    private static void LenientMember(GesSchemaPlanner.Code code, string bind)
    {
        code.Blank();
        code.Line("/// <summary>Reads a message that may leave out arguments with a schema default and optional ones, as <see cref=\"GesSchemaValues.CompleteMessage\"/> completes it; this allocates.</summary>");
        code.Line("/// <param name=\"message\">The message, whose arguments follow the declared order.</param>");
        code.Line("/// <exception cref=\"GesWireException\">An argument without a default is missing, a label is unknown, repeated or out of order, or the message does not fit.</exception>");
        code.Line($"public void ReadMessageLenient(GameEventScriptMessage message) => ReadMessage(GesSchemaValues.CompleteMessage({bind}, message));");
    }

    // An argument the initializer takes: a value without union, Bytes or List, whose default has a C# literal.
    private static bool Takes(GesPlanField field) => field.Value.Encoding.Kind is not (GesLayoutKind.Union or GesLayoutKind.Bytes or GesLayoutKind.List)
                                                     && (field.Layout.Default is null || ArgumentDefault(field.Value, field.Layout.Default) is not null);

    // The C# literal of an argument default, null when the argument's type has none.
    private static string? ArgumentDefault(GesPlanValue value, GesLayoutDefault fallback) => (fallback.Kind, value.Encoding.Kind) switch
    {
        (GesSchemaDefaultKind.Boolean, GesLayoutKind.Boolean) => fallback.Boolean ? "true" : "false",
        (GesSchemaDefaultKind.Tag, GesLayoutKind.Tag) => NestedName(value) + "." + GesSchemaPlanner.Pascal(fallback.Value),
        (GesSchemaDefaultKind.Number, GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float) => ArgumentNumber(fallback.Value, value.Encoding.Kind == GesLayoutKind.Integer),
        _ => null
    };

    // A canonical decimal as a long or a double literal.
    internal static string ArgumentNumber(string canonical, bool integer)
    {
        var e = canonical.IndexOf('e');
        var significand = long.Parse(canonical.Substring(0, e), CultureInfo.InvariantCulture);
        var exponent = int.Parse(canonical.Substring(e + 1), CultureInfo.InvariantCulture);
        if (!integer) return GesSchemaPlanner.Decimal(significand, exponent);
        for (var power = 0; power < exponent; power++) significand *= 10;
        return significand.ToString(CultureInfo.InvariantCulture);
    }

    // The parameter name of an argument.
    private static string ArgumentParameter(GesPlanField field)
    {
        var name = GesSchemaPlanner.Camel(field.Base);
        return Keywords.Contains(name) ? "@" + name : name;
    }

    // The initializer of a message with argument defaults, whose arguments are all values it can take. C# allows a default
    // only on trailing parameters, so an argument default counts there when every later argument has one or is optional.
    private static void ArgumentInitializer(GesSchemaPlanner.Code code, GesPlanType type)
    {
        if (type.IsItem || !HasArgumentDefaults(type.Layout) || !type.Fields.TrueForAll(Takes)) return;
        var trailing = type.Fields.Count;
        while (trailing > 0 && (type.Fields[trailing - 1].Optional || type.Fields[trailing - 1].Layout.Default is not null)) trailing--;
        var parameters = new List<string>();
        for (var index = 0; index < type.Fields.Count; index++)
        {
            var field = type.Fields[index];
            var name = ArgumentParameter(field);
            var typeName = TypeName(field.Value);
            if (field.Optional) parameters.Add($"{typeName}? {name}{(index >= trailing ? " = null" : "")}");
            else if (field.Layout.Default is { } fallback && index >= trailing) parameters.Add($"{typeName} {name} = {ArgumentDefault(field.Value, fallback)}");
            else parameters.Add($"{typeName} {name}");
        }

        if (type.IsClass)
        {
            code.Blank();
            code.Line($"/// <summary>Creates a {Kind(type)} with every field at its zero value.</summary>");
            code.Line($"public {type.Name}()");
            code.Open("{");
            code.Close();
        }

        code.Blank();
        code.Line($"/// <summary>Creates a {Kind(type)} from its arguments; a trailing parameter takes the argument's schema default, an optional argument is absent when null.</summary>");
        foreach (var field in type.Fields) code.Line($"/// <param name=\"{ArgumentParameter(field).TrimStart('@')}\">The argument <c>{field.Layout.Name}</c>.</param>");
        code.Line($"public {type.Name}({string.Join(", ", parameters)}){(type.IsClass ? "" : " : this()")}");
        code.Open("{");
        foreach (var field in type.Fields)
        {
            var name = ArgumentParameter(field);
            if (!field.Optional) code.Line($"{field.Base} = {name};");
            else if (Plain(field.Value)) code.Line($"{field.Base}Optional = {name};");
            else code.Line($"if ({name} is not null) {{ Has{field.Base} = true; {field.Base} = {name}; }}");
        }

        code.Close();
    }
}
