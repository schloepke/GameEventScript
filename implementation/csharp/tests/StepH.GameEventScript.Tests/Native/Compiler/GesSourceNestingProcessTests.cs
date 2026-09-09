// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using StepH_GameEventScript_Tests.Conformance;

namespace StepH_GameEventScript_Tests.Native.Compiler;

[TestClass]
public sealed class GesSourceNestingProcessTests
{
    [TestMethod]
    [TestCategory("Isolation")]
    [DataRow("deep")]
    [DataRow("boundary")]
    public void CompilerCompletesOnABoundedNativeStack(string mode)
    {
        var directory = Path.Combine(TestRepositoryPaths.ConformanceArtifactsDirectory, "received", "source-nesting", mode, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var capture = ConformanceProcessIsolation.Execute(["--source-nesting", mode], directory, 30_000);
        File.WriteAllBytes(Path.Combine(directory, "stdout.txt"), capture.StandardOutput);
        File.WriteAllBytes(Path.Combine(directory, "stderr.txt"), capture.StandardError);
        Assert.IsNull(capture.Failure, $"Compiler worker failed: {capture.Failure}; exit {capture.ExitCode}; evidence: {directory}.");
        Assert.AreEqual(0, capture.ExitCode);
        Assert.AreEqual("completed:" + mode, Encoding.UTF8.GetString(capture.StandardOutput));
    }
}
