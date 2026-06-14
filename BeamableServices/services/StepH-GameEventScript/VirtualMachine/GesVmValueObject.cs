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

internal class GesVmMapObject(GesVmState ownerState, IReadOnlyDictionary<string, GesVmValue> entries) : IGesVmKeyAccess<GesVmValue>
{
    internal const string HiddenRecordTypeField = "__type";

    private readonly int _length = entries.Keys.Count(key => !key.StartsWith("_"));
    private GesVmValue[]? _keys;
    private GesVmValue[]? _values;
    private GesVmValue[]? _entries;
    
    public int Length => _length;
    public IReadOnlyDictionary<string, GesVmValue> Entries => entries;
    
    internal GesVmValue[] KeyList => _keys ??= CreateListOfKeys();
    internal GesVmValue[] ValueList => _values ??= CreateListOfValues();
    internal GesVmValue[] EntryList => _entries ??= CreateListOfEntries();

    public bool TryGet(string key, out GesVmValue value)
    {
        if (entries.TryGetValue(key, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private GesVmValue[] CreateListOfKeys()
    {
        var list = ownerState.CreateList(_length);
        var i = 0;
        foreach (var key in entries.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (key.StartsWith("_")) continue;
            list[i++].SetTag(key);
        }
        return list;
    }

    private GesVmValue[] CreateListOfValues()
    {
        var list = ownerState.CreateList(_length);
        var i = 0;
        foreach (var key in entries.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (key.StartsWith("_")) continue;
            list[i++] = entries[key];
        }
        return list;
    }

    private GesVmValue[] CreateListOfEntries()
    {
        var list = ownerState.CreateList(_length);
        var i = 0;
        foreach (var key in entries.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (key.StartsWith("_")) continue;
            list[i++].SetMap(new GesVmMapObject(ownerState, new Dictionary<string, GesVmValue> { ["key"] = ownerState.CreateTag(key), ["value"] = entries[key] }));
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
