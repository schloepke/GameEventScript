// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Generator;

// The conversion of generated C# types to and from GES values. It goes through the bytes and GesSchemaValues, with the schema
// sources the file embeds, so it gives exactly the values of the value path; it allocates and serves values from scripts.
internal static partial class GesCSharpEmitter
{
    private static void ValueMembers(GesSchemaPlanner.Code code, GesPlanType type, string holder)
    {
        var self = (type.Kind == GesSchemaKind.Messages ? type.MembersAreClasses : type.IsClass) ? "" : "readonly ";
        var bind = $"__valueSchema ??= {holder}.Bind({GesSchemaPlanner.Literal(type.Layout.Name, false)}, BindingHash)";
        code.Blank();
        code.Line("private static GesBoundSchema? __valueSchema;");
        code.Blank();
        if (type.Kind == GesSchemaKind.Schema)
        {
            code.Line("/// <summary>Converts this record into a GES Record value, as <see cref=\"GesSchemaValues\"/> decodes its bytes; this allocates.</summary>");
            code.Line("/// <returns>The Record.</returns>");
            code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>");
            code.Line($"public {self}global::GameEventScript.Runtime.Values.GesValue ToValue() => GesSchemaValues.Decode({bind}, ToBytes()).Value;");
            code.Blank();
            code.Line("/// <summary>Reads a Record or Map into this instance, as <see cref=\"GesSchemaValues\"/> encodes it; this allocates.</summary>");
            code.Line("/// <param name=\"value\">The value.</param>");
            code.Line("/// <exception cref=\"GesWireException\">The value does not fit the schema.</exception>");
            code.Line("public void ReadValue(global::GameEventScript.Runtime.Values.GesValue value)");
            code.Open("{");
            code.Line($"var schema = {bind};");
            code.Line("var buffer = new byte[MaxByteCount];");
            code.Line("Read(new ReadOnlySpan<byte>(buffer, 0, GesSchemaValues.Encode(schema, value, buffer)));");
            code.Close();
            return;
        }

        code.Line("/// <summary>Converts this record into a message, as <see cref=\"GesSchemaValues\"/> decodes its bytes; this allocates.</summary>");
        code.Line("/// <returns>The message.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>");
        code.Line($"public {self}GameEventScriptMessage ToMessage() => GesSchemaValues.Decode({bind}, ToBytes()).Value.Message!;");
        code.Blank();
        code.Line("/// <summary>Reads a message into this instance, as <see cref=\"GesSchemaValues\"/> encodes it; this allocates.</summary>");
        code.Line("/// <param name=\"message\">The message.</param>");
        code.Line("/// <exception cref=\"GesWireException\">The message does not fit.</exception>");
        code.Line("public void ReadMessage(GameEventScriptMessage message)");
        code.Open("{");
        code.Line($"var schema = {bind};");
        code.Line("var buffer = new byte[MaxByteCount];");
        code.Line("Read(new ReadOnlySpan<byte>(buffer, 0, GesSchemaValues.Encode(schema, global::GameEventScript.Runtime.Values.GesValue.GesMessage(message), buffer)));");
        code.Close();
    }

    // The embedded sources, parsed when a type first converts a value, and bound with the parameters of this code. A binding
    // hash that differs from the generated one means that the runtime no longer binds the sources like the generator did.
    private static void ValuesHolder(GesSchemaPlanner.Code code, GesSchemaLayout layout, string holder)
    {
        code.Blank();
        code.Line("/// <summary>The schema sources of this file, which the conversion to and from GES values binds once per type.</summary>");
        code.Line($"internal static class {holder}");
        code.Open("{");
        code.Line("private static GesSchemaSet? __set;");
        code.Blank();
        code.Line("internal static GesBoundSchema Bind(string root, ulong bindingHash)");
        code.Open("{");
        code.Line("var set = __set ??= GesSchemaSet.Parse(");
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
        var parameters = "new GesSchemaParameters()";
        foreach (var parameter in layout.Parameters) parameters += $".With({GesSchemaPlanner.Literal(parameter.Name, false)}, {GesSchemaPlanner.Literal(parameter.Literal, false)})";
        code.Line($"var bound = set.Schema(root).Bind({parameters});");
        code.Line("if (bound.BindingHash != bindingHash) throw new InvalidOperationException(\"The embedded schema sources no longer bind like the generated code.\");");
        code.Line("return bound;");
        code.Close();
        code.Close();
    }
}
