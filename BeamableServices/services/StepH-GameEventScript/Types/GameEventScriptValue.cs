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
    Number,
    Boolean,
    Series,
    Range,
    Message,
    Handler,
    List,
    Map,
    Dice
}

public abstract class GameEventScriptValue : IComparable<GameEventScriptValue>, IEquatable<GameEventScriptValue>
{
    internal const string HiddenTypeKey = "__type";

    protected GameEventScriptValue()
    {
    }

    public abstract GameEventScriptValueKind Kind { get; }

    public virtual GameEventScriptBytecodeInstructionUnit Unit => GameEventScriptBytecodeInstructionUnit.UnitNone;

    public static IComparer<GameEventScriptValue> StableComparer { get; } = new StableGameEventScriptValueComparer();

    public bool IsNumber() => Kind is GameEventScriptValueKind.Number or GameEventScriptValueKind.Percentage;
    public bool IsNothing() => Kind == GameEventScriptValueKind.Nothing || IsNaN();
    public bool IsTag() => Kind == GameEventScriptValueKind.Tag;
    public bool IsInteger() => this is GameEventScriptNumberValue { IsIntegerValue: true };
    public bool IsFractional() => this is GameEventScriptNumberValue { IsFractionalValue: true };
    public bool IsText() => Kind == GameEventScriptValueKind.Text;
    public bool IsPercentage() => Kind == GameEventScriptValueKind.Percentage;
    public bool IsNumericUnit(GameEventScriptBytecodeInstructionUnit unit) => TryGetNumericUnit(this, out var valueUnit) && valueUnit == unit;
    public bool HasNumericUnit() => TryGetNumericUnit(this, out _);
    public bool HasUnit => Unit.IsNumericUnit();
    public bool IsVector() => Kind == GameEventScriptValueKind.Vector;
    public bool IsPoint() => Kind == GameEventScriptValueKind.Point;
    public bool IsSeries() => Kind == GameEventScriptValueKind.Series;
    public bool IsList() => Kind == GameEventScriptValueKind.List;
    public bool IsMap() => Kind == GameEventScriptValueKind.Map;
    public bool IsDice() => Kind == GameEventScriptValueKind.Dice;
    public bool IsRange() => Kind == GameEventScriptValueKind.Range;
    public bool IsMessage() => Kind == GameEventScriptValueKind.Message;
    public bool IsHandler() => Kind == GameEventScriptValueKind.Handler;

    public virtual string AsText() => string.Empty;

    public virtual bool AsBoolean() => false;

    public virtual long AsInteger() => 0;

    public virtual double AsNumber() => 0d;

    public bool IsNaN() => this is GameEventScriptNumberValue { IsNaNValue: true };
    public bool IsInfinity() => this is GameEventScriptNumberValue { IsInfinityValue: true };
    public bool IsNegativeInfinity() => this is GameEventScriptNumberValue { IsNegativeInfinityValue: true };

    public int CompareTo(GameEventScriptValue? other) => other is null ? 1 : StableComparer.Compare(this, other);

    public virtual IReadOnlyList<GameEventScriptValue> AsList() => [];

    public virtual IReadOnlyDictionary<string, GameEventScriptValue> AsMap() => GameEventScriptMapValue.EmptyView;

    public virtual GameEventScriptDiceValue AsDice() => GameEventScriptDiceValue.Empty;

    public virtual bool HasSemanticValue()
    {
        if (this is GameEventScriptTagValue tag)
        {
            return tag.Value.Length > 0 &&
                   (!TryConvertToNumber(out var numericValue) || numericValue.HasSemanticValue());
        }

        return true;
    }

    public virtual bool IsSemanticallyEmpty() => !HasSemanticValue();

