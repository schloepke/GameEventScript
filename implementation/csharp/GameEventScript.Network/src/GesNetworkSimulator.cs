// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Network;

/// <summary>
/// Connects a client and a server connection in one process with seeded latency. Each packet is delayed by the
/// latency plus a draw from 0 to the jitter inclusive, but never arrives before a packet sent earlier in the same
/// direction. Equal seeds, options and inputs give equal packets, events and times. The host takes the events of
/// both connections between calls of <see cref="Advance"/>. Not thread-safe.
/// </summary>
public sealed class GesNetworkSimulator
{
    private readonly GesRandomGenerator _random;
    private readonly long _latency;
    private readonly long _jitter;
    private readonly Queue<Flight> _toServer = new();
    private readonly Queue<Flight> _toClient = new();
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
    /// <exception cref="ArgumentNullException">A connection is null.</exception>
    /// <exception cref="ArgumentException">The connections are not a client and a server at the same time.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The latency or jitter is negative.</exception>
    public GesNetworkSimulator(GesNetworkConnection client, GesNetworkConnection server, long seed, long latencyMicroseconds, long jitterMicroseconds)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Server = server ?? throw new ArgumentNullException(nameof(server));
        if (!client.IsClient || server.IsClient) throw new ArgumentException("The simulator connects a client and a server.");
        if (client.Now != server.Now) throw new ArgumentException("Both connections start at the same time.");
        if (latencyMicroseconds < 0) throw new ArgumentOutOfRangeException(nameof(latencyMicroseconds));
        if (jitterMicroseconds < 0) throw new ArgumentOutOfRangeException(nameof(jitterMicroseconds));
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
            if (_toServer.Count > 0) deadline = Math.Min(deadline, _toServer.Peek().Time);
            if (_toClient.Count > 0) deadline = Math.Min(deadline, _toClient.Peek().Time);
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
            if (next > now) break;
            Now = next;
            var toServer = _toServer.Count > 0 && _toServer.Peek().Time == next;
            var toClient = _toClient.Count > 0 && _toClient.Peek().Time == next;
            if (toServer && (!toClient || _toServer.Peek().Sequence < _toClient.Peek().Sequence)) Deliver(_toServer, Server);
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

    private void Deliver(Queue<Flight> queue, GesNetworkConnection receiver)
    {
        var flight = queue.Dequeue();
        receiver.Receive(flight.Bytes.AsSpan(0, flight.Length), Now);
        _free.Push(flight.Bytes);
    }

    private void TransmitAll()
    {
        Send(Client, _toServer, ref _lastToServer);
        Send(Server, _toClient, ref _lastToClient);
    }

    private void Send(GesNetworkConnection sender, Queue<Flight> queue, ref long last)
    {
        int length;
        while ((length = sender.Transmit(_buffer)) > 0)
        {
            var delay = _latency + _random.NextInclusiveInteger(0, _jitter);
            last = Math.Max(Now + delay, last);
            var bytes = _free.Count > 0 ? _free.Pop() : new byte[_buffer.Length];
            _buffer.AsSpan(0, length).CopyTo(bytes);
            queue.Enqueue(new Flight(last, _sequence++, bytes, length));
        }
    }

    private readonly struct Flight(long time, long sequence, byte[] bytes, int length)
    {
        internal readonly long Time = time;
        internal readonly long Sequence = sequence;
        internal readonly byte[] Bytes = bytes;
        internal readonly int Length = length;
    }
}

/// <summary>
/// The in-process transport: a <see cref="GesNetworkSimulator"/> without latency, so a packet arrives at the time it
/// is sent. Not thread-safe.
/// </summary>
public sealed class GesNetworkLoopback
{
    private readonly GesNetworkSimulator _simulator;

    /// <summary>Connects a client and a server connection created at the same time.</summary>
    /// <param name="client">The client connection.</param>
    /// <param name="server">The server connection.</param>
    /// <exception cref="ArgumentNullException">A connection is null.</exception>
    /// <exception cref="ArgumentException">The connections are not a client and a server at the same time.</exception>
    public GesNetworkLoopback(GesNetworkConnection client, GesNetworkConnection server) => _simulator = new GesNetworkSimulator(client, server, 0, 0, 0);

    /// <summary>Gets the client connection.</summary>
    public GesNetworkConnection Client => _simulator.Client;

    /// <summary>Gets the server connection.</summary>
    public GesNetworkConnection Server => _simulator.Server;

    /// <summary>Gets the time in microseconds.</summary>
    public long Now => _simulator.Now;

    /// <summary>Gets the earliest deadline of both connections.</summary>
    public long NextDeadline => _simulator.NextDeadline;

    /// <summary>Exchanges all due packets and runs every deadline up to <paramref name="now"/>.</summary>
    /// <param name="now">The new time in microseconds, not earlier than <see cref="Now"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="now"/> is earlier than <see cref="Now"/>.</exception>
    public void Advance(long now) => _simulator.Advance(now);
}
