#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptSeriesValue : GameEventScriptValue
{
    public static GameEventScriptSeriesValue Create(IGameEventScriptSeries series)
        => new(series ?? throw new ArgumentNullException(nameof(series)), 0);

    public static GameEventScriptSeriesValue Create(string signatureId, Func<long, GameEventScriptValue> termProvider)
        => Create(new DelegateSeries(signatureId, termProvider));

    public static GameEventScriptSeriesValue Natural(long start = 0, long step = 1)
        => Create(new NaturalSeries(start, step));

    public static GameEventScriptSeriesValue Fibonacci()
        => Create(FibonacciSeries.Instance);

    public static GameEventScriptSeriesValue Factorial()
        => Create(FactorialSeries.Instance);

    private GameEventScriptSeriesValue(IGameEventScriptSeries series, long offset)
    {
        Series = series;
        Offset = Math.Max(0, offset);
    }

    public IGameEventScriptSeries Series { get; }

    public long Offset { get; }

    public string SignatureId => Series.SignatureId;

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Series;

    public GameEventScriptSeriesValue Drop(long count)
    {
        if (count <= 0)
        {
            return this;
        }

        return TryAddIndex(Offset, count, out var newOffset)
            ? new GameEventScriptSeriesValue(Series, newOffset)
            : new GameEventScriptSeriesValue(Series, long.MaxValue);
    }

    public IReadOnlyList<GameEventScriptValue> Take(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        var values = new GameEventScriptValue[count];
        for (var index = 0; index < count; index++)
        {
            values[index] = GetTerm(index);
        }

        return values;
    }

    public GameEventScriptValue GetTerm(long index)
    {
        if (index < 0 || !TryAddIndex(Offset, index, out var absoluteIndex))
        {
            return GameEventScriptNothingValue.Instance;
        }

        return Series.TryGetTerm(absoluteIndex, out var value)
            ? value ?? GameEventScriptNothingValue.Instance
            : GameEventScriptNothingValue.Instance;
    }

    public GameEventScriptValue FirstTerm => GetTerm(0);

    public override string AsText() => FirstTerm.AsText();

    public override bool AsBoolean() => FirstTerm.AsBoolean();

    public override long AsInteger() => FirstTerm.AsInteger();

    public override double AsNumber() => FirstTerm.AsNumber();

    public override IReadOnlyList<GameEventScriptValue> AsList() => CreateReadOnlyList(new[] { FirstTerm });

    public override GameEventScriptDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : GameEventScriptDiceValue.Empty;

    public override bool HasSemanticValue() => FirstTerm.HasSemanticValue();

    public override bool IsSemanticallyEmpty() => FirstTerm.IsSemanticallyEmpty();

    public override IEnumerable<GameEventScriptValue> AsEnumerable()
    {
        yield return FirstTerm;
    }

    protected override GameEventScriptValue LookupCore(GameEventScriptValue selector) => GameEventScriptNothingValue.Instance;

    internal override bool TryConvertToNumber(out GameEventScriptValue value) => FirstTerm.TryConvertToNumber(out value);

    internal override bool TryConvertToInteger(out GameEventScriptValue value) => FirstTerm.TryConvertToInteger(out value);

    internal override bool TryConvertToText(out GameEventScriptValue value) => FirstTerm.TryConvertToText(out value);

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = GesList(AsList());
        return true;
    }

    internal override bool TryConvertToDice(out GameEventScriptValue value) => TryConvertSequenceToDice(AsEnumerable(), out value);

    private static bool TryAddIndex(long left, long right, out long value)
    {
        try
        {
            value = checked(left + right);
            return value >= 0;
        }
        catch (OverflowException)
        {
            value = 0;
            return false;
        }
    }

    private static GameEventScriptValue NumericTerm(double value)
    {
        if (double.IsNaN(value))
        {
            return GesFloatNaN();
        }

        if (double.IsPositiveInfinity(value))
        {
            return GesFloatInfinity();
        }

        if (double.IsNegativeInfinity(value))
        {
            return GesFloatNegativeInfinity();
        }

        return Math.Truncate(value) == value && value is >= long.MinValue and <= long.MaxValue
            ? GesInteger((long)value)
            : GesFloat(value);
    }

    private sealed class DelegateSeries(string signatureId, Func<long, GameEventScriptValue> termProvider) : IGameEventScriptSeries
    {
        private readonly Func<long, GameEventScriptValue> _termProvider = termProvider ?? throw new ArgumentNullException(nameof(termProvider));

        public string SignatureId { get; } = string.IsNullOrWhiteSpace(signatureId)
            ? "external"
            : signatureId;

        public bool TryGetTerm(long index, out GameEventScriptValue value)
        {
            if (index < 0)
            {
                value = GameEventScriptNothingValue.Instance;
                return false;
            }

            value = _termProvider(index) ?? GameEventScriptNothingValue.Instance;
            return true;
        }
    }

    private sealed class NaturalSeries(long start, long step) : IGameEventScriptSeries
    {
        public string SignatureId { get; } = $"natural({start.ToString(CultureInfo.InvariantCulture)},{step.ToString(CultureInfo.InvariantCulture)})";

        public bool TryGetTerm(long index, out GameEventScriptValue value)
        {
            if (index < 0)
            {
                value = GameEventScriptNothingValue.Instance;
                return false;
            }

            try
            {
                value = GesInteger(checked(start + checked(step * index)));
                return true;
            }
            catch (OverflowException)
            {
                value = NumericTerm(start + (step * (double)index));
                return true;
            }
        }
    }

    private sealed class FibonacciSeries : IGameEventScriptSeries
    {
        public static FibonacciSeries Instance { get; } = new();

        public string SignatureId => "fibonacci";

        public bool TryGetTerm(long index, out GameEventScriptValue value)
        {
            if (index < 0)
            {
                value = GameEventScriptNothingValue.Instance;
                return false;
            }

            if (index == 0)
            {
                value = GesInteger(0);
                return true;
            }

            if (index == 1)
            {
                value = GesInteger(1);
                return true;
            }

            if (index > 1476)
            {
                value = GesFloatInfinity();
                return true;
            }

            if (index <= 92)
            {
                var previous = 0L;
                var current = 1L;
                for (var term = 2L; term <= index; term++)
                {
                    var next = checked(previous + current);
                    previous = current;
                    current = next;
                }

                value = GesInteger(current);
                return true;
            }

            var previousDouble = 0d;
            var currentDouble = 1d;
            for (var term = 2L; term <= index; term++)
            {
                var next = previousDouble + currentDouble;
                previousDouble = currentDouble;
                currentDouble = next;
            }

            value = NumericTerm(currentDouble);
            return true;
        }
    }

    private sealed class FactorialSeries : IGameEventScriptSeries
    {
        public static FactorialSeries Instance { get; } = new();

        public string SignatureId => "factorial";

        public bool TryGetTerm(long index, out GameEventScriptValue value)
        {
            if (index < 0)
            {
                value = GameEventScriptNothingValue.Instance;
                return false;
            }

            if (index <= 20)
            {
                var result = 1L;
                for (var term = 2L; term <= index; term++)
                {
                    result = checked(result * term);
                }

                value = GesInteger(result);
                return true;
            }

            if (index > 170)
            {
                value = GesFloatInfinity();
                return true;
            }

            var doubleResult = 1d;
            for (var term = 2L; term <= index; term++)
            {
                doubleResult *= term;
            }

            value = NumericTerm(doubleResult);
            return true;
        }
    }
}
