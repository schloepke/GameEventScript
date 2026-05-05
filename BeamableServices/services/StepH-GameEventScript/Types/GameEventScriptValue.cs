#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Types;

public enum GameEventScriptValueKind
{
    Nothing,
    Tag,
    Text,
    Percentage,
    Vector,
    Point,
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

public abstract class GameEventScriptValue : IComparable<GameEventScriptValue>, IEquatable<GameEventScriptValue>
{
    internal const string HiddenTypeKey = "__type";

    protected GameEventScriptValue()
    {
    }

    public abstract GameEventScriptValueKind Kind { get; }
    
    public static IComparer<GameEventScriptValue> StableComparer { get; } = new StableGameEventScriptValueComparer();

    public bool IsNumber() => Kind is GameEventScriptValueKind.Decimal or GameEventScriptValueKind.Integer or GameEventScriptValueKind.Percentage;
    public bool IsNothing() => Kind == GameEventScriptValueKind.Nothing;
    public bool IsTag() => Kind == GameEventScriptValueKind.Tag;
    public bool IsInteger() => Kind == GameEventScriptValueKind.Integer;
    public bool IsText() => Kind == GameEventScriptValueKind.Text;
    public bool IsPercentage() => Kind == GameEventScriptValueKind.Percentage;
    public bool IsDecimalUnit(GameEventScriptDecimalUnit unit) => this is GameEventScriptDecimalValue decimalValue && decimalValue.Unit == unit;
    public bool HasDecimalUnit() => this is GameEventScriptDecimalValue { Unit: not null };
    public bool IsVector() => Kind == GameEventScriptValueKind.Vector;
    public bool IsPoint() => Kind == GameEventScriptValueKind.Point;
    public bool IsSequence() => Kind == GameEventScriptValueKind.Sequence;
    public bool IsList() => Kind == GameEventScriptValueKind.List;
    public bool IsDictionary() => Kind == GameEventScriptValueKind.Dictionary;
    public bool IsOptional() => Kind == GameEventScriptValueKind.Optional;
    public bool IsSet() => Kind == GameEventScriptValueKind.Set;
    public bool IsDice() => Kind == GameEventScriptValueKind.Dice;
    public bool IsRange() => Kind == GameEventScriptValueKind.Range;
    public bool IsMessage() => Kind == GameEventScriptValueKind.Message;
    public bool IsHandler() => Kind == GameEventScriptValueKind.Handler;

    public virtual string AsText() => string.Empty;

    public virtual bool AsBoolean() => false;

    public virtual long AsInteger() => 0;

    public virtual decimal AsNumber() => 0m;

    public bool IsNaN() => this is GameEventScriptDecimalValue { IsNaNValue: true };
    public bool IsInfinity() => this is GameEventScriptDecimalValue { IsInfinityValue: true };
    public bool IsNegativeInfinity() => this is GameEventScriptDecimalValue { IsNegativeInfinityValue: true };

    public int CompareTo(GameEventScriptValue? other) => other is null ? 1 : StableComparer.Compare(this, other);

    public virtual GameEventScriptOptionalValue AsOptional() => GameEventScriptOptionalValue.Create(this);

    public virtual IReadOnlyList<GameEventScriptValue> AsList() => [];

    public virtual IReadOnlyDictionary<string, GameEventScriptValue> AsDictionary() => GameEventScriptDictionaryValue.EmptyView;

    public virtual ISet<GameEventScriptValue> AsSet() => new SortedSet<GameEventScriptValue>(StableComparer);

    public virtual GameEventScriptDiceValue AsDice() => GameEventScriptDiceValue.Empty;

    public virtual bool HasSemanticValue() => true;

    public virtual bool IsSemanticallyEmpty() => false;

    public virtual bool TryUnwrapOptional(out GameEventScriptValue unwrapped)
    {
        unwrapped = this;
        return true;
    }

