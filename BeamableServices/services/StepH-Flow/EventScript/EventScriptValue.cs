#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace StepH.Flow.EventScript;

public enum EventScriptValueKind
{
    Nothing,
    Tag,
    Text,
    Percentage,
    Number,
    Integer,
    Boolean,
    Optional,
    Iterator,
    List,
    Dictionary,
    Set,
    Dice
}

public enum EventScriptIteratorMode
{
    Values,
    Keys
}

public readonly record struct EventScriptIterator(EventScriptIteratorMode Mode, EventScriptValue Source);

public readonly record struct EventScriptOptional
{
    private static readonly EventScriptValue EmptyValue = EventScriptValue.Integer(0);
    private readonly EventScriptValue _value;

    private EventScriptOptional(bool hasValue, EventScriptValue value)
    {
        HasValue = hasValue;
        _value = value;
    }

    public bool HasValue { get; }
    public EventScriptValue Value => HasValue ? _value : EventScriptValue.Nothing;
    public static EventScriptOptional Of(EventScriptValue? value) => value == null ? None() : new EventScriptOptional(true, value);
    public static EventScriptOptional Some(EventScriptValue value) => Of(value);
    public static EventScriptOptional None() => new(false, EmptyValue);
}

public readonly record struct EventScriptDice
{
    private readonly int[] _rollsDescending;

    private EventScriptDice(int[] rollsDescending)
    {
        _rollsDescending = rollsDescending;
    }

    public IReadOnlyList<int> Rolls => Array.AsReadOnly(_rollsDescending.ToArray());

    public decimal Sum()
    {
        decimal sum = 0;
        foreach (var roll in _rollsDescending) sum += roll;
        return sum;
    }

    public EventScriptDice KeepHighest(int count)
        => count < 0 || count > _rollsDescending.Length ? Create(Array.Empty<int>()) : Create(_rollsDescending.Take(count));

    public EventScriptDice DropLowest(int count)
        => count < 0 || count > _rollsDescending.Length ? Create(Array.Empty<int>()) : Create(_rollsDescending.Take(_rollsDescending.Length - count));

    public static EventScriptDice Create(IEnumerable<int> rolls)
    {
        if (rolls == null) return new EventScriptDice([]);
        var values = rolls.ToArray();
        if (values.Any(roll => roll <= 0)) return new EventScriptDice([]);
        Array.Sort(values);
        Array.Reverse(values);
        return new EventScriptDice(values);
    }
}

public readonly record struct EventScriptNumber(decimal Value, bool IsNaN, bool IsInfinity, bool IsNegativeInfinity)
{
    public static EventScriptNumber FromDecimal(decimal value) => new(value, false, false, false);
    public static EventScriptNumber NaN() => new(0m, true, false, false);
    public static EventScriptNumber Infinity() => new(0m, false, true, false);
    public static EventScriptNumber NegativeInfinity() => new(0m, false, true, true);
}

public sealed record EventScriptValue : IComparable<EventScriptValue>
{
    private const string HiddenTypeKey = "__type";
    private static readonly IComparer<EventScriptValue> StableComparerInstance = new StableEventScriptValueComparer();
    private static readonly EventScriptValue NothingInstance = new(EventScriptValueKind.Nothing, string.Empty);
    private readonly object _value;

