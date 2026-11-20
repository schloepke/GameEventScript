// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Security.Cryptography;

namespace GameEventScript.Network;

/// <summary>
/// A server's static X25519 key pair, which encrypts its sessions. The client receives the public key from outside the
/// protocol and pins it. Immutable; safe to share between connections.
/// </summary>
public sealed class GesNetworkKey
{
    private readonly byte[] _privateKey;
    private readonly byte[] _publicKey = new byte[GesNetworkX25519.KeyBytes];

    private GesNetworkKey(byte[] privateKey)
    {
        _privateKey = privateKey;
        GesNetworkX25519.PublicKey(_privateKey, _publicKey);
    }

    /// <summary>Generates a key pair from the system's cryptographic random generator.</summary>
    /// <returns>The new key pair.</returns>
    public static GesNetworkKey Generate()
    {
        var privateKey = new byte[GesNetworkX25519.KeyBytes];
        RandomNumberGenerator.Fill(privateKey);
        return new GesNetworkKey(privateKey);
    }

    /// <summary>Creates a key pair from a private key; X25519 clamps it.</summary>
    /// <param name="privateKey">The private key of 32 bytes; it is copied.</param>
    /// <returns>The key pair.</returns>
    /// <exception cref="ArgumentException">The private key does not hold 32 bytes.</exception>
    public static GesNetworkKey FromPrivateKey(ReadOnlySpan<byte> privateKey)
    {
        if (privateKey.Length != GesNetworkX25519.KeyBytes) throw new ArgumentException("A private key holds 32 bytes.", nameof(privateKey));
        return new GesNetworkKey(privateKey.ToArray());
    }

    /// <summary>Gets the public key of 32 bytes, which the client pins.</summary>
    public ReadOnlySpan<byte> PublicKey => _publicKey;

    /// <summary>Copies the private key of 32 bytes, for example to store it.</summary>
    /// <param name="destination">The target, at least 32 bytes.</param>
    /// <exception cref="ArgumentException">The target holds fewer than 32 bytes.</exception>
    public void CopyPrivateKey(Span<byte> destination)
    {
        if (destination.Length < GesNetworkX25519.KeyBytes) throw new ArgumentException("The target holds at least 32 bytes.", nameof(destination));
        _privateKey.CopyTo(destination);
    }

    internal ReadOnlySpan<byte> PrivateKey => _privateKey;
}
