// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Conformance;

internal static class ConformanceRuntimeValueCodec
{
    internal static GameEventScriptMessage DecodeMessage(ConformanceMessage message)
    {
        var arguments = new GameEventScriptMessageArgument[message.Arguments.Count];
        for (var index = 0; index < arguments.Length; index++)
            arguments[index] = new GameEventScriptMessageArgument(message.Arguments[index].Name, DecodeValue(message.Arguments[index].Value));
        return GameEventScriptMessage.Create(message.Name, arguments, message.Tags);
    }

    internal static GesValue DecodeValue(ConformanceValue value)
    {
        var unit = ParseUnit(value.Unit);
        switch (value.Type)
        {
            case ":Nothing": return GesValue.GesNothing();
            case ":Text": return GesValue.GesText(value.Value ?? string.Empty);
            case ":Tag": return GesValue.GesTag(value.Value ?? string.Empty);
            case ":Boolean": return GesValue.GesBoolean(string.Equals(value.Value, "true", StringComparison.Ordinal));
            case ":Number.int64": return GesValue.GesInteger(long.Parse(value.Value!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
            case ":Number.binary64": return GesValue.GesFloat(ParseBinary64(value.Value!));
            case ":Quantity.int64": return GesValue.GesInteger(long.Parse(value.Value!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), unit);
            case ":Quantity.binary64": return GesValue.GesFloat(ParseBinary64(value.Value!), unit);
            case ":Percentage": return GesValue.GesPercentage(ParseBinary64(value.Value!));
            case ":Vector": return GesValue.GesVector(ParseBinary64(value.X!), ParseBinary64(value.Y!), ParseBinary64(value.Z!), unit);
            case ":Point": return GesValue.GesPoint(ParseBinary64(value.X!), ParseBinary64(value.Y!), ParseBinary64(value.Z!), unit);
            case ":List":
            {
                var items = new GesValue[value.Items.Count];
                for (var index = 0; index < items.Length; index++) items[index] = DecodeValue(value.Items[index]);
                return GesValue.GesList(items);
            }
            case ":Map": return DecodeMap(value.Entries);
            case ":Dice":
            {
                var rolls = new int[value.Rolls.Count];
                for (var index = 0; index < rolls.Length; index++) rolls[index] = value.Rolls[index];
                return GesValue.GesDice(rolls);
            }
            case ":Range.int64":
                return GesValue.GesRange(
                    long.Parse(value.From!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
                    long.Parse(value.To!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
                    long.Parse(value.Step!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
            case ":Range.binary64": return GesValue.GesRange(ParseBinary64(value.From!), ParseBinary64(value.To!), ParseBinary64(value.Step!));
            case ":Message": return GesValue.GesMessage(DecodeMessage(value.Message!));
            default:
            {
                var keys = new string[value.Entries.Count];
                var values = new GesValue[value.Entries.Count];
                for (var index = 0; index < keys.Length; index++)
                {
                    keys[index] = value.Entries[index].Key;
                    values[index] = DecodeValue(value.Entries[index].Value);
                }
                return GesValue.GesRecord(value.Type.Substring(1), keys, values);
            }
        }
    }

    internal static GesValue DecodeValueAndMutateSource(ConformanceValue value)
    {
        switch (value.Type)
        {
            case ":List":
            {
                var items = new GesValue[value.Items.Count];
                for (var index = 0; index < items.Length; index++) items[index] = DecodeValue(value.Items[index]);
                var result = GesValue.GesList(items);
                if (items.Length > 0) items[0] = GesValue.GesNothing();
                return result;
            }
            case ":Map":
            {
                var keys = new string[value.Entries.Count];
                var values = new GesValue[value.Entries.Count];
                for (var index = 0; index < keys.Length; index++)
                {
                    keys[index] = value.Entries[index].Key;
                    values[index] = DecodeValue(value.Entries[index].Value);
                }
                var result = GesValue.GesMap(keys, values);
                if (keys.Length > 0) { keys[0] = "mutated"; values[0] = GesValue.GesNothing(); }
                return result;
            }
            case ":Dice":
            {
                var rolls = new int[value.Rolls.Count];
                for (var index = 0; index < rolls.Length; index++) rolls[index] = value.Rolls[index];
                var result = GesValue.GesDice(rolls);
                if (rolls.Length > 0) rolls[0] = int.MinValue;
                return result;
            }
            case var type when type.Length > 1 &&
                                   type is not ":Nothing" and not ":Text" and not ":Tag" and not ":Boolean" and
                                   not ":Number.int64" and not ":Number.binary64" and not ":Quantity.int64" and not ":Quantity.binary64" and
                                   not ":Percentage" and not ":Vector" and not ":Point" and not ":Range.int64" and not ":Range.binary64" and not ":Message":
            {
                var keys = new string[value.Entries.Count];
                var values = new GesValue[value.Entries.Count];
                for (var index = 0; index < keys.Length; index++)
                {
                    keys[index] = value.Entries[index].Key;
                    values[index] = DecodeValue(value.Entries[index].Value);
                }
                var result = GesValue.GesRecord(value.Type.Substring(1), keys, values);
                if (keys.Length > 0) { keys[0] = "mutated"; values[0] = GesValue.GesNothing(); }
                return result;
            }
            default: return DecodeValue(value);
        }
    }

    private static GesValue DecodeMap(IReadOnlyList<ConformanceValueEntry> entries)
    {
        var keys = new string[entries.Count];
        var values = new GesValue[entries.Count];
        for (var index = 0; index < keys.Length; index++)
        {
            keys[index] = entries[index].Key;
            values[index] = DecodeValue(entries[index].Value);
        }
        return GesValue.GesMap(keys, values);
    }

    internal static bool MessagesEqual(ConformanceMessage expected, GameEventScriptMessage actual, ConformanceComparisonOptions comparison)
    {
        if (!string.Equals(expected.Name, actual.Name, StringComparison.Ordinal) || expected.Tags.Count != actual.Tags.Count || expected.Arguments.Count != actual.Arguments.Count) return false;
        for (var index = 0; index < expected.Tags.Count; index++)
            if (!string.Equals(expected.Tags[index], actual.Tags[index], StringComparison.Ordinal)) return false;
        for (var index = 0; index < expected.Arguments.Count; index++)
        {
            if (!string.Equals(expected.Arguments[index].Name, actual.Arguments.NameAt(index), StringComparison.Ordinal)) return false;
            var actualValue = actual.Arguments.ValueAt(index);
            if (!ValuesEqual(expected.Arguments[index].Value, in actualValue, comparison)) return false;
        }
        return true;
    }

    internal static bool ValuesEqual(ConformanceValue expected, in GesValue actual, ConformanceComparisonOptions comparison)
    {
        // Expected transport values must not pass through factories that normalize storage kinds.
        // NaN is the explicitly supported conformance spelling of invalid mathematics (nothing).
        var kind = actual.ValueKind;
        switch (expected.Type)
        {
            case ":Nothing": return actual.IsNothing;
            case ":Boolean": return kind == GameEventScriptBytecodeTypeKind.Boolean && (expected.Value == "true") == actual.AsBoolean();
            case ":Number.int64":
            case ":Quantity.int64":
                return kind == Integer && ParseUnit(expected.Unit) == actual.ValueUnit && ParseInt64(expected.Value!) == actual.AsInteger();
            case ":Number.binary64":
            case ":Quantity.binary64":
            case ":Percentage":
            {
                var number = ParseBinary64(expected.Value!);
                if (double.IsNaN(number)) return actual.IsNothing;
                return kind == (expected.Type == ":Percentage" ? Percentage : Float) && ParseUnit(expected.Unit) == actual.ValueUnit && Binary64Equal(number, actual.AsNumber(), comparison);
            }
            case ":Text": return kind == Text && string.Equals(expected.Value, actual.AsText(), StringComparison.Ordinal);
            case ":Tag": return kind == Tag && string.Equals(expected.Value, actual.AsText(), StringComparison.Ordinal);
            case ":Vector":
            case ":Point":
            {
                var x = ParseBinary64(expected.X!);
                var y = ParseBinary64(expected.Y!);
                var z = ParseBinary64(expected.Z!);
                if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z)) return actual.IsNothing;
                return kind == (expected.Type == ":Vector" ? Vector : Point) && ParseUnit(expected.Unit) == actual.ValueUnit &&
                       Binary64Equal(x, actual.X, comparison) && Binary64Equal(y, actual.Y, comparison) && Binary64Equal(z, actual.Z, comparison);
            }
            case ":List":
            {
                if (kind != List) return false;
                var right = actual.AsList();
                if (expected.Items.Count != right.Length) return false;
                for (var index = 0; index < right.Length; index++)
                {
                    var rightValue = right[index];
                    if (!ValuesEqual(expected.Items[index], in rightValue, comparison)) return false;
                }
                return true;
            }
            case ":Map": return kind == Map && MapEntriesEqual(expected.Entries, actual.AsMap(), comparison);
            case ":Dice":
            {
                if (kind != Dice) return false;
                var right = actual.AsDice();
                if (expected.Rolls.Count != right.Length) return false;
                for (var index = 0; index < right.Length; index++) if (expected.Rolls[index] != right[index]) return false;
                return true;
            }
            case ":Range.int64":
                return kind == GameEventScriptBytecodeTypeKind.Range && actual.IntegerRange is { } integerRange &&
                       ParseInt64(expected.From!) == integerRange.From && ParseInt64(expected.To!) == integerRange.To && ParseInt64(expected.Step!) == integerRange.Step;
            case ":Range.binary64":
            {
                var from = ParseBinary64(expected.From!);
                var to = ParseBinary64(expected.To!);
                var step = ParseBinary64(expected.Step!);
                if (double.IsNaN(from) || double.IsNaN(to) || double.IsNaN(step)) return actual.IsNothing;
                return kind == GameEventScriptBytecodeTypeKind.Range && actual.FloatRange is { } floatRange && Binary64Equal(from, floatRange.From, comparison) &&
                       Binary64Equal(to, floatRange.To, comparison) && Binary64Equal(step, floatRange.Step, comparison);
            }
            case ":Message": return kind == Message && expected.Message is not null && actual.Message is not null && MessagesEqual(expected.Message, actual.Message, comparison);
            default:
                return kind == Custom && string.Equals(expected.Type.Substring(1), actual.CustomTypeName, StringComparison.Ordinal) && MapEntriesEqual(expected.Entries, actual.AsMap(), comparison);
        }
    }

    private static bool MapEntriesEqual(IReadOnlyList<ConformanceValueEntry> expected, GesValueMap? actual, ConformanceComparisonOptions comparison)
    {
        if (actual is null) return false;
        var entries = new Dictionary<string, ConformanceValue>(StringComparer.Ordinal);
        for (var index = 0; index < expected.Count; index++) entries[expected[index].Key] = expected[index].Value;
        if (entries.Count != actual.Length) return false;
        foreach (var entry in entries)
        {
            var value = actual.Get(entry.Key);
            if (value is not { } right || !ValuesEqual(entry.Value, in right, comparison)) return false;
        }
        return true;
    }

    private static long ParseInt64(string value) => long.Parse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    internal static string Describe(GameEventScriptMessage message) => message.ToString();

    private static GameEventScriptBytecodeInstructionUnit ParseUnit(string? unit)
        => unit?.TrimStart(':') switch
        {
            null => GameEventScriptBytecodeInstructionUnit.UnitNone,
            "m" => GameEventScriptBytecodeInstructionUnit.UnitMeter,
            "s" => GameEventScriptBytecodeInstructionUnit.UnitSecond,
            var name => GameEventScriptBytecodeInstructionUnits.ParseTypeName(name) ?? GameEventScriptBytecodeInstructionUnit.UnitInvalid
        };

    internal static double ParseBinary64(string value) => value switch
    {
        "NaN" => double.NaN,
        "Infinity" => double.PositiveInfinity,
        "-Infinity" => double.NegativeInfinity,
        _ => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)
    };

    internal static string FormatBinary64(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static bool Binary64Equal(double left, double right, ConformanceComparisonOptions comparison)
    {
        if (double.IsNaN(left) || double.IsNaN(right)) return double.IsNaN(left) && double.IsNaN(right);
        if (comparison.Binary64Mode == ConformanceBinary64ComparisonMode.Exact)
            return BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right);
        if (left == right) return true;
        if (double.IsInfinity(left) || double.IsInfinity(right)) return false;
        var leftOrdered = OrderedBits(left);
        var rightOrdered = OrderedBits(right);
        var distance = leftOrdered >= rightOrdered ? leftOrdered - rightOrdered : rightOrdered - leftOrdered;
        return distance <= comparison.MaxUlps;
    }

    private static ulong OrderedBits(double value)
    {
        var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        return (bits & 0x8000000000000000UL) != 0 ? ~bits : bits | 0x8000000000000000UL;
    }
}
