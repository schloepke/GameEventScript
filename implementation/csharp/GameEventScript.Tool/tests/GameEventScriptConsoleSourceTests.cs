// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using GameEventScript.Api;
using GameEventScript.Tool;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies embedded-source inspection and CLI-only GES/GESA syntax coloring.</summary>
[TestClass]
// Color assertions must not compete with parallel process tests for the highlighter's wall-clock safety budget.
[DoNotParallelize]
public sealed class GameEventScriptConsoleSourceTests
{
    private string _directory = null!;

    /// <summary>Creates an isolated CLI workspace.</summary>
    [TestInitialize]
    public void CreateWorkspace()
    {
        _directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "tool-source", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Removes the isolated workspace.</summary>
    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_directory, true);

    /// <summary>Verifies source-only output preserves multiple documents, comments, Unicode, and original line endings.</summary>
    /// <param name="binary">Whether sources are loaded from an archive without files on disk.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SourceDisplaysAllDocumentsWithoutOtherSegments(bool binary)
    {
        const string first = "module source.demo\r\n// First document\r\non Start {\r\n\temit ConsoleOut('Grüße 😀')\r\n}\r\n";
        const string second = "// Second document has no terminal newline\non Next { emit ConsoleOut(42) }";
        string[] paths;
        if (binary)
        {
            var program = GameEventScriptBuilder.Create().AddScript(first, "first.ges").AddScript(second, "second.ges").Compile();
            WriteBinary("game.gesb", program);
            paths = ["game.gesb"];
        }
        else
        {
            Write("first.ges", first);
            Write("second.ges", second);
            paths = ["first.ges", "second.ges"];
        }
        var result = Interactive(":source source.demo\nemit Start\nemit Next\n:quit\n", paths);
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("Grüße 😀", "42"), result.StandardOutput);
        Assert.AreEqual(Environment.NewLine + Lines("// Source: \"first.ges\"") + first + Environment.NewLine + Lines("// Source: \"second.ges\"", second, ""), result.StandardError);
        Assert.HasCount(paths.Length, Directory.GetFiles(_directory));
    }

    /// <summary>Verifies a binary without source metadata reports availability without changing the exit code or stopping execution.</summary>
    [TestMethod]
    public void MissingArchiveIsInformational()
    {
        var program = GameEventScriptBuilder.Create().AddScript("module stripped\non Start { emit ConsoleOut(42) }").WithDebugInfo(GameEventScriptDebugInfoOptions.None).Compile();
        WriteBinary("game.gesb", program);
        var result = Interactive(":source stripped\nemit Start\n:quit\n", "game.gesb");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "No embedded sources available for @1 (stripped)");
        Assert.IsFalse(result.StandardError.Contains("on Start", StringComparison.Ordinal));
    }

    /// <summary>Verifies :source can select a later persistent load and rejects ambiguous names without losing the session.</summary>
    [TestMethod]
    public void DuplicateModulesRequireAnId()
    {
        Write("first.ges", "module repeat\non First { emit ConsoleOut('first') }");
        const string second = "module repeat\non Second { emit ConsoleOut('second') }";
        Write("second.ges", second);
        var result = Interactive(":load second.ges\n:source repeat\n:source @2\nemit First\nemit Second\n:quit\n", "first.ges");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("first", "second"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "Use :source with one of: @1, @2");
        StringAssert.Contains(result.StandardError, second);
        Assert.IsFalse(result.StandardError.Contains("on First", StringComparison.Ordinal));
    }

    /// <summary>Verifies malformed selectors fail recoverably before displaying any source.</summary>
    /// <param name="command">The invalid source-inspection command.</param>
    [TestMethod]
    [DataRow(":source")]
    [DataRow(":source missing")]
    [DataRow(":source @0")]
    [DataRow(":source @invalid")]
    [DataRow(":source @999999999999999")]
    [DataRow(":source @1")]
    [DataRow(":source one two")]
    public void InvalidSelectorsAllowFurtherInput(string command)
    {
        var result = Interactive(command + "\nemit ConsoleOut(42)\n:quit\n");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "cli.consoleCommand");
        Assert.IsFalse(result.StandardError.Contains("// Source:", StringComparison.Ordinal));
    }

    /// <summary>Verifies both inspection commands preserve output exactly and honor opt-in colors and NO_COLOR.</summary>
    /// <param name="command">The inspection command.</param>
    [TestMethod]
    [DataRow(":source")]
    [DataRow(":dump")]
    public void ColorsAreOptionalAndPreserveText(string command)
    {
        const string source = "module colors\non Start {\n\temit ConsoleOut(\"Grüße 😀\", 12)\n}\n";
        WriteBinary("game.gesb", GameEventScriptBuilder.Create().AddScript(source, "color.ges").Compile());
        var input = command + " @1\n:quit\n";
        var plain = Interactive(input, "game.gesb");
        var colored = Interactive(input, "game.gesb", "--color");
        var disabled = ToolProcess.ExecuteWithInputAndNoColor(_directory, input, "run", "--interactive", "--quiet", "--color", "game.gesb");
        foreach (var result in new[] { plain, colored, disabled })
        {
            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            Assert.AreEqual(string.Empty, result.StandardOutput);
        }
        Assert.DoesNotContain("\u001b", plain.StandardError);
        Assert.AreEqual(plain.StandardError, WithoutColor(colored.StandardError));
        Assert.AreEqual(plain.StandardError, disabled.StandardError);
        StringAssert.Contains(colored.StandardError, "\u001b[35memit\u001b[0m");
        StringAssert.Contains(colored.StandardError, "\u001b[32m\"Grüße 😀\"\u001b[0m");
        StringAssert.Contains(colored.StandardError, "\u001b[34m12\u001b[0m");
        if (command == ":dump")
        {
            StringAssert.Contains(colored.StandardError, "\u001b[35m.gesb\u001b[0m");
            StringAssert.Contains(colored.StandardError, "\u001b[35mEmitMessage\u001b[0m");
            StringAssert.Contains(colored.StandardError, "\u001b[35m.source-line\u001b[0m");
        }
    }

    /// <summary>Verifies GESA escapes and GES multiline/doubled-quote strings cannot corrupt text or confuse embedded-source boundaries.</summary>
    [TestMethod]
    public void EmbeddedSyntaxKeepsLiteralBoundaries()
    {
        const string source = """
module colors
on Start {
    emit ConsoleOut('Grüße 😀 ''quoted''
.region-end "not a GESA boundary"
.segment code
.source-line "also literal text" 1 | ReturnVoid
still inside the string')
    emit ConsoleOut(12)
}
""";
        var program = GameEventScriptBuilder.Create().AddScript(source, "escaped\"name\\file.ges").Compile();
        var dump = program.Dump();
        var colored = RunHighlighting.RenderAssembly(dump);
        Assert.AreEqual(dump, WithoutColor(colored));
        StringAssert.Contains(colored, "\u001b[35mReturnVoid\u001b[0m");
        StringAssert.Contains(colored, "\u001b[32m'Grüße 😀 ''quoted''\n.region-end \"not a GESA boundary\"\n.segment code\n.source-line \"also literal text\" 1 | ReturnVoid\nstill inside the string'\u001b[0m");
        StringAssert.Contains(colored, "\u001b[32m\"escaped\\\"name\\\\file.ges\"\u001b[0m");
        StringAssert.Contains(colored, "\u001b[35memit\u001b[0m");
    }

    /// <summary>Verifies source-line annotations return to assembler highlighting on the next line.</summary>
    [TestMethod]
    public void SourceLineReturnsToAssemblerSyntax()
    {
        const string text = ".source-line \"file.ges\" 3 | emit ConsoleOut('r2 // quoted', 12) // r3\r\nL_0: LoadInteger r1, #42\r\n";
        var colored = RunHighlighting.RenderAssembly(text);
        Assert.AreEqual(text, WithoutColor(colored));
        StringAssert.Contains(colored, "\u001b[32m'r2 // quoted'\u001b[0m");
        StringAssert.Contains(colored, "\u001b[90m// r3\u001b[0m");
        StringAssert.Contains(colored, "\u001b[35mLoadInteger\u001b[0m");
        StringAssert.Contains(colored, "\u001b[33mr1\u001b[0m");
        StringAssert.Contains(colored, "\u001b[34m#42\u001b[0m");
    }

    private void Write(string file, string source) => File.WriteAllText(Path.Combine(_directory, file), source, new UTF8Encoding(false, true));
    private void WriteBinary(string file, GameEventScriptProgram program) => File.WriteAllBytes(Path.Combine(_directory, file), GameEventScriptProgramWriter.ToArray(program));
    private DotNetProcessResult Interactive(string input, params string[] paths) => ToolProcess.ExecuteWithInput(_directory, input, ["run", "--interactive", "--quiet", .. paths]);
    private static string Lines(params string[] values) => string.Join(Environment.NewLine, values) + Environment.NewLine;
    private static string WithoutColor(string text) => Regex.Replace(text, "\u001b\\[[0-9;]*m", string.Empty);
}
