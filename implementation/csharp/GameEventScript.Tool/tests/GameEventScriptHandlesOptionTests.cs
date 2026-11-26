// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Tool;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies that <c>--handles</c> declares schema handle types to every CLI compilation without registering a resolver.</summary>
[TestClass]
public sealed class GameEventScriptHandlesOptionTests
{
    private const string Schema = "schema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\n";
    private const string Passing = "module pass\non Main(args) { emit Seen(target: nothing) }\non Seen(target as @Target) { emit ConsoleOut(target is @Target) }\n";
    private const string Reading = "module reads\non Main(args) { emit ConsoleOut('must not run') }\non Seen(target as @Target) { emit ConsoleOut(target.label) }\n";

    private string _directory = null!;

    /// <summary>Creates an isolated CLI workspace under artifacts.</summary>
    [TestInitialize]
    public void CreateWorkspace()
    {
        _directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "tool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Write("handles.gess", Schema);
        Write("pass.ges", Passing);
        Write("reads.ges", Reading);
    }

    /// <summary>Removes the isolated CLI workspace.</summary>
    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_directory, true);

    /// <summary>Checks that check and compile know handle types only with the option, and that dump lists the import.</summary>
    [TestMethod]
    public void CheckAndCompileUseDeclaredHandleTypes()
    {
        var unknown = Run("check", "pass.ges");
        Assert.AreEqual(1, unknown.ExitCode, unknown.StandardError);
        StringAssert.Contains(unknown.StandardError, "validate.unknownHandleType");

        var check = Run("check", "pass.ges", "--handles", "handles.gess", "--quiet");
        Assert.AreEqual(0, check.ExitCode, check.StandardError);

        var compile = Run("compile", "reads.ges", "--handles", "*.gess", "-o", "reads.gesb", "--quiet");
        Assert.AreEqual(0, compile.ExitCode, compile.StandardError);
        var dump = Run("dump", "reads.gesb");
        Assert.AreEqual(0, dump.ExitCode, dump.StandardError);
        StringAssert.Contains(dump.StandardOutput, "HandleType");
        StringAssert.Contains(dump.StandardOutput, "\"@Target\"");
    }

    /// <summary>Checks that a program passing handles runs and that one reading handle state fails to link without a resolver.</summary>
    [TestMethod]
    public void RunLinksOnlyProgramsWithoutHandleState()
    {
        var pass = Run("run", "pass.ges", "--handles", "handles.gess", "--quiet");
        Assert.AreEqual(0, pass.ExitCode, pass.StandardError);
        Assert.AreEqual(Lines("false"), pass.StandardOutput);

        var reads = Run("run", "reads.ges", "--handles", "handles.gess", "--quiet");
        Assert.AreEqual(1, reads.ExitCode, reads.StandardError);
        StringAssert.Contains(reads.StandardError, "link.missingHandleResolver");
        Assert.AreEqual(string.Empty, reads.StandardOutput);
    }

    /// <summary>Checks that schema errors use the schema-check format and that a missing path is a usage error.</summary>
    [TestMethod]
    public void SchemaErrorsAndMissingPathsFail()
    {
        Write("bad.gess", "bad\n");
        foreach (var command in new[] { "check", "compile", "run" })
        {
            var result = Run(command, "pass.ges", "--handles", "bad.gess");
            Assert.AreEqual(1, result.ExitCode, result.StandardError);
            StringAssert.Contains(result.StandardError, "bad.gess(1,1): error schema.");
            Assert.AreEqual(string.Empty, result.StandardOutput);

            var usage = Run(command, "pass.ges", "--handles");
            Assert.AreEqual(2, usage.ExitCode, usage.StandardError);
            StringAssert.Contains(usage.StandardError, "cli.usage");
        }
        Assert.IsFalse(File.Exists(Path.Combine(_directory, "pass.gesb")));
    }

    /// <summary>Checks that console input, <c>:load</c> and <c>:reload</c> compile with the declared handle types.</summary>
    [TestMethod]
    public void InteractiveCompilationsUseDeclaredHandleTypes()
    {
        var result = ToolProcess.ExecuteWithInput(
            _directory,
            "emit ConsoleOut(nothing is @Target)\n:load pass.ges\nemit Seen(target: nothing)\n:reload\nemit Seen(target: nothing)\n:load reads.ges\n",
            "run", "--interactive", "--handles", "handles.gess", "--quiet");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("false", "false", "false"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "link.missingHandleResolver");

        var unknown = ToolProcess.ExecuteWithInput(_directory, "emit ConsoleOut(nothing is @Target)\n", "run", "--interactive", "--quiet");
        StringAssert.Contains(unknown.StandardError, "validate.unknownHandleType");
    }

    private void Write(string name, string source) => File.WriteAllText(Path.Combine(_directory, name), source, new UTF8Encoding(false, true));
    private DotNetProcessResult Run(params string[] arguments) => ToolProcess.Execute(_directory, arguments);
    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines) + Environment.NewLine;
}
