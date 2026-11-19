// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using System.Threading.Tasks;
using GameEventScript.Api;

namespace GameEventScript.Network.Sockets;

/// <summary>
/// Waits for the work of an end whose transport is a socket: until one of its sockets received bytes, opened or ended,
/// a client became ready, or the end's <c>NextDeadline</c> passes, whichever is first. The caller polls after each
/// wait. A wait may end early; it never ends after the work arrived.
/// </summary>
public static class GesNetworkSocketWaits
{
    /// <summary>Blocks until the client end has work.</summary>
    /// <param name="end">A client end created with a <see cref="GesNetworkSocketConnector"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="end"/> is null.</exception>
    /// <exception cref="ArgumentException">The end's connector is no <see cref="GesNetworkSocketConnector"/>.</exception>
    public static void Wait(this GesNetworkClientEnd end) => Signal(end).Wait(Remaining(end.NextDeadline, end.Clock));

    /// <summary>Waits until the client end has work.</summary>
    /// <param name="end">A client end created with a <see cref="GesNetworkSocketConnector"/>.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the end has work.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="end"/> is null.</exception>
    /// <exception cref="ArgumentException">The end's connector is no <see cref="GesNetworkSocketConnector"/>.</exception>
    /// <exception cref="OperationCanceledException">The wait was cancelled.</exception>
    public static ValueTask WaitAsync(this GesNetworkClientEnd end, CancellationToken cancellationToken = default) =>
        Signal(end).WaitAsync(Remaining(end.NextDeadline, end.Clock), cancellationToken);

    /// <summary>Blocks until the server end has work.</summary>
    /// <param name="end">A server end created with a <see cref="GesNetworkSocketListener"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="end"/> is null.</exception>
    /// <exception cref="ArgumentException">The end's acceptor is no <see cref="GesNetworkSocketListener"/>.</exception>
    public static void Wait(this GesNetworkServerEnd end) => Signal(end).Wait(Remaining(end.NextDeadline, end.Clock));

    /// <summary>Waits until the server end has work.</summary>
    /// <param name="end">A server end created with a <see cref="GesNetworkSocketListener"/>.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the end has work.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="end"/> is null.</exception>
    /// <exception cref="ArgumentException">The end's acceptor is no <see cref="GesNetworkSocketListener"/>.</exception>
    /// <exception cref="OperationCanceledException">The wait was cancelled.</exception>
    public static ValueTask WaitAsync(this GesNetworkServerEnd end, CancellationToken cancellationToken = default) =>
        Signal(end).WaitAsync(Remaining(end.NextDeadline, end.Clock), cancellationToken);

    private static GesNetworkSocketSignal Signal(GesNetworkClientEnd end)
    {
        ArgumentNullException.ThrowIfNull(end);
        return end.Connector is GesNetworkSocketConnector connector
            ? connector.Signal
            : throw new ArgumentException("The end's connector is no socket connector.", nameof(end));
    }

    private static GesNetworkSocketSignal Signal(GesNetworkServerEnd end)
    {
        ArgumentNullException.ThrowIfNull(end);
        return end.Acceptor is GesNetworkSocketListener listener
            ? listener.Signal
            : throw new ArgumentException("The end's acceptor is no socket listener.", nameof(end));
    }

    private static long Remaining(long deadline, IGesClock clock) =>
        deadline == long.MaxValue ? long.MaxValue : deadline - clock.ElapsedMicroseconds;
}
