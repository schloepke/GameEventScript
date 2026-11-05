// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Syntax;
using Code = GameEventScript.Syntax.GesSchemaDiagnostic;

namespace GameEventScript.Api;

/// <summary>The parameter values of one binding.</summary>
public sealed class GesSchemaParameters
{
    internal readonly struct Value(GesSchemaDecimal number, GesQuantityUnit unit, bool isPercent, bool isLiteral)
    {
        internal GesSchemaDecimal Number { get; } = number;

        internal GesQuantityUnit Unit { get; } = unit;

        internal bool IsPercent { get; } = isPercent;

        // A literal says whether it is a percentage; a plain number is the ratio of either kind.
        internal bool IsLiteral { get; } = isLiteral;
    }

    private readonly Dictionary<string, Value> _values = new(StringComparer.Ordinal);

    /// <summary>Sets a parameter from a literal such as <c>5km</c> or <c>10%</c>, exactly as written.</summary>
    /// <param name="name">The parameter's name.</param>
    /// <param name="literal">The literal.</param>
    /// <returns>This instance.</returns>
    /// <exception cref="GesSchemaException">The text is no exact finite literal (<c>schema.syntax</c>).</exception>
    public GesSchemaParameters With(string name, string literal)
    {
        if (literal is null) throw new ArgumentNullException(nameof(literal));
        var read = GesSchemaLiterals.Read(literal, 0, literal.Length) ?? throw new GesSchemaException(Code.Syntax, "", 0, 0, "Expected an exact finite literal.");
        _values[name] = new Value(read.Value, read.Unit, read.IsPercent, isLiteral: true);
        return this;
    }

    /// <summary>Sets a parameter to an integer.</summary>
    /// <param name="name">The parameter's name.</param>
    /// <param name="value">The value in base units.</param>
    /// <param name="unit">The unit.</param>
    /// <returns>This instance.</returns>
    public GesSchemaParameters With(string name, long value, GesQuantityUnit unit = GesQuantityUnit.UnitNone)
    {
        _values[name] = new Value(new GesSchemaDecimal(value, 0), unit, isPercent: false, isLiteral: false);
        return this;
    }

    /// <summary>Sets a parameter to a Binary64, which converts through its shortest roundtrip text.</summary>
    /// <param name="name">The parameter's name.</param>
    /// <param name="value">The value in base units.</param>
    /// <param name="unit">The unit.</param>
    /// <returns>This instance.</returns>
    /// <exception cref="GesSchemaException">The value is not finite (<c>schema.invalidBound</c>).</exception>
    public GesSchemaParameters With(string name, double value, GesQuantityUnit unit = GesQuantityUnit.UnitNone)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new GesSchemaException(Code.InvalidBound, "", 0, 0, "Expected a finite value.");
        _values[name] = new Value(Shortest(value), unit, isPercent: false, isLiteral: false);
        return this;
    }

    internal bool TryGet(string name, out Value value) => _values.TryGetValue(name, out value);

    // The shortest decimal that reads back to the same Binary64.
    private static GesSchemaDecimal Shortest(double value)
    {
        for (var digits = 0; digits < 17; digits++)
        {
            var text = value.ToString("E" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            if (double.Parse(text, CultureInfo.InvariantCulture) != value) continue;
            return GesSchemaLiterals.Read(text, 0, text.Length)!.Value.Value;
        }

        throw new ArgumentException("Expected a finite value.", nameof(value));
    }
}

internal enum GesSchemaNodeKind : byte
{
    Boolean,
    Integer,
    Quantized,
    Float,
    Tag,
    Text,
    List,
    Group,
    Union,
    Nothing
}

// One field of a bound layout. A subtree occupies consecutive nodes, and Next is the first node after it. After is where
// writing goes on: the same, except that the end of a union alternative continues behind the union.
internal struct GesSchemaNode
{
    internal GesSchemaNodeKind Kind;
    internal bool Optional;
    internal bool Key;
    internal bool Wrapping;
    internal bool HasRange;
    internal bool IsTagBit;
    internal int Width;
    internal int Next;
    internal int After;
    internal string Name;
    internal string Path;
    internal string[]? Tags;

