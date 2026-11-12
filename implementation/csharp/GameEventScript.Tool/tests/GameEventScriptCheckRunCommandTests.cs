// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using GameEventScript.Api;
using GameEventScript.Tool;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies CLI checking, scenario loading, console output, and process exit codes using the existing compiler and host.</summary>
[TestClass]
public sealed class GameEventScriptCheckRunCommandTests
{
    private string _directory = null!;
    /// <summary>Ensures REPL diagnostics explain unknown types instead of repeating a diagnostic code.</summary>
    [TestMethod]
    public void InteractiveUnknownTypeHasReadableDiagnostic()
    {
        var result = ToolProcess.ExecuteWithInput(_directory, "let x be [1, 2, 3]; let y be :Data(input: x)\n:quit\n", "run", "--interactive", "--quiet");
        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.StandardError, "validate.invalidTypeConstructor");
        StringAssert.Contains(result.StandardError, "Unknown type constructor ':Data'");
        Assert.IsFalse(result.StandardError.Contains("validate.invalidTypeConstructor: validate.invalidTypeConstructor", StringComparison.Ordinal));
    }

    private const string ProgramSource = "module game\non Start(value) { emit ConsoleOut(value: value + 1) }\n";
    private const string ScenarioSource = "module scenario\non initialization { emit Start(value: 41) }\n";

    /// <summary>Checks that batch execution waits for chained delayed outputs and their final exit code.</summary>
    [TestMethod]
    public void RunWaitsForAllDelayedMessages()
    {
        Write("delayed.ges", "on Main(args) { emit after 0.01s Tick() }\non Tick { emit ConsoleOut('delayed'); emit after 0.01s ErrorCode(7) }");
        var result = Run("run", "delayed.ges", "--quiet");
        Assert.AreEqual(7, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("delayed"), result.StandardOutput);
    }

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
            var program = new GesProgramBuilder().WithDebugInfo(debug ? GesDebugInfoOptions.All : GesDebugInfoOptions.None).AddScript(ProgramSource).Compile();
            File.WriteAllBytes(Path.Combine(_directory, input), program.Encode());
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
        StringAssert.Matches(result.StandardError, new Regex(@"dispatch #\d+ Start\(value\)"));
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

    /// <summary>Verifies that a failed initial group reports its scheduled message as discarded in the verbose trace.</summary>
    [TestMethod]
    public void RunTracesDiscardedScheduledMessages()
    {
        Write("game.ges", "on initialization {\n  emit after 1s Tick()\n  for item in [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] { let doubled be item * 2 }\n}\non Tick {}\non Main(args) {}\n");
        var result = Run("run", "game.ges", "--verbose", "--max-steps", "10");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        StringAssert.Matches(result.StandardError, new Regex(@"emit #2 Tick\r?\nerror cli\.runtimeLimit: MaxExecutionSteps=10: [^\r\n]*\r?\ndiscard #2\r?\n"));
    }

    /// <summary>Verifies that the verbose trace reports delayed sends with their send ID and local deadline.</summary>
    [TestMethod]
    public void RunTracesScheduledMessages()
    {
        Write("game.ges", "on Main(args) { emit after 0.001s Later(); publish after 0.002s Out() }\non Later {}\n");
        var result = Run("run", "game.ges", "--verbose");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Matches(result.StandardError, new Regex(@"dispatch #1 Main\(args\) <- Main\(args: \[\]\)\r?\nschedule #2 emit Later at (\d{4}-\d{2}-\d{2} )?\d{2}:\d{2}:\d{2}\.\d{3}\r?\nemit #2 Later\r?\nschedule #3 publish Out at (\d{4}-\d{2}-\d{2} )?\d{2}:\d{2}:\d{2}\.\d{3}"));
    }

    /// <summary>Verifies that trace deadlines are shown as local wall-clock times, with the date when not today.</summary>
    [TestMethod]
    public void RunClockShowsDeadlinesAsLocalTimes()
    {
        var clock = new RunClock();
        StringAssert.Matches(clock.FormatDeadline(172_800_000_000), new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}$"));
        StringAssert.StartsWith(clock.FormatDeadline(long.MaxValue), "in ");
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

    /// <summary>Verifies run reports a missing extension once per call site, naming the extension at each location.</summary>
    [TestMethod]
    public void RunReportsEveryMissingExtensionCallSite()
    {
        Write("game.ges", "on initialization {\n    publish First(value: :probe.missing(1))\n    publish Second(value: :probe.missing(2))\n}\n");
        var run = Run("run", "game.ges");
        Assert.AreEqual(1, run.ExitCode);
        StringAssert.Contains(run.StandardError, "game.ges(2,26): error link.missingExtension: Unknown extension :probe.missing(_)");
        StringAssert.Contains(run.StandardError, "game.ges(3,27): error link.missingExtension: Unknown extension :probe.missing(_)");
        Assert.AreEqual(string.Empty, run.StandardOutput);
    }

    /// <summary>Verifies that schema formatting keeps a union on one line while it fits and otherwise breaks it before each
    /// further alternative.</summary>
    [TestMethod]
    public void SchemaFormatBreaksLongUnions()
    {
        Write("union.gess", "schema :Unit as {\n  shape: point: :Point(m) { x between 0m and 2000m step 0.1m, y between 0m and 2000m step 0.1m }\n    | box: :Box(m) { x between 0m and 2000m step 0.1m }\n    // a comment\n    | id: :Text max 64 optional\n  name: :Text max 8 |\n    :Number\n}\n");
        var format = Run("schema", "format", "union.gess");
        Assert.AreEqual(0, format.ExitCode, format.StandardError);
        Assert.AreEqual("schema :Unit as {\n  shape: point: :Point(m) {\n      x between 0m and 2000m step 0.1m\n      y between 0m and 2000m step 0.1m\n    }\n    | box: :Box(m) {\n      x between 0m and 2000m step 0.1m\n    }\n    | id: :Text max 64 optional\n  name: :Text max 8 | :Number\n}\n", format.StandardOutput.Replace("\r\n", "\n"));
    }

    /// <summary>Verifies the schema layout command: several roots, all roots, parameters, output files and usage errors.</summary>
    [TestMethod]
    public void SchemaLayoutCoversSeveralRoots()
    {
        Write("nested.gess", "parameter width: :Quantity(m)\nschema :Inner as {\n  x: :Quantity(m) between 0m and width step 1m\n}\nschema :Outer as {\n  inner: :Inner\n}\nschema :Other as {\n  inner: :Inner\n}\n");
        var layout = Run("schema", "layout", "nested.gess", "--root", "Outer", "--root", "Other", "--param", "width=8m");
        Assert.AreEqual(0, layout.ExitCode, layout.StandardError);
        StringAssert.StartsWith(layout.StandardOutput, "{\n  \"layout\": 1,\n  \"roots\": [\n    \"Outer\",\n    \"Other\"\n  ],");
        Assert.HasCount(2, layout.StandardOutput.Split("\"name\": \"Inner\",\n      \"kind\""));
        Assert.AreEqual(layout.StandardOutput.Replace("\"Outer\",\n    \"Other\"", "\"Inner\",\n    \"Outer\",\n    \"Other\"", StringComparison.Ordinal),
            Run("schema", "layout", "nested.gess", "--all-roots", "--param", "width=8m").StandardOutput);
        Assert.AreEqual(0, Run("schema", "layout", "nested.gess", "--root", "Outer", "--param", "width=8m", "-o", "layout.json").ExitCode);
        Assert.IsTrue(File.Exists(Path.Combine(_directory, "layout.json")));
        Assert.AreEqual(2, Run("schema", "layout", "nested.gess", "--param", "width=8m").ExitCode);
        Assert.AreEqual(2, Run("schema", "layout", "nested.gess", "--root", "Outer", "--all-roots", "--param", "width=8m").ExitCode);
        Assert.AreEqual(2, Run("schema", "layout", "nested.gess", "--root", "Outer", "--root", "Outer", "--param", "width=8m").ExitCode);
        Assert.AreEqual(2, Run("schema", "hash", "nested.gess", "--all-roots").ExitCode);
        var unknown = Run("schema", "layout", "nested.gess", "--root", "Outer", "--param", "width=8m", "--param", "height=1m");
        Assert.AreEqual(1, unknown.ExitCode);
        StringAssert.Contains(unknown.StandardError, "error schema.unknownName: No root reaches the parameter 'height'.");
        var missing = Run("schema", "layout", "nested.gess", "--root", "Outer");
        Assert.AreEqual(1, missing.ExitCode);
        StringAssert.Contains(missing.StandardError, "error schema.unknownName");
    }

    /// <summary>Verifies the schema generate command: both languages, output files, the check mode and errors.</summary>
    [TestMethod]
    public void SchemaGenerateWritesAndChecksCode()
    {
        Write("drone.gess", "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  on: :Boolean\n}\n");
        var csharp = Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "csharp", "--namespace", "Game.Net");
        Assert.AreEqual(0, csharp.ExitCode, csharp.StandardError);
        StringAssert.Contains(csharp.StandardOutput, "public partial struct Drone");
        var swift = Run("schema", "generate", "drone.gess", "--all-roots", "--language", "swift", "--access", "internal");
        StringAssert.Contains(swift.StandardOutput, "struct Drone: Sendable {");
        Assert.IsFalse(swift.StandardOutput.Contains("public ", StringComparison.Ordinal));
        Assert.AreEqual(1, Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "csharp", "-o", "Drone.g.cs", "--check").ExitCode);
        Assert.AreEqual(0, Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "csharp", "-o", "Drone.g.cs").ExitCode);
        Assert.AreEqual(0, Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "csharp", "-o", "Drone.g.cs", "--check").ExitCode);
        File.AppendAllText(Path.Combine(_directory, "Drone.g.cs"), "// edited\n");
        var outdated = Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "csharp", "-o", "Drone.g.cs", "--check");
        Assert.AreEqual(1, outdated.ExitCode);
        StringAssert.Contains(outdated.StandardError, "error cli.outdated: Drone.g.cs differs from the generated code.");
        Assert.AreEqual(2, Run("schema", "generate", "drone.gess", "--root", "Drone").ExitCode);
        Assert.AreEqual(2, Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "java").ExitCode);
        Assert.AreEqual(2, Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "swift", "--check").ExitCode);
        Assert.AreEqual(2, Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "swift", "--access", "private").ExitCode);
        Assert.AreEqual(2, Run("schema", "layout", "drone.gess", "--root", "Drone", "--language", "swift").ExitCode);
        StringAssert.Contains(Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "csharp", "--values").StandardOutput, "ToValue()");
        Assert.AreEqual(2, Run("schema", "layout", "drone.gess", "--root", "Drone", "--values").ExitCode);
        Assert.AreEqual(0, Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "swift", "--checked-exclusivity").ExitCode);
        Assert.AreEqual(2, Run("schema", "layout", "drone.gess", "--root", "Drone", "--checked-exclusivity").ExitCode);
        Write("extra.gess", "schema :Extra as {\n  on: :Boolean\n}\n");
        Assert.AreEqual(2, Run("schema", "generate", "drone.gess", "extra.gess", "--all-roots", "--language", "csharp", "-o", "extra.gess").ExitCode);
        Assert.AreEqual("schema :Extra as {\n  on: :Boolean\n}\n", File.ReadAllText(Path.Combine(_directory, "extra.gess")));
        var invalid = Run("schema", "generate", "drone.gess", "--root", "Drone", "--language", "swift", "--namespace", "1x");
        Assert.AreEqual(1, invalid.ExitCode);
        StringAssert.Contains(invalid.StandardError, "error generate.invalidNamespace");
    }

    /// <summary>Verifies schema generate into a directory: the split, the manifest, removed and foreign files, and the check.</summary>
    [TestMethod]
    public void SchemaGenerateOwnsItsOutputDirectory()
    {
        Write("s.gess", "model schema :Alpha as { a: :Number }\nmodel schema :Beta as { b: :Text }\n");
        var write = Run("schema", "generate", "s.gess", "--model", "default", "--language", "csharp", "--namespace", "Game", "-d", "out");
        Assert.AreEqual(0, write.ExitCode, write.StandardError);
        var output = Path.Combine(_directory, "out");
        CollectionAssert.AreEqual(new[] { ".ges-generated.json", "Game.Alpha.g.cs", "Game.Beta.g.cs", "_Game.g.cs" }, Directory.GetFiles(output).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
        StringAssert.Contains(File.ReadAllText(Path.Combine(output, ".ges-generated.json")), "\"code\": \"model\"");
        Assert.AreEqual(0, Run("schema", "generate", "s.gess", "--model", "default", "--language", "csharp", "--namespace", "Game", "-d", "out", "--check").ExitCode);
        var stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(output, "Game.Alpha.g.cs"), stamp);
        File.WriteAllText(Path.Combine(output, "Mine.cs"), "// mine\n");
        File.WriteAllText(Path.Combine(output, "Game.Alpha.g.cs.meta"), "unity\n");
        Write("s.gess", "model schema :Alpha as { a: :Number }\n");
        var rewrite = Run("schema", "generate", "s.gess", "--model", "default", "--language", "csharp", "--namespace", "Game", "--out-dir", "out", "--split", "schema");
        Assert.AreEqual(0, rewrite.ExitCode, rewrite.StandardError);
        StringAssert.Contains(rewrite.StandardError, "warning cli.unowned: " + Path.Combine("out", "Mine.cs") + " is not generated and was left alone.");
        Assert.IsFalse(rewrite.StandardError.Contains(".meta", StringComparison.Ordinal));
        Assert.IsFalse(File.Exists(Path.Combine(output, "Game.Beta.g.cs")));
        Assert.AreEqual(stamp, File.GetLastWriteTimeUtc(Path.Combine(output, "Game.Alpha.g.cs")));
        File.AppendAllText(Path.Combine(output, "Game.Alpha.g.cs"), "// edited\n");
        File.Delete(Path.Combine(output, "_Game.g.cs"));
        var check = Run("schema", "generate", "s.gess", "--model", "default", "--language", "csharp", "--namespace", "Game", "-d", "out", "--check");
        Assert.AreEqual(1, check.ExitCode);
        StringAssert.Contains(check.StandardError, "error cli.outdated: " + Path.Combine("out", "Game.Alpha.g.cs") + " differs from the generated code.");
        StringAssert.Contains(check.StandardError, "error cli.outdated: " + Path.Combine("out", "_Game.g.cs") + " does not exist.");
        StringAssert.Contains(check.StandardError, "error cli.outdated: " + Path.Combine("out", "Mine.cs") + " is not generated.");
        var file = Run("schema", "generate", "s.gess", "--model", "default", "--language", "swift", "-d", "files", "--split", "file");
        Assert.AreEqual(0, file.ExitCode, file.StandardError);
        CollectionAssert.AreEqual(new[] { ".ges-generated.json", "s.swift" }, Directory.GetFiles(Path.Combine(_directory, "files")).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
        var both = Run("schema", "generate", "s.gess", "--model", "default", "--language", "swift", "-o", "x.swift", "-d", "out");
        Assert.AreEqual(1, both.ExitCode);
        StringAssert.Contains(both.StandardError, "error generate.usage");
        var lone = Run("schema", "generate", "s.gess", "--model", "default", "--language", "swift", "--split", "file");
        Assert.AreEqual(1, lone.ExitCode);
        StringAssert.Contains(lone.StandardError, "error generate.usage");
        Assert.AreEqual(2, Run("schema", "generate", "s.gess", "--model", "default", "--language", "swift", "-d", "out", "--split", "type").ExitCode);
        Assert.AreEqual(2, Run("schema", "layout", "s.gess", "--model", "default", "-d", "out").ExitCode);
    }

    /// <summary>Verifies the wire and model groups of schema layout and generate, and their usage errors.</summary>
    [TestMethod]
    public void SchemaRolesSelectTheRootsOfAGroup()
    {
        Write("world.gess", "parameter w: :Quantity(m)\nwire(world) schema :World as {\n  at: :Point(m) { x between 0m and w step 1m }\n  badge: :Badge optional\n}\n");
        Write("panels.gess", "wire(panels) schema :Badge as { on: :Boolean }\nwire(panels) schema :Panel as { badge: :Badge }\n");
        Write("view.gess", "model(view) schema :View as {\n  name: :Text\n  at: :Point(m)\n}\n");
        var wire = Run("schema", "layout", "world.gess", "panels.gess", "view.gess", "--wire", "world", "--param", "w=10m");
        Assert.AreEqual(0, wire.ExitCode, wire.StandardError);
        StringAssert.StartsWith(wire.StandardOutput, "{\n  \"layout\": 1,\n  \"roots\": [\n    \"World\"\n  ],");
        StringAssert.Contains(wire.StandardOutput, "\"name\": \"Badge\"");
        Assert.IsFalse(wire.StandardOutput.Contains("\"Panel\"", StringComparison.Ordinal));
        var model = Run("schema", "layout", "world.gess", "panels.gess", "view.gess", "--model", "view");
        Assert.AreEqual(0, model.ExitCode, model.StandardError);
        StringAssert.StartsWith(model.StandardOutput, "{\n  \"model\": 1,\n  \"roots\": [\n    \"View\"\n  ],");
        var swift = Run("schema", "generate", "world.gess", "panels.gess", "view.gess", "--model", "view", "--language", "swift");
        StringAssert.Contains(swift.StandardOutput, "public struct View: Hashable, Sendable, Codable {");
        StringAssert.Contains(swift.StandardOutput, "public var at: GesPoint");
        var csharp = Run("schema", "generate", "world.gess", "panels.gess", "view.gess", "--wire", "world", "--param", "w=10m", "--language", "csharp");
        StringAssert.Contains(csharp.StandardOutput, "public GesPoint At;");
        var empty = Run("schema", "generate", "world.gess", "panels.gess", "--wire", "nothing", "--param", "w=10m", "--language", "csharp");
        Assert.AreEqual(1, empty.ExitCode);
        StringAssert.Contains(empty.StandardError, "error generate.usage: The files have no wire root in the group 'nothing'.");
        Assert.AreEqual(1, Run("schema", "layout", "view.gess", "--model", "view", "--param", "w=10m").ExitCode);
        Assert.AreEqual(2, Run("schema", "layout", "view.gess", "--model", "view", "--wire", "world").ExitCode);
        Assert.AreEqual(2, Run("schema", "layout", "view.gess", "--model", "view", "--root", "View").ExitCode);
        Assert.AreEqual(2, Run("schema", "generate", "view.gess", "--model", "view", "--language", "swift", "--checked-exclusivity").ExitCode);
        Assert.AreEqual(2, Run("schema", "hash", "view.gess", "--model", "view").ExitCode);
        Write("bad.gess", "wire schema :Bad as { name: :Text }\n");
        var check = Run("schema", "check", "bad.gess");
        Assert.AreEqual(1, check.ExitCode);
        StringAssert.Contains(check.StandardError, "bad.gess(1,29): error schema.notEncodable");
    }

    /// <summary>Verifies the schema commands: check, canonical form, hash, formatting, dump and error reports.</summary>
    [TestMethod]
    public void SchemaCommandsCheckHashFormatAndDump()
    {
        Write("drone.gess", "// note\nschema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between 0m and 2km step 1cm\n}\n");
        Assert.AreEqual(0, Run("schema", "check", "drone.gess", "--quiet").ExitCode);
        var canonical = Run("schema", "canonical", "drone.gess", "--root", "Drone");
        StringAssert.Contains(canonical.StandardOutput, "field x Quantity(meter) between 0e0 2e3 step 1e-2");
        Assert.AreEqual(16, Run("schema", "hash", "drone.gess", "--root", "Drone").StandardOutput.Trim().Length);
        Write("dice.gess", "schema :S as { d: :Dice max 2 between 1 and 6, e: :Dice, t: :Tag max 3, u: :Text max 1 | :Nothing }\n");
        Assert.AreEqual("schema :S as {\n  d: :Dice max 2 between 1 and 6\n  e: :Dice\n  t: :Tag max 3\n  u: :Text max 1 | :Nothing\n}\n", Run("schema", "format", "dice.gess").StandardOutput);
        var format = Run("schema", "format", "drone.gess");
        StringAssert.Contains(format.StandardOutput, "x: :Quantity(m) between 0m and 2km step 1cm");
        Assert.IsFalse(format.StandardOutput.Contains("// note", StringComparison.Ordinal));
        File.WriteAllBytes(Path.Combine(_directory, "data.bin"), [7, 0xE8, 3, 0]);
        var dump = Run("schema", "dump", "drone.gess", "--root", "Drone", "--data", "data.bin");
        StringAssert.Contains(dump.StandardOutput, "id bit=0 width=8 value=7");
        File.WriteAllBytes(Path.Combine(_directory, "delta.bin"), [7, 0]);
        var delta = Run("schema", "dump", "drone.gess", "--root", "Drone", "--data", "delta.bin", "--baseline", "data.bin");
        StringAssert.StartsWith(delta.StandardOutput, "delta bytes 2\nid bit=0 width=8 value=7");
        Assert.AreEqual(2, Run("schema", "hash", "drone.gess", "--root", "Drone", "--baseline", "data.bin").ExitCode);
        var same = Run("schema", "format", "drone.gess", "-o", "drone.gess");
        Assert.AreEqual(2, same.ExitCode);
        Assert.AreEqual(2, Run("schema", "format", "drone.gess", "-o", "").ExitCode);
        Assert.AreEqual(2, Run("schema", "format", "", "-o", "out.gess").ExitCode);
        Assert.AreEqual(2, Run("schema", "check", "").ExitCode);
        StringAssert.Contains(File.ReadAllText(Path.Combine(_directory, "drone.gess")), "// note");
        Assert.AreEqual(2, Run("schema", "check", "drone.gess", "--root", "Drone").ExitCode);
        Assert.AreEqual(2, Run("schema", "canonical", "drone.gess", "--root", "Drone", "--param", "a=1").ExitCode);
        Assert.AreEqual(2, Run("schema", "hash", "drone.gess", "--root", "Drone", "--data", "data.bin").ExitCode);
        Assert.AreEqual(2, Run("schema", "format", "drone.gess", "--root", "Drone").ExitCode);
        var unknown = Run("schema", "hash", "drone.gess", "--root", "Drone", "--param", "wrong=1");
        Assert.AreEqual(1, unknown.ExitCode);
        StringAssert.Contains(unknown.StandardError, "error schema.unknownName: The root 'Drone' has no parameter 'wrong'.");
        Write("nested.gess", "parameter width: :Quantity(m)\nschema :Inner as {\n  x: :Quantity(m) between 0m and width step 1m\n}\nschema :Outer as {\n  inner: :Inner\n}\n");
        Assert.AreEqual(0, Run("schema", "hash", "nested.gess", "--root", "Outer", "--param", "width=8m").ExitCode);
        var twice = Run("schema", "hash", "nested.gess", "--root", "Outer", "--param", "width=8m", "--param", "width=9m");
        Assert.AreEqual(2, twice.ExitCode);
        StringAssert.Contains(twice.StandardError, "The parameter 'width' is given twice.");
        Assert.AreEqual(1, Run("schema", "dump", "nested.gess", "--root", "Outer", "--param", "width=8m", "--param", "widht=9m", "--data", "data.bin").ExitCode);
        Write("broken.gess", "schema :A as {\n  a: :Quantity(kN)\n}\n");
        var broken = Run("schema", "check", "broken.gess");
        Assert.AreEqual(1, broken.ExitCode);
        StringAssert.Contains(broken.StandardError, "broken.gess(2,16): error schema.syntax");
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
