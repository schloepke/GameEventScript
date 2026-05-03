#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptTagValue : GameEventScriptValue
{
    public static readonly GameEventScriptTagValue Empty = new(string.Empty);

    public static GameEventScriptTagValue Create(string? value) => string.IsNullOrEmpty(value) ? Empty : new GameEventScriptTagValue(value);

    private GameEventScriptTagValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Tag;

    public override string AsText() => Value;

    public override decimal AsNumber() => TryConvertToNumber(out var value) ? value.AsNumber() : 0m;

    public override IReadOnlyList<GameEventScriptValue> AsList() => CreateCharacterList(Value);

    public override IEnumerable<GameEventScriptValue> AsEnumerable() => AsList();

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        if (string.Equals(Value, "infinity", StringComparison.Ordinal))
        {
            value = GesDecimalInfinity();
            return true;
        }

        if (string.Equals(Value, "negativeinfinity", StringComparison.Ordinal))
        {
            value = GesDecimalNegativeInfinity();
            return true;
        }

        if (string.Equals(Value, "nan", StringComparison.Ordinal))
        {
            value = GesDecimalNaN();
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(Value);
        return true;
    }

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = GesList(AsList());
        return true;
    }

}
