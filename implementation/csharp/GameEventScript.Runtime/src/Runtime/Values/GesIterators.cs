// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Syntax;

namespace GameEventScript.Runtime.Values;

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

internal sealed class GesIntegerRangeIterator(long from, long to, long step) : IGesIterator, IDisposable
{
    private long _current = from;
    private long _remaining = GesRangeMath.GetLength(from, to, step);
    private bool _disposed = false;

    public GesIteratorResult Next()
    {
        if (NextInteger() is not { } term) return default;

        var value = default(GesValue);
        value.SetInteger(term);
        return new GesIteratorResult(in value);
    }

    // Statically dispatched step used by VM dispatch to write the term in place; same sequence as
    // Next(). Null marks the end, mirroring the optional result of the Swift port.
    internal long? NextInteger()
    {
        if (_disposed || _remaining <= 0) return null;

        var term = _current;
        _remaining--;
        if (_remaining > 0) _current = unchecked(_current + step);
        return term;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

internal class GesFloatRangeIterator(double from, double to, double step) : IGesIterator, IDisposable
{
    private long _index;
    private long _remaining = GesRangeMath.GetLength(from, to, step);
    private bool _disposed;

    public GesIteratorResult Next()
    {
        if (_disposed || _remaining <= 0) return default;

        var value = default(GesValue);
        value.SetFloat(GesRangeMath.GetFloatTerm(from, to, step, _index++));
        _remaining--;
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
    private int _utf16Offset;
    private string? _stringValue = stringValue;

    public GesIteratorResult Next()
    {
        if (_stringValue == null || _utf16Offset >= _stringValue.Length)
        {
            _stringValue = null;
            return default;
        }

        var value = default(GesValue);
        var scalar = GesText.ScalarAtUtf16Offset(_stringValue, _utf16Offset);
        _utf16Offset += scalar.Length;
        value.SetText(scalar, 1);
        return new GesIteratorResult(in value);
    }

    public void Dispose()
    {
        _stringValue = null;
    }
}

internal class GesTripletIterator(GesSpatialValue triplet) : IGesIterator, IDisposable
{
    private int _current;
    private GesSpatialValue? _triplet = triplet;

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
