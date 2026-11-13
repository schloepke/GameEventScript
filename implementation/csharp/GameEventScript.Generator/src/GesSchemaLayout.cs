// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Syntax;

namespace GameEventScript.Generator;

// The language-neutral generation layout: every type reached from the roots, bound once with the given parameters. Emitters
// read only this model, and its JSON form is compared between the ports.
internal sealed class GesSchemaLayout
{
    internal const int Version = 1;

    internal GesSchemaLayout(List<string> roots, List<GesLayoutParameter> parameters, List<GesLayoutType> types, List<GesSchemaSource> sources)
    {
        Roots = roots;
        Parameters = parameters;
        Types = types;
        Sources = sources;
    }

    internal List<string> Roots { get; }

    internal List<GesLayoutParameter> Parameters { get; }

    internal List<GesLayoutType> Types { get; }

    // The sources the schemas come from, under their file names, which code that converts GES values embeds. Not part of the JSON.
    internal List<GesSchemaSource> Sources { get; }

    internal GesLayoutType Type(string name) => Types.Find(type => type.Name == name) ?? throw new InvalidOperationException($"Type '{name}' is not in the layout.");
}

internal sealed class GesLayoutParameter(string name, string type, string unit, string value, string literal)
{
    internal string Name { get; } = name;

    internal string Type { get; } = type;

    internal string Unit { get; } = unit;

    // The exact decimal in base units, as the binding hash spells it.
    internal string Value { get; } = value;

    // The value as a literal that binds to the same hash, which generated code passes when it binds its schemas.
    internal string Literal { get; } = literal;
}

// The default of a field: its kind, `number`, `boolean`, `tag`, `field` or `record`, and its value, a canonical decimal in
// base units, a Boolean, a tag without `#` or a sibling field's name; a record lists the fields it names with their defaults.
internal sealed class GesLayoutDefault
{
    internal GesSchemaDefaultKind Kind;
    internal string Value = "";
    internal bool Boolean;
    internal List<GesLayoutDefaultEntry> Entries = [];

    internal static GesLayoutDefault? Of(GesSchemaFieldInfo field) => field.Default is { } fallback ? Of(fallback) : null;

    private static GesLayoutDefault Of(GesSchemaDefaultInfo fallback)
    {
        var result = new GesLayoutDefault
        {
            Kind = fallback.Kind,
            Boolean = fallback.Boolean,
            Value = fallback.Number is { } number
                ? number.Significand.ToString(System.Globalization.CultureInfo.InvariantCulture) + "e" + number.Exponent.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : fallback.Name
        };
        foreach (var entry in fallback.Entries) result.Entries.Add(new GesLayoutDefaultEntry { Name = entry.Name, Value = Of(entry.Value) });
        return result;
    }

    internal void Write(GesSchemaLayoutJson.Writer json)
    {
        json.Key("default");
        json.BeginValueObject();
        json.String("kind", Kind switch
        {
            GesSchemaDefaultKind.Number => "number",
            GesSchemaDefaultKind.Boolean => "boolean",
            GesSchemaDefaultKind.Tag => "tag",
            GesSchemaDefaultKind.Record => "record",
            _ => "field"
        });
        if (Kind == GesSchemaDefaultKind.Record)
        {
            json.BeginArray("entries");
            foreach (var entry in Entries)
            {
                json.BeginObject();
                json.String("name", entry.Name);
                entry.Value.Write(json);
                json.EndObject();
            }

            json.EndArray();
        }
        else if (Kind == GesSchemaDefaultKind.Boolean) json.Boolean("value", Boolean);
        else json.String("value", Value);
        json.EndObject();
    }
}

// A field a record default names, with its value.
internal sealed class GesLayoutDefaultEntry
{
    internal string Name = "";
    internal GesLayoutDefault Value = null!;
}

