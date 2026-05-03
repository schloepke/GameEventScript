#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Types;

public enum GameEventScriptDecimalUnit
{
    Degree,
    Meter,
    Second
}


public static class GameEventScriptDecimalUnits
{
    public static bool TryParseTypeName(string? typeName, out GameEventScriptDecimalUnit unit)
    {
        unit = default;
        return typeName switch
        {
            "degree" => Set(GameEventScriptDecimalUnit.Degree, out unit),
            "meter" => Set(GameEventScriptDecimalUnit.Meter, out unit),
            "second" => Set(GameEventScriptDecimalUnit.Second, out unit),
            _ => false
        };
    }

    public static string ToTypeName(this GameEventScriptDecimalUnit unit)
        => unit switch
        {
            GameEventScriptDecimalUnit.Degree => "degree",
            GameEventScriptDecimalUnit.Meter => "meter",
            GameEventScriptDecimalUnit.Second => "second",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript decimal unit.")
        };

    public static string ToSuffix(this GameEventScriptDecimalUnit unit)
        => unit switch
        {
            GameEventScriptDecimalUnit.Degree => "\u00B0",
            GameEventScriptDecimalUnit.Meter => "m",
            GameEventScriptDecimalUnit.Second => "s",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript decimal unit.")
        };

    private static bool Set(GameEventScriptDecimalUnit value, out GameEventScriptDecimalUnit unit)
    {
        unit = value;
        return true;
    }
}