    // Integer: its lower bound. Quantized: lowerN. The largest valid index; for a wrapping field the period is one more.
    internal long Lower;
    internal ulong MaxIndex;
    internal long P;
    internal double PBinary;
    internal double SBinary;
    internal double LowerValue;
    internal double UpperValue;
    internal long MaxCount;

    // What a value of this node is, for the value path.
    internal GesSchemaTypeKind ValueKind;
    internal GesQuantityUnit Unit;
    internal string SchemaName;
}

/// <summary>A schema, message or message set bound to parameter values. Immutable; it can be shared between threads.</summary>
public sealed class GesBoundSchema
{
    private readonly Dictionary<string, int> _fields = new(StringComparer.Ordinal);

    internal GesBoundSchema(GesSchema schema, string name, GesSchemaKind kind, GesSchemaNode[] nodes, GesBoundSchema[] members, int memberWidth, long maxByteCount, ulong hash, ulong bindingHash)
    {
        Schema = schema;
        Name = name;
        Kind = kind;
        Nodes = nodes;
        Members = members;
        MemberWidth = memberWidth;
        MaxByteCount = maxByteCount;
        Hash = hash;
        BindingHash = bindingHash;
        for (var index = 0; index < nodes.Length; index++) _fields[nodes[index].Path] = index;
    }

    /// <summary>Gets the description this binding comes from.</summary>
    public GesSchema Schema { get; }

    /// <summary>Gets the declaration's name.</summary>
    public string Name { get; }

    /// <summary>Gets the size of the largest encoding in bytes.</summary>
    public long MaxByteCount { get; }

    /// <summary>Gets the schema hash, independent of parameter values.</summary>
    public ulong Hash { get; }

    /// <summary>Gets the hash over the schema hash and the parameter values.</summary>
    public ulong BindingHash { get; }

    /// <summary>Gets whether the first field is a key that a reader can take first.</summary>
    public bool HasKey => Nodes.Length > 0 && Nodes[0].Key;

    internal GesSchemaKind Kind { get; }

    internal GesSchemaNode[] Nodes { get; }

    internal GesBoundSchema[] Members { get; }

    // The labels of a message's arguments, `_` for an unlabeled one.
    internal string[] Labels { get; init; } = [];

    internal int MemberWidth { get; }

    // The largest encoding of a schema or message in bits, which a message set adds its index to.
    internal long MaxBitCount { get; init; }

    /// <summary>Gets the handle of a field; resolve it once and reuse it.</summary>
    /// <param name="path">The path, such as <c>x</c>, <c>position.x</c>, <c>units[]</c>, <c>units[].name</c>, <c>#critical</c>, or <c>facing:Tag</c> for a union alternative.</param>
    /// <returns>The handle.</returns>
    /// <exception cref="ArgumentException">The field does not exist.</exception>
    public GesSchemaField Field(string path)
        => _fields.TryGetValue(path, out var index) ? new GesSchemaField(this, index) : throw new ArgumentException($"Field '{path}' does not exist.", nameof(path));

    /// <summary>Gets a member of a message set.</summary>
    /// <param name="name">The message's name.</param>
    /// <returns>The bound message.</returns>
    /// <exception cref="ArgumentException">The set has no such message.</exception>
    public GesBoundSchema Member(string name)
    {
        foreach (var member in Members)
            if (member.Name == name) return member;
        throw new ArgumentException($"Message '{name}' is not in this set.", nameof(name));
    }

    /// <summary>Gets a member's index, which selects it in a writer.</summary>
    /// <param name="name">The message's name.</param>
    /// <returns>The index in declared order.</returns>
    /// <exception cref="ArgumentException">The set has no such message.</exception>
    public int MemberIndex(string name)
    {
        for (var index = 0; index < Members.Length; index++)
            if (Members[index].Name == name) return index;
        throw new ArgumentException($"Message '{name}' is not in this set.", nameof(name));
    }
}

/// <summary>The handle of a field in a bound schema. Resolving it once makes further uses free of lookups.</summary>
public readonly struct GesSchemaField
{
    internal GesSchemaField(GesBoundSchema owner, int index)
    {
        Owner = owner;
        Index = index;
    }

    internal GesBoundSchema Owner { get; }

    internal int Index { get; }

    /// <summary>Gets the field's path.</summary>
    public string Path => Owner.Nodes[Index].Path;
}

