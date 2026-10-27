// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using static GameEventScript.Api.GesQuantityUnit;

namespace GameEventScript.Api;

/// <summary>
/// Names, spellings and arithmetic of the closed set of GameEventScript quantity units.
/// </summary>
public static class GesQuantityUnits
{
    // One spelling of a unit suffix. Prefixed and energy spellings scale the decimal value exactly by
    // Factor × 10^Exponent before the single binary64 rounding; the value keeps the base unit.
    internal readonly struct Suffix
    {
        internal Suffix(string text, GesQuantityUnit unit, int exponent = 0, int factor = 1)
        {
            Text = text;
            Unit = unit;
            Exponent = exponent;
            Factor = factor;
        }

        internal string Text { get; }
        internal GesQuantityUnit Unit { get; }
        internal int Exponent { get; }
        internal int Factor { get; }
    }

    // Longest spellings first, so a suffix match is always the longest matching spelling.
    internal static readonly Suffix[] Suffixes =
    [
        new("m/s²", UnitMeterPerSecondSquared),
        new("m/s^2", UnitMeterPerSecondSquared),
        new("deg/s", UnitDegreePerSecond),
        new("°/s", UnitDegreePerSecond),
        new("m/s", UnitMeterPerSecond),
        new("kWh", UnitJoule, 5, 36),
        new("kHz", UnitHertz, 3),
        new("deg", UnitDegree),
        new("Wh", UnitJoule, 2, 36),
        new("Hz", UnitHertz),
        new("km", UnitMeter, 3),
        new("ms", UnitSecond, -3),
        new("kg", UnitKilogram),
        new("kN", UnitNewton, 3),
        new("kJ", UnitJoule, 3),
        new("MJ", UnitJoule, 6),
        new("kW", UnitWatt, 3),
        new("MW", UnitWatt, 6),
        new("kV", UnitVolt, 3),
        new("°", UnitDegree),
        new("m", UnitMeter),
        new("s", UnitSecond),
        new("N", UnitNewton),
        new("J", UnitJoule),
        new("W", UnitWatt),
        new("V", UnitVolt),
        new("A", UnitAmpere),
        new("K", UnitKelvin),
    ];

    /// <summary>
    /// Determines whether the unit is a defined quantity unit other than none.
    /// </summary>
    /// <param name="unit">The unit value.</param>
    /// <returns><see langword="true"/> for a defined unit other than <see cref="GesQuantityUnit.UnitNone"/>.</returns>
    public static bool IsNumericUnit(this GesQuantityUnit unit)
        => (uint)unit - 1 <= (uint)UnitHertz - 1 || unit is UnitKelvin or UnitDegreePerSecond; // UnitIds 1 to 12, 16 and 17

    internal static GesQuantityUnit ToStoredUnit(this GesQuantityUnit? unit)
        => unit is { } value && value.IsNumericUnit() ? value : UnitNone;

    internal static GesQuantityUnit ToStoredUnit(this GesQuantityUnit unit)
        => unit.IsNumericUnit() ? unit : UnitNone;

    /// <summary>
    /// Determines whether is quantity type name.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool IsQuantityTypeName(string? typeName)
        => typeName != null && typeName.StartsWith("quantity:", StringComparison.Ordinal);

