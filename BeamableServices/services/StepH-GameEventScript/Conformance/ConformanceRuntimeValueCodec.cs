#pragma warning disable CS1591

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
            case ":nothing": return GesValue.GesNothing();
            case ":text": return GesValue.GesText(value.Value ?? string.Empty);
            case ":tag": return GesValue.GesTag(value.Value ?? string.Empty);
            case ":boolean": return GesValue.GesBoolean(string.Equals(value.Value, "true", StringComparison.Ordinal));
            case ":integer": return GesValue.GesInteger(long.Parse(value.Value!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), unit);
            case ":float": return GesValue.GesFloat(ParseBinary64(value.Value!), unit);
            case ":percentage": return GesValue.GesPercentage(ParseBinary64(value.Value!));
            case ":vector": return GesValue.GesVector(ParseBinary64(value.X!), ParseBinary64(value.Y!), ParseBinary64(value.Z!), unit);
            case ":point": return GesValue.GesPoint(ParseBinary64(value.X!), ParseBinary64(value.Y!), ParseBinary64(value.Z!), unit);
            case ":list":
            {
                var items = new GesValue[value.Items.Count];
                for (var index = 0; index < items.Length; index++) items[index] = DecodeValue(value.Items[index]);
                return GesValue.GesList(items);
            }
            case ":map": return DecodeMap(value.Entries);
            case ":dice":
            {
                var rolls = new int[value.Rolls.Count];
                for (var index = 0; index < rolls.Length; index++) rolls[index] = value.Rolls[index];
                return GesValue.GesDice(rolls);
            }
            case ":range": return GesValue.GesRange(ParseBinary64(value.From!), ParseBinary64(value.To!), ParseBinary64(value.Step!));
            case ":message": return GesValue.GesMessage(DecodeMessage(value.Message!));
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

    private static bool ValuesEqual(in GesValue expected, in GesValue actual, ConformanceComparisonOptions comparison)
    {
        if (expected.ValueKind != actual.ValueKind) return false;
        switch (expected.ValueKind)
        {
            case GameEventScriptBytecodeTypeKind.Nothing: return actual.IsNothing;
            case GameEventScriptBytecodeTypeKind.Boolean: return expected.AsBoolean() == actual.AsBoolean();
            case GameEventScriptBytecodeTypeKind.Integer: return expected.ValueUnit == actual.ValueUnit && expected.AsInteger() == actual.AsInteger();
            case GameEventScriptBytecodeTypeKind.Float:
            case GameEventScriptBytecodeTypeKind.Percentage:
                return expected.ValueUnit == actual.ValueUnit && Binary64Equal(expected.AsNumber(), actual.AsNumber(), comparison);
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
                if (!string.Equals(expected.CustomTypeName, actual.CustomTypeName, StringComparison.Ordinal)) return false;
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
                var leftInteger = expected.IntegerRange;
                var rightInteger = actual.IntegerRange;
                if (leftInteger is not null || rightInteger is not null)
                    return leftInteger is not null && rightInteger is not null && leftInteger.From == rightInteger.From && leftInteger.To == rightInteger.To && leftInteger.Step == rightInteger.Step;
                var left = expected.FloatRange;
                var right = actual.FloatRange;
                return left is not null && right is not null && Binary64Equal(left.From, right.From, comparison) && Binary64Equal(left.To, right.To, comparison) && Binary64Equal(left.Step, right.Step, comparison);
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
