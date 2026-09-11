// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.Native.Tool;

/// <summary>Verifies the CLI process and filesystem integration around the portable compiler and codec.</summary>
[TestClass]
public sealed class GameEventScriptCompileCommandTests
{
    private const string Source = "module cli\non Start(value) { emit Done(result: value + 1, text: 'Grüße 😀') }\n";
    private string _directory = null!;

    /// <summary>Creates an isolated workspace below the repository artifacts directory.</summary>
    [TestInitialize]
    public void CreateWorkspace()
    {
        _directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "tool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Removes the source and output files created by the test.</summary>
    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_directory, true);

    /// <summary>Verifies UTF-8 input, canonical binary output, debug metadata, and execution of the emitted file.</summary>
    /// <param name="withBom">Whether the input includes a UTF-8 BOM.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CompileWritesCanonicalProgramWithDefaultDebugInfo(bool withBom)
    {
        File.WriteAllText(Path.Combine(_directory, "source file.ges"), Source, new UTF8Encoding(withBom, true));
        var result = Run("compile", "source file.ges");
        AssertSuccess(result);
        var bytes = File.ReadAllBytes(Path.Combine(_directory, "source file.gesb"));
        var expected = GameEventScriptBuilder.Create().AddScript(Source, "source file.ges").Compile();
        CollectionAssert.AreEqual(GameEventScriptProgramWriter.ToArray(expected), bytes);
        var program = GameEventScriptProgramReader.Read(bytes);
        Assert.IsNotNull(program.DebugSymbols);
        Assert.IsNotNull(program.SourceMap);
        Assert.IsNotNull(program.SourceArchive);
        Assert.AreEqual(Source, program.SourceArchive.Sources[0].ResolveText());
        Assert.AreEqual("source file.ges", program.SourceArchive.Sources[0].SourceName);
        StringAssert.Contains(result.StandardOutput, "Sources (1):\n  source file.ges".Replace("\n", Environment.NewLine, StringComparison.Ordinal));
        StringAssert.Contains(result.StandardOutput, "Module: cli");
        StringAssert.Contains(result.StandardOutput, "Handlers: 1");
        StringAssert.Contains(result.StandardOutput, $"File size: {bytes.Length} bytes");
        StringAssert.Contains(result.StandardOutput, "Debug info: symbols, source map, source archive");
        StringAssert.Matches(result.StandardOutput, new Regex(@"Duration: [0-9]+\.[0-9] ms"));
        Assert.IsFalse(result.StandardOutput.Contains("Required registers:", StringComparison.Ordinal));

        long? received = null;
        var host = GameEventScriptHost.CreateBuilder().WithRandomSeed(1).Build();
        host.Subscribe("Done", ["result", "text"], (message, _) => received = message.Arguments.GetAsInteger("result"));
        host.Load(program);
        Assert.IsTrue(host.Receive(GameEventScriptCSharpMessage.Create("Start", ("value", GesValue.GesInteger(41)))));
        Assert.AreEqual(GameEventScriptExecutionState.Completed, host.RunToCompletion().State);
        Assert.AreEqual(42L, received);
    }

    /// <summary>Verifies explicit output paths and omission of the optional debug sections.</summary>
    /// <param name="option">The short or long output option.</param>
    [TestMethod]
    [DataRow("-o")]
    [DataRow("--output")]
    public void CompileCreatesExplicitOutputDirectoryAndCanOmitDebugInfo(string option)
    {
        File.WriteAllText(Path.Combine(_directory, "source.ges"), Source);
        AssertSuccess(Run("compile", option, "nested output/program.gesb", "--no-debug", "source.ges"));
        Assert.IsFalse(File.Exists(Path.Combine(_directory, "source.gesb")));
        var bytes = File.ReadAllBytes(Path.Combine(_directory, "nested output", "program.gesb"));
        var expected = GameEventScriptBuilder.Create().AddScript(Source, "source.ges").WithDebugInfo(GameEventScriptDebugInfoOptions.None).Compile();
        CollectionAssert.AreEqual(GameEventScriptProgramWriter.ToArray(expected), bytes);
        var program = GameEventScriptProgramReader.Read(bytes);
        Assert.IsNull(program.DebugSymbols);
        Assert.IsNull(program.SourceMap);
        Assert.IsNull(program.SourceArchive);
    }

