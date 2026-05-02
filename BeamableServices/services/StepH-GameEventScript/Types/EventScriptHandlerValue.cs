#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static StepH.GameEventScript.Types.EventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class EventScriptHandlerValue : EventScriptValue
{
    public static readonly EventScriptHandlerValue Empty = new(EventScriptMessageSignature.Empty);

    private readonly ReadOnlyDictionary<string, EventScriptValue> _members;

    private EventScriptHandlerValue(EventScriptMessageSignature signature)
    {
        Signature = signature;

        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["name"] = Text(signature.Name),
            ["parameters"] = List(signature.Parameters.Select(parameter => EventScriptValueFactory.Text(parameter))),
            ["signatureid"] = Text(signature.SignatureId)
        };

        _members = new ReadOnlyDictionary<string, EventScriptValue>(map);
    }

    public EventScriptMessageSignature Signature { get; }

    public IReadOnlyDictionary<string, EventScriptValue> Members => _members;

    public override EventScriptValueKind Kind => EventScriptValueKind.Handler;

    public override string AsText() => ToString();

    public override IReadOnlyDictionary<string, EventScriptValue> AsDictionary() => _members;

    public override bool TryGetDictionaryMember(string key, out EventScriptValue value) => _members.TryGetValue(key, out value!);

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

    public static EventScriptHandlerValue EventScriptHandler(EventScriptMessageSignature? signature)
        => signature == null || signature.SignatureId == EventScriptMessageSignature.Empty.SignatureId ? Empty : new EventScriptHandlerValue(signature);
}
