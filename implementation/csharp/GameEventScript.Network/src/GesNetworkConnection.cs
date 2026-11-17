// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Network;

/// <summary>
/// One side of a network session: a synchronous state machine without threads, sockets or clocks. The host passes
/// received bytes and the current time, takes events and takes packets to transmit. Times are microseconds of a
/// monotonic clock chosen by the host. Not thread-safe; one caller at a time.
/// </summary>
public sealed partial class GesNetworkConnection
{
    private const long MicrosecondsPerSecond = 1_000_000;
    private const int SendTimeWindow = 256;

    private readonly bool _client;
    private readonly GesNetworkTransport _transport;
    private readonly int _maxPacket;
    private readonly long _maxPending;
    private readonly long _framesPerSecond;
    private readonly long _maxSendQueue;
    private readonly long _timeout;
    private readonly long _keepalive;
    private readonly long _ackDelay;
    private readonly long _maxBaseline;
    private long _baselineLimit;

    private readonly int _routeCount;
    private readonly GesNetworkClass[] _classes;
    private readonly bool[] _localSends;
    private readonly long[] _maxBytes;
    private readonly GesNetworkTopic?[] _topics;

    private readonly byte[] _token = [];
    private bool _helloSent;
    private bool _welcomePending;
    private int _peerPacketLimit = GesNetworkWire.MinimumPacketBytes;

    private bool _closePending;
    private string _closeCode = string.Empty;
    private byte[] _closeDetail = [];

    private long _now;
    private long _lastReceive;
    private long _lastSend;

    private readonly GesNetworkSendQueue _sendReliable = new();
    private readonly GesNetworkSendQueue _sendBulk = new();
    private readonly GesNetworkLatestSlot?[] _sendLatest;
    private ushort _nextReliableId;
    private ushort _nextBulkId;

    private readonly GesNetworkReassembly _receiveReliable = new();
    private readonly GesNetworkReassembly _receiveBulk = new();
    private readonly GesNetworkReassembly?[] _receiveLatest;

    private long _sentCount;
    private readonly long[] _sendTimes = new long[SendTimeWindow];
    // Whether the sent packet asked for acknowledgement; only those give a round-trip sample.
    private readonly bool[] _sendAsks = new bool[SendTimeWindow];
    private long _ackedIndex = -1;
    private long _receivedCount;
    private bool _ackDue;
    private long _ackDeadline;

    private long _frameUnits;
    private long _budget;
    private long _budgetUnits;

    private EventEntry[] _events = new EventEntry[8];
    private int _eventHead;
    private int _eventCount;
    private byte[] _arena = [];
    private int _arenaLength;
    private long _pending;

    private byte[] _stream = [];
    private int _streamLength;

