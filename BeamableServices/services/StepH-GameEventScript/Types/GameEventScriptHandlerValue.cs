#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptHandlerValue : GameEventScriptValue
{
    public static readonly GameEventScriptHandlerValue Empty = new(GameEventScriptMessageSignature.Empty);

    private readonly ReadOnlyDictionary<string, GameEventScriptValue> _members;

    private GameEventScriptHandlerValue(GameEventScriptMessageSignature signature)
    {
        Signature = signature;

        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
        {
            ["name"] = GesText(signature.Name),
            ["parameters"] = GesList(signature.Parameters.Select(parameter => GameEventScriptValueFactory.GesText(parameter))),
            ["signatureid"] = GesText(signature.SignatureId)
        };

        _members = new ReadOnlyDictionary<string, GameEventScriptValue>(map);
    }

    public GameEventScriptMessageSignature Signature { get; }

    public IReadOnlyDictionary<string, GameEventScriptValue> Members => _members;

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Handler;

    public override string AsText() => ToString();

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsDictionary() => _members;

    public override bool TryGetDictionaryMember(string key, out GameEventScriptValue value) => _members.TryGetValue(key, out value!);

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(ToString());
        return true;
    }

    internal override bool TryConvertToDictionary(out GameEventScriptValue value)
    {
        value = GseDictionary(AsDictionary());
        return true;
    }

    public static GameEventScriptHandlerValue Create(GameEventScriptMessageSignature? signature)
        => signature == null || signature.SignatureId == GameEventScriptMessageSignature.Empty.SignatureId ? Empty : new GameEventScriptHandlerValue(signature);
}
