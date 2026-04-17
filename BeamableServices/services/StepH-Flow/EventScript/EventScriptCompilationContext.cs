#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.Flow.EventScript;

public sealed record EventScriptExternalMessageBinding(string Message, Action<IReadOnlyList<EventScriptValue>> Handler, int? ParameterCount = null);

public sealed class EventScriptCompilationException(string message) : Exception(message);

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

    public EventScriptCompilationContext BindExternal(string message, Action<IReadOnlyList<EventScriptValue>> handler, int? parameterCount = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        _ = handler ?? throw new ArgumentNullException(nameof(handler));

        if (!_externalBindings.TryGetValue(message, out var bindings))
        {
            bindings = new List<EventScriptExternalMessageBinding>();
            _externalBindings[message] = bindings;
        }

        bindings.Add(new EventScriptExternalMessageBinding(message, handler, parameterCount));
        return this;
    }
}
