#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Api;

public static class GameEventScriptBytecodeInstructionUnits
{
    public static bool IsNumericUnit(this GameEventScriptBytecodeInstructionUnit unit)
        => unit is GameEventScriptBytecodeInstructionUnit.UnitDegree or GameEventScriptBytecodeInstructionUnit.UnitMeter or GameEventScriptBytecodeInstructionUnit.UnitSecond;

    internal static GameEventScriptBytecodeInstructionUnit ToStoredUnit(this GameEventScriptBytecodeInstructionUnit? unit)
        => unit is { } value && value.IsNumericUnit() ? value : GameEventScriptBytecodeInstructionUnit.UnitNone;

    internal static GameEventScriptBytecodeInstructionUnit ToStoredUnit(this GameEventScriptBytecodeInstructionUnit unit)
        => unit.IsNumericUnit() ? unit : GameEventScriptBytecodeInstructionUnit.UnitNone;

    public static bool IsQuantityTypeName(string? typeName)
        => typeName != null && typeName.StartsWith("quantity:", StringComparison.Ordinal);

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

    public static GameEventScriptBytecodeInstructionUnit? ParseTypeName(string? typeName)
        => typeName switch
        {
            "degree" or "\u00B0" => GameEventScriptBytecodeInstructionUnit.UnitDegree,
            "meter" => GameEventScriptBytecodeInstructionUnit.UnitMeter,
            "second" => GameEventScriptBytecodeInstructionUnit.UnitSecond,
            _ => null
        };

    public static GameEventScriptBytecodeInstructionUnit? ParseQuantityTypeName(string? typeName)
        => typeName != null && typeName.StartsWith("quantity:", StringComparison.Ordinal)
            ? ParseQuantityName(typeName["quantity:".Length..])
            : null;

    private static GameEventScriptBytecodeInstructionUnit? ParseQuantityName(string? quantityName)
        => quantityName switch
        {
            "degree" => GameEventScriptBytecodeInstructionUnit.UnitDegree,
            "m" or "meter" => GameEventScriptBytecodeInstructionUnit.UnitMeter,
            "s" or "second" => GameEventScriptBytecodeInstructionUnit.UnitSecond,
            _ => null
        };
    
}
