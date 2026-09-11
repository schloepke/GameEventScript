// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;

namespace StepH_GameEventScript_Tests.Native.Tool;

internal static class ToolProcess
{
    internal static ToolProcessResult Execute(string directory, params string[] arguments)
    {
        var configuration = typeof(ToolProcess).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var tool = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tool", "bin", configuration, "net8.0", "StepH.GameEventScript.Tool.dll");
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(tool);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
            Assert.Fail("CLI invocation did not complete within 30 seconds.");
        }
        return new ToolProcessResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}

internal sealed record ToolProcessResult(int ExitCode, string StandardOutput, string StandardError);
