// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;
using GameEventScript.Network;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkConnectionTests
{
    private const string Source = """
        wire schema :World as { tick: :Number integer between 0 and 65535 }
        wire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }
        wire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends :Commands reliable
          server sends :Blob reliable
          server publishes :World latest at most every 50ms
          server sends :Pack bulk
        }
        """;

    private const int Commands = 0;
    private const int Blob = 1;
    private const int World = 2;
    private const int BulkBlob = 3;

    private static readonly GesNetworkKey TestKey = GesNetworkKey.FromPrivateKey(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

    private static readonly GesNetworkOptions Plain = new() { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None };

    private static GesBoundNetwork Game(string source = Source) => GesSchemaSet.Parse(source).Network("Game").Bind();

    private static GesNetworkSimulator Connected(GesNetworkOptions? options = null, GesNetworkOptions? serverOptions = null)
    {
        var network = Game();
        var client = GesNetworkConnection.CreateClient(network, "t"u8, 0, options ?? Plain);
        var server = GesNetworkConnection.CreateServer(network, 0, serverOptions ?? options ?? Plain);
        var loop = new GesNetworkSimulator(client, server, 0, 0, 0);
        loop.Advance(0);
        CollectionAssert.AreEqual(new[] { "connecting t" }, Drain(server));
        server.Accept();
        loop.Advance(0);
        CollectionAssert.AreEqual(new[] { "connected" }, Drain(client));
        CollectionAssert.AreEqual(new[] { "connected" }, Drain(server));
        Assert.AreEqual(GesNetworkState.Connected, client.State);
        return loop;
    }

    private static List<string> Drain(GesNetworkConnection connection)
    {
        var events = new List<string>();
        while (true)
        {
            var next = connection.NextEvent();
            switch (next.Kind)
            {
                case GesNetworkEventKind.None:
                    return events;
                case GesNetworkEventKind.Connecting:
                    events.Add("connecting " + Encoding.UTF8.GetString(next.Payload));
                    break;
                case GesNetworkEventKind.Connected:
                    events.Add("connected");
                    break;
                case GesNetworkEventKind.Message:
                    events.Add($"message {next.Route} {Convert.ToHexString(next.Payload)}");
                    break;
                case GesNetworkEventKind.Closed:
                    events.Add($"closed {next.Code} '{next.Detail}' {(next.ByPeer ? "peer" : "local")}");
                    break;
            }
        }
    }

    private static byte[] Bytes(int length, int seed)
    {
        var bytes = new byte[length];
        for (var index = 0; index < length; index++) bytes[index] = (byte)(index * 7 + seed);
        return bytes;
    }

    [TestMethod]
    public void UntakenEventsBeyondTheLimitCloseWithQueueFull()
    {
        var loop = Connected(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxFramesPerSecond = 1_000_000 });
        // At most 65536 reliable messages wait to be sent, so the last one follows once the first have left.
        for (var index = 0; index < 65536; index++) Assert.AreEqual(GesNetworkSendStatus.Queued, loop.Server.Send(Blob, [0]));
        loop.Advance(0);
        Assert.AreEqual(GesNetworkSendStatus.Queued, loop.Server.Send(Blob, [0]));
        for (var now = 10_000L; now < 60_000_000 && loop.Client.State != GesNetworkState.Closed; now += 10_000) loop.Advance(now);
        var events = Drain(loop.Client);
        Assert.HasCount(65537, events);
        Assert.AreEqual(65536, events.Count(entry => entry == $"message {Blob} 00"));
        Assert.AreEqual("closed network.queueFull '' local", events[^1]);
    }

    [TestMethod]
    public void TakenEventsFreeTheirPlaceAndKeepTheirOrder()
    {
        var loop = Connected(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxFramesPerSecond = 1_000_000 });
        var expected = new List<string>();
        var received = new List<string>();
        var now = 0L;
        for (var round = 0; round < 3; round++)
        {
            for (var index = 0; index < 40_000; index++)
            {
                var payload = new byte[] { 1, (byte)(round * 40_000 + index) };
                Assert.AreEqual(GesNetworkSendStatus.Queued, loop.Server.Send(Blob, payload));
                expected.Add($"message {Blob} {Convert.ToHexString(payload)}");
            }

            for (var step = 0; step < 100; step++) loop.Advance(now += 10_000);
            for (var index = 0; index < 30_000; index++) received.Add(Message(loop.Client.NextEvent()));
        }

        received.AddRange(Drain(loop.Client));
        Assert.AreEqual(GesNetworkState.Connected, loop.Client.State);
        CollectionAssert.AreEqual(expected, received);
    }

    private static string Message(GesNetworkEvent next)
    {
        Assert.AreEqual(GesNetworkEventKind.Message, next.Kind);
        return $"message {next.Route} {Convert.ToHexString(next.Payload)}";
    }

    [TestMethod]
    public void HandshakeReportsTheTokenAndConnectsBothSides()
    {
        var loop = Connected();
        Assert.AreEqual(GesNetworkState.Connected, loop.Server.State);
    }

    [TestMethod]
    public void RefusalClosesBothSidesWithTheHostDetail()
    {
        var network = Game();
        var client = GesNetworkConnection.CreateClient(network, default, 0, Plain);
        var server = GesNetworkConnection.CreateServer(network, 0, Plain);
        var loop = new GesNetworkSimulator(client, server, 0, 0, 0);
        loop.Advance(0);
        Drain(server);
        server.Refuse("full");
        loop.Advance(0);
        CollectionAssert.AreEqual(new[] { "closed network.refused 'full' local" }, Drain(server));
        CollectionAssert.AreEqual(new[] { "closed network.refused 'full' peer" }, Drain(client));
    }

    [TestMethod]
    public void SchemaMismatchNamesTheDifferingRoots()
    {
        var other = Source.Replace("max 6000", "max 5000");
        var client = GesNetworkConnection.CreateClient(Game(other), default, 0, Plain);
        var server = GesNetworkConnection.CreateServer(Game(), 0, Plain);
        var loop = new GesNetworkSimulator(client, server, 0, 0, 0);
        loop.Advance(0);
        CollectionAssert.AreEqual(new[] { "closed network.schemaMismatch 'Blob, Pack' local" }, Drain(server));
        CollectionAssert.AreEqual(new[] { "closed network.schemaMismatch 'Blob, Pack' peer" }, Drain(client));
    }

    [TestMethod]
    public void SchemaMismatchWithEqualRootsNamesTheNetwork()
    {
        var other = Source.Replace("at most every 50ms", "at most every 60ms");
        var client = GesNetworkConnection.CreateClient(Game(other), default, 0, Plain);
        var server = GesNetworkConnection.CreateServer(Game(), 0, Plain);
        new GesNetworkSimulator(client, server, 0, 0, 0).Advance(0);
        CollectionAssert.AreEqual(new[] { "closed network.schemaMismatch 'Game' local" }, Drain(server));
    }

    [TestMethod]
    public void ReliableAndBulkMessagesArriveCompleteInOrderAcrossFragments()
    {
        var options = new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxPacketBytes = 1200 };
        var loop = Connected(options);
        var first = Bytes(3000, 1);
        var second = Bytes(10, 2);
        var bulk = Bytes(5000, 3);
        Assert.AreEqual(GesNetworkSendStatus.Queued, loop.Server.Send(BulkBlob, bulk));
        Assert.AreEqual(GesNetworkSendStatus.Queued, loop.Server.Send(Blob, first));
        Assert.AreEqual(GesNetworkSendStatus.Queued, loop.Server.Send(Blob, second));
        loop.Advance(1);
        CollectionAssert.AreEqual(
            new[]
            {
                $"message {Blob} {Convert.ToHexString(first)}",
                $"message {Blob} {Convert.ToHexString(second)}",
                $"message {BulkBlob} {Convert.ToHexString(bulk)}",
            },
            Drain(loop.Client));
        Assert.AreEqual(0, loop.Server.QueuedSendBytes);
        Assert.AreEqual(0, loop.Client.PendingReceiveBytes);
    }

    [TestMethod]
    public void LatestDeliversOnlyTheNewestUnstartedMessage()
    {
        var loop = Connected();
        loop.Server.Subscribe(World);
        loop.Server.Publish(World, [1, 0]);
        loop.Server.Publish(World, [2, 0]);
        loop.Server.Publish(World, [3, 0]);
        loop.Advance(1);
        CollectionAssert.AreEqual(new[] { $"message {World} 0300" }, Drain(loop.Client));
        loop.Server.Publish(World, [4, 0]);
        loop.Advance(2);
        CollectionAssert.AreEqual(new[] { $"message {World} 0400" }, Drain(loop.Client));
    }

    [TestMethod]
    public void SendReportsEveryStatusInItsOrder()
    {
        var network = Game();
        var client = GesNetworkConnection.CreateClient(network, default, 0, Plain);
        Assert.AreEqual(GesNetworkSendStatus.InvalidRoute, client.Send(Blob, [1]));
        Assert.AreEqual(GesNetworkSendStatus.InvalidRoute, client.Send(7, [1]));
        Assert.AreEqual(GesNetworkSendStatus.NotConnected, client.Send(Commands, [1]));

        var loop = Connected(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxSendQueueBytes = 100 });
        Assert.AreEqual(GesNetworkSendStatus.MessageTooLarge, loop.Client.Send(Commands, new byte[1000]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, loop.Server.Send(Blob, new byte[90]));
        Assert.AreEqual(GesNetworkSendStatus.QueueFull, loop.Server.Send(Blob, new byte[11]));
        Assert.AreEqual(GesNetworkSendStatus.InvalidRoute, loop.Server.Send(World, [1, 0]));
        Assert.AreEqual(GesNetworkSendStatus.NotSubscribed, loop.Server.Publish(World, [1, 0]));
        loop.Server.Subscribe(World);
        Assert.AreEqual(GesNetworkSendStatus.Queued, loop.Server.Publish(World, [1, 0]));
    }

    [TestMethod]
    public void HostCloseDropsQueuedMessagesAndReachesThePeer()
    {
        var loop = Connected();
        loop.Server.Send(Blob, [1, 2]);
        loop.Server.Close("bye");
        Assert.AreEqual(0, loop.Server.QueuedSendBytes);
        loop.Advance(1);
        CollectionAssert.AreEqual(new[] { "closed network.closed 'bye' local" }, Drain(loop.Server));
        CollectionAssert.AreEqual(new[] { "closed network.closed 'bye' peer" }, Drain(loop.Client));
        Assert.AreEqual(GesNetworkSendStatus.NotConnected, loop.Server.Send(Blob, [1]));
    }

    [TestMethod]
    public void KeepaliveHoldsAnIdleSessionAndSilenceTimesOut()
    {
        var loop = Connected();
        loop.Advance(30_000_000);
        Assert.AreEqual(GesNetworkState.Connected, loop.Client.State);
        Assert.AreEqual(GesNetworkState.Connected, loop.Server.State);

        var network = Game();
        var lone = GesNetworkConnection.CreateServer(network, 0, Plain);
        lone.Advance(9_999_999);
        Assert.AreEqual(GesNetworkState.AwaitHello, lone.State);
        lone.Advance(10_000_000);
        CollectionAssert.AreEqual(new[] { "closed network.timeout '' local" }, Drain(lone));
    }

    [TestMethod]
    public void TheInProcessPresetNeverTimesOut()
    {
        var options = GesNetworkOptions.InProcess();
        Assert.AreEqual(GesNetworkTransport.Message, options.Transport);
        Assert.AreEqual(65535, options.MaxPacketBytes);
        Assert.AreEqual((0L, 0L, 0L), (options.AckDelayMicroseconds, options.TimeoutMicroseconds, options.KeepaliveMicroseconds));
        var loop = Connected(options);
        Assert.AreEqual(long.MaxValue, loop.Client.NextDeadline);
        loop.Advance(3_600_000_000);
        Assert.AreEqual(GesNetworkState.Connected, loop.Client.State);
        Assert.AreEqual(GesNetworkState.Connected, loop.Server.State);
        Assert.AreEqual(0, loop.Client.Transmit(new byte[1200]));

        var lone = GesNetworkConnection.CreateServer(Game(), 0, options);
        lone.Advance(3_600_000_000);
        Assert.AreEqual(GesNetworkState.AwaitHello, lone.State);
    }

    [TestMethod]
    public void PresetsSetTheTransportAndOneSecurity()
    {
        var client = GesNetworkOptions.Datagram(GesNetworkClientSecurity.Development);
        Assert.AreEqual(GesNetworkTransport.Datagram, client.Transport);
        Assert.AreSame(GesNetworkClientSecurity.Development, client.ClientSecurity);
        Assert.IsNull(client.ServerSecurity);
        var server = GesNetworkOptions.Stream(GesNetworkServerSecurity.Key(TestKey));
        Assert.AreEqual(GesNetworkTransport.Stream, server.Transport);
        Assert.AreSame(TestKey, server.ServerSecurity!.ServerKey);
        Assert.IsNull(server.ClientSecurity);
        Assert.AreEqual(GesNetworkTransport.Message, GesNetworkOptions.Message(GesNetworkServerSecurity.None).Transport);
        Assert.ThrowsExactly<ArgumentNullException>(() => GesNetworkOptions.Message((GesNetworkClientSecurity)null!));
        Assert.ThrowsExactly<ArgumentException>(() => GesNetworkConnection.CreateServer(Game(), 0, client));
        Assert.ThrowsExactly<ArgumentException>(() => GesNetworkConnection.CreateClient(Game(), default, 0, server));
    }

    [TestMethod]
    public void RoundTripFollowsTheSimulatedLatency()
    {
        var (client, server) = Pair(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None });
        var simulator = new GesNetworkSimulator(client, server, 7, 20_000, 0);
        Handshake(simulator);
        server.Subscribe(World);
        for (var time = simulator.Now; time <= 2_000_000; time += 10_000)
        {
            client.Send(Commands, [1]);
            simulator.Advance(time);
            Drain(client);
            Drain(server);
        }

        Assert.IsTrue(client.RoundTripMicroseconds is >= 40_000 and <= 60_000, client.RoundTripMicroseconds.ToString());
    }

    [TestMethod]
    public void EqualSeedsGiveEqualTraces()
    {
        Assert.AreEqual(Trace(11), Trace(11));
        Assert.AreNotEqual(Trace(11), Trace(12));
    }

    [TestMethod]
    public void StreamTransportReassemblesPacketsFromSingleBytes()
    {
        var options = new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, Transport = GesNetworkTransport.Stream };
        var (client, server) = Pair(options);
        var buffer = new byte[70_000];
        Pump(client, server, buffer);
        CollectionAssert.AreEqual(new[] { "connecting " }, Drain(server));
        server.Accept();
        Pump(server, client, buffer);
        CollectionAssert.AreEqual(new[] { "connected" }, Drain(client));
        var payload = Bytes(4000, 5);
        server.Send(Blob, payload);
        Pump(server, client, buffer);
        CollectionAssert.AreEqual(new[] { $"message {Blob} {Convert.ToHexString(payload)}" }, Drain(client));
    }

    [TestMethod]
    public void StreamLengthZeroIsMalformedAndTooLongIsTooLarge()
    {
        var options = new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, Transport = GesNetworkTransport.Stream, MaxPacketBytes = 1200 };
        var (_, server) = Pair(options);
        server.Receive([0], 0);
        CollectionAssert.AreEqual(new[] { "closed network.malformed '' local" }, Drain(server));
        (_, server) = Pair(options);
        server.Receive([0xb1, 0x09], 0);
        CollectionAssert.AreEqual(new[] { "closed network.packetTooLarge '' local" }, Drain(server));
    }

    [TestMethod]
    public void MalformedPacketsCloseWithTheirCode()
    {
        var (_, server) = Pair(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None });
        server.Receive([9], 0);
        CollectionAssert.AreEqual(new[] { "closed network.malformed '' local" }, Drain(server));

        (_, server) = Pair(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None });
        server.Receive([1, (byte)'G', (byte)'E', (byte)'S', (byte)'X'], 0);
        CollectionAssert.AreEqual(new[] { "closed network.protocolMismatch '' local" }, Drain(server));

        (_, server) = Pair(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None });
        server.Receive([5, 0, 0, 0], 0);
        CollectionAssert.AreEqual(new[] { "closed network.malformed '' local" }, Drain(server));
    }

    [TestMethod]
    public void ClientRejectsAFrameOnItsOwnRoute()
    {
        var loop = Connected();
        // data asking for acknowledgement, seq 0, frame: channel 1 (client route), id 0, offset 0, total 1, chunk 1, byte.
        loop.Client.Receive([5, 2, 0, 0, 1, 0, 0, 0, 1, 1, 9], 0);
        CollectionAssert.AreEqual(new[] { "closed network.unexpectedRoute '' local" }, Drain(loop.Client));
    }

    [TestMethod]
    public void FrameRateLimitClosesTheConnection()
    {
        var loop = Connected(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxFramesPerSecond = 2 });
        for (var index = 0; index < 3; index++) loop.Client.Send(Commands, [(byte)index]);
        loop.Advance(1);
        CollectionAssert.AreEqual(new[] { "closed network.rateExceeded '' local" }, Drain(loop.Server)[2..]);
    }

    [TestMethod]
    public void SendBudgetDelaysFramesUntilTheBucketRefills()
    {
        var loop = Connected(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxPacketBytes = 1200 });
        loop.Server.SendBudgetBytesPerSecond = 12_000;
        loop.Server.Send(Blob, Bytes(5000, 0));
        loop.Advance(1);
        Assert.IsEmpty(Drain(loop.Client));
        loop.Advance(500_000);
        Assert.HasCount(1, Drain(loop.Client));
    }

    [TestMethod]
    [TestCategory("Allocation")]
    [DoNotParallelize]
    [DataRow(false)]
    [DataRow(true)]
    public void WarmedSessionAllocatesNothing(bool encrypted)
    {
        var loop = encrypted
            ? Connected(
                new GesNetworkOptions { MaxPacketBytes = 1200, ClientSecurity = GesNetworkClientSecurity.Development, InsecureTestKeySeed = 7 },
                new GesNetworkOptions { MaxPacketBytes = 1200, ServerSecurity = GesNetworkServerSecurity.Key(TestKey), InsecureTestKeySeed = 7 })
            : Connected(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxPacketBytes = 1200 });
        Assert.AreEqual(encrypted ? GesNetworkSecurity.Encrypted : GesNetworkSecurity.None, loop.Client.Security);
        loop.Server.Subscribe(World);
        var world = new byte[] { 1, 2 };
        var blob = Bytes(3000, 9);
        var command = new byte[] { 3 };
        long time = 0;
        long received = 0;
        void Round()
        {
            time += 16_667;
            // A changing state travels as a delta against an acknowledged one in the ring.
            world[0] = (byte)(time / 16_667);
            loop.Server.Publish(World, world);
            loop.Server.Send(Blob, blob);
            loop.Server.Send(BulkBlob, blob);
            loop.Client.Send(Commands, command);
            loop.Advance(time);
            for (var next = loop.Client.NextEvent(); next.Kind != GesNetworkEventKind.None; next = loop.Client.NextEvent()) received += next.Payload.Length;
            for (var next = loop.Server.NextEvent(); next.Kind != GesNetworkEventKind.None; next = loop.Server.NextEvent()) received += next.Payload.Length;
        }

        for (var round = 0; round < 50; round++) Round();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 1000; round++) Round();
        Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.AreEqual(1050L * (2 + 3000 + 3000 + 1), received);
        Assert.AreEqual(GesNetworkState.Connected, loop.Client.State);
    }

    // Runs the simulator until the server's host is asked and accepts the client.
    private static void Handshake(GesNetworkSimulator simulator)
    {
        simulator.Advance(simulator.Now);
        while (simulator.Server.State == GesNetworkState.AwaitHello) simulator.Advance(simulator.NextDeadline);
        CollectionAssert.AreEqual(new[] { "connecting " }, Drain(simulator.Server));
        simulator.Server.Accept();
    }

    private static (GesNetworkConnection Client, GesNetworkConnection Server) Pair(GesNetworkOptions options)
    {
        var network = Game();
        return (GesNetworkConnection.CreateClient(network, default, 0, options), GesNetworkConnection.CreateServer(network, 0, options));
    }

    // Moves every due packet from one stream connection to the other one byte at a time.
    private static void Pump(GesNetworkConnection from, GesNetworkConnection to, byte[] buffer)
    {
        int length;
        while ((length = from.Transmit(buffer)) > 0)
        {
            for (var index = 0; index < length; index++) to.Receive(buffer.AsSpan(index, 1), to.Now);
        }
    }

    private static string Trace(long seed)
    {
        var (client, server) = Pair(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxPacketBytes = 1200 });
        var simulator = new GesNetworkSimulator(client, server, seed, 5_000, 30_000);
        var trace = new StringBuilder();
        Handshake(simulator);
        server.Subscribe(World);
        for (var time = simulator.Now; time < 1_000_000; time += 16_667)
        {
            server.Publish(World, [(byte)(time / 16_667), 0]);
            if (time % 100_000 < 16_667) server.Send(Blob, Bytes(2500, (int)time));
            simulator.Advance(time);
            foreach (var entry in Drain(client)) trace.Append(simulator.Now).Append(' ').Append(entry[..Math.Min(entry.Length, 16)]).Append('\n');
            Drain(server);
        }

        return trace.Append(client.RoundTripMicroseconds).ToString();
    }

    // Every event of a connection with its transfer fields.
    private static List<string> Transfers(GesNetworkConnection connection)
    {
        var events = new List<string>();
        for (var next = connection.NextEvent(); next.Kind != GesNetworkEventKind.None; next = connection.NextEvent())
        {
            switch (next.Kind)
            {
                case GesNetworkEventKind.TransferProgress:
                    events.Add($"progress {next.Route} {next.TransferId} {next.Bytes}/{next.Total}");
                    break;
                case GesNetworkEventKind.TransferCancelled:
                    events.Add($"cancelled {next.Route} {next.TransferId} {next.Code} {(next.ByPeer ? "peer" : "local")}");
                    break;
                case GesNetworkEventKind.Message:
                    events.Add($"message {next.Route} {next.TransferId} {next.Payload.Length}");
                    break;
                default:
                    events.Add(next.Kind + " " + next.Code);
                    break;
            }
        }

        return events;
    }

    [TestMethod]
    public void TransferArrivesWithProgressOnBothSidesAndCompressesWhenShorter()
    {
        var loop = Connected(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxPacketBytes = 1200 });
        var data = new byte[5000];
        Assert.AreEqual(GesNetworkSendStatus.Queued, loop.Server.Send(BulkBlob, data));
        Assert.AreEqual(0, loop.Server.LastTransferId);
        // 1 compression byte, the 2-byte size, the hash and far fewer than 5000 deflated bytes.
        Assert.IsLessThan(200L, loop.Server.QueuedSendBytes);
        loop.Advance(1);
        var received = Transfers(loop.Client);
        Assert.AreEqual($"message {BulkBlob} 0 5000", received[^1]);
        StringAssert.StartsWith(received[0], $"progress {BulkBlob} 0 ");
        loop.Advance(20_000);
        var sent = Transfers(loop.Server);
        Assert.AreEqual(received[^2], sent.LastOrDefault());
    }

    [TestMethod]
    public void TransferWithoutCompressionCarriesTheData()
    {
        var loop = Connected(new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, DeflateTransfers = false });
        var data = Bytes(3000, 5);
        loop.Server.Send(BulkBlob, data);
        Assert.AreEqual(3000 + 1 + 2 + 32, loop.Server.QueuedSendBytes);
        loop.Advance(1);
        var next = loop.Client.NextEvent();
        while (next.Kind == GesNetworkEventKind.TransferProgress) next = loop.Client.NextEvent();
        Assert.AreEqual(GesNetworkEventKind.Message, next.Kind);
        CollectionAssert.AreEqual(data, next.Payload.ToArray());
    }

    [TestMethod]
    public void TransferAboveThePeerLimitIsTooLarge()
    {
        var network = Game();
        var client = GesNetworkConnection.CreateClient(network, default, 0, new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxTransferBytes = 100 });
        var server = GesNetworkConnection.CreateServer(network, 0, Plain);
        var loop = new GesNetworkSimulator(client, server, 0, 0, 0);
        loop.Advance(0);
        server.NextEvent();
        server.Accept();
        loop.Advance(0);
        Assert.AreEqual(GesNetworkSendStatus.MessageTooLarge, server.Send(BulkBlob, new byte[101]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Send(BulkBlob, new byte[100]));
    }

    [TestMethod]
    public void CancelledTransferReachesThePeerAndTheSessionContinues()
    {
        var loop = Connected();
        loop.Server.Send(BulkBlob, Bytes(4000, 1));
        var first = loop.Server.LastTransferId;
        loop.Server.Send(BulkBlob, Bytes(10, 2));
        Assert.IsTrue(loop.Server.Cancel(BulkBlob, first, "game.skipped"));
        Assert.IsFalse(loop.Server.Cancel(BulkBlob, first));
        CollectionAssert.AreEqual(new[] { $"cancelled {BulkBlob} 0 game.skipped local" }, Transfers(loop.Server));
        loop.Advance(1);
        var received = Transfers(loop.Client);
        Assert.AreEqual($"cancelled {BulkBlob} 0 game.skipped peer", received[0]);
        Assert.AreEqual($"message {BulkBlob} 1 10", received[^1]);
        Assert.AreEqual(GesNetworkState.Connected, loop.Client.State);
        Assert.ThrowsExactly<ArgumentException>(() => loop.Server.Cancel(Blob, 0));
        Assert.ThrowsExactly<ArgumentException>(() => loop.Server.Cancel(BulkBlob, 1, "bad code"));
    }

    [TestMethod]
    [DataRow(GesNetworkTransport.Message)]
    [DataRow(GesNetworkTransport.Stream)]
    [DataRow(GesNetworkTransport.Datagram)]
    public void TransferAboveThePendingReceiveLimitArrives(GesNetworkTransport transport)
    {
        const int Size = 5 * 1048576;
        const string arena = """
            wire schema :Arena as { data: :List(:Number integer between 0 and 255) max 6000000 }
            network :Game as { server sends :Arena bulk }
            """;
        var network = Game(arena);
        var options = new GesNetworkOptions
        {
            ClientSecurity = GesNetworkClientSecurity.None,
            ServerSecurity = GesNetworkServerSecurity.None,
            Transport = transport,
            DeflateTransfers = false,
            MaxPendingReceiveBytes = 1048576,
            MaxSendQueueBytes = 1048576,
            MaxFramesPerSecond = 1_000_000,
        };
        var client = GesNetworkConnection.CreateClient(network, default, 0, options);
        var server = GesNetworkConnection.CreateServer(network, 0, options);
        var simulator = new GesNetworkSimulator(client, server, 1, 0, 0);
        simulator.Advance(0);
        for (var step = 0; step < 100 && server.State != GesNetworkState.AwaitAccept; step++) simulator.Advance(simulator.NextDeadline);
        server.NextEvent();
        server.Accept();
        for (var step = 0; step < 100 && client.State != GesNetworkState.Connected; step++) simulator.Advance(simulator.NextDeadline);
        Assert.AreEqual(GesNetworkState.Connected, client.State);
        var data = Bytes(Size, 3);
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Send(0, data));
        byte[]? received = null;
        var closed = string.Empty;
        for (var step = 0; step < 20_000 && received is null && client.State == GesNetworkState.Connected; step++)
        {
            simulator.Advance(simulator.Now + 1000);
            for (var next = client.NextEvent(); next.Kind != GesNetworkEventKind.None; next = client.NextEvent())
            {
                if (next.Kind == GesNetworkEventKind.Message) received = next.Payload.ToArray();
                if (next.Kind == GesNetworkEventKind.Closed) closed = next.Code;
            }

            while (server.NextEvent().Kind != GesNetworkEventKind.None) { }
        }

        Assert.AreEqual(GesNetworkState.Connected, client.State, closed);
        CollectionAssert.AreEqual(data, received);
    }

    [TestMethod]
    public void UnsetLimitsGrowToTheLargestMessage()
    {
        const string source = """
            wire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000000 }
            network :Game as { server sends :Blob reliable }
            """;
        var network = Game(source);
        var client = GesNetworkConnection.CreateClient(network, "t"u8, 0, Plain);
        var server = GesNetworkConnection.CreateServer(network, 0, Plain);
        var loop = new GesNetworkSimulator(client, server, 0, 0, 0);
        loop.Advance(0);
        CollectionAssert.AreEqual(new[] { "connecting t" }, Drain(server));
        server.Accept();
        loop.Advance(0);
        Assert.AreEqual(GesNetworkState.Connected, client.State);

        // Above the defaults of 1 MiB queued and 4 MiB pending bytes: both limits take the 6000000-byte Blob.
        var blob = Bytes(5 * 1048576, 1);
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Send(0, blob));
        byte[]? received = null;
        for (var step = 0; step < 1000 && received is null && client.State == GesNetworkState.Connected; step++)
        {
            loop.Advance(loop.Now + 1000);
            for (var next = client.NextEvent(); next.Kind != GesNetworkEventKind.None; next = client.NextEvent())
            {
                if (next.Kind == GesNetworkEventKind.Message) received = next.Payload.ToArray();
            }
        }

        CollectionAssert.AreEqual(blob, received);
    }

    [TestMethod]
    public void ExplicitLimitsStayAsSet()
    {
        const string source = """
            wire schema :Arena as { data: :List(:Number integer between 0 and 255) max 6000000 }
            network :Game as { server sends :Arena bulk }
            """;
        var network = Game(source);
        var options = new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, MaxTransferBytes = 1000 };
        var client = GesNetworkConnection.CreateClient(network, "t"u8, 0, options);
        var server = GesNetworkConnection.CreateServer(network, 0, options);
        var loop = new GesNetworkSimulator(client, server, 0, 0, 0);
        loop.Advance(0);
        Drain(server);
        server.Accept();
        loop.Advance(0);
        Assert.AreEqual(GesNetworkState.Connected, server.State);
        Assert.AreEqual(GesNetworkSendStatus.MessageTooLarge, server.Send(0, new byte[1001]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Send(0, new byte[1000]));
    }
}
