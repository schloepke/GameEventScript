#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace StepH.Flow.EventScript.Types;

public enum EventScriptValueType
{
    Nothing,
    Tag,
    Text,
    Percentage,
    Decimal,
    Integer,
    Boolean,
    Optional,
    Iterator,
    Range,
    List,
    Dictionary,
    Set,
    Dice
}

public abstract class EventScriptValue : IComparable<EventScriptValue>, IEquatable<EventScriptValue>
{
    private const string HiddenTypeKey = "__type";
    private static readonly IComparer<EventScriptValue> StableComparerInstance = new StableEventScriptValueComparer();
    private static readonly EventScriptValue NothingInstance = EventScriptNothingValue.Instance;

    protected EventScriptValue()
    {
    }

    public abstract EventScriptValueType Type { get; }
    public static IComparer<EventScriptValue> StableComparer => StableComparerInstance;
    public static EventScriptValue Nothing => NothingInstance;

    public bool isNumber() => Type is EventScriptValueType.Decimal or EventScriptValueType.Integer;
    public bool isNothing() => Type == EventScriptValueType.Nothing;
    public bool isTag() => Type == EventScriptValueType.Tag;
    public bool isInteger() => Type == EventScriptValueType.Integer;
    public bool isText() => Type == EventScriptValueType.Text;
    public bool isPercentage() => Type == EventScriptValueType.Percentage;
    public bool isIterator() => Type == EventScriptValueType.Iterator;
    public bool isList() => Type == EventScriptValueType.List;
    public bool isDictionary() => Type == EventScriptValueType.Dictionary;
    public bool isOptional() => Type == EventScriptValueType.Optional;
    public bool isSet() => Type == EventScriptValueType.Set;
    public bool isDice() => Type == EventScriptValueType.Dice;
    public bool isRange() => Type == EventScriptValueType.Range;

    public virtual string AsText()
    {
        if (this is EventScriptNothingValue) return string.Empty;
        if (this is EventScriptTagValue tag) return tag.Value;
        if (this is EventScriptTextValue text) return text.Value;
        if (this is EventScriptPercentageValue percentage) return FormatPercentage(percentage.Ratio);
        if (TryConvertToText(out var converted) && converted.Type == EventScriptValueType.Text)
        {
            return ((EventScriptTextValue)converted).Value;
        }

        return string.Empty;
    }

    public virtual bool AsBoolean()
    {
        if (this is EventScriptNothingValue) return false;
        if (this is EventScriptBooleanValue boolean) return boolean.Value;
        if (TryConvertToBoolean(out var converted) && converted.Type == EventScriptValueType.Boolean)
        {
            return ((EventScriptBooleanValue)converted).Value;
        }

        return false;
    }

    public virtual long AsInteger()
    {
        if (this is EventScriptNothingValue) return 0;
        if (this is EventScriptIntegerValue integer) return integer.Value;
        if (TryConvertToInteger(out var converted) && converted.Type == EventScriptValueType.Integer)
        {
            return ((EventScriptIntegerValue)converted).Value;
        }

        return 0;
    }

    public virtual decimal AsNumber()
    {
        if (this is EventScriptNothingValue) return 0m;

        var result = this switch
        {
            EventScriptPercentageValue percentage => percentage.Ratio,
            EventScriptDecimalValue number => number.IsNaNValue
                ? 0m
                : number.IsInfinityValue
                    ? (number.IsNegativeInfinityValue ? decimal.MinValue : decimal.MaxValue)
                    : number.Value,
            EventScriptIntegerValue integer => integer.Value,
            EventScriptDiceValue dice => dice.Sum(),
            _ => (decimal?)null
        };

        if (result.HasValue)
        {
            return result.Value;
        }

        return TryConvertToNumber(out var converted) ? converted.AsNumber() : 0m;
    }

    public bool IsNaN() => this is EventScriptDecimalValue number && number.IsNaNValue;
    public bool IsInfinity() => this is EventScriptDecimalValue number && number.IsInfinityValue;
    public bool IsNegativeInfinity() => this is EventScriptDecimalValue number && number.IsNegativeInfinityValue;

    public int CompareTo(EventScriptValue? other)
    {
        if (other is null) return 1;
        return StableComparerInstance.Compare(this, other);
    }

