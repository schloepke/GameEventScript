#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace StepH.GameEventScript.Types;

public enum GseValueKind
{
    Nothing,
    Tag,
    Text,
    Percentage,
    Vector2,
    Vector3,
    Decimal,
    Integer,
    Boolean,
    Optional,
    Sequence,
    Range,
    Message,
    Handler,
    List,
    Dictionary,
    Set,
    Dice
}

public abstract class GseValue : IComparable<GseValue>, IEquatable<GseValue>
{
    internal const string HiddenTypeKey = "__type";
    private static readonly IComparer<GseValue> StableComparerInstance = new StableGseValueComparer();
    private static readonly GseValue NothingInstance = GseNothingValue.Instance;

    protected GseValue()
    {
    }

    public abstract GseValueKind Kind { get; }
    public static IComparer<GseValue> StableComparer => StableComparerInstance;
    public static GseValue Nothing => NothingInstance;

    public bool IsNumber() => Kind is GseValueKind.Decimal or GseValueKind.Integer or GseValueKind.Percentage;
    public bool IsNothing() => Kind == GseValueKind.Nothing;
    public bool IsTag() => Kind == GseValueKind.Tag;
    public bool IsInteger() => Kind == GseValueKind.Integer;
    public bool IsText() => Kind == GseValueKind.Text;
    public bool IsPercentage() => Kind == GseValueKind.Percentage;
    public bool IsDecimalUnit(GseDecimalUnit unit) => this is GseDecimalValue decimalValue && decimalValue.Unit == unit;
    public bool HasDecimalUnit() => this is GseDecimalValue { Unit: not null };
    public bool IsVector2() => Kind == GseValueKind.Vector2;
    public bool IsVector3() => Kind == GseValueKind.Vector3;
    public bool IsSequence() => Kind == GseValueKind.Sequence;
    public bool IsList() => Kind == GseValueKind.List;
    public bool IsDictionary() => Kind == GseValueKind.Dictionary;
    public bool IsOptional() => Kind == GseValueKind.Optional;
    public bool IsSet() => Kind == GseValueKind.Set;
    public bool IsDice() => Kind == GseValueKind.Dice;
    public bool IsRange() => Kind == GseValueKind.Range;
    public bool IsMessage() => Kind == GseValueKind.Message;
    public bool IsHandler() => Kind == GseValueKind.Handler;

    public virtual string AsText() => string.Empty;

    public virtual bool AsBoolean() => false;

    public virtual long AsInteger() => 0;

    public virtual decimal AsNumber() => 0m;

    public bool IsNaN() => this is GseDecimalValue { IsNaNValue: true };
    public bool IsInfinity() => this is GseDecimalValue { IsInfinityValue: true };
    public bool IsNegativeInfinity() => this is GseDecimalValue { IsNegativeInfinityValue: true };

    public int CompareTo(GseValue? other) => other is null ? 1 : StableComparerInstance.Compare(this, other);

    public virtual GseOptionalValue AsOptional() => GseOptionalValue.GseOptionalSome(this);

    public virtual IReadOnlyList<GseValue> AsList() => [];

    public virtual IReadOnlyDictionary<string, GseValue> AsDictionary() => GseDictionaryValue.EmptyView;

    public virtual ISet<GseValue> AsSet() => new SortedSet<GseValue>(StableComparerInstance);

    public virtual GseDiceValue AsDice() => GseDiceValue.Empty;

    public virtual bool HasSemanticValue() => true;

    public virtual bool IsSemanticallyEmpty() => false;

    public virtual bool TryUnwrapOptional(out GseValue unwrapped)
    {
        unwrapped = this;
        return true;
    }

    public GseValue Lookup(GseValue selector)
    {
        if (IsNothing() || selector.IsNothing())
        {
            return Nothing;
        }

        if (!selector.TryUnwrapOptional(out var lookup))
        {
            return GseValueFactory.OptionalNone();
        }

        var key = lookup.AsText();
        if (!string.IsNullOrEmpty(key) && TryGetDictionaryMember(key, out var value))
        {
            return value;
        }

        return LookupCore(lookup);
    }

    public virtual bool Contains(GseValue needle) => false;

    public virtual bool ContainsValue(GseValue needle) => false;

