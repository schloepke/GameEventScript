#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StepH.Flow.EventScript.Semantics;

public static class EventScriptValueSemantics
{
    public static bool HasValue(EventScriptValue value)
    {
        if (value.isNothing())
        {
            return false;
        }

        return value.Kind switch
        {
            EventScriptValueKind.Optional => value.AsOptional().HasValue,
            EventScriptValueKind.Iterator => value.AsEnumerable().Any(),
            EventScriptValueKind.Text => value.AsText().Length > 0,
            EventScriptValueKind.List => value.AsList().Count > 0,
            EventScriptValueKind.Dictionary => value.AsDictionary().Count > 0,
            EventScriptValueKind.Set => value.AsSet().Count > 0,
            EventScriptValueKind.Dice => value.AsDice().Rolls.Count > 0,
            EventScriptValueKind.Number => !value.IsNaN() && !value.IsInfinity(),
            _ => true
        };
    }

    public static bool IsEmpty(EventScriptValue value)
    {
        if (value.isNothing())
        {
            return true;
        }

        return value.Kind switch
        {
            EventScriptValueKind.Optional => !value.AsOptional().HasValue || IsEmpty(value.AsOptional().Value),
            EventScriptValueKind.Iterator => !value.AsEnumerable().Any(),
            EventScriptValueKind.Text => value.AsText().Length == 0,
            EventScriptValueKind.List => value.AsList().Count == 0,
            EventScriptValueKind.Dictionary => value.AsDictionary().Count == 0,
            EventScriptValueKind.Set => value.AsSet().Count == 0,
            EventScriptValueKind.Dice => value.AsDice().Rolls.Count == 0,
            _ => false
        };
    }

    public static EventScriptValue Lookup(EventScriptValue target, EventScriptValue selector)
    {
        if (target.isNothing() || selector.isNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (target.Kind == EventScriptValueKind.Dictionary)
        {
            return LookupDictionary(target, selector);
        }

        return LookupSequential(target.AsList(), selector);
    }

    public static bool Contains(EventScriptValue haystack, EventScriptValue needle)
    {
        return haystack.Kind switch
        {
            EventScriptValueKind.Text => haystack.AsText().Contains(ToComparableText(needle), StringComparison.Ordinal),
            EventScriptValueKind.Dictionary => haystack.AsDictionary().ContainsKey(needle.AsText()),
            EventScriptValueKind.List or EventScriptValueKind.Set or EventScriptValueKind.Dice => haystack.AsList().Any(item => item.Equals(needle)),
            _ => false
        };
    }

    public static bool ContainsValue(EventScriptValue haystack, EventScriptValue needle)
    {
        if (haystack.Kind != EventScriptValueKind.Dictionary)
        {
            return false;
        }

        return haystack.AsDictionary().Values.Any(value => value.Equals(needle));
    }

    public static bool StartsWith(EventScriptValue value, EventScriptValue prefix)
        => MatchSequenceBoundary(value, prefix, fromStart: true);

    public static bool EndsWith(EventScriptValue value, EventScriptValue suffix)
        => MatchSequenceBoundary(value, suffix, fromStart: false);

    public static bool TryUnwrapOptional(EventScriptValue value, out EventScriptValue unwrapped)
    {
        if (!value.isOptional())
        {
            unwrapped = value;
            return true;
        }

        var optional = value.AsOptional();
        if (!optional.HasValue)
        {
            unwrapped = default!;
            return false;
        }

        unwrapped = optional.Value;
        return true;
    }

    private static EventScriptValue LookupDictionary(EventScriptValue target, EventScriptValue selector)
    {
        if (!TryUnwrapOptional(selector, out var lookup))
        {
            return EventScriptValue.OptionalNone();
        }

        var key = lookup.AsText();
        if (string.IsNullOrEmpty(key))
        {
            return EventScriptValue.Nothing;
        }

        return target.TryGetDictionaryMember(key, out var value)
            ? value
            : EventScriptValue.Nothing;
    }

    private static EventScriptValue LookupSequential(IReadOnlyList<EventScriptValue> items, EventScriptValue selector)
    {
        if (!TryUnwrapOptional(selector, out var unwrappedSelector))
        {
            return EventScriptValue.OptionalNone();
        }

        var index = AsInt(unwrappedSelector);
        if (index <= 0 || index > items.Count)
        {
            return EventScriptValue.Nothing;
        }

        return items[index - 1];
    }

    private static bool MatchSequenceBoundary(EventScriptValue value, EventScriptValue boundary, bool fromStart)
    {
        if (value.Kind == EventScriptValueKind.Text && boundary.Kind == EventScriptValueKind.Text)
        {
            return fromStart
                ? value.AsText().StartsWith(boundary.AsText(), StringComparison.Ordinal)
                : value.AsText().EndsWith(boundary.AsText(), StringComparison.Ordinal);
        }

        if (!IsSequential(value) || !IsSequential(boundary))
        {
            return false;
        }

        var valueItems = value.AsList();
        var boundaryItems = boundary.AsList();
        if (boundaryItems.Count > valueItems.Count)
        {
            return false;
        }

        var startIndex = fromStart ? 0 : valueItems.Count - boundaryItems.Count;
        for (var i = 0; i < boundaryItems.Count; i++)
        {
            if (!valueItems[startIndex + i].Equals(boundaryItems[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSequential(EventScriptValue value)
        => value.Kind is EventScriptValueKind.List or EventScriptValueKind.Dice;

    private static int AsInt(EventScriptValue value)
    {
        var integer = value.AsInteger();
        if (integer < int.MinValue || integer > int.MaxValue)
        {
            return integer < 0 ? int.MinValue : int.MaxValue;
        }

        return (int)integer;
    }

    private static string ToComparableText(EventScriptValue value)
    {
        if (value.isNothing())
        {
            return string.Empty;
        }

        return value.Kind switch
        {
            EventScriptValueKind.Text => value.AsText(),
            EventScriptValueKind.Number => value.ToString(),
            EventScriptValueKind.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
            EventScriptValueKind.Boolean => value.AsBoolean().ToString(),
            _ => value.ToString()
        };
    }
}
