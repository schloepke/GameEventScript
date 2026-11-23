// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Network;

/// <summary>
/// The client end of a session: it owns one client connection and its transport and drives both from
/// <see cref="Poll{THandler}"/>. Sends, publishes and ticks made before the session is established wait in the end and
/// reach the connection when it connects. One caller at a time; not thread-affine.
/// </summary>
public sealed class GesNetworkClientEnd : IDisposable
{
    private readonly IGesNetworkConnector _connector;
    private readonly IGesClock _clock;
    private IGesNetworkLink? _link;
    private bool _disposed;
    private bool _polling;

    // Whether a send, publish, tick or close outside a poll left packets that only the next poll transmits.
    private bool _transmitDue;

    internal bool Polling => _polling;
    // Set by a synchronous pair, which runs both ends after every send.
    internal GesNetworkPair? Pair;
    // The connected event that a synchronous pair's handshake took and the next poll reports.
    private bool _reportConnected;

    // Sends before the handshake: reliable and bulk ones in order, latest ones as the newest per route.
    private readonly List<Queued> _queue = [];
    private long _queuedBytes;
    private int _queuedReliable;
    private long _queuedTransferBytes;
    private int _queuedTransfers;
    private readonly byte[]?[] _latest;
    private readonly int[] _latestLengths;
    private readonly long[] _latestSerials;
    private readonly bool[] _latestPublish;
    private readonly bool[] _hasLatest;
    private long _serial;

    // The last published state of each latest topic, which a tick before the handshake includes.
    private readonly byte[]?[] _kept;
    private readonly int[] _keptLengths;
    private readonly bool[] _hasKept;

    // A tick opened before the handshake, and the newest closed one that waits for it.
    private bool _tickOpen;
    private bool _tickOnConnection;
    private byte[]?[] _stage;
    private int[] _stageLengths;
    private bool[] _staged;
    private byte[]?[] _closed;
    private int[] _closedLengths;
    private bool[] _closedMembers;
    private bool _hasClosed;
    private long _closedSerial;

    /// <summary>Creates a client end. Starts no I/O: the first <see cref="Poll{THandler}"/> opens the transport.</summary>
    /// <param name="network">The bound network both sides share.</param>
    /// <param name="connector">Opens the transport.</param>
    /// <param name="options">The limits, the transport of the connector and the client security.</param>
    /// <param name="token">The token the server's host receives with <c>connecting</c>, at most 512 bytes.</param>
    /// <param name="clock">The clock the end reads on every poll; the connection starts at its current time.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The options use another transport than the connector, or are invalid.</exception>
    public GesNetworkClientEnd(GesBoundNetwork network, IGesNetworkConnector connector, GesNetworkOptions options, ReadOnlySpan<byte> token, IGesClock clock)
    {
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (options.Transport != connector.Transport) throw new ArgumentException("The options must use the transport of the connector.", nameof(options));
        Connection = GesNetworkConnection.CreateClient(network, token, clock.ElapsedMicroseconds, options);
        var routes = Connection.RouteCount;
        _latest = new byte[]?[routes];
        _latestLengths = new int[routes];
        _latestSerials = new long[routes];
        _latestPublish = new bool[routes];
        _hasLatest = new bool[routes];
        _kept = new byte[]?[routes];
        _keptLengths = new int[routes];
        _hasKept = new bool[routes];
        _stage = new byte[]?[routes];
        _stageLengths = new int[routes];
        _staged = new bool[routes];
        _closed = new byte[]?[routes];
        _closedLengths = new int[routes];
        _closedMembers = new bool[routes];
    }

    /// <summary>Gets the connection of the end.</summary>
    public GesNetworkConnection Connection { get; }

    /// <summary>Gets the bound network of the end's connection.</summary>
    public GesBoundNetwork Network => Connection.Network;

    /// <summary>Gets the connector that opens the transport.</summary>
    public IGesNetworkConnector Connector => _connector;

    /// <summary>Gets the clock the end reads the time from.</summary>
    public IGesClock Clock => _clock;

    /// <summary>
    /// Gets the earliest time at which a poll has work without new input: now before the first poll and while a
    /// send, publish, tick or close outside a poll left packets for the next poll, else the connection's next deadline.
    /// </summary>
    public long NextDeadline => (_link is null || _transmitDue) && !_disposed ? _clock.ElapsedMicroseconds : Connection.NextDeadline;

    private bool Waiting => Connection.State is GesNetworkState.Hello;

