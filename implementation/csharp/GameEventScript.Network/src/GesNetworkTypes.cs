// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Network;

/// <summary>The codes a connection closes with. Each is stable and language-neutral.</summary>
public static class GesNetworkCodes
{
    /// <summary>A <c>hello</c> has a wrong magic or version.</summary>
    public const string ProtocolMismatch = "network.protocolMismatch";

    /// <summary>The network hash or route count differs.</summary>
    public const string SchemaMismatch = "network.schemaMismatch";

    /// <summary>A packet or frame breaks the wire format or the session order.</summary>
    public const string Malformed = "network.malformed";

    /// <summary>A packet exceeds the packet limit.</summary>
    public const string PacketTooLarge = "network.packetTooLarge";

    /// <summary>A message exceeds its route's largest encoding.</summary>
    public const string MessageTooLarge = "network.messageTooLarge";

    /// <summary>Pending received bytes exceed their limit.</summary>
    public const string QueueFull = "network.queueFull";

    /// <summary>More frames arrive than the frame limit allows.</summary>
    public const string RateExceeded = "network.rateExceeded";

    /// <summary>No packet arrived within the timeout.</summary>
    public const string Timeout = "network.timeout";

    /// <summary>The server's host refused the client.</summary>
    public const string Refused = "network.refused";

    /// <summary>A frame uses a route the peer does not send.</summary>
    public const string UnexpectedRoute = "network.unexpectedRoute";

    /// <summary>The host closed the connection.</summary>
    public const string Closed = "network.closed";

    /// <summary>The transport ended without a <c>close</c> packet.</summary>
    public const string TransportClosed = "network.transportClosed";
}

/// <summary>The result of sending a payload on a route.</summary>
public enum GesNetworkSendStatus
{
    /// <summary>The message is queued.</summary>
    Queued,

    /// <summary>The connection is not connected.</summary>
    NotConnected,

    /// <summary>The reliable and bulk queue would exceed its byte limit.</summary>
    QueueFull,

    /// <summary>The payload is longer than the route root's largest encoding.</summary>
    MessageTooLarge,

    /// <summary>The route index is out of range, or the route is not sent by this side with this verb.</summary>
    InvalidRoute,

    /// <summary>The server's host has not subscribed the client to the topic.</summary>
    NotSubscribed
}

/// <summary>The state of a connection.</summary>
public enum GesNetworkState
{
    /// <summary>A client that has sent or will send <c>hello</c> and waits for <c>welcome</c>.</summary>
    Hello,

    /// <summary>A server that waits for <c>hello</c>.</summary>
    AwaitHello,

    /// <summary>A server that reported <c>connecting</c> and waits for the host to accept or refuse.</summary>
    AwaitAccept,

    /// <summary>Both sides exchange data.</summary>
    Connected,

    /// <summary>The connection has closed; it only sends its own pending <c>close</c> packet.</summary>
    Closed
}

/// <summary>How a transport carries packets.</summary>
public enum GesNetworkTransport
{
    /// <summary>One binary message per packet, for example WebSocket or in-process.</summary>
    Message,

    /// <summary>A byte stream, for example TCP; each packet is preceded by its varint length.</summary>
    Stream
}

/// <summary>The kind of a connection event.</summary>
public enum GesNetworkEventKind
{
    /// <summary>No event is pending.</summary>
    None,

    /// <summary>A client asks to connect; the payload is its token. Server only.</summary>
    Connecting,

    /// <summary>The session is established.</summary>
    Connected,

    /// <summary>A complete message arrived on a route.</summary>
    Message,

    /// <summary>The connection closed.</summary>
    Closed
}

