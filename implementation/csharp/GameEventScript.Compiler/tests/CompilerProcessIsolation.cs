// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;

namespace GameEventScript.Tests.Native;

internal static class CompilerProcessIsolation
{
    internal const int OutputLimitBytes = 64 * 1024;

    internal static ProcessCapture Execute(IReadOnlyList<string> arguments, string directory, int timeoutMilliseconds)
        => ExecuteAsync(arguments, directory, timeoutMilliseconds).GetAwaiter().GetResult();

    private static async Task<ProcessCapture> ExecuteAsync(IReadOnlyList<string> arguments, string directory, int timeoutMilliseconds)
    {
        var configuration = typeof(CompilerProcessIsolation).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var worker = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "compiler-worker", "bin", configuration, "net8.0", "GameEventScript.Compiler.Worker.dll");
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(worker);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_DbgEnableMiniDump"] = "0";
        start.Environment["COMPlus_DbgEnableMiniDump"] = "0";
        using var process = new Process { StartInfo = start };
        try
        {
            if (!File.Exists(worker) || !process.Start()) return new ProcessCapture(null, null, "startFailed", [], [], "The built worker could not be started.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new ProcessCapture(null, null, "startFailed", [], [], exception.Message);
        }

        using var cancellation = new CancellationTokenSource();
        var overflow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var readOutput = Capture(process.StandardOutput.BaseStream, stdout, overflow, cancellation.Token);
        var readError = Capture(process.StandardError.BaseStream, stderr, overflow, cancellation.Token);
        var exited = process.WaitForExitAsync(cancellation.Token);
        var finished = Task.WhenAll(exited, readOutput, readError);
        var timeout = Task.Delay(timeoutMilliseconds, cancellation.Token);
        var completed = await Task.WhenAny(finished, overflow.Task, timeout);
        string? failure = overflow.Task.IsCompleted ? "outputLimit" : completed == timeout ? "timeout" : null;
        if (failure is not null)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            if (await Task.WhenAny(finished, Task.Delay(5000)) != finished)
            {
                await cancellation.CancelAsync();
                process.StandardOutput.Close();
                process.StandardError.Close();
                return new ProcessCapture(process.Id, process.HasExited ? process.ExitCode : null, "cleanupFailed", [], [], "Worker termination or pipe closure did not complete.");
            }
        }
        try { await finished; }
        catch (Exception exception) when (exception is IOException or OperationCanceledException) { failure ??= "pipeFailed"; }
        await cancellation.CancelAsync();
        if (process.ExitCode != 0 && failure is null) failure = "processExit";
        return new ProcessCapture(process.Id, process.ExitCode, failure, stdout.ToArray(), stderr.ToArray(), "");
    }

    private static async Task Capture(Stream input, MemoryStream retained, TaskCompletionSource overflow, CancellationToken cancellation)
    {
        var buffer = new byte[4096];
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellation);
            if (count == 0) return;
            var keep = Math.Min(count, OutputLimitBytes - checked((int)retained.Length));
            retained.Write(buffer, 0, keep);
            if (keep != count) overflow.TrySetResult();
        }
    }

    internal sealed record ProcessCapture(int? ProcessId, int? ExitCode, string? Failure, byte[] StandardOutput, byte[] StandardError, string Detail);
}
