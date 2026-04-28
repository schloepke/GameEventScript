#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.Flow.EventScript.Types;

public enum EventScriptDecimalUnit
{
    Degree,
    Meter,
    Second
}

public static class EventScriptDecimalUnits
{
    public static bool TryParseTypeName(string? typeName, out EventScriptDecimalUnit unit)
    {
        unit = default;
        return typeName switch
        {
            "degree" => Set(EventScriptDecimalUnit.Degree, out unit),
            "meter" => Set(EventScriptDecimalUnit.Meter, out unit),
            "second" => Set(EventScriptDecimalUnit.Second, out unit),
            _ => false
        };
    }

    public static string ToTypeName(EventScriptDecimalUnit unit)
        => unit switch
        {
            EventScriptDecimalUnit.Degree => "degree",
            EventScriptDecimalUnit.Meter => "meter",
            EventScriptDecimalUnit.Second => "second",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown EventScript decimal unit.")
        };

    public static string ToSuffix(EventScriptDecimalUnit unit)
        => unit switch
        {
            EventScriptDecimalUnit.Degree => "\u00B0",
            EventScriptDecimalUnit.Meter => "m",
            EventScriptDecimalUnit.Second => "s",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown EventScript decimal unit.")
        };

    private static bool Set(EventScriptDecimalUnit value, out EventScriptDecimalUnit unit)
    {
        unit = value;
        return true;
    }
}
