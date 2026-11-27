// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

// The per-case handle setup: the handle types the compiler and the host know, and the scripted resolver answers
// (specs/Conformance/MarkdownFormat.md#handle-setup).
internal sealed class AdapterHandles
{
    private readonly JsonElement _store;

    private AdapterHandles(JsonElement store) => _store = store;

    internal static AdapterHandles? From(JsonElement test) => test.TryGetProperty("handles", out var store) ? new AdapterHandles(store) : null;

    internal static void Validate(JsonElement store)
    {
        Fields(store, "schemas", "hostSchemas", "resolvers", "answers", "refAnswers", "resolveRefs", "stores", "lists", "enumerable");
        _ = Parse(store.GetProperty("schemas"));
        _ = Parse(store.GetProperty("hostSchemas"));
        var resolvers = store.GetProperty("resolvers");
        if (resolvers.ValueKind is not (JsonValueKind.Null or JsonValueKind.Array)) throw new ArgumentException("Invalid resolver list.");
        foreach (var answer in store.GetProperty("answers").EnumerateArray())
        {
            Fields(answer, "handle", "answer", "text", "version");
            _ = GesHandle.Parse(answer.GetProperty("handle").GetString()!);
            if (answer.GetProperty("answer").GetString() is not ("document" or "record" or "missing" or "notVisible" or "fail")) throw new ArgumentException("Invalid handle answer.");
        }

        foreach (var answer in store.GetProperty("refAnswers").EnumerateArray())
        {
            Fields(answer, "ref", "answer", "version");
            if (GesHandle.Parse(answer.GetProperty("ref").GetString()!).Ref.IsNothing) throw new ArgumentException("A ref answer names no ref.");
            if (answer.GetProperty("answer").GetString() is not ("version" or "missing" or "fail")) throw new ArgumentException("Invalid ref answer.");
        }

        foreach (var reference in store.GetProperty("resolveRefs").EnumerateArray()) _ = GesHandle.Parse(reference.GetString()!);
        foreach (var listing in store.GetProperty("lists").EnumerateArray())
        {
            Fields(listing, "type", "root", "handles");
            if (listing.GetProperty("root").ValueKind != JsonValueKind.Null) _ = GesHandle.Parse(listing.GetProperty("root").GetString()!);
            foreach (var handle in listing.GetProperty("handles").EnumerateArray()) _ = GesHandle.Parse(handle.GetString()!);
        }

        if (store.GetProperty("enumerable").ValueKind is not (JsonValueKind.Null or JsonValueKind.Array)) throw new ArgumentException("Invalid enumerable list.");
        if (store.GetProperty("stores").ValueKind is not (JsonValueKind.Null or JsonValueKind.Object)) throw new ArgumentException("Invalid host stores.");
    }

    // Gives the compiler the handle types the sources may name and, when the case says so, the types the host enumerates.
    internal void Configure(GesProgramBuilder builder)
    {
        builder.WithHandleTypes(Parse(_store.GetProperty("schemas")).HandleTypes);
        var enumerable = _store.GetProperty("enumerable");
        if (enumerable.ValueKind == JsonValueKind.Array) builder.WithEnumerableHandleTypes(enumerable.EnumerateArray().Select(name => name.GetString()!));
    }

    // Registers a fresh scripted resolver for every selected type of the host store.
    internal void Register(GesHostBuilder builder)
    {
        var resolvers = _store.GetProperty("resolvers");
        var selected = resolvers.ValueKind == JsonValueKind.Null ? null : resolvers.EnumerateArray().Select(name => name.GetString()!).ToHashSet(StringComparer.Ordinal);
        var answers = new Dictionary<GesHandle, List<JsonElement>>();
        foreach (var answer in _store.GetProperty("answers").EnumerateArray())
        {
            var handle = GesHandle.Parse(answer.GetProperty("handle").GetString()!);
            if (!answers.TryGetValue(handle, out var list)) answers.Add(handle, list = new List<JsonElement>());
            list.Add(answer);
        }

        // A ref is part of the text form but not of handle equality, so ref answers are found by text.
        var refAnswers = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var answer in _store.GetProperty("refAnswers").EnumerateArray())
            refAnswers[GesHandle.Parse(answer.GetProperty("ref").GetString()!).ToString()] = answer;

