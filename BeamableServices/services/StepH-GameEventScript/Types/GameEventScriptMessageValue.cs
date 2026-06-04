#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptMessageValue : GameEventScriptValue
{
    public static GameEventScriptMessageValue Create(GameEventScriptMessage message)
        => new(message ?? throw new ArgumentNullException(nameof(message)));

    private GameEventScriptMessageValue(GameEventScriptMessage message)
    {
        Value = message;

        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
        {
            ["name"] = GesText(message.Name),
            ["arguments"] = GesMap(message.Arguments),
            ["signature"] = GesText(message.SignatureId),
            ["tags"] = GesList(message.Tags.Select(GesTag))
        };

        _members = new ReadOnlyDictionary<string, GameEventScriptValue>(map);
    }

    private readonly ReadOnlyDictionary<string, GameEventScriptValue> _members;

    public GameEventScriptMessage Value { get; }

    public IReadOnlyDictionary<string, GameEventScriptValue> Members => _members;

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Message;

    public override string AsText() => ToString();

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsMap() => _members;

    public override bool TryGetMapMember(string key, out GameEventScriptValue value) => _members.TryGetValue(key, out value);

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(ToString());
        return true;
    }

    internal override bool TryConvertToMap(out GameEventScriptValue value)
    {
        value = GesMap(AsMap());
        return true;
    }

}