    public GameEventScriptValue Lookup(GameEventScriptValue selector)
    {
        if (IsNothing() || selector.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (!selector.TryUnwrapOptional(out var lookup))
        {
            return GameEventScriptValueFactory.GesOptionalNone();
        }

        var key = lookup.AsText();
        if (!string.IsNullOrEmpty(key) && TryGetDictionaryMember(key, out var value))
        {
            return value;
        }

        return LookupCore(lookup);
    }

    public virtual bool Contains(GameEventScriptValue needle) => false;

    public virtual bool ContainsValue(GameEventScriptValue needle) => false;

    public virtual bool StartsWith(GameEventScriptValue prefix)
        => MatchSequenceBoundary(this, prefix, fromStart: true);

    public virtual bool EndsWith(GameEventScriptValue suffix)
        => MatchSequenceBoundary(this, suffix, fromStart: false);

    public virtual IEnumerable<GameEventScriptValue> AsEnumerable()
    {
        yield break;
    }

    public virtual bool TryGetDictionaryMember(string key, out GameEventScriptValue value)
    {
        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    protected virtual GameEventScriptValue LookupCore(GameEventScriptValue selector)
    {
        if (Kind is GameEventScriptValueKind.Dictionary or GameEventScriptValueKind.Message or GameEventScriptValueKind.Handler)
        {
            return GameEventScriptNothingValue.Instance;
        }

        return LookupSequential(AsList(), selector);
    }

    public bool TryGetCustomTypeName(out string typeName)
    {
        if (this is GameEventScriptDictionaryValue dictionary && dictionary.Storage.TryGetValue(HiddenTypeKey, out var marker) && marker.Kind == GameEventScriptValueKind.Tag)
        {
            typeName = marker.AsText();
            return true;
        }

        typeName = string.Empty;
        return false;
    }

    public string DescribeType()
    {
        if (this is GameEventScriptDecimalValue { Unit: { } unit })
        {
            return $":{ToDisplayTypeName(unit.ToTypeName())}";
        }

        return Kind switch
        {
            GameEventScriptValueKind.Nothing => ":Nothing",
            GameEventScriptValueKind.Tag => ":Tag",
            GameEventScriptValueKind.Text => ":Text",
            GameEventScriptValueKind.Percentage => ":Percentage",
            GameEventScriptValueKind.Vector => ":Vector",
            GameEventScriptValueKind.Point => ":Point",
            GameEventScriptValueKind.Decimal => ":Decimal",
            GameEventScriptValueKind.Integer => ":Integer",
            GameEventScriptValueKind.Boolean => ":Boolean",
            GameEventScriptValueKind.Optional => ":Optional",
            GameEventScriptValueKind.Sequence => ":Sequence",
            GameEventScriptValueKind.Range => ":Range",
            GameEventScriptValueKind.Message => ":Message",
            GameEventScriptValueKind.Handler => ":Handler",
            GameEventScriptValueKind.List => ":List",
            GameEventScriptValueKind.Dictionary => ":Dictionary",
            GameEventScriptValueKind.Set => ":Set",
            GameEventScriptValueKind.Dice => ":Dice",
            _ => Kind.ToString()
        };
    }

    public override string ToString()
    {
        return Kind switch
        {
            GameEventScriptValueKind.Nothing => "Nothing",
            GameEventScriptValueKind.Tag => $":{AsText()}",
            GameEventScriptValueKind.Text => AsText(),
            GameEventScriptValueKind.Percentage => FormatPercentage(((GameEventScriptPercentageValue)this).Ratio),
            GameEventScriptValueKind.Vector => FormatVector((GameEventScriptVectorValue)this),
            GameEventScriptValueKind.Point => FormatPoint((GameEventScriptPointValue)this),
            GameEventScriptValueKind.Decimal => FormatDecimalValue((GameEventScriptDecimalValue)this),
            GameEventScriptValueKind.Integer => AsInteger().ToString(CultureInfo.InvariantCulture),
            GameEventScriptValueKind.Boolean => AsBoolean().ToString(),
            GameEventScriptValueKind.Optional => AsOptional().HasValue ? AsOptional().Value.ToString() : "Optional.None",
            GameEventScriptValueKind.Sequence => $"sequence[{string.Join(", ", AsEnumerable().Select(x => x.ToString()))}]",
            GameEventScriptValueKind.Range => $"range[{((GameEventScriptRangeValue)this).From} to {((GameEventScriptRangeValue)this).To} step {((GameEventScriptRangeValue)this).Step}]",
            GameEventScriptValueKind.Message => ((GameEventScriptMessageValue)this).Value.ToString(),
            GameEventScriptValueKind.Handler => $"handler {((GameEventScriptHandlerValue)this).Signature.SignatureId}",
            GameEventScriptValueKind.List => $"[{string.Join(", ", AsList().Select(x => x.ToString()))}]",
            GameEventScriptValueKind.Set => $"set[{string.Join(", ", AsSet().Select(x => x.ToString()))}]",
            GameEventScriptValueKind.Dictionary => $"dict[{string.Join(", ", AsDictionary().Select(x => $"{x.Key}: {x.Value}"))}]",
            GameEventScriptValueKind.Dice => $"dice[{string.Join(", ", AsDice().Rolls)}]",
            _ => Kind.ToString()
        };
    }

    public bool Equals(GameEventScriptValue? other)
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
            GameEventScriptValueKind.Nothing => true,
            GameEventScriptValueKind.Tag => AsText() == other.AsText(),
            GameEventScriptValueKind.Text => AsText() == other.AsText(),
            GameEventScriptValueKind.Percentage => ((GameEventScriptPercentageValue)this).Ratio == ((GameEventScriptPercentageValue)other).Ratio,
            GameEventScriptValueKind.Vector => ((GameEventScriptVectorValue)this).X == ((GameEventScriptVectorValue)other).X &&
                                            ((GameEventScriptVectorValue)this).Y == ((GameEventScriptVectorValue)other).Y &&
                                            ((GameEventScriptVectorValue)this).Z == ((GameEventScriptVectorValue)other).Z &&
                                            ((GameEventScriptVectorValue)this).Unit == ((GameEventScriptVectorValue)other).Unit,
            GameEventScriptValueKind.Point => ((GameEventScriptPointValue)this).X == ((GameEventScriptPointValue)other).X &&
                                            ((GameEventScriptPointValue)this).Y == ((GameEventScriptPointValue)other).Y &&
                                            ((GameEventScriptPointValue)this).Z == ((GameEventScriptPointValue)other).Z &&
                                            ((GameEventScriptPointValue)this).Unit == ((GameEventScriptPointValue)other).Unit,
            GameEventScriptValueKind.Decimal => AsNumber() == other.AsNumber(),
            GameEventScriptValueKind.Integer => AsInteger() == other.AsInteger(),
            GameEventScriptValueKind.Boolean => AsBoolean() == other.AsBoolean(),
            GameEventScriptValueKind.Optional => EqualsOptional(AsOptional(), other.AsOptional()),
            GameEventScriptValueKind.Sequence => AsEnumerable().SequenceEqual(other.AsEnumerable()),
            GameEventScriptValueKind.Range => ((GameEventScriptRangeValue)this).From == ((GameEventScriptRangeValue)other).From &&
                                          ((GameEventScriptRangeValue)this).To == ((GameEventScriptRangeValue)other).To &&
                                          ((GameEventScriptRangeValue)this).Step == ((GameEventScriptRangeValue)other).Step,
            GameEventScriptValueKind.Message => ((GameEventScriptMessageValue)this).Value.SignatureId == ((GameEventScriptMessageValue)other).Value.SignatureId &&
                                            EqualsDictionary(((GameEventScriptMessageValue)this).Value.Arguments, ((GameEventScriptMessageValue)other).Value.Arguments),
            GameEventScriptValueKind.Handler => ((GameEventScriptHandlerValue)this).Signature.SignatureId == ((GameEventScriptHandlerValue)other).Signature.SignatureId,
            GameEventScriptValueKind.List => AsList().SequenceEqual(other.AsList()),
            GameEventScriptValueKind.Dictionary => EqualsDictionary(AsDictionary(), other.AsDictionary()),
            GameEventScriptValueKind.Set => AsSet().SetEquals(other.AsSet()),
            GameEventScriptValueKind.Dice => AsDice().Rolls.SequenceEqual(other.AsDice().Rolls),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is GameEventScriptValue other && Equals(other);

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
            case GameEventScriptValueKind.Tag:
            case GameEventScriptValueKind.Text:
                hash.Add(AsText(), StringComparer.Ordinal);
                break;
            case GameEventScriptValueKind.Nothing:
                hash.Add(0);
                break;
            case GameEventScriptValueKind.Boolean:
                hash.Add(AsBoolean());
                break;
            case GameEventScriptValueKind.Percentage:
                hash.Add(((GameEventScriptPercentageValue)this).Ratio);
                break;
            case GameEventScriptValueKind.Vector:
                hash.Add(((GameEventScriptVectorValue)this).X);
                hash.Add(((GameEventScriptVectorValue)this).Y);
                hash.Add(((GameEventScriptVectorValue)this).Z);
                hash.Add(((GameEventScriptVectorValue)this).Unit);
                break;
            case GameEventScriptValueKind.Point:
                hash.Add(((GameEventScriptPointValue)this).X);
                hash.Add(((GameEventScriptPointValue)this).Y);
                hash.Add(((GameEventScriptPointValue)this).Z);
                hash.Add(((GameEventScriptPointValue)this).Unit);
                break;
            case GameEventScriptValueKind.Optional:
            {
                var optional = AsOptional();
                hash.Add(optional.HasValue);
                if (optional.HasValue) hash.Add(optional.Value);
                break;
            }
            case GameEventScriptValueKind.Sequence:
                foreach (var item in AsEnumerable()) hash.Add(item);
                break;
            case GameEventScriptValueKind.Range:
            {
                var range = (GameEventScriptRangeValue)this;
                hash.Add(range.From);
                hash.Add(range.To);
                hash.Add(range.Step);
                break;
            }
            case GameEventScriptValueKind.Message:
                hash.Add(((GameEventScriptMessageValue)this).Value.SignatureId, StringComparer.Ordinal);
                foreach (var pair in ((GameEventScriptMessageValue)this).Value.Arguments.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    hash.Add(pair.Key, StringComparer.Ordinal);
                    hash.Add(pair.Value);
                }

                break;
            case GameEventScriptValueKind.Handler:
                hash.Add(((GameEventScriptHandlerValue)this).Signature.SignatureId, StringComparer.Ordinal);
                break;
            case GameEventScriptValueKind.List:
                foreach (var item in AsList()) hash.Add(item);
                break;
            case GameEventScriptValueKind.Dictionary:
                foreach (var pair in AsDictionary().OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    hash.Add(pair.Key, StringComparer.Ordinal);
                    hash.Add(pair.Value);
                }

                break;
            case GameEventScriptValueKind.Set:
                foreach (var item in AsSet().OrderBy(x => x, StableComparer)) hash.Add(item);
                break;
            case GameEventScriptValueKind.Dice:
                foreach (var roll in AsDice().Rolls) hash.Add(roll);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown GameEventScript value kind.");
        }

        return hash.ToHashCode();
    }

