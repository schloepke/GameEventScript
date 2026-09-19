// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace GameEventScript.Tests.Native;

internal static class DotNetProcess
{
    internal static DotNetProcessResult Execute(string assemblyPath, string directory, params string[] arguments)
        => ExecuteCore(assemblyPath, directory, null, arguments);

    internal static DotNetProcessResult ExecuteWithInput(string assemblyPath, string directory, string input, params string[] arguments)
        => ExecuteCore(assemblyPath, directory, input, arguments);

    internal static DotNetProcessResult ExecuteConfigured(string assemblyPath, string directory, string? input, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
        => ExecuteCore(assemblyPath, directory, input, arguments, environment);

    private static DotNetProcessResult ExecuteCore(string assemblyPath, string directory, string? input, string[] arguments, IReadOnlyDictionary<string, string?>? environment = null)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            WorkingDirectory = directory,
            CreateNoWindow = true
        };
        if (environment is not null)
            foreach (var variable in environment)
            {
                if (variable.Value is null) start.Environment.Remove(variable.Key);
                else start.Environment[variable.Key] = variable.Value;
            }
        start.ArgumentList.Add(assemblyPath);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var inputWrite = input is null ? Task.CompletedTask : WriteInput();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
            Assert.Fail(".NET consumer invocation did not complete within 30 seconds.");
        }
        inputWrite.GetAwaiter().GetResult();
        return new DotNetProcessResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());

        async Task WriteInput()
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
        }
    }
}

internal sealed record DotNetProcessResult(int ExitCode, string StandardOutput, string StandardError);
