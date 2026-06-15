using System;

namespace StepH.GameEventScript.VirtualMachine;

internal sealed class GesVmValueListBuilder
{
    private readonly GesVmState _ownerState;
    private GesVmValue[] _items;

    internal GesVmValueListBuilder(GesVmState ownerState, int capacity = 0)
    {
        _ownerState = ownerState;
        _items = ownerState.CreateRegisterArray(capacity <= 0 ? 4 : capacity);
    }

    internal int Count { get; private set; }

    internal void Add(GesVmValue value)
    {
        if (Count == _items.Length)
        {
            var resized = _ownerState.CreateRegisterArray(_items.Length << 1);
            Array.Copy(_items, resized, _items.Length);
            _items = resized;
        }

        _items[Count++] = value;
    }

    internal GesVmValue[] ToList()
    {
        if (Count == 0) return _ownerState.EmptyList;

        var list = _ownerState.CreateList(Count);
        Array.Copy(_items, list, Count);
        return list;
    }
}
