#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Api;

public static class GameEventScriptBytecodeInstructionUnits
{
    public static bool IsNumericUnit(this GameEventScriptBytecodeInstructionUnit unit)
        => unit is GameEventScriptBytecodeInstructionUnit.UnitDegree
            or GameEventScriptBytecodeInstructionUnit.UnitMeter
            or GameEventScriptBytecodeInstructionUnit.UnitSecond;

    internal static GameEventScriptBytecodeInstructionUnit ToStoredUnit(this GameEventScriptBytecodeInstructionUnit? unit)
        => unit is { } value && value.IsNumericUnit()
            ? value
            : GameEventScriptBytecodeInstructionUnit.UnitNone;

    internal static GameEventScriptBytecodeInstructionUnit ToStoredUnit(this GameEventScriptBytecodeInstructionUnit unit)
        => unit.IsNumericUnit()
            ? unit
            : GameEventScriptBytecodeInstructionUnit.UnitNone;

    internal static GameEventScriptBytecodeInstructionUnit? ToOptionalNumericUnit(this GameEventScriptBytecodeInstructionUnit unit)
        => unit.IsNumericUnit() ? unit : null;

    internal static byte ToUnitAndFlags(this GameEventScriptBytecodeInstructionUnit? unit)
        => GameEventScriptBytecodeInstruction.EncodeUnitAndFlags(unit);

    internal static byte ToUnitAndFlags(this GameEventScriptBytecodeInstructionUnit unit)
        => GameEventScriptBytecodeInstruction.EncodeUnitAndFlags(unit);

    public static bool TryParseTypeName(string? typeName, out GameEventScriptBytecodeInstructionUnit unit)
    {
        unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        return typeName switch
        {
            "degree" or "\u00B0" => Set(GameEventScriptBytecodeInstructionUnit.UnitDegree, out unit),
            "meter" => Set(GameEventScriptBytecodeInstructionUnit.UnitMeter, out unit),
            "second" => Set(GameEventScriptBytecodeInstructionUnit.UnitSecond, out unit),
            _ => false
        };
    }

    public static bool TryParseQuantityName(string? quantityName, out GameEventScriptBytecodeInstructionUnit unit)
    {
        unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        return quantityName switch
        {
            "degree" => Set(GameEventScriptBytecodeInstructionUnit.UnitDegree, out unit),
            "m" or "meter" => Set(GameEventScriptBytecodeInstructionUnit.UnitMeter, out unit),
            "s" or "second" => Set(GameEventScriptBytecodeInstructionUnit.UnitSecond, out unit),
            _ => false
        };
    }

    public static bool TryParseQuantityTypeName(string? typeName, out GameEventScriptBytecodeInstructionUnit unit)
    {
        unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        return typeName is { } value &&
               value.StartsWith("quantity:", StringComparison.Ordinal) &&
               TryParseQuantityName(value["quantity:".Length..], out unit);
    }

    public static bool IsQuantityTypeName(string? typeName)
        => typeName is { } value &&
           value.StartsWith("quantity:", StringComparison.Ordinal);

    public static string ToQuantityTypeName(this GameEventScriptBytecodeInstructionUnit unit)
        => $"quantity:{unit.ToTypeName()}";

    public static string ToTypeName(this GameEventScriptBytecodeInstructionUnit unit)
        => unit switch
        {
            GameEventScriptBytecodeInstructionUnit.UnitDegree => "degree",
            GameEventScriptBytecodeInstructionUnit.UnitMeter => "meter",
            GameEventScriptBytecodeInstructionUnit.UnitSecond => "second",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    public static string ToSuffix(this GameEventScriptBytecodeInstructionUnit unit)
        => unit switch
        {
            GameEventScriptBytecodeInstructionUnit.UnitDegree => "\u00B0",
            GameEventScriptBytecodeInstructionUnit.UnitMeter => "m",
            GameEventScriptBytecodeInstructionUnit.UnitSecond => "s",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    private static bool Set(GameEventScriptBytecodeInstructionUnit value, out GameEventScriptBytecodeInstructionUnit unit)
    {
        unit = value;
        return true;
    }
}