/// <summary>Builds the layout of a declaration for fixed parameter values.</summary>
internal static class GesSchemaBinder
{
    internal const long MaximumByteCount = 1048576;
    private const long LargestSteps = 1L << 50;
    private const long LargestMultiple = 1L << 53;
    private const int MaximumFractionDigits = 15;
    private const int MaximumListNesting = 4;
    private const int MaximumNodeCount = 1048576;

    // The numbers of a numeric field, from literals alone or with parameter values. Parameters without a value leave the
    // checks that need them to binding.
    private sealed class Numeric
    {
        internal long Lower;
        internal long Upper;
        internal long P;
        internal int K;
        internal bool Known;
        internal ulong DeltaRange;
    }

    internal static void CheckLiterals(GesFileScope scope, GesResolvedConstraints constraints)
        => Compute(scope, constraints, null);

    private static Numeric Compute(GesFileScope scope, GesResolvedConstraints constraints, Func<GesResolvedValue, GesSchemaDecimal>? parameter)
    {
        var numeric = new Numeric();
        GesSchemaDecimal? lower = constraints.Lower is { } l ? (l.IsParameter ? parameter?.Invoke(l) : l.Value) : null;
        GesSchemaDecimal? upper = constraints.Upper is { } u ? (u.IsParameter ? parameter?.Invoke(u) : u.Value) : null;
        if (constraints.Step is { } step)
        {
            if (step.Value.Significand <= 0) throw scope.Error(Code.InvalidStep, step.Offset, "A step is positive.");
            var exponent = step.Value.Exponent;
            numeric.K = exponent < 0 ? -exponent : 0;
            if (numeric.K > MaximumFractionDigits) throw scope.Error(Code.TooWide, step.Offset, "A step has at most 15 fractional digits.");
            if (!TryScale(step.Value.Significand, exponent < 0 ? 0 : exponent, out numeric.P)) throw scope.Error(Code.TooWide, step.Offset, "A step is too large.");
            if (lower is { } first) numeric.Lower = Steps(scope, first, numeric, constraints.Lower!);
            if (upper is { } last) numeric.Upper = Steps(scope, last, numeric, constraints.Upper!);
            numeric.Known = lower.HasValue && upper.HasValue;
            if (numeric.Known && numeric.Lower >= numeric.Upper) throw scope.Error(Code.InvalidRange, constraints.Lower!.Offset, "A lower bound is below its upper bound.");
            if (constraints.Delta is { } delta)
            {
                if (delta.Value.Significand <= 0) throw scope.Error(Code.InvalidStep, delta.Offset, "A delta range is positive.");
                numeric.DeltaRange = (ulong)Steps(scope, delta.Value, numeric, delta);
            }
        }
        else if (constraints.Integer)
        {
            if (lower is { } first) numeric.Lower = Whole(scope, first, constraints.Lower!);
            if (upper is { } last) numeric.Upper = Whole(scope, last, constraints.Upper!);
            numeric.Known = lower.HasValue && upper.HasValue;
            if (numeric.Known && numeric.Lower >= numeric.Upper) throw scope.Error(Code.InvalidRange, constraints.Lower!.Offset, "A lower bound is below its upper bound.");
            if (constraints.Delta is { } delta)
            {
                if (delta.Value.Significand <= 0) throw scope.Error(Code.InvalidStep, delta.Offset, "A delta range is positive.");
                numeric.DeltaRange = (ulong)Whole(scope, delta.Value, delta);
            }
        }
        else
        {
            if (lower is { } first) CheckBinary(scope, first, constraints.Lower!);
            if (upper is { } last) CheckBinary(scope, last, constraints.Upper!);
            if (lower is { } from && upper is { } to && Compare(from, to) >= 0)
                throw scope.Error(Code.InvalidRange, constraints.Lower!.Offset, "A lower bound is below its upper bound.");
        }

        return numeric;
    }

    // A fractional field compares in binary64, so its bounds must keep their meaning there.
    private static void CheckBinary(GesFileScope scope, GesSchemaDecimal value, GesResolvedValue origin)
    {
        var binary = double.Parse(value.Canonical, CultureInfo.InvariantCulture);
        if (double.IsInfinity(binary) || (binary == 0 && value.Significand != 0))
            throw scope.Error(Code.TooWide, origin.Offset, "A bound of a fractional field lies beyond binary64.");
    }