        var lists = new Dictionary<(string Type, GesHandle? Root), GesHandle[]>();
        foreach (var listing in _store.GetProperty("lists").EnumerateArray())
        {
            var root = listing.GetProperty("root").ValueKind == JsonValueKind.Null ? null : GesHandle.Parse(listing.GetProperty("root").GetString()!);
            lists.Add((listing.GetProperty("type").GetString()!, root), listing.GetProperty("handles").EnumerateArray().Select(handle => GesHandle.Parse(handle.GetString()!)).ToArray());
        }

        // A store read from the case's files answers for its own type in place of the scripted resolver.
        var set = Parse(_store.GetProperty("hostSchemas"));
        var stores = _store.GetProperty("stores").ValueKind == JsonValueKind.Object
            ? SchemaApiObservations.ReadStores(set, _store.GetProperty("stores")).ToDictionary(store => store.Type.Name, StringComparer.Ordinal)
            : new Dictionary<string, GesStore>(StringComparer.Ordinal);
        var calls = new Dictionary<GesHandle, int>();
        foreach (var type in set.HandleTypes)
        {
            if (stores.TryGetValue(type.Name, out var store)) builder.WithHandleResolver(type, store);
            else if (selected is null || selected.Contains(type.Name)) builder.WithHandleResolver(type, new ScriptedResolver(type.Name, answers, refAnswers, lists, calls));
        }
    }

    // Resolves each scripted ref through the started host, in order: the returned handle or Nothing, or the failure.
    internal object[] ResolveRefs(GameEventScriptHost host)
    {
        var results = new List<object>();
        foreach (var item in _store.GetProperty("resolveRefs").EnumerateArray())
        {
            var text = item.GetString()!;
            try
            {
                var resolved = host.ResolveRef(GesHandle.Parse(text));
                results.Add(new { @ref = text, value = resolved is null ? RuntimeValues.Encode(default) : RuntimeValues.Encode(GesValue.GesHandle(resolved)) });
            }
            catch (GesHandleException exception)
            {
                results.Add(new { @ref = text, error = new { phase = "runtime", code = exception.Code, symbol = exception.Symbol } });
            }
        }

        return results.ToArray();
    }

    private static GesSchemaSet Parse(JsonElement schemas)
        => GesSchemaSet.Parse(schemas.EnumerateArray().Select((text, index) => new GesSchemaSource($"handles-{index + 1}.gess", text.GetString()!)).ToArray());

    private static void Fields(JsonElement element, params string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
            throw new ArgumentException("Unknown field or invalid object.");
        foreach (var name in allowed) _ = element.GetProperty(name);
    }

    // Answers each object's reads in the authored order; the last answer repeats, and an object without one fails. The
    // resolvers of all types share the answers and the read counts; each lists its own type's scripted handles.
    private sealed class ScriptedResolver(
        string type,
        Dictionary<GesHandle, List<JsonElement>> answers,
        Dictionary<string, JsonElement> refAnswers,
        Dictionary<(string Type, GesHandle? Root), GesHandle[]> lists,
        Dictionary<GesHandle, int> calls) : IGesHandleResolver
    {
        /// <summary>Lists the case's scripted handles of this type, or of one root's parts; without a listing the type does not enumerate.</summary>
        public IReadOnlyList<GesHandle>? Handles(GesHandle? root) => lists.TryGetValue((type, root), out var handles) ? handles : null;

        /// <summary>Answers one state read from the case's scripted answers.</summary>
        public void Resolve(GesHandleResolveCall call)
        {
            if (!answers.TryGetValue(call.Handle, out var list)) throw new InvalidOperationException("The case has no answer for " + call.Handle + ".");
            calls.TryGetValue(call.Handle, out var count);
            calls[call.Handle] = count + 1;
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
                case "notVisible":
                    call.SetNotVisible();
                    break;
                default:
                    throw new InvalidOperationException("The case's resolver fails for " + call.Handle + ".");
            }
        }

        /// <summary>Answers a ref from the case's scripted ref answers; a ref without one fails.</summary>
        public GesValue ResolveRef(GesHandle handle)
        {
            if (!refAnswers.TryGetValue(handle.ToString(), out var answer) || answer.GetProperty("answer").GetString() == "fail")
                throw new InvalidOperationException("The case's resolver fails for " + handle + ".");
            return answer.GetProperty("answer").GetString() == "missing" ? default : RuntimeValues.Decode(answer.GetProperty("version"));
        }
    }
}
