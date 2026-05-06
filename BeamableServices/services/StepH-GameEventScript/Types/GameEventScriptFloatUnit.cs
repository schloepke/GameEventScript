#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Types;

public enum GameEventScriptFloatUnit
{
    Degree,
    Meter,
    Second
}


public static class GameEventScriptFloatUnits
{
    public static bool TryParseTypeName(string? typeName, out GameEventScriptFloatUnit unit)
    {
        unit = default;
        return typeName switch
        {
            "degree" => Set(GameEventScriptFloatUnit.Degree, out unit),
            "meter" => Set(GameEventScriptFloatUnit.Meter, out unit),
            "second" => Set(GameEventScriptFloatUnit.Second, out unit),
            _ => false
        };
    }

    public static string ToTypeName(this GameEventScriptFloatUnit unit)
        => unit switch
        {
            GameEventScriptFloatUnit.Degree => "degree",
            GameEventScriptFloatUnit.Meter => "meter",
            GameEventScriptFloatUnit.Second => "second",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript double unit.")
        };

    public static string ToSuffix(this GameEventScriptFloatUnit unit)
        => unit switch
        {
            GameEventScriptFloatUnit.Degree => "\u00B0",
            GameEventScriptFloatUnit.Meter => "m",
            GameEventScriptFloatUnit.Second => "s",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript double unit.")
        };

    private static bool Set(GameEventScriptFloatUnit value, out GameEventScriptFloatUnit unit)
    {
        unit = value;
        return true;
    }
}