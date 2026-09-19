// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies interactive file loading, session recovery, and the CLI argument boundary.</summary>
[TestClass]
public sealed class GameEventScriptConsoleLoadTests
{
    private string _directory = null!;

    /// <summary>Creates an isolated CLI workspace.</summary>
    [TestInitialize]
    public void CreateWorkspace()
    {
        _directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "tool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Removes the isolated workspace.</summary>
    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_directory, true);

    /// <summary>Verifies the three argument forms preserve Text values and ordering around CLI options.</summary>
    /// <param name="form">The command-line syntax.</param>
    [TestMethod]
    [DataRow("single")]
    [DataRow("group")]
    [DataRow("rest")]
    [DataRow("mixed")]
    public void MainArgumentsRemainText(string form)
    {
        Write("game.ges", "on Main(args) { for arg in args { emit ConsoleOut(arg, ' ', arg is :Text) } }\n");
        string[] options = form switch
        {
            "single" => ["--arg", "12", "--arg", "Hello", "--arg", "34", "--arg", "", "--arg", "true", "--arg", "[1, 2]", "--quiet"],
            "group" => ["--args", "12", "Hello", "34", "", "true", "[1, 2]", "--quiet"],
            "rest" => ["--quiet", "--", "12", "Hello", "34", "", "true", "[1, 2]"],
            _ => ["--arg", "12", "--args", "Hello", "34", "--quiet", "--arg", "", "--", "true", "[1, 2]"]
        };
        var result = ToolProcess.Execute(_directory, ["run", "game.ges", .. options]);
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("12 true", "Hello true", "34 true", " true", "true true", "[1, 2] true"), result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    /// <summary>Verifies option-looking and file-looking values after -- are forwarded literally.</summary>
    [TestMethod]
    public void TerminatorForwardsEverythingToMain()
    {
        Write("game.ges", "on Main(args) { for arg in args { emit ConsoleOut(arg) } }\n");
        var result = ToolProcess.Execute(_directory, "run", "game.ges", "--quiet", "--", "--color", "--help", "--interactive", "--arg", "--args", "--", "missing.gesb");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("--color", "--help", "--interactive", "--arg", "--args", "--", "missing.gesb"), result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    /// <summary>Verifies grouped negative values remain data and the next option resumes CLI parsing.</summary>
    [TestMethod]
    public void GroupedArgumentsStopAtOptionsAndAllowNegativeValues()
    {
        Write("game.ges", "on Main(args) { emit Work(args: args) }\n");
        Write("helper.ges", "on Work(args) { for arg in args { emit ConsoleOut(arg) } }\n");
        var result = ToolProcess.Execute(_directory, "run", "game.ges", "--args", "-12", "-.5", "-1e3", "-", "--quiet", "helper.ges", "--args", "34", "--seed", "-1");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("-12", "-.5", "-1e3", "-", "34"), result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    /// <summary>Verifies an explicit empty remainder delivers an empty List and binary composition supports Main arguments.</summary>
    [TestMethod]
    public void BinariesReceiveTextArgumentsAndEmptyRemainders()
    {
        Binary("main.gesb", "module main\non Main(args) { emit Work(args: args) }\n");
        Binary("helper.gesb", "module helper\non Work(args) { emit ConsoleOut(args) }\n");
        var empty = ToolProcess.Execute(_directory, "run", "main.gesb", "helper.gesb", "--quiet", "--");
        Assert.AreEqual(0, empty.ExitCode, empty.StandardError);
        Assert.AreEqual(Lines("[]"), empty.StandardOutput);
        var values = ToolProcess.Execute(_directory, "run", "main.gesb", "helper.gesb", "--quiet", "--args", "12", "Hello");
        Assert.AreEqual(0, values.ExitCode, values.StandardError);
        Assert.AreEqual(Lines("[\"12\", \"Hello\"]"), values.StandardOutput);
    }

    /// <summary>Verifies a console can start without a file and load persistent handlers without invoking Main.</summary>
    /// <param name="binary">Whether to load a binary instead of source.</param>
    /// <param name="quiet">Whether load reports should be suppressed.</param>
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void EmptySessionLoadsAndInitializesPersistentPrograms(bool binary, bool quiet)
    {
        const string source = "on initialization { emit ConsoleOut('ready') }\non Main(args) { emit ConsoleOut('must not run') }\non Start { emit ConsoleOut(42) }\n";
        var name = binary ? "extra program.gesb" : "extra program.ges";
        if (binary) Binary(name, source);
        else Write(name, source);
        var result = Interactive($":load \"{name}\"\nemit Start\nemit Start\n:quit\n", quiet ? ["--quiet"] : []);
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("ready", "42", "42"), result.StandardOutput);
        Assert.AreEqual(!quiet, result.StandardError.Contains("Loaded " + name, StringComparison.Ordinal));
    }

    /// <summary>Verifies loading is additive and new initialization can communicate with previously loaded Programs.</summary>
    [TestMethod]
    public void LoadingAgainAddsAnIndependentInstance()
    {
        Write("main.ges", "on Boot { emit ConsoleOut('boot') }\n");
        Write("extra.ges", "on initialization { emit Boot }\non Start { emit ConsoleOut('extra') }\n");
        var result = Interactive(":load extra.ges\n:load extra.ges\nemit Start\n", "main.ges", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("boot", "boot", "extra", "extra"), result.StandardOutput);
    }

    /// <summary>Verifies failed loading leaves existing handlers available and rejects the failed Program before initialization.</summary>
    /// <param name="failure">The loading failure.</param>
    /// <param name="code">The expected diagnostic family.</param>
    [TestMethod]
    [DataRow("missing", "cli.io")]
    [DataRow("encoding", "cli.invalidEncoding")]
    [DataRow("parse", "parse.syntax")]
    [DataRow("decode", "decode.")]
    [DataRow("link", "link.")]
    public void LoadFailurePreservesTheSession(string failure, string code)
    {
        Write("main.ges", "on Start { emit ConsoleOut(42) }\n");
        var path = failure == "decode" ? "broken.gesb" : "broken.ges";
        if (failure == "parse") Write(path, "on initialization { emit ConsoleOut('must not run') }\non Bad(\n");
        if (failure == "link") Write(path, "on initialization { emit ConsoleOut('must not run') }\non Bad { emit ConsoleOut(:probe.missing()) }\n");
        if (failure is "encoding" or "decode") File.WriteAllBytes(Path.Combine(_directory, path), [0xFF, 0xFF]);
        var result = Interactive($":load {path}\nemit Start\n", "main.ges", "--quiet");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, code);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
    }

    /// <summary>Verifies runtime safety boundaries during loaded initialization stop the console.</summary>
    [TestMethod]
    public void LoadedInitializationLimitStopsTheSession()
    {
        Write("cycle.ges", "on initialization { emit Tick }\non Tick { emit Tick }\n");
        var result = Interactive(":load cycle.ges\nemit ConsoleOut('must not run')\n", "--quiet", "--max-messages", "4");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "cli.runtimeLimit");
        Assert.AreEqual(string.Empty, result.StandardOutput);
    }

    /// <summary>Verifies rejected console commands do not exit or change the running host.</summary>
    /// <param name="command">The malformed command.</param>
    [TestMethod]
    [DataRow(":load")]
    [DataRow(":load \"\"")]
    [DataRow(":load \"unfinished")]
    [DataRow(":quit now")]
    [DataRow(":unknown")]
    [DataRow(":help unknown")]
    public void InvalidConsoleCommandsAllowFurtherInput(string command)
    {
        var result = Interactive(command + "\nemit ConsoleOut(42)\n", "--quiet");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "cli.consoleCommand");
        Assert.AreEqual(Lines("42"), result.StandardOutput);
    }

    private void Write(string path, string source) => File.WriteAllText(Path.Combine(_directory, path), source, new UTF8Encoding(false, true));

    private void Binary(string path, string source)
        => File.WriteAllBytes(Path.Combine(_directory, path), GameEventScriptProgramWriter.ToArray(GameEventScriptBuilder.Create().AddScript(source).Compile()));

    private DotNetProcessResult Interactive(string input, params string[] options)
        => ToolProcess.ExecuteWithInput(_directory, input, ["run", "--interactive", .. options]);

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines) + Environment.NewLine;
}
