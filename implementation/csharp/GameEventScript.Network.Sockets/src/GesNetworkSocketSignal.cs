// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameEventScript.Network.Sockets;

// Wakes the thread that waits for an end when a socket of the end received bytes, opened, ended or a client became
// ready. A wake may be spurious; a missed one cannot happen, because the waiter resets the flag before it polls.
internal sealed class GesNetworkSocketSignal
{
    private readonly SemaphoreSlim _semaphore = new(0);
    private int _set;

    internal void Set()
    {
        if (Interlocked.Exchange(ref _set, 1) == 0) _semaphore.Release();
    }

    internal void Wait(long microseconds)
    {
        _semaphore.Wait(Milliseconds(microseconds));
        Volatile.Write(ref _set, 0);
    }

    internal async ValueTask WaitAsync(long microseconds, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(Milliseconds(microseconds), cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _set, 0);
    }

    // Whole milliseconds rounded up, so the wait never ends before the deadline; infinite for long.MaxValue.
    private static int Milliseconds(long microseconds) =>
        microseconds == long.MaxValue ? Timeout.Infinite : (int)Math.Min(int.MaxValue, (Math.Max(0, microseconds) + 999) / 1000);
}