    /// <summary>
    /// Sends a payload on a route the client sends. Before the session is established the send waits in the end:
    /// reliable and bulk sends in order, a latest send as the newest of its route.
    /// </summary>
    /// <param name="route">The route index of a <c>sends</c> route of the client.</param>
    /// <param name="payload">The encoded root value; it is copied.</param>
    /// <returns>The send status; <see cref="GesNetworkSendStatus.QueueFull"/> when the waiting reliable sends would exceed the connection's send queue, or the waiting transfers its transfer limit or count.</returns>
    public GesNetworkSendStatus Send(int route, ReadOnlySpan<byte> payload)
    {
        if (!Waiting) return After(Connection.Send(route, payload));
        var status = Connection.Check(route, payload.Length, publish: false);
        return status != GesNetworkSendStatus.Queued ? status : Enqueue(route, payload, publish: false);
    }

    /// <summary>
    /// Publishes a state on a topic the client publishes. Before the session is established the state waits in the
    /// end: a reliable topic's in order, a latest topic's as the newest of its route or as a member of the open tick.
    /// </summary>
    /// <param name="route">The route index of a <c>publishes</c> route of the client.</param>
    /// <param name="payload">The full record of the topic's schema; it is copied.</param>
    /// <returns>The send status.</returns>
    public GesNetworkSendStatus Publish(int route, ReadOnlySpan<byte> payload)
    {
        if (!Waiting || _tickOnConnection) return After(Connection.Publish(route, payload));
        var status = Connection.Check(route, payload.Length, publish: true);
        if (status != GesNetworkSendStatus.Queued) return status;
        if (!Connection.IsLatest(route)) return Enqueue(route, payload, publish: true);
        GesNetworkConnection.Keep(ref _kept[route], payload);
        _keptLengths[route] = payload.Length;
        _hasKept[route] = true;
        if (!_tickOpen) return Enqueue(route, payload, publish: true);
        GesNetworkConnection.Keep(ref _stage[route], payload);
        _stageLengths[route] = payload.Length;
        _staged[route] = true;
        return GesNetworkSendStatus.Queued;
    }

    /// <summary>Opens a tick, as <see cref="GesNetworkConnection.BeginTick"/>; before the handshake it waits in the end.</summary>
    /// <exception cref="InvalidOperationException">A tick is open already.</exception>
    public void BeginTick()
    {
        if (_tickOpen) throw new InvalidOperationException("A tick is open already.");
        if (!Waiting)
        {
            Connection.BeginTick();
            _tickOnConnection = true;
        }

        _tickOpen = true;
    }

    /// <summary>Makes a latest topic a member of the open tick with its last published state.</summary>
    /// <param name="route">The route index of a latest topic the client publishes.</param>
    /// <exception cref="InvalidOperationException">No tick is open, or the topic has no published state.</exception>
    /// <exception cref="ArgumentException">The route is no latest topic the client publishes.</exception>
    public void Include(int route)
    {
        if (_tickOnConnection)
        {
            Connection.Include(route);
            return;
        }

        if (!_tickOpen) throw new InvalidOperationException("No tick is open.");
        if (!Connection.IsLocalLatestTopic(route)) throw new ArgumentException("The route is no latest topic this side publishes.", nameof(route));
        if (!_hasKept[route]) throw new InvalidOperationException("The topic has no published state.");
        if (_staged[route]) return;
        GesNetworkConnection.Keep(ref _stage[route], _kept[route].AsSpan(0, _keptLengths[route]));
        _stageLengths[route] = _keptLengths[route];
        _staged[route] = true;
    }

    /// <summary>Closes the open tick. Before the handshake it replaces the tick that waits.</summary>
    /// <returns><see cref="GesNetworkSendStatus.NotConnected"/> once the connection closed; otherwise <see cref="GesNetworkSendStatus.Queued"/>.</returns>
    /// <exception cref="InvalidOperationException">No tick is open.</exception>
    public GesNetworkSendStatus EndTick()
    {
        if (!_tickOpen) throw new InvalidOperationException("No tick is open.");
        _tickOpen = false;
        if (_tickOnConnection)
        {
            _tickOnConnection = false;
            return After(Connection.EndTick());
        }

        if (!Waiting)
        {
            // The connection closed while the tick waited.
            Array.Clear(_staged, 0, _staged.Length);
            return GesNetworkSendStatus.NotConnected;
        }

        var any = Array.IndexOf(_staged, true) >= 0;
        if (!any) return GesNetworkSendStatus.Queued;
        if (_hasClosed) ReleaseClosed();
        (_closed, _stage) = (_stage, _closed);
        (_closedLengths, _stageLengths) = (_stageLengths, _closedLengths);
        (_closedMembers, _staged) = (_staged, _closedMembers);
        Array.Clear(_staged, 0, _staged.Length);
        _hasClosed = true;
        _closedSerial = ++_serial;
        return GesNetworkSendStatus.Queued;
    }

