using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Runtime.Values;

internal sealed class GesExternalValue
{
    private GesValueMap? _map;

    internal GesExternalValue(IGameEventScriptExternalValue value)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        Definition = value.Definition ?? throw new System.ArgumentException("External value does not provide a type definition.", nameof(value));
    }

    internal IGameEventScriptExternalValue Value { get; }

    internal GameEventScriptExternalTypeDefinition Definition { get; }

    internal string CustomTypeName => Definition.Name;

    internal GesValueMap ToMap()
    {
        if (_map is not null)
        {
            return _map;
        }

        var keys = new string[Definition.Fields.Count];
        var values = new GesValue[Definition.Fields.Count];
        var count = 0;
        foreach (var field in Definition.Fields)
        {
            var sourceValue = Value.GetField(field.Name);
            if (!sourceValue.HasValue)
            {
                continue;
            }

            keys[count] = field.Name;
            values[count] = sourceValue.Value;
            count++;
        }

        _map = new GesValueMap(keys, values, count);
        return _map;
    }
}