    /// <summary>Verifies that recompiling changed source replaces an existing artifact.</summary>
    [TestMethod]
    public void SuccessfulRecompilationReplacesPreviousOutput()
    {
        var sourcePath = Path.Combine(_directory, "source.ges");
        var outputPath = Path.Combine(_directory, "source.gesb");
        File.WriteAllText(sourcePath, Source);
        AssertSuccess(Run("compile", "source.ges"));
        var original = File.ReadAllBytes(outputPath);
        var changed = Source.Replace("value + 1", "value + 2", StringComparison.Ordinal);
        File.WriteAllText(sourcePath, changed);
        AssertSuccess(Run("compile", "source.ges"));
        var updated = File.ReadAllBytes(outputPath);
        Assert.IsFalse(original.SequenceEqual(updated));
        CollectionAssert.AreEqual(GameEventScriptProgramWriter.ToArray(GameEventScriptBuilder.Create().AddScript(changed, "source.ges").Compile()), updated);
        Assert.IsEmpty(Directory.GetFiles(_directory, ".ges-*.tmp"));
    }

    /// <summary>Verifies diagnostic codes, locations, and output preservation for source failures.</summary>
    /// <param name="failure">The kind of source failure to exercise.</param>
    /// <param name="existingOutput">Whether a previous artifact exists.</param>
    [TestMethod]
    [DataRow("parse", false)]
    [DataRow("parse", true)]
    [DataRow("validate", false)]
    [DataRow("validate", true)]
    [DataRow("encoding", false)]
    [DataRow("encoding", true)]
    [DataRow("missing", false)]
    [DataRow("missing", true)]
    public void SourceFailuresReportDiagnosticsWithoutChangingOutput(string failure, bool existingOutput)
    {
        var sourcePath = Path.Combine(_directory, "source.ges");
        var outputPath = Path.Combine(_directory, "source.gesb");
        byte[] previous = [1, 2, 3, 4];
        if (existingOutput) File.WriteAllBytes(outputPath, previous);
        var code = failure switch
        {
            "parse" => GameEventScriptDiagnosticCodes.ParseSyntax,
            "validate" => GameEventScriptDiagnosticCodes.ValidateDuplicateVariable,
            "encoding" => "cli.invalidEncoding",
            _ => "cli.io"
        };
        if (failure == "parse") File.WriteAllText(sourcePath, "on Start {\n emit Done(value: )\n}");
        if (failure == "validate") File.WriteAllText(sourcePath, "on Start {\n let value be 1\n let value be 2\n}");
        if (failure == "encoding") File.WriteAllBytes(sourcePath, [0xC3, 0x28]);
        var result = Run("compile", "source.ges");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        StringAssert.Contains(result.StandardError, code);
        if (failure == "parse") StringAssert.Contains(result.StandardError, "source.ges(2,");
        if (failure == "validate") StringAssert.Contains(result.StandardError, "source.ges(3,");
        if (existingOutput) CollectionAssert.AreEqual(previous, File.ReadAllBytes(outputPath));
        else Assert.IsFalse(File.Exists(outputPath));
        Assert.IsEmpty(Directory.GetFiles(_directory, ".ges-*.tmp"));
    }

    /// <summary>Verifies that multiple compiler diagnostics are all printed.</summary>
    [TestMethod]
    public void CompileReportsEveryValidationDiagnostic()
    {
        File.WriteAllText(Path.Combine(_directory, "source.ges"), "on Start {\n let alpha be 1\n let alpha be 2\n let beta be 1\n let beta be 2\n}");
        var result = Run("compile", "source.ges");
        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(2, result.StandardError.Split(GameEventScriptDiagnosticCodes.ValidateDuplicateVariable, StringSplitOptions.None).Length - 1, result.StandardError);
        Assert.IsFalse(File.Exists(Path.Combine(_directory, "source.gesb")));
    }