    // value / step as an exact integer within the limits.
    private static long Steps(GesFileScope scope, GesSchemaDecimal value, Numeric numeric, GesResolvedValue origin)
    {
        var shift = value.Exponent + numeric.K;
        if (shift < 0) throw scope.Error(Code.InvalidStep, origin.Offset, "A bound or delta range is a multiple of the step.");
        if (!TryScale(value.Significand, shift, out var scaled)) throw scope.Error(Code.TooWide, origin.Offset, "A value exceeds 2^50 steps.");
        if (scaled % numeric.P != 0) throw scope.Error(Code.InvalidStep, origin.Offset, "A bound or delta range is a multiple of the step.");
        var steps = scaled / numeric.P;
        if (steps > LargestSteps || steps < -LargestSteps || scaled > LargestMultiple || scaled < -LargestMultiple)
            throw scope.Error(Code.TooWide, origin.Offset, "A value exceeds 2^50 steps.");
        return steps;
    }

    private static long Whole(GesFileScope scope, GesSchemaDecimal value, GesResolvedValue origin)
    {
        if (value.Exponent < 0 || !TryScale(value.Significand, value.Exponent, out var whole))
            throw scope.Error(Code.InvalidBound, origin.Offset, "An integer bound is integral and within Int64.");
        return whole;
    }

    private static bool TryScale(long value, int power, out long result)
    {
        result = value;
        for (var index = 0; index < power && result != 0; index++)
        {
            if (result > long.MaxValue / 10 || result < long.MinValue / 10)
                return false;
            result *= 10;
        }

        return true;
    }

    internal static int Compare(GesSchemaDecimal left, GesSchemaDecimal right)
    {
        var (a, b) = (left.Significand, right.Significand);
        if (a == 0 || b == 0 || (a < 0) != (b < 0)) return a.CompareTo(b);
        var magnitude = Digits(a) + left.Exponent;
        var other = Digits(b) + right.Exponent;
        if (magnitude != other) return (a > 0) == (magnitude > other) ? 1 : -1;
        var common = Math.Min(left.Exponent, right.Exponent);
        var order = Magnitude(a, left.Exponent - common).CompareTo(Magnitude(b, right.Exponent - common));
        return a > 0 ? order : -order;
    }

    // Equal magnitudes have at most 19 digits when scaled, which an UInt64 holds also where an Int64 does not.
    private static ulong Magnitude(long value, int power)
    {
        var magnitude = value < 0 ? 0UL - unchecked((ulong)value) : (ulong)value;
        for (var index = 0; index < power; index++) magnitude *= 10;
        return magnitude;
    }

    private static int Digits(long value)
    {
        var digits = 1;
        while (value is >= 10 or <= -10)
        {
            value /= 10;
            digits++;
        }

        return digits;
    }

    // The width of n values given n - 1: the bit length.
    internal static int BitLength(ulong value)
    {
        var length = 0;
        while (value != 0)
        {
            value >>= 1;
            length++;
        }

        return length;
    }

    private sealed class Build
    {
        internal readonly List<GesSchemaNode> Nodes = [];
        internal readonly GesSchemaParameters Parameters;

        internal Build(GesSchemaParameters parameters) => Parameters = parameters;
    }

    // The values a document check uses must have their declared kind and unit, as at binding; others stay unchecked.
    internal static void CheckParameters(GesResolvedDeclaration root, GesSchemaParameters parameters)
    {
        var reached = new SortedDictionary<string, GesSchemaTypeSyntax>(StringComparer.Ordinal);
        GesSchemaCanonical.Reach(root, [], reached);
        foreach (var parameter in reached)
            if (parameters.TryGet(parameter.Key, out var value)) CheckKind(root, parameter.Key, parameter.Value, value);
    }

    private static void CheckKind(GesResolvedDeclaration root, string name, GesSchemaTypeSyntax declared, GesSchemaParameters.Value value)
    {
        var percentage = declared.Kind == GesSchemaTypeKind.Percentage;
        if (value.Unit != declared.Unit || (value.IsLiteral && value.IsPercent != percentage))
            throw root.Scope.Error(Code.UnitMismatch, root.Offset, $"Parameter '{name}' has another kind or unit.");
    }

