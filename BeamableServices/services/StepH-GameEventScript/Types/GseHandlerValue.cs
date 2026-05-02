#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseHandlerValue : GseValue
{
    public static readonly GseHandlerValue Empty = new(GseMessageSignature.Empty);

    private readonly ReadOnlyDictionary<string, GseValue> _members;

    private GseHandlerValue(GseMessageSignature signature)
    {
        Signature = signature;

        var map = new Dictionary<string, GseValue>(StringComparer.Ordinal)
        {
            ["name"] = Text(signature.Name),
            ["parameters"] = List(signature.Parameters.Select(parameter => GseValueFactory.Text(parameter))),
            ["signatureid"] = Text(signature.SignatureId)
        };

        _members = new ReadOnlyDictionary<string, GseValue>(map);
    }

    public GseMessageSignature Signature { get; }

    public IReadOnlyDictionary<string, GseValue> Members => _members;

    public override GseValueKind Kind => GseValueKind.Handler;

    public override string AsText() => ToString();

    public override IReadOnlyDictionary<string, GseValue> AsDictionary() => _members;

    public override bool TryGetDictionaryMember(string key, out GseValue value) => _members.TryGetValue(key, out value!);

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

    public static GseHandlerValue GseHandler(GseMessageSignature? signature)
        => signature == null || signature.SignatureId == GseMessageSignature.Empty.SignatureId ? Empty : new GseHandlerValue(signature);
}
