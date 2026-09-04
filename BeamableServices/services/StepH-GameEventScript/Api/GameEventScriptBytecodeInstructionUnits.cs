// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents a game event script bytecode instruction units.
/// </summary>
public static class GameEventScriptBytecodeInstructionUnits
{
    /// <summary>
    /// Determines whether is numeric unit.
    /// </summary>
    /// <param name="unit">The unit value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool IsNumericUnit(this GameEventScriptBytecodeInstructionUnit unit)
        => unit is GameEventScriptBytecodeInstructionUnit.UnitDegree or GameEventScriptBytecodeInstructionUnit.UnitMeter or GameEventScriptBytecodeInstructionUnit.UnitSecond;

    internal static GameEventScriptBytecodeInstructionUnit ToStoredUnit(this GameEventScriptBytecodeInstructionUnit? unit)
        => unit is { } value && value.IsNumericUnit() ? value : GameEventScriptBytecodeInstructionUnit.UnitNone;

    internal static GameEventScriptBytecodeInstructionUnit ToStoredUnit(this GameEventScriptBytecodeInstructionUnit unit)
        => unit.IsNumericUnit() ? unit : GameEventScriptBytecodeInstructionUnit.UnitNone;

    /// <summary>
    /// Determines whether is quantity type name.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool IsQuantityTypeName(string? typeName)
        => typeName != null && typeName.StartsWith("quantity:", StringComparison.Ordinal);

    /// <summary>
    /// Converts this value to a type name.
    /// </summary>
    /// <param name="unit">The unit value.</param>
    /// <returns>The result of the operation.</returns>
    public static string ToTypeName(this GameEventScriptBytecodeInstructionUnit unit)
        => unit switch
        {
            GameEventScriptBytecodeInstructionUnit.UnitDegree => "degree",
            GameEventScriptBytecodeInstructionUnit.UnitMeter => "meter",
            GameEventScriptBytecodeInstructionUnit.UnitSecond => "second",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    /// <summary>
    /// Converts this value to a suffix.
    /// </summary>
    /// <param name="unit">The unit value.</param>
    /// <returns>The result of the operation.</returns>
    public static string ToSuffix(this GameEventScriptBytecodeInstructionUnit unit)
        => unit switch
        {
            GameEventScriptBytecodeInstructionUnit.UnitDegree => "\u00B0",
            GameEventScriptBytecodeInstructionUnit.UnitMeter => "m",
            GameEventScriptBytecodeInstructionUnit.UnitSecond => "s",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    /// <summary>
    /// Parses the type name.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptBytecodeInstructionUnit? ParseTypeName(string? typeName)
        => typeName switch
        {
            "degree" or "\u00B0" => GameEventScriptBytecodeInstructionUnit.UnitDegree,
            "meter" => GameEventScriptBytecodeInstructionUnit.UnitMeter,
            "second" => GameEventScriptBytecodeInstructionUnit.UnitSecond,
            _ => null
        };

    /// <summary>
    /// Parses the quantity type name.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptBytecodeInstructionUnit? ParseQuantityTypeName(string? typeName)
        => typeName != null && typeName.StartsWith("quantity:", StringComparison.Ordinal)
            ? ParseQuantityName(typeName["quantity:".Length..])
            : null;

    private static GameEventScriptBytecodeInstructionUnit? ParseQuantityName(string? quantityName)
        => quantityName switch
        {
            "none" => GameEventScriptBytecodeInstructionUnit.UnitNone,
            "degree" => GameEventScriptBytecodeInstructionUnit.UnitDegree,
            "m" or "meter" => GameEventScriptBytecodeInstructionUnit.UnitMeter,
            "s" or "second" => GameEventScriptBytecodeInstructionUnit.UnitSecond,
            _ => null
        };

}
