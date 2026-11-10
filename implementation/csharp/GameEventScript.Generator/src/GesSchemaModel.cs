// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The language-neutral model layout: every schema reached from the model roots, described without binding, since a model
// type is a plain value that is never encoded. Model emitters read only this, and its JSON form is compared between ports.
internal sealed class GesModelLayout
{
    internal const int Version = 1;

    internal GesModelLayout(List<string> roots, List<GesModelType> types)
    {
        Roots = roots;
        Types = types;
    }

    internal List<string> Roots { get; }

    internal List<GesModelType> Types { get; }

    internal GesModelType Type(string name) => Types.Find(type => type.Name == name) ?? throw new InvalidOperationException($"Type '{name}' is not in the model.");
}

internal sealed class GesModelType
{
    internal string Name = "";
    internal ulong Hash;
    internal List<GesModelField> Fields = [];
}

internal sealed class GesModelField
{
    internal string Name = "";
    internal string Path = "";
    internal bool Optional;
    internal bool Key;
    internal GesModelValue Value = new();
}

internal enum GesModelKind
{
    Boolean,
    Number,
    Tag,
    Text,
    List,
    Dice,
    Point,
    Vector,
    Schema,
    Union,
    Nothing
}

internal sealed class GesModelValue
{
    internal GesModelKind Kind;

    // Numbers: Number, Quantity or Percentage, the unit and the documented limits as canonical decimals or parameter names.
    internal string Type = "";
    internal string Unit = "none";
    internal bool Integer;
    internal bool Wrapping;
    internal string? Lower;
    internal string? Upper;
    internal string? Step;

    // A closed tag set, empty for any tag; the documented maximum of a Tag, Text, List or Dice.
    internal List<string> Tags = [];
    internal long? Max;
    internal GesModelField? Element;

    // The axes a Point or Vector declares, empty without an axis block; union alternatives; a schema reference.
    internal List<string> Axes = [];
    internal List<GesModelField> Alternatives = [];
    internal string Schema = "";
}

internal static class GesModelLayoutBuilder
{
    internal static GesModelLayout Build(GesSchemaSet set, IReadOnlyList<string> roots)
    {
        if (roots.Count == 0) throw new GesGeneratorException(GesGeneratorException.Usage, "Specify at least one root.");
        var reached = new SortedDictionary<string, GesSchema>(StringComparer.Ordinal);
        var rootNames = new List<string>();
        foreach (var root in roots)
        {
            if (rootNames.Contains(root)) continue;
            var schema = set.Schema(root);
            if (schema.Kind != GesSchemaKind.Schema) throw new GesGeneratorException(GesGeneratorException.Usage, $"The model root '{root}' is no schema.");
            rootNames.Add(root);
            Reach(schema, reached);
        }

        var types = new List<GesModelType>();
        foreach (var schema in reached.Values)
        {
            var type = new GesModelType { Name = schema.Name, Hash = schema.Hash };
            foreach (var info in schema.Fields)
            {
                var field = Field(info.Name, info.Name, info.Alternatives);
                field.Optional = info.IsOptional;
                field.Key = info.IsKey;
                type.Fields.Add(field);
            }

            types.Add(type);
        }

        return new GesModelLayout(rootNames, types);
    }

    private static void Reach(GesSchema schema, SortedDictionary<string, GesSchema> reached)
    {
        if (reached.ContainsKey(schema.Name)) return;
        reached.Add(schema.Name, schema);
        foreach (var field in schema.Fields)
            foreach (var type in field.Alternatives) Reach(type, reached);
    }

    private static void Reach(GesSchemaType type, SortedDictionary<string, GesSchema> reached)
    {
        if (type.Schema is { } schema) Reach(schema, reached);
        foreach (var element in type.Element) Reach(element, reached);
    }

    private static GesModelField Field(string name, string path, GesReadOnlyArray<GesSchemaType> alternatives)
    {
        var field = new GesModelField { Name = name, Path = path };
        if (alternatives.Count == 1)
        {
            field.Value = Value(alternatives[0], path);
            return field;
        }

        field.Value = new GesModelValue { Kind = GesModelKind.Union };
        foreach (var alternative in alternatives)
        {
            var label = alternative.Kind == GesSchemaTypeKind.Reference ? alternative.Schema!.Name : alternative.Kind.ToString();
            field.Value.Alternatives.Add(new GesModelField { Name = label, Path = path + ":" + label, Value = Value(alternative, path + ":" + label) });
        }

        return field;
    }