    internal static GesBoundSchema Bind(GesResolvedDeclaration root, GesSchemaParameters parameters)
    {
        var declarations = new List<GesResolvedDeclaration>();
        var reached = new SortedDictionary<string, GesSchemaTypeSyntax>(StringComparer.Ordinal);
        GesSchemaCanonical.Reach(root, declarations, reached);
        var hash = GesSchemaCanonical.Hash(GesSchemaCanonical.Form(root));
        var lines = new StringBuilder(GesSchemaCanonical.Hex(hash)).Append('\n');
        foreach (var parameter in reached)
        {
            if (!parameters.TryGet(parameter.Key, out var value))
                throw root.Scope.Error(Code.UnknownName, root.Offset, $"Parameter '{parameter.Key}' has no value.");
            CheckKind(root, parameter.Key, parameter.Value, value);
            lines.Append(parameter.Key).Append(' ').Append(value.Number.Canonical).Append('\n');
        }

        var bindingHash = GesSchemaCanonical.Hash(lines.ToString());
        if (root.Kind == GesSchemaKind.Messages)
        {
            var members = new GesBoundSchema[root.Members.Length];
            var largest = 0L;
            for (var index = 0; index < members.Length; index++)
            {
                members[index] = BindNodes(root.Members[index], parameters, hash, bindingHash);
                largest = Math.Max(largest, members[index].MaxBitCount);
            }

            var width = BitLength((ulong)members.Length - 1);
            var total = largest + width;
            if (total > MaximumByteCount * 8) throw root.Scope.Error(Code.TooLarge, root.Offset, $"The largest encoding of '{root.Name}' exceeds {MaximumByteCount} bytes.");
            return new GesBoundSchema(root.Public, root.Name, root.Kind, [], members, width, (total + 7) / 8, hash, bindingHash);
        }

        return BindNodes(root, parameters, hash, bindingHash);
    }

    private static GesBoundSchema BindNodes(GesResolvedDeclaration declaration, GesSchemaParameters parameters, ulong hash, ulong bindingHash)
    {
        var inner = new Build(parameters);
        foreach (var field in declaration.Fields) AddField(inner, declaration.Scope, field, "", 0, top: true);
        foreach (var tag in declaration.Tags)
            inner.Nodes.Add(new GesSchemaNode { Kind = GesSchemaNodeKind.Boolean, IsTagBit = true, Width = 1, Name = tag, Path = "#" + tag, Next = inner.Nodes.Count + 1, SchemaName = "" });
        var nodes = inner.Nodes.ToArray();
        Seal(nodes);
        var bits = Cost(nodes, 0, nodes.Length);
        if (bits > MaximumByteCount * 8) throw declaration.Scope.Error(Code.TooLarge, declaration.Offset, $"The largest encoding of '{declaration.Name}' exceeds {MaximumByteCount} bytes.");
        var labels = new string[declaration.Fields.Length];
        for (var index = 0; index < labels.Length; index++) labels[index] = declaration.Fields[index].Unlabeled ? "_" : declaration.Fields[index].Name;
        return new GesBoundSchema(declaration.Public, declaration.Name, declaration.Kind, nodes, [], 0, (bits + 7) / 8, hash, bindingHash) { Labels = labels, MaxBitCount = bits };
    }

    private static void Seal(GesSchemaNode[] nodes)
    {
        for (var index = 0; index < nodes.Length; index++) nodes[index].After = nodes[index].Next;
        for (var union = 0; union < nodes.Length; union++)
        {
            if (nodes[union].Kind != GesSchemaNodeKind.Union) continue;
            for (var alternative = union + 1; alternative < nodes[union].Next; alternative = nodes[alternative].Next)
            {
                var end = nodes[alternative].Next;
                for (var index = alternative; index < end; index++)
                    if (nodes[index].After == end) nodes[index].After = nodes[union].After;
            }
        }
    }

