using System;
using System.Globalization;

namespace StepH.GameEventScript.Runtime.Values;

internal interface IGesRandomIntegerSeries
{
    string SignatureId { get; }

    bool TryCalc(long index, out long value);
}

internal interface IGesRandomDoubleSeries
{
    string SignatureId { get; }

    bool TryCalc(long index, out double value);
}

internal interface IGesForwardIntegerSeries
{
    string SignatureId { get; }

    bool TryCreateCursor(long index, ref GesSeriesCursor cursor);

    bool TryMoveNext(ref GesSeriesCursor cursor);

    bool TryReadCursor(in GesSeriesCursor cursor, out long value);
}

internal interface IGesForwardDoubleSeries
{
    string SignatureId { get; }

    bool TryCreateCursor(long index, ref GesSeriesCursor cursor);

    bool TryMoveNext(ref GesSeriesCursor cursor);

    bool TryReadCursor(in GesSeriesCursor cursor, out double value);
}

internal sealed class GesSeries
{
    private readonly GesSeriesDefinition _definition;
    private readonly bool _hasCheckpoint;
    private readonly long _checkpointIndex;
    private readonly GesSeriesCursor _checkpoint;

    private GesSeries(GesSeriesDefinition definition, long offset, bool hasCheckpoint, long checkpointIndex, in GesSeriesCursor checkpoint)
    {
        _definition = definition;
        Offset = offset < 0 ? 0 : offset;
        _hasCheckpoint = hasCheckpoint;
        _checkpointIndex = checkpointIndex < 0 ? 0 : checkpointIndex;
        _checkpoint = checkpoint;
    }

    internal string SignatureId => _definition.SignatureId;

    internal long Offset { get; }

    internal GesSeries Drop(long count)
    {
        if (count <= 0)
        {
            return this;
        }

        var offset = TryAddIndex(Offset, count, out var value) ? value : long.MaxValue;
        return new GesSeries(_definition, offset, _hasCheckpoint, _checkpointIndex, in _checkpoint);
    }

    internal bool TryGetTerm(long index, ref GesValue value)
    {
        if (index < 0 || !TryAddIndex(Offset, index, out var absoluteIndex))
        {
            value.SetNothing();
            return false;
        }

        if (_definition.IsRandomAccess)
        {
            return _definition.TryGetRandomTerm(absoluteIndex, ref value);
        }

        GesSeriesCursor cursor;
        var cursorIndex = 0L;
        if (_hasCheckpoint && _checkpointIndex <= absoluteIndex)
        {
            cursor = _checkpoint;
            cursorIndex = _checkpointIndex;
        }
        else
        {
            cursor = default;
            if (!_definition.TryCreateCursor(0, ref cursor))
            {
                value.SetNothing();
                return false;
            }
        }

        while (cursorIndex < absoluteIndex)
        {
            if (!_definition.TryMoveNext(ref cursor))
            {
                value.SetNothing();
                return false;
            }

            cursorIndex++;
        }

        return _definition.TryReadCursor(in cursor, ref value);
    }

    internal static GesSeries Natural(long start = 0, long step = 1)
    {
        var checkpoint = default(GesSeriesCursor);
        return new GesSeries(new NaturalSeriesDefinition(start, step), 0, false, 0, in checkpoint);
    }

    internal static GesSeries Fibonacci()
    {
        var definition = FibonacciSeriesDefinition.Instance;
        var checkpoint = default(GesSeriesCursor);
        definition.TryCreateCursor(0, ref checkpoint);
        return new GesSeries(definition, 0, true, 0, in checkpoint);
    }

    internal static GesSeries Factorial()
    {
        var definition = FactorialSeriesDefinition.Instance;
        var checkpoint = default(GesSeriesCursor);
        definition.TryCreateCursor(0, ref checkpoint);
        return new GesSeries(definition, 0, true, 0, in checkpoint);
    }

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

    private abstract class GesSeriesDefinition
    {
        internal abstract string SignatureId { get; }

        internal abstract bool IsRandomAccess { get; }

        internal virtual bool TryGetRandomTerm(long index, ref GesValue value)
        {
            value.SetNothing();
            return false;
        }

        internal virtual bool TryCreateCursor(long index, ref GesSeriesCursor cursor)
        {
            cursor = default;
            _ = index;
            return false;
        }

        internal virtual bool TryMoveNext(ref GesSeriesCursor cursor)
        {
            _ = cursor;
            return false;
        }

        internal virtual bool TryReadCursor(in GesSeriesCursor cursor, ref GesValue value)
        {
            value.SetNothing();
            return false;
        }
    }