    /// <summary>
    /// Converts this value to its portable unit type name, for example <c>meterPerSecond</c>.
    /// </summary>
    /// <param name="unit">The unit value.</param>
    /// <returns>The portable unit type name.</returns>
    public static string ToTypeName(this GesQuantityUnit unit)
        => unit switch
        {
            UnitDegree => "degree",
            UnitMeter => "meter",
            UnitSecond => "second",
            UnitMeterPerSecond => "meterPerSecond",
            UnitMeterPerSecondSquared => "meterPerSecondSquared",
            UnitKilogram => "kilogram",
            UnitNewton => "newton",
            UnitJoule => "joule",
            UnitWatt => "watt",
            UnitVolt => "volt",
            UnitAmpere => "ampere",
            UnitHertz => "hertz",
            UnitKelvin => "kelvin",
            UnitDegreePerSecond => "degreePerSecond",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    /// <summary>
    /// Converts this value to its canonical text suffix, for example <c>m/s</c>.
    /// </summary>
    /// <param name="unit">The unit value.</param>
    /// <returns>The canonical unit symbol that formatted quantities append.</returns>
    public static string ToSuffix(this GesQuantityUnit unit)
        => unit switch
        {
            UnitDegree => "°",
            UnitMeter => "m",
            UnitSecond => "s",
            UnitMeterPerSecond => "m/s",
            UnitMeterPerSecondSquared => "m/s²",
            UnitKilogram => "kg",
            UnitNewton => "N",
            UnitJoule => "J",
            UnitWatt => "W",
            UnitVolt => "V",
            UnitAmpere => "A",
            UnitHertz => "Hz",
            UnitKelvin => "K",
            UnitDegreePerSecond => "°/s",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    /// <summary>
    /// Parses a portable unit type name such as <c>meterPerSecond</c>; <c>°</c> is also accepted for degree.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The unit, or <see langword="null"/> for an unknown name.</returns>
    public static GesQuantityUnit? ParseTypeName(string? typeName)
        => typeName switch
        {
            "degree" or "°" => UnitDegree,
            "meter" => UnitMeter,
            "second" => UnitSecond,
            "meterPerSecond" => UnitMeterPerSecond,
            "meterPerSecondSquared" => UnitMeterPerSecondSquared,
            "kilogram" => UnitKilogram,
            "newton" => UnitNewton,
            "joule" => UnitJoule,
            "watt" => UnitWatt,
            "volt" => UnitVolt,
            "ampere" => UnitAmpere,
            "hertz" => UnitHertz,
            "kelvin" => UnitKelvin,
            "degreePerSecond" => UnitDegreePerSecond,
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

    // `:Quantity(...)` accepts none, every unit symbol and its ASCII spelling, and the long names meter, second and
    // degree. Prefixed spellings are literal sugar only. `degree` also stands for `°` because the lexer reads `°` so.
    internal static GesQuantityUnit? ParseQuantityName(string? quantityName)
        => quantityName switch
        {
            "none" => UnitNone,
            "degree" or "°" or "deg" => UnitDegree,
            "m" or "meter" => UnitMeter,
            "s" or "second" => UnitSecond,
            "m/s" => UnitMeterPerSecond,
            "m/s²" or "m/s^2" => UnitMeterPerSecondSquared,
            "kg" => UnitKilogram,
            "N" => UnitNewton,
            "J" => UnitJoule,
            "W" => UnitWatt,
            "V" => UnitVolt,
            "A" => UnitAmpere,
            "Hz" => UnitHertz,
            "K" => UnitKelvin,
            "°/s" or "degree/s" or "deg/s" => UnitDegreePerSecond,
            _ => null
        };

    // Parses a canonical unit symbol exactly as formatting writes it, for example `m/s`; the empty symbol is none.
    internal static GesQuantityUnit? ParseSymbol(string symbol)
    {
        if (symbol.Length == 0) return UnitNone;
        foreach (var candidate in Suffixes)
            if (candidate.Exponent == 0 && candidate.Factor == 1 && candidate.Unit.ToSuffix() == symbol && candidate.Text == symbol) return candidate.Unit;
        return null;
    }

    // Finds the longest suffix spelling that ends text[start..end], leaving at least one character before it.
    internal static bool TryMatchTrailingSuffix(string text, int start, int end, out Suffix suffix)
    {
        foreach (var candidate in Suffixes)
        {
            var length = candidate.Text.Length;
            if (end - start > length && string.CompareOrdinal(text, end - length, candidate.Text, 0, length) == 0)
            {
                suffix = candidate;
                return true;
            }
        }

        suffix = default;
        return false;
    }

    // Unit of a product. Multiplying by a unitless factor keeps the unit; other products follow the closed derivation
    // table and are otherwise unsupported.
    internal static GesQuantityUnit? Product(GesQuantityUnit left, GesQuantityUnit right)
    {
        if (left == UnitNone) return right;
        if (right == UnitNone) return left;
        return DerivedProduct(left, right) ?? DerivedProduct(right, left);
    }

    // Unit of a quotient. Dividing by a unitless divisor keeps the unit, equal units cancel, and other quotients follow
    // the closed derivation table and are otherwise unsupported.
    internal static GesQuantityUnit? Quotient(GesQuantityUnit dividend, GesQuantityUnit divisor)
    {
        if (divisor == UnitNone) return dividend;
        if (dividend == divisor) return UnitNone;
        return (dividend, divisor) switch
        {
            (UnitNone, UnitSecond) => UnitHertz,
            (UnitNone, UnitHertz) => UnitSecond,
            (UnitMeter, UnitSecond) => UnitMeterPerSecond,
            (UnitMeter, UnitMeterPerSecond) => UnitSecond,
            (UnitMeterPerSecond, UnitSecond) => UnitMeterPerSecondSquared,
            (UnitMeterPerSecond, UnitMeterPerSecondSquared) => UnitSecond,
            (UnitDegree, UnitSecond) => UnitDegreePerSecond,
            (UnitDegree, UnitDegreePerSecond) => UnitSecond,
            (UnitNewton, UnitKilogram) => UnitMeterPerSecondSquared,
            (UnitNewton, UnitMeterPerSecondSquared) => UnitKilogram,
            (UnitJoule, UnitMeter) => UnitNewton,
            (UnitJoule, UnitNewton) => UnitMeter,
            (UnitJoule, UnitSecond) => UnitWatt,
            (UnitJoule, UnitWatt) => UnitSecond,
            (UnitWatt, UnitVolt) => UnitAmpere,
            (UnitWatt, UnitAmpere) => UnitVolt,
            (UnitWatt, UnitHertz) => UnitJoule,
            _ => null
        };
    }

    private static GesQuantityUnit? DerivedProduct(GesQuantityUnit left, GesQuantityUnit right)
        => (left, right) switch
        {
            (UnitMeterPerSecond, UnitSecond) => UnitMeter,
            (UnitMeterPerSecondSquared, UnitSecond) => UnitMeterPerSecond,
            (UnitDegreePerSecond, UnitSecond) => UnitDegree,
            (UnitKilogram, UnitMeterPerSecondSquared) => UnitNewton,
            (UnitNewton, UnitMeter) => UnitJoule,
            (UnitWatt, UnitSecond) => UnitJoule,
            (UnitVolt, UnitAmpere) => UnitWatt,
            (UnitHertz, UnitSecond) => UnitNone,
            (UnitMeter, UnitHertz) => UnitMeterPerSecond,
            (UnitDegree, UnitHertz) => UnitDegreePerSecond,
            (UnitMeterPerSecond, UnitHertz) => UnitMeterPerSecondSquared,
            (UnitJoule, UnitHertz) => UnitWatt,
            _ => null
        };
}
