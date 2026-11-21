// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Network;

/// <summary>
/// A client end and a server end connected within one process, without sockets. A realtime pair runs on the system
/// clock and moves packets when the ends poll. A synchronous pair runs on a virtual clock, is connected when created and
/// moves every packet before a send, publish, tick, close or poll returns: what one end sends is reported by the other
/// end's next poll. In both modes a packet waits while the receiving connection takes no further bytes, in a synchronous
/// pair until the host advances the clock far enough to refill its frame bucket; while 64 packets wait, the sending
/// connection keeps its further packets. Both ends of a pair belong to one caller at a time.
/// </summary>
public sealed class GesNetworkPair : IDisposable
{
    private readonly IGesClock _clock;
    private readonly GesVirtualClock? _virtual;
    private readonly int _packetBytes;
    private readonly Stack<byte[]> _pool = new();
    private readonly List<Link> _links = [];
    private readonly Queue<Link> _accepting = new();
    private bool _exchanging;

    private GesNetworkPair(GesBoundNetwork network, ReadOnlySpan<byte> token, GesNetworkOptions? options, bool synchronous)
    {
        options ??= GesNetworkOptions.InProcess();
        if (synchronous)
        {
            // The virtual clock stands still between sends, so a delayed acknowledgement would only hold the queue.
            options = options.Copy();
            options.AckDelayMicroseconds = 0;
            _clock = _virtual = new GesVirtualClock();
        }
        else
        {
            _clock = new GesSystemClock();
        }

        var connector = new Connector(this, options);
        Client = new GesNetworkClientEnd(network, connector, options, token, _clock);
        _packetBytes = Client.Connection.MaxPacketBytes;
        Server = new GesNetworkServerEnd(network, new Acceptor(this, network, options), _clock);
        if (!synchronous) return;
        Client.Pair = this;
        Server.Pair = this;
        // The handshake runs now; the connected events wait for the first polls.
        var silent = new GesNetworkEnds.Silent();
        for (var round = 0; round < 16 && Client.Connection.State == GesNetworkState.Hello; round++)
        {
            Client.PollCore(ref silent, report: false);
            Server.PollCore(ref silent, report: false);
        }

        if (Client.Connection.State != GesNetworkState.Connected)
            throw new InvalidOperationException("The handshake of the pair did not complete: " + Client.Connection.State + ".");
    }

    /// <summary>Creates a realtime pair on the system clock. The ends connect while they poll.</summary>
    /// <param name="network">The bound network both ends share.</param>
    /// <param name="token">The token the server end reports with the client's <c>connecting</c> event.</param>
    /// <param name="options">The options of both connections; <see cref="GesNetworkOptions.InProcess"/> when null.</param>
    /// <returns>The pair.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="network"/> is null.</exception>
    /// <exception cref="ArgumentException">The options are invalid.</exception>
    public static GesNetworkPair Realtime(GesBoundNetwork network, ReadOnlySpan<byte> token, GesNetworkOptions? options = null) =>
        new(network, token, options, synchronous: false);

    /// <summary>
    /// Creates a synchronous pair on a virtual clock that starts at 0. It is connected when this returns, and both
    /// ends report their connected events on their first polls. The server accepts the client without a
    /// <c>connecting</c> event. Both connections acknowledge at once: the pair uses the options with
    /// <see cref="GesNetworkOptions.AckDelayMicroseconds"/> 0.
    /// </summary>
    /// <param name="network">The bound network both ends share.</param>
    /// <param name="token">The client's token.</param>
    /// <param name="options">The options of both connections; <see cref="GesNetworkOptions.InProcess"/> when null.</param>
    /// <returns>The connected pair.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="network"/> is null.</exception>
    /// <exception cref="ArgumentException">The options are invalid.</exception>
    public static GesNetworkPair Synchronous(GesBoundNetwork network, ReadOnlySpan<byte> token, GesNetworkOptions? options = null) =>
        new(network, token, options, synchronous: true);

    /// <summary>Gets the client end.</summary>
    public GesNetworkClientEnd Client { get; }

    /// <summary>Gets the server end.</summary>
    public GesNetworkServerEnd Server { get; }

    /// <summary>Gets whether the pair runs on a virtual clock.</summary>
    public bool IsSynchronous => _virtual is not null;

    /// <summary>Gets the time of the pair's clock in microseconds.</summary>
    public long Now => _clock.ElapsedMicroseconds;

    /// <summary>
    /// Moves the virtual clock of a synchronous pair forward, runs the deadlines of both ends and moves the packets they
    /// produce. Events wait for the next polls.
    /// </summary>
    /// <param name="microseconds">The whole microseconds to add.</param>
    /// <exception cref="InvalidOperationException">The pair is realtime, or an end is being polled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="microseconds"/> is negative.</exception>
    public void Advance(long microseconds)
    {
        if (_virtual is null) throw new InvalidOperationException("Only a synchronous pair has a virtual clock.");

        // The event a handler holds borrows the connection's buffers, which receiving would overwrite.
        if (Client.Polling || Server.Polling) throw new InvalidOperationException("An end of the pair is being polled.");
        _virtual.Advance(microseconds);
        var now = _virtual.ElapsedMicroseconds;
        foreach (var link in _links) link.Receive(now);
        Exchange();
    }

