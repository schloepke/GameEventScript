// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Conformance;

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
            var expectedValue = DecodeValue(expected.Arguments[index].Value);
            var actualValue = actual.Arguments.ValueAt(index);
            if (!ValuesEqual(in expectedValue, in actualValue, comparison)) return false;
        }
        return true;
    }

    internal static bool ValuesEqual(in GesValue expected, in GesValue actual, ConformanceComparisonOptions comparison)
    {
        if (expected.ValueKind != actual.ValueKind) return false;
        switch (expected.ValueKind)
        {
            case GameEventScriptBytecodeTypeKind.Nothing: return actual.IsNothing;
            case GameEventScriptBytecodeTypeKind.Boolean: return expected.AsBoolean() == actual.AsBoolean();
            case GameEventScriptBytecodeTypeKind.Integer: return expected.ValueUnit == actual.ValueUnit && expected.AsInteger() == actual.AsInteger();
            case GameEventScriptBytecodeTypeKind.Float:
                return (!double.IsFinite(expected.AsNumber()) || expected.ValueUnit == actual.ValueUnit) &&
                       Binary64Equal(expected.AsNumber(), actual.AsNumber(), comparison);
            case GameEventScriptBytecodeTypeKind.Percentage:
                return Binary64Equal(expected.AsNumber(), actual.AsNumber(), comparison);
            case GameEventScriptBytecodeTypeKind.Text:
            case GameEventScriptBytecodeTypeKind.Tag: return string.Equals(expected.AsText(), actual.AsText(), StringComparison.Ordinal);
            case GameEventScriptBytecodeTypeKind.Vector:
            case GameEventScriptBytecodeTypeKind.Point:
                return expected.ValueUnit == actual.ValueUnit && Binary64Equal(expected.X, actual.X, comparison) && Binary64Equal(expected.Y, actual.Y, comparison) && Binary64Equal(expected.Z, actual.Z, comparison);
            case GameEventScriptBytecodeTypeKind.List:
            {
                var left = expected.AsList();
                var right = actual.AsList();
                if (left.Length != right.Length) return false;
                for (var index = 0; index < left.Length; index++)
                {
                    var leftValue = left[index];
                    var rightValue = right[index];
                    if (!ValuesEqual(in leftValue, in rightValue, comparison)) return false;
                }
                return true;
            }
            case GameEventScriptBytecodeTypeKind.Map:
            case GameEventScriptBytecodeTypeKind.Custom:
            {
                if (expected.ValueKind == GameEventScriptBytecodeTypeKind.Custom &&
                    !string.Equals(expected.CustomTypeName, actual.CustomTypeName, StringComparison.Ordinal)) return false;
                var left = expected.AsMap();
                var right = actual.AsMap();
                if (left is null || right is null || left.Length != right.Length) return false;
                for (var index = 0; index < left.StorageLength; index++)
                {
                    var rightValue = right.Get(left.KeyAt(index));
                    if (rightValue is null) return false;
                    var leftValue = left.ValueAt(index);
                    var rightCopy = rightValue.Value;
                    if (!ValuesEqual(in leftValue, in rightCopy, comparison)) return false;
                }
                return true;
            }
            case GameEventScriptBytecodeTypeKind.Dice:
            {
                var left = expected.AsDice();
                var right = actual.AsDice();
                if (left.Length != right.Length) return false;
                for (var index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
                return true;
            }
            case GameEventScriptBytecodeTypeKind.Range:
            {
                var leftFrom = expected.IntegerRange?.From ?? expected.FloatRange?.From;
                var leftTo = expected.IntegerRange?.To ?? expected.FloatRange?.To;
                var leftStep = expected.IntegerRange?.Step ?? expected.FloatRange?.Step;
                var rightFrom = actual.IntegerRange?.From ?? actual.FloatRange?.From;
                var rightTo = actual.IntegerRange?.To ?? actual.FloatRange?.To;
                var rightStep = actual.IntegerRange?.Step ?? actual.FloatRange?.Step;
                return leftFrom is not null && leftTo is not null && leftStep is not null &&
                       rightFrom is not null && rightTo is not null && rightStep is not null &&
                       Binary64Equal(leftFrom.Value, rightFrom.Value, comparison) &&
                       Binary64Equal(leftTo.Value, rightTo.Value, comparison) &&
                       Binary64Equal(leftStep.Value, rightStep.Value, comparison);
            }
            case GameEventScriptBytecodeTypeKind.Message:
                return expected.Message is not null && actual.Message is not null && RuntimeMessagesEqual(expected.Message, actual.Message, comparison);
            default: return expected.Equals(actual);
        }
    }

    private static bool RuntimeMessagesEqual(GameEventScriptMessage expected, GameEventScriptMessage actual, ConformanceComparisonOptions comparison)
    {
        if (!string.Equals(expected.Name, actual.Name, StringComparison.Ordinal) || expected.Tags.Count != actual.Tags.Count || expected.Arguments.Count != actual.Arguments.Count) return false;
        for (var index = 0; index < expected.Tags.Count; index++) if (!string.Equals(expected.Tags[index], actual.Tags[index], StringComparison.Ordinal)) return false;
        for (var index = 0; index < expected.Arguments.Count; index++)
        {
            if (!string.Equals(expected.Arguments.NameAt(index), actual.Arguments.NameAt(index), StringComparison.Ordinal)) return false;
            var left = expected.Arguments.ValueAt(index);
            var right = actual.Arguments.ValueAt(index);
            if (!ValuesEqual(in left, in right, comparison)) return false;
        }
        return true;
    }

    internal static string Describe(GameEventScriptMessage message) => message.ToString();

    private static GameEventScriptBytecodeInstructionUnit ParseUnit(string? unit)
        => unit is null ? GameEventScriptBytecodeInstructionUnit.UnitNone : GameEventScriptBytecodeInstructionUnits.ParseTypeName(unit.TrimStart(':')) ?? GameEventScriptBytecodeInstructionUnit.UnitInvalid;

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
