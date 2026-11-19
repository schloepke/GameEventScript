// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Network;

/// <summary>
/// The server end of a session: it accepts clients from an acceptor, owns one server connection per client and
/// drives all of them from <see cref="Poll{THandler}"/>. Clients have stable end-local ids. Publishes and ticks reach
/// every client. One caller at a time; not thread-affine.
/// </summary>
public sealed class GesNetworkServerEnd : IDisposable
{
    private readonly GesBoundNetwork _network;
    private readonly IGesNetworkAcceptor _acceptor;
    private readonly IGesClock _clock;
    private readonly bool _subscribeAll;
    // Answers the checks of sends and publishes without a client.
    private readonly GesNetworkConnection _check;
    private readonly List<Client> _clients = [];
    private readonly Dictionary<int, Client> _byId = [];
    private int _nextId;
    private bool _disposed;
    private bool _polling;

    // Whether a send, publish, tick or close outside a poll left packets that only the next poll transmits.
    private bool _transmitDue;

    internal bool Polling => _polling;
    private bool _tickOpen;
    // The last published state of each latest topic, which a client connected during a tick and Include receive.
    private readonly byte[]?[] _kept;
    private readonly int[] _keptLengths;
    private readonly bool[] _hasKept;
    // The members of the open tick, which a client connected during it receives.
    private readonly bool[] _members;
    // Set by a synchronous pair, which runs both ends after every send.
    internal GesNetworkPair? Pair;
    // Clients whose connected event a synchronous pair's handshake took and the next poll reports.
    private readonly List<int> _reportConnected = [];