// A `let` of a schema: its kind, unit, exact value in base units and whether it is a whole Number within Int64.
internal sealed class GesLayoutConstant
{
    internal string Name = "";
    internal string Type = "";
    internal string Unit = "";
    internal long Significand;
    internal int Exponent;
    internal bool Integer;

    // The unit symbol or `%` that a literal of it ends with, for generated documentation; not part of the JSON.
    internal string Suffix = "";

    internal string Value => Significand.ToString(System.Globalization.CultureInfo.InvariantCulture) + "e" + Exponent.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static List<GesLayoutConstant> Of(GesSchema schema)
    {
        var constants = new List<GesLayoutConstant>();
        foreach (var constant in schema.Constants)
            constants.Add(new GesLayoutConstant
            {
                Name = constant.Name,
                Type = GesSchemaLayoutBuilder.TypeName(constant.Kind),
                Unit = GesSchemaLayoutBuilder.UnitName(constant.Kind, constant.Unit),
                Significand = constant.Value.Significand,
                Exponent = constant.Value.Exponent,
                Integer = constant.IsInteger,
                Suffix = constant.Kind == GesSchemaTypeKind.Percentage ? "%" : constant.Kind == GesSchemaTypeKind.Quantity ? constant.Unit.ToSuffix() : ""
            });
        return constants;
    }
}

internal sealed class GesLayoutType
{
    internal string Name = "";
    internal GesSchemaKind Kind;
    internal ulong Hash;
    internal ulong BindingHash;
    internal List<string> BindingHashDepends = [];
    internal long MaxByteCount;
    internal List<string> MaxByteCountDepends = [];
    internal List<GesLayoutField> Fields = [];
    internal List<GesLayoutConstant> Constants = [];

    // The fields written `computed by`, which are never encoded, with their checked expressions.
    internal GesResolvedComputed[] Computed = [];
    internal List<string> Tags = [];
    internal List<string> Members = [];
    internal int MemberWidth;

    // A packed schema's word: its width of 8, 16, 32 or 64 bits and the bits its fields take; 0 for other types.
    internal int PackedWidth;
    internal int PackedBits;
}

internal sealed class GesLayoutField
{
    internal string Name = "";
    internal string Path = "";
    internal bool Optional;
    internal bool Key;
    internal bool Unlabeled;
    internal GesLayoutDefault? Default;
    internal GesLayoutEncoding Encoding = new();
}

internal enum GesLayoutKind
{
    Boolean,
    Integer,
    Quantized,
    Float,
    Tag,
    Text,
    Bytes,
    List,
    Point,
    Vector,
    Box,
    Schema,
    Union,
    Nothing,
    Flags,
    Disc
}

internal sealed class GesLayoutEncoding
{
    internal GesLayoutKind Kind;

    // Numbers: what a value is, Number, Quantity or Percentage, and its unit.
    internal string Type = "";
    internal string Unit = "";
    internal bool Range;
    internal bool Wrapping;
    internal long Lower;
    internal ulong MaxIndex;
    internal int Width;

    // The bits a field of fixed width takes on the wire: a Boolean has no layout width but takes one bit.
    internal int Bits => Kind == GesLayoutKind.Boolean ? 1 : Width;
    internal long Numerator;
    internal long TenPower;
    internal double LowerValue;
    internal double UpperValue;
    internal ulong DeltaRange;
    internal int DeltaWidth;
    internal List<string> Depends = [];

    // Tag sets, Texts and lists. A Tag of a named tag set carries its name.
    internal List<string> Tags = [];
    internal string TagSet = "";
    internal bool TagName;
    internal long MaxCount;
    internal bool Dice;
    internal bool Poly;
    internal bool Keyed;
    internal int PositionWidth;
    internal GesLayoutField? Element;

    // A recursive List: how many records of its schema nest at most, and 0 for every other List.
    internal int Depth;

    // Point and Vector axes, the corners lower and upper of a Box or the centre and the radius of a Disc, union alternatives
    // and schema references. A Disc whose block leaves out the radius has the centre alone.
    internal List<GesLayoutField> Axes = [];
    internal List<GesLayoutField> Corners = [];
    internal List<GesLayoutField> Alternatives = [];
    internal string Schema = "";
}

