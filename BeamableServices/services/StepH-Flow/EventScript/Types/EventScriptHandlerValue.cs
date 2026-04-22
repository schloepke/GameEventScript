#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StepH.Flow.EventScript;

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptHandlerValue : EventScriptValue
{
    private readonly ReadOnlyDictionary<string, EventScriptValue> _members;

    private EventScriptHandlerValue(EventScriptMessageSignature signature)
    {
        Signature = signature;

        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["name"] = EventScriptValue.Text(signature.Name),
            ["parameters"] = EventScriptValue.List(signature.Parameters.Select(parameter => EventScriptValue.Text(parameter))),
            ["signatureid"] = EventScriptValue.Text(signature.SignatureId)
        };

        _members = new ReadOnlyDictionary<string, EventScriptValue>(map);
    }

    public EventScriptMessageSignature Signature { get; }

    public IReadOnlyDictionary<string, EventScriptValue> Members => _members;

    public override EventScriptValueType Type => EventScriptValueType.Handler;

    public override IReadOnlyDictionary<string, EventScriptValue> AsDictionary() => _members;

    public override bool TryGetDictionaryMember(string key, out EventScriptValue value)
        => _members.TryGetValue(key, out value!);

    public static EventScriptHandlerValue Create(EventScriptMessageSignature? signature)
        => new(signature ?? new EventScriptMessageSignature(string.Empty, []));
}
