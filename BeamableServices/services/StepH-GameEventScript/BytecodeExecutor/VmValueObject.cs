#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.BytecodeExecutor;

public interface IVmIndexAccess<T>
{
    bool TryGet(int index, out T value);
}

public interface IVmKeyAccess<T>
{
    bool TryGet(string key, out T value);
}

public interface IVmLengthAccess
{
    int Length { get; }
}

public class VmListObject(int size) : IVmLengthAccess, IVmIndexAccess<VmValue>
{
    public static readonly VmListObject Empty = new(0);
    
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

public class VmDictionaryObject(IReadOnlyDictionary<string, VmValue> entries) : IVmLengthAccess, IVmKeyAccess<VmValue>
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

public class VmFloatTriplet(double x, double y, double z) : IVmLengthAccess, IVmIndexAccess<double>, IVmKeyAccess<double>
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

public interface IVmIterator
{
    public bool TryNext(ref VmValue value);
}

public class VmIntegerRangeIterator(long from, long to, long step) : IVmIterator, IDisposable
{
    private long _current = from;
    private bool disposed = false;

    public bool TryNext(ref VmValue value)
    {
        if(disposed)
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
        disposed = true;
    }
}