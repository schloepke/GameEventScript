// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using StepH.GameEventScript.Conformance;
using StepH.GameEventScript.Conformance.Worker;

namespace StepH_GameEventScript_Tests.Conformance;

internal static class ConformanceProcessIsolation
{
    internal const string SuiteId = "program.call-graph-depth";
    internal const int OutputLimitBytes = 64 * 1024;
    private const int TimeoutMilliseconds = 30_000;

    internal static bool IsRequired(ConformanceCase testCase) => testCase.SuiteId == SuiteId;

    internal static ConformanceCaseResult Run(ConformanceDocument document, ConformanceCase testCase)
    {
        var directory = Path.Combine(TestRepositoryPaths.ConformanceArtifactsDirectory, "received", "isolated", testCase.SuiteId, testCase.Id, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = document.Source.Utf8Bytes.ToArray();
        var documentHash = Convert.ToHexString(SHA256.HashData(source));
        var inputPath = Path.Combine(directory, "input.md");
        File.WriteAllBytes(inputPath, source);
        var capture = Execute([inputPath, Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures"), testCase.FullId], directory, TimeoutMilliseconds);
        File.WriteAllBytes(Path.Combine(directory, "stdout.txt"), capture.StandardOutput);
        File.WriteAllBytes(Path.Combine(directory, "stderr.txt"), capture.StandardError);
        File.WriteAllText(Path.Combine(directory, "execution.json"), JsonSerializer.Serialize(new
        {
            caseId = testCase.FullId,
            documentHash,
            fixtureHash = testCase.BinaryFixture!.Sha256,
            capture.ProcessId,
            capture.ExitCode,
            capture.Failure,
            timeoutMilliseconds = TimeoutMilliseconds,
            outputLimitBytes = OutputLimitBytes,
            requestedStackBytes = 1024 * 1024
        }) + "\n");
        if (capture.Failure is not null)
            return Failure(testCase, capture.Failure, $"Worker PID {capture.ProcessId}, exit {capture.ExitCode}; evidence: {directory}. {capture.Detail}");
        return RestoreResult(testCase, documentHash, capture.StandardOutput);
    }

    internal static ConformanceCaseResult RestoreResult(ConformanceCase testCase, string documentHash, byte[] output)
    {
        try
        {
            if (output.Length == 0 || output.Length > OutputLimitBytes) throw new InvalidDataException("Missing or oversized worker result.");
            using var parsed = JsonDocument.Parse(output, new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicateProperties(parsed.RootElement);
            var response = JsonSerializer.Deserialize<WorkerResponse>(output, new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 });
            if (response is null || response.Version != 1 || response.CaseId != testCase.FullId || response.DocumentSha256 != documentHash || response.FixtureSha256 != testCase.BinaryFixture!.Sha256)
                throw new InvalidDataException("Worker protocol identity mismatch.");
            if (response.Status is not ("Passed" or "Failed" or "Error") || string.IsNullOrWhiteSpace(response.Code) || response.Mismatches is null || response.Mismatches.Length > 256)
                throw new InvalidDataException("Worker result has an invalid status or payload.");
            if (response.Status == "Passed" && (response.Code != ConformanceRunnerCodes.Passed || response.Mismatches.Length != 0) || response.Status != "Passed" && response.Code == ConformanceRunnerCodes.Passed)
                throw new InvalidDataException("Worker result status contradicts its assertions.");
            if (response.Mismatches.Any(mismatch => mismatch is null || string.IsNullOrEmpty(mismatch.Path) || !mismatch.Path.StartsWith('/') || string.IsNullOrWhiteSpace(mismatch.Code)))
                throw new InvalidDataException("Worker mismatch has an invalid shape.");
            var mismatches = response.Mismatches.Select(mismatch => new ConformanceMismatch(mismatch.Path, mismatch.Code, mismatch.Expected, mismatch.Actual)).ToArray();
            return new ConformanceCaseResult(testCase, Enum.Parse<ConformanceCaseStatus>(response.Status), response.Code, [], mismatches, [], [], null, null, response.TechnicalDetails);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or NotSupportedException)
        {
            return Failure(testCase, "invalidResult", exception.Message);
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate worker result property.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child);
    }

    private static ConformanceCaseResult Failure(ConformanceCase testCase, string reason, string detail)
        => new(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch, [],
            [new ConformanceMismatch("/execution", ConformanceRunnerCodes.AssertionMismatch, "completed", reason)], [], [], null, null, detail);

    internal static ProcessCapture Execute(IReadOnlyList<string> arguments, string directory, int timeoutMilliseconds)
        => ExecuteAsync(arguments, directory, timeoutMilliseconds).GetAwaiter().GetResult();

    private static async Task<ProcessCapture> ExecuteAsync(IReadOnlyList<string> arguments, string directory, int timeoutMilliseconds)
    {
        var configuration = typeof(ConformanceProcessIsolation).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var worker = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "conformance-worker", "bin", configuration, "net8.0", "StepH.GameEventScript.Conformance.Worker.dll");
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