    private GesNetworkConnection(GesBoundNetwork network, bool client, ReadOnlySpan<byte> token, long now, GesNetworkOptions? options)
    {
        Network = network ?? throw new ArgumentNullException(nameof(network));
        options ??= new GesNetworkOptions();
        options.Validate();
        _client = client;
        _transport = options.Transport;
        _datagram = _transport == GesNetworkTransport.Datagram;
        _maxPacket = options.PacketLimit;
        _maxPending = options.MaxPendingReceiveBytes;
        _framesPerSecond = options.MaxFramesPerSecond;
        _maxSendQueue = options.MaxSendQueueBytes;
        _timeout = options.TimeoutMicroseconds;
        _keepalive = options.KeepaliveMicroseconds;
        _ackDelay = options.AckDelayMicroseconds;
        _budget = options.SendBudgetBytesPerSecond;
        _maxBaseline = options.MaxBaselineBytes;

        var routes = network.Network.Routes;
        _routeCount = routes.Count;
        _classes = new GesNetworkClass[_routeCount];
        _localSends = new bool[_routeCount];
        _maxBytes = new long[_routeCount];
        _sendLatest = new GesNetworkLatestSlot?[_routeCount];
        _receiveLatest = new GesNetworkReassembly?[_routeCount];
        _topics = new GesNetworkTopic?[_routeCount];
        // Only a datagram connection records its packets and restores the order of what it receives.
        var datagramRoutes = _datagram ? _routeCount : 0;
        var records = _datagram ? RecordedPackets : 0;
        var fragments = _datagram ? RecordedFragments : 0;
        _partialLatest = new GesNetworkPartial?[datagramRoutes];
        _owed = new bool[datagramRoutes];
        _owedNumbers = new ushort[datagramRoutes];
        _recordTimes = new long[records];
        _recordAsks = new bool[records];
        _recordOutstanding = new bool[records];
        _recordFragments = new long[records];
        _recordFragmentCounts = new int[records];
        _fragmentLanes = new int[fragments];
        _fragmentKeys = new long[fragments];
        _fragmentOffsets = new int[fragments];
        _fragmentLengths = new int[fragments];
        if (_datagram)
        {
            _orderedReliable = new GesNetworkOrderedReceive();
            _orderedBulk = new GesNetworkOrderedReceive();
        }

        for (var index = 0; index < _routeCount; index++)
        {
            var route = routes[index];
            _classes[index] = route.DeliveryClass;
            _localSends[index] = route.Side == GesNetworkSide.Client == client;
            _maxBytes[index] = network.Roots[index].MaxByteCount;
            if (route.Verb == GesNetworkVerb.Publishes)
            {
                // A topic message adds its mode byte; a client's own topics are always subscribed.
                _maxBytes[index]++;
                _topics[index] = new GesNetworkTopic(network.Roots[index], client);
            }

            if (route.DeliveryClass != GesNetworkClass.Latest) continue;
            if (_localSends[index]) _sendLatest[index] = new GesNetworkLatestSlot(_datagram);
            else if (_datagram) _partialLatest[index] = new GesNetworkPartial();
            else _receiveLatest[index] = new GesNetworkReassembly();
        }

        if (token.Length > GesNetworkWire.MaximumTokenBytes) throw new ArgumentException("A token holds at most 512 bytes.", nameof(token));
        _token = token.ToArray();
        _now = _lastReceive = _lastSend = now;
        _frameUnits = _framesPerSecond * MicrosecondsPerSecond;
        _budgetUnits = BudgetCapacity;
        State = client ? GesNetworkState.Hello : GesNetworkState.AwaitHello;
    }

    /// <summary>Creates the client side of a session, which sends <c>hello</c> on its first transmission.</summary>
    /// <param name="network">The bound network both sides share.</param>
    /// <param name="token">The token the server's host receives with <c>connecting</c>, at most 512 bytes.</param>
    /// <param name="now">The current time in microseconds.</param>
    /// <param name="options">The limits and transport; the defaults when omitted.</param>
    /// <returns>The new connection in state <see cref="GesNetworkState.Hello"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="network"/> is null.</exception>
    /// <exception cref="ArgumentException">The token or an option is out of range.</exception>
    public static GesNetworkConnection CreateClient(GesBoundNetwork network, ReadOnlySpan<byte> token, long now, GesNetworkOptions? options = null) =>
        new(network, true, token, now, options);

    /// <summary>Creates the server side of a session for one client.</summary>
    /// <param name="network">The bound network both sides share.</param>
    /// <param name="now">The current time in microseconds.</param>
    /// <param name="options">The limits and transport; the defaults when omitted.</param>
    /// <returns>The new connection in state <see cref="GesNetworkState.AwaitHello"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="network"/> is null.</exception>
    /// <exception cref="ArgumentException">An option is out of range.</exception>
    public static GesNetworkConnection CreateServer(GesBoundNetwork network, long now, GesNetworkOptions? options = null) =>
        new(network, false, default, now, options);

    /// <summary>Gets the bound network of the session.</summary>
    public GesBoundNetwork Network { get; }

    /// <summary>Gets whether this is the client side.</summary>
    public bool IsClient => _client;

    /// <summary>Gets the transport the packets of <see cref="Transmit"/> and <see cref="Receive"/> are framed for.</summary>
    public GesNetworkTransport Transport => _transport;

    /// <summary>Gets the largest packet this side receives and sends in bytes.</summary>
    public int MaxPacketBytes => _maxPacket;

    /// <summary>Gets the connection state.</summary>
    public GesNetworkState State { get; private set; }

    /// <summary>Gets the latest time passed to the connection in microseconds.</summary>
    public long Now => _now;

    /// <summary>Gets the smoothed round-trip time in microseconds, or -1 before the first sample.</summary>
    public long RoundTripMicroseconds { get; private set; } = -1;

    /// <summary>Gets the received bytes of incomplete messages and undelivered events.</summary>
    public long PendingReceiveBytes => _pending;

    /// <summary>
    /// Gets the queued reliable and bulk bytes not yet completely sent, on a datagram transport not yet completely
    /// acknowledged.
    /// </summary>
    public long QueuedSendBytes => _sendReliable.Bytes + _sendBulk.Bytes;

