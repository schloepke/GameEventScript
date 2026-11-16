// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Api;

// The delayed messages of one host as a binary min-heap ordered by deadline, then by send ID. Send IDs grow in scheduling
// order, so equal deadlines keep their enqueue order (specs/HostRuntime.md#delayed-dispatch). Scheduling and promoting one
// message cost O(log n) whatever order the deadlines arrive in; the rare discards sort a copy to report in deadline order.
internal sealed class GesDelayedQueue
{
    private GesDelayedMessage[] _items = new GesDelayedMessage[8];

    internal int Count { get; private set; }

    internal GesDelayedMessage First => _items[0];

    internal void Add(in GesDelayedMessage item)
    {
        if (Count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        var index = Count++;
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (!Before(in item, in _items[parent])) break;
            _items[index] = _items[parent];
            index = parent;
        }

        _items[index] = item;
    }

    internal GesDelayedMessage RemoveFirst()
    {
        var first = _items[0];
        var last = _items[--Count];
        _items[Count] = default;
        if (Count > 0) SiftDown(0, in last);
        return first;
    }

    // Reports in deadline order: a copy sorted by the heap order.
    internal GesDelayedMessage[] InOrder()
    {
        var items = new GesDelayedMessage[Count];
        Array.Copy(_items, items, Count);
        Array.Sort(items, static (left, right) => Before(in left, in right) ? -1 : Before(in right, in left) ? 1 : 0);
        return items;
    }

    internal void RemoveAll(Func<GesDelayedMessage, bool> match)
    {
        var kept = 0;
        for (var index = 0; index < Count; index++)
        {
            if (!match(_items[index])) _items[kept++] = _items[index];
        }

        Array.Clear(_items, kept, Count - kept);
        Count = kept;
        for (var index = Count / 2 - 1; index >= 0; index--) SiftDown(index, in _items[index]);
    }

    internal void Clear()
    {
        Array.Clear(_items, 0, Count);
        Count = 0;
    }

    private void SiftDown(int index, in GesDelayedMessage item)
    {
        var moving = item;
        while (true)
        {
            var child = index * 2 + 1;
            if (child >= Count) break;
            if (child + 1 < Count && Before(in _items[child + 1], in _items[child])) child++;
            if (!Before(in _items[child], in moving)) break;
            _items[index] = _items[child];
            index = child;
        }

        _items[index] = moving;
    }

    private static bool Before(in GesDelayedMessage left, in GesDelayedMessage right) => left.Deadline < right.Deadline || left.Deadline == right.Deadline && left.Id < right.Id;
}
