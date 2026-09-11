// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
#if !UNITY_DLL_SMOKE
using GameEventScript.Conformance;
#endif
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime.Values;

namespace GameEventScript.PackageConsumer;

internal static class Program
{
    private static int Main(string[] arguments)
    {
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
        if (!host.Receive(GameEventScriptCSharpMessage.Create("Start", ("value", GesValue.GesText("41"))))) return Fail("The host rejected the input message.");
        var execution = host.RunToCompletion();
        if (execution.State != GameEventScriptExecutionState.Completed || result != 42) return Fail("The packaged compiler/runtime/bridge pipeline did not produce 42.");

#if !UNITY_DLL_SMOKE
        var document = ConformanceMarkdownParser.Parse(ConformanceMarkdown);
        if (document.SuiteId != "package.consumer" || document.Cases.Count != 1 || document.Cases[0].FullId != "package.consumer/signature-mismatch")
            return Fail("The packaged Conformance parser returned an unexpected model.");
#endif

        Console.WriteLine("C# distribution consumer smoke test passed.");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private const string ConformanceMarkdown = """
        ---
        formatVersion: 1
        suiteId: package.consumer
        kind: messageApi
        level: atomic
        ---

        ## Test: Signature mismatch

        ```yaml
        gesBlock: case
        id: signature-mismatch
        messageApi:
          signature:
            name: Start
            parameters: [a]
          message:
            name: Start
            args: []
        ```

        ```yaml
        gesBlock: expect
        message:
          name: Start
          signatureId: "Start(a)"
          messageSignatureId: "Start()"
          matches: false
          argumentCount: 0
        ```
        """;
}
