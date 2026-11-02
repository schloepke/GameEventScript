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

    // Each spelling precedes every shorter one it starts or ends with, so a match is always the longest one; the
    // symbol precedes its ASCII spelling, which document formatting relies on.
    internal static readonly Suffix[] Suffixes =
    [
        new("mW/m²", UnitWattPerSquareMeter, -3),
        new("mW/m^2", UnitWattPerSquareMeter, -3),
        new("kW/m²", UnitWattPerSquareMeter, 3),
        new("kW/m^2", UnitWattPerSquareMeter, 3),
        new("W/m²", UnitWattPerSquareMeter),
        new("W/m^2", UnitWattPerSquareMeter),
        new("m/s²", UnitMeterPerSecondSquared),
        new("m/s^2", UnitMeterPerSecondSquared),
        new("deg/s", UnitDegreePerSecond),
        new("cm²", UnitSquareMeter, -4),
        new("cm^2", UnitSquareMeter, -4),
        new("km²", UnitSquareMeter, 6),
        new("km^2", UnitSquareMeter, 6),
        new("°/s", UnitDegreePerSecond),
        new("m/s", UnitMeterPerSecond),
        new("m²", UnitSquareMeter),
        new("m^2", UnitSquareMeter),
        new("m³", UnitCubicMeter),
        new("m^3", UnitCubicMeter),
        new("kWh", UnitJoule, 5, 36),
        new("kHz", UnitHertz, 3),
        new("deg", UnitDegree),
        new("Wh", UnitJoule, 2, 36),
        new("Hz", UnitHertz),
        new("/s", UnitHertz),
        new("dB", UnitDecibel),
        new("km", UnitMeter, 3),
        new("ms", UnitSecond, -3),
        new("mm", UnitMeter, -3),
        new("kg", UnitKilogram),
        new("kN", UnitNewton, 3),
        new("kJ", UnitJoule, 3),
        new("MJ", UnitJoule, 6),
        new("mW", UnitWatt, -3),
        new("kW", UnitWatt, 3),
        new("MW", UnitWatt, 6),
        new("kV", UnitVolt, 3),
        new("°", UnitDegree),
        new("m", UnitMeter),
        new("s", UnitSecond),
        new("t", UnitKilogram, 3),
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
        => (uint)unit - 1 <= (uint)UnitDecibel - 1; // UnitIds 1 to 18

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
            UnitSquareMeter => "squareMeter",
            UnitCubicMeter => "cubicMeter",
            UnitWattPerSquareMeter => "wattPerSquareMeter",
            UnitKelvin => "kelvin",
            UnitDegreePerSecond => "degreePerSecond",
            UnitDecibel => "decibel",
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
            UnitSquareMeter => "m²",
            UnitCubicMeter => "m³",
            UnitWattPerSquareMeter => "W/m²",
            UnitKelvin => "K",
            UnitDegreePerSecond => "°/s",
            UnitDecibel => "dB",
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
            "squareMeter" => UnitSquareMeter,
            "cubicMeter" => UnitCubicMeter,
            "wattPerSquareMeter" => UnitWattPerSquareMeter,
            "kelvin" => UnitKelvin,
            "degreePerSecond" => UnitDegreePerSecond,
            "decibel" => UnitDecibel,
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
            "m²" or "m^2" => UnitSquareMeter,
            "m³" or "m^3" => UnitCubicMeter,
            "W/m²" or "W/m^2" => UnitWattPerSquareMeter,
            "K" => UnitKelvin,
            "°/s" or "degree/s" or "deg/s" => UnitDegreePerSecond,
            "dB" => UnitDecibel,
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
            (UnitWatt, UnitJoule) => UnitHertz,
            (UnitMeterPerSecond, UnitHertz) => UnitMeter,
            (UnitMeterPerSecond, UnitMeter) => UnitHertz,
            (UnitDegreePerSecond, UnitHertz) => UnitDegree,
            (UnitDegreePerSecond, UnitDegree) => UnitHertz,
            (UnitMeterPerSecondSquared, UnitHertz) => UnitMeterPerSecond,
            (UnitMeterPerSecondSquared, UnitMeterPerSecond) => UnitHertz,
            (UnitSquareMeter, UnitMeter) => UnitMeter,
            (UnitCubicMeter, UnitMeter) => UnitSquareMeter,
            (UnitCubicMeter, UnitSquareMeter) => UnitMeter,
            (UnitWatt, UnitSquareMeter) => UnitWattPerSquareMeter,
            (UnitWatt, UnitWattPerSquareMeter) => UnitSquareMeter,
            _ => null
        };
    }

    // Unit of a power. Exponents 0 and 1 give none and the unit itself; 2 and 3 follow the product derivations, and
    // the square and cube root exponents 0.5 and binary64 1/3 invert them, so `m` squared is `m²` and the cube root of
    // `m³` is `m`. Every other quantity power is unsupported.
    internal static GesQuantityUnit? Power(GesQuantityUnit unit, double exponent)
    {
        if (unit == UnitNone || exponent == 0d) return UnitNone;
        if (exponent == 1d) return unit;
        if (exponent == 2d) return Product(unit, unit);
        if (exponent == 3d) return Product(unit, unit) is { } square ? Product(square, unit) : null;
        if (exponent is not (0.5d or 1d / 3d)) return null;
        // Every unit has its symbol in the spelling table, so the search needs no UnitId bound.
        foreach (var candidate in Suffixes)
            if (Power(candidate.Unit, exponent == 0.5d ? 2d : 3d) == unit) return candidate.Unit;
        return null;
    }

    // Percentages scale and offset a quantity by its own magnitude. A decibel level is logarithmic, so a percentage of
    // it has no meaning and every arithmetic combination with a percentage is invalid.
    internal static bool AcceptsPercentage(this GesQuantityUnit unit) => unit != UnitDecibel;

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
            (UnitMeter, UnitMeter) => UnitSquareMeter,
            (UnitSquareMeter, UnitMeter) => UnitCubicMeter,
            (UnitWattPerSquareMeter, UnitSquareMeter) => UnitWatt,
            _ => null
        };
}