    /// <summary>Verifies usage errors, including an output path equal to the source path.</summary>
    [TestMethod]
    public void InvalidUsageDoesNotModifyTheSource()
    {
        File.WriteAllText(Path.Combine(_directory, "source.ges"), Source);
        string[][] invalid =
        [
            ["compile"],
            ["compile", "source.ges", "second.ges"],
            ["compile", "source.ges", "--unknown"],
            ["compile", "source.ges", "-o"],
            ["compile", "source.ges", "-o", "--no-debug"],
            ["compile", "source.ges", "-o", ""],
            ["compile", "source.ges", "-o", "one.gesb", "--output", "two.gesb"],
            ["compile", "source.ges", "-o", "./source.ges"],
            ["compile", "source.ges", "-v", "-q"],
            ["compile", "source.ges", "--quiet", "--verbose"],
            ["compile", "source.ges", ""]
        ];
        foreach (var arguments in invalid)
        {
            var result = Run(arguments);
            Assert.AreEqual(2, result.ExitCode, string.Join(" ", arguments));
            Assert.AreEqual(string.Empty, result.StandardOutput);
            StringAssert.Contains(result.StandardError, "cli.usage");
        }
        Assert.AreEqual(Source, File.ReadAllText(Path.Combine(_directory, "source.ges")));
        Assert.HasCount(1, Directory.GetFiles(_directory));
    }

    /// <summary>Verifies the option terminator for a source name that starts with a dash.</summary>
    [TestMethod]
    public void EndOfOptionsAllowsSourceNamesStartingWithADash()
    {
        File.WriteAllText(Path.Combine(_directory, "-source.ges"), Source);
        AssertSuccess(Run("compile", "--", "-source.ges"));
        Assert.IsNotNull(GameEventScriptProgramReader.Read(File.ReadAllBytes(Path.Combine(_directory, "-source.gesb"))));
    }

    /// <summary>Verifies destination preservation and temporary file cleanup after a write failure.</summary>
    [TestMethod]
    public void OutputFailureLeavesDestinationAndNoTemporaryFile()
    {
        File.WriteAllText(Path.Combine(_directory, "source.ges"), Source);
        var output = Directory.CreateDirectory(Path.Combine(_directory, "output.gesb"));
        File.WriteAllText(Path.Combine(output.FullName, "keep.txt"), "keep");
        var result = Run("compile", "source.ges", "-o", "output.gesb");
        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        StringAssert.Contains(result.StandardError, "cli.io");
        Assert.AreEqual("keep", File.ReadAllText(Path.Combine(output.FullName, "keep.txt")));
        Assert.IsEmpty(Directory.GetFiles(_directory, ".ges-*.tmp"));
    }

