// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Network;

/// <summary>
/// How a server protects its sessions: with its static key, or not at all. Immutable; safe to share between connections.
/// </summary>
public sealed class GesNetworkServerSecurity
{
    private GesNetworkServerSecurity(GesNetworkKey? key) => ServerKey = key;

    /// <summary>
    /// Gets the security of a server without a key, for an in-process transport or one that protects itself, such as TLS.
    /// It closes a client that asks for encryption with <see cref="GesNetworkCodes.Insecure"/>.
    /// </summary>
    public static GesNetworkServerSecurity None { get; } = new(null);

    /// <summary>Creates the security of a server that encrypts every session with its key and never accepts a plain <c>hello</c>.</summary>
    /// <param name="key">The server's key.</param>
    /// <returns>The security.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public static GesNetworkServerSecurity Key(GesNetworkKey key) => new(key ?? throw new ArgumentNullException(nameof(key)));

    /// <summary>Gets the server's key; null for <see cref="None"/>.</summary>
    public GesNetworkKey? ServerKey { get; }
}
