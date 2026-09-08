// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StepH.GameEventScript.Conformance;
using StepH.GameEventScript.Conformance.Worker;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
[DoNotParallelize]
public sealed class ConformanceProcessIsolationTests
{
    private static ConformanceDocument Document => ConformanceCSharpTestEnvironment.Documents.Single(document => document.SuiteId == ConformanceProcessIsolation.SuiteId);
    private static ConformanceCase Case => Document.Cases.Single(testCase => testCase.Id == "valid-shallow-call-chain");
    private static string DocumentHash => Convert.ToHexString(SHA256.HashData(Document.Source.Utf8Bytes.ToArray()));

    [TestMethod]
    public void DepthCasesAreDiscoveredExactlyOnceAndAlwaysIsolated()
    {
        var regular = ConformanceMarkdownCorpusTests.Cases().ToArray();
        var isolated = ConformanceMarkdownCorpusTests.IsolatedCases().ToArray();
        Assert.HasCount(4, isolated);
        Assert.IsFalse(regular.Any(row => ((ConformanceDocument)row[0]).SuiteId == ConformanceProcessIsolation.SuiteId));
        Assert.IsTrue(isolated.All(row => ((ConformanceDocument)row[0]).SuiteId == ConformanceProcessIsolation.SuiteId));
        var all = regular.Concat(isolated).Select(row => ((ConformanceDocument)row[0]).SuiteId + "/" + row[1]).ToArray();
        Assert.HasCount(ConformanceCSharpTestEnvironment.Documents.Sum(document => document.Cases.Count), all);
        Assert.AreEqual(all.Length, all.Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    [DataRow("exit", "processExit")]
    [DataRow("hang", "timeout")]
    [DataRow("stdout", "outputLimit")]
    [DataRow("stderr", "outputLimit")]
    [DataRow("missing", null)]
    public void WorkerFailuresAreBoundedAndLeaveNoRunningProcess(string probe, string? expectedFailure)
    {
        var directory = Path.Combine(TestRepositoryPaths.ConformanceArtifactsDirectory, "received", "isolation-probes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var capture = ConformanceProcessIsolation.Execute(["--probe", probe], directory, probe == "hang" ? 2000 : 10_000);

        Assert.AreEqual(expectedFailure, capture.Failure);
        Assert.IsNotNull(capture.ProcessId);
        Assert.IsNotNull(capture.ExitCode, "The worker must have terminated before the adapter returns.");
        Assert.IsLessThanOrEqualTo(ConformanceProcessIsolation.OutputLimitBytes, capture.StandardOutput.Length);
        Assert.IsLessThanOrEqualTo(ConformanceProcessIsolation.OutputLimitBytes, capture.StandardError.Length);
        if (probe == "missing")
        {
            Assert.AreEqual(0, capture.ExitCode);
            Assert.AreEqual(ConformanceCaseStatus.Failed, ConformanceProcessIsolation.RestoreResult(Case, DocumentHash, capture.StandardOutput).Status);
        }
        try
        {
            using var process = Process.GetProcessById(capture.ProcessId.Value);
            Assert.IsTrue(process.HasExited, "The worker must not remain alive after timeout or output overflow.");
        }
        catch (ArgumentException) { }
    }

    [TestMethod]
    [DataRow("Passed")]
    [DataRow("Failed")]
    [DataRow("Error")]
    public void ValidResultPreservesTheRequestedCaseAndAssertions(string status)
    {
        var response = Response() with
        {
            Status = status,
            Code = status == "Passed" ? ConformanceRunnerCodes.Passed : ConformanceRunnerCodes.AssertionMismatch,
            Mismatches = status == "Passed" ? [] : [new WorkerMismatch("/binary/requiredCallStackDepth", ConformanceRunnerCodes.AssertionMismatch, "7", "0")]
        };
        var result = ConformanceProcessIsolation.RestoreResult(Case, DocumentHash, JsonSerializer.SerializeToUtf8Bytes(response));
        Assert.AreEqual(Enum.Parse<ConformanceCaseStatus>(status), result.Status);
        Assert.AreEqual(Case.FullId, result.Id);
        if (status != "Passed")
        {
            Assert.HasCount(1, result.Mismatches);
            Assert.AreEqual("/binary/requiredCallStackDepth", result.Mismatches[0].Path);
            Assert.AreEqual("7", result.Mismatches[0].Expected);
            Assert.AreEqual("0", result.Mismatches[0].Actual);
        }
    }

    [TestMethod]
    [DataRow("version")]
    [DataRow("case")]
    [DataRow("document")]
    [DataRow("fixture")]
    [DataRow("status")]
    [DataRow("contradiction")]
    [DataRow("failure-with-pass-code")]
    [DataRow("missing-mismatches")]
    [DataRow("missing")]
    [DataRow("malformed")]
    [DataRow("truncated")]
    [DataRow("duplicate")]
    [DataRow("multiple")]
    [DataRow("unknown-field")]
    [DataRow("oversized")]
    public void InvalidOrUnrelatedWorkerResultsNeverBecomePasses(string corruption)
    {
        var response = Response();
        response = corruption switch
        {
            "version" => response with { Version = 2 },
            "case" => response with { CaseId = "different/case" },
            "document" => response with { DocumentSha256 = new string('0', 64) },
            "fixture" => response with { FixtureSha256 = new string('0', 64) },
            "status" => response with { Status = "Skipped" },
            "contradiction" => response with { Mismatches = [new WorkerMismatch("/binary/outcome", ConformanceRunnerCodes.AssertionMismatch, "valid", "validationError")] },
            "failure-with-pass-code" => response with { Status = "Failed" },
            "missing-mismatches" => response with { Mismatches = null! },
            _ => response
        };
        var json = JsonSerializer.Serialize(response);
        json = corruption switch
        {
            "missing" => "",
            "malformed" => "{",
            "truncated" => json[..^1],
            "duplicate" => "{\"Version\":1," + json[1..],
            "multiple" => json + json,
            "unknown-field" => "{\"Unexpected\":true," + json[1..],
            "oversized" => new string(' ', ConformanceProcessIsolation.OutputLimitBytes + 1),
            _ => json
        };
        var result = ConformanceProcessIsolation.RestoreResult(Case, DocumentHash, Encoding.UTF8.GetBytes(json));
        Assert.AreEqual(ConformanceCaseStatus.Failed, result.Status);
        Assert.AreEqual("invalidResult", result.Mismatches[0].Actual);
    }

    private static WorkerResponse Response()
        => new(1, Case.FullId, DocumentHash, Case.BinaryFixture!.Sha256, "Passed", ConformanceRunnerCodes.Passed, [], null);
}