    /// <summary>
    /// Gets or sets the send budget in bytes per second; 0 for none. The host may change it at any time, for example
    /// from <see cref="RoundTripMicroseconds"/>; this is the congestion hook.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long SendBudgetBytesPerSecond
    {
        get => _budget;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (_budget == 0) _budgetUnits = BudgetCapacity;
            _budget = value;
        }
    }

    /// <summary>
    /// Gets the latest time at which <see cref="Advance"/> must be called: the timeout, keepalive, acknowledgement or
    /// budget refill, and on a datagram transport also the repetition of <c>hello</c>, loss detection or probe timeout;
    /// <see cref="long.MaxValue"/> once closed.
    /// </summary>
    public long NextDeadline
    {
        get
        {
            if (State == GesNetworkState.Closed) return long.MaxValue;
            var deadline = _lastReceive + _timeout;
            if (State == GesNetworkState.Connected) deadline = Math.Min(deadline, _lastSend + _keepalive);
            if (_ackDue) deadline = Math.Min(deadline, _ackDeadline);
            if (_budget > 0 && _budgetUnits < 0 && HasPendingFrames())
                deadline = Math.Min(deadline, _now + (-_budgetUnits + _budget - 1) / _budget);
            if (_datagram) deadline = Math.Min(deadline, DatagramDeadline());
            return deadline;
        }
    }

    private long SendPacketLimit => Math.Min(_maxPacket, _peerPacketLimit);

    private long BudgetCapacity => SendPacketLimit * MicrosecondsPerSecond;

    /// <summary>Moves the connection to the current time and closes it with <c>network.timeout</c> when the peer stayed silent.</summary>
    /// <param name="now">The current time in microseconds, not earlier than <see cref="Now"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="now"/> is earlier than <see cref="Now"/>.</exception>
    public void Advance(long now)
    {
        if (now < _now) throw new ArgumentOutOfRangeException(nameof(now), "Time does not go back.");
        var elapsed = now - _now;
        _now = now;
        _frameUnits = Refill(_frameUnits, _framesPerSecond * MicrosecondsPerSecond, elapsed, _framesPerSecond);
        _budgetUnits = _budget == 0 ? BudgetCapacity : Refill(_budgetUnits, BudgetCapacity, elapsed, _budget);
        if (State != GesNetworkState.Closed && now - _lastReceive >= _timeout) CloseLocal(GesNetworkCodes.Timeout, string.Empty, true);
        else if (_datagram) AdvanceDatagram();
    }

    /// <summary>Sends a payload on a route this side sends. Never throws; the status tells the result.</summary>
    /// <param name="route">The route index of a <c>sends</c> route of this side.</param>
    /// <param name="payload">The encoded root value; it is copied.</param>
    /// <returns>The send status.</returns>
    public GesNetworkSendStatus Send(int route, ReadOnlySpan<byte> payload)
    {
        if ((uint)route >= (uint)_routeCount || !_localSends[route] || _topics[route] is not null) return GesNetworkSendStatus.InvalidRoute;
        if (State != GesNetworkState.Connected) return GesNetworkSendStatus.NotConnected;
        if (payload.Length > _maxBytes[route]) return GesNetworkSendStatus.MessageTooLarge;
        if (_sendLatest[route] is { } slot)
        {
            slot.Set(payload);
            return GesNetworkSendStatus.Queued;
        }

        if (QueuedSendBytes + payload.Length > _maxSendQueue) return GesNetworkSendStatus.QueueFull;
        (_classes[route] == GesNetworkClass.Bulk ? _sendBulk : _sendReliable).Enqueue(route, payload);
        return GesNetworkSendStatus.Queued;
    }

    /// <summary>
    /// Publishes a state on a topic this side publishes. The receiver gets the full record; on the wire it travels as a
    /// delta against a confirmed earlier state when that is shorter. Never throws; the status tells the result.
    /// </summary>
    /// <param name="route">The route index of a <c>publishes</c> route of this side.</param>
    /// <param name="payload">The full record of the topic's schema; it is copied and not validated.</param>
    /// <returns>The send status; <see cref="GesNetworkSendStatus.NotSubscribed"/> for a server topic the client is not subscribed to.</returns>
    public GesNetworkSendStatus Publish(int route, ReadOnlySpan<byte> payload)
    {
        if ((uint)route >= (uint)_routeCount || !_localSends[route] || _topics[route] is not { } topic) return GesNetworkSendStatus.InvalidRoute;
        if (State != GesNetworkState.Connected) return GesNetworkSendStatus.NotConnected;
        if (!topic.Subscribed) return GesNetworkSendStatus.NotSubscribed;
        if (payload.Length >= _maxBytes[route]) return GesNetworkSendStatus.MessageTooLarge;
        if (_sendLatest[route] is { } slot)
        {
            slot.Set(payload);
            return GesNetworkSendStatus.Queued;
        }

        var length = EncodeState(topic, payload, topic.Next);
        if (QueuedSendBytes + length > _maxSendQueue) return GesNetworkSendStatus.QueueFull;
        _sendReliable.Enqueue(route, topic.Scratch.AsSpan(0, length), topic.Next);
        topic.Add(payload, _baselineLimit, topic.Next++);
        return GesNetworkSendStatus.Queued;
    }

    /// <summary>Subscribes the client to a topic the server publishes. Sends nothing by itself; does nothing when subscribed.</summary>
    /// <param name="route">The route index of a topic the server publishes.</param>
    /// <exception cref="InvalidOperationException">This is a client connection.</exception>
    /// <exception cref="ArgumentException">The route is no topic the server publishes.</exception>
    public void Subscribe(int route) => ServerTopic(route).Subscribed = true;

    /// <summary>
    /// Unsubscribes the client from a topic the server publishes: drops its waiting latest state and the ring of sent
    /// states. A latest state already started and queued reliable states are still sent. Does nothing when not subscribed.
    /// </summary>
    /// <param name="route">The route index of a topic the server publishes.</param>
    /// <exception cref="InvalidOperationException">This is a client connection.</exception>
    /// <exception cref="ArgumentException">The route is no topic the server publishes.</exception>
    public void Unsubscribe(int route)
    {
        var topic = ServerTopic(route);
        if (!topic.Subscribed) return;
        topic.Subscribed = false;
        topic.Clear();
        _sendLatest[route]?.DropUnstarted();
    }

    private GesNetworkTopic ServerTopic(int route)
    {
        if (_client) throw new InvalidOperationException("Only the server subscribes clients to topics.");
        if ((uint)route >= (uint)_routeCount || !_localSends[route] || _topics[route] is not { } topic)
            throw new ArgumentException("The route is no topic the server publishes.", nameof(route));
        return topic;
    }

    // Writes the message of a new state into the topic's scratch: a delta against the newest confirmed state when it is
    // shorter than the full record, else the full record. The number is the new state's. Returns the message length.
    private int EncodeState(GesNetworkTopic topic, ReadOnlySpan<byte> record, ushort number)
    {
        var message = topic.Scratch;
        var distance = topic.Base(_datagram ? -1 : _ackedIndex, number, out var baseline);
        if (distance > 0)
        {
            try
            {
                var length = GesSchemaDelta.Encode(topic.Schema, record, baseline, message.AsSpan(1));
                if (length < record.Length)
                {
                    message[0] = (byte)distance;
                    return length + 1;
                }
            }
            catch (GesWireException)
            {
                // A record without a delta, such as one with another key or an invalid one, travels in full.
            }
        }

        message[0] = 0;
        record.CopyTo(message.AsSpan(1));
        return record.Length + 1;
    }

    /// <summary>Accepts the client of a <c>connecting</c> event: the server sends <c>welcome</c> and reports <c>connected</c>.</summary>
    /// <exception cref="InvalidOperationException">The connection is not in state <see cref="GesNetworkState.AwaitAccept"/>.</exception>
    public void Accept()
    {
        if (State != GesNetworkState.AwaitAccept) throw new InvalidOperationException("Only a connecting client can be accepted.");
        State = GesNetworkState.Connected;
        _welcomePending = true;
        AddEvent(GesNetworkEventKind.Connected, -1, default, false, string.Empty, string.Empty, false);
    }

    /// <summary>Refuses the client of a <c>connecting</c> event; the connection closes with <c>network.refused</c>.</summary>
    /// <param name="detail">The detail sent to the client, shortened to 512 UTF-8 bytes.</param>
    /// <exception cref="InvalidOperationException">The connection is not in state <see cref="GesNetworkState.AwaitAccept"/>.</exception>
    public void Refuse(string detail)
    {
        if (State != GesNetworkState.AwaitAccept) throw new InvalidOperationException("Only a connecting client can be refused.");
        CloseLocal(GesNetworkCodes.Refused, detail ?? string.Empty, true);
    }

    /// <summary>Closes the connection with <c>network.closed</c>, drops queued messages and queues a <c>close</c> packet. Does nothing once closed.</summary>
    /// <param name="detail">The detail sent to the peer, shortened to 512 UTF-8 bytes.</param>
    public void Close(string detail)
    {
        if (State != GesNetworkState.Closed) CloseLocal(GesNetworkCodes.Closed, detail ?? string.Empty, true);
    }

    /// <summary>Reports the end of the transport: the connection closes with <c>network.transportClosed</c> unless it has closed already, and sends nothing more.</summary>
    public void TransportClosed()
    {
        _closePending = false;
        if (State != GesNetworkState.Closed) CloseLocal(GesNetworkCodes.TransportClosed, string.Empty, false);
    }

    /// <summary>Takes the next event. Its payload is valid until the next event is taken.</summary>
    /// <returns>The event, or one of kind <see cref="GesNetworkEventKind.None"/> when none is pending.</returns>
    public GesNetworkEvent NextEvent()
    {
        if (_eventHead == _eventCount)
        {
            _eventHead = _eventCount = 0;
            _arenaLength = 0;
            return new GesNetworkEvent(GesNetworkEventKind.None, -1, default, string.Empty, string.Empty, false);
        }

        ref readonly var entry = ref _events[_eventHead++];
        if (entry.Counted) _pending -= entry.Length;
        return new GesNetworkEvent(entry.Kind, entry.Route, _arena.AsSpan(entry.Offset, entry.Length), entry.Code, entry.Detail, entry.ByPeer);
    }

    private void CloseLocal(string code, string detail, bool sendPacket)
    {
        State = GesNetworkState.Closed;
        DropSending();
        string reported = string.Empty;
        if (sendPacket)
        {
            _closePending = true;
            _closeCode = code;
            _closeDetail = GesNetworkWire.DetailBytes(detail);
            reported = _closeDetail.Length == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(_closeDetail);
        }

        AddEvent(GesNetworkEventKind.Closed, -1, default, false, code, reported, false);
    }

    private void DropSending()
    {
        _sendReliable.Clear();
        _sendBulk.Clear();
        foreach (var slot in _sendLatest) slot?.Clear();
        _ackDue = false;
        _welcomePending = false;
        DropDatagramSending();
    }

    private bool HasPendingFrames()
    {
        if (_datagram) return HasPendingDatagramFrames();
        if (_sendReliable.Count > 0 || _sendBulk.Count > 0) return true;
        foreach (var slot in _sendLatest)
        {
            if (slot is { HasCurrent: true }) return true;
        }

        return false;
    }

    // Adds an event; a counted payload adds to the pending received bytes. Returns false when they exceed their limit.
    private bool AddEvent(GesNetworkEventKind kind, int route, ReadOnlySpan<byte> payload, bool counted, string code, string detail, bool byPeer)
    {
        if (counted)
        {
            _pending += payload.Length;
            if (_pending > _maxPending) return false;
        }

        if (_eventCount == _events.Length) Array.Resize(ref _events, _events.Length * 2);
        if (_arena.Length - _arenaLength < payload.Length)
            Array.Resize(ref _arena, Math.Max(Math.Max(_arena.Length * 2, 256), _arenaLength + payload.Length));
        payload.CopyTo(_arena.AsSpan(_arenaLength));
        _events[_eventCount++] = new EventEntry(kind, route, _arenaLength, payload.Length, counted, code, detail, byPeer);
        _arenaLength += payload.Length;
        return true;
    }

    // Refills a token bucket of units per microsecond up to its capacity, without overflow.
    private static long Refill(long units, long capacity, long elapsed, long rate)
    {
        if (units >= capacity) return capacity;
        var room = capacity - units;
        if (elapsed >= room / rate + 1) return capacity;
        return units + elapsed * rate;
    }

    private readonly struct EventEntry(GesNetworkEventKind kind, int route, int offset, int length, bool counted, string code, string detail, bool byPeer)
    {
        internal readonly GesNetworkEventKind Kind = kind;
        internal readonly int Route = route;
        internal readonly int Offset = offset;
        internal readonly int Length = length;
        internal readonly bool Counted = counted;
        internal readonly string Code = code;
        internal readonly string Detail = detail;
        internal readonly bool ByPeer = byPeer;
    }
}