// Builds the layout from bound schemas. Each reached schema is bound on its own, and a nested schema appears in its parent
// as a reference, since parameters belong to files and a schema binds the same wherever it is reached.
internal static class GesSchemaLayoutBuilder
{
    internal static GesSchemaLayout Build(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaParameters parameters)
    {
        if (roots.Count == 0) throw new GesGeneratorException(GesGeneratorException.Usage, "Specify at least one root.");
        var reached = new SortedDictionary<string, GesSchema>(StringComparer.Ordinal);
        var rootNames = new List<string>();
        var parameterInfos = new SortedDictionary<string, GesSchemaParameterInfo>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (rootNames.Contains(root)) continue;
            rootNames.Add(root);
            var schema = set.Schema(root);
            Reach(schema, reached);
            foreach (var parameter in schema.Parameters) parameterInfos[parameter.Name] = parameter;
        }

        var layoutParameters = new List<GesLayoutParameter>();
        foreach (var parameter in parameterInfos.Values)
        {
            var value = parameters.TryGet(parameter.Name, out var bound) ? bound.Number.Canonical : "";
            layoutParameters.Add(new GesLayoutParameter(parameter.Name, TypeName(parameter.Kind), UnitName(parameter.Kind, parameter.Unit), value, Literal(parameter.Kind, parameter.Unit, value)));
        }

