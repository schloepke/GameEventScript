using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Conformance;

internal static class GameEventScriptConformanceValueCodec
{
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
        var args = new Dictionary<string, GameEventScriptBoxedValue>(StringComparer.Ordinal);

        if (TryGetProperty(element, "args", out var argsElement))
        {
            args = DecodeBoxedArguments(argsElement);
        }

        var tags = TryGetProperty(element, "tags", out var tagsElement)
            ? DecodeMessageTags(tagsElement)
            : [];

        return GameEventScriptMessage.Create(name, args, tags);
    }

    public static Dictionary<string, GameEventScriptBoxedValue> DecodeBoxedArguments(JsonElement element)
    {
        RequireObject(element, "message args");
        var args = new Dictionary<string, GameEventScriptBoxedValue>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            args[property.Name] = DecodeBoxedValue(property.Value);
        }

        return args;
    }

    public static GameEventScriptBoxedValue DecodeBoxedValue(JsonElement element)
    {
        RequireObject(element, "value");
        var type = RequireCanonicalTypeName(element, "type", "value type");
        switch (type)
        {
            case ":nothing":
                return GameEventScriptBoxedValue.Nothing();
            case ":text":
                return GameEventScriptBoxedValue.FromText(RequireString(element, "value", "text value"));
            case ":tag":
                return GameEventScriptBoxedValue.FromTag(RequireString(element, "value", "tag value"));
            case ":boolean":
                return GameEventScriptBoxedValue.FromBoolean(RequireBoolean(element, "value", "boolean value"));
            case ":integer":
                return GameEventScriptBoxedValue.FromInteger(
                    RequireInt64(element, "value", "integer value"),
                    DecodeOptionalNumericUnit(element) ?? GameEventScriptBytecodeInstructionUnit.UnitNone);
            case ":float":
                return DecodeBoxedFloatValue(element);
            case ":percentage":
                return GameEventScriptBoxedValue.FromPercentage(RequireFloat(element, "value", "percentage ratio"));
            case ":vector":
                return GameEventScriptBoxedValue.FromVector(
                    RequireFloat(element, "x", "vector x component"),
                    RequireFloat(element, "y", "vector y component"),
                    RequireFloat(element, "z", "vector z component"),
                    DecodeOptionalNumericUnit(element) ?? GameEventScriptBytecodeInstructionUnit.UnitNone);
            case ":point":
                return GameEventScriptBoxedValue.FromPoint(
                    RequireFloat(element, "x", "point x component"),
                    RequireFloat(element, "y", "point y component"),
                    RequireFloat(element, "z", "point z component"),
                    DecodeOptionalNumericUnit(element) ?? GameEventScriptBytecodeInstructionUnit.UnitNone);
            case ":list":
                return GameEventScriptBoxedValue.FromList(RequireArray(element, "items", "list items").EnumerateArray().Select(DecodeBoxedValue));
            case ":map":
                return GameEventScriptBoxedValue.FromMap(DecodeBoxedEntries(element));
            case ":dice":
                return GameEventScriptBoxedValue.FromDice(RequireArray(element, "rolls", "dice rolls").EnumerateArray().Select(ReadInt32));
            case ":range":
                return GameEventScriptBoxedValue.FromFloatRange(
                    RequireFloat(element, "from", "range start"),
                    RequireFloat(element, "to", "range end"),
                    TryGetProperty(element, "step", out var stepElement) ? ReadFloat(stepElement, "range step") : 1d);
            case ":message":
                return GameEventScriptBoxedValue.FromMessage(DecodeMessage(RequireObjectProperty(element, "message", "message value")));
            default:
                return GameEventScriptBoxedValue.FromRecord(type[1..], DecodeBoxedEntries(element));
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

    public static string ToCanonicalJson(GameEventScriptBoxedValue value)
        => ToValueJson(value).ToJsonString(CompactJsonOptions);

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

        var args = new JsonObject();
        foreach (var pair in message.Arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            args[pair.Key] = ToValueJson(pair.Value);
        }

        node["args"] = args;
        return node;
    }

    public static JsonObject ToValueJson(GameEventScriptBoxedValue value)
    {
        if (value.IsNothing)
        {
            return new JsonObject { ["type"] = ":nothing" };
        }

        if (value.TryGetCustomTypeName(out var customTypeName))
        {
            return new JsonObject
            {
                ["type"] = ToCanonicalTypeName(customTypeName),
                ["entries"] = ToEntriesJson(value.AsMap())
            };
        }

        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Nothing:
                return new JsonObject { ["type"] = ":nothing" };
            case GameEventScriptBytecodeTypeKind.Text:
                return new JsonObject { ["type"] = ":text", ["value"] = value.Text };
            case GameEventScriptBytecodeTypeKind.Tag:
                return new JsonObject { ["type"] = ":tag", ["value"] = value.Text };
            case GameEventScriptBytecodeTypeKind.Boolean:
                return new JsonObject { ["type"] = ":boolean", ["value"] = value.Boolean };
            case GameEventScriptBytecodeTypeKind.Integer:
                return ToIntegerJson(value);
            case GameEventScriptBytecodeTypeKind.Float:
                return ToFloatJson(value);
            case GameEventScriptBytecodeTypeKind.Percentage:
                return new JsonObject { ["type"] = ":percentage", ["value"] = FormatFloat(value.Number) };
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
                throw new NotSupportedException($"Unsupported GameEventScript value type '{value.Kind}'.");
        }
    }

    private static GameEventScriptBoxedValue DecodeBoxedFloatValue(JsonElement element)
    {
        var value = RequireString(element, "value", "float value");
        var unit = DecodeOptionalNumericUnit(element) ?? GameEventScriptBytecodeInstructionUnit.UnitNone;

        return value switch
        {
            "NaN" => GameEventScriptBoxedValue.Nothing(),
            "Infinity" => GameEventScriptBoxedValue.FromFloat(double.PositiveInfinity, unit),
            "-Infinity" => GameEventScriptBoxedValue.FromFloat(double.NegativeInfinity, unit),
            _ => GameEventScriptBoxedValue.FromFloat(double.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture), unit)
        };
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

            if (!GameEventScriptBytecodeInstructionUnits.TryParseTypeName(unitName[1..], out var parsedUnit))
            {
                throw new InvalidOperationException($"Invalid numeric unit '{unitName}'.");
            }

            return parsedUnit;
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

    private static IReadOnlyDictionary<string, GameEventScriptBoxedValue> DecodeBoxedEntries(JsonElement element)
    {
        var entriesElement = RequireObjectProperty(element, "entries", "dictionary entries");
        var entries = new Dictionary<string, GameEventScriptBoxedValue>(StringComparer.Ordinal);
        foreach (var property in entriesElement.EnumerateObject())
        {
            entries[property.Name] = DecodeBoxedValue(property.Value);
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

    private static JsonArray ToValueArrayJson(IEnumerable<GameEventScriptBoxedValue> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(ToValueJson(value));
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

    private static JsonObject ToEntriesJson(IReadOnlyDictionary<string, GameEventScriptBoxedValue> entries)
    {
        var node = new JsonObject();
        foreach (var pair in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            node[pair.Key] = ToValueJson(pair.Value);
        }

        return node;
    }

    private static JsonObject ToRangeJson(GameEventScriptBoxedValue value)
    {
        if (value.TryGetIntegerRange(out var from, out var to, out var step))
        {
            return new JsonObject
            {
                ["type"] = ":range",
                ["from"] = from.ToString(CultureInfo.InvariantCulture),
                ["to"] = to.ToString(CultureInfo.InvariantCulture),
                ["step"] = step.ToString(CultureInfo.InvariantCulture)
            };
        }

        if (value.TryGetFloatRange(out var fromFloat, out var toFloat, out var stepFloat))
        {
            return new JsonObject
            {
                ["type"] = ":range",
                ["from"] = FormatFloat(fromFloat),
                ["to"] = FormatFloat(toFloat),
                ["step"] = FormatFloat(stepFloat)
            };
        }

        return new JsonObject { ["type"] = ":nothing" };
    }

    private static JsonObject ToFloatJson(GameEventScriptBoxedValue value)
    {
        var node = new JsonObject
        {
            ["type"] = ":float",
            ["value"] = FormatFloat(value.Number)
        };

        if (value.Unit.IsNumericUnit() && double.IsFinite(value.Number))
        {
            node["unit"] = ToCanonicalTypeName(value.Unit.ToTypeName());
        }

        return node;
    }

    private static JsonObject ToIntegerJson(GameEventScriptBoxedValue value)
    {
        var node = new JsonObject
        {
            ["type"] = ":integer",
            ["value"] = value.Integer.ToString(CultureInfo.InvariantCulture)
        };

        if (value.Unit.IsNumericUnit())
        {
            node["unit"] = ToCanonicalTypeName(value.Unit.ToTypeName());
        }

        return node;
    }

    private static JsonObject ToVectorJson(GameEventScriptBoxedValue value, string type)
    {
        var node = new JsonObject
        {
            ["type"] = type,
            ["x"] = FormatFloat(value.X),
            ["y"] = FormatFloat(value.Y),
            ["z"] = FormatFloat(value.Z)
        };

        if (value.Unit.IsNumericUnit())
        {
            node["unit"] = ToCanonicalTypeName(value.Unit.ToTypeName());
        }

        return node;
    }

    private static string ToCanonicalTypeName(string typeName)
        => typeName.StartsWith(":", StringComparison.Ordinal) ? typeName : ":" + typeName;

    private static string FormatFloat(double value)
    {
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";
        if (double.IsNaN(value)) return "NaN";
        return value == 0d ? "0" : value.ToString("0.############################", CultureInfo.InvariantCulture);
    }

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
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var textNumber) => textNumber,
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
