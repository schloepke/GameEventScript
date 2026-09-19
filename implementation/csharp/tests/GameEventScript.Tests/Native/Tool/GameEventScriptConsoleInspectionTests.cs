// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies interactive program inventories, registered handlers, and in-memory dumps through the CLI process.</summary>
[TestClass]
public sealed class GameEventScriptConsoleInspectionTests
{
    private string _directory = null!;

    /// <summary>Creates an isolated CLI workspace.</summary>
    [TestInitialize]
    public void CreateWorkspace()
    {
        _directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "tool-inspection", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Removes the isolated workspace.</summary>
    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_directory, true);

    /// <summary>Verifies native handlers are visible in an empty host and transient console inputs never enter the inventory.</summary>
    [TestMethod]
    public void EmptyConsoleShowsNativeHandlersButNotTransientInputs()
    {
        var result = Interactive(":list\n:handler\nemit ConsoleOut('live')\n:list\n:handler\n:quit\n");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("live"), result.StandardOutput);
        Assert.HasCount(2, Regex.Matches(result.StandardError, @"Loaded programs \(0\):"));
        Assert.HasCount(2, Regex.Matches(result.StandardError, @"Registered handlers \(3\):"));
        foreach (var name in new[] { "ConsoleOut", "ConsoleErr", "ErrorCode" }) StringAssert.Contains(result.StandardError, $"native  {name}(...) [name-only]");
        Assert.IsFalse(result.StandardError.Contains("<interactive:", StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains("initialization", StringComparison.Ordinal));
    }

