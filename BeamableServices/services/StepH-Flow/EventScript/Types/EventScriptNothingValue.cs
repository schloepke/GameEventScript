#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
using System;
using System.Collections.Generic;

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptNothingValue : EventScriptValue
{
    public static readonly EventScriptNothingValue Instance = new();

    private EventScriptNothingValue()
    {
    }

    public override EventScriptValueKind Kind => EventScriptValueKind.Nothing;

    public override string AsText() => string.Empty;

    public override bool AsBoolean() => false;

    public override long AsInteger() => 0;

    public override decimal AsNumber() => 0m;

    public override EventScriptOptionalValue AsOptional() => EventScriptOptionalValue.None;

    public override IReadOnlyList<EventScriptValue> AsList() => [];

    public override IReadOnlyDictionary<string, EventScriptValue> AsDictionary() => EventScriptDictionaryValue.EmptyView;

    public override ISet<EventScriptValue> AsSet() => new SortedSet<EventScriptValue>(StableComparer);

    public override EventScriptDiceValue AsDice() => EventScriptDiceValue.Empty;

    internal override bool TryConvertToNumber(out EventScriptValue value)
    {
        value = Decimal(0m);
        return true;
    }

    internal override bool TryConvertToInteger(out EventScriptValue value)
    {
        value = Integer(0);
        return true;
    }

    internal override bool TryConvertToBoolean(out EventScriptValue value)
    {
        value = Boolean(false);
        return true;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(string.Empty);
        return true;
    }

    internal override bool TryConvertToList(out EventScriptValue value)
    {
        value = List([]);
        return true;
    }

    internal override bool TryConvertToDictionary(out EventScriptValue value)
    {
        value = Dictionary(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));
        return true;
    }

    internal override bool TryConvertToSet(out EventScriptValue value)
    {
        value = Set([]);
        return true;
    }

    internal override bool TryConvertToDice(out EventScriptValue value)
    {
        value = Dice(EventScriptDiceValue.Empty);
        return true;
    }
}
