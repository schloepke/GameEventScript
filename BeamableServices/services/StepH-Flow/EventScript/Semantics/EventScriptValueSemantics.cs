#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Semantics;

public static class EventScriptValueSemantics
{
    public static bool HasValue(EventScriptValue value)
    {
        if (value.isNothing())
        {
            return false;
        }

        return value.Type switch
        {
            EventScriptValueType.Optional => value.AsOptional().HasValue,
            EventScriptValueType.Iterator => value.AsEnumerable().Any(),
            EventScriptValueType.Range => value.AsEnumerable().Any(),
            EventScriptValueType.Text => value.AsText().Length > 0,
            EventScriptValueType.List => value.AsList().Count > 0,
            EventScriptValueType.Dictionary => value.AsDictionary().Count > 0,
            EventScriptValueType.Set => value.AsSet().Count > 0,
            EventScriptValueType.Dice => value.AsDice().Rolls.Count > 0,
            EventScriptValueType.Decimal => !value.IsNaN() && !value.IsInfinity(),
            _ => true
        };
    }

    public static bool IsEmpty(EventScriptValue value)
    {
        if (value.isNothing())
        {
            return true;
        }

        return value.Type switch
        {
            EventScriptValueType.Optional => !value.AsOptional().HasValue || IsEmpty(value.AsOptional().Value),
            EventScriptValueType.Iterator => !value.AsEnumerable().Any(),
            EventScriptValueType.Range => !value.AsEnumerable().Any(),
            EventScriptValueType.Text => value.AsText().Length == 0,
            EventScriptValueType.List => value.AsList().Count == 0,
            EventScriptValueType.Dictionary => value.AsDictionary().Count == 0,
            EventScriptValueType.Set => value.AsSet().Count == 0,
            EventScriptValueType.Dice => value.AsDice().Rolls.Count == 0,
            _ => false
        };
    }

    public static EventScriptValue Lookup(EventScriptValue target, EventScriptValue selector)
    {
        if (target.isNothing() || selector.isNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (!TryUnwrapOptional(selector, out var lookup))
        {
            return EventScriptValue.OptionalNone();
        }

        var key = lookup.AsText();
        if (!string.IsNullOrEmpty(key) && target.TryGetDictionaryMember(key, out var value))
        {
            return value;
        }

        if (target.Type is EventScriptValueType.Dictionary or EventScriptValueType.Message or EventScriptValueType.Handler)
        {
            return EventScriptValue.Nothing;
        }

        return target is EventScriptRangeValue range
            ? LookupRange(range, lookup)
            : LookupSequential(target.AsList(), lookup);
    }

    public static bool Contains(EventScriptValue haystack, EventScriptValue needle)
    {
        return haystack.Type switch
        {
            EventScriptValueType.Text => haystack.AsText().Contains(ToComparableText(needle), StringComparison.Ordinal),
            EventScriptValueType.Dictionary => haystack.AsDictionary().ContainsKey(needle.AsText()),
            EventScriptValueType.Range => haystack is EventScriptRangeValue range && ContainsRange(range, needle),
            EventScriptValueType.List or EventScriptValueType.Set or EventScriptValueType.Dice => haystack.AsList().Any(item => item.Equals(needle)),
            _ => false
        };
    }

    public static bool ContainsValue(EventScriptValue haystack, EventScriptValue needle)
    {
        if (haystack.Type != EventScriptValueType.Dictionary)
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

    private static EventScriptValue LookupSequential(IReadOnlyList<EventScriptValue> items, EventScriptValue unwrappedSelector)
    {
        var index = AsInt(unwrappedSelector);
        if (index <= 0 || index > items.Count)
        {
            return EventScriptValue.Nothing;
        }

        return items[index - 1];
    }

    private static EventScriptValue LookupRange(EventScriptRangeValue range, EventScriptValue unwrappedSelector)
    {
        var index = unwrappedSelector.AsInteger();
        if (index <= 0 || index > GetRangeLength(range))
        {
            return EventScriptValue.Nothing;
        }

        var value = (decimal)range.From + ((decimal)index - 1m) * range.Step;
        if (value < long.MinValue || value > long.MaxValue)
        {
            return EventScriptValue.Nothing;
        }

        return EventScriptValue.Integer((long)value);
    }

    private static bool ContainsRange(EventScriptRangeValue range, EventScriptValue needle)
    {
        if (!needle.isNumber())
        {
            return false;
        }

        var value = needle.AsInteger();
        if (!EventScriptValue.Integer(value).Equals(needle))
        {
            return false;
        }

        if (range.Step == 0)
        {
            return false;
        }

        if (range.Step > 0)
        {
            if (value < range.From || value > range.To)
            {
                return false;
            }
        }
        else if (value > range.From || value < range.To)
        {
            return false;
        }

        return ((decimal)value - range.From) % range.Step == 0m;
    }

    private static long GetRangeLength(EventScriptRangeValue range)
    {
        if (range.Step == 0)
        {
            return 0;
        }

        if (range.Step > 0)
        {
            if (range.From > range.To)
            {
                return 0;
            }

            return ClampRangeLength(((decimal)range.To - range.From) / range.Step);
        }

        if (range.From < range.To)
        {
            return 0;
        }

        return ClampRangeLength(((decimal)range.From - range.To) / -(decimal)range.Step);
    }

    private static long ClampRangeLength(decimal zeroBasedDistance)
    {
        var length = decimal.Floor(zeroBasedDistance) + 1m;
        if (length <= 0m)
        {
            return 0;
        }

        return length > long.MaxValue ? long.MaxValue : (long)length;
    }

    private static bool MatchSequenceBoundary(EventScriptValue value, EventScriptValue boundary, bool fromStart)
    {
        if (value.Type == EventScriptValueType.Text && boundary.Type == EventScriptValueType.Text)
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
        => value.Type is EventScriptValueType.List or EventScriptValueType.Dice or EventScriptValueType.Range;

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

        return value.Type switch
        {
            EventScriptValueType.Text => value.AsText(),
            EventScriptValueType.Decimal => value.ToString(),
            EventScriptValueType.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
            EventScriptValueType.Boolean => value.AsBoolean().ToString(),
            _ => value.ToString()
        };
    }
}
