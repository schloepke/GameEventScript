#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseTagValue : GseValue
{
    public static readonly GseTagValue Empty = new(string.Empty);

    public static GseTagValue GseTag(string? value) => string.IsNullOrEmpty(value) ? Empty : new GseTagValue(value);

    private GseTagValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
    public override GseValueKind Kind => GseValueKind.Tag;

    public override string AsText() => Value;

    public override decimal AsNumber() => TryConvertToNumber(out var value) ? value.AsNumber() : 0m;

    public override IReadOnlyList<GseValue> AsList() => CreateCharacterList(Value);

    public override IEnumerable<GseValue> AsEnumerable() => AsList();

    internal override bool TryConvertToNumber(out GseValue value)
    {
        if (string.Equals(Value, "infinity", StringComparison.Ordinal))
        {
            value = DecimalInfinity();
            return true;
        }

        if (string.Equals(Value, "negativeinfinity", StringComparison.Ordinal))
        {
            value = DecimalNegativeInfinity();
            return true;
        }

        if (string.Equals(Value, "nan", StringComparison.Ordinal))
        {
            value = DecimalNaN();
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToText(out GseValue value)
    {
        value = Text(Value);
        return true;
    }

    internal override bool TryConvertToList(out GseValue value)
    {
        value = List(AsList());
        return true;
    }

}