    public static implicit operator GameEventScriptValue(string value) => GameEventScriptValueFactory.GesText(value);
    public static implicit operator GameEventScriptValue(bool value) => GameEventScriptValueFactory.GesBoolean(value);
    public static implicit operator GameEventScriptValue(int value) => GameEventScriptValueFactory.GesInteger(value);
    public static implicit operator GameEventScriptValue(long value) => GameEventScriptValueFactory.GesInteger(value);
    public static implicit operator GameEventScriptValue(decimal value) => GameEventScriptValueFactory.GesDecimal(value);
    public static implicit operator GameEventScriptValue(float value) => GameEventScriptDecimalValue.Create(value);
    public static implicit operator GameEventScriptValue(double value) => GameEventScriptDecimalValue.Create(value);

    private static GameEventScriptValue RequireNotNull(GameEventScriptValue? value) => value ?? GameEventScriptNothingValue.Instance;

    private static int GetSortRank(GameEventScriptValue value)
    {
        if (value.IsNumber()) return 1;

        return value.Kind switch
        {
            GameEventScriptValueKind.Nothing => 0,
            GameEventScriptValueKind.Tag => 1,
            GameEventScriptValueKind.Text => 2,
            GameEventScriptValueKind.Percentage => 3,
            GameEventScriptValueKind.Vector => 4,
            GameEventScriptValueKind.Point => 5,
            GameEventScriptValueKind.Boolean => 6,
            GameEventScriptValueKind.Optional => 7,
            GameEventScriptValueKind.Sequence => 8,
            GameEventScriptValueKind.Range => 9,
            GameEventScriptValueKind.Message => 10,
            GameEventScriptValueKind.Handler => 11,
            GameEventScriptValueKind.List => 12,
            GameEventScriptValueKind.Dictionary => 13,
            GameEventScriptValueKind.Set => 14,
            GameEventScriptValueKind.Dice => 15,
            _ => 8
        };
    }

