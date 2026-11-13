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
    /// <param name="items">The elements to copy in order; <see langword="null"/> or an empty list yields an empty array. Element references
    /// are copied, not the referenced objects.</param>
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
    /// Gets the number of elements; zero for a default instance.
    /// </summary>
    public int Count => _items?.Length ?? 0;
    /// <summary>
    /// Gets the number of elements; the same as <see cref="Count"/>.
    /// </summary>
    public int Length => Count;
    /// <summary>
    /// Gets the element at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the array.</exception>
    public T this[int index] => (_items ?? [])[index];
    /// <summary>
    /// Returns a new enumerator over the elements in order.
    /// </summary>
    /// <returns>An enumerator over the elements; a default instance enumerates nothing.</returns>
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
