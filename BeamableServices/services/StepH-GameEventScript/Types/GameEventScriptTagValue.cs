#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptTagValue : GameEventScriptValue
{
    private const double Pi = 3.1415926535897932384626433833d;
    private const double EulerNumber = 2.7182818284590452353602874714d;
    private const double Tau = 6.2831853071795864769252867666d;
    private const double Phi = 1.6180339887498948482045868344d;

    public static readonly GameEventScriptTagValue Empty = new(string.Empty);

    public static GameEventScriptTagValue Create(string? value) => string.IsNullOrEmpty(value) ? Empty : new GameEventScriptTagValue(value);

    private GameEventScriptTagValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Tag;

    public override string AsText() => Value;

    public override bool AsBoolean()
    {
        if (string.Equals(Value, "true", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(Value, "false", StringComparison.Ordinal))
        {
            return false;
        }

        if (TryConvertToNumber(out var value))
        {
            var number = value.AsNumber();
            return number != 0d && !double.IsNaN(number);
        }

        return false;
    }

    public override double AsNumber() => TryConvertToNumber(out var value) ? value.AsNumber() : 0d;

    public override IReadOnlyList<GameEventScriptValue> AsList() => CreateCharacterList(Value);

    public override IEnumerable<GameEventScriptValue> AsEnumerable() => AsList();

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        if (string.Equals(Value, "infinity", StringComparison.Ordinal))
        {
            value = GesFloatInfinity();
            return true;
        }

        if (string.Equals(Value, "negativeinfinity", StringComparison.Ordinal))
        {
            value = GesFloatNegativeInfinity();
            return true;
        }

        if (string.Equals(Value, "nan", StringComparison.Ordinal))
        {
            value = GesFloatNaN();
            return true;
        }

        if (string.Equals(Value, "pi", StringComparison.Ordinal))
        {
            value = GesFloat(Pi);
            return true;
        }

        if (string.Equals(Value, "e", StringComparison.Ordinal))
        {
            value = GesFloat(EulerNumber);
            return true;
        }

        if (string.Equals(Value, "tau", StringComparison.Ordinal))
        {
            value = GesFloat(Tau);
            return true;
        }

        if (string.Equals(Value, "phi", StringComparison.Ordinal))
        {
            value = GesFloat(Phi);
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
