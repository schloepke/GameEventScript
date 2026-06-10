#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.GameEventScript.VirtualMachine;

internal interface IGesVmIndexAccess<T>
{
    internal int Length { get; }
    internal bool TryGet(int index, out T value);
}

internal interface IGesVmKeyAccess<T>
{
    internal bool TryGet(string key, out T value);
}

internal class GesVmListObject(GesVmState ownerState, int size) : IGesVmIndexAccess<GesVmValue>
{
    internal readonly GesVmValue[] Items = ownerState.CreateRegisterArray(size);
    public int Length => Items.Length;

    public bool TryGet(int index, out GesVmValue value)
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

internal class GesVmMapObject(GesVmState ownerState, IReadOnlyDictionary<string, GesVmValue> entries) : IGesVmKeyAccess<GesVmValue>
{
    internal const string HiddenRecordTypeField = "__type";

    private readonly int _length = entries.Keys.Count(key => !key.StartsWith("_"));
    private GesVmListObject? _keys;
    private GesVmListObject? _values;
    private GesVmListObject? _entries;
    
    public int Length => _length;
    public IReadOnlyDictionary<string, GesVmValue> Entries => entries;
    
    internal GesVmListObject KeyList => _keys ??= CreateListOfKeys();
    internal GesVmListObject ValueList => _values ??= CreateListOfValues();
    internal GesVmListObject EntryList => _entries ??= CreateListOfEntries();

    public bool TryGet(string key, out GesVmValue value)
    {
        if (entries.TryGetValue(key, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private GesVmListObject CreateListOfKeys()
    {
        var list = new GesVmListObject(ownerState, _length);
        var i = 0;
        foreach (var key in entries.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (key.StartsWith("_")) continue;
            list.Items[i++].SetTag(key);
        }
        return list;
    }

    private GesVmListObject CreateListOfValues()
    {
        var list = new GesVmListObject(ownerState, _length);
        var i = 0;
        foreach (var key in entries.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (key.StartsWith("_")) continue;
            list.Items[i++] = entries[key];
        }
        return list;
    }

    private GesVmListObject CreateListOfEntries()
    {
        var list = new GesVmListObject(ownerState, _length);
        var i = 0;
        foreach (var key in entries.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (key.StartsWith("_")) continue;
            list.Items[i++].SetMap(new GesVmMapObject(ownerState, new Dictionary<string, GesVmValue> { ["key"] = ownerState.CreateTag(key), ["value"] = entries[key] }));
        }

        return list;
    }

}

internal class GesVmFloatTriplet(double x, double y, double z) : IGesVmIndexAccess<double>, IGesVmKeyAccess<double>
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

internal record GesVmRange(long from, long to, long step);
internal record GesVmFloatRange(double from, double to, double step);
