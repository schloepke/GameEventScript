// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Network;

/// <summary>The codes a connection closes or cancels a transfer with. Each is stable and language-neutral.</summary>
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

    /// <summary>A transfer announces more bytes than the receiver's transfer limit.</summary>
    public const string TransferTooLarge = "network.transferTooLarge";

    /// <summary>A transfer's header, compressed data or hash is invalid.</summary>
    public const string TransferCorrupt = "network.transferCorrupt";

    /// <summary>The host cancelled a transfer without giving its own code.</summary>
    public const string TransferCancelled = "network.transferCancelled";

    /// <summary>A server without a key received a key request or a sealed <c>hello</c>.</summary>
    public const string Insecure = "network.insecure";

    /// <summary>A sealed <c>hello</c>, <c>welcome</c> or packet does not authenticate.</summary>
    public const string AuthenticationFailed = "network.authenticationFailed";
}

/// <summary>The result of sending a payload on a route.</summary>
public enum GesNetworkSendStatus
{
    /// <summary>The message is queued.</summary>
    Queued,

    /// <summary>The connection is not connected.</summary>
    NotConnected,

    /// <summary>The reliable queue or the bulk queue would exceed its limit.</summary>
    QueueFull,

    /// <summary>
    /// The payload is longer than the route root's largest encoding, or, on a bulk route, than this side's or the peer's
    /// transfer limit.
    /// </summary>
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
    Stream,

    /// <summary>
    /// One datagram per packet, for example UDP. Packets may be lost, duplicated, delayed or reordered; the connection
    /// resends what is lost and restores the order of each class.
    /// </summary>
    Datagram
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
    Closed,

    /// <summary>
    /// A transfer on a bulk route advanced: the receiver got more of its bytes, or the peer confirmed more of the bytes sent.
    /// </summary>
    TransferProgress,

    /// <summary>A transfer was cancelled by this side, by its host or by the peer; the session continues.</summary>
    TransferCancelled,

    /// <summary>
    /// A tick is complete: the message events of its changed members precede it. <see cref="GesNetworkEvent.TickNumber"/>
    /// tells its number.
    /// </summary>
    Tick
}

/// <summary>
/// One connection event. The payload is borrowed from the connection and valid until the next event is taken.
/// </summary>
public readonly ref struct GesNetworkEvent
{
    private readonly string? _code;
    private readonly string? _detail;

    internal GesNetworkEvent(
        GesNetworkEventKind kind,
        int route,
        ReadOnlySpan<byte> payload,
        string code,
        string detail,
        bool byPeer,
        int transferId,
        long bytes,
        long total,
        int tickNumber = -1)
    {
        TickNumber = tickNumber;
        Kind = kind;
        Route = route;
        Payload = payload;
        _code = code;
        _detail = detail;
        ByPeer = byPeer;
        TransferId = transferId;
        Bytes = bytes;
        Total = total;
    }

    /// <summary>Gets the event kind; <see cref="GesNetworkEventKind.None"/> when no event was pending.</summary>
    public GesNetworkEventKind Kind { get; }

    /// <summary>Gets the route index of a message or transfer event; -1 for other events.</summary>
    public int Route { get; }

    /// <summary>Gets the message payload or the client's token; empty for other events.</summary>
    public ReadOnlySpan<byte> Payload { get; }

    /// <summary>Gets the close code of a closed event or the code of a cancelled transfer; empty for other events.</summary>
    public string Code => _code ?? string.Empty;

    /// <summary>Gets the close detail of a closed event; empty for other events.</summary>
    public string Detail => _detail ?? string.Empty;

    /// <summary>Gets whether the peer sent the close of a closed event or cancelled the transfer of a cancelled event.</summary>
    public bool ByPeer { get; }

    /// <summary>Gets the transfer id, 0 to 65535, of a transfer event or of a message on a bulk route; -1 for other events.</summary>
    public int TransferId { get; }

    /// <summary>
    /// Gets the bytes of a progress event: received ones on the receiver, confirmed ones on the sender. Both count the
    /// transfer's encoded bytes; 0 for other events.
    /// </summary>
    public long Bytes { get; }

    /// <summary>Gets the encoded length of a progress event's transfer; 0 for other events.</summary>
    public long Total { get; }

    /// <summary>Gets the number, 0 to 65535, of a tick event; -1 for other events.</summary>
    public int TickNumber { get; }
}

/// <summary>The limits and transport of one connection. Values are checked when a connection is created.</summary>
public sealed class GesNetworkOptions
{
    /// <summary>
    /// Creates the options of a connection within one process, such as a loopback: the message transport, packets of 65535
    /// bytes, no acknowledgement delay, no timeout or keepalive and no encryption on either side.
    /// </summary>
    /// <returns>New options; change them freely.</returns>
    public static GesNetworkOptions InProcess() => new()
    {
        Transport = GesNetworkTransport.Message,
        MaxPacketBytes = GesNetworkWire.MaximumPacketBytes,
        AckDelayMicroseconds = 0,
        TimeoutMicroseconds = 0,
        KeepaliveMicroseconds = 0,
        ClientSecurity = GesNetworkClientSecurity.None,
        ServerSecurity = GesNetworkServerSecurity.None,
    };