    private static int CompareSequence(IReadOnlyList<GameEventScriptValue> left, IReadOnlyList<GameEventScriptValue> right)
    {
        var byCount = left.Count.CompareTo(right.Count);
        if (byCount != 0) return byCount;

        for (var i = 0; i < left.Count; i++)
        {
            var byItem = StableComparer.Compare(left[i], right[i]);
            if (byItem != 0) return byItem;
        }

        return 0;
    }

    private static int CompareDictionary(IReadOnlyDictionary<string, GameEventScriptValue> left, IReadOnlyDictionary<string, GameEventScriptValue> right)
    {
        var byCount = left.Count.CompareTo(right.Count);
        if (byCount != 0) return byCount;

        var leftPairs = left.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        var rightPairs = right.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();

        for (var i = 0; i < leftPairs.Length; i++)
        {
            var byKey = StringComparer.Ordinal.Compare(leftPairs[i].Key, rightPairs[i].Key);
            if (byKey != 0) return byKey;

            var byValue = StableComparer.Compare(leftPairs[i].Value, rightPairs[i].Value);
            if (byValue != 0) return byValue;
        }

        return 0;
    }

    private static GameEventScriptValue LookupSequential(IReadOnlyList<GameEventScriptValue> items, GameEventScriptValue selector)
    {
        var index = AsInt(selector);
        if (index <= 0 || index > items.Count)
        {
            return GameEventScriptNothingValue.Instance;
        }

        return items[index - 1];
    }

