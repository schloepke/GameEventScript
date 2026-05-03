#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptMessageValue : GameEventScriptValue
{
    public static readonly GameEventScriptMessageValue Empty = new(Api.GameEventScriptMessage.Empty);

    public static GameEventScriptMessageValue Create(GameEventScriptMessage? message)
        => message == null || message.SignatureId == Api.GameEventScriptMessage.Empty.SignatureId ? Empty : new GameEventScriptMessageValue(message);

    private GameEventScriptMessageValue(GameEventScriptMessage message)
    {
        Value = message;

        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
        {
            ["name"] = GesText(message.Name),
            ["arguments"] = GesDictionary(message.Arguments),
            ["signatureid"] = GesText(message.SignatureId)
        };

        _members = new ReadOnlyDictionary<string, GameEventScriptValue>(map);
    }

    private readonly ReadOnlyDictionary<string, GameEventScriptValue> _members;

    public GameEventScriptMessage Value { get; }

    public IReadOnlyDictionary<string, GameEventScriptValue> Members => _members;

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Message;

    public override string AsText() => ToString();

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsDictionary() => _members;

    public override bool TryGetDictionaryMember(string key, out GameEventScriptValue value) => _members.TryGetValue(key, out value);

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(ToString());
        return true;
    }

    internal override bool TryConvertToDictionary(out GameEventScriptValue value)
    {
        value = GesDictionary(AsDictionary());
        return true;
    }

}