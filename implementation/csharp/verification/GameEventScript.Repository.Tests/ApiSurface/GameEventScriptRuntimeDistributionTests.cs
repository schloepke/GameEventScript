// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.ApiSurface;

/// <summary>Verifies execution in a separate application whose complete dependency set excludes the source compiler.</summary>
[TestClass]
public sealed class GameEventScriptRuntimeDistributionTests
{
    /// <summary>Loads a precompiled Program and exercises the runtime literal parser and C# message adapters without a compiler assembly.</summary>
    /// <param name="debugInfo">The metadata included in the precompiled fixture.</param>
    [TestMethod]
    [DataRow(GameEventScriptDebugInfoOptions.None)]
    [DataRow(GameEventScriptDebugInfoOptions.All)]
    public void PrecompiledProgramRunsWithRuntimeAndBridgeOnly(GameEventScriptDebugInfoOptions debugInfo)
    {
        var directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "runtime-consumer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var program = GameEventScriptBuilder.Create().WithDebugInfo(debugInfo).AddScript("on Start(value) { emit Done(result: (parse value) + 1) }").Compile();
            var path = Path.Combine(directory, "program.gesb");
            File.WriteAllBytes(path, GameEventScriptProgramWriter.ToArray(program));
            var configuration = typeof(GameEventScriptRuntimeDistributionTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
            var consumerDirectory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "runtime-consumer", "projects", "bin", configuration, "net8.0");
            var result = DotNetProcess.Execute(Path.Combine(consumerDirectory, "GameEventScript.RuntimeConsumer.dll"), directory, path);
            Assert.AreEqual(0, result.ExitCode, result.StandardOutput + result.StandardError);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
