#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using StepH.Flow.EventScript;

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptMessageValue : EventScriptValue
{
    private readonly ReadOnlyDictionary<string, EventScriptValue> _members;

    private EventScriptMessageValue(EventScriptMessage message)
    {
        Value = message;

        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["name"] = EventScriptValue.Text(message.Name),
            ["arguments"] = EventScriptValue.Dictionary(message.Arguments),
            ["signatureid"] = EventScriptValue.Text(message.SignatureId)
        };

        _members = new ReadOnlyDictionary<string, EventScriptValue>(map);
    }

    public EventScriptMessage Value { get; }

    public IReadOnlyDictionary<string, EventScriptValue> Members => _members;

    public override EventScriptValueType Type => EventScriptValueType.Message;

    public override IReadOnlyDictionary<string, EventScriptValue> AsDictionary() => _members;

    public override bool TryGetDictionaryMember(string key, out EventScriptValue value)
        => _members.TryGetValue(key, out value!);

    public static EventScriptMessageValue Create(EventScriptMessage? message)
        => new(message ?? EventScriptMessage.EmptyMessage);
}
