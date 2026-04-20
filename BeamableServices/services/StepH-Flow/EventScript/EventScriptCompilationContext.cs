#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript;

public sealed record EventScriptExternalMessageBinding(
    string Message,
    IReadOnlyCollection<string> ParameterNames,
    Action<IReadOnlyDictionary<string, EventScriptValue>> Handler);

public class EventScriptCompilationException(string message) : Exception(message);



public sealed class EventScriptCompilationContext
{
    private readonly Dictionary<string, List<EventScriptExternalMessageBinding>> _externalBindings = new(StringComparer.Ordinal);

    public int MaxProcessedEventsPerRun { get; set; } = 64;

    [Obsolete("Use MaxProcessedEventsPerRun instead.")]
    public int MaxEmitDepth
    {
        get => MaxProcessedEventsPerRun;
        set => MaxProcessedEventsPerRun = value;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<EventScriptExternalMessageBinding>> ExternalBindings
        => _externalBindings.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<EventScriptExternalMessageBinding>)pair.Value.AsReadOnly(),
            StringComparer.Ordinal);

    public EventScriptCompilationContext BindExternal(
        string message,
        IReadOnlyCollection<string> parameterNames,
        Action<IReadOnlyDictionary<string, EventScriptValue>> handler)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        _ = parameterNames ?? throw new ArgumentNullException(nameof(parameterNames));
        _ = handler ?? throw new ArgumentNullException(nameof(handler));

        if (!_externalBindings.TryGetValue(message, out var bindings))
        {
            bindings = new List<EventScriptExternalMessageBinding>();
            _externalBindings[message] = bindings;
        }

        bindings.Add(new EventScriptExternalMessageBinding(message, parameterNames.ToArray(), handler));
        return this;
    }
}