    public virtual bool StartsWith(GseValue prefix)
        => MatchSequenceBoundary(this, prefix, fromStart: true);

    public virtual bool EndsWith(GseValue suffix)
        => MatchSequenceBoundary(this, suffix, fromStart: false);

    public virtual IEnumerable<GseValue> AsEnumerable()
    {
        yield break;
    }

    public virtual bool TryGetDictionaryMember(string key, out GseValue value)
    {
        value = Nothing;
        return false;
    }

    protected virtual GseValue LookupCore(GseValue selector)
    {
        if (Kind is GseValueKind.Dictionary or GseValueKind.Message or GseValueKind.Handler)
        {
            return Nothing;
        }

        return LookupSequential(AsList(), selector);
    }

    public bool TryGetCustomTypeName(out string typeName)
    {
        if (this is GseDictionaryValue dictionary && dictionary.Storage.TryGetValue(HiddenTypeKey, out var marker) && marker.Kind == GseValueKind.Tag)
        {
            typeName = marker.AsText();
            return true;
        }

        typeName = string.Empty;
        return false;
    }

    public string DescribeType()
    {
        if (this is GseDecimalValue { Unit: { } unit })
        {
            return $":{ToDisplayTypeName(GseDecimalUnits.ToTypeName(unit))}";
        }

        return Kind switch
        {
            GseValueKind.Nothing => ":Nothing",
            GseValueKind.Tag => ":Tag",
            GseValueKind.Text => ":Text",
            GseValueKind.Percentage => ":Percentage",
            GseValueKind.Vector2 => ":Vector2",
            GseValueKind.Vector3 => ":Vector3",
            GseValueKind.Decimal => ":Decimal",
            GseValueKind.Integer => ":Integer",
            GseValueKind.Boolean => ":Boolean",
            GseValueKind.Optional => ":Optional",
            GseValueKind.Sequence => ":Sequence",
            GseValueKind.Range => ":Range",
            GseValueKind.Message => ":Message",
            GseValueKind.Handler => ":Handler",
            GseValueKind.List => ":List",
            GseValueKind.Dictionary => ":Dictionary",
            GseValueKind.Set => ":Set",
            GseValueKind.Dice => ":Dice",
            _ => Kind.ToString()
        };
    }

    public override string ToString()
    {
        return Kind switch
        {
            GseValueKind.Nothing => "Nothing",
            GseValueKind.Tag => $":{AsText()}",
            GseValueKind.Text => AsText(),
            GseValueKind.Percentage => FormatPercentage(((GsePercentageValue)this).Ratio),
            GseValueKind.Vector2 => FormatVector2((GseVector2Value)this),
            GseValueKind.Vector3 => FormatVector3((GseVector3Value)this),
            GseValueKind.Decimal => FormatDecimalValue((GseDecimalValue)this),
            GseValueKind.Integer => AsInteger().ToString(CultureInfo.InvariantCulture),
            GseValueKind.Boolean => AsBoolean().ToString(),
            GseValueKind.Optional => AsOptional().HasValue ? AsOptional().Value.ToString() : "Optional.None",
            GseValueKind.Sequence => $"sequence[{string.Join(", ", AsEnumerable().Select(x => x.ToString()))}]",
            GseValueKind.Range => $"range[{((GseRangeValue)this).From} to {((GseRangeValue)this).To} step {((GseRangeValue)this).Step}]",
            GseValueKind.Message => ((GseMessageValue)this).Value.ToString(),
            GseValueKind.Handler => $"handler {((GseHandlerValue)this).Signature.SignatureId}",
            GseValueKind.List => $"[{string.Join(", ", AsList().Select(x => x.ToString()))}]",
            GseValueKind.Set => $"set[{string.Join(", ", AsSet().Select(x => x.ToString()))}]",
            GseValueKind.Dictionary => $"dict[{string.Join(", ", AsDictionary().Select(x => $"{x.Key}: {x.Value}"))}]",
            GseValueKind.Dice => $"dice[{string.Join(", ", AsDice().Rolls)}]",
            _ => Kind.ToString()
        };
    }

