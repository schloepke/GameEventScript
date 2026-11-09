// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Generator;

// The conversion of generated Swift types to and from GES values, as in the C# output.
internal static partial class GesSwiftEmitter
{
    private static void ValueMembers(GesSchemaPlanner.Code code, GesPlanType type, string access, string holder)
    {
        var mutating = (type.Kind == GesSchemaKind.Messages ? type.MembersAreClasses : type.IsClass) ? "" : "mutating ";
        code.Blank();
        code.Line($"private static let __valueSchema = {holder}.bind({GesSchemaPlanner.Literal(type.Layout.Name, true)}, bindingHash)");
        code.Blank();
        if (type.Kind == GesSchemaKind.Schema)
        {
            code.Line("/// Converts this record into a GES Record value, as `GesSchemaValues` decodes its bytes; this allocates.");
            code.Line("/// - Throws: `GesWireError` when a value does not fit its field, or the error of binding the embedded sources.");
            code.Open($"{access}func toValue() throws -> GesValue {{");
            code.Line("try GesSchemaValues.decode(Self.__valueSchema.get(), encoded()).value");
            code.Close();
            code.Blank();
            code.Line("/// Reads a Record or Map into this instance, as `GesSchemaValues` encodes it; this allocates.");
            code.Line("/// - Throws: `GesWireError` when the value does not fit the schema, or the error of binding the embedded sources.");
            code.Open($"{access}{mutating}func read(value: GesValue) throws {{");
            code.Line("_ = try read(from: GesSchemaValues.encode(Self.__valueSchema.get(), value))");
            code.Close();
            return;
        }

        code.Line("/// Converts this record into a message, as `GesSchemaValues` decodes its bytes; this allocates.");
        code.Line("/// - Throws: `GesWireError` when a value does not fit its field, or the error of binding the embedded sources.");
        code.Open($"{access}func toMessage() throws -> GameEventScriptMessage {{");
        code.Line("try GesSchemaValues.decode(Self.__valueSchema.get(), encoded()).value.messageValue!");
        code.Close();
        code.Blank();
        code.Line("/// Reads a message into this instance, as `GesSchemaValues` encodes it; this allocates.");
        code.Line("/// - Throws: `GesWireError` when the message does not fit, or the error of binding the embedded sources.");
        code.Open($"{access}{mutating}func read(message: GameEventScriptMessage) throws {{");
        code.Line("_ = try read(from: GesSchemaValues.encode(Self.__valueSchema.get(), .message(message)))");
        code.Close();
    }

    // The embedded sources, parsed when a type first converts a value; a different binding hash fails each conversion.
    private static void ValuesHolder(GesSchemaPlanner.Code code, GesSchemaLayout layout, string holder)
    {
        code.Blank();
        code.Line("/// The schema sources of this file, which the conversion to and from GES values binds once per type.");
        code.Open($"private enum {holder} {{");
        code.Line("/// The embedded sources no longer bind like the generated code.");
        code.Line("struct Mismatch: Error {}");
        code.Blank();
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
        code.Blank();
        code.Open("static func bind(_ root: String, _ bindingHash: UInt64) -> Result<GesBoundSchema, any Error> {");
        code.Open("Result {");
        code.Line("let parameters = GesSchemaParameters()");
        foreach (var parameter in layout.Parameters) code.Line($"try parameters.with({GesSchemaPlanner.Literal(parameter.Name, true)}, literal: {GesSchemaPlanner.Literal(parameter.Literal, true)})");
        code.Line("let bound = try set.get().schema(root).bind(parameters: parameters)");
        code.Line("if bound.bindingHash != bindingHash { throw Mismatch() }");
        code.Line("return bound");
        code.Close();
        code.Close();
        code.Close();
    }
}
