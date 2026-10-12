// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace GameEventScript.Tests.Native;

[TestClass]
public sealed class CompilerProcessIsolationTests
{
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
        var capture = CompilerProcessIsolation.Execute(["--probe", probe], directory, probe == "hang" ? 2000 : 10_000);

        Assert.AreEqual(expectedFailure, capture.Failure);
        Assert.IsNotNull(capture.ProcessId);
        Assert.IsNotNull(capture.ExitCode, "The worker must have terminated before the adapter returns.");
        Assert.IsLessThanOrEqualTo(CompilerProcessIsolation.OutputLimitBytes, capture.StandardOutput.Length);
        Assert.IsLessThanOrEqualTo(CompilerProcessIsolation.OutputLimitBytes, capture.StandardError.Length);
        try
        {
            using var process = Process.GetProcessById(capture.ProcessId.Value);
            Assert.IsTrue(process.HasExited, "The worker must not remain alive after timeout or output overflow.");
        }
        catch (ArgumentException) { }
    }

}
