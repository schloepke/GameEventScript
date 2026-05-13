#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Types;

public enum GameEventScriptNumericUnit
{
    Percentage,
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
