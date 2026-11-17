// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.Network;

// Runs scripted steps over a client and a server connection and records their events and packets as trace lines.
internal static class NetworkApiObservations
{
    private static readonly string[] Steps = ["advance", "accept", "refuse", "close", "send", "publish", "subscribe", "unsubscribe", "receive", "transmit", "transportClosed", "budget", "deliver", "cookie"];

    internal static void Validate(JsonElement input)
    {
        RuntimeValues.Fields(input, "sources", "clientSources", "network", "options", "clientOptions", "token", "link", "steps");
        _ = Options(input.GetProperty("options"));
        _ = Options(input.GetProperty("clientOptions"));
        _ = Convert.FromHexString(input.GetProperty("token").GetString()!);
        foreach (var step in input.GetProperty("steps").EnumerateArray())
            if (!Steps.Contains(step.GetProperty("action").GetString(), StringComparer.Ordinal)) throw new ArgumentException("Unknown network step.");
    }

    internal static object Run(JsonElement input)
    {
        GesNetworkConnection client;
        GesNetworkConnection server;
        try
        {
            var serverNetwork = Bind(input.GetProperty("sources"), input.GetProperty("network").GetString()!);
            var clientNetwork = Bind(input.GetProperty("clientSources"), input.GetProperty("network").GetString()!);
            client = GesNetworkConnection.CreateClient(clientNetwork, Convert.FromHexString(input.GetProperty("token").GetString()!), 0, Options(input.GetProperty("clientOptions")));
            server = GesNetworkConnection.CreateServer(serverNetwork, 0, Options(input.GetProperty("options")));
        }
        catch (GesSchemaException exception)
        {
            return new { status = "ok", network = new { error = new { code = exception.Code } } };
        }
        catch (ArgumentException)
        {
            return new { status = "ok", network = new { error = new { code = "invalidArgument" } } };
        }

        var link = input.GetProperty("link");
        GesNetworkSimulator? simulator = link.ValueKind switch
        {
            JsonValueKind.Object => Simulator(client, server, link),
            _ when link.GetString() == "loopback" => new GesNetworkSimulator(client, server, 0, 0, 0),
            _ => null
        };
        if (simulator is null && link.ValueKind == JsonValueKind.Object) return new { status = "ok", network = new { error = new { code = "invalidArgument" } } };
        var trace = new List<string>();
        var sent = new Dictionary<GesNetworkConnection, List<byte[]>> { [client] = [], [server] = [] };
        var now = 0L;
        foreach (var step in input.GetProperty("steps").EnumerateArray())
        {
            var action = step.GetProperty("action").GetString()!;
            var connection = step.TryGetProperty("side", out var side) && side.GetString() == "client" ? client : server;
            var name = action == "cookie" ? "cookie" : connection == client ? "client" : "server";
            try
            {
                switch (action)
                {
                    case "advance":
                        now = step.GetProperty("now").GetInt64();
                        if (simulator is not null) simulator.Advance(now);
                        else
                        {
                            client.Advance(now);
                            server.Advance(now);
                        }
                        break;
                    case "accept":
                        server.Accept();
                        break;
                    case "refuse":
                        server.Refuse(step.GetProperty("detail").GetString()!);
                        break;
                    case "close":
                        connection.Close(step.GetProperty("detail").GetString()!);
                        break;
                    case "send":
                        var status = connection.Send(step.GetProperty("route").GetInt32(), Payload(step.GetProperty("payload")));
                        trace.Add($"{name} send {SendStatus(status)}");
                        break;
                    case "publish":
                        var published = connection.Publish(step.GetProperty("route").GetInt32(), Payload(step.GetProperty("payload")));
                        trace.Add($"{name} publish {SendStatus(published)}");
                        break;
                    case "subscribe":
                        connection.Subscribe(step.GetProperty("route").GetInt32());
                        break;
                    case "unsubscribe":
                        connection.Unsubscribe(step.GetProperty("route").GetInt32());
                        break;
                    case "receive":
                        connection.Receive(Convert.FromHexString(step.GetProperty("bytes").GetString()!), now);
                        break;
                    case "transmit":
                        var buffer = new byte[connection.MaxPacketBytes + 5];
                        int length;
                        sent[connection].Clear();
                        while ((length = connection.Transmit(buffer)) > 0)
                        {
                            sent[connection].Add(buffer[..length]);
                            trace.Add($"{name} packet {Packet(buffer.AsSpan(0, length))}");
                        }

                        break;
                    case "deliver":
                        var from = sent[connection == client ? server : client];
                        foreach (var index in step.GetProperty("packets").EnumerateArray())
                        {
                            var position = index.GetInt32();
                            if (position >= from.Count) throw new ArgumentException("No such packet.");
                            connection.Receive(from[position], now);
                        }

                        break;
                    case "cookie":
                        trace.Add($"cookie {Cookie(step, sent[client])}");
                        break;
                    case "transportClosed":
                        connection.TransportClosed();
                        break;
                    case "budget":
                        connection.SendBudgetBytesPerSecond = step.GetProperty("bytesPerSecond").GetInt64();
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                trace.Add($"{name} error invalidOperation");
            }
            catch (ArgumentException)
            {
                trace.Add($"{name} error invalidArgument");
            }

            Drain(client, "client", trace);
            Drain(server, "server", trace);
        }

        return new { status = "ok", network = new { trace, client = Describe(client), server = Describe(server) } };
    }