    // The largest encoding in bits. Sums stop growing beyond the limit, so nothing overflows.
    private static long Cost(GesSchemaNode[] nodes, int start, int end)
    {
        var limit = MaximumByteCount * 8 + 1;
        long full = 0;
        for (var index = start; index < end; index = nodes[index].Next)
        {
            ref readonly var node = ref nodes[index];
            long field;
            switch (node.Kind)
            {
                case GesSchemaNodeKind.Boolean:
                    field = 1;
                    break;
                case GesSchemaNodeKind.Text:
                    field = node.Width + Math.Min(node.MaxCount, limit) * 8;
                    break;
                case GesSchemaNodeKind.List:
                    var element = Cost(nodes, index + 1, node.Next);
                    field = node.Width + (element == 0 ? 0 : Math.Min(node.MaxCount, limit / Math.Max(element, 1) + 1) * element);
                    break;
                case GesSchemaNodeKind.Group:
                    field = Cost(nodes, index + 1, node.Next);
                    break;
                case GesSchemaNodeKind.Union:
                    long largest = 0;
                    for (var alternative = index + 1; alternative < node.Next; alternative = nodes[alternative].Next)
                        largest = Math.Max(largest, Cost(nodes, alternative, nodes[alternative].Next));
                    field = node.Width + largest;
                    break;
                default:
                    field = node.Width;
                    break;
            }

            if (node.Optional) field++;
            full = Math.Min(full + field, limit);
        }

        return full;
    }

    private static void AddField(Build build, GesFileScope scope, GesResolvedField field, string prefix, int lists, bool top)
    {
        var path = prefix.Length == 0 ? field.Name : prefix + "." + field.Name;
        AddUnion(build, scope, field.Alternatives, field.Name, path, field.Marker == GesSchemaFieldMarker.Optional, top && field.Marker == GesSchemaFieldMarker.Key, lists);
    }

    private static void AddUnion(Build build, GesFileScope scope, GesResolvedType[] alternatives, string name, string path, bool optional, bool key, int lists)
    {
        if (alternatives.Length == 1)
        {
            AddType(build, scope, alternatives[0], name, path, optional, key, lists);
            return;
        }

        CheckNodeCount(build, scope, alternatives[0].Offset);
        var index = build.Nodes.Count;
        var maxIndex = (ulong)alternatives.Length - 1;
        build.Nodes.Add(new GesSchemaNode { Kind = GesSchemaNodeKind.Union, Name = name, Path = path, Optional = optional, MaxIndex = maxIndex, Width = BitLength(maxIndex), SchemaName = "" });
        foreach (var alternative in alternatives)
        {
            var alternativePath = path + ":" + (alternative.Kind == GesSchemaTypeKind.Reference ? alternative.ReferenceName : alternative.Kind.ToString());
            if (alternative.Kind == GesSchemaTypeKind.Nothing)
            {
                CheckNodeCount(build, scope, alternative.Offset);
                build.Nodes.Add(new GesSchemaNode { Kind = GesSchemaNodeKind.Nothing, ValueKind = GesSchemaTypeKind.Nothing, Name = name, Path = alternativePath, Next = build.Nodes.Count + 1, SchemaName = "" });
            }
            else AddType(build, scope, alternative, name, alternativePath, false, false, lists);
        }

        var added = build.Nodes[index];
        added.Next = build.Nodes.Count;
        build.Nodes[index] = added;
    }

