// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace GameEventScript.Tests.Native;

internal static class DotNetProcess
{
    internal static DotNetProcessResult Execute(string assemblyPath, string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(assemblyPath);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
            Assert.Fail(".NET consumer invocation did not complete within 30 seconds.");
        }
        return new DotNetProcessResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}

internal sealed record DotNetProcessResult(int ExitCode, string StandardOutput, string StandardError);
