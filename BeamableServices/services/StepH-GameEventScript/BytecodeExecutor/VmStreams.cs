using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmStreams
{
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamCreate(ref this VmValue dst, ref VmValue x)
    {
        if (x.TryCreateStream(out var stream)) dst.SetStream(stream);
        else dst.SetNothing();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamNext(ref this VmValue dst, ref VmValue stream, ushort noMoreAddress)
    {
        if (stream is not { Kind: Stream, ObjectValue: IVmStream it } || !it.TryNext(ref dst)) dst.OwningState.JumpAddress(noMoreAddress);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamClose(ref this VmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IDisposable it }) return;
        it.Dispose();
        iterator.SetNothing();
    }
    
}

internal interface IVmStream
{
    public bool TryNext(ref VmValue value);
}

internal class VmIntegerRangeStream(long from, long to, long step) : IVmStream, IDisposable
{
    private long _current = from;
    private bool _disposed = false;

    public bool TryNext(ref VmValue value)
    {
        if(_disposed)
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
        if(_disposed)
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

    public bool TryNext(ref VmValue value)
    {
        if(_list == null || _current >= _list.Length)
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

    public bool TryNext(ref VmValue value)
    {
        if(_values == null || _current >= _values.Length)
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
        if(_stringValue == null || _current >= _stringValue.Length)
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
        if(_values == null || _current >= _values.Length || !_values.TryGet(_current, out var x))
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