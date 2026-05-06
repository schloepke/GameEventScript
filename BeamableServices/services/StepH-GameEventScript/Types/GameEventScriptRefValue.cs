#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptRefValue : GameEventScriptValue
{
    private readonly IReadOnlyDictionary<string, GameEventScriptValue> _dictionary;

    public static GameEventScriptRefValue Create(string typeName, string id)
        => new(NormalizeTypeName(typeName), NormalizeId(id));

    private GameEventScriptRefValue(string typeName, string id)
    {
        TypeName = typeName;
        Id = id;
        _dictionary = new ReadOnlyDictionary<string, GameEventScriptValue>(
            new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
            {
                ["type"] = GesTag(typeName),
                ["id"] = GesText(id)
            });
    }

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Ref;

    public string TypeName { get; }

    public string Id { get; }

    public override string AsText() => $"ref(:{TypeName}, {Id})";

    public override bool AsBoolean() => TypeName.Length > 0 && Id.Length > 0;

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsDictionary() => _dictionary;

    public override bool HasSemanticValue() => AsBoolean();

    public override bool IsSemanticallyEmpty() => !AsBoolean();

    public override bool Contains(GameEventScriptValue needle)
        => _dictionary.ContainsKey(needle.AsText());

    public override bool ContainsValue(GameEventScriptValue needle)
        => _dictionary.Values.Any(value => value.Equals(needle));

    public override bool TryGetDictionaryMember(string key, out GameEventScriptValue value)
        => _dictionary.TryGetValue(key, out value!);

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(AsText());
        return true;
    }

    private static string NormalizeTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Ref type name must not be null or whitespace.", nameof(typeName));
        }

        var normalized = typeName.Trim();
        if (normalized.StartsWith(":", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Ref type name must not be empty.", nameof(typeName));
        }

        return normalized;
    }

    private static string NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Ref id must not be null or whitespace.", nameof(id));
        }

        return id.Trim();
    }
}
