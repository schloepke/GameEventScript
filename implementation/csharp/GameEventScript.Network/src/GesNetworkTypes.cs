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

    /// <summary>A client that requires <see cref="GesNetworkSecurity.Verified"/> has no pinned key and received a server key.</summary>
    public const string Unverified = "network.unverified";

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
    TransferCancelled
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
        long total)
    {
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
}

/// <summary>The limits and transport of one connection. Values are checked when a connection is created.</summary>
public sealed class GesNetworkOptions
{
    /// <summary>Gets or sets the transport kind. The default is <see cref="GesNetworkTransport.Message"/>.</summary>
    public GesNetworkTransport Transport { get; set; } = GesNetworkTransport.Message;

    /// <summary>
    /// Gets or sets the largest packet received and sent, 1200 to 65535 bytes, or 0 for the transport's default: 1200 on
    /// the datagram transport, else 16384. The default is 0.
    /// </summary>
    public int MaxPacketBytes { get; set; }

    /// <summary>
    /// Gets or sets the limit of pending received bytes, 1 to 1073741824: incomplete messages and undelivered events. The
    /// default is 4194304.
    /// </summary>
    public long MaxPendingReceiveBytes { get; set; } = 4194304;

    /// <summary>Gets or sets the frames received per second, 1 to 1000000, a token bucket of one second. The default is 4096.</summary>
    public int MaxFramesPerSecond { get; set; } = 4096;

    /// <summary>Gets or sets the limit of queued reliable bytes to send, 1 to 1073741824. The default is 1048576.</summary>
    public long MaxSendQueueBytes { get; set; } = 1048576;

    /// <summary>Gets or sets the timeout without a received packet in microseconds, 1 to 3600000000. The default is 10 seconds.</summary>
    public long TimeoutMicroseconds { get; set; } = 10_000_000;

    /// <summary>
    /// Gets or sets the keepalive interval after the last sent packet in microseconds, 1 to 3600000000. The default is 1
    /// second.
    /// </summary>
    public long KeepaliveMicroseconds { get; set; } = 1_000_000;

    /// <summary>Gets or sets the delay of an acknowledgement in microseconds, 0 to 3600000000. The default is 10 milliseconds.</summary>
    public long AckDelayMicroseconds { get; set; } = 10_000;

    /// <summary>Gets or sets the initial send budget in bytes per second, 0 to 1073741824; 0 for none. The default is 0.</summary>
    public long SendBudgetBytesPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the baseline limit this side announces, 0 to 1073741824: the full record bytes a topic's ring of recent
    /// states holds. The smaller limit of both sides applies. The default is 65536.
    /// </summary>
    public long MaxBaselineBytes { get; set; } = 65536;

    /// <summary>
    /// Gets or sets the transfer limit this side announces, 0 to 1073741824: the largest uncompressed data of one bulk
    /// message it sends or receives. It also bounds the bulk send queue. The default is 67108864.
    /// </summary>
    public long MaxTransferBytes { get; set; } = 67108864;

    /// <summary>Gets or sets whether this side accepts and sends deflate-compressed transfers. The default is true.</summary>
    public bool DeflateTransfers { get; set; } = true;

    /// <summary>
    /// Gets or sets the lowest security level this side accepts. A client without a pinned key closes with
    /// <see cref="GesNetworkCodes.Unverified"/> when it requires <see cref="GesNetworkSecurity.Verified"/>; a server above
    /// <see cref="GesNetworkSecurity.None"/> needs a <see cref="Key"/>. The default is <see cref="GesNetworkSecurity.Verified"/>.
    /// </summary>
    public GesNetworkSecurity MinimumSecurity { get; set; } = GesNetworkSecurity.Verified;

    /// <summary>
    /// Gets or sets the server's pinned public key of 32 bytes; client only. A client with a pinned key seals its first
    /// <c>hello</c> and reaches <see cref="GesNetworkSecurity.Verified"/>. The default is none. The array is copied when a
    /// connection is created.
    /// </summary>
    public byte[]? ServerPublicKey { get; set; }

    /// <summary>Gets or sets the server's key; server only. A server with a key encrypts every session. The default is none.</summary>
    public GesNetworkKey? Key { get; set; }

    /// <summary>
    /// Gets or sets a seed that derives the connection's ephemeral keys instead of the system's random generator.
    /// INSECURE: anyone who knows the seed reads the session. For tests and golden bytes only: the socket transports
    /// refuse such options and connections. The default is none.
    /// </summary>
    public ulong? InsecureTestKeySeed { get; set; }

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
        if (TimeoutMicroseconds is < 1 or > MaximumInterval) throw new ArgumentOutOfRangeException(nameof(TimeoutMicroseconds));
        if (KeepaliveMicroseconds is < 1 or > MaximumInterval) throw new ArgumentOutOfRangeException(nameof(KeepaliveMicroseconds));
        if (AckDelayMicroseconds is < 0 or > MaximumInterval) throw new ArgumentOutOfRangeException(nameof(AckDelayMicroseconds));
        if (SendBudgetBytesPerSecond is < 0 or > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(SendBudgetBytesPerSecond));
        if (MaxBaselineBytes is < 0 or > GesNetworkWire.MaximumBaselineBytes) throw new ArgumentOutOfRangeException(nameof(MaxBaselineBytes));
        if (MaxTransferBytes is < 0 or > GesNetworkWire.MaximumTransferBytes) throw new ArgumentOutOfRangeException(nameof(MaxTransferBytes));
        if (Transport is not (GesNetworkTransport.Message or GesNetworkTransport.Stream or GesNetworkTransport.Datagram))
            throw new ArgumentOutOfRangeException(nameof(Transport));
        if (MinimumSecurity is not (GesNetworkSecurity.None or GesNetworkSecurity.Encrypted or GesNetworkSecurity.Verified))
            throw new ArgumentOutOfRangeException(nameof(MinimumSecurity));
        if (ServerPublicKey is { Length: not GesNetworkX25519.KeyBytes }) throw new ArgumentException("A server public key holds 32 bytes.", nameof(ServerPublicKey));
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
