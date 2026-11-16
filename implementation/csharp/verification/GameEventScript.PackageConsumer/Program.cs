// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Syntax;
using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Generator;
using GameEventScript.Network;
#if GES_SOCKETS
using GameEventScript.Network.Sockets;
#endif
using GameEventScript.Runtime.Values;

namespace GameEventScript.PackageConsumer;

internal static class Program
{
    private static int Main(string[] arguments)
    {
        var highlights = new GesSyntaxHighlighter().Highlight("emit Done(42)");
        if (!highlights.IsComplete || !highlights.Spans.Any(span => span.Kind == GesSyntaxKind.Number)) return Fail("The packaged highlighter did not classify a number.");
        var compiled = new GesProgramBuilder()
            .AddScript("on Start(value) { emit Done(result: (parse value) + 1) }", "package-consumer.ges")
            .Compile();
        var encoded = compiled.Encode();
        if (arguments.Length == 1) File.WriteAllBytes(arguments[0], encoded);
        var loaded = GameEventScriptProgram.Read(encoded);
        var host = GameEventScriptHost.CreateBuilder().WithRandomSeed(1).Build();
        long? result = null;
        host.Subscribe("Done", ["result"], (message, _) => result = message.Arguments.GetAsInteger("result"));
        host.Load(loaded);
        if (host.Start().State != GesStartState.Ready) throw new InvalidOperationException("Host startup failed.");
        if (!host.Receive(GesBridgeMessage.Create("Start", ("value", GesValue.GesText("41"))))) return Fail("The host rejected the input message.");
        var execution = host.RunToCompletion();
        if (execution.State != GesExecutionState.Completed || result != 42) return Fail("The packaged compiler/runtime/bridge pipeline did not produce 42.");
        var generated = GesSchemaGenerator.Generate(GesSchemaSet.Parse("schema :Ping as {\n  ready: :Boolean\n}\n"), ["Ping"], new GesSchemaParameters(), new GesSchemaGenerateOptions { Namespace = "Consumer" });
        if (!generated.Contains("public partial struct Ping", StringComparison.Ordinal)) return Fail("The packaged generator did not write the schema type.");
        var network = GesSchemaSet.Parse("wire schema :Ping as { ready: :Boolean }\nnetwork :Game as {\n  server sends :Ping reliable\n}\n").Network("Game").Bind();
        var client = GesNetworkConnection.CreateClient(network, "t"u8, 0);
        var server = GesNetworkConnection.CreateServer(network, 0);
        var loopback = new GesNetworkLoopback(client, server);
        loopback.Advance(0);
        server.Accept();
        loopback.Advance(0);
        if (client.State != GesNetworkState.Connected) return Fail("The packaged network connection did not connect.");
#if GES_SOCKETS
        if (typeof(GesNetworkSocketListener).Assembly.GetName().Name != "GameEventScript.Network.Sockets") return Fail("The packaged socket transport did not load.");
#endif

        Console.WriteLine("C# distribution consumer smoke test passed.");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

}