    /// <summary>Closes the connection with a detail and drops the sends that wait for the handshake. Does nothing once closed.</summary>
    /// <param name="detail">The detail sent to the server.</param>
    public void Close(string detail)
    {
        DropQueue();
        Connection.Close(detail);
        After(GesNetworkSendStatus.Queued);
    }

    /// <summary>
    /// Opens the transport on the first call, passes received packets to the connection, hands every event to the
    /// handler and transmits every due packet. After the handler closes the connection, its remaining events up to the
    /// disconnect are skipped. Never blocks and never calls the handler outside this call.
    /// </summary>
    /// <typeparam name="THandler">The handler type; a struct handler is called without allocating.</typeparam>
    /// <param name="handler">The handler of the events.</param>
    /// <exception cref="ObjectDisposedException">The end is disposed.</exception>
    /// <exception cref="InvalidOperationException">The handler polls the end again.</exception>
    public void Poll<THandler>(ref THandler handler) where THandler : IGesNetworkEndHandler => PollCore(ref handler, report: true);

    internal void PollCore<THandler>(ref THandler handler, bool report) where THandler : IGesNetworkEndHandler
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        if (_polling) throw new InvalidOperationException("The end is polled already.");
        _polling = true;
        _transmitDue = true;
        try
        {
            var now = Math.Max(_clock.ElapsedMicroseconds, Connection.Now);
            _link ??= _connector.Connect(Connection);
            _link.Receive(now);
            var received = false;

            // After the handler closed the connection, its remaining events up to the disconnect are skipped, also after the
            // Connected event that a synchronous pair reports late.
            var skipping = false;
            if (_reportConnected && report)
            {
                _reportConnected = false;
                var open = Connection.State != GesNetworkState.Closed;
                handler.Handle(new GesNetworkEndEvent(GesNetworkEndEventKind.Connected, -1, Connection.Security));
                skipping = open && Connection.State == GesNetworkState.Closed;
            }

            for (var next = Connection.NextEvent(); next.Kind != GesNetworkEventKind.None; next = Connection.NextEvent())
            {
                if (skipping && next.Kind != GesNetworkEventKind.Closed) continue;
                if (next.Kind == GesNetworkEventKind.Connected) PassQueue();
                else if (next.Kind == GesNetworkEventKind.Closed) DropQueue();
                if (!report)
                {
                    _reportConnected |= next.Kind == GesNetworkEventKind.Connected;
                    continue;
                }

                var kind = GesNetworkEnds.Kind(next.Kind, client: true);
                received |= kind is GesNetworkEndEventKind.Message or GesNetworkEndEventKind.Tick;
                var security = kind == GesNetworkEndEventKind.Connected ? Connection.Security : GesNetworkSecurity.None;
                var open = Connection.State != GesNetworkState.Closed;
                handler.Handle(new GesNetworkEndEvent(kind, -1, next, security));
                skipping = open && Connection.State == GesNetworkState.Closed;
            }

            if (received) handler.Handle(new GesNetworkEndEvent(GesNetworkEndEventKind.Received, -1, GesNetworkSecurity.None));
            _link.Transmit();
            _transmitDue = false;
        }
        finally
        {
            _polling = false;
        }

