#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.BytecodeExecutor;

internal interface IVmIndexAccess<T>
{
    internal bool TryGet(int index, out T value);
}

internal interface IVmKeyAccess<T>
{
    internal bool TryGet(string key, out T value);
}

internal interface IVmLengthAccess
{
    internal int Length { get; }
}

internal class VmListObject(int size) : IVmLengthAccess, IVmIndexAccess<VmValue>
{
    internal static readonly VmListObject Empty = new(0);
    
    internal VmValue[] Items = new VmValue[size];
    public int Length => Items.Length;

    public bool TryGet(int index, out VmValue value)
    {
        if (index < 0 || index >= Length)
        {
            value = default;
            return false;
        }

        value = Items[index];
        return true;
    }
}

internal class VmDictionaryObject(IReadOnlyDictionary<string, VmValue> entries) : IVmLengthAccess, IVmKeyAccess<VmValue>
{
    public int Length => entries.Count;

    public bool TryGet(string key, out VmValue value)
    {
        if (entries.TryGetValue(key, out value))
        {
            return true;
        }

        value = default;
        return false;
    }
}

internal class VmFloatTriplet(double x, double y, double z) : IVmLengthAccess, IVmIndexAccess<double>, IVmKeyAccess<double>
{
    public int Length => 3;

    public bool TryGet(int index, out double value)
    {
        switch (index)
        {
            case 0:
                value = x;
                return true;
            case 1:
                value = y;
                return true;
            case 2:
                value = z;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    public bool TryGet(string key, out double value)
    {
        switch (key)
        {
            case "x":
                value = x;
                return true;
            case "y":
                value = y;
                return true;
            case "z":
                value = z;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}

internal interface IVmIterator
{
    public bool TryNext(ref VmValue value);
}

internal class VmIntegerRangeIterator(long from, long to, long step) : IVmIterator, IDisposable
{
    private long _current = from;
    private bool _disposed = false;

    public bool TryNext(ref VmValue value)
    {
        if(_disposed)
        {
            // A disposed iterator cannot be used again and results in an error / nothing. This might be a bug in the VM itself so maybe we result in error halt?
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