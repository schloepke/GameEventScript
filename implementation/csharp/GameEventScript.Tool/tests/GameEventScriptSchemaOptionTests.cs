// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Tool;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies that <c>--schema</c> declares schema handle types to every CLI compilation and checks binary imports without registering a resolver.</summary>
[TestClass]
public sealed class GameEventScriptSchemaOptionTests
{
    private const string Schema = "schema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\n";
    private const string Narrower = "schema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 255\n";
    private const string Other = "schema :Contact as { label: :Text max 16 }\nhandle @Other of :Contact with id :Number integer between 0 and 9\n";
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

        var check = Run("check", "pass.ges", "--schema", "handles.gess", "--quiet");
        Assert.AreEqual(0, check.ExitCode, check.StandardError);

        var compile = Run("compile", "reads.ges", "--schema", "*.gess", "-o", "reads.gesb", "--quiet");
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
        var pass = Run("run", "pass.ges", "--schema", "handles.gess", "--quiet");
        Assert.AreEqual(0, pass.ExitCode, pass.StandardError);
        Assert.AreEqual(Lines("false"), pass.StandardOutput);

        var reads = Run("run", "reads.ges", "--schema", "handles.gess", "--quiet");
        Assert.AreEqual(1, reads.ExitCode, reads.StandardError);
        StringAssert.Contains(reads.StandardError, "link.missingHandleResolver");
        Assert.AreEqual(string.Empty, reads.StandardOutput);
    }

    /// <summary>Checks that binary programs must import the handle types the schemas declare, by name and hash.</summary>
    [TestMethod]
    public void RunChecksBinaryHandleImports()
    {
        Write("narrower.gess", Narrower);
        Write("other.gess", Other);
        Write("empty.gess", "schema :Contact as { label: :Text max 16 }\n");
        var compile = Run("compile", "pass.ges", "--schema", "handles.gess", "--quiet");
        Assert.AreEqual(0, compile.ExitCode, compile.StandardError);

        var matching = Run("run", "pass.gesb", "--schema", "handles.gess", "--quiet");
        Assert.AreEqual(0, matching.ExitCode, matching.StandardError);
        Assert.AreEqual(Lines("false"), matching.StandardOutput);

        var unverified = Run("run", "pass.gesb", "--quiet");
        Assert.AreEqual(0, unverified.ExitCode, unverified.StandardError);

        foreach (var schema in new[] { "narrower.gess", "other.gess", "empty.gess" })
        {
            var mismatch = Run("run", "pass.gesb", "--schema", schema, "--quiet");
            Assert.AreEqual(1, mismatch.ExitCode, mismatch.StandardError);
            StringAssert.Contains(mismatch.StandardError, "link.mismatchedHandleType");
            StringAssert.Contains(mismatch.StandardError, "@Target");
            Assert.AreEqual(string.Empty, mismatch.StandardOutput);
        }

        var load = ToolProcess.ExecuteWithInput(_directory, ":load pass.gesb\nemit Seen(target: nothing)\n", "run", "--interactive", "--schema", "narrower.gess", "--quiet");
        Assert.AreEqual(1, load.ExitCode, load.StandardError);
        StringAssert.Contains(load.StandardError, "link.mismatchedHandleType");
        Assert.AreEqual(string.Empty, load.StandardOutput);
    }

    /// <summary>Checks that schema errors use the schema-check format, that a missing path is a usage error and that the old option is gone.</summary>
    [TestMethod]
    public void SchemaErrorsAndMissingPathsFail()
    {
        Write("bad.gess", "bad\n");
        foreach (var command in new[] { "check", "compile", "run" })
        {
            var result = Run(command, "pass.ges", "--schema", "bad.gess");
            Assert.AreEqual(1, result.ExitCode, result.StandardError);
            StringAssert.Contains(result.StandardError, "bad.gess(1,1): error schema.");
            Assert.AreEqual(string.Empty, result.StandardOutput);

            var usage = Run(command, "pass.ges", "--schema");
            Assert.AreEqual(2, usage.ExitCode, usage.StandardError);
            StringAssert.Contains(usage.StandardError, "cli.usage");

            var removed = Run(command, "pass.ges", "--handles", "handles.gess");
            Assert.AreEqual(2, removed.ExitCode, removed.StandardError);
            StringAssert.Contains(removed.StandardError, "'--handles'");
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
            "run", "--interactive", "--schema", "handles.gess", "--quiet");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("false", "false", "false"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "link.missingHandleResolver");

        var unknown = ToolProcess.ExecuteWithInput(_directory, "emit ConsoleOut(nothing is @Target)\n", "run", "--interactive", "--quiet");
        StringAssert.Contains(unknown.StandardError, "validate.unknownHandleType");
    }

    /// <summary>
    /// Checks that <c>:reload</c> reads the schema files again and keeps the old session when they fail to read or no
    /// longer match. The editor of an unchanged scratch replaces the schema file between the reloads.
    /// </summary>
    [TestMethod]
    public void ReloadReadsSchemasAgain()
    {
        if (OperatingSystem.IsWindows()) Assert.Inconclusive("The replacement editor is a POSIX shell script.");
        var compile = Run("compile", "pass.ges", "--schema", "handles.gess", "--quiet");
        Assert.AreEqual(0, compile.ExitCode, compile.StandardError);
        Write("next1.gess", Narrower);
        Write("next2.gess", "bad\n");
        Write("next3.gess", Schema + Other.Split('\n')[1] + "\n");
        Write("swap.sh", "set -- next*.gess\nmv \"$1\" handles.gess\n");
        const string Step = ":edit\n:reload\nemit Seen(target: nothing)\n";
        var result = ToolProcess.ExecuteWithInputAndEditor(
            _directory,
            Step + Step + Step + "emit ConsoleOut(nothing is @Other)\n",
            "sh swap.sh",
            "run", "--interactive", "--schema", "handles.gess", "pass.gesb", "--quiet");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("false", "false", "false", "false"), result.StandardOutput);
        var errors = result.StandardError;
        StringAssert.Contains(errors, "link.mismatchedHandleType");
        StringAssert.Contains(errors, "handles.gess(1,1): error schema.");
        Assert.IsFalse(errors.Contains("validate.unknownHandleType", StringComparison.Ordinal), errors);
    }

    private void Write(string name, string source) => File.WriteAllText(Path.Combine(_directory, name), source, new UTF8Encoding(false, true));
    private DotNetProcessResult Run(params string[] arguments) => ToolProcess.Execute(_directory, arguments);
    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines) + Environment.NewLine;
}
