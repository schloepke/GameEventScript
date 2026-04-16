#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;

namespace StepH.Flow.EventScript;

public sealed record EventScriptExternalMessageBinding(string Message, Action<IReadOnlyList<object?>> Handler, int? ParameterCount = null);

public sealed class EventScriptCompilationException(string message) : Exception(message);

public sealed class EventScriptCompilationContext
{
    private readonly Dictionary<string, EventScriptExternalMessageBinding> _externalBindings = new(StringComparer.Ordinal);

    public int MaxEmitDepth { get; set; } = 64;

    public IReadOnlyDictionary<string, EventScriptExternalMessageBinding> ExternalBindings => _externalBindings;

    public EventScriptCompilationContext BindExternal(string message, Action<IReadOnlyList<object?>> handler, int? parameterCount = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _externalBindings[message] = new EventScriptExternalMessageBinding(message, handler, parameterCount);
        return this;
    }
}
