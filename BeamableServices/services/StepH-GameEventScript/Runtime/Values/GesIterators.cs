using System;

namespace StepH.GameEventScript.Runtime.Values;

internal interface IGesIterator
{
    public bool TryNext(ref GesValue value);
    public bool IsPatternSequence => false;
}

internal class GesIntegerRangeIterator(long from, long to, long step) : IGesIterator, IDisposable
{
    private long _current = from;
    private bool _disposed = false;

    public bool TryNext(ref GesValue value)
    {
        if (_disposed)
        {
            value.SetNothing();
            return false;
        }

        if (!(step switch
            {
                > 0 => _current <= to,
                < 0 => _current >= to,
                _ => false
            })) return false;
        value.SetInteger(_current);
        _current += step;
        return true;
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

    public bool TryNext(ref GesValue value)
    {
        if (_disposed)
        {
            value.SetNothing();
            return false;
        }

        if (!(step switch
            {
                > 0 => _current <= to,
                < 0 => _current >= to,
                _ => false
            })) return false;
        value.SetFloat(_current);
        _current += step;
        return true;
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

    public bool TryNext(ref GesValue value)
    {
        if (_list == null || _current >= _list.Length)
        {
            _list = null;
            value.SetNothing();
            return false;
        }

        value = _list[_current++];
        return true;
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

    public bool TryNext(ref GesValue value)
    {
        if (_values == null || _current >= _values.Length)
        {
            _values = null;
            value.SetNothing();
            return false;
        }

        value.SetInteger(_values[_current++]);
        return true;
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

    public bool TryNext(ref GesValue value)
    {
        if (_stringValue == null || _current >= _stringValue.Length)
        {
            _stringValue = null;
            value.SetNothing();
            return false;
        }

        value.SetText(_stringValue[_current++].ToString());
        return true;
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

    public bool TryNext(ref GesValue value)
    {
        if (_triplet == null || _current >= 3)
        {
            _triplet = null;
            value.SetNothing();
            return false;
        }

        value.SetFloat(_current++ switch
        {
            0 => _triplet.X,
            1 => _triplet.Y,
            _ => _triplet.Z
        });
        return true;
    }

    public void Dispose()
    {
        _triplet = null;
    }
}