    private static void AddType(Build build, GesFileScope scope, GesResolvedType type, string name, string path, bool optional, bool key, int lists)
    {
        CheckNodeCount(build, scope, type.Offset);
        var index = build.Nodes.Count;
        var node = new GesSchemaNode { Name = name, Path = path, Optional = optional, Key = key, ValueKind = type.Kind, Unit = type.Unit, SchemaName = "" };
        switch (type.Kind)
        {
            case GesSchemaTypeKind.Boolean:
                node.Kind = GesSchemaNodeKind.Boolean;
                node.Width = 1;
                break;
            case GesSchemaTypeKind.Number:
            case GesSchemaTypeKind.Percentage:
            case GesSchemaTypeKind.Quantity:
                Number(build, scope, type.Constraints, type.Offset, ref node);
                break;
            case GesSchemaTypeKind.Tag:
                if (type.Tags is null) throw scope.Error(Code.NotEncodable, type.Offset, "A Tag without a set has no width.");
                node.Kind = GesSchemaNodeKind.Tag;
                node.Tags = type.Tags;
                node.MaxIndex = (ulong)type.Tags.Length - 1;
                node.Width = BitLength(node.MaxIndex);
                break;
            case GesSchemaTypeKind.Text:
                if (type.Max is not { } textMax) throw scope.Error(Code.NotEncodable, type.Offset, "A Text without max has no hard limit.");
                node.Kind = GesSchemaNodeKind.Text;
                node.MaxCount = textMax;
                node.Width = BitLength((ulong)textMax);
                break;
            case GesSchemaTypeKind.List:
                if (type.Element is not { } element || type.Max is not { } listMax) throw scope.Error(Code.NotEncodable, type.Offset, "A List without element type and max has no hard limit.");
                if (lists >= MaximumListNesting) throw scope.Error(Code.NotEncodable, type.Offset, $"Lists nest at most {MaximumListNesting} levels for encoding.");
                node.Kind = GesSchemaNodeKind.List;
                node.MaxCount = listMax;
                node.Width = BitLength((ulong)listMax);
                build.Nodes.Add(node);
                AddUnion(build, scope, element, "[]", path + "[]", false, false, lists + 1);
                if (!HasBits(build.Nodes, index + 1)) throw scope.Error(Code.NotEncodable, type.Offset, "A List element without bits has a count that no data limits.");
                break;
            case GesSchemaTypeKind.Dice:
                if (type.Max is not { } diceMax) throw scope.Error(Code.NotEncodable, type.Offset, "A Dice without max has no hard limit.");
                if (lists >= MaximumListNesting) throw scope.Error(Code.NotEncodable, type.Offset, $"Lists nest at most {MaximumListNesting} levels for encoding.");
                CheckDice(scope, type.Constraints, type.Offset, Bound(build));
                node.Kind = GesSchemaNodeKind.List;
                node.MaxCount = diceMax;
                node.Width = BitLength((ulong)diceMax);
                build.Nodes.Add(node);
                CheckNodeCount(build, scope, type.Offset);
                var roll = new GesSchemaNode { Name = "[]", Path = path + "[]", ValueKind = GesSchemaTypeKind.Number, SchemaName = "", Next = index + 2 };
                Number(build, scope, RollConstraints(type.Constraints), type.Offset, ref roll);
                build.Nodes.Add(roll);
                break;
            case GesSchemaTypeKind.Point:
            case GesSchemaTypeKind.Vector:
                node.Kind = GesSchemaNodeKind.Group;
                build.Nodes.Add(node);
                if (type.Axes is { } axes)
                {
                    foreach (var axis in axes) AddAxis(build, scope, axis.Axis, axis.Constraints, path, type.Offset);
                }
                else
                {
                    var none = new GesResolvedConstraints();
                    foreach (var axis in "xyz") AddAxis(build, scope, axis, none, path, type.Offset);
                }

                break;
            case GesSchemaTypeKind.Reference:
                node.Kind = GesSchemaNodeKind.Group;
                node.SchemaName = type.ReferenceName;
                build.Nodes.Add(node);
                var reference = type.Reference!;
                foreach (var field in reference.Fields) AddField(build, reference.Scope, field, path, lists, top: false);
                break;
            default:
                throw scope.Error(Code.NotEncodable, type.Offset, $"A {type.Kind} has no width and is only for documents.");
        }

        if (node.Kind is not (GesSchemaNodeKind.List or GesSchemaNodeKind.Group)) build.Nodes.Add(node);
        var added = build.Nodes[index];
        added.Next = build.Nodes.Count;
        build.Nodes[index] = added;
    }

    // References expand into nodes, so a small source can ask for more than any encoding holds.
    private static void CheckNodeCount(Build build, GesFileScope scope, int offset)
    {
        if (build.Nodes.Count >= MaximumNodeCount) throw scope.Error(Code.TooLarge, offset, $"A bound layout has at most {MaximumNodeCount} fields.");
    }

    private static bool HasBits(List<GesSchemaNode> nodes, int start)
    {
        for (var index = start; index < nodes.Count; index++)
            if (nodes[index].Width > 0 || nodes[index].Optional) return true;
        return false;
    }