        Pair?.Exchange();
    }

    /// <summary>Releases the transport once the packets already handed to it have left. Further polls throw.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DropQueue();
        _link?.Dispose();
    }

    // Lets a synchronous pair deliver what a send outside a poll produced; another link transmits it with the next
    // poll, which the next deadline brings forward.
    private GesNetworkSendStatus After(GesNetworkSendStatus status)
    {
        if (_polling) return status;
        if (Pair is null) _transmitDue = true;
        else Pair.Exchange();
        return status;
    }

    private GesNetworkSendStatus Enqueue(int route, ReadOnlySpan<byte> payload, bool publish)
    {
        if (Connection.IsLatest(route))
        {
            GesNetworkConnection.Keep(ref _latest[route], payload);
            _latestLengths[route] = payload.Length;
            _latestSerials[route] = ++_serial;
            _latestPublish[route] = publish;
            _hasLatest[route] = true;
            return GesNetworkSendStatus.Queued;
        }

        if (Connection.IsReliable(route))
        {
            // As the connection counts it: a topic state with its mode byte, so passing the queue never fills it.
            var bytes = publish ? payload.Length + 1 : payload.Length;
            if (_queuedReliable >= GesNetworkSendQueue.MaximumReliable || _queuedBytes + bytes > Connection.MaxSendQueueBytes) return GesNetworkSendStatus.QueueFull;
            _queuedBytes += bytes;
            _queuedReliable++;
        }
        else
        {
            // As the connection bounds its started transfers, against this side's transfer limit; a transfer counts as
            // uncompressed. The connection takes every kept one and cancels one beyond the peer's transfer limit.
            if (_queuedTransfers >= Connection.MaxQueuedTransfers || _queuedTransferBytes + payload.Length > Connection.MaxTransferQueueBytes)
                return GesNetworkSendStatus.QueueFull;
            _queuedTransfers++;
            _queuedTransferBytes += GesNetworkConnection.QueuedTransferBytes(payload.Length);
        }

        _queue.Add(new Queued(route, publish, payload.ToArray()));
        return GesNetworkSendStatus.Queued;
    }

    // Passes everything that waited for the handshake to the connection: reliable and bulk sends in order, latest
    // states older than the waiting tick, the tick, and then newer latest states.
    private void PassQueue()
    {
        foreach (var entry in _queue)
        {
            if (entry.Publish) Connection.Publish(entry.Route, entry.Payload);
            else if (Connection.IsBulk(entry.Route)) Connection.PassTransfer(entry.Route, entry.Payload);
            else Connection.Send(entry.Route, entry.Payload);
        }

        _queue.Clear();
        _queuedBytes = 0;
        _queuedReliable = 0;
        _queuedTransferBytes = 0;
        _queuedTransfers = 0;
        PassLatest(older: true);
        if (_hasClosed) PassTick(_closed, _closedLengths, _closedMembers);
        PassLatest(older: false);
        _hasClosed = false;
        Array.Clear(_closedMembers, 0, _closedMembers.Length);
        Array.Clear(_hasLatest, 0, _hasLatest.Length);
        if (!_tickOpen) return;
        // An open tick moves to the connection with its members.
        Connection.BeginTick();
        _tickOnConnection = true;
        for (var route = 0; route < _staged.Length; route++)
            if (_staged[route]) Connection.Publish(route, _stage[route].AsSpan(0, _stageLengths[route]));
        Array.Clear(_staged, 0, _staged.Length);
    }

    private void PassLatest(bool older)
    {
        for (var route = 0; route < _hasLatest.Length; route++)
        {
            if (!_hasLatest[route] || (_hasClosed && _latestSerials[route] < _closedSerial) != older) continue;
            // A tick member newer than the state replaces it.
            if (older && _hasClosed && _closedMembers[route]) continue;
            var payload = _latest[route].AsSpan(0, _latestLengths[route]);
            if (_latestPublish[route]) Connection.Publish(route, payload);
            else Connection.Send(route, payload);
        }
    }

    // Turns the members of the replaced closed tick into latest states that newer states of their routes replace.
    private void ReleaseClosed()
    {
        for (var route = 0; route < _closedMembers.Length; route++)
        {
            if (!_closedMembers[route] || (_hasLatest[route] && _latestSerials[route] > _closedSerial)) continue;
            GesNetworkConnection.Keep(ref _latest[route], _closed[route].AsSpan(0, _closedLengths[route]));
            _latestLengths[route] = _closedLengths[route];
            _latestSerials[route] = _closedSerial;
            _latestPublish[route] = true;
            _hasLatest[route] = true;
        }
    }

    private void PassTick(byte[]?[] records, int[] lengths, bool[] members)
    {
        Connection.BeginTick();
        for (var route = 0; route < members.Length; route++)
            if (members[route]) Connection.Publish(route, records[route].AsSpan(0, lengths[route]));
        Connection.EndTick();
    }

    private void DropQueue()
    {
        _queue.Clear();
        _queuedBytes = 0;
        _queuedReliable = 0;
        _queuedTransferBytes = 0;
        _queuedTransfers = 0;
        Array.Clear(_hasLatest, 0, _hasLatest.Length);
        Array.Clear(_staged, 0, _staged.Length);
        Array.Clear(_closedMembers, 0, _closedMembers.Length);
        _hasClosed = false;
    }

    private readonly struct Queued(int route, bool publish, byte[] payload)
    {
        internal readonly int Route = route;
        internal readonly bool Publish = publish;
        internal readonly byte[] Payload = payload;
    }
}
