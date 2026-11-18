// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Security.Cryptography;
using Wire = GameEventScript.Network.GesNetworkWire;

namespace GameEventScript.Network;

/// <summary>What a server does with a datagram from an address it does not know.</summary>
public enum GesNetworkCookieResult
{
    /// <summary>The datagram is not a readable <c>hello</c> of at least 1200 bytes; the server ignores it.</summary>
    Drop,

    /// <summary>The cookie is empty or not valid; the server sends the <c>retry</c> the gate wrote.</summary>
    Retry,

    /// <summary>The cookie is valid; the server creates a connection and passes it the datagram.</summary>
    Accept,
}

/// <summary>
/// The address validation gate of a datagram server: it answers a client it does not know without keeping state, so a
/// forged source address gains no connection. A cookie is the time window number and an HMAC-SHA256 of it and the
/// address with a secret key; the current and the previous window are valid. Not thread-safe; one caller at a time.
/// </summary>
public sealed class GesNetworkCookies : IDisposable
{
    /// <summary>The number of bytes of a <c>retry</c> the gate writes.</summary>
    public const int RetryBytes = 3 + Wire.CookieBytes;

    private const int HashBytes = 16;
    private const int PrefixBytes = 12;

    private readonly HMACSHA256 _hmac;
    private readonly long _window;
    private readonly byte[] _hash = new byte[32];
    private byte[] _input = new byte[PrefixBytes + 18];

    /// <summary>Creates a gate.</summary>
    /// <param name="key">The secret key, exactly 32 bytes; it is copied.</param>
    /// <param name="windowMicroseconds">The length of a time window in microseconds, at least 1.</param>
    /// <exception cref="ArgumentException">The key does not hold 32 bytes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The window length is not positive.</exception>
    public GesNetworkCookies(ReadOnlySpan<byte> key, long windowMicroseconds)
    {
        if (key.Length != 32) throw new ArgumentException("The key holds 32 bytes.", nameof(key));
        if (windowMicroseconds < 1) throw new ArgumentOutOfRangeException(nameof(windowMicroseconds));
        _hmac = new HMACSHA256(key.ToArray());
        _window = windowMicroseconds;
        _input[0] = (byte)'G';
        _input[1] = (byte)'E';
        _input[2] = (byte)'S';
        _input[3] = (byte)'C';
    }

    /// <summary>Checks a datagram of an unknown address and writes the <c>retry</c> to send when one is due.</summary>
    /// <param name="packet">The received datagram.</param>
    /// <param name="address">The client's address as the transport gives it.</param>
    /// <param name="now">The current time in microseconds.</param>
    /// <param name="retry">The target of the <c>retry</c>, at least <see cref="RetryBytes"/> bytes; written only for <see cref="GesNetworkCookieResult.Retry"/>.</param>
    /// <returns>What the server does with the datagram.</returns>
    /// <exception cref="ArgumentException"><paramref name="retry"/> holds fewer than <see cref="RetryBytes"/> bytes.</exception>
    public GesNetworkCookieResult Check(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> address, long now, Span<byte> retry)
    {
        if (retry.Length < RetryBytes) throw new ArgumentException("The retry target holds at least 27 bytes.", nameof(retry));
        if (!FindCookie(packet, out var cookie)) return GesNetworkCookieResult.Drop;
        var window = Window(now);
        if (cookie.Length == Wire.CookieBytes)
        {
            var named = (long)Wire.ReadUInt64(cookie, 0);
            if ((named == window || (window != long.MinValue && named == window - 1)) && Matches(cookie, address)) return GesNetworkCookieResult.Accept;
        }

        retry[0] = Wire.Retry;
        retry[1] = Wire.CookieBytes;
        Wire.WriteUInt64(retry, 2, (ulong)window);
        Compute(window, address);
        _hash.AsSpan(0, HashBytes).CopyTo(retry.Slice(10));
        retry[2 + Wire.CookieBytes] = 0;
        return GesNetworkCookieResult.Retry;
    }

    /// <summary>Releases the key's HMAC instance.</summary>
    public void Dispose() => _hmac.Dispose();

    // The window number: the time divided by the window length, rounded toward negative infinity.
    private long Window(long now)
    {
        var quotient = now / _window;
        return now % _window < 0 ? quotient - 1 : quotient;
    }

    private bool Matches(ReadOnlySpan<byte> cookie, ReadOnlySpan<byte> address)
    {
        Compute((long)Wire.ReadUInt64(cookie, 0), address);
        var difference = 0;
        for (var index = 0; index < HashBytes; index++) difference |= cookie[8 + index] ^ _hash[index];
        return difference == 0;
    }

    private void Compute(long window, ReadOnlySpan<byte> address)
    {
        if (_input.Length < PrefixBytes + address.Length) Array.Resize(ref _input, PrefixBytes + address.Length);
        Wire.WriteUInt64(_input, 4, (ulong)window);
        address.CopyTo(_input.AsSpan(PrefixBytes));
        _hmac.TryComputeHash(_input.AsSpan(0, PrefixBytes + address.Length), _hash, out _);
    }

    // Reads the fields of a hello up to its cookie within their bounds; false when the datagram is to be dropped.
    private static bool FindCookie(ReadOnlySpan<byte> packet, out ReadOnlySpan<byte> cookie)
    {
        cookie = default;
        if (packet.Length < Wire.MinimumPacketBytes || packet[0] != Wire.Hello || Wire.ReadUInt32(packet, 1) != Wire.Magic) return false;
        var position = 5;
        if (Wire.ReadVarint(packet, ref position, out _) != 1 || packet.Length - position < 8) return false;
        position += 8;
        if (Wire.ReadVarint(packet, ref position, out var routes) != 1 || routes is < 1 or > Wire.MaximumRoutes || packet.Length - position < 8 * (int)routes) return false;
        position += 8 * (int)routes;
        // The packet, baseline and transfer limits and the compression mask.
        for (var field = 0; field < 4; field++)
        {
            if (Wire.ReadVarint(packet, ref position, out _) != 1) return false;
        }

        if (Wire.ReadVarint(packet, ref position, out var length) != 1 || length > Wire.MaximumCookieBytes || packet.Length - position < length) return false;
        cookie = packet.Slice(position, (int)length);
        return true;
    }
}
