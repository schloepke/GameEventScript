// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using StepH.GameEventScript.Conformance;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class ConformanceHostActionTests
{
    [TestMethod]
    public void BindsStructuredLoadErrorsInStepAndNativeActions()
    {
        var document = ConformanceMarkdownParser.Parse(ReadSuite());
        var stepError = document.Cases.Single(test => test.Id == "full-queue-reject-and-retry").Steps.Single(step => step.Id == "rejected").Actions[0].ExpectedError!;
        var nativeError = document.Cases.Single(test => test.Id == "native-callback-handles-rejection").NativeHandlers.Single(handler => handler.Id == "loader").Actions[0].ExpectedError!;

        foreach (var error in new[] { stepError, nativeError })
        {
            Assert.AreEqual("link", error.Phase);
            Assert.AreEqual("link.initializationQueueFull", error.Code);
            Assert.AreEqual("late", error.ProgramName);
        }
    }

    [TestMethod]
    [DataRow("operation", ConformanceDiagnosticCodes.SchemaInvalidValue)]
    [DataRow("both", ConformanceDiagnosticCodes.SchemaInvalidValue)]
    [DataRow("phase", ConformanceDiagnosticCodes.SchemaInvalidValue)]
    [DataRow("code", ConformanceDiagnosticCodes.SchemaMissingField)]
    public void RejectsInvalidLoadErrorExpectations(string mutation, string code)
    {
        var text = ReadSuite();
        text = mutation switch
        {
            "operation" => text.Replace("loadProgram: \"late\"", "detachProgram: \"late\"", StringComparison.Ordinal),
            "both" => Regex.Replace(text, @"(?m)^([ ]*)expectError:", "$1expectResult: false\n$1expectError:"),
            "phase" => text.Replace("phase: \"link\"", "phase: \"runtime\"", StringComparison.Ordinal),
            _ => text.Replace("code: \"link.initializationQueueFull\"", "", StringComparison.Ordinal)
        };

        var exception = Assert.ThrowsExactly<ConformanceParseException>(() => ConformanceMarkdownParser.Parse(text));

        Assert.AreEqual(code, exception.Diagnostics[0].Code);
    }

    [TestMethod]
    [DataRow("full-queue-reject-and-retry", "code")]
    [DataRow("full-queue-reject-and-retry", "program")]
    [DataRow("native-callback-handles-rejection", "code")]
    [DataRow("native-callback-handles-rejection", "program")]
    [DataRow("native-callback-handles-rejection", "result")]
    [DataRow("last-slot-accepts-initialization", "success")]
    public void HostActionAssertionsRejectIncorrectExpectations(string caseId, string mutation)
    {
        var text = ReadSuite();
        text = mutation switch
        {
            "code" => text.Replace("link.initializationQueueFull", "link.missingExtension", StringComparison.Ordinal),
            "program" => text.Replace("programName: \"late\"", "programName: \"wrong\"", StringComparison.Ordinal),
            "result" => text.Replace("expectResult: false", "expectResult: true", StringComparison.Ordinal),
            _ => Regex.Replace(text, @"(loadProgram: ""late""\n[ ]*)expectResult: true", "$1expectError: { phase: link, code: link.initializationQueueFull }")
        };
        var document = ConformanceMarkdownParser.Parse(text);

        var result = ConformanceRunner.RunCase(document, caseId, ConformanceCSharpTestEnvironment.Deterministic());

        Assert.AreEqual(ConformanceCaseStatus.Failed, result.Status);
        Assert.AreEqual(ConformanceRunnerCodes.AssertionMismatch, result.Code);
        var suffix = mutation == "result" ? "/result" : "/error";
        Assert.IsTrue(result.Mismatches.Any(mismatch => mismatch.Path.Contains("/actions/", StringComparison.Ordinal) && mismatch.Path.EndsWith(suffix, StringComparison.Ordinal)));
        if (mutation is "code" or "program")
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Phase == "link" && diagnostic.Code == "link.initializationQueueFull" && diagnostic.ProgramName == "late"));
    }

    private static string ReadSuite()
        => File.ReadAllText(Path.Combine(ConformanceCSharpTestEnvironment.GetConformanceDirectory(), "suites", "runtime", "host-load-limits.md"));
}