    private sealed class NaturalSeriesDefinition(long start, long step) : GesSeriesDefinition, IGesRandomIntegerSeries, IGesRandomDoubleSeries
    {
        internal override string SignatureId { get; } = $"natural({start.ToString(CultureInfo.InvariantCulture)},{step.ToString(CultureInfo.InvariantCulture)})";

        string IGesRandomIntegerSeries.SignatureId => SignatureId;

        string IGesRandomDoubleSeries.SignatureId => SignatureId;

        internal override bool IsRandomAccess => true;

        internal override bool TryGetRandomTerm(long index, ref GesValue value)
        {
            if (index < 0)
            {
                value.SetNothing();
                return false;
            }

            if (((IGesRandomIntegerSeries)this).TryCalc(index, out var integerValue))
            {
                value.SetInteger(integerValue);
                return true;
            }

            ((IGesRandomDoubleSeries)this).TryCalc(index, out var doubleValue);
            value.SetFloat(doubleValue);
            return true;
        }

        bool IGesRandomIntegerSeries.TryCalc(long index, out long value)
        {
            try
            {
                value = checked(start + checked(step * index));
                return true;
            }
            catch (OverflowException)
            {
                value = 0;
                return false;
            }
        }

        bool IGesRandomDoubleSeries.TryCalc(long index, out double value)
        {
            value = start + (step * (double)index);
            return index >= 0;
        }
    }

    private sealed class FibonacciSeriesDefinition : GesSeriesDefinition, IGesForwardDoubleSeries
    {
        internal static FibonacciSeriesDefinition Instance { get; } = new();

        internal override string SignatureId => "fibonacci";

        string IGesForwardDoubleSeries.SignatureId => SignatureId;

        internal override bool IsRandomAccess => false;

        internal override bool TryCreateCursor(long index, ref GesSeriesCursor cursor)
        {
            if (index != 0)
            {
                return false;
            }

            cursor.Index = 0;
            cursor.Previous = 0d;
            cursor.Current = 0d;
            return true;
        }

        internal override bool TryMoveNext(ref GesSeriesCursor cursor)
        {
            if (cursor.Index == 0)
            {
                cursor.Index = 1;
                cursor.Previous = 0d;
                cursor.Current = 1d;
                return true;
            }

            var next = cursor.Previous + cursor.Current;
            cursor.Previous = cursor.Current;
            cursor.Current = next;
            cursor.Index++;
            return true;
        }

        internal override bool TryReadCursor(in GesSeriesCursor cursor, ref GesValue value)
        {
            ((IGesForwardDoubleSeries)this).TryReadCursor(in cursor, out var number);
            value.SetFloat(number);
            return true;
        }

        bool IGesForwardDoubleSeries.TryCreateCursor(long index, ref GesSeriesCursor cursor) => TryCreateCursor(index, ref cursor);

        bool IGesForwardDoubleSeries.TryMoveNext(ref GesSeriesCursor cursor) => TryMoveNext(ref cursor);

        bool IGesForwardDoubleSeries.TryReadCursor(in GesSeriesCursor cursor, out double value)
        {
            value = cursor.Index > 1476 ? double.PositiveInfinity : cursor.Current;
            return true;
        }
    }

    private sealed class FactorialSeriesDefinition : GesSeriesDefinition, IGesForwardDoubleSeries
    {
        internal static FactorialSeriesDefinition Instance { get; } = new();

        internal override string SignatureId => "factorial";

        string IGesForwardDoubleSeries.SignatureId => SignatureId;

        internal override bool IsRandomAccess => false;

        internal override bool TryCreateCursor(long index, ref GesSeriesCursor cursor)
        {
            if (index != 0)
            {
                return false;
            }

            cursor.Index = 0;
            cursor.Current = 1d;
            return true;
        }

        internal override bool TryMoveNext(ref GesSeriesCursor cursor)
        {
            cursor.Index++;
            cursor.Current *= cursor.Index;
            return true;
        }

        internal override bool TryReadCursor(in GesSeriesCursor cursor, ref GesValue value)
        {
            ((IGesForwardDoubleSeries)this).TryReadCursor(in cursor, out var number);
            value.SetFloat(number);
            return true;
        }

        bool IGesForwardDoubleSeries.TryCreateCursor(long index, ref GesSeriesCursor cursor) => TryCreateCursor(index, ref cursor);

        bool IGesForwardDoubleSeries.TryMoveNext(ref GesSeriesCursor cursor) => TryMoveNext(ref cursor);

        bool IGesForwardDoubleSeries.TryReadCursor(in GesSeriesCursor cursor, out double value)
        {
            value = cursor.Index > 170 ? double.PositiveInfinity : cursor.Current;
            return true;
        }
    }

}

internal struct GesSeriesCursor
{
    internal long Index;
    internal double Previous;
    internal double Current;
}
