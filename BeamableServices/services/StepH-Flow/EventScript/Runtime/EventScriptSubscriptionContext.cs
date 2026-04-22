#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Types;
using static StepH.Flow.EventScript.EventScriptMessage;

namespace StepH.Flow.EventScript.Runtime;

public sealed class EventScriptSubscriptionContext
{
    private readonly Action<EventScriptMessage> _publisher;

    internal EventScriptSubscriptionContext(EventScriptMessage eventMessage, Action<EventScriptMessage> publish)
    {
        Event = eventMessage;
        _publisher = publish ?? throw new ArgumentNullException(nameof(publish));
    }

    public EventScriptMessage Event { get; }

    public string Message => Event.Name;

    public IReadOnlyDictionary<string, EventScriptValue> Arguments => Event.Arguments;

    public void Publish(string message, IReadOnlyDictionary<string, EventScriptValue> args) => Publish(Message(message, args));

    public void Publish(EventScriptMessage message) => _publisher(message);
}