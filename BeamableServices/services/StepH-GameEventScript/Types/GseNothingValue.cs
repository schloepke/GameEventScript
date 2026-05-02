#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseNothingValue : GseValue
{
    public static readonly GseNothingValue Instance = new();

    private GseNothingValue()
    {
    }

    public override GseValueKind Kind => GseValueKind.Nothing;

    public override string AsText() => string.Empty;

    public override bool AsBoolean() => false;

    public override long AsInteger() => 0;

    public override decimal AsNumber() => 0m;

    public override GseOptionalValue AsOptional() => GseOptionalValue.None;

    public override IReadOnlyList<GseValue> AsList() => [];

    public override IReadOnlyDictionary<string, GseValue> AsDictionary() => GseDictionaryValue.EmptyView;

    public override ISet<GseValue> AsSet() => new SortedSet<GseValue>(StableComparer);

    public override GseDiceValue AsDice() => GseDiceValue.Empty;

    public override bool HasSemanticValue() => false;

    public override bool IsSemanticallyEmpty() => true;

    internal override bool TryConvertToNumber(out GseValue value)
    {
        value = Decimal(0m);
        return true;
    }

    internal override bool TryConvertToInteger(out GseValue value)
    {
        value = Integer(0);
        return true;
    }

    internal override bool TryConvertToBoolean(out GseValue value)
    {
        value = Boolean(false);
        return true;
    }

    internal override bool TryConvertToText(out GseValue value)
    {
        value = Text(string.Empty);
        return true;
    }

    internal override bool TryConvertToList(out GseValue value)
    {
        value = List([]);
        return true;
    }

    internal override bool TryConvertToDictionary(out GseValue value)
    {
        value = Dictionary(new Dictionary<string, GseValue>(StringComparer.Ordinal));
        return true;
    }

    internal override bool TryConvertToSet(out GseValue value)
    {
        value = Set([]);
        return true;
    }

    internal override bool TryConvertToDice(out GseValue value)
    {
        value = Dice(GseDiceValue.Empty);
        return true;
    }
}
