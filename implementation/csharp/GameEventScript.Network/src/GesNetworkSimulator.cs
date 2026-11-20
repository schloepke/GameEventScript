// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Network;

/// <summary>
/// Connects a client and a server connection in one process with seeded latency. Each packet is delayed by the
/// latency plus a draw from 0 to the jitter inclusive, but never arrives before a packet sent earlier in the same
/// direction. Between datagram connections it may also lose, duplicate and reorder packets. Equal seeds, options and
/// inputs give equal packets, events and times. The host takes the events of both connections between calls of
/// <see cref="Advance"/>. Not thread-safe.
/// </summary>
public sealed class GesNetworkSimulator
{
    private readonly GesRandomGenerator _random;
    private readonly long _latency;
    private readonly long _jitter;
    private readonly int _loss;
    private readonly int _duplicate;
    private readonly bool _reorder;
    // The packets on their way in arrival order: by time, then in the order sent.
    private readonly List<Flight> _toServer = [];
    private readonly List<Flight> _toClient = [];
    private readonly Stack<byte[]> _free = new();
    private readonly byte[] _buffer = new byte[GesNetworkWire.MaximumPacketBytes + 3];
    private long _lastToServer;
    private long _lastToClient;
    private long _sequence;

    /// <summary>Creates a simulated network between two connections created at the same time.</summary>
    /// <param name="client">The client connection.</param>
    /// <param name="server">The server connection.</param>
    /// <param name="seed">The seed of the random generator that draws the jitter.</param>
    /// <param name="latencyMicroseconds">The base latency of every packet in microseconds.</param>
    /// <param name="jitterMicroseconds">The largest additional delay in microseconds.</param>
    /// <param name="lossPerMille">The share of packets lost in per mille, 0 to 1000.</param>
    /// <param name="duplicatePerMille">The share of packets delivered twice in per mille, 0 to 1000.</param>
    /// <param name="reorder">Whether a packet arrives after its own delay instead of behind packets sent earlier.</param>
    /// <exception cref="ArgumentNullException">A connection is null.</exception>
    /// <exception cref="ArgumentException">
    /// The connections are not a client and a server at the same time, or loss, duplicates or reordering are requested
    /// for connections that do not both use <see cref="GesNetworkTransport.Datagram"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The latency or jitter is negative, or a rate is outside 0 to 1000.</exception>
    public GesNetworkSimulator(
        GesNetworkConnection client,
        GesNetworkConnection server,
        long seed,
        long latencyMicroseconds,
        long jitterMicroseconds,
        int lossPerMille = 0,
        int duplicatePerMille = 0,
        bool reorder = false)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Server = server ?? throw new ArgumentNullException(nameof(server));
        if (!client.IsClient || server.IsClient) throw new ArgumentException("The simulator connects a client and a server.");
        if (client.Now != server.Now) throw new ArgumentException("Both connections start at the same time.");
        if (latencyMicroseconds < 0) throw new ArgumentOutOfRangeException(nameof(latencyMicroseconds));
        if (jitterMicroseconds < 0) throw new ArgumentOutOfRangeException(nameof(jitterMicroseconds));
        if (lossPerMille is < 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(lossPerMille));
        if (duplicatePerMille is < 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(duplicatePerMille));
        if ((lossPerMille > 0 || duplicatePerMille > 0 || reorder) &&
            (client.Transport != GesNetworkTransport.Datagram || server.Transport != GesNetworkTransport.Datagram))
            throw new ArgumentException("Loss, duplicates and reordering require datagram connections.");
        _loss = lossPerMille;
        _duplicate = duplicatePerMille;
        _reorder = reorder;
        _random = GesRandomGenerator.FromSeed(seed);
        _latency = latencyMicroseconds;
        _jitter = jitterMicroseconds;
        Now = _lastToServer = _lastToClient = client.Now;
    }

    /// <summary>Gets the client connection.</summary>
    public GesNetworkConnection Client { get; }

    /// <summary>Gets the server connection.</summary>
    public GesNetworkConnection Server { get; }

    /// <summary>Gets the simulated time in microseconds.</summary>
    public long Now { get; private set; }

    /// <summary>Gets the number of packets on their way.</summary>
    public int PacketsInFlight => _toServer.Count + _toClient.Count;

    /// <summary>Gets the earliest time at which a packet arrives or a connection has a deadline.</summary>
    public long NextDeadline
    {
        get
        {
            var deadline = Math.Min(Client.NextDeadline, Server.NextDeadline);
            deadline = Math.Min(deadline, Arrival(_toServer, Server));
            deadline = Math.Min(deadline, Arrival(_toClient, Client));
            return Math.Max(deadline, Now);
        }
    }

    /// <summary>
    /// Runs the network up to <paramref name="now"/>: both connections transmit, and each arrival and deadline is
    /// processed at its time in order, the client's packets before the server's at equal times.
    /// </summary>
    /// <param name="now">The new time in microseconds, not earlier than <see cref="Now"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="now"/> is earlier than <see cref="Now"/>.</exception>
    public void Advance(long now)
    {
        if (now < Now) throw new ArgumentOutOfRangeException(nameof(now), "Time does not go back.");
        while (true)
        {
            TransmitAll();
            var next = NextDeadline;
            if (next > now || next == long.MaxValue) break;
            Now = next;
            var toServer = Arrival(_toServer, Server) == next;
            var toClient = Arrival(_toClient, Client) == next;
            if (toServer && (!toClient || _toServer[0].Sequence < _toClient[0].Sequence)) Deliver(_toServer, Server);
            else if (toClient) Deliver(_toClient, Client);
            else
            {
                Client.Advance(next);
                Server.Advance(next);
            }
        }

        Now = now;
        Client.Advance(now);
        Server.Advance(now);
        TransmitAll();
    }

    // A stream or message connection that takes no bytes leaves its packets waiting until its frame bucket refills.
    private static long Arrival(List<Flight> queue, GesNetworkConnection receiver)
    {
        if (queue.Count == 0) return long.MaxValue;
        var time = queue[0].Time;
        return receiver.CanReceive ? time : Math.Max(time, receiver.NextDeadline);
    }

    private void Deliver(List<Flight> queue, GesNetworkConnection receiver)
    {
        // The bucket refills up to the arrival; if it still holds no frame, the packet waits for the next refill.
        receiver.Advance(Now);
        if (!receiver.CanReceive) return;
        var flight = queue[0];
        queue.RemoveAt(0);
        receiver.Receive(flight.Bytes.AsSpan(0, flight.Length), Now);
        _free.Push(flight.Bytes);
    }

    private void TransmitAll()
    {
        Send(Client, Server, _toServer, ref _lastToServer);
        Send(Server, Client, _toClient, ref _lastToClient);
    }

    // While the receiver takes no bytes and this many packets wait for it, the sender holds its packets, as a socket
    // whose peer stops reading does; only the close packet of a closed connection still leaves.
    private const int HeldPackets = 64;

    private void Send(GesNetworkConnection sender, GesNetworkConnection receiver, List<Flight> queue, ref long last)
    {
        int length;
        while ((queue.Count < HeldPackets || receiver.CanReceive || sender.State == GesNetworkState.Closed) && (length = sender.Transmit(_buffer)) > 0)
        {
            var delay = Delay();
            if (_loss > 0 && _random.NextInclusiveInteger(0, 999) < _loss) continue;
            Enqueue(queue, ref last, delay, length);
            if (_duplicate > 0 && _random.NextInclusiveInteger(0, 999) < _duplicate) Enqueue(queue, ref last, Delay(), length);
        }
    }

    private long Delay() => _latency + _random.NextInclusiveInteger(0, _jitter);

    // Adds a packet in arrival order; without reordering it arrives no earlier than the packet sent before.
    private void Enqueue(List<Flight> queue, ref long last, long delay, int length)
    {
        var time = Now + delay;
        if (!_reorder) time = last = Math.Max(time, last);
        var bytes = _free.Count > 0 ? _free.Pop() : new byte[_buffer.Length];
        _buffer.AsSpan(0, length).CopyTo(bytes);
        var index = queue.Count;
        while (index > 0 && queue[index - 1].Time > time) index--;
        queue.Insert(index, new Flight(time, _sequence++, bytes, length));
    }

    private readonly struct Flight(long time, long sequence, byte[] bytes, int length)
    {
        internal readonly long Time = time;
        internal readonly long Sequence = sequence;
        internal readonly byte[] Bytes = bytes;
        internal readonly int Length = length;
    }
}
