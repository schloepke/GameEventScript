#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using StepH.Flow.EventScript;

namespace StepH.Flow.EventScript.Types;

public enum EventScriptValueKind
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
    Message,
    Handler,
    List,
    Dictionary,
    Set,
    Dice
}

public abstract class EventScriptValue : IComparable<EventScriptValue>, IEquatable<EventScriptValue>
{
    internal const string HiddenTypeKey = "__type";
    private static readonly IComparer<EventScriptValue> StableComparerInstance = new StableEventScriptValueComparer();
    private static readonly EventScriptValue NothingInstance = EventScriptNothingValue.Instance;

    protected EventScriptValue()
    {
    }

    public abstract EventScriptValueKind Kind { get; }
    public static IComparer<EventScriptValue> StableComparer => StableComparerInstance;
    public static EventScriptValue Nothing => NothingInstance;

    public bool IsNumber() => Kind is EventScriptValueKind.Decimal or EventScriptValueKind.Integer or EventScriptValueKind.Percentage;
    public bool IsNothing() => Kind == EventScriptValueKind.Nothing;
    public bool IsTag() => Kind == EventScriptValueKind.Tag;
    public bool IsInteger() => Kind == EventScriptValueKind.Integer;
    public bool IsText() => Kind == EventScriptValueKind.Text;
    public bool IsPercentage() => Kind == EventScriptValueKind.Percentage;
    public bool IsIterator() => Kind == EventScriptValueKind.Iterator;
    public bool IsList() => Kind == EventScriptValueKind.List;
    public bool IsDictionary() => Kind == EventScriptValueKind.Dictionary;
    public bool IsOptional() => Kind == EventScriptValueKind.Optional;
    public bool IsSet() => Kind == EventScriptValueKind.Set;
    public bool IsDice() => Kind == EventScriptValueKind.Dice;
    public bool IsRange() => Kind == EventScriptValueKind.Range;
    public bool IsMessage() => Kind == EventScriptValueKind.Message;
    public bool IsHandler() => Kind == EventScriptValueKind.Handler;

    public virtual string AsText() => string.Empty;

    public virtual bool AsBoolean() => false;

    public virtual long AsInteger() => 0;

    public virtual decimal AsNumber() => 0m;

    public bool IsNaN() => this is EventScriptDecimalValue { IsNaNValue: true };
    public bool IsInfinity() => this is EventScriptDecimalValue { IsInfinityValue: true };
    public bool IsNegativeInfinity() => this is EventScriptDecimalValue { IsNegativeInfinityValue: true };

    public int CompareTo(EventScriptValue? other) => other is null ? 1 : StableComparerInstance.Compare(this, other);

    public virtual EventScriptOptionalValue AsOptional() => EventScriptOptionalValue.EventScriptOptionalSome(this);

    public virtual IReadOnlyList<EventScriptValue> AsList() => [];

    public virtual IReadOnlyDictionary<string, EventScriptValue> AsDictionary() => EventScriptDictionaryValue.EmptyView;

    public virtual ISet<EventScriptValue> AsSet() => new SortedSet<EventScriptValue>(StableComparerInstance);

    public virtual EventScriptDiceValue AsDice() => EventScriptDiceValue.Empty;

    public virtual bool HasSemanticValue() => true;

    public virtual bool IsSemanticallyEmpty() => false;

    public virtual bool TryUnwrapOptional(out EventScriptValue unwrapped)
    {
        unwrapped = this;
        return true;
    }

    public EventScriptValue Lookup(EventScriptValue selector)
    {
        if (IsNothing() || selector.IsNothing())
        {
            return Nothing;
        }

        if (!selector.TryUnwrapOptional(out var lookup))
        {
            return EventScriptValueFactory.OptionalNone();
        }

        var key = lookup.AsText();
        if (!string.IsNullOrEmpty(key) && TryGetDictionaryMember(key, out var value))
        {
            return value;
        }

        return LookupCore(lookup);
    }

    public virtual bool Contains(EventScriptValue needle) => false;

    public virtual bool ContainsValue(EventScriptValue needle) => false;

    public virtual bool StartsWith(EventScriptValue prefix)
        => MatchSequenceBoundary(this, prefix, fromStart: true);

