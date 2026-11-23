// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Network;

/// <summary>
/// One connection's transport as an end drives it: it passes received packets to the connection and hands the
/// connection's packets to the transport. Implementations come from a connector or an acceptor.
/// </summary>
public interface IGesNetworkLink : IDisposable
{
    /// <summary>Gets the connection this link carries.</summary>
    GesNetworkConnection Connection { get; }

    /// <summary>
    /// Passes every received packet to the connection, advances it to <paramref name="now"/> and reports the end of
    /// the transport with <see cref="GesNetworkConnection.TransportClosed"/>.
    /// </summary>
    /// <param name="now">The current time in microseconds.</param>
    void Receive(long now);

    /// <summary>Hands every packet the connection has due to the transport.</summary>
    void Transmit();
}

/// <summary>Opens the transport of a client end.</summary>
public interface IGesNetworkConnector
{
    /// <summary>Gets the transport the connector opens; the client end's options must use it.</summary>
    GesNetworkTransport Transport { get; }

    /// <summary>
    /// Starts opening the transport of a client connection without blocking. The link may still be connecting: it
    /// takes packets once the transport is open, and a failure to open ends the transport.
    /// </summary>
    /// <param name="connection">The client connection.</param>
    /// <returns>The link of the connection.</returns>
    IGesNetworkLink Connect(GesNetworkConnection connection);
}

/// <summary>Accepts the transports of a server end's clients.</summary>
public interface IGesNetworkAcceptor : IDisposable
{
    /// <summary>Takes the next accepted client without blocking.</summary>
    /// <param name="now">The current time in microseconds, which the new server connection starts at.</param>
    /// <returns>The link of a new server connection, or null when no client waits.</returns>
    IGesNetworkLink? Accept(long now);
}

/// <summary>The kind of an end event.</summary>
public enum GesNetworkEndEventKind
{
    /// <summary>
    /// A client asks to connect; the payload is its token. Server only: the end accepts it after the handler returns
    /// unless the handler refused it with <see cref="GesNetworkServerEnd.Refuse"/>.
    /// </summary>
    Connecting,

    /// <summary>The client's session is established, with the reached security. Client only.</summary>
    Connected,

    /// <summary>The client's connection closed, with its code, detail and whether the peer closed it. Client only.</summary>
    Disconnected,

    /// <summary>A client was accepted and is connected, with the reached security. Server only.</summary>
    ClientConnected,

    /// <summary>A client's connection closed, with its code, detail and whether the peer closed it; the client leaves the end. Server only.</summary>
    ClientDisconnected,

    /// <summary>A complete message arrived on a route.</summary>
    Message,

    /// <summary>A tick is complete: the message events of its changed members precede it.</summary>
    Tick,

    /// <summary>A transfer on a bulk route advanced.</summary>
    TransferProgress,

    /// <summary>A transfer was cancelled; the session continues.</summary>
    TransferCancelled,

    /// <summary>The last event of a poll that reported at least one message or tick.</summary>
    Received
}

/// <summary>One end event. The payload is borrowed and valid until the handler returns.</summary>
public readonly ref struct GesNetworkEndEvent
{
    private readonly string? _code;
    private readonly string? _detail;

    internal GesNetworkEndEvent(GesNetworkEndEventKind kind, int client, GesNetworkEvent source, GesNetworkSecurity security)
    {
        Kind = kind;
        Client = client;
        Route = source.Route;
        Payload = source.Payload;
        _code = source.Code;
        _detail = source.Detail;
        ByPeer = source.ByPeer;
        Security = security;
        TickNumber = source.TickNumber;
        TransferId = source.TransferId;
        Bytes = source.Bytes;
        Total = source.Total;
    }

    internal GesNetworkEndEvent(GesNetworkEndEventKind kind, int client, GesNetworkSecurity security)
    {
        Kind = kind;
        Client = client;
        Route = -1;
        Security = security;
        TickNumber = -1;
        TransferId = -1;
    }

    /// <summary>Gets the event kind.</summary>
    public GesNetworkEndEventKind Kind { get; }

    /// <summary>Gets the end-local id of the client on a server end; -1 on a client end and for <see cref="GesNetworkEndEventKind.Received"/>.</summary>
    public int Client { get; }

    /// <summary>Gets the route index of a message or transfer event; -1 for other events.</summary>
    public int Route { get; }

    /// <summary>Gets the message payload or the client's token; empty for other events.</summary>
    public ReadOnlySpan<byte> Payload { get; }

    /// <summary>Gets the close code of a disconnect or the code of a cancelled transfer; empty for other events.</summary>
    public string Code => _code ?? string.Empty;

    /// <summary>Gets the close detail of a disconnect; empty for other events.</summary>
    public string Detail => _detail ?? string.Empty;

    /// <summary>Gets whether the peer closed the connection of a disconnect or cancelled the transfer of a cancelled event.</summary>
    public bool ByPeer { get; }

    /// <summary>Gets the reached security of a connect event; <see cref="GesNetworkSecurity.None"/> for other events.</summary>
    public GesNetworkSecurity Security { get; }

    /// <summary>Gets the number, 0 to 65535, of a tick event; -1 for other events.</summary>
    public int TickNumber { get; }

    /// <summary>Gets the transfer id of a transfer event or of a message on a bulk route; -1 for other events.</summary>
    public int TransferId { get; }

    /// <summary>Gets the bytes of a progress event; 0 for other events.</summary>
    public long Bytes { get; }

    /// <summary>Gets the encoded length of a progress event's transfer; 0 for other events.</summary>
    public long Total { get; }
}

/// <summary>Receives the events of an end during its poll.</summary>
public interface IGesNetworkEndHandler
{
    /// <summary>Handles one event. It may send, publish, close and open or close ticks on the end.</summary>
    /// <param name="networkEvent">The event; its payload is valid until the method returns.</param>
    void Handle(GesNetworkEndEvent networkEvent);
}

// Translates connection events into end events.
internal static class GesNetworkEnds
{
    internal static GesNetworkEndEventKind Kind(GesNetworkEventKind kind, bool client) => kind switch
    {
        GesNetworkEventKind.Connecting => GesNetworkEndEventKind.Connecting,
        GesNetworkEventKind.Connected => client ? GesNetworkEndEventKind.Connected : GesNetworkEndEventKind.ClientConnected,
        GesNetworkEventKind.Closed => client ? GesNetworkEndEventKind.Disconnected : GesNetworkEndEventKind.ClientDisconnected,
        GesNetworkEventKind.Message => GesNetworkEndEventKind.Message,
        GesNetworkEventKind.Tick => GesNetworkEndEventKind.Tick,
        GesNetworkEventKind.TransferProgress => GesNetworkEndEventKind.TransferProgress,
        _ => GesNetworkEndEventKind.TransferCancelled,
    };

    // A handler for polls that report nothing, such as the handshake of a synchronous pair.
    internal struct Silent : IGesNetworkEndHandler
    {
        public readonly void Handle(GesNetworkEndEvent networkEvent)
        {
        }
    }
}
