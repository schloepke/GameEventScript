#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.Flow.EventScript.Types;

public enum EventScriptIteratorMode
{
    Values,
    Keys,
    Entries
}

public sealed class EventScriptIteratorValue : EventScriptValue
{
    public static readonly EventScriptIteratorValue Empty = new(EventScriptIteratorMode.Values, Nothing);

    public static EventScriptIteratorValue EventScriptIterator(EventScriptIteratorMode mode, EventScriptValue? source)
        => mode == EventScriptIteratorMode.Values && (source == null || source.IsNothing()) ? Empty : new EventScriptIteratorValue(mode, source ?? Nothing);

    private EventScriptIteratorValue(EventScriptIteratorMode mode, EventScriptValue source)
    {
        Mode = mode;
        Source = source;
    }

    public EventScriptIteratorMode Mode { get; }
    public EventScriptValue Source { get; }
    public override EventScriptValueKind Kind => EventScriptValueKind.Iterator;

    public override string AsText() => ToString();

    public override long AsInteger() => AsEnumerable().LongCount();

    public override decimal AsNumber() => AsEnumerable().Count();

    public override IReadOnlyList<EventScriptValue> AsList() => CreateReadOnlyList(AsEnumerable());

    public override ISet<EventScriptValue> AsSet() => TryConvertToSet(out var value) ? value.AsSet() : new SortedSet<EventScriptValue>(StableComparer);

    public override EventScriptDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : EventScriptDiceValue.Empty;

    public override IEnumerable<EventScriptValue> AsEnumerable()
    {
        if (Source.Kind == EventScriptValueKind.Optional)
        {
            var optional = Source.AsOptional();
            if (!optional.HasValue)
            {
                yield break;
            }

            foreach (var item in EventScriptIterator(Mode, optional.Value).AsEnumerable())
            {
                yield return item;
            }

            yield break;
        }

        if (Mode == EventScriptIteratorMode.Keys)
        {
            if (Source is not EventScriptDictionaryValue dictionarySource)
            {
                yield break;
            }

            foreach (var key in dictionarySource.VisibleView.Keys.OrderBy(key => key, StringComparer.Ordinal))
            {
                yield return Tag(key);
            }

            yield break;
        }

        if (Mode == EventScriptIteratorMode.Entries)
        {
            if (Source is not EventScriptDictionaryValue dictionarySource)
            {
                yield break;
            }

            foreach (var pair in dictionarySource.VisibleView.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                yield return Dictionary(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
                {
                    ["key"] = Tag(pair.Key),
                    ["value"] = pair.Value
                });
            }

            yield break;
        }

        switch (Source)
        {
            case EventScriptNothingValue:
                yield break;
            case EventScriptDictionaryValue dictionary:
                foreach (var value in dictionary.VisibleView.Values)
                {
                    yield return value;
                }

                yield break;
            case EventScriptListValue:
            case EventScriptSetValue:
            case EventScriptDiceValue:
            case EventScriptIteratorValue:
                foreach (var item in Source.AsEnumerable())
                {
                    yield return item;
                }

                yield break;
            default:
                yield return Source;
                yield break;
        }
    }

    internal override bool TryConvertToNumber(out EventScriptValue value)
    {
        value = Decimal(AsEnumerable().Count());
        return true;
    }

    internal override bool TryConvertToInteger(out EventScriptValue value)
    {
        value = Integer(AsEnumerable().LongCount());
        return true;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToList(out EventScriptValue value)
    {
        value = List(AsEnumerable());
        return true;
    }

    internal override bool TryConvertToSet(out EventScriptValue value)
    {
        value = Set(AsEnumerable());
        return true;
    }

    internal override bool TryConvertToDice(out EventScriptValue value) => TryConvertSequenceToDice(AsEnumerable(), out value);
    
}
