// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The conversion of generated C# types to and from GES values. It goes through the bytes and GesSchemaValues, with the schema
// sources the file embeds, so it gives exactly the values of the value path; it allocates and serves values from scripts.
extension GesCSharpEmitter {
    static func valueMembers(_ code: inout Code, _ type: GesPlanType, _ holder: String) {
        let selfModifier = (type.kind == .messages ? type.membersAreClasses : type.isClass) ? "" : "readonly "
        let bind = "__valueSchema ??= \(holder).Bind(\(GesSchemaPlanner.literal(type.layout.name, swift: false)), BindingHash)"
        code.blank()
        code.line("private static GesBoundSchema? __valueSchema;")
        code.blank()
        if type.kind == .schema {
            code.line("/// <summary>Converts this record into a GES Record value, as <see cref=\"GesSchemaValues\"/> decodes its bytes; this allocates.</summary>")
            code.line("/// <returns>The Record.</returns>")
            code.line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>")
            code.line("public \(selfModifier)global::GameEventScript.Runtime.Values.GesValue ToValue() => GesSchemaValues.Decode(\(bind), ToBytes()).Value;")
            code.blank()
            code.line("/// <summary>Reads a Record or Map into this instance, as <see cref=\"GesSchemaValues\"/> encodes it; this allocates.</summary>")
            code.line("/// <param name=\"value\">The value.</param>")
            code.line("/// <exception cref=\"GesWireException\">The value does not fit the schema.</exception>")
            code.line("public void ReadValue(global::GameEventScript.Runtime.Values.GesValue value)")
            code.open("{")
            code.line("var schema = \(bind);")
            code.line("var buffer = new byte[MaxByteCount];")
            code.line("Read(new ReadOnlySpan<byte>(buffer, 0, GesSchemaValues.Encode(schema, value, buffer)));")
            code.close()
            return
        }
        code.line("/// <summary>Converts this record into a message, as <see cref=\"GesSchemaValues\"/> decodes its bytes; this allocates.</summary>")
        code.line("/// <returns>The message.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>")
        code.line("public \(selfModifier)GameEventScriptMessage ToMessage() => GesSchemaValues.Decode(\(bind), ToBytes()).Value.Message!;")
        code.blank()
        code.line("/// <summary>Reads a message into this instance, as <see cref=\"GesSchemaValues\"/> encodes it; this allocates.</summary>")
        code.line("/// <param name=\"message\">The message.</param>")
        code.line("/// <exception cref=\"GesWireException\">The message does not fit.</exception>")
        code.line("public void ReadMessage(GameEventScriptMessage message)")
        code.open("{")
        code.line("var schema = \(bind);")
        code.line("var buffer = new byte[MaxByteCount];")
        code.line("Read(new ReadOnlySpan<byte>(buffer, 0, GesSchemaValues.Encode(schema, global::GameEventScript.Runtime.Values.GesValue.GesMessage(message), buffer)));")
        code.close()
    }

    // The embedded sources, parsed when a type first converts a value, and bound with the parameters of this code. A binding
    // hash that differs from the generated one means that the runtime no longer binds the sources like the generator did.
    static func valuesHolder(_ code: inout Code, _ layout: GesSchemaLayout, _ holder: String) {
        code.blank()
        code.line("/// <summary>The schema sources of this file, which the conversion to and from GES values binds once per type.</summary>")
        code.line("internal static class \(holder)")
        code.open("{")
        code.line("private static GesSchemaSet? __set;")
        code.blank()
        code.line("internal static GesBoundSchema Bind(string root, ulong bindingHash)")
        code.open("{")
        code.line("var set = __set ??= GesSchemaSet.Parse(")
        code.indent()
        for (index, source) in layout.sources.enumerated() {
            code.line("new GesSchemaSource(\(GesSchemaPlanner.literal(source.name, swift: false)), string.Join(\"\\n\", new[]")
            code.open("{")
            for line in GesSchemaPlanner.lines(source.text) { code.line(GesSchemaPlanner.literal(line, swift: false) + ",") }
            code.close(index + 1 < layout.sources.count ? "}))," : "})));")
        }
        code.dedent()
        var parameters = "new GesSchemaParameters()"
        for parameter in layout.parameters {
            parameters += ".With(\(GesSchemaPlanner.literal(parameter.name, swift: false)), \(GesSchemaPlanner.literal(parameter.literal, swift: false)))"
        }
        code.line("var bound = set.Schema(root).Bind(\(parameters));")
        code.line("if (bound.BindingHash != bindingHash) throw new InvalidOperationException(\"The embedded schema sources no longer bind like the generated code.\");")
        code.line("return bound;")
        code.close()
        code.close()
    }
}