    /// <summary>Disposes both ends.</summary>
    public void Dispose()
    {
        Client.Dispose();
        Server.Dispose();
    }

    // Moves packets between the links of a synchronous pair until none moves. Waits while an end polls, because the
    // event the handler holds borrows the connection's buffers.
    internal void Exchange()
    {
        if (_virtual is null || _exchanging || Client.Polling || Server.Polling) return;
        _exchanging = true;
        try
        {
            var now = _virtual.ElapsedMicroseconds;
            for (var round = 0; round < 64; round++)
            {
                var moved = false;
                foreach (var link in _links) moved |= link.Move();
                if (!moved) return;
                foreach (var link in _links) link.Receive(now);
            }
        }
        finally
        {
            _exchanging = false;
        }
    }

    private byte[] Rent() => _pool.Count > 0 ? _pool.Pop() : new byte[_packetBytes];

    private sealed class Connector(GesNetworkPair pair, GesNetworkOptions options) : IGesNetworkConnector
    {
        public GesNetworkTransport Transport => options.Transport;

        public IGesNetworkLink Connect(GesNetworkConnection connection)
        {
            var server = GesNetworkConnection.CreateServer(connection.Network, pair._clock.ElapsedMicroseconds, options);
            var clientLink = new Link(pair, connection);
            var serverLink = new Link(pair, server);
            clientLink.Peer = serverLink;
            serverLink.Peer = clientLink;
            pair._links.Add(clientLink);
            pair._links.Add(serverLink);
            pair._accepting.Enqueue(serverLink);
            return clientLink;
        }
    }

    private sealed class Acceptor(GesNetworkPair pair, GesBoundNetwork network, GesNetworkOptions options) : IGesNetworkAcceptor
    {
        // Validates the server options when the pair is created.
        private readonly GesNetworkConnection _validated = GesNetworkConnection.CreateServer(network, 0, options);

        public IGesNetworkLink? Accept(long now) => pair._accepting.Count > 0 ? pair._accepting.Dequeue() : null;

        public void Dispose()
        {
            while (pair._accepting.Count > 0) pair._accepting.Dequeue().Dispose();
            GC.KeepAlive(_validated);
        }
    }

    // One side of an in-process connection: packets wait in the inbox of the receiving side.
    private sealed class Link(GesNetworkPair pair, GesNetworkConnection connection) : IGesNetworkLink
    {
        private const int HeldPackets = 64;
        private readonly Queue<(byte[] Bytes, int Length)> _inbox = new();
        private bool _disposed;
        private bool _ended;
        internal Link? Peer;

        public GesNetworkConnection Connection => connection;

        private bool Feed(long now)
        {
            while (_inbox.Count > 0)
            {
                if (!connection.CanReceive) return false;
                var (bytes, length) = _inbox.Dequeue();
                connection.Receive(bytes.AsSpan(0, length), now);
                pair._pool.Push(bytes);
            }

            return true;
        }

        public void Receive(long now)
        {
            if (_ended) return;
            now = Math.Max(now, connection.Now);
            // Packets wait while the connection takes no bytes; advancing refills its frame bucket.
            var drained = Feed(now);
            connection.Advance(now);
            if (!drained) drained = Feed(now);
            if (drained && (Peer!._disposed || _disposed))
            {
                _ended = true;
                connection.TransportClosed();
            }
        }

        public void Transmit() => Move();

        // Hands every due packet to the peer's inbox; returns whether one moved. While the peer takes no bytes and its
        // inbox holds HeldPackets, the packets wait in the connection, as with a socket whose peer stops reading; only
        // the close packet of a closed connection still leaves.
        internal bool Move()
        {
            if (_disposed || _ended) return false;
            var moved = false;
            while (true)
            {
                if (Peer!._inbox.Count >= HeldPackets && !Peer.Connection.CanReceive && connection.State != GesNetworkState.Closed) return moved;
                var buffer = pair.Rent();
                var length = connection.Transmit(buffer);
                if (length == 0)
                {
                    pair._pool.Push(buffer);
                    return moved;
                }

                if (Peer!._disposed) pair._pool.Push(buffer);
                else Peer._inbox.Enqueue((buffer, length));
                moved = true;
            }
        }

        // The packets already handed over still reach the peer, which then sees the transport end.
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            while (_inbox.Count > 0) pair._pool.Push(_inbox.Dequeue().Bytes);
            pair._links.Remove(this);
            if (Peer!._ended) return;
            pair.Exchange();
        }
    }
}
