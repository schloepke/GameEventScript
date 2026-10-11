// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

internal static class ValueApiObservations
{
    internal static void Validate(JsonElement input)
    {
        RuntimeValues.Fields(input, "value", "equalTo", "notEqualTo", "mutateSourceAfterCreate");
        _ = input.GetProperty("mutateSourceAfterCreate").GetBoolean();
        foreach (var key in new[] { "value", "equalTo", "notEqualTo" })
            if (key == "value" || input.GetProperty(key).ValueKind != JsonValueKind.Null) _ = Construct(input.GetProperty(key));
    }

    internal static object Run(JsonElement input)
    {
        var value = Construct(input.GetProperty("value"), input.GetProperty("mutateSourceAfterCreate").GetBoolean());
        var equalInput = input.GetProperty("equalTo");
        var other = equalInput.ValueKind == JsonValueKind.Null ? (GesValue?)null : Construct(equalInput);
        var differentInput = input.GetProperty("notEqualTo");
        return new
        {
            status = "ok",
            value = new
            {
                normalized = RuntimeValues.Encode(value),
                isNumeric = value.IsNumeric,
                hasValue = value.HasValue,
                isNothing = value.IsNothing,
                hasUnit = value.HasUnit,
                asBoolean = value.AsBoolean(),
                length = value.Length,
                customTypeName = value.CustomTypeName,
                equal = other.HasValue ? (bool?)value.Equals(other.Value) : null,
                equalHash = other.HasValue ? (bool?)(value.GetHashCode() == other.Value.GetHashCode()) : null,
                notEqual = differentInput.ValueKind == JsonValueKind.Null ? (bool?)null : !value.Equals(Construct(differentInput))
            }
        };
    }

    internal static GesValue Construct(JsonElement input, bool mutate = false)
    {
        var type = input.GetProperty("type").GetString();
        if (type == ":List")
        {
            RuntimeValues.Fields(input, "type", "items");
            var items = input.GetProperty("items").EnumerateArray().Select(item => Construct(item)).ToArray();
            var result = GesValue.GesList(items);
            if (mutate && items.Length > 0) items[0] = GesValue.GesNothing();
            return result;
        }
        if (type == ":Dice")
        {
            RuntimeValues.Fields(input, "type", "rolls");
            var rolls = input.GetProperty("rolls").EnumerateArray().Select(item => item.GetInt32() > 0 ? item.GetInt32() : throw new ArgumentException("Positive roll required.")).ToArray();
            var result = GesValue.GesDice(rolls);
            if (mutate && rolls.Length > 0) rolls[0] = int.MinValue;
            return result;
        }
        if (input.TryGetProperty("entries", out var entries))
        {
            RuntimeValues.Fields(input, "type", "entries");
            if (type is null || !Regex.IsMatch(type, @"\A:[A-Z][A-Za-z0-9]*\z")) throw new ArgumentException("Invalid collection type.");
            foreach (var entry in entries.EnumerateArray()) RuntimeValues.Fields(entry, "key", "value");
            var keys = entries.EnumerateArray().Select(e => e.GetProperty("key").GetString() ?? throw new ArgumentException("Key required.")).ToArray();
            var values = entries.EnumerateArray().Select(e => Construct(e.GetProperty("value"))).ToArray();
            var result = type == ":Map" ? GesValue.GesMap(keys, values) : GesValue.GesRecord(type[1..], keys, values);
            if (mutate && keys.Length > 0) { keys[0] = "mutated"; values[0] = GesValue.GesNothing(); }
            return result;
        }
        if (type == ":Message")
        {
            RuntimeValues.Fields(input, "type", "message");
            var message = input.GetProperty("message");
            RuntimeValues.Fields(message, "name", "args", "tags");
            var name = message.GetProperty("name").GetString();
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Message name required.");
            var arguments = message.GetProperty("args").EnumerateArray().Select(a =>
            {
                RuntimeValues.Fields(a, "name", "value");
                return new GameEventScriptMessageArgument(a.GetProperty("name").GetString() ?? throw new ArgumentException("Argument name required."), Construct(a.GetProperty("value")));
            }).ToArray();
            return GesValue.GesMessage(GameEventScriptMessage.Create(name, arguments, message.GetProperty("tags").EnumerateArray().Select(t => t.GetString() ?? throw new ArgumentException("Tag required.")).ToArray()));
        }
        return RuntimeValues.Decode(input);
    }
}
