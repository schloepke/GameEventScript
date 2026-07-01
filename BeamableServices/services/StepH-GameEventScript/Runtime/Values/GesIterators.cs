using System;

namespace StepH.GameEventScript.Runtime.Values;

internal interface IGesIterator
{
    public GesIteratorResult Next();
    public bool IsPatternSequence => false;
}

internal readonly struct GesIteratorResult
{
    internal GesIteratorResult(in GesValue value)
    {
        HasValue = true;
        Value = value;
    }

    internal readonly bool HasValue;
    internal readonly GesValue Value;
}

internal class GesIntegerRangeIterator(long from, long to, long step) : IGesIterator, IDisposable
{
    private long _current = from;
    private bool _disposed = false;

    public GesIteratorResult Next()
    {
        if (_disposed) return default;

        if (!(step switch
            {
                > 0 => _current <= to,
                < 0 => _current >= to,
                _ => false
            })) return default;
        var value = default(GesValue);
        value.SetInteger(_current);
        _current += step;
        return new GesIteratorResult(in value);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

internal class GesFloatRangeIterator(double from, double to, double step) : IGesIterator, IDisposable
{
    private double _current = from;
    private bool _disposed;

    public GesIteratorResult Next()
    {
        if (_disposed) return default;

        if (!(step switch
            {
                > 0 => _current <= to,
                < 0 => _current >= to,
                _ => false
            })) return default;
        var value = default(GesValue);
        value.SetFloat(_current);
        _current += step;
        return new GesIteratorResult(in value);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

internal class GesListIterator(GesValue[] list) : IGesIterator, IDisposable
{
    private int _current;
    private GesValue[]? _list = list;
    public bool IsPatternSequence => true;

    public GesIteratorResult Next()
    {
        if (_list == null || _current >= _list.Length)
        {
            _list = null;
            return default;
        }

        return new GesIteratorResult(in _list[_current++]);
    }

    public void Dispose()
    {
        _list = null;
    }
}

internal class GesIntIterator(int[] values) : IGesIterator, IDisposable
{
    private int _current;
    private int[]? _values = values;
    public bool IsPatternSequence => true;

    public GesIteratorResult Next()
    {
        if (_values == null || _current >= _values.Length)
        {
            _values = null;
            return default;
        }

        var value = default(GesValue);
        value.SetInteger(_values[_current++]);
        return new GesIteratorResult(in value);
    }

    public void Dispose()
    {
        _values = null;
    }
}

internal class GesStringIterator(string stringValue) : IGesIterator, IDisposable
{
    private int _current;
    private string? _stringValue = stringValue;

    public GesIteratorResult Next()
    {
        if (_stringValue == null || _current >= _stringValue.Length)
        {
            _stringValue = null;
            return default;
        }

        var value = default(GesValue);
        value.SetText(_stringValue[_current++].ToString());
        return new GesIteratorResult(in value);
    }

    public void Dispose()
    {
        _stringValue = null;
    }
}

internal class GesTripletIterator(GesValueVectorPoint triplet) : IGesIterator, IDisposable
{
    private int _current;
    private GesValueVectorPoint? _triplet = triplet;

    public GesIteratorResult Next()
    {
        if (_triplet == null || _current >= 3)
        {
            _triplet = null;
            return default;
        }

        var value = default(GesValue);
        value.SetFloat(_current++ switch
        {
            0 => _triplet.X,
            1 => _triplet.Y,
            _ => _triplet.Z
        });
        return new GesIteratorResult(in value);
    }

    public void Dispose()
    {
        _triplet = null;
    }
}
