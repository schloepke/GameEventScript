// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>The kinds of declarations that have a hash and can be bound.</summary>
public enum GesSchemaKind : byte
{
    /// <summary>A schema.</summary>
    Schema,

    /// <summary>A message.</summary>
    Message,

    /// <summary>A message set.</summary>
    Messages
}

/// <summary>A bound, step or delta range: a parameter or an exact decimal.</summary>
public readonly struct GesSchemaBound
{
    private readonly string? _parameter;

    internal GesSchemaBound(GesResolvedValue value)
    {
        _parameter = value.Parameter;
        Significand = value.Value.Significand;
        Exponent = value.Value.Exponent;
    }

    /// <summary>Gets the parameter's name, or an empty text for an exact decimal.</summary>
    public string Parameter => _parameter ?? "";

    /// <summary>Gets whether the value comes from a parameter at binding.</summary>
    public bool IsParameter => Parameter.Length > 0;

    /// <summary>Gets the significand of the exact decimal, without trailing zeros.</summary>
    public long Significand { get; }

    /// <summary>Gets the decimal exponent of the exact decimal.</summary>
    public int Exponent { get; }

    /// <summary>Converts the exact decimal to the closest Binary64.</summary>
    /// <returns>The value, or null for a parameter.</returns>
    public double? ToDouble() => IsParameter ? null : double.Parse(Significand.ToString(CultureInfo.InvariantCulture) + "e" + Exponent.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}

/// <summary>The limits of a numeric axis of a Point or Vector.</summary>
public sealed class GesSchemaAxisInfo
{
    internal GesSchemaAxisInfo(GesResolvedAxis axis)
    {
        Name = axis.Axis.ToString();
        var constraints = axis.Constraints;
        IsInteger = constraints.Integer;
        IsWrapping = constraints.Wrapping;
        Lower = GesSchemaType.Bound(constraints.Lower);
        Upper = GesSchemaType.Bound(constraints.Upper);
        Step = GesSchemaType.Bound(constraints.Step);
        Delta = GesSchemaType.Bound(constraints.Delta);
    }

    /// <summary>Gets the axis: <c>x</c>, <c>y</c> or <c>z</c>.</summary>
    public string Name { get; }

    /// <summary>Gets whether the axis holds whole numbers.</summary>
    public bool IsInteger { get; }

    /// <summary>Gets whether the range wraps around.</summary>
    public bool IsWrapping { get; }

    /// <summary>Gets the lower bound, if there is a range.</summary>
    public GesSchemaBound? Lower { get; }

    /// <summary>Gets the upper bound, if there is a range.</summary>
    public GesSchemaBound? Upper { get; }

    /// <summary>Gets the step, if the axis is quantized.</summary>
    public GesSchemaBound? Step { get; }

    /// <summary>Gets the range of a delta, if there is one.</summary>
    public GesSchemaBound? Delta { get; }
}

/// <summary>The type of a field, or one alternative of a union.</summary>
public sealed class GesSchemaType
{
    internal GesSchemaType(GesResolvedType type)
    {
        Kind = type.Kind;
        Unit = type.Unit;
        var constraints = type.Constraints;
        IsInteger = constraints.Integer;
        IsWrapping = constraints.Wrapping;
        Lower = Bound(constraints.Lower);
        Upper = Bound(constraints.Upper);
        Step = Bound(constraints.Step);
        Delta = Bound(constraints.Delta);
        Tags = new GesReadOnlyArray<string>(type.Tags);
        Max = type.Max;
        Element = GesSchema.Types(type.Element ?? []);
        var axes = new List<GesSchemaAxisInfo>();
        foreach (var axis in type.Axes ?? []) axes.Add(new GesSchemaAxisInfo(axis));
        Axes = new GesReadOnlyArray<GesSchemaAxisInfo>(axes);
        Schema = type.Reference?.Public;
    }

    internal static GesSchemaBound? Bound(GesResolvedValue? value) => value is null ? null : new GesSchemaBound(value);

    /// <summary>Gets the kind of type.</summary>
    public GesSchemaTypeKind Kind { get; }

    /// <summary>Gets the unit of a Quantity, Point or Vector.</summary>
    public GesQuantityUnit Unit { get; }

    /// <summary>Gets whether a number holds whole numbers.</summary>
    public bool IsInteger { get; }

    /// <summary>Gets whether the range wraps around.</summary>
    public bool IsWrapping { get; }

    /// <summary>Gets the lower bound, if there is a range.</summary>
    public GesSchemaBound? Lower { get; }

    /// <summary>Gets the upper bound, if there is a range.</summary>
    public GesSchemaBound? Upper { get; }

    /// <summary>Gets the step, if the number is quantized.</summary>
    public GesSchemaBound? Step { get; }

    /// <summary>Gets the range of a delta, if there is one.</summary>
    public GesSchemaBound? Delta { get; }

    /// <summary>Gets the closed set of a Tag; empty when any tag is allowed.</summary>
    public GesReadOnlyArray<string> Tags { get; }

    /// <summary>Gets the maximum of a Text in UTF-8 bytes or of a List in elements, if there is one.</summary>
    public long? Max { get; }

    /// <summary>Gets the alternatives of a List's element; empty when any element is allowed.</summary>
    public GesReadOnlyArray<GesSchemaType> Element { get; }

    /// <summary>Gets the axes of an axis block in x, y, z order; empty without a block, when all three are stored in full.</summary>
    public GesReadOnlyArray<GesSchemaAxisInfo> Axes { get; }

    /// <summary>Gets the schema a <see cref="GesSchemaTypeKind.Reference"/> refers to.</summary>
    public GesSchema? Schema { get; }
}

/// <summary>A field of a schema or an argument of a message.</summary>
public sealed class GesSchemaFieldInfo
{
    internal GesSchemaFieldInfo(GesResolvedField field)
    {
        Name = field.Name;
        IsUnlabeled = field.Unlabeled;
        IsOptional = field.Marker == GesSchemaFieldMarker.Optional;
        IsKey = field.Marker == GesSchemaFieldMarker.Key;
        Alternatives = GesSchema.Types(field.Alternatives);
    }

    /// <summary>Gets the name.</summary>
    public string Name { get; }

    /// <summary>Gets whether a message argument has no label.</summary>
    public bool IsUnlabeled { get; }

    /// <summary>Gets whether the field may be absent.</summary>
    public bool IsOptional { get; }

    /// <summary>Gets whether the field is the key of its schema.</summary>
    public bool IsKey { get; }

    /// <summary>Gets the types of the field: one, or the alternatives of a union in source order.</summary>
    public GesReadOnlyArray<GesSchemaType> Alternatives { get; }
}

/// <summary>A parameter that a declaration reaches and every binding must supply.</summary>
public sealed class GesSchemaParameterInfo
{
    internal GesSchemaParameterInfo(string name, GesSchemaTypeKind kind, GesQuantityUnit unit)
    {
        Name = name;
        Kind = kind;
        Unit = unit;
    }

    /// <summary>Gets the name.</summary>
    public string Name { get; }

    /// <summary>Gets the kind: Number, Percentage or Quantity.</summary>
    public GesSchemaTypeKind Kind { get; }

    /// <summary>Gets the unit of a Quantity.</summary>
    public GesQuantityUnit Unit { get; }
}

/// <summary>
/// The immutable description of one schema, message or message set of a <see cref="GesSchemaSet"/>. It can be shared
/// between threads and bound any number of times.
/// </summary>
public sealed class GesSchema
{
    private readonly GesResolvedDeclaration _declaration;

    internal GesSchema(GesResolvedDeclaration declaration)
    {
        _declaration = declaration;
        Name = declaration.Name;
        Kind = declaration.Kind;
        Tags = new GesReadOnlyArray<string>(declaration.Tags);
        WireGroup = declaration.Syntax.WireGroup;
        ModelGroup = declaration.Syntax.ModelGroup;
    }

    /// <summary>Gets the name.</summary>
    public string Name { get; }

    /// <summary>Gets the kind of declaration.</summary>
    public GesSchemaKind Kind { get; }

    /// <summary>
    /// Gets the group in which the declaration is a wire root, <c>default</c> for <c>wire</c> without a group, or null when
    /// it is no wire root. Roles are generation metadata: they are not part of the canonical form or any hash.
    /// </summary>
    public string? WireGroup { get; }

    /// <summary>Gets the group in which the schema is a model root, <c>default</c> for <c>model</c> without a group, or
    /// null when it is no model root. Roles are not part of the canonical form or any hash.</summary>
    public string? ModelGroup { get; }

    /// <summary>Gets the schema hash: FNV-1a 64 over the canonical form.</summary>
    public ulong Hash { get; private set; }

    /// <summary>Gets the canonical form, which the hash runs over. It is computed on each call.</summary>
    public string CanonicalForm => GesSchemaCanonical.Form(_declaration);

    /// <summary>Gets the parameters it reaches, in ascending name order.</summary>
    public GesReadOnlyArray<GesSchemaParameterInfo> Parameters { get; private set; }

    /// <summary>Gets the fields of a schema or the arguments of a message, in declared order.</summary>
    public GesReadOnlyArray<GesSchemaFieldInfo> Fields { get; private set; }

    /// <summary>Gets the tags of a message, in declared order.</summary>
    public GesReadOnlyArray<string> Tags { get; }

    /// <summary>Gets the members of a message set, in declared order.</summary>
    public GesReadOnlyArray<GesSchema> Members { get; private set; }

    /// <summary>Binds the declaration for encoding and decoding. It can be bound any number of times.</summary>
    /// <param name="parameters">The values of the parameters it reaches, if it has any.</param>
    /// <returns>The immutable bound schema.</returns>
    /// <exception cref="GesSchemaException">The declaration cannot be encoded or a value breaks a rule.</exception>
    public GesBoundSchema Bind(GesSchemaParameters? parameters = null) => GesSchemaBinder.Bind(_declaration, parameters ?? new GesSchemaParameters());

    /// <summary>Checks a GES document against this schema, whatever its header says.</summary>
    /// <param name="text">The document text.</param>
    /// <param name="parameters">Parameter values; a bound whose parameter has no value is not checked.</param>
    /// <exception cref="GesSchemaException">The document is unreadable or breaks the schema; the code is a <c>document.*</c>
    /// code with its position, or a <c>schema.*</c> code when this is no schema or a parameter value has another kind.</exception>
    public void CheckDocument(string text, GesSchemaParameters? parameters = null)
        => GesSchemaSet.Check(text, GesSchemaSet.ReadDocument(text), _declaration, parameters);

    // Declarations can refer to each other, so the sets are filled once every declaration has its object.
    internal void Complete()
    {
        var parameters = new SortedDictionary<string, GesSchemaTypeSyntax>(System.StringComparer.Ordinal);
        GesSchemaCanonical.Reach(_declaration, [], parameters);
        var infos = new List<GesSchemaParameterInfo>();
        foreach (var parameter in parameters) infos.Add(new GesSchemaParameterInfo(parameter.Key, parameter.Value.Kind, parameter.Value.Unit));
        Parameters = new GesReadOnlyArray<GesSchemaParameterInfo>(infos);
        Hash = GesSchemaCanonical.Hash(CanonicalForm);
        var fields = new List<GesSchemaFieldInfo>();
        foreach (var field in _declaration.Fields) fields.Add(new GesSchemaFieldInfo(field));
        Fields = new GesReadOnlyArray<GesSchemaFieldInfo>(fields);
        var members = new List<GesSchema>();
        foreach (var member in _declaration.Members) members.Add(member.Public);
        Members = new GesReadOnlyArray<GesSchema>(members);
    }

    internal static GesReadOnlyArray<GesSchemaType> Types(GesResolvedType[] types)
    {
        var result = new List<GesSchemaType>();
        foreach (var type in types) result.Add(new GesSchemaType(type));
        return new GesReadOnlyArray<GesSchemaType>(result);
    }
}
