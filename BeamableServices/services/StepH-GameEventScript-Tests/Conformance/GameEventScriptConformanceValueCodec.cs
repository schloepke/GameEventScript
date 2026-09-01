using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.Conformance;

internal static class GameEventScriptConformanceValueCodec
{
    public const int DefaultMaxFloatUlps = 4096;

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        WriteIndented = true
    };

    public static GameEventScriptMessage DecodeMessage(JsonElement element)
    {
        RequireObject(element, "message");
        var name = RequireString(element, "name", "message name");
        GameEventScriptMessageArgument[] args = [];

        if (TryGetProperty(element, "args", out var argsElement))
        {
            args = DecodeArguments(argsElement);
        }

        var tags = TryGetProperty(element, "tags", out var tagsElement)
            ? DecodeMessageTags(tagsElement)
            : [];

        return GameEventScriptMessage.Create(name, args, tags);
    }

    public static GameEventScriptMessageArgument[] DecodeArguments(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("message args must be an ordered JSON array.");
        }

        var args = new GameEventScriptMessageArgument[element.GetArrayLength()];
        var index = 0;
        foreach (var entry in element.EnumerateArray())
        {
            RequireObject(entry, "message argument");
            var name = RequireString(entry, "name", "message argument name");
            var value = DecodeValue(RequireObjectProperty(entry, "value", "message argument value"));
            args[index++] = new GameEventScriptMessageArgument(name, value);
        }
        return args;
    }

    public static GesValue DecodeValue(JsonElement element)
    {
        RequireObject(element, "value");
        var type = RequireCanonicalTypeName(element, "type", "value type");
        switch (type)
        {
            case ":nothing":
                return GesValue.GesNothing();
            case ":text":
                return GesValue.GesText(RequireString(element, "value", "text value"));
            case ":tag":
                return GesValue.GesTag(RequireString(element, "value", "tag value"));
            case ":boolean":
                return GesValue.GesBoolean(RequireBoolean(element, "value", "boolean value"));
            case ":integer":
                return GesValue.GesInteger(
                    RequireInt64(element, "value", "integer value"),
                    DecodeOptionalNumericUnit(element) ?? GameEventScriptBytecodeInstructionUnit.UnitNone);
            case ":float":
                return DecodeFloatValue(element);
            case ":percentage":
                return GesValue.GesPercentage(RequireFloat(element, "value", "percentage ratio"));
            case ":vector":
                return GesValue.GesVector(
                    RequireFloat(element, "x", "vector x component"),
                    RequireFloat(element, "y", "vector y component"),
                    RequireFloat(element, "z", "vector z component"),
                    DecodeOptionalNumericUnit(element) ?? GameEventScriptBytecodeInstructionUnit.UnitNone);
            case ":point":
                return GesValue.GesPoint(
                    RequireFloat(element, "x", "point x component"),
                    RequireFloat(element, "y", "point y component"),
                    RequireFloat(element, "z", "point z component"),
                    DecodeOptionalNumericUnit(element) ?? GameEventScriptBytecodeInstructionUnit.UnitNone);
            case ":list":
                return DecodeListValue(element);
            case ":map":
                return GesValue.GesMap(DecodeEntries(element));
            case ":dice":
                return DecodeDiceValue(element);
            case ":range":
                return GesValue.GesRange(
                    RequireFloat(element, "from", "range start"),
                    RequireFloat(element, "to", "range end"),
                    TryGetProperty(element, "step", out var stepElement) ? ReadFloat(stepElement, "range step") : 1d);
            case ":message":
                return GesValue.GesMessage(DecodeMessage(RequireObjectProperty(element, "message", "message value")));
            default:
                return GesValue.GesRecord(type[1..], DecodeEntries(element));
        }
    }

    public static string ToCanonicalJson(GameEventScriptMessage message)
        => ToMessageJson(message).ToJsonString(CompactJsonOptions);

    public static string ToPrettyJson(GameEventScriptMessage message)
        => ToMessageJson(message).ToJsonString(PrettyJsonOptions);

    public static string ToCanonicalJson(IEnumerable<GameEventScriptMessage> messages)
        => ToMessageArrayJson(messages).ToJsonString(CompactJsonOptions);

    public static string ToPrettyJson(IEnumerable<GameEventScriptMessage> messages)
        => ToMessageArrayJson(messages).ToJsonString(PrettyJsonOptions);

    public static string ToCanonicalJson(GesValue value)
        => ToValueJson(value).ToJsonString(CompactJsonOptions);

    public static bool ConformanceEquals(
        IReadOnlyList<GameEventScriptMessage> expected,
        IReadOnlyList<GameEventScriptMessage> actual,
        int maxFloatUlps)
    {
        if (expected.Count != actual.Count) return false;
        var tolerance = (ulong)Math.Max(0, maxFloatUlps);
        for (var index = 0; index < expected.Count; index++)
        {
            if (!ConformanceEquals(expected[index], actual[index], tolerance)) return false;
        }

        return true;
    }

    private static bool ConformanceEquals(GameEventScriptMessage expected, GameEventScriptMessage actual, ulong maxFloatUlps)
    {
        if (!string.Equals(expected.Name, actual.Name, StringComparison.Ordinal) ||
            expected.Tags.Count != actual.Tags.Count ||
            expected.Arguments.Count != actual.Arguments.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Tags.Count; index++)
        {
            if (!string.Equals(expected.Tags[index], actual.Tags[index], StringComparison.Ordinal)) return false;
        }

        for (var index = 0; index < expected.Arguments.Count; index++)
        {
            if (!string.Equals(expected.Arguments.NameAt(index), actual.Arguments.NameAt(index), StringComparison.Ordinal) ||
                !ConformanceEquals(expected.Arguments.ValueAt(index), actual.Arguments.ValueAt(index), maxFloatUlps))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ConformanceEquals(GesValue expected, GesValue actual, ulong maxFloatUlps)
    {
        if (expected.ValueKind != actual.ValueKind) return false;

        switch (expected.ValueKind)
        {
            case GameEventScriptBytecodeTypeKind.Nothing:
                return true;
            case GameEventScriptBytecodeTypeKind.Integer:
                return expected.ValueUnit == actual.ValueUnit && expected.AsInteger() == actual.AsInteger();
            case GameEventScriptBytecodeTypeKind.Float:
                return (!double.IsFinite(expected.AsNumber()) || expected.ValueUnit == actual.ValueUnit) &&
                       GameEventScriptNumber.EqualsWithinUlps(expected.AsNumber(), actual.AsNumber(), maxFloatUlps);
            case GameEventScriptBytecodeTypeKind.Percentage:
                return GameEventScriptNumber.EqualsWithinUlps(expected.AsNumber(), actual.AsNumber(), maxFloatUlps);
            case GameEventScriptBytecodeTypeKind.Boolean:
                return expected.AsBoolean() == actual.AsBoolean();
            case GameEventScriptBytecodeTypeKind.Text:
            case GameEventScriptBytecodeTypeKind.Tag:
                return string.Equals(expected.AsText(), actual.AsText(), StringComparison.Ordinal);
            case GameEventScriptBytecodeTypeKind.Vector:
            case GameEventScriptBytecodeTypeKind.Point:
                return expected.ValueUnit == actual.ValueUnit &&
                       GameEventScriptNumber.EqualsWithinUlps(expected.X, actual.X, maxFloatUlps) &&
                       GameEventScriptNumber.EqualsWithinUlps(expected.Y, actual.Y, maxFloatUlps) &&
                       GameEventScriptNumber.EqualsWithinUlps(expected.Z, actual.Z, maxFloatUlps);
            case GameEventScriptBytecodeTypeKind.List:
            {
                var expectedItems = expected.AsList();
                var actualItems = actual.AsList();
                if (expectedItems.Length != actualItems.Length) return false;
                for (var index = 0; index < expectedItems.Length; index++)
                {
                    if (!ConformanceEquals(expectedItems[index], actualItems[index], maxFloatUlps)) return false;
                }

                return true;
            }
            case GameEventScriptBytecodeTypeKind.Map:
            case GameEventScriptBytecodeTypeKind.Custom:
            {
                if (expected.ValueKind == GameEventScriptBytecodeTypeKind.Custom &&
                    !string.Equals(expected.CustomTypeName, actual.CustomTypeName, StringComparison.Ordinal)) return false;
                var expectedMap = expected.AsMap();
                var actualMap = actual.AsMap();
                if (expectedMap is null || actualMap is null || expectedMap.Length != actualMap.Length) return false;
                for (var index = 0; index < expectedMap.StorageLength; index++)
                {
                    var key = expectedMap.KeyAt(index);
                    if (actualMap.Get(key) is not { } actualValue ||
                        !ConformanceEquals(expectedMap.ValueAt(index), actualValue, maxFloatUlps)) return false;
                }

                return true;
            }
            case GameEventScriptBytecodeTypeKind.Dice:
                return expected.AsDice().SequenceEqual(actual.AsDice());
            case GameEventScriptBytecodeTypeKind.Range:
                var expectedFrom = expected.IntegerRange?.From ?? expected.FloatRange?.From;
                var expectedTo = expected.IntegerRange?.To ?? expected.FloatRange?.To;
                var expectedStep = expected.IntegerRange?.Step ?? expected.FloatRange?.Step;
                var actualFrom = actual.IntegerRange?.From ?? actual.FloatRange?.From;
                var actualTo = actual.IntegerRange?.To ?? actual.FloatRange?.To;
                var actualStep = actual.IntegerRange?.Step ?? actual.FloatRange?.Step;
                return expectedFrom is { } leftFrom && expectedTo is { } leftTo && expectedStep is { } leftStep &&
                       actualFrom is { } rightFrom && actualTo is { } rightTo && actualStep is { } rightStep &&
                       GameEventScriptNumber.EqualsWithinUlps(leftFrom, rightFrom, maxFloatUlps) &&
                       GameEventScriptNumber.EqualsWithinUlps(leftTo, rightTo, maxFloatUlps) &&
                       GameEventScriptNumber.EqualsWithinUlps(leftStep, rightStep, maxFloatUlps);
            case GameEventScriptBytecodeTypeKind.Message:
                return expected.Message is { } expectedMessage && actual.Message is { } actualMessage &&
                       ConformanceEquals(expectedMessage, actualMessage, maxFloatUlps);
            default:
                return expected.Equals(actual);
        }
    }

    public static JsonObject ToMessageJson(GameEventScriptMessage message)
    {
        var node = new JsonObject
        {
            ["name"] = message.Name
        };

        if (message.Tags.Count > 0)
        {
            var tags = new JsonArray();
            foreach (var tag in message.Tags)
            {
                tags.Add(tag);
            }

            node["tags"] = tags;
        }

        var args = new JsonArray();
        var arguments = message.Arguments;
        for (var index = 0; index < arguments.Count; index++)
        {
            args.Add(new JsonObject
            {
                ["name"] = arguments.NameAt(index),
                ["value"] = ToValueJson(arguments.ValueAt(index))
            });
        }

        node["args"] = args;
        return node;
    }

    public static JsonObject ToValueJson(GesValue value)
    {
        if (value.IsNothing)
        {
            return new JsonObject { ["type"] = ":nothing" };
        }

        if (value.CustomTypeName is { } customTypeName)
        {
            return new JsonObject
            {
                ["type"] = ToCanonicalTypeName(customTypeName),
                ["entries"] = ToEntriesJson(value.AsMap())
            };
        }

        switch (value.ValueKind)
        {
            case GameEventScriptBytecodeTypeKind.Nothing:
                return new JsonObject { ["type"] = ":nothing" };
            case GameEventScriptBytecodeTypeKind.Text:
                return new JsonObject { ["type"] = ":text", ["value"] = value.AsText() };
            case GameEventScriptBytecodeTypeKind.Tag:
                return new JsonObject { ["type"] = ":tag", ["value"] = value.AsText() };
            case GameEventScriptBytecodeTypeKind.Boolean:
                return new JsonObject { ["type"] = ":boolean", ["value"] = value.AsBoolean() };
            case GameEventScriptBytecodeTypeKind.Integer:
                return ToIntegerJson(value);
            case GameEventScriptBytecodeTypeKind.Float:
                return ToFloatJson(value);
            case GameEventScriptBytecodeTypeKind.Percentage:
                return new JsonObject { ["type"] = ":percentage", ["value"] = FormatFloat(value.AsNumber()) };
            case GameEventScriptBytecodeTypeKind.Vector:
                return ToVectorJson(value, ":vector");
            case GameEventScriptBytecodeTypeKind.Point:
                return ToVectorJson(value, ":point");
            case GameEventScriptBytecodeTypeKind.List:
                return new JsonObject { ["type"] = ":list", ["items"] = ToValueArrayJson(value.AsList()) };
            case GameEventScriptBytecodeTypeKind.Map:
                return new JsonObject { ["type"] = ":map", ["entries"] = ToEntriesJson(value.AsMap()) };
            case GameEventScriptBytecodeTypeKind.Dice:
                return new JsonObject { ["type"] = ":dice", ["rolls"] = ToIntegerArrayJson(value.AsDice()) };
            case GameEventScriptBytecodeTypeKind.Range:
                return ToRangeJson(value);
            case GameEventScriptBytecodeTypeKind.Message when value.Message is { } message:
                return new JsonObject { ["type"] = ":message", ["message"] = ToMessageJson(message) };
            case GameEventScriptBytecodeTypeKind.Handler:
                throw new NotSupportedException("Handler values are not part of the conformance JSON value wire format.");
            case GameEventScriptBytecodeTypeKind.Series:
                throw new NotSupportedException("Series values are not part of the conformance JSON value wire format.");
            default:
                throw new NotSupportedException($"Unsupported GameEventScript value type '{value.ValueKind}'.");
        }
    }

    private static GesValue DecodeFloatValue(JsonElement element)
    {
        var value = RequireString(element, "value", "float value");
        var unit = DecodeOptionalNumericUnit(element) ?? GameEventScriptBytecodeInstructionUnit.UnitNone;

        return value switch
        {
            "NaN" => GesValue.GesNothing(),
            "Infinity" => GesValue.GesFloat(double.PositiveInfinity, unit),
            "-Infinity" => GesValue.GesFloat(double.NegativeInfinity, unit),
            _ => GesValue.GesFloat(double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture), unit)
        };
    }

    private static GesValue DecodeListValue(JsonElement element)
    {
        var items = RequireArray(element, "items", "list items");
        var values = new GesValue[items.GetArrayLength()];
        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            values[index++] = DecodeValue(item);
        }

        return GesValue.GesList(values);
    }

    private static GesValue DecodeDiceValue(JsonElement element)
    {
        var rolls = RequireArray(element, "rolls", "dice rolls");
        var values = new int[rolls.GetArrayLength()];
        var index = 0;
        foreach (var roll in rolls.EnumerateArray())
        {
            values[index++] = ReadInt32(roll);
        }

        return GesValue.GesDice(values);
    }

    private static GameEventScriptBytecodeInstructionUnit? DecodeOptionalNumericUnit(JsonElement element)
    {
        if (TryGetProperty(element, "unit", out var unitElement))
        {
            if (unitElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("Invalid numeric unit.");
            }

            var unitName = unitElement.GetString()!;
            if (!unitName.StartsWith(":", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid numeric unit '{unitName}'.");
            }

            var parsedUnit = GameEventScriptBytecodeInstructionUnits.ParseTypeName(unitName[1..]);
            if (parsedUnit is null)
            {
                throw new InvalidOperationException($"Invalid numeric unit '{unitName}'.");
            }

            return parsedUnit.Value;
        }

        return null;
    }

    private static IReadOnlyList<string> DecodeMessageTags(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Invalid message tags; expected JSON array.");
        }

        var tags = new List<string>();
        foreach (var tag in element.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("Invalid message tag; expected string.");
            }

            tags.Add(tag.GetString()!);
        }

        return tags;
    }

    private static IReadOnlyDictionary<string, GesValue> DecodeEntries(JsonElement element)
    {
        var entriesElement = RequireObjectProperty(element, "entries", "dictionary entries");
        var entries = new Dictionary<string, GesValue>(StringComparer.Ordinal);
        foreach (var property in entriesElement.EnumerateObject())
        {
            entries[property.Name] = DecodeValue(property.Value);
        }

        return entries;
    }

    private static JsonArray ToMessageArrayJson(IEnumerable<GameEventScriptMessage> messages)
    {
        var array = new JsonArray();
        foreach (var message in messages)
        {
            array.Add(ToMessageJson(message));
        }

        return array;
    }

    private static JsonArray ToValueArrayJson(GesValueSlice values)
    {
        var array = new JsonArray();
        for (var index = 0; index < values.Length; index++)
        {
            array.Add(ToValueJson(values.ValueAt(index)));
        }

        return array;
    }

    private static JsonArray ToIntegerArrayJson(IEnumerable<int> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonObject ToEntriesJson(GesValueMap? entries)
    {
        var node = new JsonObject();
        if (entries is null)
        {
            return node;
        }

        for (var index = 0; index < entries.StorageLength; index++)
        {
            node[entries.KeyAt(index)] = ToValueJson(entries.ValueAt(index));
        }

        return node;
    }

    private static JsonObject ToRangeJson(GesValue value)
    {
        if (value.IntegerRange is { } integerRange)
        {
            return new JsonObject
            {
                ["type"] = ":range",
                ["from"] = integerRange.From.ToString(CultureInfo.InvariantCulture),
                ["to"] = integerRange.To.ToString(CultureInfo.InvariantCulture),
                ["step"] = integerRange.Step.ToString(CultureInfo.InvariantCulture)
            };
        }

        if (value.FloatRange is { } floatRange)
        {
            return new JsonObject
            {
                ["type"] = ":range",
                ["from"] = FormatFloat(floatRange.From),
                ["to"] = FormatFloat(floatRange.To),
                ["step"] = FormatFloat(floatRange.Step)
            };
        }

        return new JsonObject { ["type"] = ":nothing" };
    }

    private static JsonObject ToFloatJson(GesValue value)
    {
        var node = new JsonObject
        {
            ["type"] = ":float",
            ["value"] = FormatFloat(value.AsNumber())
        };

        if (value.ValueUnit.IsNumericUnit() && double.IsFinite(value.AsNumber()))
        {
            node["unit"] = ToCanonicalTypeName(value.ValueUnit.ToTypeName());
        }

        return node;
    }

    private static JsonObject ToIntegerJson(GesValue value)
    {
        var node = new JsonObject
        {
            ["type"] = ":integer",
            ["value"] = value.AsInteger().ToString(CultureInfo.InvariantCulture)
        };

        if (value.ValueUnit.IsNumericUnit())
        {
            node["unit"] = ToCanonicalTypeName(value.ValueUnit.ToTypeName());
        }

        return node;
    }

    private static JsonObject ToVectorJson(GesValue value, string type)
    {
        var node = new JsonObject
        {
            ["type"] = type,
            ["x"] = FormatFloat(value.X),
            ["y"] = FormatFloat(value.Y),
            ["z"] = FormatFloat(value.Z)
        };

        if (value.ValueUnit.IsNumericUnit())
        {
            node["unit"] = ToCanonicalTypeName(value.ValueUnit.ToTypeName());
        }

        return node;
    }

    private static string ToCanonicalTypeName(string typeName)
        => typeName.StartsWith(":", StringComparison.Ordinal) ? typeName : ":" + typeName;

    private static string FormatFloat(double value) => GameEventScriptNumber.FormatCanonicalFloat(value);

    private static JsonElement RequireObjectProperty(JsonElement element, string propertyName, string description)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            throw new InvalidOperationException($"Missing {description}.");
        }

        RequireObject(property, description);
        return property;
    }

    private static JsonElement RequireArray(JsonElement element, string propertyName, string description)
    {
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Missing or invalid {description}.");
        }

        return property;
    }

    private static string RequireString(JsonElement element, string propertyName, string description)
    {
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Missing or invalid {description}.");
        }

        return property.GetString()!;
    }

    private static string RequireCanonicalTypeName(JsonElement element, string propertyName, string description)
    {
        var typeName = RequireString(element, propertyName, description);
        if (!typeName.StartsWith(":", StringComparison.Ordinal) || typeName.Length == 1)
        {
            throw new InvalidOperationException($"Invalid {description}; expected canonical GameEventScript type name like ':integer'.");
        }

        return typeName;
    }

    private static bool RequireBoolean(JsonElement element, string propertyName, string description)
    {
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new InvalidOperationException($"Missing or invalid {description}.");
        }

        return property.GetBoolean();
    }

    private static long RequireInt64(JsonElement element, string propertyName, string description)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            throw new InvalidOperationException($"Missing {description}.");
        }

        return ReadInt64(property, description);
    }

    private static double RequireFloat(JsonElement element, string propertyName, string description)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            throw new InvalidOperationException($"Missing {description}.");
        }

        return ReadFloat(property, description);
    }

    private static int ReadInt32(JsonElement element)
    {
        var value = ReadInt64(element, "dice roll");
        if (value < int.MinValue || value > int.MaxValue)
        {
            throw new InvalidOperationException($"Dice roll '{value}' is outside the Int32 range.");
        }

        return (int)value;
    }

    private static long ReadInt64(JsonElement element, string description)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var textNumber) => textNumber,
            _ => throw new InvalidOperationException($"Invalid {description}; expected integer string or JSON integer.")
        };

    private static double ReadFloat(JsonElement element, string description)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var textNumber) => textNumber,
            _ => throw new InvalidOperationException($"Invalid {description}; expected double string or JSON number.")
        };

    private static void RequireObject(JsonElement element, string description)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Invalid {description}; expected JSON object.");
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