    public bool Equals(GseValue? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (IsNumber() && other.IsNumber())
        {
            if (!HaveCompatibleNumericUnits(this, other))
            {
                return false;
            }

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
            GseValueKind.Nothing => true,
            GseValueKind.Tag => AsText() == other.AsText(),
            GseValueKind.Text => AsText() == other.AsText(),
            GseValueKind.Percentage => ((GsePercentageValue)this).Ratio == ((GsePercentageValue)other).Ratio,
            GseValueKind.Vector2 => ((GseVector2Value)this).X == ((GseVector2Value)other).X &&
                                            ((GseVector2Value)this).Y == ((GseVector2Value)other).Y &&
                                            ((GseVector2Value)this).Unit == ((GseVector2Value)other).Unit,
            GseValueKind.Vector3 => ((GseVector3Value)this).X == ((GseVector3Value)other).X &&
                                            ((GseVector3Value)this).Y == ((GseVector3Value)other).Y &&
                                            ((GseVector3Value)this).Z == ((GseVector3Value)other).Z &&
                                            ((GseVector3Value)this).Unit == ((GseVector3Value)other).Unit,
            GseValueKind.Decimal => AsNumber() == other.AsNumber(),
            GseValueKind.Integer => AsInteger() == other.AsInteger(),
            GseValueKind.Boolean => AsBoolean() == other.AsBoolean(),
            GseValueKind.Optional => EqualsOptional(AsOptional(), other.AsOptional()),
            GseValueKind.Sequence => AsEnumerable().SequenceEqual(other.AsEnumerable()),
            GseValueKind.Range => ((GseRangeValue)this).From == ((GseRangeValue)other).From &&
                                          ((GseRangeValue)this).To == ((GseRangeValue)other).To &&
                                          ((GseRangeValue)this).Step == ((GseRangeValue)other).Step,
            GseValueKind.Message => ((GseMessageValue)this).Value.SignatureId == ((GseMessageValue)other).Value.SignatureId &&
                                            EqualsDictionary(((GseMessageValue)this).Value.Arguments, ((GseMessageValue)other).Value.Arguments),
            GseValueKind.Handler => ((GseHandlerValue)this).Signature.SignatureId == ((GseHandlerValue)other).Signature.SignatureId,
            GseValueKind.List => AsList().SequenceEqual(other.AsList()),
            GseValueKind.Dictionary => EqualsDictionary(AsDictionary(), other.AsDictionary()),
            GseValueKind.Set => AsSet().SetEquals(other.AsSet()),
            GseValueKind.Dice => AsDice().Rolls.SequenceEqual(other.AsDice().Rolls),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is GseValue other && Equals(other);

    public override int GetHashCode()
    {
        if (IsNumber())
        {
            if (IsNaN()) return int.MinValue;
            if (IsInfinity()) return IsNegativeInfinity() ? int.MinValue + 1 : int.MaxValue;
            if (TryGetDecimalUnit(this, out var unit))
            {
                var numberHash = new HashCode();
                numberHash.Add(AsNumber());
                numberHash.Add(unit);
                return numberHash.ToHashCode();
            }

            return AsNumber().GetHashCode();
        }

        var hash = new HashCode();
        hash.Add(Kind);

        switch (Kind)
        {
            case GseValueKind.Tag:
            case GseValueKind.Text:
                hash.Add(AsText(), StringComparer.Ordinal);
                break;
            case GseValueKind.Nothing:
                hash.Add(0);
                break;
            case GseValueKind.Boolean:
                hash.Add(AsBoolean());
                break;
            case GseValueKind.Percentage:
                hash.Add(((GsePercentageValue)this).Ratio);
                break;
            case GseValueKind.Vector2:
                hash.Add(((GseVector2Value)this).X);
                hash.Add(((GseVector2Value)this).Y);
                hash.Add(((GseVector2Value)this).Unit);
                break;
            case GseValueKind.Vector3:
                hash.Add(((GseVector3Value)this).X);
                hash.Add(((GseVector3Value)this).Y);
                hash.Add(((GseVector3Value)this).Z);
                hash.Add(((GseVector3Value)this).Unit);
                break;
            case GseValueKind.Optional:
            {
                var optional = AsOptional();
                hash.Add(optional.HasValue);
                if (optional.HasValue) hash.Add(optional.Value);
                break;
            }
            case GseValueKind.Sequence:
                foreach (var item in AsEnumerable()) hash.Add(item);
                break;
            case GseValueKind.Range:
            {
                var range = (GseRangeValue)this;
                hash.Add(range.From);
                hash.Add(range.To);
                hash.Add(range.Step);
                break;
            }
            case GseValueKind.Message:
                hash.Add(((GseMessageValue)this).Value.SignatureId, StringComparer.Ordinal);
                foreach (var pair in ((GseMessageValue)this).Value.Arguments.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    hash.Add(pair.Key, StringComparer.Ordinal);
                    hash.Add(pair.Value);
                }

                break;
            case GseValueKind.Handler:
                hash.Add(((GseHandlerValue)this).Signature.SignatureId, StringComparer.Ordinal);
                break;
            case GseValueKind.List:
                foreach (var item in AsList()) hash.Add(item);
                break;
            case GseValueKind.Dictionary:
                foreach (var pair in AsDictionary().OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    hash.Add(pair.Key, StringComparer.Ordinal);
                    hash.Add(pair.Value);
                }

                break;
            case GseValueKind.Set:
                foreach (var item in AsSet().OrderBy(x => x, StableComparerInstance)) hash.Add(item);
                break;
            case GseValueKind.Dice:
                foreach (var roll in AsDice().Rolls) hash.Add(roll);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown Gse value kind.");
        }

        return hash.ToHashCode();
    }

    public static implicit operator GseValue(string value) => GseValueFactory.Text(value);
    public static implicit operator GseValue(bool value) => GseValueFactory.Boolean(value);
    public static implicit operator GseValue(int value) => GseValueFactory.Integer(value);
    public static implicit operator GseValue(long value) => GseValueFactory.Integer(value);
    public static implicit operator GseValue(decimal value) => GseValueFactory.Decimal(value);
    public static implicit operator GseValue(float value) => GseDecimalValue.GseDecimal(value);
    public static implicit operator GseValue(double value) => GseDecimalValue.GseDecimal(value);

    private static GseValue RequireNotNull(GseValue? value) => value ?? Nothing;

    private static int GetSortRank(GseValue value)
    {
        if (value.IsNumber()) return 1;

        return value.Kind switch
        {
            GseValueKind.Nothing => 0,
            GseValueKind.Tag => 1,
            GseValueKind.Text => 2,
            GseValueKind.Percentage => 3,
            GseValueKind.Vector2 => 4,
            GseValueKind.Vector3 => 5,
            GseValueKind.Boolean => 6,
            GseValueKind.Optional => 7,
            GseValueKind.Sequence => 8,
            GseValueKind.Range => 9,
            GseValueKind.Message => 10,
            GseValueKind.Handler => 11,
            GseValueKind.List => 12,
            GseValueKind.Dictionary => 13,
            GseValueKind.Set => 14,
            GseValueKind.Dice => 15,
            _ => 8
        };
    }

    private static int CompareSequence(IReadOnlyList<GseValue> left, IReadOnlyList<GseValue> right)
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

    private static int CompareDictionary(IReadOnlyDictionary<string, GseValue> left, IReadOnlyDictionary<string, GseValue> right)
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

    private static GseValue LookupSequential(IReadOnlyList<GseValue> items, GseValue selector)
    {
        var index = AsInt(selector);
        if (index <= 0 || index > items.Count)
        {
            return Nothing;
        }

        return items[index - 1];
    }

    private static int AsInt(GseValue value)
    {
        var integer = value.AsInteger();
        if (integer < int.MinValue || integer > int.MaxValue)
        {
            return integer < 0 ? int.MinValue : int.MaxValue;
        }

        return (int)integer;
    }

    private static bool MatchSequenceBoundary(GseValue value, GseValue boundary, bool fromStart)
    {
        if (!IsSequential(value) || !IsSequential(boundary))
        {
            return false;
        }

        return fromStart
            ? MatchSequenceStart(value, boundary)
            : MatchSequenceEnd(value, boundary);
    }

    private static bool MatchSequenceStart(GseValue value, GseValue boundary)
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

    private static bool MatchSequenceEnd(GseValue value, GseValue boundary)
    {
        var boundaryItems = boundary.AsEnumerable().ToArray();
        if (boundaryItems.Length == 0)
        {
            return true;
        }

        var tail = new Queue<GseValue>(boundaryItems.Length);
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

    private static bool IsSequential(GseValue value)
        => value.Kind is GseValueKind.List or GseValueKind.Dice or GseValueKind.Range;

    protected static string ToComparableText(GseValue value)
    {
        if (value.IsNothing())
        {
            return string.Empty;
        }

        return value.Kind switch
        {
            GseValueKind.Text => value.AsText(),
            GseValueKind.Decimal => value.ToString(),
            GseValueKind.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
            GseValueKind.Boolean => value.AsBoolean().ToString(),
            _ => value.ToString()
        };
    }

    private sealed class StableGseValueComparer : IComparer<GseValue>
    {
        public int Compare(GseValue? left, GseValue? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            if (left.IsNumber() && right.IsNumber())
            {
                var unitComparison = CompareNumericUnits(left, right);
                if (unitComparison != 0)
                {
                    return unitComparison;
                }

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
                GseValueKind.Nothing => 0,
                GseValueKind.Tag => StringComparer.Ordinal.Compare(left.AsText(), right.AsText()),
                GseValueKind.Text => StringComparer.Ordinal.Compare(left.AsText(), right.AsText()),
                GseValueKind.Percentage => ((GsePercentageValue)left).Ratio.CompareTo(((GsePercentageValue)right).Ratio),
                GseValueKind.Vector2 => CompareSequence(left.AsList(), right.AsList()),
                GseValueKind.Vector3 => CompareSequence(left.AsList(), right.AsList()),
                GseValueKind.Boolean => left.AsBoolean().CompareTo(right.AsBoolean()),
                GseValueKind.Optional => CompareOptional(left.AsOptional(), right.AsOptional()),
                GseValueKind.Sequence => CompareSequence(left.AsEnumerable().ToArray(), right.AsEnumerable().ToArray()),
                GseValueKind.Range => CompareRange((GseRangeValue)left, (GseRangeValue)right),
                GseValueKind.Message => CompareMessage((GseMessageValue)left, (GseMessageValue)right),
                GseValueKind.Handler => CompareHandler((GseHandlerValue)left, (GseHandlerValue)right),
                GseValueKind.List => CompareSequence(left.AsList(), right.AsList()),
                GseValueKind.Dictionary => CompareDictionary(left.AsDictionary(), right.AsDictionary()),
                GseValueKind.Set => CompareSequence(
                    left.AsSet().OrderBy(x => x, StableComparerInstance).ToArray(),
                    right.AsSet().OrderBy(x => x, StableComparerInstance).ToArray()),
                GseValueKind.Dice => CompareDice(left.AsDice(), right.AsDice()),
                _ => left.Kind.CompareTo(right.Kind)
            };
        }

        private static int CompareOptional(GseOptionalValue left, GseOptionalValue right)
        {
            if (!left.HasValue && !right.HasValue) return 0;
            if (!left.HasValue) return -1;
            if (!right.HasValue) return 1;
            return StableComparerInstance.Compare(left.Value, right.Value);
        }

        private static int CompareDice(GseDiceValue left, GseDiceValue right)
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

        private static int CompareRange(GseRangeValue left, GseRangeValue right)
        {
            var byFrom = left.From.CompareTo(right.From);
            if (byFrom != 0) return byFrom;
            var byTo = left.To.CompareTo(right.To);
            return byTo != 0 ? byTo : left.Step.CompareTo(right.Step);
        }

        private static int CompareMessage(GseMessageValue left, GseMessageValue right)
        {
            var bySignature = StringComparer.Ordinal.Compare(left.Value.SignatureId, right.Value.SignatureId);
            return bySignature != 0 ? bySignature : CompareDictionary(left.Value.Arguments, right.Value.Arguments);
        }

        private static int CompareHandler(GseHandlerValue left, GseHandlerValue right) => StringComparer.Ordinal.Compare(left.Signature.SignatureId, right.Signature.SignatureId);
    }

    internal virtual bool TryConvertToNumber(out GseValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToInteger(out GseValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToBoolean(out GseValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToText(out GseValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToList(out GseValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToDictionary(out GseValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToSet(out GseValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToDice(out GseValue value)
    {
        value = default!;
        return false;
    }

    private static bool EqualsOptional(GseOptionalValue left, GseOptionalValue right) => left.HasValue == right.HasValue && (!left.HasValue || left.Value.Equals(right.Value));

    private static bool EqualsDictionary(IReadOnlyDictionary<string, GseValue> left, IReadOnlyDictionary<string, GseValue> right)
    {
        if (left.Count != right.Count) return false;

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue)) return false;
            if (!pair.Value.Equals(rightValue)) return false;
        }

        return true;
    }

    internal static IReadOnlyList<GseValue> CreateReadOnlyList(IEnumerable<GseValue> values)
    {
        var list = (values ?? []).Select(RequireNotNull).ToArray();
        return list.Length == 0
            ? GseListValue.Empty.AsList()
            : new ReadOnlyCollection<GseValue>(list);
    }

    internal static IReadOnlyList<GseValue> CreateCharacterList(string value)
        => CreateReadOnlyList((value ?? string.Empty).Select(ch => GseValueFactory.Text(ch.ToString())));

    internal static bool TryConvertSequenceToDice(IEnumerable<GseValue>? source, out GseValue value)
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

        value = GseValueFactory.Dice(GseDiceValue.GseDice(rolls));
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

    internal static string FormatDecimalValue(GseDecimalValue value)
    {
        if (value.IsNaNValue)
        {
            return "NaN";
        }

        if (value.IsInfinityValue)
        {
            return value.IsNegativeInfinityValue ? "-Infinity" : "Infinity";
        }

        var formatted = value.Value.ToString("0.############################", CultureInfo.InvariantCulture);
        return value.Unit.HasValue
            ? $"{formatted}{GseDecimalUnits.ToSuffix(value.Unit.Value)}"
            : formatted;
    }

    public static decimal WrapDegrees(decimal degrees)
    {
        var wrapped = degrees % 360m;
        if (wrapped < 0m)
        {
            wrapped += 360m;
        }

        return wrapped == 360m ? 0m : wrapped;
    }

    internal static bool TryGetDecimalUnit(GseValue value, out GseDecimalUnit unit)
    {
        if (value is GseDecimalValue { Unit: { } decimalUnit })
        {
            unit = decimalUnit;
            return true;
        }

        unit = default;
        return false;
    }

    private static bool HaveCompatibleNumericUnits(GseValue left, GseValue right)
        => TryGetDecimalUnit(left, out var leftUnit) == TryGetDecimalUnit(right, out var rightUnit) &&
           (!TryGetDecimalUnit(left, out _) || leftUnit == rightUnit);

    private static int CompareNumericUnits(GseValue left, GseValue right)
    {
        var leftHasUnit = TryGetDecimalUnit(left, out var leftUnit);
        var rightHasUnit = TryGetDecimalUnit(right, out var rightUnit);
        if (!leftHasUnit && !rightHasUnit)
        {
            return 0;
        }

        if (leftHasUnit != rightHasUnit)
        {
            return leftHasUnit ? 1 : -1;
        }

        return leftUnit.CompareTo(rightUnit);
    }

    private static string ToDisplayTypeName(string typeName)
        => string.IsNullOrEmpty(typeName)
            ? typeName
            : string.Create(typeName.Length, typeName, static (chars, value) =>
            {
                chars[0] = char.ToUpperInvariant(value[0]);
                for (var i = 1; i < value.Length; i++)
                {
                    chars[i] = value[i];
                }
            });

    internal static string FormatVector2(GseVector2Value value)
        => $"vector2[x: {FormatDecimalComponent(value.X, value.Unit)}, y: {FormatDecimalComponent(value.Y, value.Unit)}]";

    internal static string FormatVector3(GseVector3Value value)
        => $"vector3[x: {FormatDecimalComponent(value.X, value.Unit)}, y: {FormatDecimalComponent(value.Y, value.Unit)}, z: {FormatDecimalComponent(value.Z, value.Unit)}]";

    internal static string FormatDecimalComponent(decimal value, GseDecimalUnit? unit = null)
    {
        var formatted = value.ToString("0.############################", CultureInfo.InvariantCulture);
        return unit.HasValue ? $"{formatted}{GseDecimalUnits.ToSuffix(unit.Value)}" : formatted;
    }

    internal static long ToIntegerPercentage(decimal ratio)
    {
        var percent = decimal.Truncate(ratio * 100m);
        if (percent < long.MinValue) return long.MinValue;
        if (percent > long.MaxValue) return long.MaxValue;
        return (long)percent;
    }
}
