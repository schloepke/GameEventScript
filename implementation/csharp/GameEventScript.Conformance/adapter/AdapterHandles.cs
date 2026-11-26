// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

// The per-case handle catalog: the handle types the compiler and the host know, and the scripted resolver answers
// (specs/Conformance/MarkdownFormat.md#handle-catalogs).
internal sealed class AdapterHandles
{
    private readonly JsonElement _catalog;

    private AdapterHandles(JsonElement catalog) => _catalog = catalog;

    internal static AdapterHandles? From(JsonElement test) => test.TryGetProperty("handles", out var catalog) ? new AdapterHandles(catalog) : null;

    internal static void Validate(JsonElement catalog)
    {
        Fields(catalog, "schemas", "hostSchemas", "resolvers", "answers");
        _ = Parse(catalog.GetProperty("schemas"));
        _ = Parse(catalog.GetProperty("hostSchemas"));
        var resolvers = catalog.GetProperty("resolvers");
        if (resolvers.ValueKind is not (JsonValueKind.Null or JsonValueKind.Array)) throw new ArgumentException("Invalid resolver list.");
        foreach (var answer in catalog.GetProperty("answers").EnumerateArray())
        {
            Fields(answer, "handle", "answer", "text", "version");
            _ = GesHandle.Parse(answer.GetProperty("handle").GetString()!);
            if (answer.GetProperty("answer").GetString() is not ("document" or "record" or "missing" or "fail")) throw new ArgumentException("Invalid handle answer.");
        }
    }

    // The handle types the sources may name.
    internal GesReadOnlyArray<GesHandleType> CompilerTypes => Parse(_catalog.GetProperty("schemas")).HandleTypes;

    // Registers a fresh scripted resolver for every selected type of the host catalog.
    internal void Register(GesHostBuilder builder)
    {
        var resolvers = _catalog.GetProperty("resolvers");
        var selected = resolvers.ValueKind == JsonValueKind.Null ? null : resolvers.EnumerateArray().Select(name => name.GetString()!).ToHashSet(StringComparer.Ordinal);
        var answers = new Dictionary<GesHandle, List<JsonElement>>();
        foreach (var answer in _catalog.GetProperty("answers").EnumerateArray())
        {
            var handle = GesHandle.Parse(answer.GetProperty("handle").GetString()!);
            if (!answers.TryGetValue(handle, out var list)) answers.Add(handle, list = new List<JsonElement>());
            list.Add(answer);
        }

        var resolver = new ScriptedResolver(answers);
        foreach (var type in Parse(_catalog.GetProperty("hostSchemas")).HandleTypes)
            if (selected is null || selected.Contains(type.Name)) builder.WithHandleResolver(type, resolver);
    }

    private static GesSchemaSet Parse(JsonElement schemas)
        => GesSchemaSet.Parse(schemas.EnumerateArray().Select((text, index) => new GesSchemaSource($"handles-{index + 1}.gess", text.GetString()!)).ToArray());

    private static void Fields(JsonElement element, params string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
            throw new ArgumentException("Unknown field or invalid object.");
        foreach (var name in allowed) _ = element.GetProperty(name);
    }

    // Answers each object's reads in the authored order; the last answer repeats, and an object without one fails.
    private sealed class ScriptedResolver(Dictionary<GesHandle, List<JsonElement>> answers) : IGesHandleResolver
    {
        private readonly Dictionary<GesHandle, int> _calls = new();

        /// <summary>Answers one state read from the case's scripted answers.</summary>
        public void Resolve(GesHandleResolveCall call)
        {
            if (!answers.TryGetValue(call.Handle, out var list)) throw new InvalidOperationException("The case has no answer for " + call.Handle + ".");
            _calls.TryGetValue(call.Handle, out var count);
            _calls[call.Handle] = count + 1;
            var answer = list[Math.Min(count, list.Count - 1)];
            var version = answer.GetProperty("version");
            var passed = version.ValueKind == JsonValueKind.Null ? (GesValue?)null : RuntimeValues.Decode(version);
            switch (answer.GetProperty("answer").GetString())
            {
                case "document":
                    var text = answer.GetProperty("text").GetString()!;
                    if (passed is { } documentVersion) call.SetDocument(text, documentVersion);
                    else call.SetDocument(text);
                    break;
                case "record":
                    var record = GesValue.ParseLiteralStrict(answer.GetProperty("text").GetString()!) ?? throw new ArgumentException("The record answer is no data literal.");
                    if (passed is { } recordVersion) call.SetState(record, recordVersion);
                    else call.SetState(record);
                    break;
                case "missing":
                    call.SetMissing();
                    break;
                default:
                    throw new InvalidOperationException("The case's resolver fails for " + call.Handle + ".");
            }
        }

        /// <summary>Refs are not part of the scripted answers.</summary>
        public GesValue ResolveRef(GesHandle handle) => throw new InvalidOperationException("The case resolves no refs.");
    }
}
