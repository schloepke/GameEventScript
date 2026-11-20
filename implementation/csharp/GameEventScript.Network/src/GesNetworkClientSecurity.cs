// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Network;

/// <summary>
/// How a client protects its session: with the server's pinned public key, encrypted with a key the server sends during
/// the handshake, or not at all. Immutable; safe to share between connections.
/// </summary>
public sealed class GesNetworkClientSecurity
{
    private readonly byte[] _serverPublicKey;

    private GesNetworkClientSecurity(GesNetworkSecurity level, byte[] serverPublicKey)
    {
        Level = level;
        _serverPublicKey = serverPublicKey;
    }

    /// <summary>
    /// Gets the security of a client without encryption of its own, for an in-process transport or one that protects itself,
    /// such as TLS. A server with a key still encrypts the session.
    /// </summary>
    public static GesNetworkClientSecurity None { get; } = new(GesNetworkSecurity.None, []);

    /// <summary>
    /// Gets the security of a development client, which takes the key from the server's <c>retry</c> and reaches
    /// <see cref="GesNetworkSecurity.Encrypted"/>. It protects only against attackers who read but do not change packets.
    /// </summary>
    public static GesNetworkClientSecurity Development { get; } = new(GesNetworkSecurity.Encrypted, []);

    /// <summary>
    /// Creates the security of a client that pins the server's public key and reaches <see cref="GesNetworkSecurity.Verified"/>.
    /// </summary>
    /// <param name="serverPublicKey">The server's public key of 32 bytes; it is copied.</param>
    /// <returns>The security.</returns>
    /// <exception cref="ArgumentException">The key does not hold 32 bytes.</exception>
    public static GesNetworkClientSecurity Pinned(ReadOnlySpan<byte> serverPublicKey)
    {
        if (serverPublicKey.Length != GesNetworkX25519.KeyBytes) throw new ArgumentException("A server public key holds 32 bytes.", nameof(serverPublicKey));
        return new GesNetworkClientSecurity(GesNetworkSecurity.Verified, serverPublicKey.ToArray());
    }

    /// <summary>
    /// Gets the lowest level the client accepts: <see cref="GesNetworkSecurity.Verified"/> when pinned,
    /// <see cref="GesNetworkSecurity.Encrypted"/> for development, else <see cref="GesNetworkSecurity.None"/>.
    /// </summary>
    public GesNetworkSecurity Level { get; }

    /// <summary>Gets the pinned public key of 32 bytes; empty unless pinned.</summary>
    public ReadOnlySpan<byte> ServerPublicKey => _serverPublicKey;
}
