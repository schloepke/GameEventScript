using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Conformance;

internal static class EventScriptConformanceValueCodec
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

    public static EventScriptMessage DecodeMessage(JsonElement element)
    {
        RequireObject(element, "message");
        var name = RequireString(element, "name", "message name");
        var args = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);

        if (TryGetProperty(element, "args", out var argsElement))
        {
            args = DecodeArguments(argsElement);
        }

        return EventScriptMessage.Message(name, args);
    }

    public static Dictionary<string, EventScriptValue> DecodeArguments(JsonElement element)
    {
        RequireObject(element, "message args");
        var args = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            args[property.Name] = DecodeValue(property.Value);
        }

        return args;
    }

    public static EventScriptValue DecodeValue(JsonElement element)
    {
        RequireObject(element, "value");
        var type = RequireCanonicalTypeName(element, "type", "value type");
        switch (type)
        {
            case ":nothing":
                return EventScriptValue.Nothing;
            case ":text":
                return EventScriptValueFactory.Text(RequireString(element, "value", "text value"));
            case ":tag":
                return EventScriptValueFactory.Tag(RequireString(element, "value", "tag value"));
            case ":boolean":
                return EventScriptValueFactory.Boolean(RequireBoolean(element, "value", "boolean value"));
            case ":integer":
                return EventScriptValueFactory.Integer(RequireInt64(element, "value", "integer value"));
            case ":decimal":
                return DecodeDecimalValue(element);
            case ":percentage":
                return EventScriptValueFactory.Percentage(RequireDecimal(element, "value", "percentage ratio"));
            case ":vector2":
                return EventScriptValueFactory.Vector2(
                    RequireDecimal(element, "x", "vector2 x component"),
                    RequireDecimal(element, "y", "vector2 y component"));
            case ":vector3":
                return EventScriptValueFactory.Vector3(
                    RequireDecimal(element, "x", "vector3 x component"),
                    RequireDecimal(element, "y", "vector3 y component"),
                    RequireDecimal(element, "z", "vector3 z component"));
            case ":optional":
                return DecodeOptionalValue(element);
            case ":list":
                return EventScriptValueFactory.List(RequireArray(element, "items", "list items").EnumerateArray().Select(DecodeValue));
            case ":dictionary":
                return EventScriptValueFactory.Dictionary(DecodeEntries(element));
            case ":set":
                return EventScriptValueFactory.Set(RequireArray(element, "items", "set items").EnumerateArray().Select(DecodeValue));
            case ":dice":
                return EventScriptValueFactory.Dice(EventScriptDiceValue.EventScriptDice(RequireArray(element, "rolls", "dice rolls").EnumerateArray().Select(ReadInt32)));
            case ":range":
                return EventScriptValueFactory.Range(
                    RequireInt64(element, "from", "range start"),
                    RequireInt64(element, "to", "range end"),
                    TryGetProperty(element, "step", out var stepElement) ? ReadInt64(stepElement, "range step") : 1L);
            case ":message":
                return EventScriptValueFactory.Message(DecodeMessage(RequireObjectProperty(element, "message", "message value")));
            default:
                return EventScriptValueFactory.CustomType(type[1..], DecodeEntries(element));
        }
    }

    public static string ToCanonicalJson(EventScriptMessage message)
        => ToMessageJson(message).ToJsonString(CompactJsonOptions);

    public static string ToPrettyJson(EventScriptMessage message)
        => ToMessageJson(message).ToJsonString(PrettyJsonOptions);

    public static string ToCanonicalJson(IEnumerable<EventScriptMessage> messages)
        => ToMessageArrayJson(messages).ToJsonString(CompactJsonOptions);

    public static string ToPrettyJson(IEnumerable<EventScriptMessage> messages)
        => ToMessageArrayJson(messages).ToJsonString(PrettyJsonOptions);

    public static string ToCanonicalJson(EventScriptValue value)
        => ToValueJson(value).ToJsonString(CompactJsonOptions);

    public static string ToPrettyJson(EventScriptValue value)
        => ToValueJson(value).ToJsonString(PrettyJsonOptions);

    public static JsonObject ToMessageJson(EventScriptMessage message)
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

    public static JsonObject ToValueJson(EventScriptValue value)
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
            EventScriptValueKind.Nothing => new JsonObject { ["type"] = ":nothing" },
            EventScriptValueKind.Text => new JsonObject { ["type"] = ":text", ["value"] = value.AsText() },
            EventScriptValueKind.Tag => new JsonObject { ["type"] = ":tag", ["value"] = value.AsText() },
            EventScriptValueKind.Boolean => new JsonObject { ["type"] = ":boolean", ["value"] = value.AsBoolean() },
            EventScriptValueKind.Integer => new JsonObject { ["type"] = ":integer", ["value"] = value.AsInteger().ToString(CultureInfo.InvariantCulture) },
            EventScriptValueKind.Decimal => ToDecimalJson((EventScriptDecimalValue)value),
            EventScriptValueKind.Percentage => new JsonObject { ["type"] = ":percentage", ["value"] = FormatDecimal(value.AsNumber()) },
            EventScriptValueKind.Vector2 => ToVector2Json((EventScriptVector2Value)value),
            EventScriptValueKind.Vector3 => ToVector3Json((EventScriptVector3Value)value),
            EventScriptValueKind.Optional => ToOptionalJson(value),
            EventScriptValueKind.List => new JsonObject { ["type"] = ":list", ["items"] = ToValueArrayJson(value.AsList()) },
            EventScriptValueKind.Dictionary => new JsonObject { ["type"] = ":dictionary", ["entries"] = ToEntriesJson(value.AsDictionary()) },
            EventScriptValueKind.Set => new JsonObject { ["type"] = ":set", ["items"] = ToValueArrayJson(value.AsSet().OrderBy(item => item, EventScriptValue.StableComparer)) },
            EventScriptValueKind.Dice => new JsonObject { ["type"] = ":dice", ["rolls"] = ToIntegerArrayJson(value.AsDice().Rolls) },
            EventScriptValueKind.Range => ToRangeJson(value),
            EventScriptValueKind.Message => new JsonObject { ["type"] = ":message", ["message"] = ToMessageJson(GetInternalProperty<EventScriptMessage>(value, "Value")) },
            EventScriptValueKind.Handler => throw new NotSupportedException("Handler values are not part of the conformance JSON value wire format."),
            EventScriptValueKind.Sequence => throw new NotSupportedException("Sequence values are not part of the conformance JSON value wire format."),
            _ => throw new NotSupportedException($"Unsupported EventScript value type '{value.Kind}'.")
        };
    }

    private static EventScriptValue DecodeDecimalValue(JsonElement element)
    {
        var value = RequireString(element, "value", "decimal value");
        var unit = default(EventScriptDecimalUnit?);
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

            if (!EventScriptDecimalUnits.TryParseTypeName(unitName[1..], out var parsedUnit))
            {
                throw new InvalidOperationException($"Invalid decimal unit '{unitName}'.");
            }

            unit = parsedUnit;
        }

        return value switch
        {
            "NaN" => EventScriptValueFactory.DecimalNaN(),
            "Infinity" => EventScriptValueFactory.DecimalInfinity(),
            "-Infinity" => EventScriptValueFactory.DecimalNegativeInfinity(),
            _ => EventScriptValueFactory.Decimal(decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture), unit)
        };
    }

    private static EventScriptValue DecodeOptionalValue(JsonElement element)
    {
        var hasValue = RequireBoolean(element, "hasValue", "optional hasValue");
        if (!hasValue)
        {
            return EventScriptValueFactory.OptionalNone();
        }

        return EventScriptValueFactory.OptionalSome(DecodeValue(RequireObjectProperty(element, "value", "optional value")));
    }

    private static IReadOnlyDictionary<string, EventScriptValue> DecodeEntries(JsonElement element)
    {
        var entriesElement = RequireObjectProperty(element, "entries", "dictionary entries");
        var entries = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var property in entriesElement.EnumerateObject())
        {
            entries[property.Name] = DecodeValue(property.Value);
        }

        return entries;
    }

    private static JsonArray ToMessageArrayJson(IEnumerable<EventScriptMessage> messages)
    {
        var array = new JsonArray();
        foreach (var message in messages)
        {
            array.Add(ToMessageJson(message));
        }

        return array;
    }

    private static JsonArray ToValueArrayJson(IEnumerable<EventScriptValue> values)
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

    private static JsonObject ToEntriesJson(IReadOnlyDictionary<string, EventScriptValue> entries)
    {
        var node = new JsonObject();
        foreach (var pair in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            node[pair.Key] = ToValueJson(pair.Value);
        }

        return node;
    }

    private static JsonObject ToOptionalJson(EventScriptValue value)
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

    private static JsonObject ToRangeJson(EventScriptValue value)
        => new()
        {
            ["type"] = ":range",
            ["from"] = GetInternalProperty<long>(value, "From").ToString(CultureInfo.InvariantCulture),
            ["to"] = GetInternalProperty<long>(value, "To").ToString(CultureInfo.InvariantCulture),
            ["step"] = GetInternalProperty<long>(value, "Step").ToString(CultureInfo.InvariantCulture)
        };

    private static JsonObject ToDecimalJson(EventScriptDecimalValue value)
    {
        var node = new JsonObject
        {
            ["type"] = ":decimal",
            ["value"] = FormatDecimal(value)
        };

        if (value.Unit.HasValue)
        {
            node["unit"] = ToCanonicalTypeName(EventScriptDecimalUnits.ToTypeName(value.Unit.Value));
        }

        return node;
    }

    private static JsonObject ToVector2Json(EventScriptVector2Value value)
        => new()
        {
            ["type"] = ":vector2",
            ["x"] = FormatDecimal(value.X),
            ["y"] = FormatDecimal(value.Y)
        };

    private static JsonObject ToVector3Json(EventScriptVector3Value value)
        => new()
        {
            ["type"] = ":vector3",
            ["x"] = FormatDecimal(value.X),
            ["y"] = FormatDecimal(value.Y),
            ["z"] = FormatDecimal(value.Z)
        };

    private static string ToCanonicalTypeName(string typeName)
        => typeName.StartsWith(":", StringComparison.Ordinal) ? typeName : ":" + typeName;

    private static string FormatDecimal(EventScriptValue value)
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

    private static T GetInternalProperty<T>(EventScriptValue value, string propertyName)
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
            throw new InvalidOperationException($"Invalid {description}; expected canonical EventScript type name like ':integer'.");
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
