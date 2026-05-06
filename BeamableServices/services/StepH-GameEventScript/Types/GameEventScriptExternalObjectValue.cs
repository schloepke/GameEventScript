#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Types;

internal sealed class GameEventScriptExternalObjectValue : GameEventScriptValue, IGameEventScriptCustomTypeValue, IGameEventScriptExternalObjectValue
{
    private IReadOnlyDictionary<string, GameEventScriptValue>? _dictionary;

    internal GameEventScriptExternalObjectValue(object instance, GameEventScriptExternalTypeDefinition definition)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Dictionary;

    public object Instance { get; }

    internal GameEventScriptExternalTypeDefinition Definition { get; }

    public string CustomTypeName => Definition.Name;

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsDictionary()
        => _dictionary ??= MaterializeDictionary();

    public override bool HasSemanticValue() => Definition.Fields.Count > 0;

    public override bool IsSemanticallyEmpty() => Definition.Fields.Count == 0;

    public override bool Contains(GameEventScriptValue needle)
        => Definition.Fields.Any(field => string.Equals(field.Name, needle.AsText(), StringComparison.Ordinal));

    public override bool ContainsValue(GameEventScriptValue needle)
        => AsDictionary().Values.Any(value => value.Equals(needle));

    public override bool TryGetDictionaryMember(string key, out GameEventScriptValue value)
        => Definition.TryGetField(key, Instance, out value);

    private IReadOnlyDictionary<string, GameEventScriptValue> MaterializeDictionary()
    {
        if (Definition.Fields.Count == 0)
        {
            return GameEventScriptDictionaryValue.EmptyView;
        }

        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        foreach (var field in Definition.Fields)
        {
            if (Definition.TryGetField(field.Name, Instance, out var value))
            {
                map[field.Name] = value;
            }
        }

        return new ReadOnlyDictionary<string, GameEventScriptValue>(map);
    }
}

internal interface IGameEventScriptCustomTypeValue
{
    string CustomTypeName { get; }
}

internal interface IGameEventScriptExternalObjectValue
{
    object Instance { get; }
}
