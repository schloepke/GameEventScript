#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.GameEventScript.BytecodeExecutor;

internal interface IVmIndexAccess<T>
{
    internal int Length { get; }
    internal bool TryGet(int index, out T value);
}

internal interface IVmKeyAccess<T>
{
    internal bool TryGet(string key, out T value);
}

internal class VmListObject(VmState ownerState, int size) : IVmIndexAccess<VmValue>
{
    internal readonly VmValue[] Items = ownerState.CreateRegisterArray(size);
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

internal class VmMapObject(VmState ownerState, IReadOnlyDictionary<string, VmValue> entries) : IVmKeyAccess<VmValue>
{
    private VmListObject? _keys;
    private VmListObject? _values;
    private VmListObject? _entries;
    
    public int Length => entries.Count;
    public IReadOnlyDictionary<string, VmValue> Entries => entries;
    
    internal VmListObject KeyList => _keys ??= CreateListOfKeys();
    internal VmListObject ValueList => _values ??= CreateListOfValues();
    internal VmListObject EntryList => _entries ??= CreateListOfEntries();

    public bool TryGet(string key, out VmValue value)
    {
        if (entries.TryGetValue(key, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private VmListObject CreateListOfKeys()
    {
        var list = new VmListObject(ownerState, entries.Count);
        var i = 0;
        foreach (var key in entries.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            list.Items[i++].SetTag(key);
        }
        return list;
    }

    private VmListObject CreateListOfValues()
    {
        var list = new VmListObject(ownerState, entries.Count);
        var i = 0;
        foreach (var key in entries.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            list.Items[i++] = entries[key];
        }
        return list;
    }

    private VmListObject CreateListOfEntries()
    {
        var list = new VmListObject(ownerState, entries.Count);
        var i = 0;
        foreach (var key in entries.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            list.Items[i++].SetMap(new VmMapObject(ownerState, new Dictionary<string, VmValue> { ["key"] = VmValue.CreateTag(key), ["value"] = entries[key] }));
        }

        return list;
    }

}

internal class VmFloatTriplet(double x, double y, double z) : IVmIndexAccess<double>, IVmKeyAccess<double>
{
    internal readonly double X = x;
    internal readonly double Y = y;
    internal readonly double Z = z;

    public int Length => 3;

    public bool TryGet(int index, out double value)
    {
        switch (index)
        {
            case 0:
                value = X;
                return true;
            case 1:
                value = Y;
                return true;
            case 2:
                value = Z;
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
                value = X;
                return true;
            case "y":
                value = Y;
                return true;
            case "z":
                value = Z;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}

internal record VmRange(long from, long to, long step);
internal record VmFloatRange(double from, double to, double step);