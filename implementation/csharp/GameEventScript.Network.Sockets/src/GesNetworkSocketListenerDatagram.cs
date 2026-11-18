// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GameEventScript.Api;

namespace GameEventScript.Network.Sockets;

public sealed partial class GesNetworkSocketListener
{
    // A cookie stays valid for one to two windows.
    private const long CookieWindowMicroseconds = 10_000_000;

    private readonly ConcurrentDictionary<SocketAddress, GesNetworkDatagramPeer> _peers = new();
    private Socket? _datagrams;
    private GesNetworkCookies? _cookies;

    /// <summary>
    /// Starts receiving UDP datagrams for server connections of the datagram transport. Datagrams of an unknown
    /// address pass through address validation with a random key of this listener; a client whose <c>hello</c> carries
    /// a valid cookie becomes ready for <see cref="Accept"/>. The datagrams of a known address go to its session.
    /// </summary>
    /// <param name="endPoint">The local address and port; port 0 lets the system choose.</param>
    /// <param name="network">The bound network of every session.</param>
    /// <param name="options">The options of every server connection, using <see cref="GesNetworkTransport.Datagram"/>; the defaults when omitted.</param>
    /// <returns>The listener. Its sessions send through its socket, so dispose it only after them.</returns>
    /// <exception cref="ArgumentException">The options use another transport or are out of range, or name no key while their lowest security level is above none, as the defaults do.</exception>
    /// <exception cref="SocketException">The address cannot be bound.</exception>
    public static GesNetworkSocketListener ListenUdp(IPEndPoint endPoint, GesBoundNetwork network, GesNetworkOptions? options = null) =>
        new(endPoint, network, options, GesNetworkTransport.Datagram, null);

    private IPEndPoint StartDatagrams(IPEndPoint endPoint)
    {
        var socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            socket.Bind(endPoint);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        _datagrams = socket;
        _cookies = new GesNetworkCookies(RandomNumberGenerator.GetBytes(32), CookieWindowMicroseconds);
        var limit = GesNetworkConnection.CreateServer(_network, 0, _options).MaxPacketBytes;
        _ = Task.Run(() => ReceiveDatagramsAsync(socket, _cookies, limit));
        return (IPEndPoint)socket.LocalEndPoint!;
    }

    private void StopDatagrams()
    {
        if (_datagrams == null) return;
        _datagrams.Dispose();
        _peers.Clear();
    }

    internal void Remove(GesNetworkDatagramPeer peer) => _peers.TryRemove(KeyValuePair.Create(peer.Address, peer));

    // Reads every datagram of the shared socket and routes it by its source address. A datagram longer than the
    // packet limit is passed one byte beyond it, so its connection closes with network.packetTooLarge.
    private async Task ReceiveDatagramsAsync(Socket socket, GesNetworkCookies cookies, int limit)
    {
        var address = new SocketAddress(socket.AddressFamily);
        var template = new IPEndPoint(socket.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
        var retry = new byte[GesNetworkCookies.RetryBytes];
        var key = new byte[18];
        var start = Stopwatch.GetTimestamp();
        try
        {
            while (true)
            {
                var bytes = ArrayPool<byte>.Shared.Rent(limit + 1);
                int length;
                try
                {
                    length = await socket.ReceiveFromAsync(bytes.AsMemory(0, limit + 1), SocketFlags.None, address, _cancel.Token).ConfigureAwait(false);
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.MessageSize || GesNetworkSocketSession.IsTransient(exception.SocketErrorCode))
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                    continue;
                }

                var chunk = new GesNetworkSocketSession.Chunk(bytes, length);
                if (_peers.TryGetValue(address, out var peer))
                {
                    if (!peer.Received.Writer.TryWrite(chunk)) ArrayPool<byte>.Shared.Return(bytes);
                    continue;
                }

                var source = (IPEndPoint)template.Create(address);
                var keyLength = Key(source, key);
                var now = (long)Stopwatch.GetElapsedTime(start).TotalMicroseconds;
                switch (cookies.Check(bytes.AsSpan(0, length), key.AsSpan(0, keyLength), now, retry))
                {
                    case GesNetworkCookieResult.Retry:
                        ArrayPool<byte>.Shared.Return(bytes);
                        try
                        {
                            await socket.SendToAsync(retry, SocketFlags.None, address, _cancel.Token).ConfigureAwait(false);
                        }
                        catch (SocketException exception) when (GesNetworkSocketSession.IsTransient(exception.SocketErrorCode))
                        {
                            // The client sends hello again.
                        }

                        break;
                    case GesNetworkCookieResult.Accept:
                        Admit(socket, address, chunk);
                        break;
                    default:
                        ArrayPool<byte>.Shared.Return(bytes);
                        break;
                }
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException or OperationCanceledException)
        {
            // The listener stopped.
        }
    }

    // Registers a validated address with its hello and offers it to the host; a full queue drops the hello, and the
    // client sends it again.
    private void Admit(Socket socket, SocketAddress address, GesNetworkSocketSession.Chunk hello)
    {
        var copy = new SocketAddress(address.Family, address.Size);
        address.Buffer.Span[..address.Size].CopyTo(copy.Buffer.Span);
        var peer = new GesNetworkDatagramPeer(this, socket, copy);
        peer.Received.Writer.TryWrite(hello);
        _peers[copy] = peer;
        if (!_pending.Writer.TryWrite(new Pending(null, null, peer))) peer.Dispose();
    }

    // The address for validation: the 4 or 16 bytes of the IP address and the port, most significant byte first.
    private static int Key(IPEndPoint source, Span<byte> key)
    {
        source.Address.TryWriteBytes(key, out var length);
        BinaryPrimitives.WriteUInt16BigEndian(key[length..], (ushort)source.Port);
        return length + 2;
    }
}

// One client address of a UDP listener: its received datagrams and its registration.
internal sealed class GesNetworkDatagramPeer(GesNetworkSocketListener listener, Socket socket, SocketAddress address) : IDisposable
{
    internal Socket Socket { get; } = socket;

    internal SocketAddress Address { get; } = address;

    internal System.Threading.Channels.Channel<GesNetworkSocketSession.Chunk> Received { get; } = GesNetworkSocketSession.DatagramChannel();

    // Gives the address back to address validation.
    public void Dispose() => listener.Remove(this);
}