    private EventScriptValue(EventScriptValueKind kind, object value)
    {
        Kind = kind;
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public EventScriptValueKind Kind { get; }
    public static IComparer<EventScriptValue> StableComparer => StableComparerInstance;
    public static EventScriptValue Nothing => NothingInstance;

    public bool isNumber() => Kind is EventScriptValueKind.Number or EventScriptValueKind.Integer;
    public bool isNothing() => Kind == EventScriptValueKind.Nothing;
    public bool isTag() => Kind == EventScriptValueKind.Tag;
    public bool isInteger() => Kind == EventScriptValueKind.Integer;
    public bool isText() => Kind == EventScriptValueKind.Text;
    public bool isPercentage() => Kind == EventScriptValueKind.Percentage;
    public bool isIterator() => Kind == EventScriptValueKind.Iterator;
    public bool isList() => Kind == EventScriptValueKind.List;
    public bool isDictionary() => Kind == EventScriptValueKind.Dictionary;
    public bool isOptional() => Kind == EventScriptValueKind.Optional;
    public bool isSet() => Kind == EventScriptValueKind.Set;
    public bool isDice() => Kind == EventScriptValueKind.Dice;

    public string AsText()
    {
        if (Kind == EventScriptValueKind.Nothing) return string.Empty;
        if (Kind == EventScriptValueKind.Tag) return (string)_value;
        if (Kind == EventScriptValueKind.Text) return (string)_value;
        if (Kind == EventScriptValueKind.Percentage) return FormatPercentage((decimal)_value);
        if (TryConvertToText(out var converted) && converted.Kind == EventScriptValueKind.Text)
        {
            return (string)converted._value;
        }

        return string.Empty;
    }

    public bool AsBoolean()
    {
        if (Kind == EventScriptValueKind.Nothing) return false;
        if (Kind == EventScriptValueKind.Boolean) return (bool)_value;
        if (TryConvertToBoolean(out var converted) && converted.Kind == EventScriptValueKind.Boolean)
        {
            return (bool)converted._value;
        }

        return false;
    }

    public long AsInteger()
    {
        if (Kind == EventScriptValueKind.Nothing) return 0;
        if (Kind == EventScriptValueKind.Integer) return (long)_value;
        if (TryConvertToInteger(out var converted) && converted.Kind == EventScriptValueKind.Integer) return (long)converted._value;
        return 0;
    }

    public decimal AsNumber()
    {
        if (Kind == EventScriptValueKind.Nothing) return 0m;
        var result = Kind switch
        {
            EventScriptValueKind.Percentage => (decimal)_value,
            EventScriptValueKind.Number => ((EventScriptNumber)_value).IsNaN
                ? 0m
                : ((EventScriptNumber)_value).IsInfinity
                    ? (((EventScriptNumber)_value).IsNegativeInfinity ? decimal.MinValue : decimal.MaxValue)
                    : ((EventScriptNumber)_value).Value,
            EventScriptValueKind.Integer => (long)_value,
            EventScriptValueKind.Dice => AsDice().Sum(),
            _ => (decimal?)null
        };

        if (result.HasValue)
        {
            return result.Value;
        }

        return TryConvertToNumber(out var converted) ? converted.AsNumber() : 0m;
    }

    public bool IsNaN() => Kind == EventScriptValueKind.Number && ((EventScriptNumber)_value).IsNaN;
    public bool IsInfinity() => Kind == EventScriptValueKind.Number && ((EventScriptNumber)_value).IsInfinity;
    public bool IsNegativeInfinity() => Kind == EventScriptValueKind.Number && ((EventScriptNumber)_value).IsNegativeInfinity;

    public int CompareTo(EventScriptValue? other)
    {
        if (other is null) return 1;
        return StableComparerInstance.Compare(this, other);
    }

    public EventScriptOptional AsOptional()
    {
        if (Kind == EventScriptValueKind.Nothing) return EventScriptOptional.None();
        if (Kind == EventScriptValueKind.Optional) return (EventScriptOptional)_value;
        return EventScriptOptional.Some(this);
    }

    public IReadOnlyList<EventScriptValue> AsList()
    {
        switch (Kind)
        {
            case EventScriptValueKind.Nothing:
                return Array.AsReadOnly(Array.Empty<EventScriptValue>());
            case EventScriptValueKind.Iterator:
                return Array.AsReadOnly(AsEnumerable().ToArray());
            case EventScriptValueKind.List:
                return Array.AsReadOnly(((EventScriptValue[])_value).ToArray());
            case EventScriptValueKind.Set:
                return Array.AsReadOnly(((ISet<EventScriptValue>)_value).OrderBy(x => x, StableComparerInstance).ToArray());
            case EventScriptValueKind.Dice:
                return Array.AsReadOnly(AsDice().Rolls.Select(roll => Integer(roll)).ToArray());
            case EventScriptValueKind.Text:
                return Array.AsReadOnly(AsText().Select(ch => Text(ch.ToString())).ToArray());
        }

        if (TryConvertToList(out var converted) && converted.Kind == EventScriptValueKind.List)
        {
            return converted.AsList();
        }

        return Array.AsReadOnly(Array.Empty<EventScriptValue>());
    }

    public IReadOnlyDictionary<string, EventScriptValue> AsDictionary()
    {
        switch (Kind)
        {
            case EventScriptValueKind.Nothing:
                return new ReadOnlyDictionary<string, EventScriptValue>(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));
            case EventScriptValueKind.Dictionary:
            {
                var copy = new Dictionary<string, EventScriptValue>(
                    ((IReadOnlyDictionary<string, EventScriptValue>)_value)
                        .Where(pair => !IsHiddenKey(pair.Key)),
                    StringComparer.Ordinal);
                return new ReadOnlyDictionary<string, EventScriptValue>(copy);
            }
        }

        if (TryConvertToDictionary(out var converted) && converted.Kind == EventScriptValueKind.Dictionary)
        {
            return converted.AsDictionary();
        }

        return new ReadOnlyDictionary<string, EventScriptValue>(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));
    }

    public ISet<EventScriptValue> AsSet()
    {
        if (Kind == EventScriptValueKind.Nothing)
        {
            return new SortedSet<EventScriptValue>(StableComparerInstance);
        }

        if (Kind == EventScriptValueKind.Set)
        {
            return new SortedSet<EventScriptValue>((ISet<EventScriptValue>)_value, StableComparerInstance);
        }

        if (TryConvertToSet(out var converted) && converted.Kind == EventScriptValueKind.Set)
        {
            return converted.AsSet();
        }

        return new SortedSet<EventScriptValue>(StableComparerInstance);
    }

    public EventScriptDice AsDice()
    {
        switch (Kind)
        {
            case EventScriptValueKind.Nothing:
                return EventScriptDice.Create(Array.Empty<int>());
            case EventScriptValueKind.Dice:
                return (EventScriptDice)_value;
        }

        if (TryConvertToDice(out var converted) && converted.Kind == EventScriptValueKind.Dice)
        {
            return (EventScriptDice)converted._value;
        }

        return EventScriptDice.Create(Array.Empty<int>());
    }

    public EventScriptValue ToNumberOptional()
        => TryConvertToNumber(out var value) ? OptionalSome(value) : OptionalNone();

    public EventScriptValue ToIntegerOptional()
        => TryConvertToInteger(out var value) ? OptionalSome(value) : OptionalNone();

    public EventScriptValue ToBooleanOptional()
        => TryConvertToBoolean(out var value) ? OptionalSome(value) : OptionalNone();

    public EventScriptValue ToTextOptional()
        => TryConvertToText(out var value) ? OptionalSome(value) : OptionalNone();

    public EventScriptValue ToListOptional()
        => TryConvertToList(out var value) ? OptionalSome(value) : OptionalNone();

    public EventScriptValue ToDictionaryOptional()
        => TryConvertToDictionary(out var value) ? OptionalSome(value) : OptionalNone();

    public EventScriptValue ToSetOptional()
        => TryConvertToSet(out var value) ? OptionalSome(value) : OptionalNone();

    public EventScriptValue ToDiceOptional()
        => TryConvertToDice(out var value) ? OptionalSome(value) : OptionalNone();

    public IEnumerable<EventScriptValue> AsEnumerable()
    {
        if (Kind == EventScriptValueKind.Nothing)
        {
            yield break;
        }

        if (Kind == EventScriptValueKind.Iterator)
        {
            foreach (var item in EnumerateIterator((EventScriptIterator)_value))
            {
                yield return item;
            }

            yield break;
        }

        if (Kind == EventScriptValueKind.Text)
        {
            foreach (var ch in AsText())
            {
                yield return Text(ch.ToString());
            }

            yield break;
        }

        if (Kind == EventScriptValueKind.List)
        {
            foreach (var item in (IReadOnlyList<EventScriptValue>)_value)
            {
                yield return item;
            }

            yield break;
        }

        if (Kind == EventScriptValueKind.Set)
        {
            foreach (var item in ((ISet<EventScriptValue>)_value).OrderBy(x => x, StableComparerInstance))
            {
                yield return item;
            }

            yield break;
        }

        if (Kind == EventScriptValueKind.Dice)
        {
            foreach (var roll in AsDice().Rolls)
            {
                yield return Integer(roll);
            }

            yield break;
        }

        yield break;
    }

    private IEnumerable<EventScriptValue> EnumerateIterator(EventScriptIterator iterator)
    {
        if (iterator.Source.Kind == EventScriptValueKind.Optional)
        {
            var optional = iterator.Source.AsOptional();
            if (!optional.HasValue)
            {
                yield break;
            }

            foreach (var item in EnumerateIterator(new EventScriptIterator(iterator.Mode, optional.Value)))
            {
                yield return item;
            }

            yield break;
        }

        if (iterator.Mode == EventScriptIteratorMode.Keys)
        {
            if (iterator.Source.Kind != EventScriptValueKind.Dictionary)
            {
                yield break;
            }

            foreach (var key in iterator.Source.AsDictionary().Keys.OrderBy(key => key, StringComparer.Ordinal))
            {
                yield return Tag(key);
            }

            yield break;
        }

        switch (iterator.Source.Kind)
        {
            case EventScriptValueKind.Nothing:
                yield break;

            case EventScriptValueKind.Dictionary:
                foreach (var value in iterator.Source.AsDictionary().Values)
                {
                    yield return value;
                }

                yield break;

            case EventScriptValueKind.List:
            case EventScriptValueKind.Set:
            case EventScriptValueKind.Dice:
            case EventScriptValueKind.Iterator:
                foreach (var item in iterator.Source.AsEnumerable())
                {
                    yield return item;
                }

                yield break;

            default:
                yield return iterator.Source;
                yield break;
        }
    }

    public bool TryGetDictionaryMember(string key, out EventScriptValue value)
    {
        if (Kind != EventScriptValueKind.Dictionary)
        {
            value = Nothing;
            return false;
        }

        if (IsHiddenKey(key))
        {
            value = Nothing;
            return false;
        }

        return ((IReadOnlyDictionary<string, EventScriptValue>)_value).TryGetValue(key, out value!);
    }

    public bool TryGetCustomTypeName(out string typeName)
    {
        if (Kind == EventScriptValueKind.Dictionary &&
            ((IReadOnlyDictionary<string, EventScriptValue>)_value).TryGetValue(HiddenTypeKey, out var marker) &&
            marker.Kind == EventScriptValueKind.Tag)
        {
            typeName = marker.AsText();
            return true;
        }

        typeName = string.Empty;
        return false;
    }

    public string DescribeType()
    {
        return Kind switch
        {
            EventScriptValueKind.Nothing => "Nothing",
            EventScriptValueKind.Tag => "Tag",
            EventScriptValueKind.Text => "Text",
            EventScriptValueKind.Percentage => "Percentage",
            EventScriptValueKind.Number => "Decimal",
            EventScriptValueKind.Integer => "Integer",
            EventScriptValueKind.Boolean => "Boolean",
            EventScriptValueKind.Optional => "Optional",
            EventScriptValueKind.Iterator => "Iterator",
            EventScriptValueKind.List => "List",
            EventScriptValueKind.Dictionary => "Dictionary",
            EventScriptValueKind.Set => "Set",
            EventScriptValueKind.Dice => "Dice",
            _ => Kind.ToString()
        };
    }

    public override string ToString()
    {
        return Kind switch
        {
            EventScriptValueKind.Nothing => "Nothing",
            EventScriptValueKind.Tag => $":{AsText()}",
            EventScriptValueKind.Text => AsText(),
            EventScriptValueKind.Percentage => FormatPercentage((decimal)_value),
            EventScriptValueKind.Number => IsNaN()
                ? "NaN"
                : IsInfinity()
                    ? (IsNegativeInfinity() ? "-Infinity" : "Infinity")
                    : AsNumber().ToString(CultureInfo.InvariantCulture),
            EventScriptValueKind.Integer => AsInteger().ToString(CultureInfo.InvariantCulture),
            EventScriptValueKind.Boolean => AsBoolean().ToString(),
            EventScriptValueKind.Optional => AsOptional().HasValue ? AsOptional().Value.ToString() : "Optional.None",
            EventScriptValueKind.Iterator => $"iterator[{string.Join(", ", AsEnumerable().Select(x => x.ToString()))}]",
            EventScriptValueKind.List => $"[{string.Join(", ", AsList().Select(x => x.ToString()))}]",
            EventScriptValueKind.Set => $"set[{string.Join(", ", AsSet().Select(x => x.ToString()))}]",
            EventScriptValueKind.Dictionary => $"dict[{string.Join(", ", AsDictionary().Select(x => $"{x.Key}: {x.Value}"))}]",
            EventScriptValueKind.Dice => $"dice[{string.Join(", ", AsDice().Rolls)}]",
            _ => base.ToString() ?? Kind.ToString()
        };
    }

    public bool Equals(EventScriptValue? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        if (isNumber() && other.isNumber())
        {
            if (IsNaN() || other.IsNaN())
            {
                return IsNaN() && other.IsNaN();
            }

            if (IsInfinity() || other.IsInfinity())
            {
                return IsInfinity() &&
                       other.IsInfinity() &&
                       IsNegativeInfinity() == other.IsNegativeInfinity();
            }

            return AsNumber() == other.AsNumber();
        }

        if (Kind != other.Kind) return false;

        return Kind switch
        {
            EventScriptValueKind.Nothing => true,
            EventScriptValueKind.Tag => AsText() == other.AsText(),
            EventScriptValueKind.Text => AsText() == other.AsText(),
            EventScriptValueKind.Number => AsNumber() == other.AsNumber(),
            EventScriptValueKind.Integer => AsInteger() == other.AsInteger(),
            EventScriptValueKind.Boolean => AsBoolean() == other.AsBoolean(),
            EventScriptValueKind.Optional => EqualsOptional(AsOptional(), other.AsOptional()),
            EventScriptValueKind.Iterator => AsEnumerable().SequenceEqual(other.AsEnumerable()),
            EventScriptValueKind.List => AsList().SequenceEqual(other.AsList()),
            EventScriptValueKind.Dictionary => EqualsDictionary(AsDictionary(), other.AsDictionary()),
            EventScriptValueKind.Set => AsSet().SetEquals(other.AsSet()),
            EventScriptValueKind.Dice => AsDice().Rolls.SequenceEqual(other.AsDice().Rolls),
            _ => false
        };
    }

    public override int GetHashCode()
    {
        if (isNumber())
        {
            if (IsNaN()) return int.MinValue;
            if (IsInfinity()) return IsNegativeInfinity() ? int.MinValue + 1 : int.MaxValue;
            return AsNumber().GetHashCode();
        }

        var hash = new HashCode();
        hash.Add(Kind);

        switch (Kind)
        {
            case EventScriptValueKind.Tag:
                hash.Add(AsText(), StringComparer.Ordinal);
                break;
            case EventScriptValueKind.Text:
                hash.Add(AsText(), StringComparer.Ordinal);
                break;
            case EventScriptValueKind.Nothing:
                hash.Add(0);
                break;
            case EventScriptValueKind.Boolean:
                hash.Add(AsBoolean());
                break;
            case EventScriptValueKind.Optional:
            {
                var optional = AsOptional();
                hash.Add(optional.HasValue);
                if (optional.HasValue) hash.Add(optional.Value);
                break;
            }
            case EventScriptValueKind.Iterator:
                foreach (var item in AsEnumerable()) hash.Add(item);
                break;
            case EventScriptValueKind.List:
                foreach (var item in AsList()) hash.Add(item);
                break;
            case EventScriptValueKind.Dictionary:
                foreach (var pair in AsDictionary().OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    hash.Add(pair.Key, StringComparer.Ordinal);
                    hash.Add(pair.Value);
                }

                break;
            case EventScriptValueKind.Set:
                foreach (var item in AsSet().OrderBy(x => x, StableComparerInstance)) hash.Add(item);
                break;
            case EventScriptValueKind.Dice:
                foreach (var roll in AsDice().Rolls) hash.Add(roll);
                break;
        }

        return hash.ToHashCode();
    }

    public static EventScriptValue Text(string value)
    {
        if (value == null) value = string.Empty;
        return new EventScriptValue(EventScriptValueKind.Text, value);
    }

    public static EventScriptValue Tag(string value)
    {
        if (value == null) value = string.Empty;
        return new EventScriptValue(EventScriptValueKind.Tag, value);
    }

    public static EventScriptValue Decimal(decimal value) => Number(value);
    public static EventScriptValue Percentage(decimal ratio) => new(EventScriptValueKind.Percentage, ratio);

    public static EventScriptValue Number(decimal value) => new(EventScriptValueKind.Number, EventScriptNumber.FromDecimal(value));

    private static EventScriptValue Number(double value)
    {
        if (double.IsNaN(value)) return NumberNaN();
        if (double.IsPositiveInfinity(value)) return NumberInfinity();
        if (double.IsNegativeInfinity(value)) return NumberNegativeInfinity();
        return Number((decimal)value);
    }

    public static EventScriptValue NumberNaN() => new(EventScriptValueKind.Number, EventScriptNumber.NaN());
    public static EventScriptValue NumberInfinity() => new(EventScriptValueKind.Number, EventScriptNumber.Infinity());
    public static EventScriptValue NumberNegativeInfinity() => new(EventScriptValueKind.Number, EventScriptNumber.NegativeInfinity());
    public static EventScriptValue DecimalNaN() => NumberNaN();
    public static EventScriptValue DecimalInfinity() => NumberInfinity();
    public static EventScriptValue DecimalNegativeInfinity() => NumberNegativeInfinity();

    public static EventScriptValue Integer(long value) => new(EventScriptValueKind.Integer, value);

    public static EventScriptValue Boolean(bool value) => new(EventScriptValueKind.Boolean, value);

    private static EventScriptValue Optional(EventScriptOptional value) => new(EventScriptValueKind.Optional, value);

    public static EventScriptValue OptionalSome(EventScriptValue value) => Optional(EventScriptOptional.Some(value));

    public static EventScriptValue OptionalNone() => Optional(EventScriptOptional.None());

    public static EventScriptValue Iterator(EventScriptIteratorMode mode, EventScriptValue source)
        => new(EventScriptValueKind.Iterator, new EventScriptIterator(mode, RequireNotNull(source)));

    public static EventScriptValue Values(EventScriptValue source) => Iterator(EventScriptIteratorMode.Values, source);

    public static EventScriptValue Keys(EventScriptValue source) => Iterator(EventScriptIteratorMode.Keys, source);

    public static EventScriptValue List(IEnumerable<EventScriptValue> values)
    {
        if (values == null) values = Array.Empty<EventScriptValue>();
        var list = values.Select(RequireNotNull).ToArray();
        return new EventScriptValue(EventScriptValueKind.List, list);
    }

    public static EventScriptValue Dictionary(IReadOnlyDictionary<string, EventScriptValue> values)
    {
        if (values == null)
        {
            values = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        }

        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (pair.Key == null) continue;
            map[pair.Key] = RequireNotNull(pair.Value);
        }

        return new EventScriptValue(EventScriptValueKind.Dictionary, map);
    }

    public static EventScriptValue CustomType(string typeName, IReadOnlyDictionary<string, EventScriptValue> values)
    {
        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            [HiddenTypeKey] = Tag(typeName)
        };

        if (values != null)
        {
            foreach (var pair in values)
            {
                if (pair.Key == null || IsHiddenKey(pair.Key)) continue;
                map[pair.Key] = RequireNotNull(pair.Value);
            }
        }

        return new EventScriptValue(EventScriptValueKind.Dictionary, map);
    }

    public static EventScriptValue Set(IEnumerable<EventScriptValue> values)
    {
        if (values == null) values = Array.Empty<EventScriptValue>();
        var set = new SortedSet<EventScriptValue>(values.Select(RequireNotNull), StableComparerInstance);
        return new EventScriptValue(EventScriptValueKind.Set, set);
    }

    public static EventScriptValue Dice(EventScriptDice dice) => new(EventScriptValueKind.Dice, dice);

    public static EventScriptValue FromClr(object? value)
    {
        if (value is null)
        {
            return OptionalNone();
        }

        if (value is EventScriptValue eventScriptValue)
        {
            return eventScriptValue;
        }

        if (value is EventScriptOptional optional)
        {
            return Optional(optional);
        }

        if (value is EventScriptDice dice)
        {
            return Dice(dice);
        }

        switch (value)
        {
            case string text:
                return Text(text);
            case bool boolean:
                return Boolean(boolean);
            case byte number:
                return Integer(number);
            case sbyte number:
                return Integer(number);
            case short number:
                return Integer(number);
            case ushort number:
                return Integer(number);
            case int number:
                return Integer(number);
            case uint number:
                return Integer(number);
            case long number:
                return Integer(number);
            case ulong number when number <= long.MaxValue:
                return Integer((long)number);
            case ulong number:
                return Number((decimal)number);
            case float number:
                return Number(number);
            case double number:
                return Number(number);
            case decimal number:
                return Number(number);
        }

        if (TryExtractStringDictionary(value, out var dictionaryEntries))
        {
            return Dictionary(dictionaryEntries.ToDictionary(x => x.Key, x => FromClr(x.Value), StringComparer.Ordinal));
        }

        if (value is ISet<EventScriptValue> typedSet)
        {
            return Set(typedSet);
        }

        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<EventScriptValue>();
            foreach (var item in enumerable)
            {
                list.Add(FromClr(item));
            }

            return List(list);
        }

        return Dictionary(ExtractObjectMembers(value));
    }

    public static IReadOnlyList<EventScriptValue> FromClrList(IEnumerable<object?> values)
    {
        if (values == null) return Array.Empty<EventScriptValue>();
        return values.Select(FromClr).ToArray();
    }

    public static implicit operator EventScriptValue(string value) => Text(value);
    public static implicit operator EventScriptValue(bool value) => Boolean(value);
    public static implicit operator EventScriptValue(int value) => Integer(value);
    public static implicit operator EventScriptValue(long value) => Integer(value);
    public static implicit operator EventScriptValue(decimal value) => Number(value);
    public static implicit operator EventScriptValue(float value) => Number((decimal)value);
    public static implicit operator EventScriptValue(double value) => Number(value);

    private static EventScriptValue RequireNotNull(EventScriptValue value)
    {
        return value ?? Nothing;
    }

    private static int GetSortRank(EventScriptValue value)
    {
        if (value.isNumber()) return 1;

        return value.Kind switch
        {
            EventScriptValueKind.Nothing => 0,
            EventScriptValueKind.Tag => 1,
            EventScriptValueKind.Text => 2,
            EventScriptValueKind.Percentage => 3,
            EventScriptValueKind.Boolean => 4,
            EventScriptValueKind.Optional => 5,
            EventScriptValueKind.Iterator => 6,
            EventScriptValueKind.List => 7,
            EventScriptValueKind.Dictionary => 8,
            EventScriptValueKind.Set => 9,
            EventScriptValueKind.Dice => 10,
            _ => 8
        };
    }

    private static int CompareSequence(IReadOnlyList<EventScriptValue> left, IReadOnlyList<EventScriptValue> right)
    {
        var byCount = left.Count.CompareTo(right.Count);
        if (byCount != 0) return byCount;

        for (var i = 0; i < left.Count; i++)
        {
            var byItem = StableComparerInstance.Compare(left[i], right[i]);
            if (byItem != 0) return byItem;
        }

        return 0;
    }

    private static int CompareDictionary(IReadOnlyDictionary<string, EventScriptValue> left, IReadOnlyDictionary<string, EventScriptValue> right)
    {
        var byCount = left.Count.CompareTo(right.Count);
        if (byCount != 0) return byCount;

        var leftPairs = left.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        var rightPairs = right.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();

        for (var i = 0; i < leftPairs.Length; i++)
        {
            var byKey = StringComparer.Ordinal.Compare(leftPairs[i].Key, rightPairs[i].Key);
            if (byKey != 0) return byKey;

            var byValue = StableComparerInstance.Compare(leftPairs[i].Value, rightPairs[i].Value);
            if (byValue != 0) return byValue;
        }

        return 0;
    }

    private sealed class StableEventScriptValueComparer : IComparer<EventScriptValue>
    {
        public int Compare(EventScriptValue? left, EventScriptValue? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;

            if (left.isNumber() && right.isNumber())
            {
                if (left.IsNaN() || right.IsNaN())
                {
                    if (left.IsNaN() && right.IsNaN()) return 0;
                    return left.IsNaN() ? -1 : 1;
                }

                if (left.IsInfinity() || right.IsInfinity())
                {
                    if (left.IsInfinity() && right.IsInfinity())
                    {
                        if (left.IsNegativeInfinity() == right.IsNegativeInfinity()) return 0;
                        return left.IsNegativeInfinity() ? -1 : 1;
                    }

                    return left.IsInfinity() ? (left.IsNegativeInfinity() ? -1 : 1) : (right.IsNegativeInfinity() ? 1 : -1);
                }

                return left.AsNumber().CompareTo(right.AsNumber());
            }

            var byRank = GetSortRank(left).CompareTo(GetSortRank(right));
            if (byRank != 0) return byRank;

            return left.Kind switch
            {
                EventScriptValueKind.Nothing => 0,
                EventScriptValueKind.Tag => StringComparer.Ordinal.Compare(left.AsText(), right.AsText()),
                EventScriptValueKind.Text => StringComparer.Ordinal.Compare(left.AsText(), right.AsText()),
                EventScriptValueKind.Percentage => ((decimal)left._value).CompareTo((decimal)right._value),
                EventScriptValueKind.Boolean => left.AsBoolean().CompareTo(right.AsBoolean()),
                EventScriptValueKind.Optional => CompareOptional(left.AsOptional(), right.AsOptional()),
                EventScriptValueKind.Iterator => CompareSequence(left.AsEnumerable().ToArray(), right.AsEnumerable().ToArray()),
                EventScriptValueKind.List => CompareSequence(left.AsList(), right.AsList()),
                EventScriptValueKind.Dictionary => CompareDictionary(left.AsDictionary(), right.AsDictionary()),
                EventScriptValueKind.Set => CompareSequence(
                    left.AsSet().OrderBy(x => x, StableComparerInstance).ToArray(),
                    right.AsSet().OrderBy(x => x, StableComparerInstance).ToArray()),
                EventScriptValueKind.Dice => CompareDice(left.AsDice(), right.AsDice()),
                _ => left.Kind.CompareTo(right.Kind)
            };
        }

        private static int CompareOptional(EventScriptOptional left, EventScriptOptional right)
        {
            if (!left.HasValue && !right.HasValue) return 0;
            if (!left.HasValue) return -1;
            if (!right.HasValue) return 1;
            return StableComparerInstance.Compare(left.Value, right.Value);
        }

        private static int CompareDice(EventScriptDice left, EventScriptDice right)
        {
            var leftRolls = left.Rolls;
            var rightRolls = right.Rolls;
            var byCount = leftRolls.Count.CompareTo(rightRolls.Count);
            if (byCount != 0) return byCount;

            for (var i = 0; i < leftRolls.Count; i++)
            {
                var byRoll = leftRolls[i].CompareTo(rightRolls[i]);
                if (byRoll != 0) return byRoll;
            }

            return 0;
        }
    }

    private bool TryConvertToNumber(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToNumber(out var converted) ? converted : null, out value)) return true;

        switch (Kind)
        {
            case EventScriptValueKind.Nothing:
                value = Number(0m);
                return true;
            case EventScriptValueKind.Tag:
                if (string.Equals(AsText(), "infinity", StringComparison.Ordinal))
                {
                    value = NumberInfinity();
                    return true;
                }

                if (string.Equals(AsText(), "negativeinfinity", StringComparison.Ordinal))
                {
                    value = NumberNegativeInfinity();
                    return true;
                }

                if (string.Equals(AsText(), "nan", StringComparison.Ordinal))
                {
                    value = NumberNaN();
                    return true;
                }

                break;
            case EventScriptValueKind.Percentage:
                value = Number((decimal)_value);
                return true;
            case EventScriptValueKind.Number:
                value = this;
                return true;
            case EventScriptValueKind.Integer:
                value = Number((decimal)AsInteger());
                return true;
            case EventScriptValueKind.Boolean:
                value = Number(AsBoolean() ? 1m : 0m);
                return true;
            case EventScriptValueKind.Text:
                if (decimal.TryParse(AsText(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    value = Number(number);
                    return true;
                }

                break;
            case EventScriptValueKind.Dice:
                value = Number(AsDice().Sum());
                return true;
            case EventScriptValueKind.Iterator:
                value = Number((decimal)AsEnumerable().Count());
                return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToInteger(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToInteger(out var converted) ? converted : null, out value)) return true;

        switch (Kind)
        {
            case EventScriptValueKind.Nothing:
                value = Integer(0);
                return true;
            case EventScriptValueKind.Integer:
                value = this;
                return true;
            case EventScriptValueKind.Percentage:
                value = Integer(ToIntegerPercentage((decimal)_value));
                return true;
            case EventScriptValueKind.Number:
            {
                if (IsNaN())
                {
                    value = Integer(0);
                    return true;
                }

                if (IsInfinity())
                {
                    value = Integer(IsNegativeInfinity() ? long.MinValue : long.MaxValue);
                    return true;
                }

                var number = AsNumber();
                number = decimal.Truncate(number);
                if (number >= long.MinValue &&
                    number <= long.MaxValue)
                {
                    value = Integer((long)number);
                    return true;
                }

                value = Integer(number < 0 ? long.MinValue : long.MaxValue);
                return true;
            }
            case EventScriptValueKind.Boolean:
                value = Integer(AsBoolean() ? 1 : 0);
                return true;
            case EventScriptValueKind.Text:
            {
                if (long.TryParse(AsText(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    value = Integer(integer);
                    return true;
                }

                if (decimal.TryParse(AsText(), NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalNumber))
                {
                    decimalNumber = decimal.Truncate(decimalNumber);
                    if (decimalNumber < long.MinValue)
                    {
                        value = Integer(long.MinValue);
                        return true;
                    }

                    if (decimalNumber > long.MaxValue)
                    {
                        value = Integer(long.MaxValue);
                        return true;
                    }

                    value = Integer((long)decimalNumber);
                    return true;
                }

                break;
            }
            case EventScriptValueKind.Dice:
                value = Integer((long)AsDice().Sum());
                return true;
            case EventScriptValueKind.Iterator:
                value = Integer(AsEnumerable().LongCount());
                return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToBoolean(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToBoolean(out var converted) ? converted : null, out value)) return true;

        switch (Kind)
        {
            case EventScriptValueKind.Nothing:
                value = Boolean(false);
                return true;
            case EventScriptValueKind.Boolean:
                value = this;
                return true;
            case EventScriptValueKind.Integer:
                value = Boolean(AsInteger() != 0);
                return true;
            case EventScriptValueKind.Number:
                value = Boolean(!IsNaN() && AsNumber() != 0);
                return true;
            case EventScriptValueKind.Percentage:
                value = Boolean((decimal)_value != 0m);
                return true;
            case EventScriptValueKind.Text:
            {
                var text = AsText();
                if (bool.TryParse(text, out var boolean))
                {
                    value = Boolean(boolean);
                    return true;
                }

                if (string.Equals(text, "1", StringComparison.Ordinal))
                {
                    value = Boolean(true);
                    return true;
                }

                if (string.Equals(text, "0", StringComparison.Ordinal))
                {
                    value = Boolean(false);
                    return true;
                }

                break;
            }
        }

        value = default!;
        return false;
    }

    private bool TryConvertToText(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToText(out var converted) ? converted : null, out value)) return true;

        switch (Kind)
        {
            case EventScriptValueKind.Nothing:
                value = Text(string.Empty);
                return true;
            case EventScriptValueKind.Tag:
                value = Text(AsText());
                return true;
            case EventScriptValueKind.Text:
                value = this;
                return true;
            case EventScriptValueKind.Percentage:
                value = Text(FormatPercentage((decimal)_value));
                return true;
            case EventScriptValueKind.Number:
                value = Text(ToString());
                return true;
            case EventScriptValueKind.Integer:
                value = Text(AsInteger().ToString(CultureInfo.InvariantCulture));
                return true;
            case EventScriptValueKind.Boolean:
                value = Text(AsBoolean().ToString());
                return true;
            case EventScriptValueKind.Dice:
                value = Text(ToString());
                return true;
            case EventScriptValueKind.Iterator:
                value = Text(ToString());
                return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToList(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToList(out var converted) ? converted : null, out value)) return true;

        switch (Kind)
        {
            case EventScriptValueKind.Nothing:
                value = List(Array.Empty<EventScriptValue>());
                return true;
            case EventScriptValueKind.List:
                value = this;
                return true;
            case EventScriptValueKind.Iterator:
                value = List(AsEnumerable());
                return true;
            case EventScriptValueKind.Set:
                value = List(AsSet());
                return true;
            case EventScriptValueKind.Text:
                value = List(AsText().Select(ch => Text(ch.ToString())));
                return true;
            case EventScriptValueKind.Dice:
                value = List(AsDice().Rolls.Select(roll => Integer(roll)));
                return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToDictionary(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToDictionary(out var converted) ? converted : null, out value)) return true;

        if (Kind == EventScriptValueKind.Dictionary)
        {
            value = this;
            return true;
        }

        if (Kind == EventScriptValueKind.Nothing)
        {
            value = Dictionary(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));
            return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToSet(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToSet(out var converted) ? converted : null, out value)) return true;

        switch (Kind)
        {
            case EventScriptValueKind.Nothing:
                value = Set(Array.Empty<EventScriptValue>());
                return true;
            case EventScriptValueKind.Set:
                value = this;
                return true;
            case EventScriptValueKind.List:
                value = Set(AsList());
                return true;
            case EventScriptValueKind.Iterator:
                value = Set(AsEnumerable());
                return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToDice(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToDice(out var converted) ? converted : null, out value)) return true;

        if (Kind == EventScriptValueKind.Dice)
        {
            value = this;
            return true;
        }

        if (Kind == EventScriptValueKind.Nothing)
        {
            value = Dice(EventScriptDice.Create(Array.Empty<int>()));
            return true;
        }

        IEnumerable<EventScriptValue>? source = Kind switch
        {
            EventScriptValueKind.List => AsList(),
            EventScriptValueKind.Set => AsSet(),
            EventScriptValueKind.Iterator => AsEnumerable(),
            _ => null
        };

        if (source == null)
        {
            value = default!;
            return false;
        }

        var rolls = new List<int>();
        foreach (var item in source)
        {
            if (!item.TryConvertToInteger(out var integerValue))
            {
                value = default!;
                return false;
            }

            var integer = integerValue.AsInteger();
            if (integer <= 0 || integer > int.MaxValue)
            {
                value = default!;
                return false;
            }

            rolls.Add((int)integer);
        }

        value = Dice(EventScriptDice.Create(rolls));
        return true;
    }

    private static bool TryConvertFromOptional(
        EventScriptValue source,
        Func<EventScriptValue, EventScriptValue?> converter,
        out EventScriptValue value)
    {
        if (source.Kind != EventScriptValueKind.Optional)
        {
            value = default!;
            return false;
        }

        var optional = source.AsOptional();
        if (!optional.HasValue)
        {
            value = default!;
            return false;
        }

        var converted = converter(optional.Value);
        if (converted == null)
        {
            value = default!;
            return false;
        }

        value = converted;
        return true;
    }

    private static bool EqualsOptional(EventScriptOptional left, EventScriptOptional right)
    {
        if (left.HasValue != right.HasValue) return false;
        if (!left.HasValue) return true;
        return left.Value.Equals(right.Value);
    }

    private static bool EqualsDictionary(IReadOnlyDictionary<string, EventScriptValue> left, IReadOnlyDictionary<string, EventScriptValue> right)
    {
        if (left.Count != right.Count) return false;

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue)) return false;
            if (!pair.Value.Equals(rightValue)) return false;
        }

        return true;
    }

    private static bool TryExtractStringDictionary(object value, out IReadOnlyList<KeyValuePair<string, object?>> entries)
    {
        if (value is IDictionary genericDictionary)
        {
            var list = new List<KeyValuePair<string, object?>>();
            foreach (DictionaryEntry entry in genericDictionary)
            {
                if (entry.Key is not string key)
                {
                    continue;
                }

                list.Add(new KeyValuePair<string, object?>(key, entry.Value));
            }

            entries = list;
            return true;
        }

        foreach (var interfaceType in value.GetType().GetInterfaces())
        {
            if (!interfaceType.IsGenericType) continue;
            var genericType = interfaceType.GetGenericTypeDefinition();
            if (genericType != typeof(IDictionary<,>) && genericType != typeof(IReadOnlyDictionary<,>)) continue;
            if (interfaceType.GetGenericArguments()[0] != typeof(string)) continue;

            var result = new List<KeyValuePair<string, object?>>();
            var keyProperty = interfaceType.GetProperty("Keys");
            if (keyProperty == null) continue;
            if (keyProperty.GetValue(value) is not IEnumerable keys) continue;

            var tryGetValue = interfaceType.GetMethod("TryGetValue");
            if (tryGetValue == null) continue;

            foreach (var keyObject in keys)
            {
                if (keyObject is not string key)
                {
                    continue;
                }

                var parameters = new object?[] { key, null };
                var found = (bool)tryGetValue.Invoke(value, parameters)!;
                if (!found) continue;
                result.Add(new KeyValuePair<string, object?>(key, parameters[1]));
            }

            entries = result;
            return true;
        }

        entries = Array.Empty<KeyValuePair<string, object?>>();
        return false;
    }

    private static IReadOnlyDictionary<string, EventScriptValue> ExtractObjectMembers(object value)
    {
        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        var type = value.GetType();

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            if (!property.CanRead) continue;
            map[property.Name] = FromClr(property.GetValue(value));
        }

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            map[field.Name] = FromClr(field.GetValue(value));
        }

        return map;
    }

    private static bool IsHiddenKey(string key)
        => key != null && key.StartsWith("__", StringComparison.Ordinal);

    private static string FormatPercentage(decimal ratio)
        => $"{(ratio * 100m).ToString("0.############################", CultureInfo.InvariantCulture)}%";

    private static long ToIntegerPercentage(decimal ratio)
    {
        var percent = decimal.Truncate(ratio * 100m);
        if (percent < long.MinValue) return long.MinValue;
        if (percent > long.MaxValue) return long.MaxValue;
        return (long)percent;
    }
}
