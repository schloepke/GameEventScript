using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.VirtualMachine;

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

        var builder = new GesVmValueMapBuilder(Definition.Fields.Count);
        foreach (var field in Definition.Fields)
        {
            if (!Definition.TryGetField(field.Name, Instance, out var sourceValue))
            {
                continue;
            }

            var value = new GesVmValue();
            value.BindArguments(sourceValue);
            builder.Set(field.Name, value);
        }

        _map = builder.ToMap();
        return _map;
    }
}