    public virtual EventScriptOptionalValue AsOptional()
    {
        if (this is EventScriptNothingValue) return EventScriptOptionalValue.None();
        if (this is EventScriptOptionalValue optional) return optional;
        return EventScriptOptionalValue.Some(this);
    }

    public virtual IReadOnlyList<EventScriptValue> AsList()
    {
        switch (this)
        {
            case EventScriptNothingValue:
                return Array.Empty<EventScriptValue>();
            case EventScriptIteratorValue:
                return new ReadOnlyCollection<EventScriptValue>(AsEnumerable().ToArray());
            case EventScriptRangeValue:
                return new ReadOnlyCollection<EventScriptValue>(AsEnumerable().ToArray());
            case EventScriptListValue list:
                return list.Items;
            case EventScriptSetValue set:
                return new ReadOnlyCollection<EventScriptValue>(set.Items.OrderBy(x => x, StableComparerInstance).ToArray());
            case EventScriptDiceValue dice:
                return new ReadOnlyCollection<EventScriptValue>(dice.Rolls.Select(roll => Integer(roll)).ToArray());
            case EventScriptTextValue:
            case EventScriptTagValue:
                return new ReadOnlyCollection<EventScriptValue>(AsText().Select(ch => Text(ch.ToString())).ToArray());
        }

        if (TryConvertToList(out var converted) && converted.Type == EventScriptValueType.List)
        {
            return converted.AsList();
        }

        return Array.Empty<EventScriptValue>();
    }

    public virtual IReadOnlyDictionary<string, EventScriptValue> AsDictionary()
    {
        switch (this)
        {
            case EventScriptNothingValue:
                return EmptyDictionaryView.Instance;
            case EventScriptDictionaryValue dictionary:
                return dictionary.VisibleView;
        }

        if (TryConvertToDictionary(out var converted) && converted.Type == EventScriptValueType.Dictionary)
        {
            return converted.AsDictionary();
        }

        return EmptyDictionaryView.Instance;
    }

    public virtual ISet<EventScriptValue> AsSet()
    {
        if (this is EventScriptNothingValue)
        {
            return new SortedSet<EventScriptValue>(StableComparerInstance);
        }

        if (this is EventScriptSetValue set)
        {
            return new SortedSet<EventScriptValue>(set.Items, StableComparerInstance);
        }

        if (TryConvertToSet(out var converted) && converted.Type == EventScriptValueType.Set)
        {
            return converted.AsSet();
        }

        return new SortedSet<EventScriptValue>(StableComparerInstance);
    }

