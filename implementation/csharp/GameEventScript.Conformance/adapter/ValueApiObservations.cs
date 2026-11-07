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
        if (input.TryGetProperty("literal", out var literal))
        {
            RuntimeValues.Fields(input, "literal");
            RuntimeValues.Fields(literal, "text", "mode");
            _ = literal.GetProperty("text").GetString() ?? throw new ArgumentException("Literal text required.");
            if (literal.GetProperty("mode").GetString() is not ("lenient" or "strict")) throw new ArgumentException("Invalid literal mode.");
            return;
        }
        if (input.TryGetProperty("access", out var access))
        {
            RuntimeValues.Fields(input, "access");
            RuntimeValues.Fields(access, "value", "external", "fromEntries", "members", "readFields", "materialize", "toJson", "toMessageJson");
            var probe = access.GetProperty("external").GetString();
            if (probe is not (null or "counting" or "typed" or "failing" or "faulting" or "ordered" or "sparse")) throw new ArgumentException("Unknown external probe.");
            if (probe is null == (access.GetProperty("value").ValueKind == JsonValueKind.Null)) throw new ArgumentException("Access requires one of value and external.");
            _ = access.GetProperty("fromEntries").GetBoolean();
            _ = access.GetProperty("toJson").GetBoolean();
            _ = access.GetProperty("toMessageJson").GetBoolean();
            if (probe is null) _ = Construct(access.GetProperty("value"));
            foreach (var path in access.GetProperty("members").EnumerateArray())
                foreach (var name in path.EnumerateArray()) _ = name.GetString() ?? throw new ArgumentException("Member name required.");
            return;
        }
        RuntimeValues.Fields(input, "value", "equalTo", "notEqualTo", "mutateSourceAfterCreate");
        _ = input.GetProperty("mutateSourceAfterCreate").GetBoolean();
        foreach (var key in new[] { "value", "equalTo", "notEqualTo" })
            if (key == "value" || input.GetProperty(key).ValueKind != JsonValueKind.Null) _ = Construct(input.GetProperty(key));
    }

    internal static object Run(JsonElement input)
    {
        if (input.TryGetProperty("literal", out var literal)) return RunLiteral(literal);
        if (input.TryGetProperty("access", out var access)) return RunAccess(access);
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

    // Member reads and map traversal; `normalized` and `traversal` stay null for an external value, whose fields only
    // its getters could produce.
    private static object RunAccess(JsonElement access)
    {
        var external = access.GetProperty("external").GetString();
        var probe = new ExternalProbe(external ?? "counting");
        var value = external is not null ? GesValue.GesExternal(probe)
            : access.GetProperty("fromEntries").GetBoolean() ? FromEntries(access.GetProperty("value"))
            : Construct(access.GetProperty("value"));
        var length = value.Length;
        var members = new List<object?>();
        foreach (var path in access.GetProperty("members").EnumerateArray())
        {
            var member = value;
            foreach (var name in path.EnumerateArray()) member = member[name.GetString()!];
            members.Add(RuntimeValues.Encode(member));
        }
        var asMapPresent = value.AsMap() is not null;
        var traversal = Entries(value.AsMap());
        var readFields = new List<object?>();
        foreach (var name in access.GetProperty("readFields").EnumerateArray())
            readFields.Add(Observed(() => value.ReadField(name.GetString()!) is { } field ? RuntimeValues.Encode(field) : null));
        var materialized = new List<object?>();
        for (var round = 0; round < access.GetProperty("materialize").GetInt32(); round++) materialized.Add(Observed(() => Entries(value.MaterializedMap())));
        var json = access.GetProperty("toJson").GetBoolean() ? Observed(() => value.ToJson()) : null;
        var messageJson = access.GetProperty("toMessageJson").GetBoolean()
            ? Observed(() => GameEventScriptMessage.Create("Done", [new GesMessageArgument("value", value)]).ToJson())
            : null;
        return new
        {
            status = "ok",
            access = new
            {
                length,
                json,
                messageJson,
                normalized = external is not null ? null : RuntimeValues.Encode(value),
                members,
                traversal,
                asMapPresent,
                hostObject = ReferenceEquals(value.ExternalValue, probe),
                readFields,
                materialized,
                externalFieldReads = probe.Reads
            }
        };
    }

    private static object? Observed(Func<object?> read)
    {
        try
        {
            return read();
        }
        catch (GesFatalRuntimeException exception)
        {
            return new { failure = exception.Diagnostic.Code };
        }
    }

    private static GesValue FromEntries(JsonElement input)
    {
        var type = input.GetProperty("type").GetString()!;
        var entries = input.GetProperty("entries").EnumerateArray().Select(e => (e.GetProperty("key").GetString()!, Construct(e.GetProperty("value")))).ToArray();
        return type == ":Map" ? GesValue.GesMap(entries) : GesValue.GesRecord(type[1..], entries);
    }

    private static List<object>? Entries(GesValueMap? map)
    {
        if (map is null) return null;
        var entries = new List<object>();
        foreach (var (key, entry) in map) entries.Add(new { key, value = RuntimeValues.Encode(entry) });
        return entries;
    }

    // Exposes the fields of a Record as an external value of the same type name; a custom field type keeps each value,
    // and one getter can raise a platform failure or, with a code, a deliberate fault.
    internal sealed class ExternalRecord(GesValue record, string? failingField = null, string? failingCode = null) : IGesExternalValue
    {
        public GesExternalTypeDefinition Definition { get; } = new(record.CustomTypeName!, record.AsMap()!.Select(entry => new GesExternalTypeFieldDefinition(entry.Key, "Field")), []);

        public GesValue? GetField(string fieldName)
        {
            if (fieldName != failingField) return record.AsMap()!.Get(fieldName);
            throw failingCode is null ? new InvalidOperationException("Configured field failure.") : new GesExtensionFaultException(failingCode, "Configured field fault.");
        }
    }

    // `counting` returns the number of reads, `typed` an integer that its Float<Watt> declaration turns into watts,
    // `failing` raises an unanticipated platform failure, `faulting` a deliberate fault, `ordered` a deliberate fault
    // per field and `sparse` leaves one field without a value.
    private sealed class ExternalProbe(string mode) : IGesExternalValue
    {
        public int Reads { get; private set; }

        public GesExternalTypeDefinition Definition { get; } = new("AccessProbe", mode switch
        {
            "typed" => [new GesExternalTypeFieldDefinition("power", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitWatt)],
            "ordered" => [new GesExternalTypeFieldDefinition("z", "Number"), new GesExternalTypeFieldDefinition("a", "Number")],
            "sparse" => [new GesExternalTypeFieldDefinition("c", "Number"), new GesExternalTypeFieldDefinition("b", "Number"), new GesExternalTypeFieldDefinition("a", "Number")],
            _ => [new GesExternalTypeFieldDefinition("x", "Number")]
        }, []);

        public GesValue? GetField(string fieldName)
        {
            Reads++;
            return mode switch
            {
                "failing" => throw new InvalidOperationException("Configured field failure."),
                "faulting" => throw new GesExtensionFaultException("probe.fault", "Configured field fault."),
                "typed" => GesValue.GesInteger(3),
                "ordered" => throw new GesExtensionFaultException("probe." + fieldName, "Configured field fault."),
                "sparse" => fieldName == "b" ? null : GesValue.GesInteger(fieldName == "c" ? 1 : 3),
                _ => GesValue.GesInteger(Reads)
            };
        }
    }

    private static object RunLiteral(JsonElement literal)
    {
        var text = literal.GetProperty("text").GetString()!;
        try
        {
            var value = literal.GetProperty("mode").GetString() == "strict" ? GesValue.ParseLiteralStrict(text) : GesValue.ParseLiteral(text);
            return new { status = "ok", literal = new { value = value.HasValue ? RuntimeValues.Encode(value.Value) : null, limit = (object?)null } };
        }
        catch (GesLiteralLimitException exception)
        {
            return new { status = "ok", literal = new { value = (object?)null, limit = (object?)new { name = exception.LimitName, limit = exception.Limit } } };
        }
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
            if (type is null || !Regex.IsMatch(type, @"\A:[A-Z][A-Za-z0-9]*(?:_[A-Z][A-Za-z0-9]*)*\z")) throw new ArgumentException("Invalid collection type.");
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
                return new GesMessageArgument(a.GetProperty("name").GetString() ?? throw new ArgumentException("Argument name required."), Construct(a.GetProperty("value")));
            }).ToArray();
            return GesValue.GesMessage(GameEventScriptMessage.Create(name, arguments, message.GetProperty("tags").EnumerateArray().Select(t => t.GetString() ?? throw new ArgumentException("Tag required.")).ToArray()));
        }
        return RuntimeValues.Decode(input);
    }
}
