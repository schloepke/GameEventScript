using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

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
        var args = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);

        if (TryGetProperty(element, "args", out var argsElement))
        {
            args = DecodeArguments(argsElement);
        }

        var tags = TryGetProperty(element, "tags", out var tagsElement)
            ? DecodeMessageTags(tagsElement)
            : [];

        return GameEventScriptMessage.Create(name, args, tags);
    }

    public static Dictionary<string, GameEventScriptValue> DecodeArguments(JsonElement element)
    {
        RequireObject(element, "message args");
        var args = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            args[property.Name] = DecodeValue(property.Value);
        }

        return args;
    }

    public static GameEventScriptValue DecodeValue(JsonElement element)
    {
        RequireObject(element, "value");
        var type = RequireCanonicalTypeName(element, "type", "value type");
        switch (type)
        {
            case ":nothing":
                return GameEventScriptNothingValue.Instance;
            case ":text":
                return GameEventScriptValueFactory.GesText(RequireString(element, "value", "text value"));
            case ":tag":
                return GameEventScriptValueFactory.GesTag(RequireString(element, "value", "tag value"));
            case ":boolean":
                return GameEventScriptValueFactory.GesBoolean(RequireBoolean(element, "value", "boolean value"));
            case ":integer":
                return GameEventScriptValueFactory.GesInteger(RequireInt64(element, "value", "integer value"));
            case ":float":
                return DecodeFloatValue(element);
            case ":percentage":
                return GameEventScriptValueFactory.GesPercentage(RequireFloat(element, "value", "percentage ratio"));
            case ":vector":
                return GameEventScriptValueFactory.GesVector(
                    RequireFloat(element, "x", "vector x component"),
                    RequireFloat(element, "y", "vector y component"),
                    RequireFloat(element, "z", "vector z component"),
                    DecodeOptionalFloatUnit(element));
            case ":point":
                return GameEventScriptValueFactory.GesPoint(
                    RequireFloat(element, "x", "point x component"),
                    RequireFloat(element, "y", "point y component"),
                    RequireFloat(element, "z", "point z component"),
                    DecodeOptionalFloatUnit(element));
            case ":optional":
                return DecodeOptionalValue(element);
            case ":list":
                return GameEventScriptValueFactory.GesList(RequireArray(element, "items", "list items").EnumerateArray().Select(DecodeValue));
            case ":dictionary":
                return GameEventScriptValueFactory.GesDictionary(DecodeEntries(element));
            case ":set":
                return GameEventScriptValueFactory.GesSet(RequireArray(element, "items", "set items").EnumerateArray().Select(DecodeValue));
            case ":dice":
                return GameEventScriptValueFactory.GesDice(GameEventScriptDiceValue.Create(RequireArray(element, "rolls", "dice rolls").EnumerateArray().Select(ReadInt32)));
            case ":range":
                return GameEventScriptValueFactory.GesRange(
                    RequireInt64(element, "from", "range start"),
                    RequireInt64(element, "to", "range end"),
                    TryGetProperty(element, "step", out var stepElement) ? ReadInt64(stepElement, "range step") : 1L);
            case ":message":
                return GameEventScriptValueFactory.GesMessage(DecodeMessage(RequireObjectProperty(element, "message", "message value")));
            default:
                return GameEventScriptValueFactory.GesCustomType(type[1..], DecodeEntries(element));
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

    public static string ToCanonicalJson(GameEventScriptValue value)
        => ToValueJson(value).ToJsonString(CompactJsonOptions);

    public static string ToPrettyJson(GameEventScriptValue value)
        => ToValueJson(value).ToJsonString(PrettyJsonOptions);

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

    public static JsonObject ToValueJson(GameEventScriptValue value)
    {
        if (value.TryGetCustomTypeName(out var customTypeName))
        {
            return new JsonObject
            {
                ["type"] = ToCanonicalTypeName(customTypeName),
                ["entries"] = ToEntriesJson(value.AsDictionary())
            };
        }

        return value.Kind switch
        {
            GameEventScriptValueKind.Nothing => new JsonObject { ["type"] = ":nothing" },
            GameEventScriptValueKind.Text => new JsonObject { ["type"] = ":text", ["value"] = value.AsText() },
            GameEventScriptValueKind.Tag => new JsonObject { ["type"] = ":tag", ["value"] = value.AsText() },
            GameEventScriptValueKind.Boolean => new JsonObject { ["type"] = ":boolean", ["value"] = value.AsBoolean() },
            GameEventScriptValueKind.Integer => new JsonObject { ["type"] = ":integer", ["value"] = value.AsInteger().ToString(CultureInfo.InvariantCulture) },
            GameEventScriptValueKind.Float => ToFloatJson((GameEventScriptFloatValue)value),
            GameEventScriptValueKind.Percentage => new JsonObject { ["type"] = ":percentage", ["value"] = FormatFloat(value.AsNumber()) },
            GameEventScriptValueKind.Vector => ToVectorJson((GameEventScriptVectorValue)value),
            GameEventScriptValueKind.Point => ToPointJson((GameEventScriptPointValue)value),
            GameEventScriptValueKind.Optional => ToOptionalJson(value),
            GameEventScriptValueKind.List => new JsonObject { ["type"] = ":list", ["items"] = ToValueArrayJson(value.AsList()) },
            GameEventScriptValueKind.Dictionary => new JsonObject { ["type"] = ":dictionary", ["entries"] = ToEntriesJson(value.AsDictionary()) },
            GameEventScriptValueKind.Set => new JsonObject { ["type"] = ":set", ["items"] = ToValueArrayJson(value.AsSet().OrderBy(item => item, GameEventScriptValue.StableComparer)) },
            GameEventScriptValueKind.Dice => new JsonObject { ["type"] = ":dice", ["rolls"] = ToIntegerArrayJson(value.AsDice().Rolls) },
            GameEventScriptValueKind.Range => ToRangeJson(value),
            GameEventScriptValueKind.Message => new JsonObject { ["type"] = ":message", ["message"] = ToMessageJson(GetInternalProperty<GameEventScriptMessage>(value, "Value")) },
            GameEventScriptValueKind.Handler => throw new NotSupportedException("Handler values are not part of the conformance JSON value wire format."),
            GameEventScriptValueKind.Sequence => throw new NotSupportedException("Sequence values are not part of the conformance JSON value wire format."),
            _ => throw new NotSupportedException($"Unsupported GameEventScript value type '{value.Kind}'.")
        };
    }

    private static GameEventScriptValue DecodeFloatValue(JsonElement element)
    {
        var value = RequireString(element, "value", "float value");
        var unit = DecodeOptionalFloatUnit(element);

        return value switch
        {
            "NaN" => GameEventScriptValueFactory.GesFloatNaN(),
            "Infinity" => GameEventScriptValueFactory.GesFloatInfinity(),
            "-Infinity" => GameEventScriptValueFactory.GesFloatNegativeInfinity(),
            _ => GameEventScriptValueFactory.GesFloat(double.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture), unit)
        };
    }

    private static GameEventScriptFloatUnit? DecodeOptionalFloatUnit(JsonElement element)
    {
        if (TryGetProperty(element, "unit", out var unitElement))
        {
            if (unitElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("Invalid float unit.");
            }

            var unitName = unitElement.GetString()!;
            if (!unitName.StartsWith(":", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid float unit '{unitName}'.");
            }

            if (!GameEventScriptFloatUnits.TryParseTypeName(unitName[1..], out var parsedUnit))
            {
                throw new InvalidOperationException($"Invalid float unit '{unitName}'.");
            }

            return parsedUnit;
        }

        return null;
    }

    private static GameEventScriptValue DecodeOptionalValue(JsonElement element)
    {
        var hasValue = RequireBoolean(element, "hasValue", "optional hasValue");
        if (!hasValue)
        {
            return GameEventScriptValueFactory.GesOptionalNone();
        }

        return GameEventScriptValueFactory.GesOptionalSome(DecodeValue(RequireObjectProperty(element, "value", "optional value")));
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

    private static IReadOnlyDictionary<string, GameEventScriptValue> DecodeEntries(JsonElement element)
    {
        var entriesElement = RequireObjectProperty(element, "entries", "dictionary entries");
        var entries = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
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

    private static JsonArray ToValueArrayJson(IEnumerable<GameEventScriptValue> values)
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

    private static JsonObject ToEntriesJson(IReadOnlyDictionary<string, GameEventScriptValue> entries)
    {
        var node = new JsonObject();
        foreach (var pair in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            node[pair.Key] = ToValueJson(pair.Value);
        }

        return node;
    }

    private static JsonObject ToOptionalJson(GameEventScriptValue value)
    {
        var optional = value.AsOptional();
        var node = new JsonObject
        {
            ["type"] = ":optional",
            ["hasValue"] = optional.HasValue
        };

        if (optional.HasValue)
        {
            node["value"] = ToValueJson(optional.Value);
        }

        return node;
    }

    private static JsonObject ToRangeJson(GameEventScriptValue value)
        => new()
        {
            ["type"] = ":range",
            ["from"] = GetInternalProperty<long>(value, "From").ToString(CultureInfo.InvariantCulture),
            ["to"] = GetInternalProperty<long>(value, "To").ToString(CultureInfo.InvariantCulture),
            ["step"] = GetInternalProperty<long>(value, "Step").ToString(CultureInfo.InvariantCulture)
        };

    private static JsonObject ToFloatJson(GameEventScriptFloatValue value)
    {
        var node = new JsonObject
        {
            ["type"] = ":float",
            ["value"] = FormatFloat(value)
        };

        if (value.Unit.HasValue)
        {
            node["unit"] = ToCanonicalTypeName(value.Unit.Value.ToTypeName());
        }

        return node;
    }

    private static JsonObject ToVectorJson(GameEventScriptVectorValue value)
    {
        var node = new JsonObject
        {
            ["type"] = ":vector",
            ["x"] = FormatFloat(value.X),
            ["y"] = FormatFloat(value.Y),
            ["z"] = FormatFloat(value.Z)
        };

        if (value.Unit.HasValue)
        {
            node["unit"] = ToCanonicalTypeName(value.Unit.Value.ToTypeName());
        }

        return node;
    }

    private static JsonObject ToPointJson(GameEventScriptPointValue value)
    {
        var node = new JsonObject
        {
            ["type"] = ":point",
            ["x"] = FormatFloat(value.X),
            ["y"] = FormatFloat(value.Y),
            ["z"] = FormatFloat(value.Z)
        };

        if (value.Unit.HasValue)
        {
            node["unit"] = ToCanonicalTypeName(value.Unit.Value.ToTypeName());
        }

        return node;
    }

    private static string ToCanonicalTypeName(string typeName)
        => typeName.StartsWith(":", StringComparison.Ordinal) ? typeName : ":" + typeName;

    private static string FormatFloat(GameEventScriptValue value)
    {
        if (value.IsNaN())
        {
            return "NaN";
        }

        if (value.IsInfinity())
        {
            return value.IsNegativeInfinity() ? "-Infinity" : "Infinity";
        }

        return FormatFloat(value.AsNumber());
    }

    private static string FormatFloat(double value)
        => value == 0d ? "0" : value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static T GetInternalProperty<T>(GameEventScriptValue value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.GetValue(value) is T typed)
        {
            return typed;
        }

        throw new NotSupportedException($"Cannot serialize {value.Kind} value because property '{propertyName}' is unavailable.");
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
