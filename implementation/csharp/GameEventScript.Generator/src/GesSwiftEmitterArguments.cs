// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Generator;

// The members of a message with argument defaults: an initializer whose parameters take the defaults, and with values the
// lenient read. A message without an argument default generates neither, so its code stays as it was.
internal static partial class GesSwiftEmitter
{
    // The lenient read, which fills left-out arguments as GesSchemaValues.completeMessage does and then reads the message.
    private static void LenientMember(GesSchemaPlanner.Code code, string access, string mutating)
    {
        code.Blank();
        code.Line("/// Reads a message that may leave out arguments with a schema default and optional ones, as `GesSchemaValues.completeMessage` completes it; this allocates.");
        code.Line("/// - Throws: `GesWireError` when an argument without a default is missing, a label is unknown, repeated or out of order, or the message does not fit, or the error of binding the embedded sources.");
        code.Open($"{access}{mutating}func read(lenientMessage message: GameEventScriptMessage) throws {{");
        code.Line("try read(message: GesSchemaValues.completeMessage(Self.__valueSchema.get(), message))");
        code.Close();
    }

    // An argument the initializer takes: a value without union, Bytes or List, whose default has a Swift literal.
    private static bool Takes(GesPlanField field) => field.Value.Encoding.Kind is not (GesLayoutKind.Union or GesLayoutKind.Bytes or GesLayoutKind.List)
                                                     && (field.Layout.Default is null || ArgumentDefault(field.Value, field.Layout.Default) is not null);

    // The Swift literal of an argument default, null when the argument's type has none.
    private static string? ArgumentDefault(GesPlanValue value, GesLayoutDefault fallback) => (fallback.Kind, value.Encoding.Kind) switch
    {
        (GameEventScript.Api.GesSchemaDefaultKind.Boolean, GesLayoutKind.Boolean) => fallback.Boolean ? "true" : "false",
        (GameEventScript.Api.GesSchemaDefaultKind.Tag, GesLayoutKind.Tag) => "." + Name(fallback.Value),
        (GameEventScript.Api.GesSchemaDefaultKind.Number, GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float) => GesCSharpEmitter.ArgumentNumber(fallback.Value, value.Encoding.Kind == GesLayoutKind.Integer),
        _ => null
    };

    // The initializer of a message with argument defaults, whose arguments are all values it can take.
    private static void ArgumentInitializer(GesSchemaPlanner.Code code, GesPlanType type, string access)
    {
        if (type.IsItem || !GesCSharpEmitter.HasArgumentDefaults(type.Layout) || !type.Fields.TrueForAll(Takes)) return;
        var parameters = new List<string>();
        foreach (var field in type.Fields)
        {
            var typeName = TypeName(field.Value);
            if (field.Optional) parameters.Add($"{Member(field.Base)}: {typeName}? = nil");
            else if (field.Layout.Default is { } fallback) parameters.Add($"{Member(field.Base)}: {typeName} = {ArgumentDefault(field.Value, fallback)}");
            else parameters.Add($"{Member(field.Base)}: {typeName}");
        }

        code.Blank();
        code.Line($"/// Creates a {Kind(type)} from its arguments; a left-out parameter takes the argument's schema default, an optional argument is absent when nil.");
        code.Open($"{access}init({string.Join(", ", parameters)}) {{");
        foreach (var field in type.Fields)
        {
            var name = Member(field.Base);
            if (!field.Optional) code.Line($"self.{name} = {name}");
            else code.Line($"if let {name} {{ self.has{field.Base} = true; self.{name} = {name} }}");
        }

        code.Close();
    }
}