    private static GesModelValue Value(GesSchemaType type, string path)
    {
        var value = new GesModelValue();
        switch (type.Kind)
        {
            case GesSchemaTypeKind.Boolean:
                value.Kind = GesModelKind.Boolean;
                break;
            case GesSchemaTypeKind.Number or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Quantity:
                value.Kind = GesModelKind.Number;
                value.Type = GesSchemaLayoutBuilder.TypeName(type.Kind);
                value.Unit = GesSchemaLayoutBuilder.UnitName(type.Kind, type.Unit);
                value.Integer = type.IsInteger;
                value.Wrapping = type.IsWrapping;
                value.Lower = Bound(type.Lower);
                value.Upper = Bound(type.Upper);
                value.Step = Bound(type.Step);
                break;
            case GesSchemaTypeKind.Tag:
                value.Kind = GesModelKind.Tag;
                value.Tags.AddRange(type.Tags);
                value.Max = type.Max;
                break;
            case GesSchemaTypeKind.Text:
                value.Kind = GesModelKind.Text;
                value.Max = type.Max;
                break;
            case GesSchemaTypeKind.List when type.Element.Count > 0:
                value.Kind = GesModelKind.List;
                value.Max = type.Max;
                value.Element = Field("[]", path + "[]", type.Element);
                break;
            case GesSchemaTypeKind.Dice:
                value.Kind = GesModelKind.Dice;
                value.Max = type.Max;
                value.Lower = Bound(type.Lower);
                value.Upper = Bound(type.Upper);
                break;
            case GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector:
                value.Kind = type.Kind == GesSchemaTypeKind.Point ? GesModelKind.Point : GesModelKind.Vector;
                value.Unit = GesSchemaLayoutBuilder.UnitName(type.Kind, type.Unit);
                foreach (var axis in type.Axes) value.Axes.Add(axis.Name);
                break;
            case GesSchemaTypeKind.Reference:
                value.Kind = GesModelKind.Schema;
                value.Schema = type.Schema!.Name;
                break;
            case GesSchemaTypeKind.Nothing:
                value.Kind = GesModelKind.Nothing;
                break;
            default:
                var what = type.Kind == GesSchemaTypeKind.List ? "A List without an element type" : $"A {type.Kind}";
                throw new GesGeneratorException(GesGeneratorException.Unsupported, $"{what} at '{path}' has no model type.");
        }

        return value;
    }

    private static string? Bound(GesSchemaBound? bound)
    {
        if (bound is not { } value) return null;
        return value.IsParameter ? value.Parameter : value.Significand.ToString(CultureInfo.InvariantCulture) + "e" + value.Exponent.ToString(CultureInfo.InvariantCulture);
    }
}

// The canonical JSON form of a model layout, in the manner of the generation layout.
internal static class GesModelLayoutJson
{
    internal static string Write(GesModelLayout layout)
    {
        var json = new GesSchemaLayoutJson.Writer();
        json.BeginObject();
        json.Number("model", GesModelLayout.Version);
        json.Strings("roots", layout.Roots);
        json.BeginArray("types");
        foreach (var type in layout.Types)
        {
            json.BeginObject();
            json.String("name", type.Name);
            json.String("hash", GesSchemaLayoutJson.Hex(type.Hash));
            json.BeginArray("fields");
            foreach (var field in type.Fields) Field(json, field, member: true);
            json.EndArray();
            json.EndObject();
        }

        json.EndArray();
        json.EndObject();
        return json.ToString();
    }

    private static void Field(GesSchemaLayoutJson.Writer json, GesModelField field, bool member, bool value = false)
    {
        if (value) json.BeginValueObject();
        else json.BeginObject();
        json.String("name", field.Name);
        json.String("path", field.Path);
        if (member)
        {
            json.Boolean("optional", field.Optional);
            json.Boolean("key", field.Key);
        }

        json.Key("type");
        Value(json, field.Value);
        json.EndObject();
    }

    private static void Value(GesSchemaLayoutJson.Writer json, GesModelValue value)
    {
        json.BeginValueObject();
        json.String("kind", Kind(value.Kind));
        switch (value.Kind)
        {
            case GesModelKind.Number:
                json.String("type", value.Type);
                json.String("unit", value.Unit);
                json.Boolean("integer", value.Integer);
                json.Boolean("wrapping", value.Wrapping);
                Optional(json, "lower", value.Lower);
                Optional(json, "upper", value.Upper);
                Optional(json, "step", value.Step);
                break;
            case GesModelKind.Tag:
                json.Strings("tags", value.Tags);
                Optional(json, "max", value.Max);
                break;
            case GesModelKind.Text:
                Optional(json, "max", value.Max);
                break;
            case GesModelKind.List:
                Optional(json, "max", value.Max);
                json.Key("element");
                Field(json, value.Element!, member: false, value: true);
                break;
            case GesModelKind.Dice:
                Optional(json, "max", value.Max);
                Optional(json, "lower", value.Lower);
                Optional(json, "upper", value.Upper);
                break;
            case GesModelKind.Point or GesModelKind.Vector:
                json.String("unit", value.Unit);
                json.Strings("axes", value.Axes);
                break;
            case GesModelKind.Schema:
                json.String("name", value.Schema);
                break;
            case GesModelKind.Union:
                json.BeginArray("alternatives");
                foreach (var alternative in value.Alternatives) Field(json, alternative, member: false);
                json.EndArray();
                break;
        }

        json.EndObject();
    }

    private static void Optional(GesSchemaLayoutJson.Writer json, string key, string? value)
    {
        if (value is null) json.Null(key);
        else json.String(key, value);
    }

    private static void Optional(GesSchemaLayoutJson.Writer json, string key, long? value)
    {
        if (value is { } number) json.Number(key, number);
        else json.Null(key);
    }

    internal static string Kind(GesModelKind kind) => kind switch
    {
        GesModelKind.Boolean => "boolean",
        GesModelKind.Number => "number",
        GesModelKind.Tag => "tag",
        GesModelKind.Text => "text",
        GesModelKind.List => "list",
        GesModelKind.Dice => "dice",
        GesModelKind.Point => "point",
        GesModelKind.Vector => "vector",
        GesModelKind.Schema => "schema",
        GesModelKind.Union => "union",
        _ => "nothing"
    };
}
