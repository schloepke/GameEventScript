// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies CLI checking, scenario loading, console output, and process exit codes using the existing compiler and host.</summary>
[TestClass]
public sealed class GameEventScriptCheckRunCommandTests
{
    private string _directory = null!;
    private const string ProgramSource = "module game\non Start(value) { emit ConsoleOut(value: value + 1) }\n";
    private const string ScenarioSource = "module scenario\non initialization { emit Start(value: 41) }\n";

    /// <summary>Creates an isolated CLI workspace under artifacts.</summary>
    [TestInitialize]
    public void CreateWorkspace()
    {
        _directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "tool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Removes the isolated CLI workspace.</summary>
    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_directory, true);

    /// <summary>Verifies source aggregation, wildcard deduplication, reporting, and absence of execution or artifact writes.</summary>
    /// <param name="option">The output verbosity option.</param>
    [TestMethod]
    [DataRow("--quiet")]
    [DataRow("--verbose")]
    public void CheckCompilesSourcesWithoutWritingOrExecuting(string option)
    {
        Write("a.ges", "module check\non initialization { publish MustNotRun(value: $answer) }\n");
        Write("b.ges", "module check\nconstant $answer be 42\n");
        var previous = new byte[] { 1, 2, 3 };
        File.WriteAllBytes(Path.Combine(_directory, "a.gesb"), previous);
        var result = Run("check", "*.ges", "a.ges", option);
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardError);
        CollectionAssert.AreEqual(previous, File.ReadAllBytes(Path.Combine(_directory, "a.gesb")));
        Assert.HasCount(3, Directory.GetFiles(_directory));
        Assert.IsFalse(result.StandardOutput.Contains("publish MustNotRun", StringComparison.Ordinal));
        if (option == "--quiet") Assert.AreEqual(string.Empty, result.StandardOutput);
        else
        {
            StringAssert.Contains(result.StandardOutput, "Checked successfully.");
            StringAssert.Contains(result.StandardOutput, "Sources (2):");
            StringAssert.Contains(result.StandardOutput, "Required registers:");
        }
    }

    /// <summary>Verifies source and precompiled execution through a separately compiled scenario.</summary>
    /// <param name="binary">Whether the main program is precompiled.</param>
    /// <param name="debug">Whether a precompiled input includes debug metadata.</param>
    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DataRow(true, false)]
    public void RunScenarioProducesConsolePublications(bool binary, bool debug)
    {
        Write("game.ges", ProgramSource);
        Write("scenario.ges", ScenarioSource);
        var input = "game.ges";
        if (binary)
        {
            input = "game.gesb";
            var program = GameEventScriptBuilder.Create().WithDebugInfo(debug ? GameEventScriptDebugInfoOptions.All : GameEventScriptDebugInfoOptions.None).AddScript(ProgramSource).Compile();
            File.WriteAllBytes(Path.Combine(_directory, input), GameEventScriptProgramWriter.ToArray(program));
        }
        var original = File.ReadAllBytes(Path.Combine(_directory, input));
        var result = Run("run", input, "--scenario", "scenario.ges", "--seed", "42");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "Run completed:");
        CollectionAssert.AreEqual(original, File.ReadAllBytes(Path.Combine(_directory, input)));
        Assert.HasCount(binary ? 3 : 2, Directory.GetFiles(_directory));
    }

    /// <summary>Verifies that all programs are loaded before pumping and initialization follows load order.</summary>
    [TestMethod]
    public void RunLoadsScenarioHandlersBeforeMainInitialization()
    {
        Write("game.ges", "module game\non initialization { emit Ready }\n");
        Write("scenario.ges", "module scenario\non initialization { emit ConsoleOut(value: 'scenario ready') }\non Ready { emit ConsoleOut(value: 'observed') }\n");
        var result = Run("run", "game.ges", "--scenario", "scenario.ges", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardError);
        Assert.AreEqual(Lines("scenario ready", "observed"), result.StandardOutput);
    }

    /// <summary>Verifies wildcard source aggregation independently for the main program and scenario, including BOM input.</summary>
    [TestMethod]
    public void RunCompilesMainAndScenarioSourceGroupsInOrder()
    {
        Write("main-a.ges", "module game\non Start(value) { emit ConsoleOut(value: value + $offset) }\n");
        Write("main-b.ges", "module game\nconstant $offset be 1\n");
        Write("scenario-a.ges", "module scenario\non initialization { emit Start(value: $answer) }\n");
        File.WriteAllText(Path.Combine(_directory, "scenario-b.ges"), "module scenario\nconstant $answer be 41\n", new UTF8Encoding(true, true));
        var result = Run("run", "main-*.ges", "main-a.ges", "--scenario", "scenario-a.ges", "--scenario", "scenario-b.ges", "--verbose");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "dispatch Start(value)");
        Assert.HasCount(4, Directory.GetFiles(_directory));
    }

    /// <summary>Verifies that omitting a scenario requires a matching Main entry point.</summary>
    [TestMethod]
    public void RunWithoutScenarioRequiresMain()
    {
        Write("game.ges", ProgramSource);
        var result = Run("run", "game.ges");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        StringAssert.Contains(result.StandardError, "cli.missingMain");
    }

    /// <summary>Verifies seed parsing and repeatable output from independent CLI processes.</summary>
    [TestMethod]
    public void RunSeedReproducesRandomOutput()
    {
        Write("game.ges", "on Main(args) { emit ConsoleOut(value: random from 1 to 1000000) }\n");
        var first = Run("run", "game.ges", "--seed", "-9223372036854775808", "--quiet");
        var second = Run("run", "game.ges", "--seed", "-9223372036854775808", "--quiet");
        Assert.AreEqual(0, first.ExitCode, first.StandardError);
        Assert.AreEqual(0, second.ExitCode, second.StandardError);
        Assert.IsTrue(long.TryParse(first.StandardOutput.Trim(), out var rolled) && rolled is >= 1 and <= 1000000, first.StandardOutput);
        Assert.AreEqual(first.StandardOutput, second.StandardOutput);
        Assert.AreEqual(string.Empty, first.StandardError);
    }

    /// <summary>Verifies text escaping and visibility of unmatched emit attempts in the verbose trace only.</summary>
    [TestMethod]
    public void RunPrintsUnmatchedEmitsAndEscapesText()
    {
        Write("game.ges", "on Main(args) { emit Unhandled(text: \"hello\nworld\") }\n");
        var result = Run("run", "game.ges", "--verbose");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        StringAssert.Contains(result.StandardError, "emit Unhandled(text: \"hello\\nworld\") [not queued]");
    }

    /// <summary>Verifies bounded termination of an event cycle and per-handler execution limits.</summary>
    /// <param name="option">The runtime limit option.</param>
    /// <param name="value">The configured limit.</param>
    /// <param name="limitName">The expected runtime limit identity.</param>
    [TestMethod]
    [DataRow("--max-messages", "3", "MaxProcessedEventsPerRun")]
    [DataRow("--max-steps", "1", "MaxExecutionSteps")]
    public void RunConsoleOutsFailureAtRuntimeLimits(string option, string value, string limitName)
    {
        Write("game.ges", "on Main(args) { emit Tick }\non Tick { emit Tick }\n");
        var result = Run("run", "game.ges", option, value, "--quiet");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "cli.runtimeLimit");
        StringAssert.Contains(result.StandardError, limitName);
        Assert.IsFalse(result.StandardError.Contains("Run completed", StringComparison.Ordinal));
    }

    /// <summary>Verifies parser, validation, encoding, and file errors through both new commands.</summary>
    /// <param name="command">The CLI command.</param>
    /// <param name="failure">The input failure.</param>
    /// <param name="code">The expected stable diagnostic code.</param>
    [TestMethod]
    [DataRow("check", "parse", "parse.syntax")]
    [DataRow("run", "parse", "parse.syntax")]
    [DataRow("check", "validate", "validate.duplicateVariable")]
    [DataRow("run", "validate", "validate.duplicateVariable")]
    [DataRow("check", "encoding", "cli.invalidEncoding")]
    [DataRow("run", "encoding", "cli.invalidEncoding")]
    [DataRow("check", "missing", "cli.io")]
    [DataRow("run", "missing", "cli.io")]
    public void InputFailuresConsoleOutDiagnostics(string command, string failure, string code)
    {
        if (failure == "parse") Write("game.ges", "on Start { emit Done(value: ) }");
        if (failure == "validate") Write("game.ges", "on Start { let value be 1\n let value be 2 }");
        if (failure == "encoding") File.WriteAllBytes(Path.Combine(_directory, "game.ges"), [0xC3, 0x28]);
        var result = Run(command, "game.ges");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        StringAssert.Contains(result.StandardError, code);
        if (failure is "parse" or "validate") StringAssert.Contains(result.StandardError, "game.ges(");
        Assert.IsFalse(File.Exists(Path.Combine(_directory, "game.gesb")));
    }

    /// <summary>Verifies that failed scenario compilation prevents main-program execution.</summary>
    [TestMethod]
    public void InvalidScenarioPreventsAllExecution()
    {
        Write("game.ges", "on initialization { publish MustNotRun }\n");
        Write("scenario.ges", "on initialization { emit Broken(value: ) }\n");
        var result = Run("run", "game.ges", "--scenario", "scenario.ges");
        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        StringAssert.Contains(result.StandardError, "scenario.ges(");
        StringAssert.Contains(result.StandardError, "parse.syntax");
    }

    /// <summary>Verifies missing extension diagnostics at runtime linking, while check remains independent of a host.</summary>
    [TestMethod]
    public void CheckDoesNotLinkButRunReportsMissingExtensions()
    {
        Write("game.ges", "on initialization { publish Done(value: :probe.missing()) }\n");
        var check = Run("check", "game.ges", "--quiet");
        Assert.AreEqual(0, check.ExitCode, check.StandardError);
        var run = Run("run", "game.ges");
        Assert.AreEqual(1, run.ExitCode);
        StringAssert.Contains(run.StandardError, "link.");
        Assert.AreEqual(string.Empty, run.StandardOutput);
    }

    /// <summary>Verifies existing malformed-binary fixtures are rejected before execution.</summary>
    [TestMethod]
    public void RunReportsBinaryDecodeFailure()
    {
        var fixture = Path.Combine(TestRepositoryPaths.Root, "conformance", "fixtures", "GesbV1", "invalid-magic.gesb");
        File.Copy(fixture, Path.Combine(_directory, "game.gesb"));
        var result = Run("run", "game.gesb");
        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.StandardError, "decode.");
        StringAssert.Contains(result.StandardError, "game.gesb");
        Assert.AreEqual(string.Empty, result.StandardOutput);
    }

    /// <summary>Verifies command help and program paths starting with a dash.</summary>
    /// <param name="command">The CLI command.</param>
    [TestMethod]
    [DataRow("check")]
    [DataRow("run")]
    public void CommandsSupportHelpAndDashPaths(string command)
    {
        var help = Run(command, "--help");
        Assert.AreEqual(0, help.ExitCode, help.StandardError);
        StringAssert.Contains(help.StandardOutput, "dotnet ges " + command);
        Write("-game.ges", "on Main(args) { emit ConsoleOut(value: 42) }\n");
        var result = command == "run" ? Run(command, "./-game.ges") : Run(command, "--", "-game.ges");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
    }

    /// <summary>Verifies malformed CLI options and ambiguous program inputs fail before execution.</summary>
    [TestMethod]
    public void InvalidArgumentsConsoleOutUsageWithoutExecution()
    {
        Write("game.ges", "on initialization { publish MustNotRun }\n");
        string[][] invalid =
        [
            ["check"], ["check", "game.ges", "-o", "out.gesb"], ["check", "game.ges", "--no-debug"], ["check", "game.ges", "-q", "-v"],
            ["run"], ["run", "game.ges", "other.gesb"], ["run", "game.ges", "--unknown"],
            ["run", "game.ges", "--seed"], ["run", "game.ges", "--seed", "9223372036854775808"], ["run", "game.ges", "--seed", "1", "--seed", "2"],
            ["run", "game.ges", "--seed", "1.2"], ["run", "game.ges", "--max-messages", "0"], ["run", "game.ges", "--max-steps", "-1"],
            ["run", "game.ges", "--max-steps", "2147483648"], ["run", "game.ges", "--scenario"], ["run", "game.ges", "--scenario", ""],
            ["run", "game.ges", "--scenario", "--seed"], ["run", "game.ges", "--quiet", "--verbose"], ["run", "game.ges", "--arg"],
            ["run", "game.ges", "--interactive", "--scenario", "scenario.ges"], ["run", "game.ges", "--interactive", "--arg", "text"],
            ["run", "game.ges", "--scenario", "scenario.ges", "--arg", "text"],
            ["run", "game.ges", "--args"], ["run", "game.ges", "--args", "--color"], ["run", "game.ges", "--args", "text", "--colro"],
            ["run", "game.ges", "--interactive", "--args", "text"], ["run", "game.ges", "--scenario", "scenario.ges", "--args", "text"],
            ["run", "game.ges", "--interactive", "--", "text"], ["run", "game.ges", "--scenario", "scenario.ges", "--", "text"]
        ];
        foreach (var arguments in invalid)
        {
            var result = Run(arguments);
            Assert.AreEqual(2, result.ExitCode, string.Join(' ', arguments) + Environment.NewLine + result.StandardError);
            StringAssert.Contains(result.StandardError, "cli.usage");
            Assert.AreEqual(string.Empty, result.StandardOutput);
        }
        Assert.HasCount(1, Directory.GetFiles(_directory));
    }

    private void Write(string name, string source) => File.WriteAllText(Path.Combine(_directory, name), source, new UTF8Encoding(false, true));
    private DotNetProcessResult Run(params string[] arguments) => ToolProcess.Execute(_directory, arguments);
    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines) + Environment.NewLine;
}