    private static int AsInt(GameEventScriptValue value)
    {
        var integer = value.AsInteger();
        if (integer < int.MinValue || integer > int.MaxValue)
        {
            return integer < 0 ? int.MinValue : int.MaxValue;
        }

        return (int)integer;
    }

    private static bool MatchSequenceBoundary(GameEventScriptValue value, GameEventScriptValue boundary, bool fromStart)
    {
        if (!IsSequential(value) || !IsSequential(boundary))
        {
            return false;
        }

        return fromStart
            ? MatchSequenceStart(value, boundary)
            : MatchSequenceEnd(value, boundary);
    }

    private static bool MatchSequenceStart(GameEventScriptValue value, GameEventScriptValue boundary)
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

    private static bool MatchSequenceEnd(GameEventScriptValue value, GameEventScriptValue boundary)
    {
        var boundaryItems = boundary.AsEnumerable().ToArray();
        if (boundaryItems.Length == 0)
        {
            return true;
        }

        var tail = new Queue<GameEventScriptValue>(boundaryItems.Length);
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

    private static bool IsSequential(GameEventScriptValue value)
        => value.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice or GameEventScriptValueKind.Range;

    protected static string ToComparableText(GameEventScriptValue value)
    {
        if (value.IsNothing())
        {
            return string.Empty;
        }

        return value.Kind switch
        {
            GameEventScriptValueKind.Text => value.AsText(),
            GameEventScriptValueKind.Decimal => value.ToString(),
            GameEventScriptValueKind.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
            GameEventScriptValueKind.Boolean => value.AsBoolean().ToString(),
            _ => value.ToString()
        };
    }

    private sealed class StableGameEventScriptValueComparer : IComparer<GameEventScriptValue>
    {
        public int Compare(GameEventScriptValue? left, GameEventScriptValue? right)
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
                GameEventScriptValueKind.Nothing => 0,
                GameEventScriptValueKind.Tag => StringComparer.Ordinal.Compare(left.AsText(), right.AsText()),
                GameEventScriptValueKind.Text => StringComparer.Ordinal.Compare(left.AsText(), right.AsText()),
                GameEventScriptValueKind.Percentage => ((GameEventScriptPercentageValue)left).Ratio.CompareTo(((GameEventScriptPercentageValue)right).Ratio),
                GameEventScriptValueKind.Vector => CompareSequence(left.AsList(), right.AsList()),
                GameEventScriptValueKind.Point => CompareSequence(left.AsList(), right.AsList()),
                GameEventScriptValueKind.Boolean => left.AsBoolean().CompareTo(right.AsBoolean()),
                GameEventScriptValueKind.Optional => CompareOptional(left.AsOptional(), right.AsOptional()),
                GameEventScriptValueKind.Sequence => CompareSequence(left.AsEnumerable().ToArray(), right.AsEnumerable().ToArray()),
                GameEventScriptValueKind.Range => CompareRange((GameEventScriptRangeValue)left, (GameEventScriptRangeValue)right),
                GameEventScriptValueKind.Message => CompareMessage((GameEventScriptMessageValue)left, (GameEventScriptMessageValue)right),
                GameEventScriptValueKind.Handler => CompareHandler((GameEventScriptHandlerValue)left, (GameEventScriptHandlerValue)right),
                GameEventScriptValueKind.List => CompareSequence(left.AsList(), right.AsList()),
                GameEventScriptValueKind.Dictionary => CompareDictionary(left.AsDictionary(), right.AsDictionary()),
                GameEventScriptValueKind.Set => CompareSequence(
                    left.AsSet().OrderBy(x => x, StableComparer).ToArray(),
                    right.AsSet().OrderBy(x => x, StableComparer).ToArray()),
                GameEventScriptValueKind.Dice => CompareDice(left.AsDice(), right.AsDice()),
                _ => left.Kind.CompareTo(right.Kind)
            };
        }

