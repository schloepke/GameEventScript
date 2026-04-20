#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

internal static class EventScriptArgumentMap
{
    public static readonly EventScriptNamedArguments Empty = EventScriptNamedArguments.Empty;

    public static IReadOnlyDictionary<string, EventScriptValue> Normalize(IReadOnlyDictionary<string, EventScriptValue>? values)
        => values is null || values.Count == 0 ? Empty : values.ToDictionary(pair => pair.Key, pair => pair.Value ?? EventScriptValue.Nothing, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, EventScriptValue> FromClr(IReadOnlyDictionary<string, object?> values)
        => values.ToDictionary(pair => pair.Key, pair => EventScriptValue.FromClr(pair.Value), StringComparer.Ordinal);

    public static string CreateSignatureKey(IEnumerable<string> names)
        => string.Join("|", names.OrderBy(name => name, StringComparer.Ordinal));
}