        var types = new List<GesLayoutType>();
        foreach (var schema in reached.Values) types.Add(Type(schema, parameters));
        var sources = new List<GesSchemaSource>();
        foreach (var source in set.Sources) sources.Add(new GesSchemaSource(source.Name.Substring(source.Name.LastIndexOfAny(['/', '\\']) + 1), source.Text));
        return new GesSchemaLayout(rootNames, layoutParameters, types, sources);
    }

    private static void Reach(GesSchema schema, SortedDictionary<string, GesSchema> reached)
    {
        if (reached.ContainsKey(schema.Name)) return;
        reached.Add(schema.Name, schema);
        foreach (var member in schema.Members) Reach(member, reached);
        foreach (var field in schema.Fields)
            foreach (var type in field.Alternatives) Reach(type, reached);
    }

    private static void Reach(GesSchemaType type, SortedDictionary<string, GesSchema> reached)
    {
        if (type.Schema is { } schema) Reach(schema, reached);
        foreach (var element in type.Element) Reach(element, reached);
    }

    private static GesLayoutType Type(GesSchema schema, GesSchemaParameters parameters)
    {
        var bound = schema.Bind(parameters);
        var type = new GesLayoutType
        {
            Name = schema.Name,
            Kind = schema.Kind,
            Hash = bound.Hash,
            BindingHash = bound.BindingHash,
            MaxByteCount = bound.MaxByteCount,
            PackedWidth = schema.PackedWidth,
            PackedBits = schema.PackedBits
        };
        foreach (var parameter in schema.Parameters) type.BindingHashDepends.Add(parameter.Name);
        var depends = new SortedSet<string>(StringComparer.Ordinal);
        if (schema.Kind == GesSchemaKind.Messages)
        {
            foreach (var member in schema.Members) type.Members.Add(member.Name);
            type.MemberWidth = bound.MemberWidth;
            foreach (var member in bound.Members)
                foreach (var field in Fields(member.Schema, member)) Collect(field, depends);
        }
        else
        {
            type.Fields = Fields(schema, bound);
            type.Constants = GesLayoutConstant.Of(schema);
            type.Computed = schema.Declaration.Computed;
            foreach (var field in type.Fields) Collect(field, depends);
            foreach (var tag in schema.Tags) type.Tags.Add(tag);
        }

        type.MaxByteCountDepends.AddRange(depends);
        return type;
    }

    private static List<GesLayoutField> Fields(GesSchema schema, GesBoundSchema bound)
    {
        var fields = new List<GesLayoutField>();
        var index = 0;
        foreach (var info in schema.Fields)
        {
            var field = Field(bound.Nodes, ref index, info.Name, info.Alternatives);
            field.Unlabeled = info.IsUnlabeled;
            field.Default = GesLayoutDefault.Of(info);
            fields.Add(field);
        }

        return fields;
    }

    // The parameters that a width of a binding depends on: those its fields and nested schemas name in their bounds.
    private static void Collect(GesLayoutField field, SortedSet<string> depends)
    {
        var encoding = field.Encoding;
        foreach (var name in encoding.Depends) depends.Add(name);
        if (encoding.Element is { } element) Collect(element, depends);
        foreach (var axis in encoding.Axes) Collect(axis, depends);
        foreach (var alternative in encoding.Alternatives) Collect(alternative, depends);
    }

    // One field from its description and its nodes, which start at `index`; `index` moves past them.
    private static GesLayoutField Field(GesSchemaNode[] nodes, ref int index, string name, GesReadOnlyArray<GesSchemaType> alternatives)
    {
        ref readonly var node = ref nodes[index];
        var field = new GesLayoutField { Name = name, Path = node.Path, Optional = node.Optional, Key = node.Key };
        if (alternatives.Count == 1)
        {
            field.Encoding = Encoding(nodes, ref index, alternatives[0]);
            return field;
        }

        var union = new GesLayoutEncoding { Kind = GesLayoutKind.Union, Width = node.Width };
        index++;
        foreach (var alternative in alternatives)
        {
            var path = nodes[index].Path;
            var label = alternative.Label.Length > 0 ? alternative.Label : path.Substring(path.LastIndexOf(':') + 1);
            var encoding = alternative.Kind == GesSchemaTypeKind.Nothing ? Nothing(ref index) : Encoding(nodes, ref index, alternative);
            union.Alternatives.Add(new GesLayoutField { Name = label, Path = path, Encoding = encoding });
        }

        field.Encoding = union;
        return field;
    }

    private static GesLayoutEncoding Nothing(ref int index)
    {
        index++;
        return new GesLayoutEncoding { Kind = GesLayoutKind.Nothing };
    }

    private static GesLayoutEncoding Encoding(GesSchemaNode[] nodes, ref int index, GesSchemaType type)
    {
        var start = index;
        ref readonly var node = ref nodes[start];
        var encoding = new GesLayoutEncoding();
        switch (type.Kind)
        {
            case GesSchemaTypeKind.Boolean:
                encoding.Kind = GesLayoutKind.Boolean;
                break;
            case GesSchemaTypeKind.Number or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Quantity:
                Number(in node, type.Kind, type.Unit, type.Lower, type.Upper, encoding);
                break;
            case GesSchemaTypeKind.Tag when node.Kind == GesSchemaNodeKind.Tag:
                encoding.Kind = GesLayoutKind.Tag;
                encoding.Tags.AddRange(node.Tags!);
                encoding.TagSet = node.TagSet ?? "";
                encoding.Width = node.Width;
                break;
            case GesSchemaTypeKind.Flags:
                encoding.Kind = GesLayoutKind.Flags;
                encoding.Tags.AddRange(node.Tags!);
                encoding.Width = node.Width;
                break;
            case GesSchemaTypeKind.Bytes:
                encoding.Kind = GesLayoutKind.Bytes;
                encoding.MaxCount = node.MaxCount;
                encoding.Width = node.Width;
                break;
            case GesSchemaTypeKind.Tag or GesSchemaTypeKind.Text:
                encoding.Kind = GesLayoutKind.Text;
                encoding.TagName = node.IsTagText;
                encoding.MaxCount = node.MaxCount;
                encoding.Width = node.Width;
                break;
            case GesSchemaTypeKind.List:
                List(in node, encoding);
                encoding.Depth = type.Depth;
                index++;
                encoding.Element = Field(nodes, ref index, "[]", type.Element);
                return encoding;
            case GesSchemaTypeKind.Dice:
                List(in node, encoding);
                encoding.Dice = true;
                index++;
                var roll = new GesLayoutEncoding();
                Number(in nodes[index], GesSchemaTypeKind.Number, GesQuantityUnit.UnitNone, type.Lower, type.Upper, roll);
                encoding.Element = new GesLayoutField { Name = "[]", Path = nodes[index].Path, Encoding = roll };
                index++;
                return encoding;
            case GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector:
                encoding.Kind = type.Kind == GesSchemaTypeKind.Point ? GesLayoutKind.Point : GesLayoutKind.Vector;
                encoding.Unit = UnitName(type.Kind, type.Unit);
                index++;
                Axes(nodes, ref index, start, type, encoding);
                return encoding;
            case GesSchemaTypeKind.Box:
                // Two corners, each a Point of the block's axes.
                encoding.Kind = GesLayoutKind.Box;
                encoding.Unit = UnitName(type.Kind, type.Unit);
                index++;
                encoding.Corners.Add(Corner(nodes, ref index, type));
                encoding.Corners.Add(Corner(nodes, ref index, type));
                return encoding;
            case GesSchemaTypeKind.Disc:
                // The centre, a Point of the block's axes x and y, then the radius, unless the block leaves it out.
                encoding.Kind = GesLayoutKind.Disc;
                encoding.Unit = UnitName(type.Kind, type.Unit);
                var disc = index;
                index++;
                encoding.Corners.Add(Corner(nodes, ref index, type));
                if (index < nodes[disc].Next)
                {
                    var radius = new GesLayoutEncoding();
                    GesSchemaAxisInfo? bounds = null;
                    foreach (var axis in type.Axes)
                        if (axis.Name == "radius") bounds = axis;
                    Number(in nodes[index], GesSchemaTypeKind.Number, GesQuantityUnit.UnitNone, bounds?.Lower, bounds?.Upper, radius);
                    encoding.Corners.Add(new GesLayoutField { Name = nodes[index].Name, Path = nodes[index].Path, Encoding = radius });
                    index++;
                }

                return encoding;
            case GesSchemaTypeKind.Poly:
                // A List of Points with the axes x and y.
                List(in node, encoding);
                encoding.Poly = true;
                encoding.Unit = UnitName(type.Kind, type.Unit);
                index++;
                encoding.Element = Corner(nodes, ref index, type);
                return encoding;
            case GesSchemaTypeKind.Reference:
                encoding.Kind = GesLayoutKind.Schema;
                encoding.Schema = node.SchemaName;
                var depends = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var parameter in type.Schema!.Parameters) depends.Add(parameter.Name);
                encoding.Depends.AddRange(depends);
                index = node.Next;
                return encoding;
            default:
                throw new InvalidOperationException($"A {type.Kind} has no encoding.");
        }

        index++;
        return encoding;
    }

    // The axes of a Point, a Box corner or a Poly point, whose group node ends at `group`'s Next.
    private static void Axes(GesSchemaNode[] nodes, ref int index, int group, GesSchemaType type, GesLayoutEncoding encoding)
    {
        for (var axis = 0; axis < 3 && index < nodes[group].Next; axis++)
        {
            ref readonly var axisNode = ref nodes[index];
            var bounds = type.Axes.Count > 0 ? type.Axes[axis] : null;
            var axisEncoding = new GesLayoutEncoding();
            Number(in axisNode, GesSchemaTypeKind.Number, GesQuantityUnit.UnitNone, bounds?.Lower, bounds?.Upper, axisEncoding);
            encoding.Axes.Add(new GesLayoutField { Name = axisNode.Name, Path = axisNode.Path, Encoding = axisEncoding });
            index++;
        }
    }

    private static GesLayoutField Corner(GesSchemaNode[] nodes, ref int index, GesSchemaType type)
    {
        var group = index;
        var corner = new GesLayoutEncoding { Kind = GesLayoutKind.Point, Unit = UnitName(type.Kind, type.Unit) };
        index++;
        Axes(nodes, ref index, group, type, corner);
        return new GesLayoutField { Name = nodes[group].Name, Path = nodes[group].Path, Encoding = corner };
    }

    private static void List(in GesSchemaNode node, GesLayoutEncoding encoding)
    {
        encoding.Kind = GesLayoutKind.List;
        encoding.MaxCount = node.MaxCount;
        encoding.Width = node.Width;
        encoding.Keyed = node.KeyedElements;
        encoding.PositionWidth = node.KeyedElements ? node.PositionWidth : 0;
    }

    private static void Number(in GesSchemaNode node, GesSchemaTypeKind kind, GesQuantityUnit unit, GesSchemaBound? lower, GesSchemaBound? upper, GesLayoutEncoding encoding)
    {
        encoding.Kind = node.Kind switch
        {
            GesSchemaNodeKind.Integer => GesLayoutKind.Integer,
            GesSchemaNodeKind.Quantized => GesLayoutKind.Quantized,
            _ => GesLayoutKind.Float
        };
        encoding.Type = TypeName(kind);
        encoding.Unit = UnitName(kind, unit);
        encoding.Range = node.HasRange;
        encoding.Wrapping = node.Wrapping;
        encoding.Width = node.Width;
        encoding.DeltaRange = node.DeltaRange;
        encoding.DeltaWidth = node.DeltaWidth;
        if (node.Kind is GesSchemaNodeKind.Integer or GesSchemaNodeKind.Quantized)
        {
            encoding.Lower = node.Lower;
            encoding.MaxIndex = node.MaxIndex;
        }

        if (node.Kind == GesSchemaNodeKind.Quantized)
        {
            encoding.Numerator = node.P;
            encoding.TenPower = (long)node.SBinary;
        }

        if (node.Kind == GesSchemaNodeKind.Quantized || (node.Kind == GesSchemaNodeKind.Float && node.HasRange))
        {
            encoding.LowerValue = node.LowerValue;
            encoding.UpperValue = node.UpperValue;
        }

        var depends = new SortedSet<string>(StringComparer.Ordinal);
        if (lower is { IsParameter: true } from) depends.Add(from.Parameter);
        if (upper is { IsParameter: true } to) depends.Add(to.Parameter);
        encoding.Depends.AddRange(depends);
    }

    internal static string TypeName(GesSchemaTypeKind kind) => kind switch
    {
        GesSchemaTypeKind.Number => "Number",
        GesSchemaTypeKind.Quantity => "Quantity",
        GesSchemaTypeKind.Percentage => "Percentage",
        GesSchemaTypeKind.Point => "Point",
        GesSchemaTypeKind.Vector => "Vector",
        _ => kind.ToString()
    };

    // A Percentage holds a fraction, so its literal moves the exponent by two; a Quantity takes the symbol of its unit.
    private static string Literal(GesSchemaTypeKind kind, GesQuantityUnit unit, string value)
    {
        if (value.Length == 0) return "";
        if (kind == GesSchemaTypeKind.Percentage)
        {
            var exponent = value.IndexOf('e');
            return value.Substring(0, exponent + 1) + (long.Parse(value.Substring(exponent + 1), System.Globalization.CultureInfo.InvariantCulture) + 2).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        return kind == GesSchemaTypeKind.Quantity && unit != GesQuantityUnit.UnitNone ? value + unit.ToSuffix() : value;
    }

    internal static string UnitName(GesSchemaTypeKind kind, GesQuantityUnit unit)
        => kind is GesSchemaTypeKind.Quantity or GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector or GesSchemaTypeKind.Box or GesSchemaTypeKind.Poly or GesSchemaTypeKind.Disc && unit != GesQuantityUnit.UnitNone ? unit.ToTypeName() : "none";
}
