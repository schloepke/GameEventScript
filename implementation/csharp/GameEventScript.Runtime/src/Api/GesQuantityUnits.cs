// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Api;

/// <summary>
/// Represents a game event script bytecode instruction units.
/// </summary>
public static class GesQuantityUnits
{
    /// <summary>
    /// Determines whether is numeric unit.
    /// </summary>
    /// <param name="unit">The unit value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool IsNumericUnit(this GesQuantityUnit unit)
        => unit is GesQuantityUnit.UnitDegree or GesQuantityUnit.UnitMeter or GesQuantityUnit.UnitSecond;

    internal static GesQuantityUnit ToStoredUnit(this GesQuantityUnit? unit)
        => unit is { } value && value.IsNumericUnit() ? value : GesQuantityUnit.UnitNone;

    internal static GesQuantityUnit ToStoredUnit(this GesQuantityUnit unit)
        => unit.IsNumericUnit() ? unit : GesQuantityUnit.UnitNone;

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
    public static string ToTypeName(this GesQuantityUnit unit)
        => unit switch
        {
            GesQuantityUnit.UnitDegree => "degree",
            GesQuantityUnit.UnitMeter => "meter",
            GesQuantityUnit.UnitSecond => "second",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    /// <summary>
    /// Converts this value to a suffix.
    /// </summary>
    /// <param name="unit">The unit value.</param>
    /// <returns>The result of the operation.</returns>
    public static string ToSuffix(this GesQuantityUnit unit)
        => unit switch
        {
            GesQuantityUnit.UnitDegree => "\u00B0",
            GesQuantityUnit.UnitMeter => "m",
            GesQuantityUnit.UnitSecond => "s",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    /// <summary>
    /// Parses the type name.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesQuantityUnit? ParseTypeName(string? typeName)
        => typeName switch
        {
            "degree" or "\u00B0" => GesQuantityUnit.UnitDegree,
            "meter" => GesQuantityUnit.UnitMeter,
            "second" => GesQuantityUnit.UnitSecond,
            _ => null
        };

    /// <summary>
    /// Parses the quantity type name.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesQuantityUnit? ParseQuantityTypeName(string? typeName)
        => typeName != null && typeName.StartsWith("quantity:", StringComparison.Ordinal)
            ? ParseQuantityName(typeName["quantity:".Length..])
            : null;

    internal static GesQuantityUnit? ParseSourceQuantityTypeName(string? typeName)
        => typeName is not null && typeName.StartsWith("Quantity(", StringComparison.Ordinal) && typeName.EndsWith(")", StringComparison.Ordinal)
            ? ParseQuantityName(typeName["Quantity(".Length..^1])
            : null;

    private static GesQuantityUnit? ParseQuantityName(string? quantityName)
        => quantityName switch
        {
            "none" => GesQuantityUnit.UnitNone,
            "degree" or "°" => GesQuantityUnit.UnitDegree,
            "m" or "meter" => GesQuantityUnit.UnitMeter,
            "s" or "second" => GesQuantityUnit.UnitSecond,
            _ => null
        };

}
