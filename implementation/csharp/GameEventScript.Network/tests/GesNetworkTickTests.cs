// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Network;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkTickTests
{
    private const string Source = """
        wire schema :Alpha as { v: :Number integer between 0 and 255 }
        wire schema :Beta as { v: :Number integer between 0 and 255 }
        wire schema :Log as { v: :Number integer between 0 and 255 }
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends :Commands reliable
          server publishes :Alpha latest
          server publishes :Beta latest
          server publishes :Log reliable
        }
        """;

    private const int Commands = 0;
    private const int Alpha = 1;
    private const int Beta = 2;
    private const int Log = 3;

    private static GesNetworkOptions Options(GesNetworkTransport transport, int maxFramesPerSecond = 4096) =>
        new() { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, Transport = transport, MaxFramesPerSecond = maxFramesPerSecond };

    private static GesNetworkSimulator Connected(GesNetworkTransport transport, int lossPerMille = 0, long seed = 0, int maxFramesPerSecond = 4096)
    {
        var network = GesSchemaSet.Parse(Source).Network("Game").Bind();
        var client = GesNetworkConnection.CreateClient(network, default, 0, Options(transport));
        var server = GesNetworkConnection.CreateServer(network, 0, Options(transport, maxFramesPerSecond));
        var simulator = new GesNetworkSimulator(client, server, seed, lossPerMille == 0 ? 0 : 5_000, lossPerMille == 0 ? 0 : 20_000, lossPerMille, 0, lossPerMille != 0);
        simulator.Advance(0);
        for (var step = 0; step < 1000 && server.State == GesNetworkState.AwaitHello; step++) simulator.Advance(simulator.NextDeadline);
        server.Accept();
        for (var step = 0; step < 1000 && client.State == GesNetworkState.Hello; step++) simulator.Advance(simulator.NextDeadline);
        Assert.AreEqual(GesNetworkState.Connected, client.State, $"seed {seed} now {simulator.Now} " + string.Join(", ", Drain(client)) + " / " + string.Join(", ", Drain(server)));
        Drain(client);
        Drain(server);
        server.Subscribe(Alpha);
        server.Subscribe(Beta);
        server.Subscribe(Log);
        return simulator;
    }

    private static List<string> Drain(GesNetworkConnection connection)
    {
        var events = new List<string>();
        for (var next = connection.NextEvent(); next.Kind != GesNetworkEventKind.None; next = connection.NextEvent())
        {
            if (next.Kind == GesNetworkEventKind.Message) events.Add($"message {next.Route} {Convert.ToHexString(next.Payload)}");
            else if (next.Kind == GesNetworkEventKind.Tick) events.Add($"tick {next.TickNumber}");
            else events.Add(next.Kind + " " + next.Code);
        }

        return events;
    }

    private static void Settle(GesNetworkSimulator simulator)
    {
        for (var step = 0; step < 50; step++) simulator.Advance(simulator.Now + 10_000);
    }

    [TestMethod]
    [DataRow(GesNetworkTransport.Message)]
    [DataRow(GesNetworkTransport.Stream)]
    public void PacketsAboveTheFrameLimitWaitForTheFrameBucket(GesNetworkTransport transport)
    {
        var simulator = Connected(transport, maxFramesPerSecond: 4);
        var client = simulator.Client;
        var server = simulator.Server;
        var start = simulator.Now;
        var expected = new List<string>();
        for (var x = 0; x < 12; x++)
        {
            Assert.AreEqual(GesNetworkSendStatus.Queued, client.Send(Commands, [(byte)x]));
            expected.Add($"message {Commands} {x:X2}");
            simulator.Advance(simulator.Now);
        }

        // The server took frames until its bucket ran dry; the simulator holds the rest instead of closing it.
        Assert.IsFalse(server.CanReceive);
        Assert.IsGreaterThan(simulator.Now, server.NextDeadline);
        Assert.IsGreaterThan(0, simulator.PacketsInFlight);
        var received = Drain(server);
        Assert.IsLessThan(expected.Count, received.Count);
        for (var step = 0; step < 1000 && received.Count < expected.Count; step++)
        {
            simulator.Advance(simulator.NextDeadline);
            received.AddRange(Drain(server));
        }

        CollectionAssert.AreEqual(expected, received);
        Assert.AreEqual(GesNetworkState.Connected, server.State);
        Assert.AreEqual(GesNetworkState.Connected, client.State);
        Assert.IsGreaterThanOrEqualTo(start + 1_000_000, simulator.Now);
    }

    [TestMethod]
    [DataRow(GesNetworkTransport.Message)]
    [DataRow(GesNetworkTransport.Stream)]
    [DataRow(GesNetworkTransport.Datagram)]
    public void TickReportsItsChangedMembersAndThenTheTick(GesNetworkTransport transport)
    {
        var simulator = Connected(transport);
        var server = simulator.Server;
        server.BeginTick();
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Publish(Alpha, [1]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Publish(Beta, [2]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Publish(Log, [3]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.EndTick());
        Settle(simulator);
        // The reliable topic is no member and travels at once.
        CollectionAssert.AreEqual(new[] { $"message {Log} 03", $"message {Alpha} 01", $"message {Beta} 02", "tick 0" }, Drain(simulator.Client));

        server.BeginTick();
        server.Publish(Alpha, [4]);
        server.Include(Beta);
        server.EndTick();
        Settle(simulator);
        CollectionAssert.AreEqual(new[] { $"message {Alpha} 04", "tick 1" }, Drain(simulator.Client));

        // Only an included member: the tick names the state the client holds.
        server.BeginTick();
        server.Include(Alpha);
        server.EndTick();
        Settle(simulator);
        CollectionAssert.AreEqual(new[] { "tick 2" }, Drain(simulator.Client));

        // A state published alone is reported on arrival; an empty tick sends nothing.
        server.Publish(Beta, [5]);
        server.BeginTick();
        server.EndTick();
        Settle(simulator);
        CollectionAssert.AreEqual(new[] { $"message {Beta} 05" }, Drain(simulator.Client));
    }

    [TestMethod]
    [DataRow(GesNetworkTransport.Message)]
    [DataRow(GesNetworkTransport.Datagram)]
    public void ClosedTicksWaitAndANewerOneReplacesTheWaitingOne(GesNetworkTransport transport)
    {
        var simulator = Connected(transport);
        var server = simulator.Server;
        for (var value = 1; value <= 3; value++)
        {
            server.BeginTick();
            server.Publish(Alpha, [(byte)value]);
            server.Publish(Beta, [(byte)(value + 16)]);
            server.EndTick();
        }

        Settle(simulator);
        // The first tick is in transmission, the second waits and the third replaces it. The second tick's states become
        // latest states, which replace the first tick's states that have not started yet.
        var events = Drain(simulator.Client);
        CollectionAssert.AreEqual(
            new[] { $"message {Alpha} 02", $"message {Beta} 12", "tick 0", $"message {Alpha} 03", $"message {Beta} 13", "tick 1" },
            events,
            string.Join(" | ", events));
    }

    [TestMethod]
    public void TicksStayConsistentUnderLossAndReordering()
    {
        // Seeds whose handshake survives the loss.
        foreach (var seed in new[] { 1, 3, 4, 5, 6, 7, 8, 10, 11, 12 })
        {
            var simulator = Connected(GesNetworkTransport.Datagram, 200, seed);
            var server = simulator.Server;
            var client = simulator.Client;
            int alpha = -1, beta = -1, lastTick = -1, ticks = 0;
            for (var round = 0; round < 200; round++)
            {
                server.BeginTick();
                server.Publish(Alpha, [(byte)round]);
                // Beta changes every third round and is included otherwise.
                if (round % 3 == 0) server.Publish(Beta, [(byte)round]);
                else server.Include(Beta);
                server.EndTick();
                simulator.Advance(simulator.Now + 16_667);
                foreach (var entry in Drain(client))
                {
                    var parts = entry.Split(' ');
                    if (parts[0] == "message")
                    {
                        var value = Convert.ToByte(parts[2], 16);
                        if (parts[1] == $"{Alpha}") alpha = value;
                        else beta = value;
                        continue;
                    }

                    Assert.AreEqual("tick", parts[0], entry);
                    var number = int.Parse(parts[1]);
                    Assert.IsGreaterThan(lastTick, number, $"seed {seed}");
                    lastTick = number;
                    ticks++;
                    // A complete tick shows Alpha and Beta of one round.
                    Assert.AreEqual(alpha - alpha % 3, beta, $"seed {seed} tick {number}");
                }
            }

            for (var step = 0; step < 200; step++) simulator.Advance(simulator.Now + 16_667);
            foreach (var entry in Drain(client))
            {
                var parts = entry.Split(' ');
                if (parts[0] == "message") (parts[1] == $"{Alpha}" ? ref alpha : ref beta) = Convert.ToByte(parts[2], 16);
                else lastTick = int.Parse(parts[1]);
            }

            Assert.AreEqual(GesNetworkState.Connected, client.State);
            Assert.AreEqual(199, alpha, $"seed {seed}");
            Assert.AreEqual(198, beta, $"seed {seed}");
            Assert.IsGreaterThan(20, ticks, $"seed {seed}");
        }
    }

    [TestMethod]
    public void TickCallsCheckTheirOrderAndRoutes()
    {
        var simulator = Connected(GesNetworkTransport.Message);
        var server = simulator.Server;
        Assert.ThrowsExactly<InvalidOperationException>(() => server.Include(Alpha));
        Assert.ThrowsExactly<InvalidOperationException>(() => server.EndTick());
        server.BeginTick();
        Assert.ThrowsExactly<InvalidOperationException>(server.BeginTick);
        Assert.ThrowsExactly<ArgumentException>(() => server.Include(Log));
        Assert.ThrowsExactly<ArgumentException>(() => server.Include(Commands));
        Assert.ThrowsExactly<InvalidOperationException>(() => server.Include(Alpha));
        server.Publish(Alpha, [1]);
        server.Include(Alpha);
        server.EndTick();

        // A topic the client left is no member.
        server.Unsubscribe(Beta);
        server.BeginTick();
        server.Publish(Alpha, [2]);
        Assert.AreEqual(GesNetworkSendStatus.NotSubscribed, server.Publish(Beta, [2]));
        server.EndTick();
        Settle(simulator);
        CollectionAssert.AreEqual(new[] { $"message {Alpha} 01", "tick 0", $"message {Alpha} 02", "tick 1" }, Drain(simulator.Client));

        server.Close("bye");
        server.BeginTick();
        Assert.AreEqual(GesNetworkSendStatus.NotConnected, server.EndTick());
    }
}
