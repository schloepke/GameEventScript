#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.VirtualMachine;

namespace StepH.GameEventScript.Types;

internal sealed class GameEventScriptExternalObjectValue : GameEventScriptValue, IGameEventScriptCustomTypeValue, IGameEventScriptExternalObjectValue
{
    private IReadOnlyDictionary<string, GameEventScriptValue>? _dictionary;

    internal GameEventScriptExternalObjectValue(object instance, GameEventScriptExternalTypeDefinition definition)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public override GameEventScriptBytecodeTypeKind Kind => GameEventScriptBytecodeTypeKind.Map;

    public object Instance { get; }

    internal GameEventScriptExternalTypeDefinition Definition { get; }

    public string CustomTypeName => Definition.Name;

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsMap()
        => _dictionary ??= MaterializeDictionary();

    public override bool HasSemanticValue() => Definition.Fields.Count > 0;

    public override bool IsSemanticallyEmpty() => Definition.Fields.Count == 0;

    public override bool Contains(GameEventScriptValue needle)
        => Definition.Fields.Any(field => string.Equals(field.Name, needle.AsText(), StringComparison.Ordinal));

    public override bool ContainsValue(GameEventScriptValue needle)
        => AsMap().Values.Any(value => value.Equals(needle));

    public override bool TryGetMapMember(string key, out GameEventScriptValue value)
    {
        if (Definition.TryGetField(key, Instance, out var boxedValue))
        {
            value = ToGameEventScriptValue(boxedValue);
            return true;
        }

        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    private IReadOnlyDictionary<string, GameEventScriptValue> MaterializeDictionary()
    {
        if (Definition.Fields.Count == 0)
        {
            return GameEventScriptMapValue.EmptyView;
        }

        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        foreach (var field in Definition.Fields)
        {
            if (Definition.TryGetField(field.Name, Instance, out var value))
            {
                map[field.Name] = ToGameEventScriptValue(value);
            }
        }

        return new ReadOnlyDictionary<string, GameEventScriptValue>(map);
    }

    private static GameEventScriptValue ToGameEventScriptValue(GameEventScriptBoxedValue value)
    {
        ref readonly var vmValue = ref value.GetVmValue();
        return vmValue.ToGameEventScriptValue();
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
