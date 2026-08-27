using System;

namespace StepH.GameEventScript.Runtime.Values;

internal interface IGesForwardIntegerSeries
{
    string SignatureId { get; }

    GesSeriesCursor? CreateCursor(long index);

    bool MoveNext(ref GesSeriesCursor cursor);

    long? ReadCursor(in GesSeriesCursor cursor);
}

internal interface IGesForwardDoubleSeries
{
    string SignatureId { get; }

    GesSeriesCursor? CreateCursor(long index);

    bool MoveNext(ref GesSeriesCursor cursor);

    double? ReadCursor(in GesSeriesCursor cursor);
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

        var offset = AddIndex(Offset, count) ?? long.MaxValue;
        return new GesSeries(_definition, offset, _hasCheckpoint, _checkpointIndex, in _checkpoint);
    }

    internal GesValue GetTerm(long index)
    {
        if (index < 0 || AddIndex(Offset, index) is not { } absoluteIndex)
        {
            return default;
        }

        if (_definition.IsRandomAccess)
        {
            return _definition.GetRandomTerm(absoluteIndex);
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
            if (_definition.CreateCursor(0) is not { } createdCursor)
            {
                return default;
            }

            cursor = createdCursor;
        }

        while (cursorIndex < absoluteIndex)
        {
            if (!_definition.MoveNext(ref cursor))
            {
                return default;
            }

            cursorIndex++;
        }

        return _definition.ReadCursorValue(in cursor);
    }

    internal static GesSeries Fibonacci()
    {
        var definition = FibonacciSeriesDefinition.Instance;
        var checkpoint = default(GesSeriesCursor);
        if (definition.CreateCursor(0) is { } cursor) checkpoint = cursor;
        return new GesSeries(definition, 0, true, 0, in checkpoint);
    }

    internal static GesSeries Factorial()
    {
        var definition = FactorialSeriesDefinition.Instance;
        var checkpoint = default(GesSeriesCursor);
        if (definition.CreateCursor(0) is { } cursor) checkpoint = cursor;
        return new GesSeries(definition, 0, true, 0, in checkpoint);
    }

    private static long? AddIndex(long left, long right)
    {
        try
        {
            var value = checked(left + right);
            return value >= 0 ? value : null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private abstract class GesSeriesDefinition
    {
        internal abstract string SignatureId { get; }

        internal abstract bool IsRandomAccess { get; }

        internal virtual GesValue GetRandomTerm(long index) => default;

        internal virtual GesSeriesCursor? CreateCursor(long index)
        {
            _ = index;
            return null;
        }

        internal virtual bool MoveNext(ref GesSeriesCursor cursor)
        {
            _ = cursor;
            return false;
        }

        internal virtual GesValue ReadCursorValue(in GesSeriesCursor cursor)
        {
            _ = cursor;
            return default;
        }
    }

    private sealed class FibonacciSeriesDefinition : GesSeriesDefinition, IGesForwardDoubleSeries
    {
        internal static FibonacciSeriesDefinition Instance { get; } = new();

        internal override string SignatureId => "fibonacci";

        string IGesForwardDoubleSeries.SignatureId => SignatureId;

        internal override bool IsRandomAccess => false;

        internal override GesSeriesCursor? CreateCursor(long index)
        {
            if (index != 0)
            {
                return null;
            }

            var cursor = default(GesSeriesCursor);
            cursor.Index = 0;
            cursor.Previous = 0d;
            cursor.Current = 0d;
            return cursor;
        }

        internal override bool MoveNext(ref GesSeriesCursor cursor)
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

        internal override GesValue ReadCursorValue(in GesSeriesCursor cursor)
        {
            var value = default(GesValue);
            if (((IGesForwardDoubleSeries)this).ReadCursor(in cursor) is not { } number)
            {
                return default;
            }

            value.SetFloat(number);
            return value;
        }

        GesSeriesCursor? IGesForwardDoubleSeries.CreateCursor(long index) => CreateCursor(index);

        bool IGesForwardDoubleSeries.MoveNext(ref GesSeriesCursor cursor) => MoveNext(ref cursor);

        double? IGesForwardDoubleSeries.ReadCursor(in GesSeriesCursor cursor)
            => cursor.Index > 1476 ? double.PositiveInfinity : cursor.Current;
    }

    private sealed class FactorialSeriesDefinition : GesSeriesDefinition, IGesForwardDoubleSeries
    {
        internal static FactorialSeriesDefinition Instance { get; } = new();

        internal override string SignatureId => "factorial";

        string IGesForwardDoubleSeries.SignatureId => SignatureId;

        internal override bool IsRandomAccess => false;

        internal override GesSeriesCursor? CreateCursor(long index)
        {
            if (index != 0)
            {
                return null;
            }

            var cursor = default(GesSeriesCursor);
            cursor.Index = 0;
            cursor.Current = 1d;
            return cursor;
        }

        internal override bool MoveNext(ref GesSeriesCursor cursor)
        {
            cursor.Index++;
            cursor.Current *= cursor.Index;
            return true;
        }

        internal override GesValue ReadCursorValue(in GesSeriesCursor cursor)
        {
            var value = default(GesValue);
            if (((IGesForwardDoubleSeries)this).ReadCursor(in cursor) is not { } number)
            {
                return default;
            }

            value.SetFloat(number);
            return value;
        }

        GesSeriesCursor? IGesForwardDoubleSeries.CreateCursor(long index) => CreateCursor(index);

        bool IGesForwardDoubleSeries.MoveNext(ref GesSeriesCursor cursor) => MoveNext(ref cursor);

        double? IGesForwardDoubleSeries.ReadCursor(in GesSeriesCursor cursor)
            => cursor.Index > 170 ? double.PositiveInfinity : cursor.Current;
    }

}

internal struct GesSeriesCursor
{
    internal long Index;
    internal double Previous;
    internal double Current;
}
