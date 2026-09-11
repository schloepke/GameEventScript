// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.RuntimeConsumer;

internal static class Program
{
    private static int Main(string[] arguments)
    {
        if (arguments.Length != 1) throw new ArgumentException("Supply the precompiled .gesb fixture path.");
        AssertRuntimeOnly();
        var bytes = File.ReadAllBytes(arguments[0]);
        var program = GameEventScriptProgramReader.Read(bytes);
        if (!bytes.AsSpan().SequenceEqual(GameEventScriptProgramWriter.ToArray(program))) throw new InvalidOperationException("The Program codec changed the fixture bytes.");
        var host = GameEventScriptHost.CreateBuilder().WithRandomSeed(1).Build();
        long? result = null;
        host.Subscribe("Done", ["result"], (message, _) => result = message.Arguments.GetAsInteger("result"));
        host.Load(program);
        if (!host.Receive(GameEventScriptCSharpMessage.Create("Start", ("value", GesValue.GesText("41"))))) throw new InvalidOperationException("The host rejected the input message.");
        if (host.RunToCompletion().State != GameEventScriptExecutionState.Completed || result != 42) throw new InvalidOperationException("The runtime/bridge pipeline did not produce 42.");
        AssertRuntimeOnly();
        Console.WriteLine("Runtime and CSharpBridge loaded and executed .gesb without a compiler.");
        return 0;
    }

    private static void AssertRuntimeOnly()
    {
        foreach (var forbidden in new[] { "StepH.GameEventScript.Compiler", "StepH.GameEventScript.Conformance" })
        {
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, forbidden + ".dll"))) throw new InvalidOperationException($"Runtime distribution unexpectedly includes {forbidden}.");
            if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == forbidden)) throw new InvalidOperationException($"Runtime process unexpectedly loaded {forbidden}.");
            var dependencies = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "StepH.GameEventScript.RuntimeConsumer.deps.json"));
            if (dependencies.Contains(forbidden, StringComparison.Ordinal)) throw new InvalidOperationException($"Runtime dependency manifest unexpectedly includes {forbidden}.");
        }
    }
}
