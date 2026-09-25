// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.SyntaxHighlighter;
using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime.Values;

namespace GameEventScript.PackageConsumer;

internal static class Program
{
    private static int Main(string[] arguments)
    {
        var highlights = new GameEventScriptSyntaxHighlighter().Highlight("emit Done(42)");
        if (!highlights.IsComplete || !highlights.Spans.Any(span => span.Kind == GameEventScriptSyntaxKind.Number)) return Fail("The packaged highlighter did not classify a number.");
        var compiled = GameEventScriptBuilder.Create()
            .AddScript("on Start(value) { emit Done(result: (parse value) + 1) }", "package-consumer.ges")
            .Compile();
        var encoded = GameEventScriptProgramWriter.ToArray(compiled);
        if (arguments.Length == 1) File.WriteAllBytes(arguments[0], encoded);
        var loaded = GameEventScriptProgramReader.Read(encoded);
        var host = GameEventScriptHost.CreateBuilder().WithRandomSeed(1).Build();
        long? result = null;
        host.Subscribe("Done", ["result"], (message, _) => result = message.Arguments.GetAsInteger("result"));
        host.Load(loaded);
        if (host.Start().State != GameEventScriptStartState.Ready) throw new InvalidOperationException("Host startup failed.");
        if (!host.Receive(GameEventScriptCSharpMessage.Create("Start", ("value", GesValue.GesText("41"))))) return Fail("The host rejected the input message.");
        var execution = host.RunToCompletion();
        if (execution.State != GameEventScriptExecutionState.Completed || result != 42) return Fail("The packaged compiler/runtime/bridge pipeline did not produce 42.");

        Console.WriteLine("C# distribution consumer smoke test passed.");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

}
