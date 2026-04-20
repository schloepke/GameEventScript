#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Runtime;

public sealed class EventScriptSubscriptionContext
{
    private readonly Action<string, IReadOnlyDictionary<string, EventScriptValue>> _publish;
    private readonly Action<string, IReadOnlyDictionary<string, object?>> _publishClr;

    internal EventScriptSubscriptionContext(
        string message,
        IReadOnlyDictionary<string, EventScriptValue> arguments,
        Action<string, IReadOnlyDictionary<string, EventScriptValue>> publish,
        Action<string, IReadOnlyDictionary<string, object?>> publishClr)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Arguments = EventScriptArgumentMap.Normalize(arguments);
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        _publishClr = publishClr ?? throw new ArgumentNullException(nameof(publishClr));
    }

    public string Message { get; }

    public IReadOnlyDictionary<string, EventScriptValue> Arguments { get; }

    public void Publish(string message)
        => Publish(message, EventScriptArgumentMap.Empty);

    public void Publish(string message, IReadOnlyDictionary<string, EventScriptValue> args)
        => _publish(message, EventScriptArgumentMap.Normalize(args));

    public void PublishClr(string message, IReadOnlyDictionary<string, object?> args)
        => _publishClr(message, args);
}