    /// <summary>Creates a server end. Accepts nothing until the first <see cref="Poll{THandler}"/>.</summary>
    /// <param name="network">The bound network both sides share.</param>
    /// <param name="acceptor">Accepts the transports of clients; the end disposes it.</param>
    /// <param name="clock">The clock the end reads on every poll.</param>
    /// <param name="subscribeAll">Whether every connected client is subscribed to every topic the server publishes.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public GesNetworkServerEnd(GesBoundNetwork network, IGesNetworkAcceptor acceptor, IGesClock clock, bool subscribeAll = true)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _acceptor = acceptor ?? throw new ArgumentNullException(nameof(acceptor));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _subscribeAll = subscribeAll;
        _check = GesNetworkConnection.CreateServer(network, 0, GesNetworkOptions.InProcess());
        var routes = _check.RouteCount;
        _kept = new byte[]?[routes];
        _keptLengths = new int[routes];
        _hasKept = new bool[routes];
        _members = new bool[routes];
    }

    /// <summary>Gets the bound network the end's connections use.</summary>
    public GesBoundNetwork Network => _network;

    /// <summary>Gets the number of clients the end holds, connecting or connected.</summary>
    public int ClientCount => _clients.Count;

    /// <summary>Gets the acceptor of the clients' transports.</summary>
    public IGesNetworkAcceptor Acceptor => _acceptor;

    /// <summary>Gets the clock the end reads the time from.</summary>
    public IGesClock Clock => _clock;

    /// <summary>
    /// Gets the earliest deadline of all clients, now while a send, publish, tick or close outside a poll left packets
    /// for the next poll; long.MaxValue without clients.
    /// </summary>
    /// <remarks>An acceptor has no deadline: a server end that waits for clients polls at its own pace.</remarks>
    public long NextDeadline
    {
        get
        {
            if (_transmitDue && _clients.Count > 0) return _clock.ElapsedMicroseconds;
            var deadline = long.MaxValue;
            foreach (var client in _clients) deadline = Math.Min(deadline, client.Link.Connection.NextDeadline);
            return deadline;
        }
    }

    /// <summary>Gets the connection of a client.</summary>
    /// <param name="client">The end-local client id.</param>
    /// <returns>The server connection of the client.</returns>
    /// <exception cref="ArgumentException">No client has the id.</exception>
    public GesNetworkConnection Connection(int client) => Find(client).Link.Connection;

    /// <summary>
    /// Publishes a state on a topic the server publishes to every subscribed client. In an open tick a latest state is
    /// a member of it.
    /// </summary>
    /// <param name="route">The route index of a <c>publishes</c> route of the server.</param>
    /// <param name="payload">The full record of the topic's schema; it is copied.</param>
    /// <returns>
    /// <see cref="GesNetworkSendStatus.Queued"/>; <see cref="GesNetworkSendStatus.MessageTooLarge"/> when the payload
    /// exceeds the limit of some client; <see cref="GesNetworkSendStatus.QueueFull"/> when the queue of some client is
    /// full; or the status of an invalid route or payload. Clients that are not connected or not subscribed
    /// are skipped.
    /// </returns>
    public GesNetworkSendStatus Publish(int route, ReadOnlySpan<byte> payload)
    {
        var status = _check.Check(route, payload.Length, publish: true);
        if (status != GesNetworkSendStatus.Queued) return status;
        if (_check.IsLatest(route))
        {
            GesNetworkConnection.Keep(ref _kept[route], payload);
            _keptLengths[route] = payload.Length;
            _hasKept[route] = true;
            if (_tickOpen) _members[route] = true;
        }

        foreach (var client in _clients)
        {
            status = Merge(status, client.Link.Connection.Publish(route, payload));
        }

        return After(status);
    }

    /// <summary>Sends a payload on a route the server sends to every connected client.</summary>
    /// <param name="route">The route index of a <c>sends</c> route of the server.</param>
    /// <param name="payload">The encoded root value; it is copied.</param>
    /// <returns>As <see cref="Publish"/>.</returns>
    public GesNetworkSendStatus Send(int route, ReadOnlySpan<byte> payload)
    {
        var status = _check.Check(route, payload.Length, publish: false);
        if (status != GesNetworkSendStatus.Queued) return status;
        foreach (var client in _clients)
        {
            status = Merge(status, client.Link.Connection.Send(route, payload));
        }

        return After(status);
    }

    // The status of a send to every client: messageTooLarge when the payload exceeds the limit of some client, else
    // queueFull when the queue of some client is full; clients that are not connected or not subscribed are skipped.
    private static GesNetworkSendStatus Merge(GesNetworkSendStatus status, GesNetworkSendStatus client) => client switch
    {
        GesNetworkSendStatus.MessageTooLarge => client,
        GesNetworkSendStatus.QueueFull when status == GesNetworkSendStatus.Queued => client,
        _ => status,
    };

    /// <summary>Sends a payload on a route the server sends to one client.</summary>
    /// <param name="route">The route index of a <c>sends</c> route of the server.</param>
    /// <param name="payload">The encoded root value; it is copied.</param>
    /// <param name="client">The end-local client id.</param>
    /// <returns>The send status of the client's connection.</returns>
    /// <exception cref="ArgumentException">No client has the id.</exception>
    public GesNetworkSendStatus Send(int route, ReadOnlySpan<byte> payload, int client) => After(Find(client).Link.Connection.Send(route, payload));

    /// <summary>Opens a tick on every client, as <see cref="GesNetworkConnection.BeginTick"/>; a client connected during it joins it.</summary>
    /// <exception cref="InvalidOperationException">A tick is open already.</exception>
    public void BeginTick()
    {
        if (_tickOpen) throw new InvalidOperationException("A tick is open already.");
        _tickOpen = true;
        foreach (var client in _clients)
            if (client.Link.Connection.State == GesNetworkState.Connected) client.Link.Connection.BeginTick();
    }

    /// <summary>
    /// Makes a latest topic a member of the open tick with its last published state: a client that has received the
    /// state names it, a subscribed client that has not receives it.
    /// </summary>
    /// <param name="route">The route index of a latest topic the server publishes.</param>
    /// <exception cref="InvalidOperationException">No tick is open, or the topic has no published state.</exception>
    /// <exception cref="ArgumentException">The route is no latest topic the server publishes.</exception>
    public void Include(int route)
    {
        if (!_tickOpen) throw new InvalidOperationException("No tick is open.");
        if (!_check.IsLocalLatestTopic(route)) throw new ArgumentException("The route is no latest topic this side publishes.", nameof(route));
        if (!_hasKept[route]) throw new InvalidOperationException("The topic has no published state.");
        _members[route] = true;
        foreach (var client in _clients)
            if (client.Link.Connection.TickOpen) IncludeKept(client.Link.Connection, route);
    }

    /// <summary>Closes the open tick on every client.</summary>
    /// <returns><see cref="GesNetworkSendStatus.Queued"/>.</returns>
    /// <exception cref="InvalidOperationException">No tick is open.</exception>
    public GesNetworkSendStatus EndTick()
    {
        if (!_tickOpen) throw new InvalidOperationException("No tick is open.");
        _tickOpen = false;
        Array.Clear(_members, 0, _members.Length);
        foreach (var client in _clients)
            if (client.Link.Connection.TickOpen) client.Link.Connection.EndTick();
        return After(GesNetworkSendStatus.Queued);
    }

    /// <summary>Subscribes a client to a topic the server publishes.</summary>
    /// <param name="client">The end-local client id.</param>
    /// <param name="route">The route index of a topic the server publishes.</param>
    /// <exception cref="ArgumentException">No client has the id, or the route is no topic the server publishes.</exception>
    public void Subscribe(int client, int route) => Find(client).Link.Connection.Subscribe(route);

    /// <summary>Unsubscribes a client from a topic the server publishes, as <see cref="GesNetworkConnection.Unsubscribe"/>.</summary>
    /// <param name="client">The end-local client id.</param>
    /// <param name="route">The route index of a topic the server publishes.</param>
    /// <exception cref="ArgumentException">No client has the id, or the route is no topic the server publishes.</exception>
    public void Unsubscribe(int client, int route) => Find(client).Link.Connection.Unsubscribe(route);

    /// <summary>Refuses a client during its <see cref="GesNetworkEndEventKind.Connecting"/> event.</summary>
    /// <param name="client">The end-local client id.</param>
    /// <param name="detail">The detail sent to the client.</param>
    /// <exception cref="ArgumentException">No client has the id.</exception>
    /// <exception cref="InvalidOperationException">The client is not connecting.</exception>
    public void Refuse(int client, string detail)
    {
        Find(client).Link.Connection.Refuse(detail);
        After(GesNetworkSendStatus.Queued);
    }

    /// <summary>Closes the connection of a client; it leaves the end with its disconnect event. Does nothing once closed.</summary>
    /// <param name="client">The end-local client id.</param>
    /// <param name="detail">The detail sent to the client.</param>
    /// <exception cref="ArgumentException">No client has the id.</exception>
    public void Close(int client, string detail)
    {
        Find(client).Link.Connection.Close(detail);
        After(GesNetworkSendStatus.Queued);
    }

    /// <summary>Closes the connections of all clients.</summary>
    /// <param name="detail">The detail sent to the clients.</param>
    public void Close(string detail)
    {
        foreach (var client in _clients) client.Link.Connection.Close(detail);
        After(GesNetworkSendStatus.Queued);
    }

    /// <summary>
    /// Accepts every waiting client, passes received packets to every connection, hands every event to the handler and
    /// transmits every due packet. A connecting client is accepted after its event unless the handler accepted or
    /// refused it, also when the handler throws; after the handler closes or refuses a client, that client's remaining
    /// events up to its disconnect are skipped, and a closed client leaves the end after its event. Never blocks and
    /// never calls the handler outside this call.
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
            var now = _clock.ElapsedMicroseconds;
            while (_acceptor.Accept(now) is { } link)
            {
                var client = new Client(_nextId++, link);
                _clients.Add(client);
                _byId.Add(client.Id, client);
            }

            var received = false;
            if (report)
            {
                foreach (var id in _reportConnected)
                {
                    if (_byId.TryGetValue(id, out var client))
                        handler.Handle(new GesNetworkEndEvent(GesNetworkEndEventKind.ClientConnected, id, client.Link.Connection.Security));
                }

                _reportConnected.Clear();
            }

            // Clients that join during the poll wait for the next one; a client that leaves is removed after its events.
            var count = _clients.Count;
            for (var index = 0; index < count; index++)
            {
                var client = _clients[index];
                var connection = client.Link.Connection;
                client.Link.Receive(Math.Max(now, connection.Now));

                // After the handler closed or refused the connection, its remaining events up to the disconnect are skipped.
                var skipping = false;
                for (var next = connection.NextEvent(); next.Kind != GesNetworkEventKind.None; next = connection.NextEvent())
                {
                    if (skipping && next.Kind != GesNetworkEventKind.Closed) continue;
                    var kind = GesNetworkEnds.Kind(next.Kind, client: false);
                    if (kind == GesNetworkEndEventKind.ClientConnected) Join(connection);
                    else if (kind == GesNetworkEndEventKind.ClientDisconnected) client.Left = true;
                    if (!report)
                    {
                        if (kind == GesNetworkEndEventKind.ClientConnected) _reportConnected.Add(client.Id);
                    }
                    else if (kind == GesNetworkEndEventKind.Connecting)
                    {
                        try
                        {
                            handler.Handle(new GesNetworkEndEvent(kind, client.Id, next, GesNetworkSecurity.None));
                        }
                        finally
                        {
                            // A client the handler neither accepted nor refused is accepted, also when the handler throws.
                            if (connection.State == GesNetworkState.AwaitAccept) connection.Accept();
                        }

                        skipping = connection.State == GesNetworkState.Closed;
                        continue;
                    }
                    else
                    {
                        received |= kind is GesNetworkEndEventKind.Message or GesNetworkEndEventKind.Tick;
                        var security = kind == GesNetworkEndEventKind.ClientConnected ? connection.Security : GesNetworkSecurity.None;
                        var open = connection.State != GesNetworkState.Closed;
                        handler.Handle(new GesNetworkEndEvent(kind, client.Id, next, security));
                        skipping = open && connection.State == GesNetworkState.Closed;
                    }

                    // A client is accepted when no handler sees its request.
                    if (kind == GesNetworkEndEventKind.Connecting && connection.State == GesNetworkState.AwaitAccept) connection.Accept();
                }
            }

            for (var index = _clients.Count - 1; index >= 0; index--)
            {
                var client = _clients[index];
                if (!client.Left) continue;
                _clients.RemoveAt(index);
                _byId.Remove(client.Id);
                client.Link.Transmit();
                client.Link.Dispose();
            }

            if (received) handler.Handle(new GesNetworkEndEvent(GesNetworkEndEventKind.Received, -1, GesNetworkSecurity.None));
            foreach (var client in _clients) client.Link.Transmit();
            _transmitDue = false;
        }
        finally
        {
            _polling = false;
        }

        Pair?.Exchange();
    }

    /// <summary>Releases every client's transport once the packets already handed to it have left, and the acceptor.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var client in _clients) client.Link.Dispose();
        _clients.Clear();
        _byId.Clear();
        _acceptor.Dispose();
    }

    // Prepares a client that just connected: its subscriptions and the open tick with the members so far.
    private void Join(GesNetworkConnection connection)
    {
        if (_subscribeAll)
        {
            for (var route = 0; route < _check.RouteCount; route++)
                if (_check.Check(route, 0, publish: true) != GesNetworkSendStatus.InvalidRoute) connection.Subscribe(route);
        }

        if (!_tickOpen || connection.TickOpen) return;
        connection.BeginTick();
        for (var route = 0; route < _members.Length; route++)
            if (_members[route]) IncludeKept(connection, route);
    }

    // A connection whose last state of the topic is the kept one names it; another one, which missed a later publish
    // while it was not subscribed, receives the kept state.
    private void IncludeKept(GesNetworkConnection connection, int route)
    {
        var kept = _kept[route].AsSpan(0, _keptLengths[route]);
        if (connection.LastIs(route, kept)) connection.Include(route);
        else if (connection.IsSubscribed(route)) connection.Publish(route, kept);
    }

    private Client Find(int client) =>
        _byId.TryGetValue(client, out var found) ? found : throw new ArgumentException("No client has the id.", nameof(client));

    // Lets a synchronous pair deliver what a send outside a poll produced; another link transmits it with the next
    // poll, which the next deadline brings forward.
    private GesNetworkSendStatus After(GesNetworkSendStatus status)
    {
        if (_polling) return status;
        if (Pair is null) _transmitDue = true;
        else Pair.Exchange();
        return status;
    }

    private sealed class Client(int id, IGesNetworkLink link)
    {
        internal readonly int Id = id;
        internal readonly IGesNetworkLink Link = link;
        internal bool Left;
    }
}
