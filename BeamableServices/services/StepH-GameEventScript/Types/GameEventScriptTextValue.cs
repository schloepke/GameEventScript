#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Globalization;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptTextValue : GameEventScriptValue
{
    public static readonly GameEventScriptTextValue Empty = new(string.Empty);

    public static GameEventScriptTextValue Create(string? value) => string.IsNullOrEmpty(value) ? Empty : new GameEventScriptTextValue(value);

    private GameEventScriptTextValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Text;

    public override string AsText() => Value;

    public override bool AsBoolean() => TryConvertToBoolean(out var value) && value.AsBoolean();

    public override long AsInteger() => TryConvertToInteger(out var value) ? value.AsInteger() : 0;

    public override decimal AsNumber() => TryConvertToNumber(out var value) ? value.AsNumber() : 0m;

    public override IReadOnlyList<GameEventScriptValue> AsList() => CreateCharacterList(Value);

    public override IEnumerable<GameEventScriptValue> AsEnumerable() => AsList();

    public override bool HasSemanticValue() => Value.Length > 0;

    public override bool IsSemanticallyEmpty() => Value.Length == 0;

    public override bool Contains(GameEventScriptValue needle)
        => Value.Contains(ToComparableText(needle), System.StringComparison.Ordinal);

    public override bool StartsWith(GameEventScriptValue prefix)
        => prefix.Kind == GameEventScriptValueKind.Text && Value.StartsWith(prefix.AsText(), System.StringComparison.Ordinal);

    public override bool EndsWith(GameEventScriptValue suffix)
        => suffix.Kind == GameEventScriptValueKind.Text && Value.EndsWith(suffix.AsText(), System.StringComparison.Ordinal);

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        if (decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            value = GesDecimal(number);
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
    {
        if (long.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            value = GesInteger(integer);
            return true;
        }

        if (decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalNumber))
        {
            value = GesInteger(ToIntegerSaturated(decimalNumber));
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToBoolean(out GameEventScriptValue value)
    {
        if (bool.TryParse(Value, out var boolean))
        {
            value = GesBoolean(boolean);
            return true;
        }

        if (string.Equals(Value, "1", System.StringComparison.Ordinal))
        {
            value = GesBoolean(true);
            return true;
        }

        if (string.Equals(Value, "0", System.StringComparison.Ordinal))
        {
            value = GesBoolean(false);
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = GesList(AsList());
        return true;
    }

}
