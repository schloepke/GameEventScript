#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static StepH.GameEventScript.Types.EventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class EventScriptVector3Value : EventScriptValue
{
    public static readonly EventScriptVector3Value Zero = new(0m, 0m, 0m, null);

    public static EventScriptVector3Value EventScriptVector3(decimal x, decimal y, decimal z, EventScriptDecimalUnit? unit = null)
        => x == 0m && y == 0m && z == 0m && unit is null ? Zero : new EventScriptVector3Value(x, y, z, unit);

    private EventScriptVector3Value(decimal x, decimal y, decimal z, EventScriptDecimalUnit? unit)
    {
        X = x;
        Y = y;
        Z = z;
        Unit = unit;
        Components = CreateReadOnlyList(new[] { Decimal(x, unit), Decimal(y, unit), Decimal(z, unit) });
        Members = new ReadOnlyDictionary<string, EventScriptValue>(
            new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
            {
                ["x"] = Decimal(x, unit),
                ["y"] = Decimal(y, unit),
                ["z"] = Decimal(z, unit)
            });
    }

    public decimal X { get; }
    public decimal Y { get; }
    public decimal Z { get; }
    public EventScriptDecimalUnit? Unit { get; }
    public override EventScriptValueKind Kind => EventScriptValueKind.Vector3;

    private IReadOnlyList<EventScriptValue> Components { get; }
    private IReadOnlyDictionary<string, EventScriptValue> Members { get; }

    public override string AsText() => ToString();

    public override bool AsBoolean() => X != 0m || Y != 0m || Z != 0m;

    public override IReadOnlyList<EventScriptValue> AsList() => Components;

    public override IReadOnlyDictionary<string, EventScriptValue> AsDictionary() => Members;

    public override IEnumerable<EventScriptValue> AsEnumerable() => Components;

    public override bool Contains(EventScriptValue needle) => Components.Contains(needle);

    public override bool ContainsValue(EventScriptValue needle) => Contains(needle);

    public override bool TryGetDictionaryMember(string key, out EventScriptValue value)
    {
        if (Members.TryGetValue(key, out value))
        {
            return true;
        }

        value = Nothing;
        return false;
    }

    internal override bool TryConvertToBoolean(out EventScriptValue value)
    {
        value = Boolean(AsBoolean());
        return true;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToList(out EventScriptValue value)
    {
        value = List(Components);
        return true;
    }

    internal override bool TryConvertToDictionary(out EventScriptValue value)
    {
        value = Dictionary(Members);
        return true;
    }
}
