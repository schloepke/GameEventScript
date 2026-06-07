using System;

namespace StepH.GameEventScript.BytecodeExecutor;

internal interface IVmStream
{
    public bool TryNext(ref VmValue value);
    public bool IsPatternSequence => false;
}

internal interface IVmStreamEntryEvaluator
{
    bool TryEvaluateStreamEntry(ushort entryAddress, ushort itemSlot, ref VmValue item, VmValue[]? captures, ref VmValue result);
}

internal class VmIntegerRangeStream(long from, long to, long step) : IVmStream, IDisposable
{
    private long _current = from;
    private bool _disposed = false;

    public bool TryNext(ref VmValue value)
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

internal class VmFloatRangeStream(double from, double to, double step) : IVmStream, IDisposable
{
    private double _current = from;
    private bool _disposed;

    public bool TryNext(ref VmValue value)
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

internal class VmListStream(VmListObject list) : IVmStream, IDisposable
{
    private int _current;
    private VmListObject? _list = list;
    public bool IsPatternSequence => true;

    public bool TryNext(ref VmValue value)
    {
        if (_list == null || _current >= _list.Length)
        {
            _list = null;
            value.SetNothing();
            return false;
        }

        value = _list.Items[_current++];
        return true;
    }

    public void Dispose()
    {
        _list = null;
    }
}

internal class VmIntStream(int[] values) : IVmStream, IDisposable
{
    private int _current;
    private int[]? _values = values;
    public bool IsPatternSequence => true;

    public bool TryNext(ref VmValue value)
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

internal class VmStringStream(string stringValue) : IVmStream, IDisposable
{
    private int _current;
    private string? _stringValue = stringValue;

    public bool TryNext(ref VmValue value)
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

internal class VmIndexAccessStream(IVmIndexAccess<double> indexAccess) : IVmStream, IDisposable
{
    private int _current;
    private IVmIndexAccess<double>? _values = indexAccess;

    public bool TryNext(ref VmValue value)
    {
        if (_values == null || _current >= _values.Length || !_values.TryGet(_current, out var x))
        {
            _values = null;
            value.SetNothing();
            return false;
        }

        value.SetFloat(x);
        _current++;
        return true;
    }

    public void Dispose()
    {
        _values = null;
    }
}

internal sealed class VmTransformStream(
    VmState ownerState,
    IVmStream source,
    IVmStreamEntryEvaluator evaluator,
    ushort entryAddress,
    ushort itemSlot,
    VmValue[] captures,
    bool filter) : IVmStream, IDisposable
{
    private IVmStream? _source = source;
    private VmValue _item = ownerState.CreateNothing();
    private VmValue _result = ownerState.CreateNothing();
    public bool IsPatternSequence { get; } = source.IsPatternSequence;

    public bool TryNext(ref VmValue value)
    {
        var stream = _source;
        if (stream is null)
        {
            value.SetNothing();
            return false;
        }

        while (stream.TryNext(ref _item))
        {
            if (!evaluator.TryEvaluateStreamEntry(entryAddress, itemSlot, ref _item, captures, ref _result))
            {
                value.SetNothing();
                return false;
            }

            if (filter)
            {
                _result.UpdatedTextTruthinessCache();
                if (!_result.IsTrue) continue;
                value = _item;
                return true;
            }

            value = _result;
            return true;
        }

        value.SetNothing();
        return false;
    }

    public void Dispose()
    {
        if (_source is IDisposable disposable) disposable.Dispose();
        _source = null;
        _item.SetNothing();
        _result.SetNothing();
    }
}
