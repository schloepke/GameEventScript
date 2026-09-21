// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies CLI entry-point conventions, native console endpoints, binary composition, and interactive process I/O.</summary>
[TestClass]
public sealed class GameEventScriptRunEntryPointTests
{
    private string _directory = null!;

    /// <summary>Creates an isolated CLI workspace below artifacts.</summary>
    [TestInitialize]
    public void CreateWorkspace()
    {
        _directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "tool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Removes the isolated workspace.</summary>
    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_directory, true);

    /// <summary>Verifies ordered Text arguments, repeated results, raw root Text output, and separate console output.</summary>
    /// <param name="withArguments">Whether to supply Main arguments.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MainReceivesTextListAndCanPrintMultipleResults(bool withArguments)
    {
        Write("game.ges", "on Main(args) { emit ConsoleErr(text: 'working')\n emit ConsoleOut(value: args)\n emit ConsoleOut(value: 42)\n emit ConsoleOut(value: 'done') }\n");
        var arguments = new List<string> { "run", "game.ges", "--quiet" };
        if (withArguments) arguments.AddRange(["--arg", "hello", "--arg", "42", "--arg", ""]);
        var result = Run(arguments.ToArray());
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines(withArguments ? "[\"hello\", \"42\", \"\"]" : "[]", "42", "done"), result.StandardOutput);
        Assert.AreEqual(Lines("working"), result.StandardError);
    }

    /// <summary>Verifies option-looking Main arguments remain data.</summary>
    [TestMethod]
    public void MainArgumentsMayStartWithDashes()
    {
        Write("game.ges", "on Main(args) { emit ConsoleOut(value: args[1]) }\n");
        var result = Run("run", "game.ges", "--arg", "--interactive", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("--interactive"), result.StandardOutput);
    }

    /// <summary>Verifies initialization and all resulting messages finish before Main is sent.</summary>
    [TestMethod]
    public void MainWaitsForInitializationFollowupMessages()
    {
        Write("game.ges", "on initialization { emit Boot }\non Boot { emit ConsoleOut(value: 'boot') }\non Main(args) { emit ConsoleOut(value: 'main') }\n");
        var result = Run("run", "game.ges", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("boot", "main"), result.StandardOutput);
    }

    /// <summary>Verifies missing Main fails before initialization and cannot be hidden by undeliverable or tag-filtered handlers.</summary>
    /// <param name="handler">An ineligible entry point.</param>
    [TestMethod]
    [DataRow("on Main { emit ConsoleOut(value: 'wrong signature') }")]
    [DataRow("on Main(args) matching #ready { emit ConsoleOut(value: 'requires tag') }")]
    [DataRow("on undeliverable as message { emit ConsoleOut(value: 'fallback') }")]
    public void MissingMainDoesNotExecuteInitializationOrFallback(string handler)
    {
        Write("game.ges", "on initialization { emit ConsoleOut(value: 'must not run') }\n" + handler + "\n");
        var result = Run("run", "game.ges");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        StringAssert.Contains(result.StandardError, "cli.missingMain");
    }

    /// <summary>Verifies name-only Main handlers follow ordinary host matching.</summary>
    [TestMethod]
    public void MainSupportsMessageNameHandlers()
    {
        Write("game.ges", "on Main as message { emit ConsoleOut(value: 'matched') }\n");
        var result = Run("run", "game.ges", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("matched"), result.StandardOutput);
    }

    /// <summary>Verifies binary ordering, wildcard ordering, path deduplication, and one Main broadcast to all Programs.</summary>
    /// <param name="reverse">Whether to explicitly reverse the input order.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MultipleBinariesInitializeThenHandleOneMainInInputOrder(bool reverse)
    {
        var first = Binary("a.gesb", "module first\non initialization { emit ConsoleOut(value: 'initA') }\non Main(args) { emit ConsoleOut(value: 'mainA') }\n");
        var second = Binary("b.gesb", "module second\non initialization { emit ConsoleOut(value: 'initB') }\non Main(args) { emit ConsoleOut(value: 'mainB') }\n");
        var result = reverse ? Run("run", "b.gesb", "a.gesb", "--quiet") : Run("run", "*.gesb", "a.gesb", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(reverse ? Lines("initB", "initA", "mainB", "mainA") : Lines("initA", "initB", "mainA", "mainB"), result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
        CollectionAssert.AreEqual(first, File.ReadAllBytes(Path.Combine(_directory, "a.gesb")));
        CollectionAssert.AreEqual(second, File.ReadAllBytes(Path.Combine(_directory, "b.gesb")));
        Assert.HasCount(2, Directory.GetFiles(_directory));
    }

    /// <summary>Verifies all binary handlers are present before any initialization runs.</summary>
    [TestMethod]
    public void InitializationCanReachALaterBinary()
    {
        Binary("first.gesb", "module first\non initialization { emit Boot }\non Main(args) { emit Work(value: 41) }\n");
        Binary("second.gesb", "module second\non Boot { emit ConsoleOut(value: 'boot') }\non Work(value) { emit ConsoleOut(value: value + 1) }\n");
        var result = Run("run", "first.gesb", "second.gesb", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("boot", "42"), result.StandardOutput);
    }

    /// <summary>Verifies decoding or linking a later binary fails before earlier initialization can execute.</summary>
    /// <param name="failure">The later-program failure.</param>
    [TestMethod]
    [DataRow("decode")]
    [DataRow("link")]
    public void LaterBinaryFailurePreventsAllExecution(string failure)
    {
        Binary("first.gesb", "module first\non initialization { emit ConsoleOut(value: 'must not run') }\non Main(args) { emit ConsoleOut(value: 'main') }\n");
        if (failure == "decode") File.WriteAllBytes(Path.Combine(_directory, "second.gesb"), [0, 1, 2, 3]);
        else Binary("second.gesb", "module second\non Start { emit ConsoleOut(value: :probe.missing()) }\n");
        var result = Run("run", "first.gesb", "second.gesb");
        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        StringAssert.Contains(result.StandardError, failure + ".");
        if (failure == "decode") StringAssert.Contains(result.StandardError, "second.gesb");
    }

    /// <summary>Verifies a scenario can drive multiple binaries without an automatic Main message.</summary>
    [TestMethod]
    public void ScenarioReplacesMainForMultipleBinaries()
    {
        Binary("first.gesb", "module first\non Main(args) { emit ConsoleOut(value: 'must not run') }\non Start { emit Work(value: 41) }\n");
        Binary("second.gesb", "module second\non Work(value) { emit ConsoleOut(value: value + 1) }\n");
        Write("scenario.ges", "on initialization { emit Start }\n");
        var result = Run("run", "first.gesb", "second.gesb", "--scenario", "scenario.ges", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
    }

    /// <summary>Verifies verbose publication/dispatch output cannot contaminate ConsoleOut values.</summary>
    /// <param name="verbose">Whether to enable tracing.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PublishConsoleOutIsDeliveredOnceAndTraceStaysOnStderr(bool verbose)
    {
        Write("game.ges", "on Main(args) { publish ConsoleOut(value: 42)\n emit ConsoleErr(text: 'note') }\n");
        var result = Run("run", "game.ges", verbose ? "--verbose" : "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "note");
        if (verbose)
        {
            StringAssert.Contains(result.StandardError, "publish ConsoleOut(value: 42)");
            StringAssert.Contains(result.StandardError, "dispatch Main(args)");
        }
        else Assert.AreEqual(Lines("note"), result.StandardError);
    }

    /// <summary>Verifies native console contract failures retain their diagnostic and make the process fail.</summary>
    [TestMethod]
    public void InvalidErrorCodeReportsRuntimeFailure()
    {
        Write("game.ges", "on Main(args) { emit ErrorCode(code: 256)\n emit ConsoleOut(value: 'later result') }\n");
        var result = Run("run", "game.ges");
        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.StandardError, "cli.errorCodeArgument");
        StringAssert.Contains(result.StandardError, "handler=ErrorCode(*)");
        Assert.AreEqual(Lines("later result"), result.StandardOutput);
        Assert.IsFalse(result.StandardError.Contains("Run completed", StringComparison.Ordinal));
    }

    /// <summary>Verifies redirected interactive input, repeated commands, UTF-8, initialization, and explicit or EOF exit.</summary>
    /// <param name="quit">Whether to explicitly quit.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InteractiveConsoleProcessesCommandsWithoutSendingMain(bool quit)
    {
        Write("game.ges", "on initialization { emit ConsoleOut(value: 'ready') }\non Main(args) { emit ConsoleOut(value: 'must not run') }\non Start(value) { emit ConsoleOut(value: value) }\n");
        var input = "emit Start(value: 41)\nemit Start(value: 'Grüße 😀')\n" + (quit ? ":quit\nemit ConsoleOut(value: 'must not run')\n" : string.Empty);
        var result = Interactive(input, "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("ready", "41", "Grüße 😀"), result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    /// <summary>Verifies console help stays on stderr and an optional UTF-8 BOM is accepted.</summary>
    [TestMethod]
    public void InteractiveHelpAndConsoleOutputStayOffStdout()
    {
        Write("game.ges", "on Start { emit ConsoleOut(value: 42) }\n");
        var result = Interactive("\uFEFF:help\nemit ConsoleErr(text: 'note')\nemit Start\n", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, ":quit");
        StringAssert.Contains(result.StandardError, "note");
        Assert.IsFalse(result.StandardError.Contains("ges> ", StringComparison.Ordinal));
    }

    /// <summary>Verifies rejected input leaves the host usable and marks the session unsuccessful.</summary>
    /// <param name="input">The invalid interactive input.</param>
    /// <param name="code">The expected diagnostic family.</param>
    [TestMethod]
    [DataRow("emit ConsoleOut(value: )", "parse.syntax")]
    [DataRow("emit ConsoleOut(value: :probe.missing())", "link.")]
    [DataRow("}; on Extra { emit ConsoleOut(value: 1)", "cli.interactiveInput")]
    [DataRow("}; on initialization { emit ConsoleOut(value: 1)", "cli.interactiveInput")]
    public void InteractiveConsoleRecoversFromRejectedInput(string input, string code)
    {
        Write("game.ges", "on Start { emit ConsoleOut(value: 42) }\n");
        var result = Interactive(input + "\nemit Start\n", "--quiet");
        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.StandardError, code);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
    }

    /// <summary>Verifies safety limits terminate the console instead of resuming queued event cycles.</summary>
    [TestMethod]
    public void InteractiveConsoleStopsAtRuntimeLimit()
    {
        Write("game.ges", "on Tick { emit Tick }\n");
        var result = Interactive("emit Tick\nemit ConsoleOut(value: 'must not run')\n", "--max-messages", "3", "--quiet");
        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.StandardError, "cli.runtimeLimit");
        Assert.AreEqual(string.Empty, result.StandardOutput);
    }

    /// <summary>Verifies the console shares all loaded binary Programs and ignores blank or comment-only input.</summary>
    [TestMethod]
    public void InteractiveConsoleComposesMultipleBinaries()
    {
        Binary("first.gesb", "module first\non Start { emit Work(value: 41) }\n");
        Binary("second.gesb", "module second\non Work(value) { emit ConsoleOut(value: value + 1) }\n");
        var result = ToolProcess.ExecuteWithInput(_directory, "\n// comment\nemit Start\n", "run", "first.gesb", "second.gesb", "--interactive", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    /// <summary>Verifies variadic endpoints preserve value order and Text remains unquoted.</summary>
    [TestMethod]
    public void ConsoleEndpointsAcceptAnyArgumentCountAndValueKind()
    {
        Write("game.ges", """
            on Main(args) {
                emit ConsoleOut("Hello", 12, true, #ready, [1, "two"])
                emit ConsoleOut
                emit ConsoleErr("problem", 42, nothing)
                emit ConsoleErr
            }
            """);
        var result = Run("run", "game.ges", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("Hello12true#ready[1, \"two\"]", ""), result.StandardOutput);
        Assert.AreEqual(Lines("problem42nothing", ""), result.StandardError);
    }

    /// <summary>Verifies a numeric script exit code, including its reset, never stops queued output.</summary>
    /// <param name="code">The GES exit-code value.</param>
    /// <param name="expected">The process exit code.</param>
    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("1", 1)]
    [DataRow("7", 7)]
    [DataRow("255", 255)]
    [DataRow("7.0", 7)]
    [DataRow("nothing", 0)]
    public void ErrorCodeSetsProcessResultAfterDraining(string code, int expected)
    {
        Write("game.ges", $"on initialization {{ emit ErrorCode(code: 23) }}\non Main(args) {{ emit ErrorCode({code})\n emit ConsoleOut(42) }}\n");
        var result = Run("run", "game.ges", "--quiet");
        Assert.AreEqual(expected, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    /// <summary>Verifies invalid codes fail instead of rounding, wrapping, converting text, or silently resetting.</summary>
    /// <param name="arguments">Invalid ErrorCode arguments.</param>
    [TestMethod]
    [DataRow("-1")]
    [DataRow("256")]
    [DataRow("1.5")]
    [DataRow("'7'")]
    [DataRow("true")]
    [DataRow("7m")]
    [DataRow("7%")]
    [DataRow("infinity")]
    [DataRow("[]")]
    [DataRow("0, 1")]
    [DataRow("value: 0")]
    [DataRow("")]
    public void InvalidErrorCodeCannotBeHiddenBySubsequentReset(string arguments)
    {
        Write("game.ges", $"on Main(args) {{ emit ErrorCode({arguments})\n emit ErrorCode(code: nothing) }}\n");
        var result = Run("run", "game.ges", "--quiet");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "cli.errorCodeArgument");
    }

    /// <summary>Verifies the final delivered code wins across independently loaded Programs.</summary>
    [TestMethod]
    public void LastDeliveredErrorCodeWinsAcrossPrograms()
    {
        Binary("first.gesb", "module first\non Main(args) { emit ErrorCode(7) }\n");
        Binary("second.gesb", "module second\non Main(args) { emit ErrorCode(9) }\n");
        var result = Run("run", "first.gesb", "second.gesb", "--quiet");
        Assert.AreEqual(9, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    /// <summary>Verifies scenarios and interactive inputs can set or reset the same CLI-owned exit status.</summary>
    /// <param name="interactive">Whether to use interactive input instead of a scenario.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ErrorCodeWorksOutsideMain(bool interactive)
    {
        Write("game.ges", "on Start { emit ErrorCode(7) }\n");
        Write("scenario.ges", "on initialization { emit Start }\n");
        var result = interactive ? Interactive("emit Start\n:quit\n", "--quiet") : Run("run", "game.ges", "--scenario", "scenario.ges", "--quiet");
        Assert.AreEqual(7, result.ExitCode, result.StandardError);
    }

    /// <summary>Verifies a runtime limit overrides a script-selected status.</summary>
    [TestMethod]
    public void RuntimeLimitOverridesScriptExitCode()
    {
        Write("game.ges", "on Main(args) { emit ErrorCode(23)\n emit Tick }\non Tick { emit Tick }\n");
        var result = Run("run", "game.ges", "--quiet", "--max-messages", "5");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "cli.runtimeLimit");
    }

    /// <summary>Verifies rejected interactive input remains an error even after an explicit reset.</summary>
    [TestMethod]
    public void InteractiveErrorCannotBeClearedByScriptExitCode()
    {
        Write("game.ges", "on Start { emit ConsoleOut(42) }\n");
        var result = Interactive("emit ConsoleOut(\nemit ErrorCode(nothing)\nemit Start\n", "--quiet");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
    }

    /// <summary>Verifies opt-in color styles typed values, keeps root Text raw, and colors all ConsoleErr values red.</summary>
    /// <param name="interactive">Whether to use redirected interactive input.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitColorWorksForOutputIncludingRedirectedStreams(bool interactive)
    {
        const string statements = "emit ConsoleOut('Hello 12', 12)\n emit ConsoleErr('problem', 12)";
        Write("game.ges", "on Main(args) { " + statements + " }\n");
        var result = interactive ? Interactive(statements + "\n", "--quiet", "--color") : Run("run", "game.ges", "--quiet", "--color");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("Hello 12\u001b[34m12\u001b[0m"), result.StandardOutput);
        Assert.AreEqual(Lines("\u001b[31mproblem12\u001b[0m"), result.StandardError);
    }

    /// <summary>Verifies nested literal highlighting protects quoted text from numeric, keyword, and comment coloring.</summary>
    [TestMethod]
    public void ColoredNestedValuesPreserveLiteralText()
    {
        Write("game.ges", "on Main(args) { emit ConsoleOut([12, 'true // 42', true, #ready]) }\n");
        var result = Run("run", "game.ges", "--quiet", "--color");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardOutput, "\u001b[34m12\u001b[0m");
        StringAssert.Contains(result.StandardOutput, "\u001b[32m\"true // 42\"\u001b[0m");
        StringAssert.Contains(result.StandardOutput, "\u001b[35mtrue\u001b[0m");
        StringAssert.Contains(result.StandardOutput, "\u001b[36m#ready\u001b[0m");
    }

    /// <summary>Verifies NO_COLOR keeps output plain even when color was requested.</summary>
    [TestMethod]
    public void NoColorEnvironmentDisablesColor()
    {
        Write("game.ges", "on Main(args) { emit ConsoleOut(12)\n emit ConsoleErr('problem') }\n");
        var result = ToolProcess.ExecuteWithNoColor(_directory, "run", "game.ges", "--quiet", "--color");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("12"), result.StandardOutput);
        Assert.AreEqual(Lines("problem"), result.StandardError);
    }

    private void Write(string name, string source) => File.WriteAllText(Path.Combine(_directory, name), source, new UTF8Encoding(false, true));

    private byte[] Binary(string name, string source)
    {
        var program = GameEventScriptBuilder.Create().AddScript(source).Compile();
        var bytes = GameEventScriptProgramWriter.ToArray(program);
        File.WriteAllBytes(Path.Combine(_directory, name), bytes);
        return bytes;
    }

    private DotNetProcessResult Run(params string[] arguments) => ToolProcess.Execute(_directory, arguments);
    private DotNetProcessResult Interactive(string input, params string[] arguments) => ToolProcess.ExecuteWithInput(_directory, input, ["run", "game.ges", "--interactive", .. arguments]);
    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines) + Environment.NewLine;
}
