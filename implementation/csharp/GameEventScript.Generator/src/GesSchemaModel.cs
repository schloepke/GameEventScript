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

    // The schema sources, with their file names only, which generated code that reads documents embeds.
    internal List<GesSchemaSource> Sources { get; } = [];

    // The schemas whose fields reached types take unchanged with `with`, directly or through each other, by name.
    internal List<GesModelProtocol> Protocols { get; } = [];

    internal GesModelType Type(string name) => Types.Find(type => type.Name == name) ?? throw new InvalidOperationException($"Type '{name}' is not in the model.");
}

internal sealed class GesModelType
{
    internal string Name = "";
    internal ulong Hash;
    internal List<GesModelField> Fields = [];
    internal List<GesLayoutConstant> Constants = [];

    // The schemas it takes unchanged with `with`, whose protocols or interfaces it adopts, in declared order.
    internal List<string> Takes = [];

    // The fields written `computed by`, which models derive from their other fields.
    internal GesResolvedComputed[] Computed = [];

    // The wire layout of a packed schema, whose fields' encodings place them in its word; null for other types.
    internal GesSchemaLayout? Packed;
}

// A schema that types take with `with`: generated as a protocol (Swift) or interface (C#) with read-only properties. It
// refines the protocols of the schemas it takes unchanged and requires its other fields whose type is shared by name, as
// every field's type is except a union, Flags and a closed Tag written in place, which generated code declares per field.
internal sealed class GesModelProtocol
{
    internal string Name = "";
    internal bool Abstract;
    internal List<string> Takes = [];
    internal List<GesModelField> Fields = [];
}

internal sealed class GesModelField
{
    internal string Name = "";
    internal string Path = "";
    internal bool Optional;
    internal bool Key;
    internal GesLayoutDefault? Default;
    internal GesModelValue Value = new();

    // Whether the member takes the suffix Value because a schema lends the field with `with` and the field is named like that
    // schema or one that takes it, so that the interface or protocol and every type taking it name it alike.
    internal bool Renamed;
}

internal enum GesModelKind
{
    Boolean,
    Number,
    Tag,
    Text,
    Bytes,
    List,
    Dice,
    Point,
    Vector,
    Schema,
    Union,
    Nothing,
    Flags,
    Box,
    Poly,
    Disc
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

    // A closed tag set, empty for any tag, or the members of Flags; the documented maximum of a Tag, Text, List or Dice.
    internal List<string> Tags = [];
    internal string TagSet = "";
    internal long? Max;
    internal GesModelField? Element;

    // The depth of a recursive List, 0 for every other List.
    internal int Depth;

    // The axes a Point or Vector declares, empty without an axis block; union alternatives; a schema reference.
    internal List<string> Axes = [];
    internal List<GesModelField> Alternatives = [];
    internal string Schema = "";

