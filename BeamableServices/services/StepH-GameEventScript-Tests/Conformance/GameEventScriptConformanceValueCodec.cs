using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using StepH.GameEventScript;
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

    public static GseMessage DecodeMessage(JsonElement element)
    {
        RequireObject(element, "message");
        var name = RequireString(element, "name", "message name");
        var args = new Dictionary<string, GseValue>(StringComparer.Ordinal);

        if (TryGetProperty(element, "args", out var argsElement))
        {
            args = DecodeArguments(argsElement);
        }

        return GseMessage.Message(name, args);
    }

    public static Dictionary<string, GseValue> DecodeArguments(JsonElement element)
    {
        RequireObject(element, "message args");
        var args = new Dictionary<string, GseValue>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            args[property.Name] = DecodeValue(property.Value);
        }

        return args;
    }

    public static GseValue DecodeValue(JsonElement element)
    {
        RequireObject(element, "value");
        var type = RequireCanonicalTypeName(element, "type", "value type");
        switch (type)
        {
            case ":nothing":
                return GseValue.Nothing;
            case ":text":
                return GseValueFactory.Text(RequireString(element, "value", "text value"));
            case ":tag":
                return GseValueFactory.Tag(RequireString(element, "value", "tag value"));
            case ":boolean":
                return GseValueFactory.Boolean(RequireBoolean(element, "value", "boolean value"));
            case ":integer":
                return GseValueFactory.Integer(RequireInt64(element, "value", "integer value"));
            case ":decimal":
                return DecodeDecimalValue(element);
            case ":percentage":
                return GseValueFactory.Percentage(RequireDecimal(element, "value", "percentage ratio"));
            case ":vector2":
                return GseValueFactory.Vector2(
                    RequireDecimal(element, "x", "vector2 x component"),
                    RequireDecimal(element, "y", "vector2 y component"),
                    DecodeOptionalDecimalUnit(element));
            case ":vector3":
                return GseValueFactory.Vector3(
                    RequireDecimal(element, "x", "vector3 x component"),
                    RequireDecimal(element, "y", "vector3 y component"),
                    RequireDecimal(element, "z", "vector3 z component"),
                    DecodeOptionalDecimalUnit(element));
            case ":optional":
                return DecodeOptionalValue(element);
            case ":list":
                return GseValueFactory.List(RequireArray(element, "items", "list items").EnumerateArray().Select(DecodeValue));
            case ":dictionary":
                return GseValueFactory.Dictionary(DecodeEntries(element));
            case ":set":
                return GseValueFactory.Set(RequireArray(element, "items", "set items").EnumerateArray().Select(DecodeValue));
            case ":dice":
                return GseValueFactory.Dice(GseDiceValue.GseDice(RequireArray(element, "rolls", "dice rolls").EnumerateArray().Select(ReadInt32)));
            case ":range":
                return GseValueFactory.Range(
                    RequireInt64(element, "from", "range start"),
                    RequireInt64(element, "to", "range end"),
                    TryGetProperty(element, "step", out var stepElement) ? ReadInt64(stepElement, "range step") : 1L);
            case ":message":
                return GseValueFactory.Message(DecodeMessage(RequireObjectProperty(element, "message", "message value")));
            default:
                return GseValueFactory.CustomType(type[1..], DecodeEntries(element));
        }
    }

    public static string ToCanonicalJson(GseMessage message)
        => ToMessageJson(message).ToJsonString(CompactJsonOptions);

    public static string ToPrettyJson(GseMessage message)
        => ToMessageJson(message).ToJsonString(PrettyJsonOptions);

    public static string ToCanonicalJson(IEnumerable<GseMessage> messages)
        => ToMessageArrayJson(messages).ToJsonString(CompactJsonOptions);

    public static string ToPrettyJson(IEnumerable<GseMessage> messages)
        => ToMessageArrayJson(messages).ToJsonString(PrettyJsonOptions);

    public static string ToCanonicalJson(GseValue value)
        => ToValueJson(value).ToJsonString(CompactJsonOptions);

    public static string ToPrettyJson(GseValue value)
        => ToValueJson(value).ToJsonString(PrettyJsonOptions);

    public static JsonObject ToMessageJson(GseMessage message)
    {
        var node = new JsonObject
        {
            ["name"] = message.Name
        };

        var args = new JsonObject();
        foreach (var pair in message.Arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            args[pair.Key] = ToValueJson(pair.Value);
        }

        node["args"] = args;
        return node;
    }

    public static JsonObject ToValueJson(GseValue value)
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
            GseValueKind.Nothing => new JsonObject { ["type"] = ":nothing" },
            GseValueKind.Text => new JsonObject { ["type"] = ":text", ["value"] = value.AsText() },
            GseValueKind.Tag => new JsonObject { ["type"] = ":tag", ["value"] = value.AsText() },
            GseValueKind.Boolean => new JsonObject { ["type"] = ":boolean", ["value"] = value.AsBoolean() },
            GseValueKind.Integer => new JsonObject { ["type"] = ":integer", ["value"] = value.AsInteger().ToString(CultureInfo.InvariantCulture) },
            GseValueKind.Decimal => ToDecimalJson((GseDecimalValue)value),
            GseValueKind.Percentage => new JsonObject { ["type"] = ":percentage", ["value"] = FormatDecimal(value.AsNumber()) },
            GseValueKind.Vector2 => ToVector2Json((GseVector2Value)value),
            GseValueKind.Vector3 => ToVector3Json((GseVector3Value)value),
            GseValueKind.Optional => ToOptionalJson(value),
            GseValueKind.List => new JsonObject { ["type"] = ":list", ["items"] = ToValueArrayJson(value.AsList()) },
            GseValueKind.Dictionary => new JsonObject { ["type"] = ":dictionary", ["entries"] = ToEntriesJson(value.AsDictionary()) },
            GseValueKind.Set => new JsonObject { ["type"] = ":set", ["items"] = ToValueArrayJson(value.AsSet().OrderBy(item => item, GseValue.StableComparer)) },
            GseValueKind.Dice => new JsonObject { ["type"] = ":dice", ["rolls"] = ToIntegerArrayJson(value.AsDice().Rolls) },
            GseValueKind.Range => ToRangeJson(value),
            GseValueKind.Message => new JsonObject { ["type"] = ":message", ["message"] = ToMessageJson(GetInternalProperty<GseMessage>(value, "Value")) },
            GseValueKind.Handler => throw new NotSupportedException("Handler values are not part of the conformance JSON value wire format."),
            GseValueKind.Sequence => throw new NotSupportedException("Sequence values are not part of the conformance JSON value wire format."),
            _ => throw new NotSupportedException($"Unsupported Gse value type '{value.Kind}'.")
        };
    }

    private static GseValue DecodeDecimalValue(JsonElement element)
    {
        var value = RequireString(element, "value", "decimal value");
        var unit = DecodeOptionalDecimalUnit(element);

        return value switch
        {
            "NaN" => GseValueFactory.DecimalNaN(),
            "Infinity" => GseValueFactory.DecimalInfinity(),
            "-Infinity" => GseValueFactory.DecimalNegativeInfinity(),
            _ => GseValueFactory.Decimal(decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture), unit)
        };
    }

    private static GseDecimalUnit? DecodeOptionalDecimalUnit(JsonElement element)
    {
        if (TryGetProperty(element, "unit", out var unitElement))
        {
            if (unitElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("Invalid decimal unit.");
            }

            var unitName = unitElement.GetString()!;
            if (!unitName.StartsWith(":", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid decimal unit '{unitName}'.");
            }

            if (!GseDecimalUnits.TryParseTypeName(unitName[1..], out var parsedUnit))
            {
                throw new InvalidOperationException($"Invalid decimal unit '{unitName}'.");
            }

            return parsedUnit;
        }

        return null;
    }

    private static GseValue DecodeOptionalValue(JsonElement element)
    {
        var hasValue = RequireBoolean(element, "hasValue", "optional hasValue");
        if (!hasValue)
        {
            return GseValueFactory.OptionalNone();
        }

        return GseValueFactory.OptionalSome(DecodeValue(RequireObjectProperty(element, "value", "optional value")));
    }

    private static IReadOnlyDictionary<string, GseValue> DecodeEntries(JsonElement element)
    {
        var entriesElement = RequireObjectProperty(element, "entries", "dictionary entries");
        var entries = new Dictionary<string, GseValue>(StringComparer.Ordinal);
        foreach (var property in entriesElement.EnumerateObject())
        {
            entries[property.Name] = DecodeValue(property.Value);
        }

        return entries;
    }

    private static JsonArray ToMessageArrayJson(IEnumerable<GseMessage> messages)
    {
        var array = new JsonArray();
        foreach (var message in messages)
        {
            array.Add(ToMessageJson(message));
        }

        return array;
    }

    private static JsonArray ToValueArrayJson(IEnumerable<GseValue> values)
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

    private static JsonObject ToEntriesJson(IReadOnlyDictionary<string, GseValue> entries)
    {
        var node = new JsonObject();
        foreach (var pair in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            node[pair.Key] = ToValueJson(pair.Value);
        }

        return node;
    }

    private static JsonObject ToOptionalJson(GseValue value)
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

    private static JsonObject ToRangeJson(GseValue value)
        => new()
        {
            ["type"] = ":range",
            ["from"] = GetInternalProperty<long>(value, "From").ToString(CultureInfo.InvariantCulture),
            ["to"] = GetInternalProperty<long>(value, "To").ToString(CultureInfo.InvariantCulture),
            ["step"] = GetInternalProperty<long>(value, "Step").ToString(CultureInfo.InvariantCulture)
        };

    private static JsonObject ToDecimalJson(GseDecimalValue value)
    {
        var node = new JsonObject
        {
            ["type"] = ":decimal",
            ["value"] = FormatDecimal(value)
        };

        if (value.Unit.HasValue)
        {
            node["unit"] = ToCanonicalTypeName(GseDecimalUnits.ToTypeName(value.Unit.Value));
        }

        return node;
    }

    private static JsonObject ToVector2Json(GseVector2Value value)
    {
        var node = new JsonObject
        {
            ["type"] = ":vector2",
            ["x"] = FormatDecimal(value.X),
            ["y"] = FormatDecimal(value.Y)
        };

        if (value.Unit.HasValue)
        {
            node["unit"] = ToCanonicalTypeName(GseDecimalUnits.ToTypeName(value.Unit.Value));
        }

        return node;
    }

    private static JsonObject ToVector3Json(GseVector3Value value)
    {
        var node = new JsonObject
        {
            ["type"] = ":vector3",
            ["x"] = FormatDecimal(value.X),
            ["y"] = FormatDecimal(value.Y),
            ["z"] = FormatDecimal(value.Z)
        };

        if (value.Unit.HasValue)
        {
            node["unit"] = ToCanonicalTypeName(GseDecimalUnits.ToTypeName(value.Unit.Value));
        }

        return node;
    }

    private static string ToCanonicalTypeName(string typeName)
        => typeName.StartsWith(":", StringComparison.Ordinal) ? typeName : ":" + typeName;

    private static string FormatDecimal(GseValue value)
    {
        if (value.IsNaN())
        {
            return "NaN";
        }

        if (value.IsInfinity())
        {
            return value.IsNegativeInfinity() ? "-Infinity" : "Infinity";
        }

        return FormatDecimal(value.AsNumber());
    }

    private static string FormatDecimal(decimal value)
        => value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static T GetInternalProperty<T>(GseValue value, string propertyName)
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
            throw new InvalidOperationException($"Invalid {description}; expected canonical Gse type name like ':integer'.");
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

    private static decimal RequireDecimal(JsonElement element, string propertyName, string description)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            throw new InvalidOperationException($"Missing {description}.");
        }

        return ReadDecimal(property, description);
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

    private static decimal ReadDecimal(JsonElement element, string description)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var textNumber) => textNumber,
            _ => throw new InvalidOperationException($"Invalid {description}; expected decimal string or JSON number.")
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