    /// <summary>Creates the options of a client on the message transport, such as a WebSocket.</summary>
    /// <param name="security">How the client protects its session.</param>
    /// <returns>New options; change them freely.</returns>
    public static GesNetworkOptions Message(GesNetworkClientSecurity security) => Client(GesNetworkTransport.Message, security);

    /// <summary>Creates the options of a server on the message transport, such as a WebSocket.</summary>
    /// <param name="security">How the server protects its sessions.</param>
    /// <returns>New options; change them freely.</returns>
    public static GesNetworkOptions Message(GesNetworkServerSecurity security) => Server(GesNetworkTransport.Message, security);

    /// <summary>Creates the options of a client on the stream transport, such as TCP.</summary>
    /// <param name="security">How the client protects its session.</param>
    /// <returns>New options; change them freely.</returns>
    public static GesNetworkOptions Stream(GesNetworkClientSecurity security) => Client(GesNetworkTransport.Stream, security);

    /// <summary>Creates the options of a server on the stream transport, such as TCP.</summary>
    /// <param name="security">How the server protects its sessions.</param>
    /// <returns>New options; change them freely.</returns>
    public static GesNetworkOptions Stream(GesNetworkServerSecurity security) => Server(GesNetworkTransport.Stream, security);

    /// <summary>Creates the options of a client on the datagram transport, such as UDP.</summary>
    /// <param name="security">How the client protects its session.</param>
    /// <returns>New options; change them freely.</returns>
    public static GesNetworkOptions Datagram(GesNetworkClientSecurity security) => Client(GesNetworkTransport.Datagram, security);

    /// <summary>Creates the options of a server on the datagram transport, such as UDP.</summary>
    /// <param name="security">How the server protects its sessions.</param>
    /// <returns>New options; change them freely.</returns>
    public static GesNetworkOptions Datagram(GesNetworkServerSecurity security) => Server(GesNetworkTransport.Datagram, security);

    private static GesNetworkOptions Client(GesNetworkTransport transport, GesNetworkClientSecurity security) =>
        new() { Transport = transport, ClientSecurity = security ?? throw new ArgumentNullException(nameof(security)) };

    private static GesNetworkOptions Server(GesNetworkTransport transport, GesNetworkServerSecurity security) =>
        new() { Transport = transport, ServerSecurity = security ?? throw new ArgumentNullException(nameof(security)) };

    /// <summary>Gets or sets the transport kind. The default is <see cref="GesNetworkTransport.Message"/>.</summary>
    public GesNetworkTransport Transport { get; set; } = GesNetworkTransport.Message;

    /// <summary>
    /// Gets or sets the largest packet received and sent, 1200 to 65535 bytes, or 0 for the transport's default: 1200 on
    /// the datagram transport, else 16384. The default is 0.
    /// </summary>
    public int MaxPacketBytes { get; set; }

    /// <summary>
    /// Gets or sets the limit of pending received bytes, 1 to 1073741824: incomplete messages and undelivered events. Unset,
    /// the default, it is the larger of 4194304 and the largest message this side receives outside bulk routes, topic mode
    /// byte included, at most 1073741824.
    /// </summary>
    public long? MaxPendingReceiveBytes { get; set; }

