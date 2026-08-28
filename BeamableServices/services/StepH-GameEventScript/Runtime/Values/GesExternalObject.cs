using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Runtime.Values;

internal sealed class GesExternalObject(object instance, GameEventScriptExternalTypeDefinition definition)
{
    private GesValueMap? _map;

    internal object Instance { get; } = instance;

    internal GameEventScriptExternalTypeDefinition Definition { get; } = definition;

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
            var sourceValue = Definition.GetField(field.Name, Instance);
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