    private static GesNetworkSimulator? Simulator(GesNetworkConnection client, GesNetworkConnection server, JsonElement link)
    {
        try
        {
            return new GesNetworkSimulator(
                client,
                server,
                link.GetProperty("seed").GetInt64(),
                link.GetProperty("latencyMicroseconds").GetInt64(),
                link.GetProperty("jitterMicroseconds").GetInt64(),
                link.TryGetProperty("lossPerMille", out var loss) ? loss.GetInt32() : 0,
                link.TryGetProperty("duplicatePerMille", out var duplicate) ? duplicate.GetInt32() : 0,
                link.TryGetProperty("reorder", out var reorder) && reorder.GetBoolean());
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // Checks a datagram at a fresh gate and names the result, with the retry's bytes.
    private static string Cookie(JsonElement step, List<byte[]> sent)
    {
        var packet = step.TryGetProperty("bytes", out var bytes)
            ? Convert.FromHexString(bytes.GetString()!)
            : step.GetProperty("packet").GetInt32() < sent.Count ? sent[step.GetProperty("packet").GetInt32()] : throw new ArgumentException("No such packet.");
        using var cookies = new GesNetworkCookies(Convert.FromHexString(step.GetProperty("key").GetString()!), step.GetProperty("windowMicroseconds").GetInt64());
        var retry = new byte[GesNetworkCookies.RetryBytes];
        return cookies.Check(packet, Convert.FromHexString(step.GetProperty("address").GetString()!), step.GetProperty("now").GetInt64(), retry) switch
        {
            GesNetworkCookieResult.Accept => "accept",
            GesNetworkCookieResult.Retry => "retry " + Convert.ToHexString(retry).ToLowerInvariant(),
            _ => "drop"
        };
    }

    // A packet up to 512 bytes as hexadecimal, a longer one as its length and FNV-1a 64 hash.
    private static string Packet(ReadOnlySpan<byte> bytes) => bytes.Length <= 512 ? Convert.ToHexString(bytes).ToLowerInvariant() : Bytes(bytes);

    private static GesBoundNetwork Bind(JsonElement sources, string network)
    {
        var parsed = sources.EnumerateArray().Select((text, index) => new GesSchemaSource($"schema{index}.gess", text.GetString()!)).ToArray();
        return GesSchemaSet.Parse(parsed).Network(network).Bind();
    }

    private static GesNetworkOptions Options(JsonElement raw)
    {
        var options = new GesNetworkOptions();
        foreach (var property in raw.EnumerateObject())
        {
            switch (property.Name)
            {
                case "transport":
                    options.Transport = property.Value.GetString() switch
                    {
                        "stream" => GesNetworkTransport.Stream,
                        "datagram" => GesNetworkTransport.Datagram,
                        _ => GesNetworkTransport.Message
                    };
                    break;
                case "maxPacketBytes": options.MaxPacketBytes = checked((int)property.Value.GetInt64()); break;
                case "maxBaselineBytes": options.MaxBaselineBytes = property.Value.GetInt64(); break;
                case "maxPendingReceiveBytes": options.MaxPendingReceiveBytes = property.Value.GetInt64(); break;
                case "maxFramesPerSecond": options.MaxFramesPerSecond = checked((int)property.Value.GetInt64()); break;
                case "maxSendQueueBytes": options.MaxSendQueueBytes = property.Value.GetInt64(); break;
                case "timeoutMicroseconds": options.TimeoutMicroseconds = property.Value.GetInt64(); break;
                case "keepaliveMicroseconds": options.KeepaliveMicroseconds = property.Value.GetInt64(); break;
                case "ackDelayMicroseconds": options.AckDelayMicroseconds = property.Value.GetInt64(); break;
                case "sendBudgetBytesPerSecond": options.SendBudgetBytesPerSecond = property.Value.GetInt64(); break;
                default: throw new ArgumentException("Unknown network option.");
            }
        }

        return options;
    }

    private static byte[] Payload(JsonElement raw)
    {
        if (raw.TryGetProperty("hex", out var hex)) return Convert.FromHexString(hex.GetString()!);
        var bytes = new byte[raw.GetProperty("length").GetInt32()];
        var seed = raw.GetProperty("seed").GetInt32();
        for (var index = 0; index < bytes.Length; index++) bytes[index] = (byte)((index + seed) % 251);
        return bytes;
    }

    private static string SendStatus(GesNetworkSendStatus status) => status switch
    {
        GesNetworkSendStatus.Queued => "queued",
        GesNetworkSendStatus.NotConnected => "notConnected",
        GesNetworkSendStatus.QueueFull => "queueFull",
        GesNetworkSendStatus.MessageTooLarge => "messageTooLarge",
        GesNetworkSendStatus.NotSubscribed => "notSubscribed",
        _ => "invalidRoute"
    };

    private static void Drain(GesNetworkConnection connection, string name, List<string> trace)
    {
        while (true)
        {
            var next = connection.NextEvent();
            switch (next.Kind)
            {
                case GesNetworkEventKind.None:
                    return;
                case GesNetworkEventKind.Connecting:
                    trace.Add($"{name} connecting {Bytes(next.Payload)}");
                    break;
                case GesNetworkEventKind.Connected:
                    trace.Add($"{name} connected");
                    break;
                case GesNetworkEventKind.Message:
                    trace.Add($"{name} message {next.Route} {Bytes(next.Payload)}");
                    break;
                case GesNetworkEventKind.Closed:
                    trace.Add($"{name} closed {next.Code} {(next.ByPeer ? "peer" : "local")} \"{next.Detail}\"");
                    break;
            }
        }
    }

    // Up to 32 bytes as hexadecimal, longer payloads as their length and FNV-1a 64 hash.
    private static string Bytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length <= 32) return bytes.Length == 0 ? "-" : Convert.ToHexString(bytes).ToLowerInvariant();
        var hash = 0xcbf29ce484222325UL;
        foreach (var value in bytes) hash = (hash ^ value) * 0x100000001b3UL;
        return $"{bytes.Length}:{hash:x16}";
    }

    private static object Describe(GesNetworkConnection connection) => new
    {
        state = connection.State switch
        {
            GesNetworkState.Hello => "hello",
            GesNetworkState.AwaitHello => "awaitHello",
            GesNetworkState.AwaitAccept => "awaitAccept",
            GesNetworkState.Connected => "connected",
            _ => "closed"
        },
        roundTripMicroseconds = connection.RoundTripMicroseconds,
        queuedSendBytes = connection.QueuedSendBytes,
        pendingReceiveBytes = connection.PendingReceiveBytes
    };
}
