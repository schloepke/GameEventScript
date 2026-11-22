// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameEventScript.Network.Sockets;

// The time limits of the socket transports, equal in both ports.
internal static class GesNetworkSocketTimeouts
{
    // A TCP connection opens within this, so a server whose packets vanish fails as soon as on SwiftNIO instead of after
    // the system's retries.
    internal static readonly TimeSpan Connect = TimeSpan.FromSeconds(10);

    // Connecting, TLS and the WebSocket upgrade together complete within this, on a client and on a listener.
    internal static readonly TimeSpan Upgrade = TimeSpan.FromSeconds(10);

    // A disposed session of a closed connection writes its last packets and ends in order within this.
    internal static readonly TimeSpan OrderlyEnd = TimeSpan.FromSeconds(10);

    // Runs an operation within a time limit. Returns false when the limit ended it; a cancellation by the caller stays an
    // OperationCanceledException.
    internal static async Task<bool> WithinAsync(Func<CancellationToken, Task> operation, TimeSpan limit, CancellationToken cancellationToken)
    {
        // A limit that has run out ends the operation before it starts, even one that would complete at once.
        cancellationToken.ThrowIfCancellationRequested();
        if (limit <= TimeSpan.Zero) return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limit);
        try
        {
            await operation(timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
