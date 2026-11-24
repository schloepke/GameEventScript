// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Runtime.Values;

/// <summary>
/// An immutable List that holds only data: no external value anywhere inside, whose fields could change between
/// handlers. Equality and hashing follow GES List semantics. The default value is the empty list.
/// </summary>
public readonly struct GesDataList : IEquatable<GesDataList>, IReadOnlyList<GesValue>
{
    private readonly GesValue[]? _items;

    /// <summary>
    /// Creates a data list from a copy of <paramref name="items"/>, so later changes to the array do not affect it.
    /// </summary>
    /// <param name="items">The elements in order.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an element holds an external value anywhere inside.</exception>
    public GesDataList(GesValue[] items)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        for (var index = 0; index < items.Length; index++)
        {
            if (!GesDataMap.IsData(items[index])) throw new ArgumentException("A data list holds no external value.", nameof(items));
        }

        _items = items.Length == 0 ? null : (GesValue[])items.Clone();
    }

    private GesDataList(GesValue[] items, bool shared)
    {
        _ = shared;
        _items = items.Length == 0 ? null : items;
    }

    /// <summary>
    /// Gets the list without elements, equal to the default value.
    /// </summary>
    public static GesDataList Empty => default;

    /// <summary>
    /// Gets the number of elements.
    /// </summary>
    public int Length => _items?.Length ?? 0;

    int IReadOnlyCollection<GesValue>.Count => Length;

    /// <summary>
    /// Gets the element at a zero-based index without copying it.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is outside the elements.</exception>
    public ref readonly GesValue this[int index] => ref Items[index];

    GesValue IReadOnlyList<GesValue>.this[int index] => Items[index];

    private GesValue[] Items => _items ?? [];

    /// <summary>
    /// Returns the data list of a List value that holds only data, sharing its elements; every other kind and a List
    /// holding an external value anywhere inside give <see langword="null"/>.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <returns>The data list, or <see langword="null"/>.</returns>
    public static GesDataList? FromValue(in GesValue value)
        => value.ValueKind == GesBytecodeTypeKind.List && value.ObjectValue is GesValue[] items && GesDataMap.IsData(value) ? new GesDataList(items, true) : null;

    /// <summary>
    /// Returns the element at a zero-based index without copying it.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The element.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is outside the elements.</exception>
    public ref readonly GesValue ValueAt(int index) => ref Items[index];

    /// <summary>
    /// Returns a read-only view of the elements without copying them.
    /// </summary>
    /// <returns>The elements.</returns>
    public GesValueSlice AsList() => _items is null ? GesValueSlice.Empty : new GesValueSlice(_items);

    /// <summary>
    /// Returns the List value that holds the elements, without copying them.
    /// </summary>
    /// <returns>The List value.</returns>
    public GesValue ToValue()
    {
        var value = new GesValue();
        value.SetList(Items);
        return value;
    }

    /// <summary>
    /// Returns an enumerator over the elements in order.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public Enumerator GetEnumerator() => new(Items);

    IEnumerator<GesValue> IEnumerable<GesValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Compares the elements as GES compares two Lists.
    /// </summary>
    /// <param name="other">The other list.</param>
    /// <returns><see langword="true"/> when both hold equal elements.</returns>
    public bool Equals(GesDataList other) => ReferenceEquals(Items, other.Items) || ToValue().Equals(other.ToValue());

    /// <summary>
    /// Compares with another object.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal data list.</returns>
    public override bool Equals(object? obj) => obj is GesDataList other && Equals(other);

    /// <summary>
    /// Hashes the elements as GES hashes a List.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => ToValue().GetHashCode();

    /// <summary>
    /// Returns the GES text of the List.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString() => ToValue().ToString();

    /// <summary>
    /// Compares two data lists for equal elements.
    /// </summary>
    /// <param name="left">The left list.</param>
    /// <param name="right">The right list.</param>
    /// <returns><see langword="true"/> when both hold equal elements.</returns>
    public static bool operator ==(GesDataList left, GesDataList right) => left.Equals(right);

    /// <summary>
    /// Compares two data lists for different elements.
    /// </summary>
    /// <param name="left">The left list.</param>
    /// <param name="right">The right list.</param>
    /// <returns><see langword="true"/> when the elements differ.</returns>
    public static bool operator !=(GesDataList left, GesDataList right) => !left.Equals(right);

    /// <summary>
    /// Enumerates the elements of a <see cref="GesDataList"/> in order.
    /// </summary>
    public struct Enumerator : IEnumerator<GesValue>
    {
        private readonly GesValue[] _items;
        private int _index;

        internal Enumerator(GesValue[] items)
        {
            _items = items;
            _index = -1;
        }

        /// <summary>
        /// Gets the element at the current position.
        /// </summary>
        public readonly GesValue Current => _items[_index];

        readonly object IEnumerator.Current => Current;

        /// <summary>
        /// Advances to the next element.
        /// </summary>
        /// <returns><see langword="true"/> when another element exists.</returns>
        public bool MoveNext() => ++_index < _items.Length;

        /// <summary>
        /// Returns to the position before the first element.
        /// </summary>
        public void Reset() => _index = -1;

        /// <summary>
        /// Releases nothing; the enumerator owns no resources.
        /// </summary>
        public readonly void Dispose()
        {
        }
    }
}