    private static void AddAxis(Build build, GesFileScope scope, char axis, GesResolvedConstraints constraints, string path, int offset)
    {
        CheckNodeCount(build, scope, offset);
        var index = build.Nodes.Count;
        var name = axis.ToString();
        var node = new GesSchemaNode { Name = name, Path = path + "." + name, ValueKind = GesSchemaTypeKind.Number, SchemaName = "", Next = index + 1 };
        Number(build, scope, constraints, offset, ref node);
        build.Nodes.Add(node);
    }

    private static Func<GesResolvedValue, GesSchemaDecimal> Bound(Build build) => value =>
    {
        build.Parameters.TryGet(value.Parameter, out var bound);
        return bound.Number;
    };

    // The rolls of a Dice are positive Int32 values.
    private static GesResolvedConstraints RollConstraints(GesResolvedConstraints constraints) => new()
    {
        Integer = true,
        Lower = constraints.Lower ?? new GesResolvedValue { Value = new GesSchemaDecimal(1, 0) },
        Upper = constraints.Upper ?? new GesResolvedValue { Value = new GesSchemaDecimal(int.MaxValue, 0) }
    };

    internal static void CheckDice(GesFileScope scope, GesResolvedConstraints constraints, int offset, Func<GesResolvedValue, GesSchemaDecimal>? parameter)
    {
        var rolls = RollConstraints(constraints);
        var numeric = Compute(scope, rolls, parameter);
        if ((parameter is not null || !rolls.Lower!.IsParameter) && numeric.Lower < 1) throw scope.Error(Code.InvalidBound, offset, "A Dice roll is at least 1.");
        if ((parameter is not null || !rolls.Upper!.IsParameter) && numeric.Upper > int.MaxValue) throw scope.Error(Code.InvalidBound, offset, "A Dice roll fits Int32.");
    }

    private static void Number(Build build, GesFileScope scope, GesResolvedConstraints constraints, int offset, ref GesSchemaNode node)
    {
        var numeric = Compute(scope, constraints, Bound(build));
        node.HasRange = constraints.Lower is not null;
        node.Wrapping = constraints.Wrapping;
        if (constraints.Step is not null)
        {
            node.Kind = GesSchemaNodeKind.Quantized;
            node.P = numeric.P;
            TryScale(1, numeric.K, out var tenPower);
            node.PBinary = numeric.P;
            node.SBinary = tenPower;
            node.Lower = numeric.Lower;
            var span = (ulong)(numeric.Upper - numeric.Lower);
            node.MaxIndex = constraints.Wrapping ? span - 1 : span;
            node.Width = BitLength(node.MaxIndex);
            node.LowerValue = numeric.Lower * numeric.P / node.SBinary;
            node.UpperValue = numeric.Upper * numeric.P / node.SBinary;
        }
        else if (constraints.Integer)
        {
            node.Kind = GesSchemaNodeKind.Integer;
            node.Lower = numeric.Lower;
            if (node.HasRange)
            {
                var span = (ulong)numeric.Upper - (ulong)numeric.Lower;
                node.MaxIndex = constraints.Wrapping ? span - 1 : span;
                node.Width = BitLength(node.MaxIndex);
            }
            else
            {
                node.MaxIndex = ulong.MaxValue;
                node.Width = 64;
            }
        }
        else
        {
            if (constraints.Wrapping) throw scope.Error(Code.NotEncodable, offset, "A wrapping field without a step or integer has no index and is only for documents.");
            node.Kind = GesSchemaNodeKind.Float;
            node.Width = 64;
            if (node.HasRange)
            {
                node.LowerValue = Binary(constraints.Lower!, build);
                node.UpperValue = Binary(constraints.Upper!, build);
            }
        }

        if (constraints.Delta is not null)
        {
            if (BitLength(numeric.DeltaRange * 2) >= node.Width) throw scope.Error(Code.InvalidConstraint, constraints.Delta.Offset, "A delta range is narrower than its field.");
        }
    }

    private static double Binary(GesResolvedValue value, Build build)
    {
        var number = value.Value;
        if (value.IsParameter)
        {
            build.Parameters.TryGet(value.Parameter, out var bound);
            number = bound.Number;
        }

        return double.Parse(number.Canonical, CultureInfo.InvariantCulture);
    }
}