        private static int CompareOptional(GameEventScriptOptionalValue left, GameEventScriptOptionalValue right)
        {
            if (!left.HasValue && !right.HasValue) return 0;
            if (!left.HasValue) return -1;
            if (!right.HasValue) return 1;
            return StableComparer.Compare(left.Value, right.Value);
        }

        private static int CompareDice(GameEventScriptDiceValue left, GameEventScriptDiceValue right)
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

        private static int CompareRange(GameEventScriptRangeValue left, GameEventScriptRangeValue right)
        {
            var byFrom = left.From.CompareTo(right.From);
            if (byFrom != 0) return byFrom;
            var byTo = left.To.CompareTo(right.To);
            return byTo != 0 ? byTo : left.Step.CompareTo(right.Step);
        }

        private static int CompareMessage(GameEventScriptMessageValue left, GameEventScriptMessageValue right)
        {
            var bySignature = StringComparer.Ordinal.Compare(left.Value.SignatureId, right.Value.SignatureId);
            return bySignature != 0 ? bySignature : CompareDictionary(left.Value.Arguments, right.Value.Arguments);
        }

        private static int CompareHandler(GameEventScriptHandlerValue left, GameEventScriptHandlerValue right) => StringComparer.Ordinal.Compare(left.Signature.SignatureId, right.Signature.SignatureId);
    }

