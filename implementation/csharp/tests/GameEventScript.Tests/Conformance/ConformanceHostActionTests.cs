// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using GameEventScript.Conformance;

namespace GameEventScript.Tests.Conformance;

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
    [DataRow("full-queue-reject-and-retry", "program-null")]
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
            "program-null" => text.Replace("programName: \"late\"", "programName: null", StringComparison.Ordinal),
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

    [TestMethod]
    [DataRow("faultCode: \"\"")]
    [DataRow("faultCode: \" \\t\"")]
    [DataRow("faultCode: runtime.invalid")]
    [DataRow("faultCode: test.nativeFault\n    throw: true")]
    [DataRow("faultCode: null")]
    public void RejectsInvalidNativeFaultConfiguration(string replacement)
    {
        var text = File.ReadAllText(Path.Combine(ConformanceCSharpTestEnvironment.GetConformanceDirectory(), "suites", "runtime", "host-dispatch.md"));
        text = text.Replace("faultCode: test.nativeFault", replacement, StringComparison.Ordinal);

        var exception = Assert.ThrowsExactly<ConformanceParseException>(() => ConformanceMarkdownParser.Parse(text));

        Assert.AreEqual(ConformanceDiagnosticCodes.SchemaInvalidValue, exception.Diagnostics[0].Code);
    }

    [TestMethod]
    [DataRow("faultContext: {}", ConformanceDiagnosticCodes.SchemaInvalidValue)]
    [DataRow("faultCode: test.nativeFault\n    faultContext: null", ConformanceDiagnosticCodes.SchemaInvalidValue)]
    [DataRow("faultCode: test.nativeFault\n    faultContext: { programName: 1 }", ConformanceDiagnosticCodes.SchemaInvalidValue)]
    [DataRow("faultCode: test.nativeFault\n    faultContext: { unknown: value }", ConformanceDiagnosticCodes.SchemaUnknownField)]
    public void RejectsInvalidNativeFaultContext(string replacement, string code)
    {
        var text = ReadDispatchSuite().Replace("    faultCode: test.nativeFault\n    emit:", "    " + replacement + "\n    emit:", StringComparison.Ordinal);

        var exception = Assert.ThrowsExactly<ConformanceParseException>(() => ConformanceMarkdownParser.Parse(text));

        Assert.AreEqual(code, exception.Diagnostics[0].Code);
    }

    [TestMethod]
    [DataRow("native-declared-fault-context", "programName: null", "programName: wrong")]
    [DataRow("native-declared-fault-context", "handlerName: \"Start()\"", "handlerName: null")]
    [DataRow("native-supplied-fault-context", "programName: reported.program", "programName: null")]
    public void DiagnosticContextAssertionsRejectIncorrectExpectations(string caseId, string expected, string replacement)
    {
        // Eight spaces target expectation fields; fixture inputs use six spaces.
        var text = ReadDispatchSuite().Replace("        " + expected, "        " + replacement, StringComparison.Ordinal);
        var document = ConformanceMarkdownParser.Parse(text);

        var result = ConformanceRunner.RunCase(document, caseId, ConformanceCSharpTestEnvironment.Deterministic());

        Assert.AreEqual(ConformanceCaseStatus.Failed, result.Status);
        Assert.AreEqual(ConformanceRunnerCodes.AssertionMismatch, result.Code);
        Assert.IsTrue(result.Mismatches.Any(mismatch => mismatch.Path.Contains("/diagnostics/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void OmittedDiagnosticContextRemainsUnconstrained()
    {
        var text = ReadDispatchSuite().Replace("        programName: reported.program\n", string.Empty, StringComparison.Ordinal);
        var document = ConformanceMarkdownParser.Parse(text);

        var result = ConformanceRunner.RunCase(document, "native-supplied-fault-context", ConformanceCSharpTestEnvironment.Deterministic());

        Assert.AreEqual(ConformanceCaseStatus.Passed, result.Status);
    }

    private static string ReadDispatchSuite()
        => File.ReadAllText(Path.Combine(ConformanceCSharpTestEnvironment.GetConformanceDirectory(), "suites", "runtime", "host-dispatch.md"));

    private static string ReadSuite()
        => File.ReadAllText(Path.Combine(ConformanceCSharpTestEnvironment.GetConformanceDirectory(), "suites", "runtime", "host-load-limits.md"));
}
