using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Runtime.Values;

internal sealed class GesVmExternalObject(object instance, GameEventScriptExternalTypeDefinition definition)
{
    private GesVmValueMap? _map;

    internal object Instance { get; } = instance;

    internal GameEventScriptExternalTypeDefinition Definition { get; } = definition;

    internal string CustomTypeName => Definition.Name;

    internal GesVmValueMap ToMap()
    {
        if (_map is not null)
        {
            return _map;
        }

        var keys = new string[Definition.Fields.Count];
        var values = new GesVmValue[Definition.Fields.Count];
        var count = 0;
        foreach (var field in Definition.Fields)
        {
            if (!Definition.TryGetField(field.Name, Instance, out var sourceValue))
            {
                continue;
            }

            keys[count] = field.Name;
            values[count] = sourceValue.GetVmValue();
            count++;
        }

        _map = new GesVmValueMap(keys, values, count);
        return _map;
    }
}