    internal virtual bool TryConvertToNumber(out GameEventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToInteger(out GameEventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToBoolean(out GameEventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToText(out GameEventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToList(out GameEventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToDictionary(out GameEventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToSet(out GameEventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToDice(out GameEventScriptValue value)
    {
        value = default!;
        return false;
    }

    private static bool EqualsOptional(GameEventScriptOptionalValue left, GameEventScriptOptionalValue right) => left.HasValue == right.HasValue && (!left.HasValue || left.Value.Equals(right.Value));

    private static bool EqualsDictionary(IReadOnlyDictionary<string, GameEventScriptValue> left, IReadOnlyDictionary<string, GameEventScriptValue> right)
    {
        if (left.Count != right.Count) return false;

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue)) return false;
            if (!pair.Value.Equals(rightValue)) return false;
        }

        return true;
    }

    internal static IReadOnlyList<GameEventScriptValue> CreateReadOnlyList(IEnumerable<GameEventScriptValue> values)
    {
        var list = (values ?? []).Select(RequireNotNull).ToArray();
        return list.Length == 0
            ? GameEventScriptListValue.Empty.AsList()
            : new ReadOnlyCollection<GameEventScriptValue>(list);
    }

    internal static IReadOnlyList<GameEventScriptValue> CreateCharacterList(string value)
        => CreateReadOnlyList((value ?? string.Empty).Select(ch => GameEventScriptValueFactory.GesText(ch.ToString())));

    internal static bool TryConvertSequenceToDice(IEnumerable<GameEventScriptValue>? source, out GameEventScriptValue value)
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

        value = GameEventScriptValueFactory.GesDice(GameEventScriptDiceValue.Create(rolls));
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

    internal static string FormatDecimalValue(GameEventScriptDecimalValue value)
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
            ? $"{formatted}{value.Unit.Value.ToSuffix()}"
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

    internal static bool TryGetDecimalUnit(GameEventScriptValue value, out GameEventScriptDecimalUnit unit)
    {
        if (value is GameEventScriptDecimalValue { Unit: { } decimalUnit })
        {
            unit = decimalUnit;
            return true;
        }

        unit = default;
        return false;
    }

    private static bool HaveCompatibleNumericUnits(GameEventScriptValue left, GameEventScriptValue right)
        => TryGetDecimalUnit(left, out var leftUnit) == TryGetDecimalUnit(right, out var rightUnit) &&
           (!TryGetDecimalUnit(left, out _) || leftUnit == rightUnit);

    private static int CompareNumericUnits(GameEventScriptValue left, GameEventScriptValue right)
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

    internal static string FormatVector(GameEventScriptVectorValue value)
        => $"vector[x: {FormatDecimalComponent(value.X, value.Unit)}, y: {FormatDecimalComponent(value.Y, value.Unit)}, z: {FormatDecimalComponent(value.Z, value.Unit)}]";

    internal static string FormatPoint(GameEventScriptPointValue value)
        => $"point[x: {FormatDecimalComponent(value.X, value.Unit)}, y: {FormatDecimalComponent(value.Y, value.Unit)}, z: {FormatDecimalComponent(value.Z, value.Unit)}]";

    internal static string FormatDecimalComponent(decimal value, GameEventScriptDecimalUnit? unit = null)
    {
        var formatted = value.ToString("0.############################", CultureInfo.InvariantCulture);
        return unit.HasValue ? $"{formatted}{unit.Value.ToSuffix()}" : formatted;
    }

    internal static long ToIntegerPercentage(decimal ratio)
    {
        var percent = decimal.Truncate(ratio * 100m);
        if (percent < long.MinValue) return long.MinValue;
        if (percent > long.MaxValue) return long.MaxValue;
        return (long)percent;
    }
}
