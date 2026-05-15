#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Types;

public enum GameEventScriptNumericUnit
{
    Degree,
    Meter,
    Second
}


public static class GameEventScriptNumericUnits
{
    public static bool TryParseTypeName(string? typeName, out GameEventScriptNumericUnit unit)
    {
        unit = default;
        return typeName switch
        {
            "degree" => Set(GameEventScriptNumericUnit.Degree, out unit),
            "meter" => Set(GameEventScriptNumericUnit.Meter, out unit),
            "second" => Set(GameEventScriptNumericUnit.Second, out unit),
            _ => false
        };
    }

    public static bool TryParseQuantityName(string? quantityName, out GameEventScriptNumericUnit unit)
    {
        unit = default;
        return quantityName switch
        {
            "degree" => Set(GameEventScriptNumericUnit.Degree, out unit),
            "m" or "meter" => Set(GameEventScriptNumericUnit.Meter, out unit),
            "s" or "second" => Set(GameEventScriptNumericUnit.Second, out unit),
            _ => false
        };
    }

    public static bool TryParseQuantityTypeName(string? typeName, out GameEventScriptNumericUnit unit)
    {
        unit = default;
        return typeName is { } value &&
               value.StartsWith("quantity:", StringComparison.Ordinal) &&
               TryParseQuantityName(value["quantity:".Length..], out unit);
    }

    public static bool IsQuantityTypeName(string? typeName)
        => typeName is { } value &&
           value.StartsWith("quantity:", StringComparison.Ordinal);

    public static string ToQuantityTypeName(this GameEventScriptNumericUnit unit)
        => $"quantity:{unit.ToTypeName()}";

    public static string ToTypeName(this GameEventScriptNumericUnit unit)
        => unit switch
        {
            GameEventScriptNumericUnit.Degree => "degree",
            GameEventScriptNumericUnit.Meter => "meter",
            GameEventScriptNumericUnit.Second => "second",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    public static string ToSuffix(this GameEventScriptNumericUnit unit)
        => unit switch
        {
            GameEventScriptNumericUnit.Degree => "\u00B0",
            GameEventScriptNumericUnit.Meter => "m",
            GameEventScriptNumericUnit.Second => "s",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    private static bool Set(GameEventScriptNumericUnit value, out GameEventScriptNumericUnit unit)
    {
        unit = value;
        return true;
    }
}
