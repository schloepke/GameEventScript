#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.ObjectModel;
using System.Linq;

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptListValue : EventScriptValue
{
    private EventScriptListValue(ReadOnlyCollection<EventScriptValue> items)
    {
        Items = items;
    }

    public ReadOnlyCollection<EventScriptValue> Items { get; }
    public override EventScriptValueType Type => EventScriptValueType.List;

    public static EventScriptListValue Create(System.Collections.Generic.IEnumerable<EventScriptValue>? values)
    {
        var list = (values ?? []).Select(value => value ?? EventScriptValue.Nothing).ToArray();
        return new EventScriptListValue(new ReadOnlyCollection<EventScriptValue>(list));
    }
}