    public virtual EventScriptDiceValue AsDice()
    {
        switch (this)
        {
            case EventScriptNothingValue:
                return EventScriptDiceValue.Create(Array.Empty<int>());
            case EventScriptDiceValue dice:
                return dice;
        }

        if (TryConvertToDice(out var converted) && converted.Type == EventScriptValueType.Dice)
        {
            return (EventScriptDiceValue)converted;
        }

        return EventScriptDiceValue.Create(Array.Empty<int>());
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

    public virtual IEnumerable<EventScriptValue> AsEnumerable()
    {
        if (this is EventScriptNothingValue)
        {
            yield break;
        }

        if (this is EventScriptIteratorValue iterator)
        {
            foreach (var item in EnumerateIterator(iterator))
            {
                yield return item;
            }

            yield break;
        }

        if (this is EventScriptRangeValue range)
        {
            foreach (var item in EnumerateRange(range))
            {
                yield return item;
            }

            yield break;
        }

        if (this is EventScriptTextValue || this is EventScriptTagValue)
        {
            foreach (var ch in AsText())
            {
                yield return Text(ch.ToString());
            }

            yield break;
        }

        if (this is EventScriptListValue list)
        {
            foreach (var item in list.Items)
            {
                yield return item;
            }

            yield break;
        }

        if (this is EventScriptSetValue set)
        {
            foreach (var item in set.Items.OrderBy(x => x, StableComparerInstance))
            {
                yield return item;
            }

            yield break;
        }

        if (this is EventScriptDiceValue dice)
        {
            foreach (var roll in dice.Rolls)
            {
                yield return Integer(roll);
            }
        }
    }

    private IEnumerable<EventScriptValue> EnumerateIterator(EventScriptIteratorValue iterator)
    {
        if (iterator.Source.Type == EventScriptValueType.Optional)
        {
            var optional = iterator.Source.AsOptional();
            if (!optional.HasValue)
            {
                yield break;
            }

            foreach (var item in EnumerateIterator(EventScriptIteratorValue.Create(iterator.Mode, optional.Value)))
            {
                yield return item;
            }

            yield break;
        }

        if (iterator.Mode == EventScriptIteratorMode.Keys)
        {
            if (iterator.Source is not EventScriptDictionaryValue dictionarySource)
            {
                yield break;
            }

            foreach (var key in dictionarySource.VisibleView.Keys.OrderBy(key => key, StringComparer.Ordinal))
            {
                yield return Tag(key);
            }

            yield break;
        }

        if (iterator.Mode == EventScriptIteratorMode.Entries)
        {
            if (iterator.Source is not EventScriptDictionaryValue dictionarySource)
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

        switch (iterator.Source)
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

    private IEnumerable<EventScriptValue> EnumerateRange(EventScriptRangeValue range)
    {
        if (range.Step == 0)
        {
            yield break;
        }

        if (range.Step > 0)
        {
            for (var current = range.From; current <= range.To; current += range.Step)
            {
                yield return Integer(current);
            }

            yield break;
        }

        for (var current = range.From; current >= range.To; current += range.Step)
        {
            yield return Integer(current);
        }
    }

    public virtual bool TryGetDictionaryMember(string key, out EventScriptValue value)
    {
        if (this is not EventScriptDictionaryValue dictionary)
        {
            value = Nothing;
            return false;
        }

        if (IsHiddenKey(key))
        {
            value = Nothing;
            return false;
        }

        return dictionary.Storage.TryGetValue(key, out value!);
    }

    public bool TryGetCustomTypeName(out string typeName)
    {
        if (this is EventScriptDictionaryValue dictionary &&
            dictionary.Storage.TryGetValue(HiddenTypeKey, out var marker) &&
            marker.Type == EventScriptValueType.Tag)
        {
            typeName = marker.AsText();
            return true;
        }

        typeName = string.Empty;
        return false;
    }

    public string DescribeType()
    {
        return Type switch
        {
            EventScriptValueType.Nothing => "Nothing",
            EventScriptValueType.Tag => "Tag",
            EventScriptValueType.Text => "Text",
            EventScriptValueType.Percentage => "Percentage",
            EventScriptValueType.Decimal => "Decimal",
            EventScriptValueType.Integer => "Integer",
            EventScriptValueType.Boolean => "Boolean",
            EventScriptValueType.Optional => "Optional",
            EventScriptValueType.Iterator => "Iterator",
            EventScriptValueType.Range => "Range",
            EventScriptValueType.List => "List",
            EventScriptValueType.Dictionary => "Dictionary",
            EventScriptValueType.Set => "Set",
            EventScriptValueType.Dice => "Dice",
            _ => Type.ToString()
        };
    }

    public override string ToString()
    {
        return Type switch
        {
            EventScriptValueType.Nothing => "Nothing",
            EventScriptValueType.Tag => $":{AsText()}",
            EventScriptValueType.Text => AsText(),
            EventScriptValueType.Percentage => FormatPercentage(((EventScriptPercentageValue)this).Ratio),
            EventScriptValueType.Decimal => IsNaN()
                ? "NaN"
                : IsInfinity()
                    ? (IsNegativeInfinity() ? "-Infinity" : "Infinity")
                    : AsNumber().ToString(CultureInfo.InvariantCulture),
            EventScriptValueType.Integer => AsInteger().ToString(CultureInfo.InvariantCulture),
            EventScriptValueType.Boolean => AsBoolean().ToString(),
            EventScriptValueType.Optional => AsOptional().HasValue ? AsOptional().Value.ToString() : "Optional.None",
            EventScriptValueType.Iterator => $"iterator[{string.Join(", ", AsEnumerable().Select(x => x.ToString()))}]",
            EventScriptValueType.Range => $"range[{((EventScriptRangeValue)this).From} to {((EventScriptRangeValue)this).To} step {((EventScriptRangeValue)this).Step}]",
            EventScriptValueType.List => $"[{string.Join(", ", AsList().Select(x => x.ToString()))}]",
            EventScriptValueType.Set => $"set[{string.Join(", ", AsSet().Select(x => x.ToString()))}]",
            EventScriptValueType.Dictionary => $"dict[{string.Join(", ", AsDictionary().Select(x => $"{x.Key}: {x.Value}"))}]",
            EventScriptValueType.Dice => $"dice[{string.Join(", ", AsDice().Rolls)}]",
            _ => Type.ToString()
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

        if (Type != other.Type) return false;

        return Type switch
        {
            EventScriptValueType.Nothing => true,
            EventScriptValueType.Tag => AsText() == other.AsText(),
            EventScriptValueType.Text => AsText() == other.AsText(),
            EventScriptValueType.Decimal => AsNumber() == other.AsNumber(),
            EventScriptValueType.Integer => AsInteger() == other.AsInteger(),
            EventScriptValueType.Boolean => AsBoolean() == other.AsBoolean(),
            EventScriptValueType.Optional => EqualsOptional(AsOptional(), other.AsOptional()),
            EventScriptValueType.Iterator => AsEnumerable().SequenceEqual(other.AsEnumerable()),
            EventScriptValueType.Range => ((EventScriptRangeValue)this).From == ((EventScriptRangeValue)other).From &&
                                          ((EventScriptRangeValue)this).To == ((EventScriptRangeValue)other).To &&
                                          ((EventScriptRangeValue)this).Step == ((EventScriptRangeValue)other).Step,
            EventScriptValueType.List => AsList().SequenceEqual(other.AsList()),
            EventScriptValueType.Dictionary => EqualsDictionary(AsDictionary(), other.AsDictionary()),
            EventScriptValueType.Set => AsSet().SetEquals(other.AsSet()),
            EventScriptValueType.Dice => AsDice().Rolls.SequenceEqual(other.AsDice().Rolls),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is EventScriptValue other && Equals(other);

    public override int GetHashCode()
    {
        if (isNumber())
        {
            if (IsNaN()) return int.MinValue;
            if (IsInfinity()) return IsNegativeInfinity() ? int.MinValue + 1 : int.MaxValue;
            return AsNumber().GetHashCode();
        }

        var hash = new HashCode();
        hash.Add(Type);

        switch (Type)
        {
            case EventScriptValueType.Tag:
            case EventScriptValueType.Text:
                hash.Add(AsText(), StringComparer.Ordinal);
                break;
            case EventScriptValueType.Nothing:
                hash.Add(0);
                break;
            case EventScriptValueType.Boolean:
                hash.Add(AsBoolean());
                break;
            case EventScriptValueType.Optional:
            {
                var optional = AsOptional();
                hash.Add(optional.HasValue);
                if (optional.HasValue) hash.Add(optional.Value);
                break;
            }
            case EventScriptValueType.Iterator:
                foreach (var item in AsEnumerable()) hash.Add(item);
                break;
            case EventScriptValueType.Range:
            {
                var range = (EventScriptRangeValue)this;
                hash.Add(range.From);
                hash.Add(range.To);
                hash.Add(range.Step);
                break;
            }
            case EventScriptValueType.List:
                foreach (var item in AsList()) hash.Add(item);
                break;
            case EventScriptValueType.Dictionary:
                foreach (var pair in AsDictionary().OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    hash.Add(pair.Key, StringComparer.Ordinal);
                    hash.Add(pair.Value);
                }

                break;
            case EventScriptValueType.Set:
                foreach (var item in AsSet().OrderBy(x => x, StableComparerInstance)) hash.Add(item);
                break;
            case EventScriptValueType.Dice:
                foreach (var roll in AsDice().Rolls) hash.Add(roll);
                break;
        }

        return hash.ToHashCode();
    }

    public static EventScriptValue Text(string value) => EventScriptTextValue.Create(value);

    public static EventScriptValue Tag(string value) => EventScriptTagValue.Create(value);

    public static EventScriptValue Decimal(decimal value) => EventScriptDecimalValue.FromDecimal(value);
    public static EventScriptValue Percentage(decimal ratio) => EventScriptPercentageValue.FromRatio(ratio);

    public static EventScriptValue DecimalNaN() => EventScriptDecimalValue.NaN();
    public static EventScriptValue DecimalInfinity() => EventScriptDecimalValue.Infinity();
    public static EventScriptValue DecimalNegativeInfinity() => EventScriptDecimalValue.NegativeInfinity();

    public static EventScriptValue Integer(long value) => EventScriptIntegerValue.FromInteger(value);

    public static EventScriptValue Boolean(bool value) => EventScriptBooleanValue.FromBoolean(value);

    private static EventScriptValue Optional(EventScriptOptionalValue value) => value;

    public static EventScriptValue OptionalSome(EventScriptValue value) => Optional(EventScriptOptionalValue.Some(value));

    public static EventScriptValue OptionalNone() => Optional(EventScriptOptionalValue.None());

    public static EventScriptValue Iterator(EventScriptIteratorMode mode, EventScriptValue source)
        => EventScriptIteratorValue.Create(mode, RequireNotNull(source));

    public static EventScriptValue Range(long from, long to, long step = 1)
        => EventScriptRangeValue.Create(from, to, step);

    public static EventScriptValue Values(EventScriptValue source) => Iterator(EventScriptIteratorMode.Values, source);

    public static EventScriptValue Keys(EventScriptValue source) => Iterator(EventScriptIteratorMode.Keys, source);

    public static EventScriptValue Entries(EventScriptValue source) => Iterator(EventScriptIteratorMode.Entries, source);

    public static EventScriptValue List(IEnumerable<EventScriptValue> values)
    {
        if (values == null) values = Array.Empty<EventScriptValue>();
        return EventScriptListValue.Create(values.Select(RequireNotNull));
    }

    public static EventScriptValue Dictionary(IReadOnlyDictionary<string, EventScriptValue> values)
    {
        if (values == null)
        {
            values = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        }

        return EventScriptDictionaryValue.Create(values.ToDictionary(pair => pair.Key, pair => RequireNotNull(pair.Value), StringComparer.Ordinal));
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

        return EventScriptDictionaryValue.Create(map);
    }

    public static EventScriptValue Set(IEnumerable<EventScriptValue> values)
    {
        if (values == null) values = Array.Empty<EventScriptValue>();
        return EventScriptSetValue.Create(values.Select(RequireNotNull), StableComparerInstance);
    }

    public static EventScriptValue Dice(EventScriptDiceValue diceValue) => diceValue ?? EventScriptDiceValue.Create(Array.Empty<int>());

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

        if (value is EventScriptDiceValue dice)
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
                return Decimal((decimal)number);
            case float number:
                return EventScriptDecimalValue.FromFloat(number);
            case double number:
                return EventScriptDecimalValue.FromDouble(number);
            case decimal number:
                return Decimal(number);
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
    public static implicit operator EventScriptValue(decimal value) => Decimal(value);
    public static implicit operator EventScriptValue(float value) => EventScriptDecimalValue.FromFloat(value);
    public static implicit operator EventScriptValue(double value) => EventScriptDecimalValue.FromDouble(value);

    private static EventScriptValue RequireNotNull(EventScriptValue? value) => value ?? Nothing;

    private static int GetSortRank(EventScriptValue value)
    {
        if (value.isNumber()) return 1;

        return value.Type switch
        {
            EventScriptValueType.Nothing => 0,
            EventScriptValueType.Tag => 1,
            EventScriptValueType.Text => 2,
            EventScriptValueType.Percentage => 3,
            EventScriptValueType.Boolean => 4,
            EventScriptValueType.Optional => 5,
            EventScriptValueType.Iterator => 6,
            EventScriptValueType.Range => 7,
            EventScriptValueType.List => 8,
            EventScriptValueType.Dictionary => 9,
            EventScriptValueType.Set => 10,
            EventScriptValueType.Dice => 11,
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

            return left.Type switch
            {
                EventScriptValueType.Nothing => 0,
                EventScriptValueType.Tag => StringComparer.Ordinal.Compare(left.AsText(), right.AsText()),
                EventScriptValueType.Text => StringComparer.Ordinal.Compare(left.AsText(), right.AsText()),
                EventScriptValueType.Percentage => ((EventScriptPercentageValue)left).Ratio.CompareTo(((EventScriptPercentageValue)right).Ratio),
                EventScriptValueType.Boolean => left.AsBoolean().CompareTo(right.AsBoolean()),
                EventScriptValueType.Optional => CompareOptional(left.AsOptional(), right.AsOptional()),
                EventScriptValueType.Iterator => CompareSequence(left.AsEnumerable().ToArray(), right.AsEnumerable().ToArray()),
                EventScriptValueType.Range => CompareRange((EventScriptRangeValue)left, (EventScriptRangeValue)right),
                EventScriptValueType.List => CompareSequence(left.AsList(), right.AsList()),
                EventScriptValueType.Dictionary => CompareDictionary(left.AsDictionary(), right.AsDictionary()),
                EventScriptValueType.Set => CompareSequence(
                    left.AsSet().OrderBy(x => x, StableComparerInstance).ToArray(),
                    right.AsSet().OrderBy(x => x, StableComparerInstance).ToArray()),
                EventScriptValueType.Dice => CompareDice(left.AsDice(), right.AsDice()),
                _ => left.Type.CompareTo(right.Type)
            };
        }

        private static int CompareOptional(EventScriptOptionalValue left, EventScriptOptionalValue right)
        {
            if (!left.HasValue && !right.HasValue) return 0;
            if (!left.HasValue) return -1;
            if (!right.HasValue) return 1;
            return StableComparerInstance.Compare(left.Value, right.Value);
        }

        private static int CompareDice(EventScriptDiceValue left, EventScriptDiceValue right)
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

        private static int CompareRange(EventScriptRangeValue left, EventScriptRangeValue right)
        {
            var byFrom = left.From.CompareTo(right.From);
            if (byFrom != 0) return byFrom;

            var byTo = left.To.CompareTo(right.To);
            if (byTo != 0) return byTo;

            return left.Step.CompareTo(right.Step);
        }
    }

    private bool TryConvertToNumber(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToNumber(out var converted) ? converted : null, out value)) return true;

        switch (this)
        {
            case EventScriptNothingValue:
                value = Decimal(0m);
                return true;
            case EventScriptTagValue tag:
                if (string.Equals(tag.Value, "infinity", StringComparison.Ordinal))
                {
                    value = DecimalInfinity();
                    return true;
                }

                if (string.Equals(tag.Value, "negativeinfinity", StringComparison.Ordinal))
                {
                    value = DecimalNegativeInfinity();
                    return true;
                }

                if (string.Equals(tag.Value, "nan", StringComparison.Ordinal))
                {
                    value = DecimalNaN();
                    return true;
                }

                break;
            case EventScriptPercentageValue percentage:
                value = Decimal(percentage.Ratio);
                return true;
            case EventScriptDecimalValue:
                value = this;
                return true;
            case EventScriptIntegerValue integer:
                value = Decimal((decimal)integer.Value);
                return true;
            case EventScriptBooleanValue boolean:
                value = Decimal(boolean.Value ? 1m : 0m);
                return true;
            case EventScriptTextValue text:
                if (decimal.TryParse(text.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    value = Decimal(number);
                    return true;
                }

                break;
            case EventScriptDiceValue dice:
                value = Decimal(dice.Sum());
                return true;
            case EventScriptIteratorValue:
                value = Decimal((decimal)AsEnumerable().Count());
                return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToInteger(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToInteger(out var converted) ? converted : null, out value)) return true;

        switch (this)
        {
            case EventScriptNothingValue:
                value = Integer(0);
                return true;
            case EventScriptIntegerValue:
                value = this;
                return true;
            case EventScriptPercentageValue percentage:
                value = Integer(ToIntegerPercentage(percentage.Ratio));
                return true;
            case EventScriptDecimalValue:
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
                if (number >= long.MinValue && number <= long.MaxValue)
                {
                    value = Integer((long)number);
                    return true;
                }

                value = Integer(number < 0 ? long.MinValue : long.MaxValue);
                return true;
            }
            case EventScriptBooleanValue boolean:
                value = Integer(boolean.Value ? 1 : 0);
                return true;
            case EventScriptTextValue text:
            {
                if (long.TryParse(text.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    value = Integer(integer);
                    return true;
                }

                if (decimal.TryParse(text.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalNumber))
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
            case EventScriptDiceValue dice:
                value = Integer((long)dice.Sum());
                return true;
            case EventScriptIteratorValue:
                value = Integer(AsEnumerable().LongCount());
                return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToBoolean(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToBoolean(out var converted) ? converted : null, out value)) return true;

        switch (this)
        {
            case EventScriptNothingValue:
                value = Boolean(false);
                return true;
            case EventScriptBooleanValue:
                value = this;
                return true;
            case EventScriptIntegerValue integer:
                value = Boolean(integer.Value != 0);
                return true;
            case EventScriptDecimalValue:
                value = Boolean(!IsNaN() && AsNumber() != 0);
                return true;
            case EventScriptPercentageValue percentage:
                value = Boolean(percentage.Ratio != 0m);
                return true;
            case EventScriptTextValue text:
                if (bool.TryParse(text.Value, out var boolean))
                {
                    value = Boolean(boolean);
                    return true;
                }

                if (string.Equals(text.Value, "1", StringComparison.Ordinal))
                {
                    value = Boolean(true);
                    return true;
                }

                if (string.Equals(text.Value, "0", StringComparison.Ordinal))
                {
                    value = Boolean(false);
                    return true;
                }

                break;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToText(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToText(out var converted) ? converted : null, out value)) return true;

        switch (this)
        {
            case EventScriptNothingValue:
                value = Text(string.Empty);
                return true;
            case EventScriptTagValue tag:
                value = Text(tag.Value);
                return true;
            case EventScriptTextValue:
                value = this;
                return true;
            case EventScriptPercentageValue percentage:
                value = Text(FormatPercentage(percentage.Ratio));
                return true;
            case EventScriptDecimalValue:
            case EventScriptIntegerValue:
            case EventScriptBooleanValue:
            case EventScriptDiceValue:
            case EventScriptRangeValue:
            case EventScriptIteratorValue:
                value = Text(ToString());
                return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToList(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToList(out var converted) ? converted : null, out value)) return true;

        switch (this)
        {
            case EventScriptNothingValue:
                value = List(Array.Empty<EventScriptValue>());
                return true;
            case EventScriptListValue:
                value = this;
                return true;
            case EventScriptIteratorValue:
                value = List(AsEnumerable());
                return true;
            case EventScriptRangeValue:
                value = List(AsEnumerable());
                return true;
            case EventScriptSetValue:
                value = List(AsSet());
                return true;
            case EventScriptTextValue:
            case EventScriptTagValue:
                value = List(AsText().Select(ch => Text(ch.ToString())));
                return true;
            case EventScriptDiceValue dice:
                value = List(dice.Rolls.Select(roll => Integer(roll)));
                return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToDictionary(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToDictionary(out var converted) ? converted : null, out value)) return true;

        if (this is EventScriptDictionaryValue)
        {
            value = this;
            return true;
        }

        if (this is EventScriptNothingValue)
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

        switch (this)
        {
            case EventScriptNothingValue:
                value = Set(Array.Empty<EventScriptValue>());
                return true;
            case EventScriptSetValue:
                value = this;
                return true;
            case EventScriptListValue:
                value = Set(AsList());
                return true;
            case EventScriptIteratorValue:
                value = Set(AsEnumerable());
                return true;
            case EventScriptRangeValue:
                value = Set(AsEnumerable());
                return true;
        }

        value = default!;
        return false;
    }

    private bool TryConvertToDice(out EventScriptValue value)
    {
        if (TryConvertFromOptional(this, static source => source.TryConvertToDice(out var converted) ? converted : null, out value)) return true;

        if (this is EventScriptDiceValue)
        {
            value = this;
            return true;
        }

        if (this is EventScriptNothingValue)
        {
            value = Dice(EventScriptDiceValue.Create(Array.Empty<int>()));
            return true;
        }

        IEnumerable<EventScriptValue>? source = this switch
        {
            EventScriptListValue => AsList(),
            EventScriptSetValue => AsSet(),
            EventScriptIteratorValue => AsEnumerable(),
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

        value = Dice(EventScriptDiceValue.Create(rolls));
        return true;
    }

    private static bool TryConvertFromOptional(
        EventScriptValue source,
        Func<EventScriptValue, EventScriptValue?> converter,
        out EventScriptValue value)
    {
        if (source.Type != EventScriptValueType.Optional)
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

    private static bool EqualsOptional(EventScriptOptionalValue left, EventScriptOptionalValue right)
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
