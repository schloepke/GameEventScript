using System;

namespace StepH.GameEventScript.VirtualMachine;

internal sealed class GesVmValueListBuilder
{
    private GesVmValue[] _items;

    internal GesVmValueListBuilder(int capacity = 0)
    {
        _items = new GesVmValue[capacity <= 0 ? 4 : capacity];
    }

    internal int Count { get; private set; }

    internal void Add(in GesVmValue value)
    {
        if (Count == _items.Length)
        {
            var resized = new GesVmValue[_items.Length << 1];
            Array.Copy(_items, resized, _items.Length);
            _items = resized;
        }

        _items[Count++] = value;
    }

    internal GesVmValue[] ToList()
    {
        if (Count == 0) return [];

        var list = new GesVmValue[Count];
        Array.Copy(_items, list, Count);
        return list;
    }
}
