#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using StepH.Flow.EventScript;

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptMessageValue : EventScriptValue
{
    public static readonly EventScriptMessageValue Empty = new(EventScript.EventScriptMessage.EmptyMessage);

    public static EventScriptMessageValue EventScriptMessage(EventScriptMessage? message)
        => message == null || message.SignatureId == EventScript.EventScriptMessage.EmptyMessage.SignatureId ? Empty : new EventScriptMessageValue(message);

    private EventScriptMessageValue(EventScriptMessage message)
    {
        Value = message;

        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["name"] = Text(message.Name),
            ["arguments"] = Dictionary(message.Arguments),
            ["signatureid"] = Text(message.SignatureId)
        };

        _members = new ReadOnlyDictionary<string, EventScriptValue>(map);
    }

    private readonly ReadOnlyDictionary<string, EventScriptValue> _members;

    public EventScriptMessage Value { get; }

    public IReadOnlyDictionary<string, EventScriptValue> Members => _members;

    public override EventScriptValueKind Kind => EventScriptValueKind.Message;

    public override string AsText() => ToString();

    public override IReadOnlyDictionary<string, EventScriptValue> AsDictionary() => _members;

    public override bool TryGetDictionaryMember(string key, out EventScriptValue value) => _members.TryGetValue(key, out value);

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToDictionary(out EventScriptValue value)
    {
        value = Dictionary(AsDictionary());
        return true;
    }

}