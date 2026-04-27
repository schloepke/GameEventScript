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
            RequireObject(argsElement, "message args");
            foreach (var property in argsElement.EnumerateObject())
            {
                args[property.Name] = DecodeValue(property.Value);
            }
        }

        return EventScriptMessage.Message(name, args);
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
                return EventScriptValue.Text(RequireString(element, "value", "text value"));
            case ":tag":
                return EventScriptValue.Tag(RequireString(element, "value", "tag value"));
            case ":boolean":
                return EventScriptValue.Boolean(RequireBoolean(element, "value", "boolean value"));
            case ":integer":
                return EventScriptValue.Integer(RequireInt64(element, "value", "integer value"));
            case ":decimal":
                return DecodeDecimalValue(element);
            case ":percentage":
                return EventScriptValue.Percentage(RequireDecimal(element, "value", "percentage ratio"));
            case ":optional":
                return DecodeOptionalValue(element);
            case ":list":
                return EventScriptValue.List(RequireArray(element, "items", "list items").EnumerateArray().Select(DecodeValue));
            case ":dictionary":
                return EventScriptValue.Dictionary(DecodeEntries(element));
            case ":set":
                return EventScriptValue.Set(RequireArray(element, "items", "set items").EnumerateArray().Select(DecodeValue));
            case ":dice":
                return EventScriptValue.Dice(EventScriptDiceValue.Create(RequireArray(element, "rolls", "dice rolls").EnumerateArray().Select(ReadInt32)));
            case ":range":
                return EventScriptValue.Range(
                    RequireInt64(element, "from", "range start"),
                    RequireInt64(element, "to", "range end"),
                    TryGetProperty(element, "step", out var stepElement) ? ReadInt64(stepElement, "range step") : 1L);
            case ":message":
                return EventScriptValue.Message(DecodeMessage(RequireObjectProperty(element, "message", "message value")));
            default:
                return EventScriptValue.CustomType(type[1..], DecodeEntries(element));
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

        return value.Type switch
        {
            EventScriptValueType.Nothing => new JsonObject { ["type"] = ":nothing" },
            EventScriptValueType.Text => new JsonObject { ["type"] = ":text", ["value"] = value.AsText() },
            EventScriptValueType.Tag => new JsonObject { ["type"] = ":tag", ["value"] = value.AsText() },
            EventScriptValueType.Boolean => new JsonObject { ["type"] = ":boolean", ["value"] = value.AsBoolean() },
            EventScriptValueType.Integer => new JsonObject { ["type"] = ":integer", ["value"] = value.AsInteger().ToString(CultureInfo.InvariantCulture) },
            EventScriptValueType.Decimal => new JsonObject { ["type"] = ":decimal", ["value"] = FormatDecimal(value) },
            EventScriptValueType.Percentage => new JsonObject { ["type"] = ":percentage", ["value"] = FormatDecimal(value.AsNumber()) },
            EventScriptValueType.Optional => ToOptionalJson(value),
            EventScriptValueType.List => new JsonObject { ["type"] = ":list", ["items"] = ToValueArrayJson(value.AsList()) },
            EventScriptValueType.Dictionary => new JsonObject { ["type"] = ":dictionary", ["entries"] = ToEntriesJson(value.AsDictionary()) },
            EventScriptValueType.Set => new JsonObject { ["type"] = ":set", ["items"] = ToValueArrayJson(value.AsSet().OrderBy(item => item, EventScriptValue.StableComparer)) },
            EventScriptValueType.Dice => new JsonObject { ["type"] = ":dice", ["rolls"] = ToIntegerArrayJson(value.AsDice().Rolls) },
            EventScriptValueType.Range => ToRangeJson(value),
            EventScriptValueType.Message => new JsonObject { ["type"] = ":message", ["message"] = ToMessageJson(GetInternalProperty<EventScriptMessage>(value, "Value")) },
            EventScriptValueType.Handler => throw new NotSupportedException("Handler values are not part of the conformance JSON value wire format."),
            EventScriptValueType.Iterator => throw new NotSupportedException("Iterator values are not part of the conformance JSON value wire format."),
            _ => throw new NotSupportedException($"Unsupported EventScript value type '{value.Type}'.")
        };
    }

    private static EventScriptValue DecodeDecimalValue(JsonElement element)
    {
        var value = RequireString(element, "value", "decimal value");
        return value switch
        {
            "NaN" => EventScriptValue.DecimalNaN(),
            "Infinity" => EventScriptValue.DecimalInfinity(),
            "-Infinity" => EventScriptValue.DecimalNegativeInfinity(),
            _ => EventScriptValue.Decimal(decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture))
        };
    }

    private static EventScriptValue DecodeOptionalValue(JsonElement element)
    {
        var hasValue = RequireBoolean(element, "hasValue", "optional hasValue");
        if (!hasValue)
        {
            return EventScriptValue.OptionalNone();
        }

        return EventScriptValue.OptionalSome(DecodeValue(RequireObjectProperty(element, "value", "optional value")));
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

        throw new NotSupportedException($"Cannot serialize {value.Type} value because property '{propertyName}' is unavailable.");
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
