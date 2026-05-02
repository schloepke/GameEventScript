#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Globalization;
using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseTextValue : GseValue
{
    public static readonly GseTextValue Empty = new(string.Empty);

    public static GseTextValue GseText(string? value) => string.IsNullOrEmpty(value) ? Empty : new GseTextValue(value);

    private GseTextValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
    public override GseValueKind Kind => GseValueKind.Text;

    public override string AsText() => Value;

    public override bool AsBoolean() => TryConvertToBoolean(out var value) && value.AsBoolean();

    public override long AsInteger() => TryConvertToInteger(out var value) ? value.AsInteger() : 0;

    public override decimal AsNumber() => TryConvertToNumber(out var value) ? value.AsNumber() : 0m;

    public override IReadOnlyList<GseValue> AsList() => CreateCharacterList(Value);

    public override IEnumerable<GseValue> AsEnumerable() => AsList();

    public override bool HasSemanticValue() => Value.Length > 0;

    public override bool IsSemanticallyEmpty() => Value.Length == 0;

    public override bool Contains(GseValue needle)
        => Value.Contains(ToComparableText(needle), System.StringComparison.Ordinal);

    public override bool StartsWith(GseValue prefix)
        => prefix.Kind == GseValueKind.Text && Value.StartsWith(prefix.AsText(), System.StringComparison.Ordinal);

    public override bool EndsWith(GseValue suffix)
        => suffix.Kind == GseValueKind.Text && Value.EndsWith(suffix.AsText(), System.StringComparison.Ordinal);

    internal override bool TryConvertToNumber(out GseValue value)
    {
        if (decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            value = Decimal(number);
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToInteger(out GseValue value)
    {
        if (long.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            value = Integer(integer);
            return true;
        }

        if (decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalNumber))
        {
            value = Integer(ToIntegerSaturated(decimalNumber));
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToBoolean(out GseValue value)
    {
        if (bool.TryParse(Value, out var boolean))
        {
            value = Boolean(boolean);
            return true;
        }

        if (string.Equals(Value, "1", System.StringComparison.Ordinal))
        {
            value = Boolean(true);
            return true;
        }

        if (string.Equals(Value, "0", System.StringComparison.Ordinal))
        {
            value = Boolean(false);
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToText(out GseValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToList(out GseValue value)
    {
        value = List(AsList());
        return true;
    }

}
