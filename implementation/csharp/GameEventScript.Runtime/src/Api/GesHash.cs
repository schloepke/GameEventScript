// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;

namespace GameEventScript.Api;

/// <summary>A 20-byte hash, such as a SHA-1 handle version, held without allocation; equality and order follow its bytes.</summary>
public readonly struct GesHash20 : IEquatable<GesHash20>, IComparable<GesHash20>
{
    private readonly ulong _first;
    private readonly ulong _second;
    private readonly uint _third;

    private GesHash20(ulong first, ulong second, uint third)
    {
        _first = first;
        _second = second;
        _third = third;
    }

    /// <summary>The number of bytes.</summary>
    public const int Length = 20;

    /// <summary>Creates a hash from exactly 20 bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The hash.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bytes"/> does not hold exactly 20 bytes.</exception>
    public static GesHash20 FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length) throw new ArgumentException("A GesHash20 has 20 bytes.", nameof(bytes));
        return new GesHash20(GesHashBytes.Read64(bytes), GesHashBytes.Read64(bytes[8..]), (uint)GesHashBytes.Read(bytes[16..], 4));
    }

    /// <summary>Reads 40 lowercase or uppercase hexadecimal digits.</summary>
    /// <param name="hex">The digits.</param>
    /// <returns>The hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hex"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the text is not 40 hexadecimal digits.</exception>
    public static GesHash20 Parse(string hex)
    {
        Span<byte> bytes = stackalloc byte[Length];
        GesHashBytes.ParseHex(hex, bytes, nameof(hex));
        return FromBytes(bytes);
    }

    /// <summary>Copies the 20 bytes into a destination.</summary>
    /// <param name="destination">The destination, at least 20 bytes long.</param>
    /// <exception cref="ArgumentException">Thrown when the destination is shorter than 20 bytes.</exception>
    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < Length) throw new ArgumentException("The destination holds fewer than 20 bytes.", nameof(destination));
        GesHashBytes.Write(_first, destination, 8);
        GesHashBytes.Write(_second, destination[8..], 8);
        GesHashBytes.Write(_third, destination[16..], 4);
    }

    /// <summary>Writes the bytes as 40 lowercase hexadecimal digits.</summary>
    /// <returns>The hexadecimal text.</returns>
    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[Length];
        CopyTo(bytes);
        return GesHashBytes.Hex(bytes);
    }

    /// <summary>Determines whether another hash has the same bytes.</summary>
    /// <param name="other">The other hash.</param>
    /// <returns><see langword="true"/> when the bytes are equal.</returns>
    public bool Equals(GesHash20 other) => _first == other._first && _second == other._second && _third == other._third;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GesHash20 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_first, _second, _third);

    /// <summary>Compares the bytes in order.</summary>
    /// <param name="other">The other hash.</param>
    /// <returns>A negative number, zero or a positive number.</returns>
    public int CompareTo(GesHash20 other)
    {
        var comparison = _first.CompareTo(other._first);
        if (comparison != 0) return comparison;
        comparison = _second.CompareTo(other._second);
        return comparison != 0 ? comparison : _third.CompareTo(other._third);
    }

    /// <summary>Determines whether two hashes have the same bytes.</summary>
    /// <param name="left">The first hash.</param>
    /// <param name="right">The second hash.</param>
    /// <returns><see langword="true"/> when the bytes are equal.</returns>
    public static bool operator ==(GesHash20 left, GesHash20 right) => left.Equals(right);

    /// <summary>Determines whether two hashes differ.</summary>
    /// <param name="left">The first hash.</param>
    /// <param name="right">The second hash.</param>
    /// <returns><see langword="true"/> when the bytes differ.</returns>
    public static bool operator !=(GesHash20 left, GesHash20 right) => !left.Equals(right);
}

