#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Types;

public enum GseDecimalUnit
{
    Degree,
    Meter,
    Second
}

public static class GseDecimalUnits
{
    public static bool TryParseTypeName(string? typeName, out GseDecimalUnit unit)
    {
        unit = default;
        return typeName switch
        {
            "degree" => Set(GseDecimalUnit.Degree, out unit),
            "meter" => Set(GseDecimalUnit.Meter, out unit),
            "second" => Set(GseDecimalUnit.Second, out unit),
            _ => false
        };
    }

    public static string ToTypeName(GseDecimalUnit unit)
        => unit switch
        {
            GseDecimalUnit.Degree => "degree",
            GseDecimalUnit.Meter => "meter",
            GseDecimalUnit.Second => "second",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown Gse decimal unit.")
        };

    public static string ToSuffix(GseDecimalUnit unit)
        => unit switch
        {
            GseDecimalUnit.Degree => "\u00B0",
            GseDecimalUnit.Meter => "m",
            GseDecimalUnit.Second => "s",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown Gse decimal unit.")
        };

    private static bool Set(GseDecimalUnit value, out GseDecimalUnit unit)
    {
        unit = value;
        return true;
    }
}
