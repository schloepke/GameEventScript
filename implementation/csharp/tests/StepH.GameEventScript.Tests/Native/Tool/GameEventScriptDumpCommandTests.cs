// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Native.Tool;

/// <summary>Verifies binary loading, text output, and diagnostics through the dump CLI process.</summary>
[TestClass]
public sealed class GameEventScriptDumpCommandTests
{
    private const string Source = "module dumpdemo\non Start { emit Done(text: 'Grüße 😀') }\n";
    private string _directory = null!;

    /// <summary>Creates an isolated directory for binary inputs and text output.</summary>
    [TestInitialize]
    public void CreateWorkspace()
    {
        _directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "tool-dump", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Deletes the test's binary and dump files.</summary>
    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_directory, true);

    /// <summary>Verifies exact stdout output, optional addresses, and operation without original source files.</summary>
    /// <param name="debug">Whether the binary contains debug information.</param>
    /// <param name="addresses">Whether instruction addresses are requested.</param>
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DumpWritesOnlyGesaToStdout(bool debug, bool addresses)
    {
        var bytes = CreateBinary("program file.gesb", debug);
        var arguments = addresses ? new[] { "dump", "program file.gesb", "--addresses" } : ["dump", "program file.gesb"];
        var result = Run(arguments);
        AssertSuccess(result);
        Assert.AreEqual(GameEventScriptProgramReader.Read(bytes).Dump(addresses), result.StandardOutput);
        StringAssert.Contains(result.StandardOutput, "Grüße 😀");
        Assert.AreEqual(addresses, result.StandardOutput.Contains("@0000", StringComparison.Ordinal));
        Assert.AreEqual(debug, result.StandardOutput.Contains(".segment source", StringComparison.Ordinal));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(_directory, "program file.gesb")));
        Assert.HasCount(1, Directory.GetFiles(_directory));
    }

    /// <summary>Verifies UTF-8 without BOM, output directory creation, and replacement of a previous dump.</summary>
    /// <param name="option">The short or long output option.</param>
    [TestMethod]
    [DataRow("-o")]
    [DataRow("--output")]
    public void DumpWritesAndReplacesAnExplicitOutputFile(string option)
    {
        var bytes = CreateBinary("program.gesb", true);
        var output = Path.Combine(_directory, "nested output", "program.gesa");
        AssertSuccess(Run("dump", option, "nested output/program.gesa", "program.gesb"));
        var program = GameEventScriptProgramReader.Read(bytes);
        CollectionAssert.AreEqual(new UTF8Encoding(false, true).GetBytes(program.Dump()), File.ReadAllBytes(output));
        AssertSuccess(Run("dump", "program.gesb", "--addresses", option, "nested output/program.gesa"));
        CollectionAssert.AreEqual(new UTF8Encoding(false, true).GetBytes(program.Dump(true)), File.ReadAllBytes(output));
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(output)!, ".ges-*.tmp"));
    }

    /// <summary>Verifies that malformed corpus binaries report the Reader's code and context before producing output.</summary>
    /// <param name="fixture">The existing invalid binary fixture.</param>
    /// <param name="existingOutput">Whether an existing dump must be preserved.</param>
    [TestMethod]
    [DataRow("invalid-magic.gesb", false)]
    [DataRow("invalid-magic.gesb", true)]
    [DataRow("invalid-truncated-payload.gesb", false)]
    [DataRow("invalid-truncated-payload.gesb", true)]
    [DataRow("invalid-string-index.gesb", false)]
    [DataRow("invalid-string-index.gesb", true)]
    [DataRow("invalid-reserved-opcode-da.gesb", false)]
    [DataRow("invalid-reserved-opcode-da.gesb", true)]
    public void InvalidBinaryReportsStructuredContextWithoutWriting(string fixture, bool existingOutput)
    {
        var bytes = File.ReadAllBytes(Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "GesbV1", fixture));
        File.WriteAllBytes(Path.Combine(_directory, "invalid.gesb"), bytes);
        var expected = Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(bytes));
        var output = Path.Combine(_directory, "previous.gesa");
        if (existingOutput) File.WriteAllText(output, "previous");
        var result = existingOutput ? Run("dump", "invalid.gesb", "-o", "previous.gesa") : Run("dump", "invalid.gesb");
        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        StringAssert.Contains(result.StandardError, "invalid.gesb");
        StringAssert.Contains(result.StandardError, expected.Diagnostic.Code);
        if (expected.ByteOffset is { } offset) StringAssert.Contains(result.StandardError, $"byteOffset={offset}");
        if (expected.SectionType is { } section) StringAssert.Contains(result.StandardError, $"sectionType=0x{section:X4}");
        if (expected.EntryIndex is { } entry) StringAssert.Contains(result.StandardError, $"entryIndex={entry}");
        if (existingOutput) Assert.AreEqual("previous", File.ReadAllText(output));
        else Assert.IsFalse(File.Exists(output));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(_directory, "invalid.gesb")));
        Assert.IsEmpty(Directory.GetFiles(_directory, ".ges-*.tmp"));
    }

    /// <summary>Verifies usage validation and protects the input against being selected as output.</summary>
    [TestMethod]
    public void InvalidUsageLeavesTheInputUntouched()
    {
        var bytes = CreateBinary("program.gesb", false);
        string[][] invalid =
        [
            ["dump"],
            ["dump", ""],
            ["dump", "program.gesb", "second.gesb"],
            ["dump", "program.gesb", "--unknown"],
            ["dump", "program.gesb", "-o"],
            ["dump", "program.gesb", "-o", "--addresses"],
            ["dump", "program.gesb", "-o", ""],
            ["dump", "program.gesb", "-o", "one.gesa", "--output", "two.gesa"],
            ["dump", "program.gesb", "-o", "./program.gesb"]
        ];
        foreach (var arguments in invalid)
        {
            var result = Run(arguments);
            Assert.AreEqual(2, result.ExitCode, result.StandardError);
            Assert.AreEqual(string.Empty, result.StandardOutput);
            StringAssert.Contains(result.StandardError, "cli.usage");
        }
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(_directory, "program.gesb")));
        Assert.HasCount(1, Directory.GetFiles(_directory));
    }

    /// <summary>Verifies file I/O diagnostics and temporary-file cleanup on output failure.</summary>
    [TestMethod]
    public void IoFailuresAreReportedWithoutChangingExistingFiles()
    {
        var output = Path.Combine(_directory, "previous.gesa");
        File.WriteAllText(output, "previous");
        var missing = Run("dump", "missing.gesb", "-o", "previous.gesa");
        Assert.AreEqual(1, missing.ExitCode);
        Assert.AreEqual(string.Empty, missing.StandardOutput);
        StringAssert.Contains(missing.StandardError, "cli.io");
        Assert.AreEqual("previous", File.ReadAllText(output));
        CreateBinary("program.gesb", false);
        var targetDirectory = Directory.CreateDirectory(Path.Combine(_directory, "output.gesa"));
        var blocked = Run("dump", "program.gesb", "-o", "output.gesa");
        Assert.AreEqual(1, blocked.ExitCode);
        Assert.AreEqual(string.Empty, blocked.StandardOutput);
        StringAssert.Contains(blocked.StandardError, "cli.io");
        Assert.IsTrue(Directory.Exists(targetDirectory.FullName));
        Assert.IsEmpty(Directory.GetFiles(_directory, ".ges-*.tmp"));
    }

    /// <summary>Verifies literal file names after the option terminator.</summary>
    [TestMethod]
    public void EndOfOptionsAcceptsABinaryNameStartingWithADash()
    {
        var bytes = CreateBinary("-program.gesb", false);
        var result = Run("dump", "--", "-program.gesb");
        AssertSuccess(result);
        Assert.AreEqual(GameEventScriptProgramReader.Read(bytes).Dump(), result.StandardOutput);
    }

    private byte[] CreateBinary(string name, bool debug)
    {
        var program = GameEventScriptBuilder.Create().AddScript(Source, "unavailable-source.ges").WithDebugInfo(debug ? GameEventScriptDebugInfoOptions.All : GameEventScriptDebugInfoOptions.None).Compile();
        var bytes = GameEventScriptProgramWriter.ToArray(program);
        File.WriteAllBytes(Path.Combine(_directory, name), bytes);
        return bytes;
    }

    private ToolProcessResult Run(params string[] arguments) => ToolProcess.Execute(_directory, arguments);

    private static void AssertSuccess(ToolProcessResult result)
    {
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardError);
    }
}