/// <summary>
/// One connection event. The payload is borrowed from the connection and valid until the next event is taken.
/// </summary>
public readonly ref struct GesNetworkEvent
{
    private readonly string? _code;
    private readonly string? _detail;

    internal GesNetworkEvent(GesNetworkEventKind kind, int route, ReadOnlySpan<byte> payload, string code, string detail, bool byPeer)
    {
        Kind = kind;
        Route = route;
        Payload = payload;
        _code = code;
        _detail = detail;
        ByPeer = byPeer;
    }

    /// <summary>Gets the event kind; <see cref="GesNetworkEventKind.None"/> when no event was pending.</summary>
    public GesNetworkEventKind Kind { get; }

    /// <summary>Gets the route index of a message; -1 for other events.</summary>
    public int Route { get; }

    /// <summary>Gets the message payload or the client's token; empty for other events.</summary>
    public ReadOnlySpan<byte> Payload { get; }

    /// <summary>Gets the close code of a closed event; empty for other events.</summary>
    public string Code => _code ?? string.Empty;

    /// <summary>Gets the close detail of a closed event; empty for other events.</summary>
    public string Detail => _detail ?? string.Empty;

    /// <summary>Gets whether the peer sent the close of a closed event.</summary>
    public bool ByPeer { get; }
}

/// <summary>The limits and transport of one connection. Values are checked when a connection is created.</summary>
public sealed class GesNetworkOptions
{
    /// <summary>Gets or sets the transport kind. The default is <see cref="GesNetworkTransport.Message"/>.</summary>
    public GesNetworkTransport Transport { get; set; } = GesNetworkTransport.Message;

    /// <summary>Gets or sets the largest packet received and sent, 1200 to 65535 bytes. The default is 16384.</summary>
    public int MaxPacketBytes { get; set; } = 16384;

    /// <summary>Gets or sets the limit of pending received bytes: incomplete messages and undelivered events. The default is 4194304.</summary>
    public long MaxPendingReceiveBytes { get; set; } = 4194304;

    /// <summary>Gets or sets the frames received per second, a token bucket of one second. The default is 4096.</summary>
    public int MaxFramesPerSecond { get; set; } = 4096;

    /// <summary>Gets or sets the limit of queued reliable and bulk bytes to send. The default is 1048576.</summary>
    public long MaxSendQueueBytes { get; set; } = 1048576;

    /// <summary>Gets or sets the timeout without a received packet in microseconds. The default is 10 seconds.</summary>
    public long TimeoutMicroseconds { get; set; } = 10_000_000;

    /// <summary>Gets or sets the keepalive interval after the last sent packet in microseconds. The default is 1 second.</summary>
    public long KeepaliveMicroseconds { get; set; } = 1_000_000;

    /// <summary>Gets or sets the delay of an acknowledgement in microseconds. The default is 10 milliseconds.</summary>
    public long AckDelayMicroseconds { get; set; } = 10_000;

    /// <summary>Gets or sets the initial send budget in bytes per second; 0 for none. The default is 0.</summary>
    public long SendBudgetBytesPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the baseline limit this side announces, 0 to 1073741824: the full record bytes a topic's ring of recent
    /// states holds. The smaller limit of both sides applies. The default is 65536.
    /// </summary>
    public long MaxBaselineBytes { get; set; } = 65536;

    internal void Validate()
    {
        if (MaxPacketBytes is < GesNetworkWire.MinimumPacketBytes or > GesNetworkWire.MaximumPacketBytes)
            throw new ArgumentOutOfRangeException(nameof(MaxPacketBytes), "The packet limit is 1200 to 65535 bytes.");
        if (MaxPendingReceiveBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxPendingReceiveBytes));
        if (MaxFramesPerSecond < 1) throw new ArgumentOutOfRangeException(nameof(MaxFramesPerSecond));
        if (MaxSendQueueBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxSendQueueBytes));
        if (TimeoutMicroseconds < 1) throw new ArgumentOutOfRangeException(nameof(TimeoutMicroseconds));
        if (KeepaliveMicroseconds < 1) throw new ArgumentOutOfRangeException(nameof(KeepaliveMicroseconds));
        if (AckDelayMicroseconds < 0) throw new ArgumentOutOfRangeException(nameof(AckDelayMicroseconds));
        if (SendBudgetBytesPerSecond < 0) throw new ArgumentOutOfRangeException(nameof(SendBudgetBytesPerSecond));
        if (MaxBaselineBytes is < 0 or > GesNetworkWire.MaximumBaselineBytes) throw new ArgumentOutOfRangeException(nameof(MaxBaselineBytes));
        if (Transport is not (GesNetworkTransport.Message or GesNetworkTransport.Stream))
            throw new ArgumentOutOfRangeException(nameof(Transport));
    }
}
