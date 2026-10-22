// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace GameEventScript.Api;

/// <summary>
/// Immutable array view used by portable programs and other immutable API models.
/// </summary>
public readonly struct GesReadOnlyArray<T> : IReadOnlyList<T>
{
    private readonly T[]? _items;

    private GesReadOnlyArray(T[] ownedItems) => _items = ownedItems;

    // The caller transfers exclusive ownership; no mutable reference may escape or be retained.
    internal static GesReadOnlyArray<T> FromOwnedArray(T[] items) => new(items);

    /// <summary>
    /// Takes an immutable snapshot, reusing storage only when the input is already a GesReadOnlyArray of the same element type.
    /// </summary>
    /// <param name="items">The items value.</param>
    public GesReadOnlyArray(IReadOnlyList<T>? items)
    {
        if (items is GesReadOnlyArray<T> immutable)
        {
            _items = immutable._items;
            return;
        }

        if (items is null || items.Count == 0)
        {
            _items = [];
            return;
        }

        _items = new T[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            _items[index] = items[index];
        }
    }

    /// <summary>
    /// Gets the count.
    /// </summary>
    public int Count => _items?.Length ?? 0;
    /// <summary>
    /// Gets the length.
    /// </summary>
    public int Length => Count;
    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The index value.</param>
    public T this[int index] => (_items ?? [])[index];
    /// <summary>
    /// Gets the enumerator.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? [])).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal ReadOnlySpan<T> AsSpan() => _items;

    internal string DecodeUtf8(Encoding encoding, int start, int length)
    {
        if (typeof(T) != typeof(byte)) throw new InvalidOperationException("Only byte arrays can be decoded as UTF-8.");
        var bytes = (byte[])(object)(_items ?? Array.Empty<T>());
        return encoding.GetString(bytes, start, length);
    }
}
