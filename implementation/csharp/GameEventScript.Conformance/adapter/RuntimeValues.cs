// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

internal static class RuntimeValues
{
    internal static GameEventScriptMessage DecodeMessage(JsonElement message)
    {
        Fields(message, "name", "args", "tags");
        var name = message.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Message name required.");
        var arguments = new List<GameEventScriptMessageArgument>();
        foreach (var arg in message.GetProperty("args").EnumerateArray())
        {
            Fields(arg, "name", "value");
            arguments.Add(new GameEventScriptMessageArgument(arg.GetProperty("name").GetString() ?? throw new ArgumentException("Argument name required."), Decode(arg.GetProperty("value"))));
        }
        var tags = message.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString() ?? throw new ArgumentException("Tag required.")).ToArray();
        return GameEventScriptMessage.Create(name, arguments.ToArray(), tags);
    }

    internal static void Fields(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != fields.Length || value.EnumerateObject().Any(p => !fields.Contains(p.Name)))
            throw new ArgumentException("Invalid runtime transport fields.");
    }

    internal static GesValue Decode(JsonElement value)
    {
        var type = value.GetProperty("type").GetString();
        if (type is ":Vector" or ":Point")
        {
            if (value.TryGetProperty("unit", out _)) Fields(value, "type", "x", "y", "z", "unit");
            else Fields(value, "type", "x", "y", "z");
            var x = ParseNumber(value.GetProperty("x").GetString()!);
            var y = ParseNumber(value.GetProperty("y").GetString()!);
            var z = ParseNumber(value.GetProperty("z").GetString()!);
            return type == ":Vector" ? GesValue.GesVector(x, y, z, ReadUnit(value)) : GesValue.GesPoint(x, y, z, ReadUnit(value));
        }
        if (type is ":Range.int64" or ":Range.binary64")
        {
            Fields(value, "type", "from", "to", "step");
            var from = value.GetProperty("from").GetString()!;
            var to = value.GetProperty("to").GetString()!;
            var step = value.GetProperty("step").GetString()!;
            return type == ":Range.int64"
                ? GesValue.GesRange(long.Parse(from, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), long.Parse(to, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
                    long.Parse(step, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture))
                : GesValue.GesRange(ParseNumber(from), ParseNumber(to), ParseNumber(step));
        }
        if (type == ":Message")
        {
            Fields(value, "type", "message");
            return GesValue.GesMessage(DecodeMessage(value.GetProperty("message")));
        }
        if (type != ":Map" && type is not null && Regex.IsMatch(type, @"\A:[A-Z][A-Za-z0-9]*\z") && value.TryGetProperty("entries", out var recordEntries))
        {
            Fields(value, "type", "entries");
            foreach (var entry in recordEntries.EnumerateArray()) Fields(entry, "key", "value");
            return GesValue.GesRecord(type[1..], recordEntries.EnumerateArray().Select(item => item.GetProperty("key").GetString() ?? throw new ArgumentException("Record key required.")).ToArray(),
                recordEntries.EnumerateArray().Select(item => Decode(item.GetProperty("value"))).ToArray());
        }
        if (type == ":Nothing") Fields(value, "type");
        else if (type is ":Quantity.int64" or ":Quantity.binary64") Fields(value, "type", "value", "unit");
        else Fields(value, "type", type == ":List" ? "items" : type == ":Map" ? "entries" : type == ":Dice" ? "rolls" : "value");
        if (type == ":Map")
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in value.GetProperty("entries").EnumerateArray())
            {
                Fields(entry, "key", "value");
                if (!keys.Add(entry.GetProperty("key").GetString() ?? throw new ArgumentException("Map key required."))) throw new ArgumentException("Duplicate map key.");
            }
        }
        var unit = type is ":Quantity.int64" or ":Quantity.binary64" ? ReadUnit(value) : GameEventScriptBytecodeInstructionUnit.UnitNone;
        return type switch
        {
            ":Nothing" => GesValue.GesNothing(),
            ":Text" => GesValue.GesText(value.GetProperty("value").GetString() ?? throw new ArgumentException("Text value required.")),
            ":Boolean" => value.GetProperty("value").GetString() switch
            {
                "true" => GesValue.GesBoolean(true),
                "false" => GesValue.GesBoolean(false),
                _ => throw new ArgumentException("Invalid Boolean transport.")
            },
            ":Number.int64" or ":Quantity.int64" => GesValue.GesInteger(long.Parse(value.GetProperty("value").GetString()!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), unit),
            ":Number.binary64" or ":Quantity.binary64" => GesValue.GesFloat(ParseNumber(value.GetProperty("value").GetString()!), unit),
            ":Percentage" => GesValue.GesPercentage(ParseNumber(value.GetProperty("value").GetString()!)),
            ":List" => GesValue.GesList(value.GetProperty("items").EnumerateArray().Select(Decode).ToArray()),
            ":Tag" => GesValue.GesTag(value.GetProperty("value").GetString() ?? throw new ArgumentException("Tag value required.")),
            ":Dice" => GesValue.GesDice(value.GetProperty("rolls").EnumerateArray().Select(roll => roll.GetInt32() > 0 ? roll.GetInt32() : throw new ArgumentException("Positive roll required.")).ToArray()),
            ":Map" => GesValue.GesMap(value.GetProperty("entries").EnumerateArray().Select(item => item.GetProperty("key").GetString()!).ToArray(),
                value.GetProperty("entries").EnumerateArray().Select(item => Decode(item.GetProperty("value"))).ToArray()),
            _ => throw new ArgumentException("Unsupported runtime value transport.")
        };
    }

    internal static object EncodeMessage(GameEventScriptMessage message) => new
    {
        name = message.Name,
        tags = message.Tags.ToArray(),
        args = Enumerable.Range(0, message.Arguments.Count).Select(index => new { name = message.Arguments.NameAt(index), value = Encode(message.Arguments.ValueAt(index)) }).ToArray()
    };

    internal static object Encode(GesValue value)
    {

        switch (value.ValueKind)
        {
            case GameEventScriptBytecodeTypeKind.Nothing: return new { type = ":Nothing" };
            case GameEventScriptBytecodeTypeKind.Boolean: return new { type = ":Boolean", value = value.AsBoolean() ? "true" : "false" };
            case GameEventScriptBytecodeTypeKind.Text: return new { type = ":Text", value = value.AsText() };
            case GameEventScriptBytecodeTypeKind.Tag: return new { type = ":Tag", value = value.AsText() };
            case GameEventScriptBytecodeTypeKind.Dice: return new { type = ":Dice", rolls = value.AsDice().ToArray() };
            case GameEventScriptBytecodeTypeKind.Integer: return Numeric(value, "int64", value.AsInteger().ToString(CultureInfo.InvariantCulture));
            case GameEventScriptBytecodeTypeKind.Float: return Numeric(value, "binary64", value.AsNumber().ToString("R", CultureInfo.InvariantCulture));
            case GameEventScriptBytecodeTypeKind.Percentage: return new { type = ":Percentage", value = value.AsNumber().ToString("R", CultureInfo.InvariantCulture) };
            case GameEventScriptBytecodeTypeKind.List: return new { type = ":List", items = Enumerable.Range(0, value.AsList().Length).Select(index => Encode(value.AsList()[index])).ToArray() };
            case GameEventScriptBytecodeTypeKind.Vector:
            case GameEventScriptBytecodeTypeKind.Point:
                var spatial = new Dictionary<string, object>
                {
                    ["type"] = value.ValueKind == GameEventScriptBytecodeTypeKind.Vector ? ":Vector" : ":Point",
                    ["x"] = value.X.ToString("R", CultureInfo.InvariantCulture),
                    ["y"] = value.Y.ToString("R", CultureInfo.InvariantCulture),
                    ["z"] = value.Z.ToString("R", CultureInfo.InvariantCulture)
                };
                if (value.ValueUnit != GameEventScriptBytecodeInstructionUnit.UnitNone) spatial["unit"] = ":" + value.ValueUnit.ToTypeName();
                return spatial;
            case GameEventScriptBytecodeTypeKind.Range:
                if (value.IntegerRange is { } integerRange)
                    return new
                    {
                        type = ":Range.int64",
                        from = integerRange.From.ToString(CultureInfo.InvariantCulture),
                        to = integerRange.To.ToString(CultureInfo.InvariantCulture),
                        step = integerRange.Step.ToString(CultureInfo.InvariantCulture)
                    };
                var range = value.FloatRange!;
                return new
                {
                    type = ":Range.binary64",
                    from = range.From.ToString("R", CultureInfo.InvariantCulture),
                    to = range.To.ToString("R", CultureInfo.InvariantCulture),
                    step = range.Step.ToString("R", CultureInfo.InvariantCulture)
                };
            case GameEventScriptBytecodeTypeKind.Message: return new { type = ":Message", message = EncodeMessage(value.Message!) };
            case GameEventScriptBytecodeTypeKind.Custom:
            case GameEventScriptBytecodeTypeKind.Map:
                var map = value.AsMap()!;
                return new
                {
                    type = value.ValueKind == GameEventScriptBytecodeTypeKind.Map ? ":Map" : ":" + value.CustomTypeName,
                    entries = Enumerable.Range(0, map.Length).Select(index => new { key = map.KeyAt(index), value = Encode(map.ValueAt(index)) }).ToArray()
                };
            default: throw new ArgumentException("Unsupported observed runtime value kind: " + value.ValueKind);
        }
    }
    private static GameEventScriptBytecodeInstructionUnit ReadUnit(JsonElement value) => !value.TryGetProperty("unit", out var unit)
        ? GameEventScriptBytecodeInstructionUnit.UnitNone
        : unit.GetString() switch
        {
            ":meter" => GameEventScriptBytecodeInstructionUnit.UnitMeter,
            ":second" => GameEventScriptBytecodeInstructionUnit.UnitSecond,
            ":degree" => GameEventScriptBytecodeInstructionUnit.UnitDegree,
            _ => throw new ArgumentException("Invalid numeric unit.")
        };

    internal static double ParseNumber(string text)
    {
        if (text == "NaN") return double.NaN;
        if (text == "Infinity") return double.PositiveInfinity;
        if (text == "-Infinity") return double.NegativeInfinity;
        if (!Regex.IsMatch(text, @"\A[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?\z", RegexOptions.CultureInvariant)) throw new ArgumentException("Invalid Binary64 text.");
        var result = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!double.IsFinite(result)) throw new ArgumentException("Binary64 decimal overflow.");
        return result;
    }

    private static object Numeric(GesValue value, string storage, string text)
    {
        var result = new Dictionary<string, object> { ["type"] = (value.ValueUnit == GameEventScriptBytecodeInstructionUnit.UnitNone ? ":Number." : ":Quantity.") + storage, ["value"] = text };
        if (value.ValueUnit != GameEventScriptBytecodeInstructionUnit.UnitNone) result["unit"] = ":" + value.ValueUnit.ToTypeName();
        return result;
    }

}
