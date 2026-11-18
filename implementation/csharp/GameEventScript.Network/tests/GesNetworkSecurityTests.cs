// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Network;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkSecurityTests
{
    private const string Source = """
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends :Commands reliable
          server sends :Commands reliable
        }
        """;

    private const int Commands = 0;
    private const int Replies = 1;

    private static readonly GesNetworkKey ServerKey = GesNetworkKey.FromPrivateKey(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

    private static GesBoundNetwork Game() => GesSchemaSet.Parse(Source).Network("Game").Bind();

    private static GesNetworkOptions ClientOptions(GesNetworkTransport transport, GesNetworkSecurity minimum, byte[]? pinned) =>
        new() { Transport = transport, MinimumSecurity = minimum, ServerPublicKey = pinned, InsecureTestKeySeed = 7 };

    private static GesNetworkOptions ServerOptions(GesNetworkTransport transport, GesNetworkKey? key) =>
        new() { Transport = transport, MinimumSecurity = key is null ? GesNetworkSecurity.None : GesNetworkSecurity.Encrypted, Key = key, InsecureTestKeySeed = 7 };

    // Moves every due packet from one side to the other; returns the packets moved.
    private static List<byte[]> Pump(GesNetworkConnection from, GesNetworkConnection to, long now, Func<byte[], byte[]>? change = null)
    {
        var moved = new List<byte[]>();
        var buffer = new byte[70_000];
        for (var length = from.Transmit(buffer); length > 0; length = from.Transmit(buffer))
        {
            var packet = buffer.AsSpan(0, length).ToArray();
            if (change is not null) packet = change(packet);
            moved.Add(packet);
            to.Receive(packet, now);
        }

        return moved;
    }

    private static List<byte[]> Exchange(GesNetworkConnection client, GesNetworkConnection server, long now, Func<byte[], byte[]>? change = null)
    {
        var moved = new List<byte[]>();
        for (var round = 0; round < 4; round++)
        {
            moved.AddRange(Pump(client, server, now, change));
            moved.AddRange(Pump(server, client, now, change));
        }

        return moved;
    }

    private static List<string> Drain(GesNetworkConnection connection)
    {
        var events = new List<string>();
        for (var next = connection.NextEvent(); next.Kind != GesNetworkEventKind.None; next = connection.NextEvent())
        {
            events.Add(next.Kind switch
            {
                GesNetworkEventKind.Closed => $"closed {next.Code}",
                GesNetworkEventKind.Message => $"message {next.Route} {next.Payload[0]}",
                _ => next.Kind.ToString().ToLowerInvariant(),
            });
        }

        return events;
    }

    private static (GesNetworkConnection Client, GesNetworkConnection Server) Connect(GesNetworkTransport transport, GesNetworkSecurity minimum, byte[]? pinned, GesNetworkKey? key)
    {
        var network = Game();
        var client = GesNetworkConnection.CreateClient(network, "t"u8, 0, ClientOptions(transport, minimum, pinned));
        var server = GesNetworkConnection.CreateServer(network, 0, ServerOptions(transport, key));
        Exchange(client, server, 0);
        CollectionAssert.AreEqual(new[] { "connecting" }, Drain(server));
        server.Accept();
        CollectionAssert.AreEqual(new[] { "connected" }, Drain(server));
        Exchange(client, server, 0);
        return (client, server);
    }

    [TestMethod]
    [DataRow(GesNetworkTransport.Stream)]
    [DataRow(GesNetworkTransport.Message)]
    [DataRow(GesNetworkTransport.Datagram)]
    public void PinnedKeyGivesVerifiedSessionWhosePacketsAreSealed(GesNetworkTransport transport)
    {
        var (client, server) = Connect(transport, GesNetworkSecurity.Verified, ServerKey.PublicKey.ToArray(), ServerKey);
        CollectionAssert.AreEqual(new[] { "connected" }, Drain(client));
        Assert.AreEqual(GesNetworkSecurity.Verified, client.Security);
        Assert.AreEqual(GesNetworkSecurity.Encrypted, server.Security);
        Assert.AreEqual(GesNetworkSendStatus.Queued, client.Send(Commands, [0x5a]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Send(Replies, [0x6b]));
        var moved = Exchange(client, server, 1000);
        Assert.IsNotEmpty(moved);
        var prefix = transport == GesNetworkTransport.Stream ? 1 : 0;
        foreach (var packet in moved) Assert.AreEqual(GesNetworkWire.Sealed, packet[prefix]);
        CollectionAssert.AreEqual(new[] { "message 0 90" }, Drain(server));
        CollectionAssert.AreEqual(new[] { "message 1 107" }, Drain(client));
    }

    [TestMethod]
    [DataRow(GesNetworkTransport.Stream)]
    [DataRow(GesNetworkTransport.Datagram)]
    public void KeyFromRetryGivesEncryptedSession(GesNetworkTransport transport)
    {
        var (client, server) = Connect(transport, GesNetworkSecurity.Encrypted, null, ServerKey);
        CollectionAssert.AreEqual(new[] { "connected" }, Drain(client));
        Assert.AreEqual(GesNetworkSecurity.Encrypted, client.Security);
        client.Send(Commands, [3]);
        Exchange(client, server, 1000);
        CollectionAssert.AreEqual(new[] { "message 0 3" }, Drain(server));
    }

    [TestMethod]
    public void PlainClientIsEncryptedByAServerWithAKey()
    {
        var (client, _) = Connect(GesNetworkTransport.Stream, GesNetworkSecurity.None, null, ServerKey);
        CollectionAssert.AreEqual(new[] { "connected" }, Drain(client));
        Assert.AreEqual(GesNetworkSecurity.Encrypted, client.Security);
    }

    [TestMethod]
    public void PlainSessionReportsNone()
    {
        var (client, server) = Connect(GesNetworkTransport.Stream, GesNetworkSecurity.None, null, null);
        CollectionAssert.AreEqual(new[] { "connected" }, Drain(client));
        Assert.AreEqual(GesNetworkSecurity.None, client.Security);
        Assert.AreEqual(GesNetworkSecurity.None, server.Security);
    }

    [TestMethod]
    public void VerifiedMinimumWithoutPinnedKeyClosesUnverified()
    {
        var network = Game();
        var client = GesNetworkConnection.CreateClient(network, default, 0, ClientOptions(GesNetworkTransport.Stream, GesNetworkSecurity.Verified, null));
        var server = GesNetworkConnection.CreateServer(network, 0, ServerOptions(GesNetworkTransport.Stream, ServerKey));
        Exchange(client, server, 0);
        CollectionAssert.AreEqual(new[] { "closed network.unverified" }, Drain(client));
    }

    [TestMethod]
    public void ServerWithoutKeyClosesAKeyRequestInsecure()
    {
        var network = Game();
        var client = GesNetworkConnection.CreateClient(network, default, 0, ClientOptions(GesNetworkTransport.Stream, GesNetworkSecurity.Encrypted, null));
        var server = GesNetworkConnection.CreateServer(network, 0, ServerOptions(GesNetworkTransport.Stream, null));
        Exchange(client, server, 0);
        CollectionAssert.AreEqual(new[] { "closed network.insecure" }, Drain(server));
        CollectionAssert.AreEqual(new[] { "closed network.insecure" }, Drain(client));
    }

    [TestMethod]
    public void WrongPinnedKeyFailsAuthentication()
    {
        var other = GesNetworkKey.FromPrivateKey(new byte[32]).PublicKey.ToArray();
        var network = Game();
        var client = GesNetworkConnection.CreateClient(network, default, 0, ClientOptions(GesNetworkTransport.Stream, GesNetworkSecurity.Verified, other));
        var server = GesNetworkConnection.CreateServer(network, 0, ServerOptions(GesNetworkTransport.Stream, ServerKey));
        Exchange(client, server, 0);
        CollectionAssert.AreEqual(new[] { "closed network.authenticationFailed" }, Drain(server));
    }

    [TestMethod]
    public void ServerNeedsAKeyAboveSecurityNone()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GesNetworkConnection.CreateServer(Game(), 0, new GesNetworkOptions()));
    }

    [TestMethod]
    public void ChangedSealedPacketClosesAStream()
    {
        var (client, server) = Connect(GesNetworkTransport.Message, GesNetworkSecurity.Verified, ServerKey.PublicKey.ToArray(), ServerKey);
        Drain(client);
        client.Send(Commands, [1]);
        Pump(client, server, 1000, packet =>
        {
            packet[^1] ^= 1;
            return packet;
        });
        CollectionAssert.AreEqual(new[] { "closed network.authenticationFailed" }, Drain(server));
    }

    [TestMethod]
    public void ChangedSealedDatagramIsDropped()
    {
        var (client, server) = Connect(GesNetworkTransport.Datagram, GesNetworkSecurity.Verified, ServerKey.PublicKey.ToArray(), ServerKey);
        Drain(client);
        client.Send(Commands, [1]);
        Pump(client, server, 1000, packet =>
        {
            packet[^1] ^= 1;
            return packet;
        });
        Assert.IsEmpty(Drain(server));
        Assert.AreEqual(GesNetworkState.Connected, server.State);
        // A plain data packet on an encrypted session is dropped as well.
        server.Receive([GesNetworkWire.Data, 2, 9, 0], 1000);
        Assert.AreEqual(GesNetworkState.Connected, server.State);
    }

    [TestMethod]
    public void TestSeedMakesTheHandshakeReproducible()
    {
        static List<byte[]> Run()
        {
            var network = Game();
            var client = GesNetworkConnection.CreateClient(network, default, 0, ClientOptions(GesNetworkTransport.Stream, GesNetworkSecurity.Verified, ServerKey.PublicKey.ToArray()));
            var server = GesNetworkConnection.CreateServer(network, 0, ServerOptions(GesNetworkTransport.Stream, ServerKey));
            var moved = Exchange(client, server, 0);
            server.Accept();
            moved.AddRange(Exchange(client, server, 0));
            return moved;
        }

        var first = Run();
        var second = Run();
        Assert.HasCount(first.Count, second);
        for (var index = 0; index < first.Count; index++) CollectionAssert.AreEqual(first[index], second[index]);
    }
}
