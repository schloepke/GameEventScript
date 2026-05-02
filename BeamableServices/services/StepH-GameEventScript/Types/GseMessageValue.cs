#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseMessageValue : GseValue
{
    public static readonly GseMessageValue Empty = new(GameEventScript.GseMessage.EmptyMessage);

    public static GseMessageValue GseMessage(GseMessage? message)
        => message == null || message.SignatureId == GameEventScript.GseMessage.EmptyMessage.SignatureId ? Empty : new GseMessageValue(message);

    private GseMessageValue(GseMessage message)
    {
        Value = message;

        var map = new Dictionary<string, GseValue>(StringComparer.Ordinal)
        {
            ["name"] = Text(message.Name),
            ["arguments"] = Dictionary(message.Arguments),
            ["signatureid"] = Text(message.SignatureId)
        };

        _members = new ReadOnlyDictionary<string, GseValue>(map);
    }

    private readonly ReadOnlyDictionary<string, GseValue> _members;

    public GseMessage Value { get; }

    public IReadOnlyDictionary<string, GseValue> Members => _members;

    public override GseValueKind Kind => GseValueKind.Message;

    public override string AsText() => ToString();

    public override IReadOnlyDictionary<string, GseValue> AsDictionary() => _members;

    public override bool TryGetDictionaryMember(string key, out GseValue value) => _members.TryGetValue(key, out value);

    internal override bool TryConvertToText(out GseValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToDictionary(out GseValue value)
    {
        value = Dictionary(AsDictionary());
        return true;
    }

}