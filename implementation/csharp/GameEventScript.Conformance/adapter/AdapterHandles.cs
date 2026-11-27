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
        Fields(catalog, "schemas", "hostSchemas", "resolvers", "answers", "refAnswers", "resolveRefs");
        _ = Parse(catalog.GetProperty("schemas"));
        _ = Parse(catalog.GetProperty("hostSchemas"));
        var resolvers = catalog.GetProperty("resolvers");
        if (resolvers.ValueKind is not (JsonValueKind.Null or JsonValueKind.Array)) throw new ArgumentException("Invalid resolver list.");
        foreach (var answer in catalog.GetProperty("answers").EnumerateArray())
        {
            Fields(answer, "handle", "answer", "text", "version");
            _ = GesHandle.Parse(answer.GetProperty("handle").GetString()!);
            if (answer.GetProperty("answer").GetString() is not ("document" or "record" or "missing" or "notVisible" or "fail")) throw new ArgumentException("Invalid handle answer.");
        }

        foreach (var answer in catalog.GetProperty("refAnswers").EnumerateArray())
        {
            Fields(answer, "ref", "answer", "version");
            if (GesHandle.Parse(answer.GetProperty("ref").GetString()!).Ref.IsNothing) throw new ArgumentException("A ref answer names no ref.");
            if (answer.GetProperty("answer").GetString() is not ("version" or "missing" or "fail")) throw new ArgumentException("Invalid ref answer.");
        }

        foreach (var reference in catalog.GetProperty("resolveRefs").EnumerateArray()) _ = GesHandle.Parse(reference.GetString()!);
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

        // A ref is part of the text form but not of handle equality, so ref answers are found by text.
        var refAnswers = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var answer in _catalog.GetProperty("refAnswers").EnumerateArray())
            refAnswers[GesHandle.Parse(answer.GetProperty("ref").GetString()!).ToString()] = answer;

        var resolver = new ScriptedResolver(answers, refAnswers);
        foreach (var type in Parse(_catalog.GetProperty("hostSchemas")).HandleTypes)
            if (selected is null || selected.Contains(type.Name)) builder.WithHandleResolver(type, resolver);
    }

    // Resolves each scripted ref through the started host, in order: the returned handle or Nothing, or the failure.
    internal object[] ResolveRefs(GameEventScriptHost host)
    {
        var results = new List<object>();
        foreach (var item in _catalog.GetProperty("resolveRefs").EnumerateArray())
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

    // Answers each object's reads in the authored order; the last answer repeats, and an object without one fails.
    private sealed class ScriptedResolver(Dictionary<GesHandle, List<JsonElement>> answers, Dictionary<string, JsonElement> refAnswers) : IGesHandleResolver
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