    public GameEventScriptValue Lookup(GameEventScriptValue selector)
    {
        if (IsNothing() || selector.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (selector.Kind is GameEventScriptValueKind.Text or GameEventScriptValueKind.Tag)
        {
            var key = selector.AsText();
            if (!string.IsNullOrEmpty(key) && TryGetMapMember(key, out var value))
            {
                return value;
            }
        }

        return LookupCore(selector);
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

    public virtual bool TryGetMapMember(string key, out GameEventScriptValue value)
    {
        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    protected virtual GameEventScriptValue LookupCore(GameEventScriptValue selector)
    {
        if (Kind is GameEventScriptValueKind.Map or GameEventScriptValueKind.Message or GameEventScriptValueKind.Handler)
        {
            return GameEventScriptNothingValue.Instance;
        }

        return LookupSequential(AsList(), selector);
    }

    public bool TryGetCustomTypeName(out string typeName)
    {
        if (this is IGameEventScriptCustomTypeValue customTypeValue)
        {
            typeName = customTypeValue.CustomTypeName;
            return true;
        }

        if (this is GameEventScriptMapValue dictionary && dictionary.Storage.TryGetValue(HiddenTypeKey, out var marker) && marker.Kind == GameEventScriptValueKind.Tag)
        {
            typeName = marker.AsText();
            return true;
        }

        typeName = string.Empty;
        return false;
    }

    public bool TryGetExternalObject<T>(out T value)
    {
        if (TryGetExternalObject(typeof(T), out var externalObject) &&
            externalObject is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    public bool TryGetExternalObject(Type objectType, out object value)
    {
        _ = objectType ?? throw new ArgumentNullException(nameof(objectType));
        if (this is IGameEventScriptExternalObjectValue externalObject &&
            objectType.IsInstanceOfType(externalObject.Instance))
        {
            value = externalObject.Instance;
            return true;
        }

        value = default!;
        return false;
    }

    public string DescribeType()
    {
        if (this is GameEventScriptNumberValue { Unit: var unit } && unit.IsNumericUnit())
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
            GameEventScriptValueKind.Number => ":Number",
            GameEventScriptValueKind.Boolean => ":Boolean",
            GameEventScriptValueKind.Series => ":Series",
            GameEventScriptValueKind.Range => ":Range",
            GameEventScriptValueKind.Message => ":Message",
            GameEventScriptValueKind.Handler => ":Handler",
            GameEventScriptValueKind.List => ":List",
            GameEventScriptValueKind.Map => ":Map",
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
            GameEventScriptValueKind.Percentage => FormatPercentage(AsNumber()),
            GameEventScriptValueKind.Vector => FormatVector((GameEventScriptVectorValue)this),
            GameEventScriptValueKind.Point => FormatPoint((GameEventScriptPointValue)this),
            GameEventScriptValueKind.Number => FormatNumberValue((GameEventScriptNumberValue)this),
            GameEventScriptValueKind.Boolean => AsBoolean().ToString(),
            GameEventScriptValueKind.Series => $"series[{((GameEventScriptSeriesValue)this).SignatureId} offset {((GameEventScriptSeriesValue)this).Offset}]",
            GameEventScriptValueKind.Range => FormatRange((GameEventScriptRangeValue)this),
            GameEventScriptValueKind.Message => ((GameEventScriptMessageValue)this).Value.ToString(),
            GameEventScriptValueKind.Handler => $"handler {((GameEventScriptHandlerValue)this).Signature.SignatureId}",
            GameEventScriptValueKind.List => $"[{string.Join(", ", AsList().Select(x => x.ToString()))}]",
            GameEventScriptValueKind.Map => $"map[{string.Join(", ", AsMap().Select(x => $"{x.Key}: {x.Value}"))}]",
            GameEventScriptValueKind.Dice => $"dice[{string.Join(", ", AsDice().Rolls)}]",
            _ => Kind.ToString()
        };
    }

    public bool Equals(GameEventScriptValue? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (IsNumber() && other.IsNumber())
        {
            if (!HaveCompatibleNumericUnits(this, other))
            {
                return false;
            }

            if (IsNaN() || other.IsNaN()) return false;

            if (IsInfinity() || other.IsInfinity())
            {
                return IsInfinity() && other.IsInfinity() && IsNegativeInfinity() == other.IsNegativeInfinity();
            }

            return AsNumber() == other.AsNumber();
        }

        if (ReferenceEquals(this, other)) return true;
        if (Kind != other.Kind) return false;
        return Kind switch
        {
            GameEventScriptValueKind.Nothing => true,
            GameEventScriptValueKind.Tag => AsText() == other.AsText(),
            GameEventScriptValueKind.Text => AsText() == other.AsText(),
            GameEventScriptValueKind.Percentage => AsNumber() == other.AsNumber(),
            GameEventScriptValueKind.Vector => ((GameEventScriptVectorValue)this).X == ((GameEventScriptVectorValue)other).X &&
                                               ((GameEventScriptVectorValue)this).Y == ((GameEventScriptVectorValue)other).Y &&
                                               ((GameEventScriptVectorValue)this).Z == ((GameEventScriptVectorValue)other).Z &&
                                               ((GameEventScriptVectorValue)this).Unit == ((GameEventScriptVectorValue)other).Unit,
            GameEventScriptValueKind.Point => ((GameEventScriptPointValue)this).X == ((GameEventScriptPointValue)other).X &&
                                              ((GameEventScriptPointValue)this).Y == ((GameEventScriptPointValue)other).Y &&
                                              ((GameEventScriptPointValue)this).Z == ((GameEventScriptPointValue)other).Z &&
                                              ((GameEventScriptPointValue)this).Unit == ((GameEventScriptPointValue)other).Unit,
            GameEventScriptValueKind.Number => AsNumber() == other.AsNumber(),
            GameEventScriptValueKind.Boolean => AsBoolean() == other.AsBoolean(),
            GameEventScriptValueKind.Series => ((GameEventScriptSeriesValue)this).SignatureId == ((GameEventScriptSeriesValue)other).SignatureId &&
                                               ((GameEventScriptSeriesValue)this).Offset == ((GameEventScriptSeriesValue)other).Offset,
            GameEventScriptValueKind.Range => ((GameEventScriptRangeValue)this).FromNumber == ((GameEventScriptRangeValue)other).FromNumber &&
                                              ((GameEventScriptRangeValue)this).ToNumber == ((GameEventScriptRangeValue)other).ToNumber &&
                                              ((GameEventScriptRangeValue)this).StepNumber == ((GameEventScriptRangeValue)other).StepNumber,
            GameEventScriptValueKind.Message => ((GameEventScriptMessageValue)this).Value.Equals(((GameEventScriptMessageValue)other).Value),
            GameEventScriptValueKind.Handler => ((GameEventScriptHandlerValue)this).Signature.SignatureId == ((GameEventScriptHandlerValue)other).Signature.SignatureId,
            GameEventScriptValueKind.List => AsList().SequenceEqual(other.AsList()),
            GameEventScriptValueKind.Map => EqualsDictionary(AsMap(), other.AsMap()),
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
            if (TryGetNumericUnit(this, out var unit))
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
                hash.Add(AsNumber());
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
            case GameEventScriptValueKind.Series:
            {
                var series = (GameEventScriptSeriesValue)this;
                hash.Add(series.SignatureId, StringComparer.Ordinal);
                hash.Add(series.Offset);
                break;
            }
            case GameEventScriptValueKind.Range:
            {
                var range = (GameEventScriptRangeValue)this;
                hash.Add(range.FromNumber);
                hash.Add(range.ToNumber);
                hash.Add(range.StepNumber);
                break;
            }
            case GameEventScriptValueKind.Message:
                hash.Add(((GameEventScriptMessageValue)this).Value);
                break;
            case GameEventScriptValueKind.Handler:
                hash.Add(((GameEventScriptHandlerValue)this).Signature.SignatureId, StringComparer.Ordinal);
                break;
            case GameEventScriptValueKind.List:
                foreach (var item in AsList()) hash.Add(item);
                break;
            case GameEventScriptValueKind.Map:
                foreach (var pair in AsMap().OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    hash.Add(pair.Key, StringComparer.Ordinal);
                    hash.Add(pair.Value);
                }

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
    public static implicit operator GameEventScriptValue(double value) => GameEventScriptValueFactory.GesFloat(value);

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
            GameEventScriptValueKind.Series => 7,
            GameEventScriptValueKind.Range => 8,
            GameEventScriptValueKind.Message => 9,
            GameEventScriptValueKind.Handler => 10,
            GameEventScriptValueKind.List => 11,
            GameEventScriptValueKind.Map => 12,
            GameEventScriptValueKind.Dice => 13,
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
            GameEventScriptValueKind.Text or GameEventScriptValueKind.Tag => value.AsText(),
                GameEventScriptValueKind.Number => value.ToString(),
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
            if (left.Kind != right.Kind &&
                ((left.IsNumber() && right.Kind == GameEventScriptValueKind.Tag) ||
                 (left.Kind == GameEventScriptValueKind.Tag && right.IsNumber())))
            {
                return left.IsNumber() ? -1 : 1;
            }

            return left.Kind switch
            {
                GameEventScriptValueKind.Nothing => 0,
                GameEventScriptValueKind.Tag => StringComparer.Ordinal.Compare(left.AsText(), right.AsText()),
                GameEventScriptValueKind.Text => StringComparer.Ordinal.Compare(left.AsText(), right.AsText()),
                GameEventScriptValueKind.Percentage => left.AsNumber().CompareTo(right.AsNumber()),
                GameEventScriptValueKind.Vector => CompareSequence(left.AsList(), right.AsList()),
                GameEventScriptValueKind.Point => CompareSequence(left.AsList(), right.AsList()),
                GameEventScriptValueKind.Boolean => left.AsBoolean().CompareTo(right.AsBoolean()),
                GameEventScriptValueKind.Series => CompareSeries((GameEventScriptSeriesValue)left, (GameEventScriptSeriesValue)right),
                GameEventScriptValueKind.Range => CompareRange((GameEventScriptRangeValue)left, (GameEventScriptRangeValue)right),
                GameEventScriptValueKind.Message => CompareMessage((GameEventScriptMessageValue)left, (GameEventScriptMessageValue)right),
                GameEventScriptValueKind.Handler => CompareHandler((GameEventScriptHandlerValue)left, (GameEventScriptHandlerValue)right),
                GameEventScriptValueKind.List => CompareSequence(left.AsList(), right.AsList()),
                GameEventScriptValueKind.Map => CompareDictionary(left.AsMap(), right.AsMap()),
                GameEventScriptValueKind.Dice => CompareDice(left.AsDice(), right.AsDice()),
                _ => left.Kind.CompareTo(right.Kind)
            };
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

        private static int CompareSeries(GameEventScriptSeriesValue left, GameEventScriptSeriesValue right)
        {
            var bySignature = StringComparer.Ordinal.Compare(left.SignatureId, right.SignatureId);
            return bySignature != 0 ? bySignature : left.Offset.CompareTo(right.Offset);
        }

        private static int CompareRange(GameEventScriptRangeValue left, GameEventScriptRangeValue right)
        {
            var byFrom = left.FromNumber.CompareTo(right.FromNumber);
            if (byFrom != 0) return byFrom;
            var byTo = left.ToNumber.CompareTo(right.ToNumber);
            return byTo != 0 ? byTo : left.StepNumber.CompareTo(right.StepNumber);
        }

        private static int CompareMessage(GameEventScriptMessageValue left, GameEventScriptMessageValue right)
        {
            var bySignature = StringComparer.Ordinal.Compare(left.Value.SignatureId, right.Value.SignatureId);
            if (bySignature != 0) return bySignature;
            var byArguments = CompareDictionary(left.Value.Arguments, right.Value.Arguments);
            return byArguments != 0 ? byArguments : CompareTags(left.Value.Tags, right.Value.Tags);
        }

        private static int CompareTags(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            var byCount = left.Count.CompareTo(right.Count);
            if (byCount != 0) return byCount;

            for (var i = 0; i < left.Count; i++)
            {
                var byTag = StringComparer.Ordinal.Compare(left[i], right[i]);
                if (byTag != 0) return byTag;
            }

            return 0;
        }

        private static int CompareHandler(GameEventScriptHandlerValue left, GameEventScriptHandlerValue right) =>
            StringComparer.Ordinal.Compare(left.Signature.SignatureId, right.Signature.SignatureId);

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

    internal virtual bool TryConvertToMap(out GameEventScriptValue value)
    {
        value = default!;
        return false;
    }

    internal virtual bool TryConvertToDice(out GameEventScriptValue value)
    {
        value = default!;
        return false;
    }

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

    internal static long ToIntegerSaturated(double number)
    {
        number = Math.Truncate(number);
        if (number < long.MinValue) return long.MinValue;
        if (number > long.MaxValue) return long.MaxValue;
        return (long)number;
    }

    internal static bool IsHiddenKey(string key) => key != null && key.StartsWith("__", StringComparison.Ordinal);

    internal static string FormatPercentage(double ratio) => $"{(ratio * 100d).ToString("0.############################", CultureInfo.InvariantCulture)}%";

    internal static string FormatNumberValue(GameEventScriptNumberValue value)
    {
        if (value.IsNaNValue)
        {
            return "NaN";
        }

        if (value.IsInfinityValue)
        {
            return value.IsNegativeInfinityValue ? "-Infinity" : "Infinity";
        }

        var formatted = value.IsIntegerValue
            ? value.IntegerValue.ToString(CultureInfo.InvariantCulture)
            : value.NumberValue.ToString("0.############################", CultureInfo.InvariantCulture);
        return value.Unit.IsNumericUnit()
            ? $"{formatted}{value.Unit.ToSuffix()}"
            : formatted;
    }

    internal static string FormatRange(GameEventScriptRangeValue value)
        => $"range[{FormatRangeComponent(value.FromNumber)} to {FormatRangeComponent(value.ToNumber)} step {FormatRangeComponent(value.StepNumber)}]";

    private static string FormatRangeComponent(double value)
        => value.ToString("0.############################", CultureInfo.InvariantCulture);

    public static double WrapDegrees(double degrees)
    {
        var wrapped = degrees % 360d;
        if (wrapped < 0d)
        {
            wrapped += 360d;
        }

        return wrapped == 360d ? 0d : wrapped;
    }

    internal static bool TryGetNumericUnit(GameEventScriptValue value, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (value is GameEventScriptNumberValue { Unit: var floatUnit } && floatUnit.IsNumericUnit())
        {
            unit = floatUnit;
            return true;
        }

        unit = default;
        return false;
    }

    private static bool HaveCompatibleNumericUnits(GameEventScriptValue left, GameEventScriptValue right)
        => TryGetNumericUnit(left, out var leftUnit) == TryGetNumericUnit(right, out var rightUnit) &&
           (!TryGetNumericUnit(left, out _) || leftUnit == rightUnit);

    private static int CompareNumericUnits(GameEventScriptValue left, GameEventScriptValue right)
    {
        var leftHasUnit = TryGetNumericUnit(left, out var leftUnit);
        var rightHasUnit = TryGetNumericUnit(right, out var rightUnit);
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
        => $"vector[x: {FormatFloatComponent(value.X, value.Unit)}, y: {FormatFloatComponent(value.Y, value.Unit)}, z: {FormatFloatComponent(value.Z, value.Unit)}]";

    internal static string FormatPoint(GameEventScriptPointValue value)
        => $"point[x: {FormatFloatComponent(value.X, value.Unit)}, y: {FormatFloatComponent(value.Y, value.Unit)}, z: {FormatFloatComponent(value.Z, value.Unit)}]";

    internal static string FormatFloatComponent(double value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        var formatted = value.ToString("0.############################", CultureInfo.InvariantCulture);
        return unit.IsNumericUnit() ? $"{formatted}{unit.ToSuffix()}" : formatted;
    }

    internal static long ToIntegerPercentage(double ratio)
    {
        var percent = Math.Truncate(ratio * 100d);
        if (percent < long.MinValue) return long.MinValue;
        if (percent > long.MaxValue) return long.MaxValue;
        return (long)percent;
    }
}