    /// <summary>Gets or sets the frames received per second, 1 to 1000000, a token bucket of one second. The default is 4096.</summary>
    public int MaxFramesPerSecond { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the limit of queued reliable bytes to send, 1 to 1073741824. Unset, the default, it is the larger of
    /// 1048576 and the largest reliable payload this side sends, at most 1073741824.
    /// </summary>
    public long? MaxSendQueueBytes { get; set; }

    /// <summary>
    /// Gets or sets the timeout without a received packet in microseconds, 1 to 3600000000, or 0 for none. The default is 10
    /// seconds.
    /// </summary>
    public long TimeoutMicroseconds { get; set; } = 10_000_000;

    /// <summary>
    /// Gets or sets the keepalive interval after the last sent packet in microseconds, 1 to 3600000000, or 0 for none. The
    /// default is 1 second.
    /// </summary>
    public long KeepaliveMicroseconds { get; set; } = 1_000_000;

    /// <summary>Gets or sets the delay of an acknowledgement in microseconds, 0 to 3600000000. The default is 10 milliseconds.</summary>
    public long AckDelayMicroseconds { get; set; } = 10_000;

    /// <summary>Gets or sets the initial send budget in bytes per second, 0 to 1073741824; 0 for none. The default is 0.</summary>
    public long SendBudgetBytesPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the baseline limit this side announces, 0 to 1073741824: the full record bytes a topic's ring of recent
    /// states holds. The smaller limit of both sides applies. Unset, the default, it is the larger of 65536 and the largest
    /// full record of a topic of the network, at most 1073741824.
    /// </summary>
    public long? MaxBaselineBytes { get; set; }

    /// <summary>
    /// Gets or sets the transfer limit this side announces, 0 to 1073741824: the largest uncompressed data of one bulk
    /// message it sends or receives. It also bounds the bulk send queue. Unset, the default, it is the larger of 67108864
    /// and the largest record of a bulk route of the network, at most 1073741824.
    /// </summary>
    public long? MaxTransferBytes { get; set; }

    /// <summary>Gets or sets whether this side accepts and sends deflate-compressed transfers. The default is true.</summary>
    public bool DeflateTransfers { get; set; } = true;

    /// <summary>
    /// Gets or sets how a client protects its session; required to create a client. The default is none set.
    /// </summary>
    public GesNetworkClientSecurity? ClientSecurity { get; set; }

    /// <summary>
    /// Gets or sets how a server protects its sessions; required to create a server. The default is none set.
    /// </summary>
    public GesNetworkServerSecurity? ServerSecurity { get; set; }

    /// <summary>
    /// Gets or sets a seed that derives the connection's ephemeral keys instead of the system's random generator.
    /// INSECURE: anyone who knows the seed reads the session. For tests and golden bytes only: the socket transports
    /// refuse such options and connections. The default is none.
    /// </summary>
    public ulong? InsecureTestKeySeed { get; set; }

    // The defaults of the derived byte limits.
    internal const long DefaultPendingReceiveBytes = 4194304;
    internal const long DefaultSendQueueBytes = 1048576;
    internal const long DefaultBaselineBytes = 65536;
    internal const long DefaultTransferBytes = 67108864;

    // The largest byte limits and send budget, frame rate and time interval in microseconds.
    internal const long MaximumBytes = 1 << 30;
    internal const int MaximumFramesPerSecond = 1_000_000;
    internal const long MaximumInterval = 3_600_000_000;

    // The packet limit in effect: the configured one, or the transport's default.
    internal int PacketLimit => MaxPacketBytes != 0 ? MaxPacketBytes : Transport == GesNetworkTransport.Datagram ? GesNetworkWire.MinimumPacketBytes : 16384;

    internal void Validate()
    {
        if (MaxPacketBytes != 0 && MaxPacketBytes is < GesNetworkWire.MinimumPacketBytes or > GesNetworkWire.MaximumPacketBytes)
            throw new ArgumentOutOfRangeException(nameof(MaxPacketBytes), "The packet limit is 0 or 1200 to 65535 bytes.");
        if (MaxPendingReceiveBytes is < 1 or > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(MaxPendingReceiveBytes));
        if (MaxFramesPerSecond is < 1 or > MaximumFramesPerSecond) throw new ArgumentOutOfRangeException(nameof(MaxFramesPerSecond));
        if (MaxSendQueueBytes is < 1 or > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(MaxSendQueueBytes));
        if (TimeoutMicroseconds is < 0 or > MaximumInterval) throw new ArgumentOutOfRangeException(nameof(TimeoutMicroseconds));
        if (KeepaliveMicroseconds is < 0 or > MaximumInterval) throw new ArgumentOutOfRangeException(nameof(KeepaliveMicroseconds));
        if (AckDelayMicroseconds is < 0 or > MaximumInterval) throw new ArgumentOutOfRangeException(nameof(AckDelayMicroseconds));
        if (SendBudgetBytesPerSecond is < 0 or > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(SendBudgetBytesPerSecond));
        if (MaxBaselineBytes is < 0 or > GesNetworkWire.MaximumBaselineBytes) throw new ArgumentOutOfRangeException(nameof(MaxBaselineBytes));
        if (MaxTransferBytes is < 0 or > GesNetworkWire.MaximumTransferBytes) throw new ArgumentOutOfRangeException(nameof(MaxTransferBytes));
        if (Transport is not (GesNetworkTransport.Message or GesNetworkTransport.Stream or GesNetworkTransport.Datagram))
            throw new ArgumentOutOfRangeException(nameof(Transport));
    }
}

/// <summary>The security level of a session.</summary>
public enum GesNetworkSecurity
{
    /// <summary>The session is not encrypted.</summary>
    None,

    /// <summary>
    /// The session is encrypted with a server key the client received during the handshake; it protects only against
    /// attackers who read but do not change packets. A server reports this level for every encrypted session.
    /// </summary>
    Encrypted,

    /// <summary>The session is encrypted with the server key the client was given in advance.</summary>
    Verified,
}
