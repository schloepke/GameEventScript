#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseVector3Value : GseValue
{
    public static readonly GseVector3Value Zero = new(0m, 0m, 0m, null);

    public static GseVector3Value GseVector3(decimal x, decimal y, decimal z, GseDecimalUnit? unit = null)
        => x == 0m && y == 0m && z == 0m && unit is null ? Zero : new GseVector3Value(x, y, z, unit);

    private GseVector3Value(decimal x, decimal y, decimal z, GseDecimalUnit? unit)
    {
        X = x;
        Y = y;
        Z = z;
        Unit = unit;
        Components = CreateReadOnlyList(new[] { Decimal(x, unit), Decimal(y, unit), Decimal(z, unit) });
        Members = new ReadOnlyDictionary<string, GseValue>(
            new Dictionary<string, GseValue>(StringComparer.Ordinal)
            {
                ["x"] = Decimal(x, unit),
                ["y"] = Decimal(y, unit),
                ["z"] = Decimal(z, unit)
            });
    }

    public decimal X { get; }
    public decimal Y { get; }
    public decimal Z { get; }
    public GseDecimalUnit? Unit { get; }
    public override GseValueKind Kind => GseValueKind.Vector3;

    private IReadOnlyList<GseValue> Components { get; }
    private IReadOnlyDictionary<string, GseValue> Members { get; }

    public override string AsText() => ToString();

    public override bool AsBoolean() => X != 0m || Y != 0m || Z != 0m;

    public override IReadOnlyList<GseValue> AsList() => Components;

    public override IReadOnlyDictionary<string, GseValue> AsDictionary() => Members;

    public override IEnumerable<GseValue> AsEnumerable() => Components;

    public override bool Contains(GseValue needle) => Components.Contains(needle);

    public override bool ContainsValue(GseValue needle) => Contains(needle);

    public override bool TryGetDictionaryMember(string key, out GseValue value)
    {
        if (Members.TryGetValue(key, out value))
        {
            return true;
        }

        value = Nothing;
        return false;
    }

    internal override bool TryConvertToBoolean(out GseValue value)
    {
        value = Boolean(AsBoolean());
        return true;
    }

    internal override bool TryConvertToText(out GseValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToList(out GseValue value)
    {
        value = List(Components);
        return true;
    }

    internal override bool TryConvertToDictionary(out GseValue value)
    {
        value = Dictionary(Members);
        return true;
    }
}
