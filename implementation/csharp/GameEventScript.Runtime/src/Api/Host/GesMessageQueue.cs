// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Vm;
using static GameEventScript.Runtime.Vm.GesVirtualMachine.StateValue;

namespace GameEventScript.Api;

/// <summary>Growable FIFO ring of pending logical messages.</summary>
internal sealed class GesMessageQueue
{
    private GesPendingMessage[] _items;
    private int _head;
    private int _tail;

    internal GesMessageQueue(int capacity) => _items = new GesPendingMessage[Math.Max(1, capacity)];
    internal int Count { get; private set; }

    internal void Enqueue(GesPendingMessage item)
    {
        if (Count == _items.Length) Grow();
        _items[_tail] = item;
        _tail = (_tail + 1) % _items.Length;
        Count++;
    }

    internal bool Dequeue(out GesPendingMessage item)
    {
        if (Count == 0) { item = default; return false; }
        item = _items[_head];
        _items[_head] = default;
        _head = (_head + 1) % _items.Length;
        Count--;
        return true;
    }

    // Discards every entry. Each is reported in queue order before the queue changes, so a throwing observer cannot
    // leave it inconsistent.
    internal void Clear(IGesRuntimeObserver? observer)
    {
        if (observer is not null)
            for (var index = 0; index < Count; index++) observer.MessageDiscarded(_items[(_head + index) % _items.Length].Id);
        while (Dequeue(out _)) { }
    }

    // Removes entries a failed initialization left without recipients, reporting each in queue order first.
    internal void RemoveFailedRecipients(IGesRuntimeObserver? observer)
    {
        if (observer is not null)
        {
            for (var index = 0; index < Count; index++)
            {
                var item = _items[(_head + index) % _items.Length];
                if (!item.HasRecipients()) observer.MessageDiscarded(item.Id);
            }
        }

        var count = Count;
        for (var index = 0; index < count; index++)
            if (Dequeue(out var item) && item.HasRecipients()) Enqueue(item);
    }

    private void Grow()
    {
        var expanded = new GesPendingMessage[_items.Length * 2];
        for (var index = 0; index < Count; index++) expanded[index] = _items[(_head + index) % _items.Length];
        _items = expanded;
        _head = 0;
        _tail = Count;
    }
}
