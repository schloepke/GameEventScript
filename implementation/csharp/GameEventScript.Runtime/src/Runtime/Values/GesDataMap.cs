// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Runtime.Values;

/// <summary>
/// An immutable plain Map that holds only data: no external value anywhere inside, whose fields could change between
/// handlers. Keys are unique and sorted by Unicode scalar values; equality and hashing follow GES Map semantics. The
/// default value is the empty map.
/// </summary>
public readonly struct GesDataMap : IEquatable<GesDataMap>, IEnumerable<KeyValuePair<string, GesValue>>
{
    private readonly GesValueMap? _map;

    /// <summary>
    /// Creates a data map that shares the entries of <paramref name="map"/>; the stored fields of a Record become plain
    /// entries.
    /// </summary>
    /// <param name="map">The entries.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an entry holds an external value anywhere inside.</exception>
    public GesDataMap(GesValueMap map)
    {
        if (map is null) throw new ArgumentNullException(nameof(map));
        for (var index = 0; index < map.Length; index++)
        {
            if (!IsData(map.ValueAt(index))) throw new ArgumentException("A data map holds no external value.", nameof(map));
        }

        _map = map;
    }

    private GesDataMap(GesValueMap map, bool shared)
    {
        _ = shared;
        _map = map;
    }

    /// <summary>
    /// Gets the map without entries, equal to the default value.
    /// </summary>
    public static GesDataMap Empty => default;

    /// <summary>
    /// Gets the number of entries.
    /// </summary>
    public int Length => _map?.Length ?? 0;

    /// <summary>
    /// Returns the data map of a plain Map value that holds only data, sharing its entries; a Record, an external value,
    /// every other kind and a Map holding an external value anywhere inside give <see langword="null"/>.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <returns>The data map, or <see langword="null"/>.</returns>
    public static GesDataMap? FromValue(in GesValue value)
        => value.ValueKind == GesBytecodeTypeKind.Map && value.CustomTypeName is null && value.AsMap() is { } map && IsData(value) ? new GesDataMap(map, true) : null;

    /// <summary>
    /// Looks up an exact scalar key, distinguishing absence from a stored Nothing.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The value, or <see langword="null"/> when the key is absent.</returns>
    public GesValue? Get(string key) => _map?.Get(key);

    /// <summary>
    /// Tests whether a scalar-exact key is present, including keys that store Nothing.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key is present.</returns>
    public bool ContainsKey(string key) => _map?.ContainsKey(key) ?? false;

    /// <summary>
    /// Returns the key at a zero-based index in canonical key order.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The key.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is outside the entries.</exception>
    public string KeyAt(int index) => AsMap().KeyAt(index);

    /// <summary>
    /// Returns the value at a zero-based index, in the same order as <see cref="KeyAt"/>.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The value.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is outside the entries.</exception>
    public GesValue ValueAt(int index) => AsMap().ValueAt(index);

    /// <summary>
    /// Returns the entries without copying them.
    /// </summary>
    /// <returns>The entries.</returns>
    public GesValueMap AsMap() => _map ?? GesValueMap.Empty;

    /// <summary>
    /// Returns the Map value that holds the entries, without copying them.
    /// </summary>
    /// <returns>The Map value.</returns>
    public GesValue ToValue() => GesValue.GesMap(AsMap());

    /// <summary>
    /// Returns an enumerator over the entries in canonical key order.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public GesValueMap.Enumerator GetEnumerator() => AsMap().GetEnumerator();

    IEnumerator<KeyValuePair<string, GesValue>> IEnumerable<KeyValuePair<string, GesValue>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Compares the entries as GES compares two Maps.
    /// </summary>
    /// <param name="other">The other map.</param>
    /// <returns><see langword="true"/> when both hold equal entries.</returns>
    public bool Equals(GesDataMap other) => ReferenceEquals(AsMap(), other.AsMap()) || ToValue().Equals(other.ToValue());

    /// <summary>
    /// Compares with another object.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal data map.</returns>
    public override bool Equals(object? obj) => obj is GesDataMap other && Equals(other);

    /// <summary>
    /// Hashes the entries as GES hashes a Map.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => ToValue().GetHashCode();

    /// <summary>
    /// Returns the GES text of the Map.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString() => ToValue().ToString();

    /// <summary>
    /// Compares two data maps for equal entries.
    /// </summary>
    /// <param name="left">The left map.</param>
    /// <param name="right">The right map.</param>
    /// <returns><see langword="true"/> when both hold equal entries.</returns>
    public static bool operator ==(GesDataMap left, GesDataMap right) => left.Equals(right);

    /// <summary>
    /// Compares two data maps for different entries.
    /// </summary>
    /// <param name="left">The left map.</param>
    /// <param name="right">The right map.</param>
    /// <returns><see langword="true"/> when the entries differ.</returns>
    public static bool operator !=(GesDataMap left, GesDataMap right) => !left.Equals(right);

    // Data holds no external value, whose fields may change between handlers, anywhere inside: not in List items, Map
    // entries, Record fields or Message arguments.
    internal static bool IsData(in GesValue value)
    {
        if (value.ExternalValue is not null) return false;
        if (value.ValueKind == GesBytecodeTypeKind.List)
        {
            var items = value.AsList();
            for (var index = 0; index < items.Length; index++)
            {
                if (!IsData(items[index])) return false;
            }
        }
        else if (value.AsMap() is { } map)
        {
            for (var index = 0; index < map.Length; index++)
            {
                if (!IsData(map.ValueAt(index))) return false;
            }
        }
        else if (value.Message is { } message)
        {
            for (var index = 0; index < message.Arguments.Count; index++)
            {
                if (!IsData(message.Arguments.ValueAt(index))) return false;
            }
        }

        return true;
    }
}
