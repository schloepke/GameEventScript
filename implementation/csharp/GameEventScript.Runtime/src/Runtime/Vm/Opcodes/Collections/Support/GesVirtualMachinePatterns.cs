// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private static bool IsStraightItems(GesValue[] items, int length)
    {
        if (length < 2) return false;
        var unique = new long[length];
        var count = 0;
        for (var i = 0; i < length; i++)
        {
            var number = items[i].AsNumeric;
            if (!double.IsFinite(number)) return false;
            var value = (long)number;
            var exists = false;
            for (var j = 0; j < count; j++) if (unique[j] == value) exists = true;
            if (!exists) unique[count++] = value;
        }

        if (count < 2) return false;
        for (var i = 1; i < count; i++)
        {
            var value = unique[i];
            var j = i - 1;
            while (j >= 0 && unique[j] > value)
            {
                unique[j + 1] = unique[j];
                j--;
            }
            unique[j + 1] = value;
        }
        for (var i = 1; i < count; i++) if (unique[i - 1] + 1 != unique[i]) return false;
        return true;
    }

    private static IteratorBuffer ReadIterator(IGesIterator iterator)
    {
        var buffer = new GesVmListBuilder(8);
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }

        return new IteratorBuffer(buffer.Items, buffer.Count);
    }

    private readonly record struct IteratorBuffer(GesValue[] Items, int Length);
}