    public virtual bool EndsWith(EventScriptValue suffix)
        => MatchSequenceBoundary(this, suffix, fromStart: false);

    public virtual IEnumerable<EventScriptValue> AsEnumerable()
    {
        yield break;
    }

    public virtual bool TryGetDictionaryMember(string key, out EventScriptValue value)
    {
        value = Nothing;
        return false;
    }

    protected virtual EventScriptValue LookupCore(EventScriptValue selector)
    {
        if (Kind is EventScriptValueKind.Dictionary or EventScriptValueKind.Message or EventScriptValueKind.Handler)
        {
            return Nothing;
        }

        return LookupSequential(AsList(), selector);
    }

    public bool TryGetCustomTypeName(out string typeName)
    {
        if (this is EventScriptDictionaryValue dictionary && dictionary.Storage.TryGetValue(HiddenTypeKey, out var marker) && marker.Kind == EventScriptValueKind.Tag)
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
            EventScriptValueKind.Nothing => ":Nothing",
            EventScriptValueKind.Tag => ":Tag",
            EventScriptValueKind.Text => ":Text",
            EventScriptValueKind.Percentage => ":Percentage",
            EventScriptValueKind.Decimal => ":Decimal",
            EventScriptValueKind.Integer => ":Integer",
            EventScriptValueKind.Boolean => ":Boolean",
            EventScriptValueKind.Optional => ":Optional",
            EventScriptValueKind.Iterator => ":Iterator",
            EventScriptValueKind.Range => ":Range",
            EventScriptValueKind.Message => ":Message",
            EventScriptValueKind.Handler => ":Handler",
            EventScriptValueKind.List => ":List",
            EventScriptValueKind.Dictionary => ":Dictionary",
            EventScriptValueKind.Set => ":Set",
            EventScriptValueKind.Dice => ":Dice",
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
            EventScriptValueKind.Percentage => FormatPercentage(((EventScriptPercentageValue)this).Ratio),
            EventScriptValueKind.Decimal => IsNaN() ? "NaN" : IsInfinity() ? IsNegativeInfinity() ? "-Infinity" : "Infinity" : AsNumber().ToString(CultureInfo.InvariantCulture),
            EventScriptValueKind.Integer => AsInteger().ToString(CultureInfo.InvariantCulture),
            EventScriptValueKind.Boolean => AsBoolean().ToString(),
            EventScriptValueKind.Optional => AsOptional().HasValue ? AsOptional().Value.ToString() : "Optional.None",
            EventScriptValueKind.Iterator => $"iterator[{string.Join(", ", AsEnumerable().Select(x => x.ToString()))}]",
            EventScriptValueKind.Range => $"range[{((EventScriptRangeValue)this).From} to {((EventScriptRangeValue)this).To} step {((EventScriptRangeValue)this).Step}]",
            EventScriptValueKind.Message => ((EventScriptMessageValue)this).Value.ToString(),
            EventScriptValueKind.Handler => $"handler {((EventScriptHandlerValue)this).Signature.SignatureId}",
            EventScriptValueKind.List => $"[{string.Join(", ", AsList().Select(x => x.ToString()))}]",
            EventScriptValueKind.Set => $"set[{string.Join(", ", AsSet().Select(x => x.ToString()))}]",
            EventScriptValueKind.Dictionary => $"dict[{string.Join(", ", AsDictionary().Select(x => $"{x.Key}: {x.Value}"))}]",
            EventScriptValueKind.Dice => $"dice[{string.Join(", ", AsDice().Rolls)}]",
            _ => Kind.ToString()
        };
    }

    public bool Equals(EventScriptValue? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (IsNumber() && other.IsNumber())
        {
            if (IsNaN() || other.IsNaN())
            {
                return IsNaN() && other.IsNaN();
            }

            if (IsInfinity() || other.IsInfinity())
            {
                return IsInfinity() && other.IsInfinity() && IsNegativeInfinity() == other.IsNegativeInfinity();
            }

            return AsNumber() == other.AsNumber();
        }

        if (Kind != other.Kind) return false;
        return Kind switch
        {
            EventScriptValueKind.Nothing => true,
            EventScriptValueKind.Tag => AsText() == other.AsText(),
            EventScriptValueKind.Text => AsText() == other.AsText(),
            EventScriptValueKind.Percentage => ((EventScriptPercentageValue)this).Ratio == ((EventScriptPercentageValue)other).Ratio,
            EventScriptValueKind.Decimal => AsNumber() == other.AsNumber(),
            EventScriptValueKind.Integer => AsInteger() == other.AsInteger(),
            EventScriptValueKind.Boolean => AsBoolean() == other.AsBoolean(),
            EventScriptValueKind.Optional => EqualsOptional(AsOptional(), other.AsOptional()),
            EventScriptValueKind.Iterator => AsEnumerable().SequenceEqual(other.AsEnumerable()),
            EventScriptValueKind.Range => ((EventScriptRangeValue)this).From == ((EventScriptRangeValue)other).From &&
                                          ((EventScriptRangeValue)this).To == ((EventScriptRangeValue)other).To &&
                                          ((EventScriptRangeValue)this).Step == ((EventScriptRangeValue)other).Step,
            EventScriptValueKind.Message => ((EventScriptMessageValue)this).Value.SignatureId == ((EventScriptMessageValue)other).Value.SignatureId &&
                                            EqualsDictionary(((EventScriptMessageValue)this).Value.Arguments, ((EventScriptMessageValue)other).Value.Arguments),
            EventScriptValueKind.Handler => ((EventScriptHandlerValue)this).Signature.SignatureId == ((EventScriptHandlerValue)other).Signature.SignatureId,
            EventScriptValueKind.List => AsList().SequenceEqual(other.AsList()),
            EventScriptValueKind.Dictionary => EqualsDictionary(AsDictionary(), other.AsDictionary()),
            EventScriptValueKind.Set => AsSet().SetEquals(other.AsSet()),
            EventScriptValueKind.Dice => AsDice().Rolls.SequenceEqual(other.AsDice().Rolls),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is EventScriptValue other && Equals(other);

    public override int GetHashCode()
    {
        if (IsNumber())
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
            case EventScriptValueKind.Text:
                hash.Add(AsText(), StringComparer.Ordinal);
                break;
            case EventScriptValueKind.Nothing:
                hash.Add(0);
                break;
            case EventScriptValueKind.Boolean:
                hash.Add(AsBoolean());
                break;
            case EventScriptValueKind.Percentage:
                hash.Add(((EventScriptPercentageValue)this).Ratio);
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
            case EventScriptValueKind.Range:
            {
                var range = (EventScriptRangeValue)this;
                hash.Add(range.From);
                hash.Add(range.To);
                hash.Add(range.Step);
                break;
            }
            case EventScriptValueKind.Message:
                hash.Add(((EventScriptMessageValue)this).Value.SignatureId, StringComparer.Ordinal);
                foreach (var pair in ((EventScriptMessageValue)this).Value.Arguments.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    hash.Add(pair.Key, StringComparer.Ordinal);
                    hash.Add(pair.Value);
                }

                break;
            case EventScriptValueKind.Handler:
                hash.Add(((EventScriptHandlerValue)this).Signature.SignatureId, StringComparer.Ordinal);
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
            default:
                throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown EventScript value kind.");
        }

        return hash.ToHashCode();
    }

    public static implicit operator EventScriptValue(string value) => EventScriptValueFactory.Text(value);
    public static implicit operator EventScriptValue(bool value) => EventScriptValueFactory.Boolean(value);
    public static implicit operator EventScriptValue(int value) => EventScriptValueFactory.Integer(value);
    public static implicit operator EventScriptValue(long value) => EventScriptValueFactory.Integer(value);
    public static implicit operator EventScriptValue(decimal value) => EventScriptValueFactory.Decimal(value);
    public static implicit operator EventScriptValue(float value) => EventScriptDecimalValue.EventScriptDecimal(value);
    public static implicit operator EventScriptValue(double value) => EventScriptDecimalValue.EventScriptDecimal(value);

    private static EventScriptValue RequireNotNull(EventScriptValue? value) => value ?? Nothing;

    private static int GetSortRank(EventScriptValue value)
    {
        if (value.IsNumber()) return 1;

        return value.Kind switch
        {
            EventScriptValueKind.Nothing => 0,
            EventScriptValueKind.Tag => 1,
            EventScriptValueKind.Text => 2,
            EventScriptValueKind.Percentage => 3,
            EventScriptValueKind.Boolean => 4,
            EventScriptValueKind.Optional => 5,
            EventScriptValueKind.Iterator => 6,
            EventScriptValueKind.Range => 7,
            EventScriptValueKind.Message => 8,
            EventScriptValueKind.Handler => 9,
            EventScriptValueKind.List => 10,
            EventScriptValueKind.Dictionary => 11,
            EventScriptValueKind.Set => 12,
            EventScriptValueKind.Dice => 13,
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

    private static EventScriptValue LookupSequential(IReadOnlyList<EventScriptValue> items, EventScriptValue selector)
    {
        var index = AsInt(selector);
        if (index <= 0 || index > items.Count)
        {
            return Nothing;
        }

        return items[index - 1];
    }

    private static int AsInt(EventScriptValue value)
    {
        var integer = value.AsInteger();
        if (integer < int.MinValue || integer > int.MaxValue)
        {
            return integer < 0 ? int.MinValue : int.MaxValue;
        }

        return (int)integer;
    }

    private static bool MatchSequenceBoundary(EventScriptValue value, EventScriptValue boundary, bool fromStart)
    {
        if (!IsSequential(value) || !IsSequential(boundary))
        {
            return false;
        }

        return fromStart
            ? MatchSequenceStart(value, boundary)
            : MatchSequenceEnd(value, boundary);
    }

    private static bool MatchSequenceStart(EventScriptValue value, EventScriptValue boundary)
    {
        using var valueItems = value.AsEnumerable().GetEnumerator();
        foreach (var boundaryItem in boundary.AsEnumerable())
        {
            if (!valueItems.MoveNext() || !valueItems.Current.Equals(boundaryItem))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchSequenceEnd(EventScriptValue value, EventScriptValue boundary)
    {
        var boundaryItems = boundary.AsEnumerable().ToArray();
        if (boundaryItems.Length == 0)
        {
            return true;
        }

        var tail = new Queue<EventScriptValue>(boundaryItems.Length);
        foreach (var item in value.AsEnumerable())
        {
            if (tail.Count == boundaryItems.Length)
            {
                tail.Dequeue();
            }

            tail.Enqueue(item);
        }

        if (tail.Count < boundaryItems.Length)
        {
            return false;
        }

        var index = 0;
        foreach (var item in tail)
        {
            if (!item.Equals(boundaryItems[index++]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSequential(EventScriptValue value)
        => value.Kind is EventScriptValueKind.List or EventScriptValueKind.Dice or EventScriptValueKind.Range;

    protected static string ToComparableText(EventScriptValue value)
    {
        if (value.IsNothing())
        {
            return string.Empty;
        }

        return value.Kind switch
        {
            EventScriptValueKind.Text => value.AsText(),
            EventScriptValueKind.Decimal => value.ToString(),
            EventScriptValueKind.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
            EventScriptValueKind.Boolean => value.AsBoolean().ToString(),
            _ => value.ToString()
        };
    }

    private sealed class StableEventScriptValueComparer : IComparer<EventScriptValue>
    {
        public int Compare(EventScriptValue? left, EventScriptValue? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            if (left.IsNumber() && right.IsNumber())
            {
                if (left.IsNaN() || right.IsNaN())
                {
                    return left.IsNaN() && right.IsNaN() ? 0 : left.IsNaN() ? -1 : 1;
                }

                if (left.IsInfinity() || right.IsInfinity())
                {
                    if (left.IsInfinity() && right.IsInfinity())
                    {
                        return left.IsNegativeInfinity() == right.IsNegativeInfinity() ? 0 : left.IsNegativeInfinity() ? -1 : 1;
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
                EventScriptValueKind.Percentage => ((EventScriptPercentageValue)left).Ratio.CompareTo(((EventScriptPercentageValue)right).Ratio),
                EventScriptValueKind.Boolean => left.AsBoolean().CompareTo(right.AsBoolean()),
                EventScriptValueKind.Optional => CompareOptional(left.AsOptional(), right.AsOptional()),
                EventScriptValueKind.Iterator => CompareSequence(left.AsEnumerable().ToArray(), right.AsEnumerable().ToArray()),
                EventScriptValueKind.Range => CompareRange((EventScriptRangeValue)left, (EventScriptRangeValue)right),
                EventScriptValueKind.Message => CompareMessage((EventScriptMessageValue)left, (EventScriptMessageValue)right),
                EventScriptValueKind.Handler => CompareHandler((EventScriptHandlerValue)left, (EventScriptHandlerValue)right),
                EventScriptValueKind.List => CompareSequence(left.AsList(), right.AsList()),
                EventScriptValueKind.Dictionary => CompareDictionary(left.AsDictionary(), right.AsDictionary()),
                EventScriptValueKind.Set => CompareSequence(
                    left.AsSet().OrderBy(x => x, StableComparerInstance).ToArray(),
                    right.AsSet().OrderBy(x => x, StableComparerInstance).ToArray()),
                EventScriptValueKind.Dice => CompareDice(left.AsDice(), right.AsDice()),
                _ => left.Kind.CompareTo(right.Kind)
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
            return byTo != 0 ? byTo : left.Step.CompareTo(right.Step);
        }

        private static int CompareMessage(EventScriptMessageValue left, EventScriptMessageValue right)
        {
            var bySignature = StringComparer.Ordinal.Compare(left.Value.SignatureId, right.Value.SignatureId);
            return bySignature != 0 ? bySignature : CompareDictionary(left.Value.Arguments, right.Value.Arguments);
        }

        private static int CompareHandler(EventScriptHandlerValue left, EventScriptHandlerValue right) => StringComparer.Ordinal.Compare(left.Signature.SignatureId, right.Signature.SignatureId);
    }

    internal virtual bool TryConvertToNumber(out EventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToInteger(out EventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToBoolean(out EventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToText(out EventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToList(out EventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToDictionary(out EventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToSet(out EventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToDice(out EventScriptValue value)
    {
        value = default!;
        return false;
    }

    private static bool EqualsOptional(EventScriptOptionalValue left, EventScriptOptionalValue right) => left.HasValue == right.HasValue && (!left.HasValue || left.Value.Equals(right.Value));

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

    internal static IReadOnlyList<EventScriptValue> CreateReadOnlyList(IEnumerable<EventScriptValue> values)
    {
        var list = (values ?? []).Select(RequireNotNull).ToArray();
        return list.Length == 0
            ? EventScriptListValue.Empty.AsList()
            : new ReadOnlyCollection<EventScriptValue>(list);
    }

    internal static IReadOnlyList<EventScriptValue> CreateCharacterList(string value)
        => CreateReadOnlyList((value ?? string.Empty).Select(ch => EventScriptValueFactory.Text(ch.ToString())));

    internal static bool TryConvertSequenceToDice(IEnumerable<EventScriptValue>? source, out EventScriptValue value)
    {
        if (source == null)
        {
            value = default!;
            return false;
        }

        var rolls = new List<int>();
        foreach (var item in source)
        {
            if (!RequireNotNull(item).TryConvertToInteger(out var integerValue))
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

        value = EventScriptValueFactory.Dice(EventScriptDiceValue.EventScriptDice(rolls));
        return true;
    }

    internal static long ToIntegerSaturated(decimal number)
    {
        number = decimal.Truncate(number);
        if (number < long.MinValue) return long.MinValue;
        if (number > long.MaxValue) return long.MaxValue;
        return (long)number;
    }

    internal static bool IsHiddenKey(string key) => key != null && key.StartsWith("__", StringComparison.Ordinal);

    internal static string FormatPercentage(decimal ratio) => $"{(ratio * 100m).ToString("0.############################", CultureInfo.InvariantCulture)}%";

    internal static long ToIntegerPercentage(decimal ratio)
    {
        var percent = decimal.Truncate(ratio * 100m);
        if (percent < long.MinValue) return long.MinValue;
        if (percent > long.MaxValue) return long.MaxValue;
        return (long)percent;
    }
}
