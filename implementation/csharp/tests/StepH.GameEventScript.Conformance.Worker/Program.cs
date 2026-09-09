// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;

namespace StepH.GameEventScript.Conformance.Worker;

internal static class Program
{
    private const int StackBytes = 1024 * 1024;
    private const int MaximumDocumentBytes = 16 * 1024 * 1024;

    private static int Main(string[] args)
    {
        // The compiler probe also exercises the larger frames of unoptimized Debug builds.
        if (args.Length == 2 && args[0] == "--source-nesting") return SourceNestingProbe.Run(args[1], 512 * 1024);
        if (args.Length == 2 && args[0] == "--probe") return Probe(args[1]);
        if (args.Length != 3) return 64;
        try
        {
            var bytes = ReadBounded(args[0], MaximumDocumentBytes);
            var document = ConformanceMarkdownParser.Parse(bytes);
            var testCase = document.Cases.Single(testCase => testCase.FullId == args[2]);
            if (testCase.Kind != ConformanceTestKind.ProgramBinary || testCase.Steps.Count != 0 || testCase.BinaryFixture!.CompareCompiledRuntime)
                throw new InvalidOperationException("The isolation worker supports binary validation/rewrite cases without execution or compilation.");

            var resourceRoot = Path.GetFullPath(args[1]);
            var resourcePath = Path.GetFullPath(Path.Combine(resourceRoot, testCase.BinaryFixture.RelativePath));
            if (!resourcePath.StartsWith(resourceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Fixture path escaped the resource root.");
            var environment = new ConformanceRunnerEnvironment(
                "steph.ges.conformance.csharp.worker", "1", "steph.ges.csharp", "0.1.0", ["program-binary"],
                resourceResolver: new FixtureResolver(testCase.BinaryFixture.ResourceId, resourcePath));
            ConformanceCaseResult? result = null;
            var thread = new Thread(() => result = ConformanceRunner.RunCase(document, testCase.Id, environment, new ConformanceRunnerOptions { IncludeTechnicalDetails = true }), StackBytes);
            thread.Start();
            thread.Join();
            if (result is null || result.Diagnostics.Count != 0 || result.RuntimeLimits.Count != 0 || result.MissingCapabilities.Count != 0 || result.Mismatches.Any(mismatch => mismatch.Diagnostic is not null))
                throw new InvalidOperationException("Worker result exceeds the binary-only result protocol.");
            var response = new WorkerResponse(
                1, result.Id, Convert.ToHexString(SHA256.HashData(bytes)), testCase.BinaryFixture.Sha256, result.Status.ToString(), result.Code,
                result.Mismatches.Select(mismatch => new WorkerMismatch(mismatch.Path, mismatch.Code, mismatch.Expected, mismatch.Actual)).ToArray(), result.TechnicalDetails);
            Console.Write(JsonSerializer.Serialize(response));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.GetType().Name + ": " + exception.Message);
            return 70;
        }
    }

    private static byte[] ReadBounded(string path, int maximum)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > maximum) throw new InvalidDataException("Worker input exceeds its byte limit.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidDataException("Worker input grew while being read.");
        return bytes;
    }

    private sealed class FixtureResolver(string resourceId, string path) : IConformanceResourceResolver
    {
        public ConformanceResourceResult Resolve(string requestedId, int maximumByteCount)
            => requestedId == resourceId
                ? new ConformanceResourceResult(ConformanceResourceStatus.Found, ReadBounded(path, maximumByteCount))
                : new ConformanceResourceResult(ConformanceResourceStatus.NotFound);
    }

    private static int Probe(string mode)
    {
        switch (mode)
        {
            case "exit":
                Console.Write("{\"Status\":\"Passed\"}");
                return 23;
            case "hang":
                Thread.Sleep(Timeout.Infinite);
                return 0;
            case "stdout":
            case "stderr":
                var output = mode == "stdout" ? Console.Out : Console.Error;
                var chunk = new string('x', 4096);
                while (true) output.Write(chunk);
            case "missing":
                return 0;
            default:
                return 64;
        }
    }
}
