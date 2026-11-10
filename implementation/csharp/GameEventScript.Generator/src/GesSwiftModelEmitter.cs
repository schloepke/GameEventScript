// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Generator;

// Writes Swift models: a Hashable, Sendable and Codable struct per schema with stored properties in declared order and a
// memberwise initializer, an enum per closed Tag and per union, and the runtime's GesPoint and GesVector for spatial fields.
internal static class GesSwiftModelEmitter
{
    internal static string Emit(GesModelLayout layout, GesSchemaGenerateOptions options)
    {
        var access = options.Access == GesSchemaAccess.Public ? "public " : "";
        var space = GesSchemaPlanner.Namespace(options.Namespace, GesSwiftEmitter.Keywords);
        var code = new GesSchemaPlanner.Code();
        code.Raw(GesModelPlanner.Header(layout));
        code.Line("// swift-format-ignore-file");
        code.Blank();
        code.Line("import GameEventScriptRuntime");
        code.Blank();
        foreach (var part in space)
        {
            code.Line($"/// The namespace `{part}` of generated model types.");
            code.Open($"{access}enum {part} {{");
        }

        for (var index = 0; index < layout.Types.Count; index++)
        {
            if (index > 0) code.Blank();
            Record(code, layout.Types[index], access);
        }

        foreach (var _ in space) code.Close();
        return code.ToString();
    }

    private static string Name(string name) => GesSwiftEmitter.Keywords.Contains(name) ? "`" + name + "`" : name;

    private static string Member(string pascal) => Name(GesSchemaPlanner.Camel(pascal));

    private static void Record(GesSchemaPlanner.Code code, GesModelType type, string access)
    {
        var fields = GesModelPlanner.Fields(type);
        var key = fields.Find(field => field.Field.Key);
        var names = new List<(string, string)>();
        foreach (var field in fields)
        {
            names.Add((GesSchemaPlanner.Camel(field.Base), "the field " + field.Field.Name));
            Nested(field.Value, names, field.Field.Name);
        }

        if (key is not null && GesSchemaPlanner.Camel(key.Base) != "id") names.Add(("id", "the identity of the key " + key.Field.Name));
        GesSchemaPlanner.Unique(type.Name, names);

        code.Line($"/// The model of the schema `:{type.Name}`: a plain value that is never encoded.");
        code.Open($"{access}struct {type.Name}: Hashable, Sendable, Codable{(key is null ? "" : ", Identifiable")} {{");
        var first = true;
        foreach (var field in fields)
        {
            if (!first) code.Blank();
            first = false;
            code.Line($"/// `{field.Field.Name}: {GesModelPlanner.Describe(field.Field)}`.");
            code.Line($"{access}var {Member(field.Base)}: {Type(field.Value)}");
        }

        if (key is not null && GesSchemaPlanner.Camel(key.Base) != "id")
        {
            code.Blank();
            code.Line($"/// The key `{key.Field.Name}`, which identifies the record in a keyed list.");
            code.Line($"{access}var id: {Type(key.Value)} {{ {Member(key.Base)} }}");
        }

        code.Blank();
        code.Line("/// Creates a record from its fields; an absent value defaults to nil and a list to empty.");
        var parameters = new List<string>();
        foreach (var field in fields) parameters.Add($"{Member(field.Base)}: {Type(field.Value)}{Default(field.Value)}");
        code.Open($"{access}init({string.Join(", ", parameters)}) {{");
        foreach (var field in fields) code.Line($"self.{Member(field.Base)} = {Member(field.Base)}");
        code.Close();
        foreach (var field in fields) Declarations(code, field.Value, access);
        code.Close();
    }

    private static void Nested(GesModelPlanValue value, List<(string, string)> names, string field)
    {
        if (value.Nested.Length > 0) names.Add((value.Nested, "a type of the field " + field));
        if (value.Element is not null) Nested(value.Element, names, field);
        foreach (var alternative in value.Alternatives) Nested(alternative, names, field);
    }

    private static string Default(GesModelPlanValue value) => value.Nullable ? " = nil" : value.Kind is GesModelKind.List or GesModelKind.Dice ? " = []" : "";

    internal static string Type(GesModelPlanValue value) => Plain(value) + (value.Nullable ? "?" : "");

    private static string Plain(GesModelPlanValue value) => value.Kind switch
    {
        GesModelKind.Boolean => "Bool",
        GesModelKind.Number => value.Model.Integer ? "Int" : "Double",
        GesModelKind.Tag => value.ClosedTag ? value.Nested : "String",
        GesModelKind.Text => "String",
        GesModelKind.List => $"[{Type(value.Element!)}]",
        GesModelKind.Dice => "[Int]",
        GesModelKind.Point => "GesPoint",
        GesModelKind.Vector => "GesVector",
        GesModelKind.Schema => value.Model.Schema,
        _ => value.Nested
    };

    private static void Declarations(GesSchemaPlanner.Code code, GesModelPlanValue value, string access)
    {
        if (value.ClosedTag)
        {
            code.Blank();
            code.Line("/// The tags, in declared order; the raw value is the tag's name.");
            code.Open($"{access}enum {value.Nested}: String, Hashable, Sendable, Codable, CaseIterable {{");
            foreach (var tag in value.Model.Tags)
            {
                code.Line($"/// The tag `#{tag}`.");
                code.Line($"case {Name(tag)}");
            }

            code.Close();
        }
        else if (value.Kind == GesModelKind.Union)
        {
            var cases = new List<(string, string)>();
            foreach (var alternative in value.Alternatives) cases.Add((GesSchemaPlanner.Camel(alternative.Label), "the alternative " + alternative.Label));
            GesSchemaPlanner.Unique(value.Nested, cases);
            code.Blank();
            code.Line("/// The alternatives, in declared order.");
            code.Open($"{access}enum {value.Nested}: Hashable, Sendable, Codable {{");
            foreach (var alternative in value.Alternatives)
            {
                code.Line($"/// The alternative `:{alternative.Label}`.");
                code.Line($"case {Member(alternative.Label)}({Type(alternative)})");
            }

            code.Close();
            foreach (var alternative in value.Alternatives) Declarations(code, alternative, access);
        }
        else if (value.Element is not null) Declarations(code, value.Element, access);
    }
}