    /// <summary>Verifies that verbosity changes only the report and quiet mode still reports errors.</summary>
    /// <param name="option">A short or long verbosity option.</param>
    /// <param name="quiet">Whether successful output should be suppressed.</param>
    [TestMethod]
    [DataRow("-q", true)]
    [DataRow("--quiet", true)]
    [DataRow("-v", false)]
    [DataRow("--verbose", false)]
    public void VerbosityDoesNotChangeTheArtifact(string option, bool quiet)
    {
        const string source = "module cli\non Start(value) { emit Done(result: :test.vectorSum value) }\non Ping as message { emit Pong() }\n";
        var path = Path.Combine(_directory, "source.ges");
        File.WriteAllText(path, source);
        var result = Run("compile", "source.ges", "--no-debug", option);
        AssertSuccess(result);
        var expected = GameEventScriptBuilder.Create().AddScript(source, "source.ges").WithDebugInfo(GameEventScriptDebugInfoOptions.None).Compile();
        var bytes = File.ReadAllBytes(Path.Combine(_directory, "source.gesb"));
        CollectionAssert.AreEqual(GameEventScriptProgramWriter.ToArray(expected), bytes);
        if (quiet) Assert.AreEqual(string.Empty, result.StandardOutput);
        else
        {
            StringAssert.Contains(result.StandardOutput, "Handlers: 2");
            StringAssert.Contains(result.StandardOutput, "Debug info: none");
            StringAssert.Contains(result.StandardOutput, $"Bytecode: {expected.Code.Count} instructions, {expected.Code.Count * 16L} bytes");
            StringAssert.Contains(result.StandardOutput, $"Required registers: {expected.RequiredRegisterCount}");
            StringAssert.Contains(result.StandardOutput, $"Required call stack depth: {expected.RequiredCallStackDepth}");
            StringAssert.Contains(result.StandardOutput, "handler Start(value)");
            StringAssert.Contains(result.StandardOutput, "handler Ping as message");
            StringAssert.Contains(result.StandardOutput, "outbound Done(result)");
            StringAssert.Contains(result.StandardOutput, "Required extensions (1):");
            StringAssert.Contains(result.StandardOutput, "test.vectorSum(");
            StringAssert.Contains(result.StandardOutput, "Required external types (0):");
        }
        File.WriteAllText(path, "on Start { emit Done(value: ) }");
        var failure = Run("compile", "source.ges", option);
        Assert.AreEqual(1, failure.ExitCode);
        Assert.AreEqual(string.Empty, failure.StandardOutput);
        StringAssert.Contains(failure.StandardError, GameEventScriptDiagnosticCodes.ParseSyntax);
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(_directory, "source.gesb")));
    }

    /// <summary>Verifies cross-file definitions, source metadata, and handler order in a single output program.</summary>
    [TestMethod]
    public void MultipleSourcesCompileTogetherInArgumentOrder()
    {
        const string handlers = "module cli\non Start(value) { emit First(result: increment(value)) }\n";
        const string definitions = "module cli\nconstant $offset be 1\nfunction increment(_ value) be value + $offset\non Start(value) { emit Second(result: increment(value)) }\n";
        File.WriteAllText(Path.Combine(_directory, "handlers.ges"), handlers);
        File.WriteAllText(Path.Combine(_directory, "definitions.ges"), definitions);
        var result = Run("compile", "handlers.ges", "-o", "combined.gesb", "definitions.ges");
        AssertSuccess(result);
        var bytes = File.ReadAllBytes(Path.Combine(_directory, "combined.gesb"));
        var expected = GameEventScriptBuilder.Create().AddScript(handlers, "handlers.ges").AddScript(definitions, "definitions.ges").Compile();
        CollectionAssert.AreEqual(GameEventScriptProgramWriter.ToArray(expected), bytes);
        var program = GameEventScriptProgramReader.Read(bytes);
        CollectionAssert.AreEqual(new[] { "handlers.ges", "definitions.ges" }, program.SourceArchive!.Sources.Select(source => source.SourceName).ToArray());
        StringAssert.Contains(result.StandardOutput, "Sources (2):");
        StringAssert.Contains(result.StandardOutput, "Handlers: 2");
        var received = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().WithRandomSeed(1).Build();
        host.Subscribe("First", ["result"], (message, _) => received.Add("first:" + message.Arguments.GetAsInteger("result")));
        host.Subscribe("Second", ["result"], (message, _) => received.Add("second:" + message.Arguments.GetAsInteger("result")));
        host.Load(program);
        Assert.IsTrue(host.Receive(GameEventScriptCSharpMessage.Create("Start", ("value", GesValue.GesInteger(41)))));
        Assert.AreEqual(GameEventScriptExecutionState.Completed, host.RunToCompletion().State);
        CollectionAssert.AreEqual(new[] { "first:42", "second:42" }, received);
    }

    /// <summary>Verifies deterministic wildcard expansion and removal of duplicate normalized input paths.</summary>
    [TestMethod]
    public void WildcardsExpandInOrderAndKeepTheFirstOccurrenceOfEachPath()
    {
        File.WriteAllText(Path.Combine(_directory, "z.ges"), "module cli\non Last { emit Done() }");
        File.WriteAllText(Path.Combine(_directory, "a.ges"), "module cli\non First { emit Done() }");
        File.WriteAllText(Path.Combine(_directory, "ignored.txt"), "This is not a source file.");
        var nested = Directory.CreateDirectory(Path.Combine(_directory, "nested"));
        File.WriteAllText(Path.Combine(nested.FullName, "ignored.ges"), "Not recursively included.");
        AssertSuccess(Run("compile", "*.ges", "-o", "glob.gesb"));
        var program = GameEventScriptProgramReader.Read(File.ReadAllBytes(Path.Combine(_directory, "glob.gesb")));
        CollectionAssert.AreEqual(new[] { "a.ges", "z.ges" }, program.SourceArchive!.Sources.Select(source => source.SourceName).ToArray());
        AssertSuccess(Run("compile", "z.ges", "./z.ges", "*.ges", "-o", "mixed.gesb"));
        var mixed = GameEventScriptProgramReader.Read(File.ReadAllBytes(Path.Combine(_directory, "mixed.gesb")));
        CollectionAssert.AreEqual(new[] { "z.ges", "a.ges" }, mixed.SourceArchive!.Sources.Select(source => source.SourceName).ToArray());
    }

    /// <summary>Verifies that multi-source failures identify the failing input and preserve the existing output.</summary>
    /// <param name="failure">The failure in the second input file.</param>
    [TestMethod]
    [DataRow("parse")]
    [DataRow("encoding")]
    [DataRow("missing")]
    [DataRow("module")]
    public void FailureInALaterSourcePreservesTheOutput(string failure)
    {
        File.WriteAllText(Path.Combine(_directory, "first.ges"), Source);
        var second = Path.Combine(_directory, "second.ges");
        if (failure == "parse") File.WriteAllText(second, "on Start { emit Done(value: ) }");
        if (failure == "encoding") File.WriteAllBytes(second, [0xC3, 0x28]);
        if (failure == "module") File.WriteAllText(second, "module different\non Other { emit Done() }");
        var output = Path.Combine(_directory, "combined.gesb");
        byte[] original = [1, 2, 3];
        File.WriteAllBytes(output, original);
        var result = Run("compile", "first.ges", "second.ges", "-o", "combined.gesb", "-q");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        var code = failure switch
        {
            "parse" => GameEventScriptDiagnosticCodes.ParseSyntax,
            "encoding" => "cli.invalidEncoding",
            "module" => GameEventScriptDiagnosticCodes.ValidateInvalidIdentifierCase,
            _ => "cli.io"
        };
        StringAssert.Contains(result.StandardError, code);
        StringAssert.Contains(result.StandardError, "second.ges");
        CollectionAssert.AreEqual(original, File.ReadAllBytes(output));
    }

    /// <summary>Verifies wildcard and multi-source argument errors before any output is changed.</summary>
    [TestMethod]
    public void InvalidSourceSetsDoNotOverwriteInputsOrOutput()
    {
        File.WriteAllText(Path.Combine(_directory, "first.ges"), Source);
        File.WriteAllText(Path.Combine(_directory, "second.ges"), "module cli");
        var output = Path.Combine(_directory, "previous.gesb");
        File.WriteAllText(output, "previous");
        string[][] invalid =
        [
            ["compile", "*.ges"],
            ["compile", "first.ges", "second.ges", "-o", "./second.ges"],
            ["compile", "**/*.ges", "-o", "previous.gesb"],
            ["compile", "**.ges", "-o", "previous.gesb"]
        ];
        foreach (var arguments in invalid)
        {
            var result = Run(arguments);
            Assert.AreEqual(2, result.ExitCode, result.StandardError);
            StringAssert.Contains(result.StandardError, "cli.usage");
        }
        var noMatches = Run("compile", "missing*.ges", "-o", "previous.gesb");
        Assert.AreEqual(1, noMatches.ExitCode);
        StringAssert.Contains(noMatches.StandardError, "cli.io");
        Assert.AreEqual("previous", File.ReadAllText(output));
        Assert.AreEqual("module cli", File.ReadAllText(Path.Combine(_directory, "second.ges")));
        Assert.AreEqual(Source, File.ReadAllText(Path.Combine(_directory, "first.ges")));
    }

    private DotNetProcessResult Run(params string[] arguments) => ToolProcess.Execute(_directory, arguments);

    private static void AssertSuccess(DotNetProcessResult result)
    {
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

}
