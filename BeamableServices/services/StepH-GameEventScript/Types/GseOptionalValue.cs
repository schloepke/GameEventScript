using System.Collections.Generic;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.GameEventScript.Types;

public sealed class GseOptionalValue : GseValue
{
    public static readonly GseOptionalValue None = new(false, Nothing);

    public static GseOptionalValue GseOptionalSome(GseValue? value) => value == null ? None : new GseOptionalValue(true, value);

    private GseOptionalValue(bool hasValue, GseValue value)
    {
        HasValue = hasValue;
        _value = value;
    }

    private readonly GseValue _value;

    public bool HasValue { get; }
    public GseValue Value => HasValue ? _value : Nothing;
    public override GseValueKind Kind => GseValueKind.Optional;

    public override GseOptionalValue AsOptional() => this;

    public override string AsText() => TryConvertToText(out var value) ? value.AsText() : string.Empty;

    public override bool AsBoolean() => TryConvertToBoolean(out var value) && value.AsBoolean();

    public override long AsInteger() => TryConvertToInteger(out var value) ? value.AsInteger() : 0;

    public override decimal AsNumber() => TryConvertToNumber(out var value) ? value.AsNumber() : 0m;

    public override IReadOnlyList<GseValue> AsList() => TryConvertToList(out var value) ? value.AsList() : System.Array.Empty<GseValue>();

    public override IReadOnlyDictionary<string, GseValue> AsDictionary() => TryConvertToDictionary(out var value) ? value.AsDictionary() : GseDictionaryValue.EmptyView;

    public override ISet<GseValue> AsSet() => TryConvertToSet(out var value) ? value.AsSet() : new SortedSet<GseValue>(StableComparer);

    public override GseDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : GseDiceValue.Empty;

    public override bool HasSemanticValue() => HasValue;

    public override bool IsSemanticallyEmpty() => !HasValue || Value.IsSemanticallyEmpty();

    public override bool TryUnwrapOptional(out GseValue unwrapped)
    {
        if (!HasValue)
        {
            unwrapped = default!;
            return false;
        }

        unwrapped = Value;
        return true;
    }

    internal override bool TryConvertToNumber(out GseValue value)
        => TryConvertValue(static source => source.TryConvertToNumber(out var converted) ? converted : null, out value);

    internal override bool TryConvertToInteger(out GseValue value)
        => TryConvertValue(static source => source.TryConvertToInteger(out var converted) ? converted : null, out value);

    internal override bool TryConvertToBoolean(out GseValue value)
        => TryConvertValue(static source => source.TryConvertToBoolean(out var converted) ? converted : null, out value);

    internal override bool TryConvertToText(out GseValue value)
        => TryConvertValue(static source => source.TryConvertToText(out var converted) ? converted : null, out value);

    internal override bool TryConvertToList(out GseValue value)
        => TryConvertValue(static source => source.TryConvertToList(out var converted) ? converted : null, out value);

    internal override bool TryConvertToDictionary(out GseValue value)
        => TryConvertValue(static source => source.TryConvertToDictionary(out var converted) ? converted : null, out value);

    internal override bool TryConvertToSet(out GseValue value)
        => TryConvertValue(static source => source.TryConvertToSet(out var converted) ? converted : null, out value);

    internal override bool TryConvertToDice(out GseValue value)
        => TryConvertValue(static source => source.TryConvertToDice(out var converted) ? converted : null, out value);

    private bool TryConvertValue(System.Func<GseValue, GseValue?> converter, out GseValue value)
    {
        if (!HasValue)
        {
            value = default!;
            return false;
        }

        var converted = converter(Value);
        if (converted == null)
        {
            value = default!;
            return false;
        }

        value = converted;
        return true;
    }

}