    // Whether the schema is packed, so that its type is a value type over one word.
    internal bool Packed;
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
            if (schema.IsAbstract) throw new GesGeneratorException(GesGeneratorException.Usage, $"The model root '{root}' is abstract; only its protocol is generated, where a model takes it.");
            rootNames.Add(root);
            Reach(schema, reached);
        }

        var types = new List<GesModelType>();
        foreach (var schema in reached.Values)
        {
            var type = new GesModelType { Name = schema.Name, Hash = schema.Hash, Constants = GesLayoutConstant.Of(schema), Computed = schema.Declaration.Computed };
            if (schema.PackedWidth > 0) type.Packed = GesSchemaLayoutBuilder.Build(set, [schema.Name], new GesSchemaParameters());
            foreach (var info in schema.Fields)
            {
                var field = Field(info.Name, info.Name, info.Alternatives);
                field.Optional = info.IsOptional;
                field.Key = info.IsKey;
                field.Default = GesLayoutDefault.Of(info);
                type.Fields.Add(field);
            }

            foreach (var take in schema.Takes)
                if (take.IsUnchanged) type.Takes.Add(take.Schema.Name);
            types.Add(type);
        }

        var layout = new GesModelLayout(rootNames, types);
        foreach (var source in set.Sources) layout.Sources.Add(new GesSchemaSource(source.Name.Substring(source.Name.LastIndexOfAny(['/', '\\']) + 1), source.Text));
        var protocols = new SortedDictionary<string, GesSchema>(StringComparer.Ordinal);
        foreach (var schema in reached.Values) Protocols(schema, protocols);
        foreach (var schema in protocols.Values)
        {
            var protocol = new GesModelProtocol { Name = schema.Name, Abstract = schema.IsAbstract };
            var inherited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var take in schema.Takes)
            {
                if (!take.IsUnchanged) continue;
                protocol.Takes.Add(take.Schema.Name);
                foreach (var field in take.Schema.Fields) inherited.Add(field.Name);
            }

            foreach (var info in schema.Fields)
            {
                if (inherited.Contains(info.Name)) continue;
                var field = Field(info.Name, info.Name, info.Alternatives);
                field.Optional = info.IsOptional;
                field.Key = info.IsKey;
                if (Shared(field.Value)) protocol.Fields.Add(field);
            }

            layout.Protocols.Add(protocol);
        }

        RenameLent(layout);
        return layout;
    }

    // A field that a schema lends with `with` takes one member name in its interface or protocol and in every type that takes
    // it: like a field named after its own type, it is renamed where it is named like the lending schema or one taking it.
    private static void RenameLent(GesModelLayout layout)
    {
        foreach (var protocol in layout.Protocols)
        {
            var owners = new HashSet<string>(StringComparer.Ordinal) { protocol.Name };
            for (var grew = true; grew;)
            {
                grew = false;
                foreach (var other in layout.Protocols)
                    if (!owners.Contains(other.Name) && other.Takes.Exists(owners.Contains)) grew |= owners.Add(other.Name);
                foreach (var type in layout.Types)
                    if (!owners.Contains(type.Name) && type.Takes.Exists(owners.Contains)) grew |= owners.Add(type.Name);
            }

            foreach (var field in protocol.Fields)
            {
                if (!owners.Contains(GesSchemaPlanner.Pascal(field.Name))) continue;
                field.Renamed = true;
                foreach (var type in layout.Types)
                    if (owners.Contains(type.Name))
                        foreach (var taken in type.Fields)
                            if (taken.Name == field.Name) taken.Renamed = true;
            }
        }
    }

    private static void Protocols(GesSchema schema, SortedDictionary<string, GesSchema> protocols)
    {
        foreach (var take in schema.Takes)
            if (take.IsUnchanged && !protocols.ContainsKey(take.Schema.Name))
            {
                protocols.Add(take.Schema.Name, take.Schema);
                Protocols(take.Schema, protocols);
            }
    }

    // Whether generated code names the field's type alike in every type: not a union, Flags or a closed Tag written in place.
    private static bool Shared(GesModelValue value)
    {
        switch (value.Kind)
        {
            case GesModelKind.Union:
                var present = value.Alternatives.FindAll(alternative => alternative.Value.Kind != GesModelKind.Nothing);
                return present.Count == 1 && Shared(present[0].Value);
            case GesModelKind.Flags:
                return false;
            case GesModelKind.Tag:
                return value.Tags.Count == 0 || value.TagSet.Length > 0;
            case GesModelKind.List:
                return value.Element is null || Shared(value.Element.Value);
            default:
                return true;
        }
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
            var type = alternative.Kind == GesSchemaTypeKind.Reference ? alternative.Schema!.Name : alternative.Kind.ToString();
            var label = alternative.Label.Length > 0 ? alternative.Label : type;
            field.Value.Alternatives.Add(new GesModelField { Name = label, Path = path + ":" + type, Value = Value(alternative, path + ":" + type) });
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
                value.TagSet = type.TagSet;
                value.Max = type.Max;
                break;
            case GesSchemaTypeKind.Flags:
                value.Kind = GesModelKind.Flags;
                value.Tags.AddRange(type.Tags);
                break;
            case GesSchemaTypeKind.Text:
                value.Kind = GesModelKind.Text;
                value.Max = type.Max;
                break;
            case GesSchemaTypeKind.Bytes:
                value.Kind = GesModelKind.Bytes;
                value.Max = type.Max;
                break;
            case GesSchemaTypeKind.List when type.Element.Count > 0:
                value.Kind = GesModelKind.List;
                value.Max = type.Max;
                value.Depth = type.Depth;
                value.Element = Field("[]", path + "[]", type.Element);
                break;
            case GesSchemaTypeKind.Dice:
                value.Kind = GesModelKind.Dice;
                value.Max = type.Max;
                value.Lower = Bound(type.Lower);
                value.Upper = Bound(type.Upper);
                break;
            case GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector or GesSchemaTypeKind.Box or GesSchemaTypeKind.Poly or GesSchemaTypeKind.Disc:
                value.Kind = type.Kind switch
                {
                    GesSchemaTypeKind.Point => GesModelKind.Point,
                    GesSchemaTypeKind.Vector => GesModelKind.Vector,
                    GesSchemaTypeKind.Box => GesModelKind.Box,
                    GesSchemaTypeKind.Disc => GesModelKind.Disc,
                    _ => GesModelKind.Poly
                };
                value.Unit = GesSchemaLayoutBuilder.UnitName(type.Kind, type.Unit);
                value.Max = type.Max;
                foreach (var axis in type.Axes) value.Axes.Add(axis.Name);
                break;
            case GesSchemaTypeKind.Reference:
                value.Kind = GesModelKind.Schema;
                value.Schema = type.Schema!.Name;
                value.Packed = type.Schema.PackedWidth > 0;
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
            if (type.Packed?.Type(type.Name) is { } packed)
            {
                json.Number("packedWidth", packed.PackedWidth);
                json.Number("packedBits", packed.PackedBits);
            }

            json.BeginArray("fields");
            foreach (var field in type.Fields) Field(json, field, member: true);
            json.EndArray();
            GesSchemaLayoutJson.Constants(json, type.Constants);
            GesSchemaLayoutJson.Computed(json, type.Computed);
            if (type.Takes.Count > 0) json.Strings("takes", type.Takes);
            json.EndObject();
        }

        json.EndArray();
        if (layout.Protocols.Count > 0)
        {
            json.BeginArray("protocols");
            foreach (var protocol in layout.Protocols)
            {
                json.BeginObject();
                json.String("name", protocol.Name);
                json.Boolean("abstract", protocol.Abstract);
                json.Strings("takes", protocol.Takes);
                json.BeginArray("fields");
                foreach (var field in protocol.Fields) Field(json, field, member: true);
                json.EndArray();
                json.EndObject();
            }

            json.EndArray();
        }

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
            field.Default?.Write(json);
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
                if (value.TagSet.Length > 0) json.String("tagSet", value.TagSet);
                Optional(json, "max", value.Max);
                break;
            case GesModelKind.Flags:
                json.Strings("tags", value.Tags);
                break;
            case GesModelKind.Text or GesModelKind.Bytes:
                Optional(json, "max", value.Max);
                break;
            case GesModelKind.List:
                Optional(json, "max", value.Max);
                if (value.Depth > 0) json.Number("depth", value.Depth);
                json.Key("element");
                Field(json, value.Element!, member: false, value: true);
                break;
            case GesModelKind.Dice:
                Optional(json, "max", value.Max);
                Optional(json, "lower", value.Lower);
                Optional(json, "upper", value.Upper);
                break;
            case GesModelKind.Point or GesModelKind.Vector or GesModelKind.Box or GesModelKind.Disc:
                json.String("unit", value.Unit);
                json.Strings("axes", value.Axes);
                break;
            case GesModelKind.Poly:
                json.String("unit", value.Unit);
                json.Strings("axes", value.Axes);
                Optional(json, "max", value.Max);
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
        GesModelKind.Flags => "flags",
        GesModelKind.Text => "text",
        GesModelKind.Bytes => "bytes",
        GesModelKind.List => "list",
        GesModelKind.Dice => "dice",
        GesModelKind.Point => "point",
        GesModelKind.Vector => "vector",
        GesModelKind.Box => "box",
        GesModelKind.Poly => "poly",
        GesModelKind.Disc => "disc",
        GesModelKind.Schema => "schema",
        GesModelKind.Union => "union",
        _ => "nothing"
    };
}
