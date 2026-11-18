// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Network;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkDatagramTests
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

    private static readonly GesNetworkOptions Options = new() { MinimumSecurity = GesNetworkSecurity.None, Transport = GesNetworkTransport.Datagram };

    private static readonly GesNetworkOptions EncryptedClient = new() { Transport = GesNetworkTransport.Datagram, MinimumSecurity = GesNetworkSecurity.Encrypted, InsecureTestKeySeed = 7 };

    private static readonly GesNetworkOptions EncryptedServer = new()
    {
        Transport = GesNetworkTransport.Datagram,
        MinimumSecurity = GesNetworkSecurity.Encrypted,
        Key = GesNetworkKey.FromPrivateKey(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
        InsecureTestKeySeed = 7,
    };

    private static (GesNetworkConnection Client, GesNetworkConnection Server) Pair(bool encrypted = false)
    {
        var network = GesSchemaSet.Parse(Source).Network("Game").Bind();
        return (GesNetworkConnection.CreateClient(network, default, 0, encrypted ? EncryptedClient : Options),
            GesNetworkConnection.CreateServer(network, 0, encrypted ? EncryptedServer : Options));
    }

    private static GesNetworkSimulator Connected(long seed, int loss, int duplicate, bool reorder, bool encrypted = false)
    {
        var (client, server) = Pair(encrypted);
        var simulator = new GesNetworkSimulator(client, server, seed, 20_000, 15_000, loss, duplicate, reorder);
        simulator.Advance(0);
        for (var step = 0; step < 1000 && server.State == GesNetworkState.AwaitHello; step++) simulator.Advance(simulator.NextDeadline);
        Assert.AreEqual(GesNetworkState.AwaitAccept, server.State, $"server {server.State}, client {client.State}");
        Assert.AreEqual(GesNetworkEventKind.Connecting, server.NextEvent().Kind);
        server.Accept();
        for (var step = 0; step < 1000 && client.State == GesNetworkState.Hello; step++) simulator.Advance(simulator.NextDeadline);
        Assert.AreEqual(GesNetworkState.Connected, client.State, $"client {client.State} {Codes(client)}, server {server.State} {Codes(server)}");
        Assert.AreEqual(GesNetworkEventKind.Connected, client.NextEvent().Kind);
        return simulator;
    }

    private static string Codes(GesNetworkConnection connection)
    {
        var text = "";
        for (var next = connection.NextEvent(); next.Kind != GesNetworkEventKind.None; next = connection.NextEvent()) text += $"{next.Kind}:{next.Code} ";
        return text;
    }

    private static byte[] Bytes(int length, int seed)
    {
        var bytes = new byte[length];
        for (var index = 0; index < length; index++) bytes[index] = (byte)(index * 7 + seed);
        return bytes;
    }

    private static List<(int Route, byte[] Payload)> Messages(GesNetworkConnection connection)
    {
        var messages = new List<(int, byte[])>();
        for (var next = connection.NextEvent(); next.Kind != GesNetworkEventKind.None; next = connection.NextEvent())
        {
            Assert.AreNotEqual(GesNetworkEventKind.Closed, next.Kind, next.Code);
            if (next.Kind == GesNetworkEventKind.Message) messages.Add((next.Route, next.Payload.ToArray()));
        }

        return messages;
    }

    [TestMethod]
    public void HelloIsPaddedToTheMinimumDatagramAndRepeatedWithDoublingInterval()
    {
        var (client, _) = Pair();
        var buffer = new byte[2048];
        Assert.AreEqual(1200, client.Transmit(buffer));
        Assert.AreEqual(0, client.Transmit(buffer));
        Assert.AreEqual(1_000_000, client.NextDeadline);
        client.Advance(1_000_000);
        Assert.AreEqual(1200, client.Transmit(buffer));
        Assert.AreEqual(3_000_000, client.NextDeadline);
    }

    // 1024 packets of 40 small messages each carry far more than the 16384 recorded fragments; acknowledged packets
    // free theirs, so the sender keeps sending.
    [TestMethod]
    public void ManySmallMessagesKeepFlowingBeyondTheRecordedFragments()
    {
        var simulator = Connected(2, 0, 0, false);
        var payload = new byte[1];
        var received = 0;
        for (var round = 0; round < 1200; round++)
        {
            for (var index = 0; index < 40; index++)
            {
                payload[0] = (byte)(round * 40 + index);
                Assert.AreEqual(GesNetworkSendStatus.Queued, simulator.Server.Send(Blob, payload));
            }

            simulator.Advance(simulator.Now + 16_667);
            foreach (var (route, message) in Messages(simulator.Client))
            {
                Assert.AreEqual(Blob, route);
                Assert.AreEqual((byte)received++, message[0]);
            }
        }

        // At most the last rounds remain in flight.
        Assert.IsGreaterThanOrEqualTo(1190 * 40, received, $"received {received}");
    }

    [TestMethod]
    [DataRow(1, 0, 0, false)]
    [DataRow(5, 200, 0, false)]
    [DataRow(3, 0, 300, true)]
    [DataRow(4, 250, 150, true)]
    public void ReliableAndBulkArriveCompleteAndInOrderDespiteLossDuplicatesAndReordering(int seed, int loss, int duplicate, bool reorder)
    {
        var simulator = Connected(seed, loss, duplicate, reorder);
        var sent = new List<(int Route, byte[] Payload)>();
        var reliable = new List<(int, byte[])>();
        var bulk = new List<(int, byte[])>();
        var commands = 0;
        for (var round = 0; round < 40; round++)
        {
            var blob = Bytes(round * 97 % 3000 + 1, round);
            var pack = Bytes(round * 211 % 5000 + 1, round + 50);
            Assert.AreEqual(GesNetworkSendStatus.Queued, simulator.Server.Send(Blob, blob));
            Assert.AreEqual(GesNetworkSendStatus.Queued, simulator.Server.Send(BulkBlob, pack));
            Assert.AreEqual(GesNetworkSendStatus.Queued, simulator.Client.Send(Commands, [(byte)round]));
            sent.Add((Blob, blob));
            sent.Add((BulkBlob, pack));
            simulator.Advance(simulator.Now + 16_667);
            foreach (var message in Messages(simulator.Client)) (message.Route == Blob ? reliable : bulk).Add(message);
            foreach (var message in Messages(simulator.Server)) Assert.AreEqual(commands++, message.Payload[0]);
        }

        for (var step = 0; step < 2000 && (reliable.Count < 40 || bulk.Count < 40 || commands < 40); step++)
        {
            simulator.Advance(simulator.Now + 16_667);
            foreach (var message in Messages(simulator.Client)) (message.Route == Blob ? reliable : bulk).Add(message);
            foreach (var message in Messages(simulator.Server)) Assert.AreEqual(commands++, message.Payload[0]);
        }

        Assert.AreEqual(40, commands);
        var expectedReliable = sent.Where(message => message.Route == Blob).ToList();
        var expectedBulk = sent.Where(message => message.Route == BulkBlob).ToList();
        Assert.HasCount(40, reliable);
        Assert.HasCount(40, bulk);
        for (var index = 0; index < 40; index++)
        {
            CollectionAssert.AreEqual(expectedReliable[index].Payload, reliable[index].Item2);
            CollectionAssert.AreEqual(expectedBulk[index].Payload, bulk[index].Item2);
        }

        simulator.Advance(simulator.Now + 2_000_000);
        Assert.AreEqual(0, simulator.Server.QueuedSendBytes);
        Assert.AreEqual(0, simulator.Client.QueuedSendBytes);
        Assert.AreEqual(GesNetworkState.Connected, simulator.Client.State);
        Assert.AreEqual(GesNetworkState.Connected, simulator.Server.State);
    }

    [TestMethod]
    public void LatestDeliversIncreasingStatesAndEndsWithTheNewestDespiteLoss()
    {
        var simulator = Connected(9, 300, 100, true);
        simulator.Server.Subscribe(World);
        var last = -1;
        for (var tick = 0; tick < 200; tick++)
        {
            simulator.Server.Publish(World, [(byte)tick, (byte)(tick >> 8)]);
            simulator.Advance(simulator.Now + 16_667);
            foreach (var message in Messages(simulator.Client))
            {
                var value = message.Payload[0] | (message.Payload[1] << 8);
                Assert.IsGreaterThan(last, value);
                last = value;
            }
        }

        simulator.Advance(simulator.Now + 3_000_000);
        foreach (var message in Messages(simulator.Client)) last = message.Payload[0] | (message.Payload[1] << 8);
        Assert.AreEqual(199, last);
    }

    [TestMethod]
    public void RoundTripFollowsTheSimulatedLatency()
    {
        var simulator = Connected(5, 100, 0, true);
        for (var round = 0; round < 200; round++)
        {
            simulator.Client.Send(Commands, [1]);
            simulator.Advance(simulator.Now + 10_000);
            Messages(simulator.Server);
        }

        Assert.IsTrue(simulator.Client.RoundTripMicroseconds is >= 40_000 and <= 80_000, simulator.Client.RoundTripMicroseconds.ToString());
    }

    [TestMethod]
    public void EqualSeedsGiveEqualTraces()
    {
        Assert.AreEqual(Trace(21), Trace(21));
        Assert.AreNotEqual(Trace(21), Trace(22));
    }

    private static string Trace(long seed)
    {
        var simulator = Connected(seed, 200, 100, true);
        var trace = new System.Text.StringBuilder();
        for (var round = 0; round < 60; round++)
        {
            simulator.Server.Send(Blob, Bytes(1500, round));
            simulator.Advance(simulator.Now + 16_667);
            foreach (var message in Messages(simulator.Client)) trace.Append(simulator.Now).Append(' ').Append(message.Payload[0]).Append('\n');
        }

        return trace.ToString();
    }

    [TestMethod]
    public void SilenceAfterLossTimesOut()
    {
        var simulator = Connected(1, 0, 0, false);
        var (client, server) = (simulator.Client, simulator.Server);
        var buffer = new byte[2048];
        client.Send(Commands, [1]);
        while (client.Transmit(buffer) > 0)
        {
        }

        client.Advance(client.Now + 10_000_000);
        while (client.Transmit(buffer) > 0)
        {
        }

        Assert.AreEqual(GesNetworkState.Closed, client.State);
        Assert.AreEqual(GesNetworkState.Connected, server.State);
    }

    [TestMethod]
    [TestCategory("Allocation")]
    [DoNotParallelize]
    [DataRow(false)]
    [DataRow(true)]
    public void WarmedDatagramSessionAllocatesNothing(bool encrypted)
    {
        var simulator = Connected(3, 100, 50, true, encrypted);
        Assert.AreEqual(encrypted ? GesNetworkSecurity.Encrypted : GesNetworkSecurity.None, simulator.Client.Security);
        simulator.Server.Subscribe(World);
        var world = new byte[] { 1, 2 };
        var blob = Bytes(1000, 9);
        var command = new byte[] { 3 };
        void Round()
        {
            world[0]++;
            simulator.Server.Publish(World, world);
            simulator.Server.Send(Blob, blob);
            simulator.Server.Send(BulkBlob, blob);
            simulator.Client.Send(Commands, command);
            simulator.Advance(simulator.Now + 16_667);
            for (var next = simulator.Client.NextEvent(); next.Kind != GesNetworkEventKind.None; next = simulator.Client.NextEvent())
            {
            }

            for (var next = simulator.Server.NextEvent(); next.Kind != GesNetworkEventKind.None; next = simulator.Server.NextEvent())
            {
            }
        }

        // Random loss reaches rare new peaks of held messages, and the buffers grow to each. With this seed the last
        // growth before the measured rounds happens in round 2467 and the next one after them.
        for (var round = 0; round < 2500; round++) Round();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 2500; round++) Round();
        Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.AreEqual(GesNetworkState.Connected, simulator.Client.State);
    }

    [TestMethod]
    [DataRow(11, 0, 0, false)]
    [DataRow(12, 250, 150, true)]
    public void CancelledTransfersAreSkippedAndLaterOnesArriveDespiteLoss(int seed, int loss, int duplicate, bool reorder)
    {
        var simulator = Connected(seed, loss, duplicate, reorder);
        var kept = new List<byte[]>();
        for (var index = 0; index < 20; index++)
        {
            var data = Bytes(index * 331 % 5000 + 1, index);
            Assert.AreEqual(GesNetworkSendStatus.Queued, simulator.Server.Send(BulkBlob, data));
            if (index % 3 == 1) Assert.IsTrue(simulator.Server.Cancel(BulkBlob, simulator.Server.LastTransferId));
            else kept.Add(data);
        }

        var received = new List<byte[]>();
        var cancelled = new List<int>();
        for (var step = 0; step < 4000 && (received.Count < kept.Count || cancelled.Count < 7); step++)
        {
            simulator.Advance(simulator.Now + 16_667);
            for (var next = simulator.Client.NextEvent(); next.Kind != GesNetworkEventKind.None; next = simulator.Client.NextEvent())
            {
                Assert.AreNotEqual(GesNetworkEventKind.Closed, next.Kind, next.Code);
                if (next.Kind == GesNetworkEventKind.Message) received.Add(next.Payload.ToArray());
                if (next.Kind == GesNetworkEventKind.TransferCancelled)
                {
                    Assert.IsTrue(next.ByPeer);
                    cancelled.Add(next.TransferId);
                }
            }

            Messages(simulator.Server);
        }

        CollectionAssert.AreEqual(new[] { 1, 4, 7, 10, 13, 16, 19 }, cancelled.Order().ToArray());
        Assert.HasCount(kept.Count, received);
        for (var index = 0; index < kept.Count; index++) CollectionAssert.AreEqual(kept[index], received[index]);
        simulator.Advance(simulator.Now + 2_000_000);
        Assert.AreEqual(0, simulator.Server.QueuedSendBytes);
    }
}