    /// <summary>Verifies jointly compiled files produce one inventory entry with exact/name-only signatures and tag filters.</summary>
    [TestMethod]
    public void SourceInventoryDescribesRegisteredHandlers()
    {
        Write("first.ges", """
module inspect.demo
function twice(_ x) be x + x
on initialization { emit ConsoleOut('initialized') }
on Start(value) matching #ready without #hidden { emit ConsoleOut(twice(value)) }
on Any as incoming matching #visible { emit ConsoleOut(incoming) }
""");
        Write("second.ges", """
on Start(left, right) { emit Outbound(value: left + right) }
on undeliverable as rejected { emit ConsoleOut(rejected) }
""");
        var result = Interactive(":list\n:handler\nemit Start(value: 21) with #ready\n:quit\n", "first.ges", "second.ges");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("initialized", "42"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "Loaded programs (1):");
        StringAssert.Contains(result.StandardError, "@1  inspect.demo");
        StringAssert.Contains(result.StandardError, "handlers=4");
        StringAssert.Contains(result.StandardError, "first.ges");
        StringAssert.Contains(result.StandardError, "second.ges");
        StringAssert.Contains(result.StandardError, "Registered handlers (7):");
        StringAssert.Contains(result.StandardError, "Start(value) [signature] matching #ready without #hidden");
        StringAssert.Contains(result.StandardError, "Start(left,right) [signature]");
        StringAssert.Contains(result.StandardError, "Any(...) [name-only] matching #visible");
        StringAssert.Contains(result.StandardError, "undeliverable(...) [name-only]");
        Assert.IsFalse(result.StandardError.Contains("initialization", StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains("twice", StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains("Outbound", StringComparison.Ordinal));
    }

    /// <summary>Verifies binary order, inspection without source/debug files, and exact reuse of the portable GESA dumper.</summary>
    /// <param name="debug">Whether the binaries contain debug information.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BinaryInventoryDumpsTheSelectedProgram(bool debug)
    {
        Binary("first.gesb", "module first\non First { emit ConsoleOut('first') }\n", debug);
        var second = Binary("second.gesb", "module second\non Second { emit ConsoleOut('Grüße 😀') }\n", debug);
        var result = Interactive(":list\n:handler\n:dump second\nemit Second\n:quit\n", "first.gesb", "second.gesb");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("Grüße 😀"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "Loaded programs (2):");
        var firstIndex = result.StandardError.IndexOf("@1  first", StringComparison.Ordinal);
        var secondIndex = result.StandardError.IndexOf("@2  second", StringComparison.Ordinal);
        Assert.IsTrue(firstIndex >= 0 && secondIndex > firstIndex);
        StringAssert.Contains(result.StandardError, "@1  first  First() [signature]");
        StringAssert.Contains(result.StandardError, "@2  second  Second() [signature]");
        StringAssert.Contains(result.StandardError, Environment.NewLine + second.Dump() + Environment.NewLine);
        Assert.IsFalse(result.StandardError.Contains(".module \"first\"", StringComparison.Ordinal));
        Assert.HasCount(2, Directory.GetFiles(_directory));
    }

    /// <summary>Verifies anonymous programs can be dumped unambiguously by their session ID.</summary>
    [TestMethod]
    public void AnonymousProgramCanBeDumpedById()
    {
        var program = Binary("unnamed.gesb", "on Start { emit ConsoleOut(42) }\n", false);
        var result = Interactive(":dump @1\n:quit\n", "unnamed.gesb");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        Assert.AreEqual(Environment.NewLine + program.Dump() + Environment.NewLine, result.StandardError);
    }

    /// <summary>Verifies successful loads receive IDs, rejected loads do not, and repeated module names require an explicit ID.</summary>
    /// <param name="binary">Whether programs added during the console session are binaries.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LoadsAndDuplicateModulesRemainIndividuallyInspectable(bool binary)
    {
        const string source = "module repeat\non Start { emit ConsoleOut('repeat') }\n";
        var file = binary ? "extra.gesb" : "extra.ges";
        if (binary) Binary(file, source, false);
        else Write(file, source);
        Write("initial.ges", "on Initial { emit ConsoleOut('initial') }\n");
        var commands = $":load missing.ges\n:load {file}\n:load {file}\n:list\n:handler\n:dump repeat\n:dump @3\nemit Initial\nemit Start\n:quit\n";
        var result = Interactive(commands, "initial.ges");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("initial", "repeat", "repeat"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "Loaded programs (3):");
        StringAssert.Contains(result.StandardError, "@1  anonymous.");
        StringAssert.Contains(result.StandardError, "@2  repeat");
        StringAssert.Contains(result.StandardError, "@3  repeat");
        StringAssert.Contains(result.StandardError, "Registered handlers (6):");
        StringAssert.Contains(result.StandardError, "cli.consoleCommand");
        StringAssert.Contains(result.StandardError, "@2, @3");
        Assert.HasCount(1, Regex.Matches(result.StandardError, "\\.module \"repeat\""));
    }

    /// <summary>Verifies a link failure never adds an inventory entry or consumes a program ID.</summary>
    [TestMethod]
    public void LinkFailureDoesNotRegisterAnInspectableProgram()
    {
        Write("broken.ges", "module broken\non Start { emit ConsoleOut(:probe.missing()) }\n");
        Write("good.ges", "module good\non Start { emit ConsoleOut(42) }\n");
        var result = Interactive(":load broken.ges\n:load good.ges\n:list\n:handler\nemit Start\n:quit\n");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "Loaded programs (1):");
        StringAssert.Contains(result.StandardError, "@1  good");
        StringAssert.Contains(result.StandardError, "Registered handlers (4):");
        Assert.IsFalse(result.StandardError.Contains("@2", StringComparison.Ordinal));
    }

    /// <summary>Verifies malformed inspection commands fail recoverably and never execute script code.</summary>
    /// <param name="command">The invalid command.</param>
    [TestMethod]
    [DataRow(":list extra")]
    [DataRow(":handler extra")]
    [DataRow(":dump")]
    [DataRow(":dump missing")]
    [DataRow(":dump @0")]
    [DataRow(":dump @invalid")]
    [DataRow(":dump @999999999999999")]
    [DataRow(":dump @1")]
    [DataRow(":dump one two")]
    public void InvalidInspectionCommandsAllowFurtherInput(string command)
    {
        var result = Interactive(command + "\nemit ConsoleOut(42)\n:quit\n");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("42"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "cli.consoleCommand");
    }

    private GameEventScriptProgram Binary(string file, string source, bool debug)
    {
        var builder = GameEventScriptBuilder.Create().AddScript(source, "unavailable-source.ges");
        if (!debug) builder.WithDebugInfo(GameEventScriptDebugInfoOptions.None);
        var program = builder.Compile();
        File.WriteAllBytes(Path.Combine(_directory, file), GameEventScriptProgramWriter.ToArray(program));
        return program;
    }

    private void Write(string file, string source) => File.WriteAllText(Path.Combine(_directory, file), source, new UTF8Encoding(false, true));

    private DotNetProcessResult Interactive(string input, params string[] paths)
        => ToolProcess.ExecuteWithInput(_directory, input, ["run", "--interactive", "--quiet", .. paths]);

    private static string Lines(params string[] values) => string.Join(Environment.NewLine, values) + Environment.NewLine;
}