/// <summary>A 32-byte hash, such as a SHA-256 or content hash handle version, held without allocation; equality and order follow its bytes.</summary>
public readonly struct GesHash32 : IEquatable<GesHash32>, IComparable<GesHash32>
{
    private readonly ulong _first;
    private readonly ulong _second;
    private readonly ulong _third;
    private readonly ulong _fourth;

    private GesHash32(ulong first, ulong second, ulong third, ulong fourth)
    {
        _first = first;
        _second = second;
        _third = third;
        _fourth = fourth;
    }

    /// <summary>The number of bytes.</summary>
    public const int Length = 32;

    /// <summary>Creates a hash from exactly 32 bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The hash.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bytes"/> does not hold exactly 32 bytes.</exception>
    public static GesHash32 FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length) throw new ArgumentException("A GesHash32 has 32 bytes.", nameof(bytes));
        return new GesHash32(GesHashBytes.Read64(bytes), GesHashBytes.Read64(bytes[8..]), GesHashBytes.Read64(bytes[16..]), GesHashBytes.Read64(bytes[24..]));
    }

    /// <summary>Reads 64 lowercase or uppercase hexadecimal digits.</summary>
    /// <param name="hex">The digits.</param>
    /// <returns>The hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hex"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the text is not 64 hexadecimal digits.</exception>
    public static GesHash32 Parse(string hex)
    {
        Span<byte> bytes = stackalloc byte[Length];
        GesHashBytes.ParseHex(hex, bytes, nameof(hex));
        return FromBytes(bytes);
    }

    /// <summary>Copies the 32 bytes into a destination.</summary>
    /// <param name="destination">The destination, at least 32 bytes long.</param>
    /// <exception cref="ArgumentException">Thrown when the destination is shorter than 32 bytes.</exception>
    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < Length) throw new ArgumentException("The destination holds fewer than 32 bytes.", nameof(destination));
        GesHashBytes.Write(_first, destination, 8);
        GesHashBytes.Write(_second, destination[8..], 8);
        GesHashBytes.Write(_third, destination[16..], 8);
        GesHashBytes.Write(_fourth, destination[24..], 8);
    }

    /// <summary>Writes the bytes as 64 lowercase hexadecimal digits.</summary>
    /// <returns>The hexadecimal text.</returns>
    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[Length];
        CopyTo(bytes);
        return GesHashBytes.Hex(bytes);
    }

    /// <summary>Determines whether another hash has the same bytes.</summary>
    /// <param name="other">The other hash.</param>
    /// <returns><see langword="true"/> when the bytes are equal.</returns>
    public bool Equals(GesHash32 other) => _first == other._first && _second == other._second && _third == other._third && _fourth == other._fourth;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GesHash32 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_first, _second, _third, _fourth);

    /// <summary>Compares the bytes in order.</summary>
    /// <param name="other">The other hash.</param>
    /// <returns>A negative number, zero or a positive number.</returns>
    public int CompareTo(GesHash32 other)
    {
        var comparison = _first.CompareTo(other._first);
        if (comparison != 0) return comparison;
        comparison = _second.CompareTo(other._second);
        if (comparison != 0) return comparison;
        comparison = _third.CompareTo(other._third);
        return comparison != 0 ? comparison : _fourth.CompareTo(other._fourth);
    }

    /// <summary>Determines whether two hashes have the same bytes.</summary>
    /// <param name="left">The first hash.</param>
    /// <param name="right">The second hash.</param>
    /// <returns><see langword="true"/> when the bytes are equal.</returns>
    public static bool operator ==(GesHash32 left, GesHash32 right) => left.Equals(right);

    /// <summary>Determines whether two hashes differ.</summary>
    /// <param name="left">The first hash.</param>
    /// <param name="right">The second hash.</param>
    /// <returns><see langword="true"/> when the bytes differ.</returns>
    public static bool operator !=(GesHash32 left, GesHash32 right) => !left.Equals(right);
}

// Big-endian words, so that comparing the words compares the bytes in order.
internal static class GesHashBytes
{
    internal static ulong Read64(ReadOnlySpan<byte> bytes) => Read(bytes, 8);

    internal static ulong Read(ReadOnlySpan<byte> bytes, int count)
    {
        ulong value = 0;
        for (var index = 0; index < count; index++) value = (value << 8) | bytes[index];
        return value;
    }

    internal static void Write(ulong value, Span<byte> destination, int count)
    {
        for (var index = count - 1; index >= 0; index--)
        {
            destination[index] = (byte)value;
            value >>= 8;
        }
    }

    internal static void ParseHex(string hex, Span<byte> destination, string parameter)
    {
        if (hex is null) throw new ArgumentNullException(parameter);
        if (hex.Length != destination.Length * 2) throw new ArgumentException($"A hash of {destination.Length} bytes has {destination.Length * 2} hexadecimal digits.", parameter);
        for (var index = 0; index < destination.Length; index++) destination[index] = (byte)((Digit(hex[2 * index], parameter) << 4) | Digit(hex[(2 * index) + 1], parameter));
    }

    internal static string Hex(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        GesHandle.AppendHex(bytes, builder);
        return builder.ToString();
    }

    private static int Digit(char character, string parameter) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'a' and <= 'f' => character - 'a' + 10,
        >= 'A' and <= 'F' => character - 'A' + 10,
        _ => throw new ArgumentException("A hash is hexadecimal.", parameter)
    };
}
