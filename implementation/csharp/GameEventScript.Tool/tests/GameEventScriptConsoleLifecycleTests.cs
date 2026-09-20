// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Tool;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies CLI program removal and transactional host replacement.</summary>
[TestClass]
public sealed class GameEventScriptConsoleLifecycleTests
{
    private string _directory = null!;

    /// <summary>Creates an isolated file workspace.</summary>
    [TestInitialize]
    public void CreateWorkspace()
    {
        _directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "tool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Removes the isolated workspace.</summary>
    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_directory, true);

    /// <summary>Verifies removal by ID and module, monotonic IDs, and retained native handlers.</summary>
    [TestMethod]
    public void ConsoleUnloadsProgramsAndKeepsNativeHandlers()
    {
        Write("one.ges", "module one\non Start { emit ConsoleOut('one') }");
        Write("two.ges", "module two\non Start { emit ConsoleOut('two') }");
        var input = ":load one.ges\n:load two.ges\n:unload @1\nemit Start\n:load one.ges\n:list\n:unload two\n:unloadAll\n"
            + ":reload\n:handler\n:list\n:load two.ges\n:list\nemit Start\nemit ConsoleOut('native')\n";
        var result = ToolProcess.ExecuteWithInput(_directory, input, "run", "--interactive", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("two", "two", "native"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "@3  one");
        StringAssert.Contains(result.StandardError, "@4  two");
        StringAssert.Contains(result.StandardError, "Loaded programs (0):");
        StringAssert.Contains(result.StandardError, "Registered handlers (3):");
    }

    /// <summary>Verifies ambiguous or invalid removals and command arguments leave handlers intact.</summary>
    [TestMethod]
    public void InvalidLifecycleCommandsPreservePrograms()
    {
        Write("one.ges", "module one\non Start { emit ConsoleOut('one') }");
        var input = ":load one.ges\n:load one.ges\n:unload one\n:unload @99\n:unload\n:unloadAll extra\n:reload extra\nemit Start\n";
        var result = ToolProcess.ExecuteWithInput(_directory, input, "run", "--interactive", "--quiet");
        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("one", "one"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "@1, @2");
        StringAssert.Contains(result.StandardError, "cli.consoleCommand");
    }

    /// <summary>Verifies repeated binary loads initialize as one group and Main is not invoked.</summary>
    [TestMethod]
    public void ReloadInitializesAllBinariesBeforeDispatch()
    {
        Binary("one.gesb", "module one\non initialization { emit ConsoleOut('init one'); emit Ready }\non Main(args) { emit ConsoleOut('not main') }");
        Binary("two.gesb", "module two\non initialization { emit ConsoleOut('init two') }\non Ready { emit ConsoleOut('ready') }");
        var result = ToolProcess.ExecuteWithInput(_directory, ":reload\n:list\n", "run", "--interactive", "--quiet", "one.gesb", "two.gesb");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines("init one", "init two", "ready", "init one", "init two", "ready"), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "@1  one");
        StringAssert.Contains(result.StandardError, "@2  two");
    }

    /// <summary>Verifies reload rereads a source group and preserves program identity while replacing the host.</summary>
    [TestMethod]
    public void ReloadRereadsJointSources()
    {
        var first = Write("first.ges", "module duo\nfunction value() be 7");
        var second = Write("second.ges", "module duo\non Start { emit ErrorCode(value()) }");
        var session = Session();
        string? activePath = null;
        var instance = session.Inventory.Load(RunProgramFiles.Compile([first, second], ref activePath), [first, second]);
        Assert.IsTrue(session.Pump());
        Deliver(session, "Start");
        Assert.AreEqual(7, session.Observer.ScriptExitCode);
        var oldHost = session.Host;
        Write("first.ges", "module duo\nfunction value() be 12");
        Assert.IsTrue(session.Reload(ref activePath));
        Assert.AreNotSame(oldHost, session.Host);
        Assert.IsFalse(instance.IsAttached);
        Assert.AreEqual(0, session.Observer.ScriptExitCode);
        Deliver(session, "Start");
        Assert.AreEqual(12, session.Observer.ScriptExitCode);
        var entry = session.Inventory.ActivePrograms().Single();
        Assert.AreEqual(1, entry.Id);
        Assert.HasCount(2, entry.Paths);
    }

    /// <summary>Verifies reload reads fresh binary bytes.</summary>
    [TestMethod]
    public void ReloadRereadsBinary()
    {
        var path = Binary("one.gesb", "on Start { emit ErrorCode(7) }");
        var session = Session();
        session.Inventory.Load(RunProgramFiles.ReadOne(path), [path]);
        Assert.IsTrue(session.Pump());
        Binary("one.gesb", "on Start { emit ErrorCode(12) }");
        string? activePath = null;
        Assert.IsTrue(session.Reload(ref activePath));
        Deliver(session, "Start");
        Assert.AreEqual(12, session.Observer.ScriptExitCode);
    }

    /// <summary>Verifies preparation failures preserve the entire previous host and its loaded programs.</summary>
    /// <param name="failure">The failure while preparing the replacement host.</param>
    [TestMethod]
    [DataRow("read")]
    [DataRow("compile")]
    [DataRow("decode")]
    [DataRow("link")]
    public void FailedReloadPreservesTheEntireSession(string failure)
    {
        var first = Write("one.ges", "module one\non Start { emit ErrorCode(7) }");
        var second = failure == "decode" ? Binary("two.gesb", "module two\non Other {}") : Write("two.ges", "module two\non Other {}");
        var session = Session();
        session.Inventory.Load(RunProgramFiles.ReadOne(first), [first]);
        session.Inventory.Load(RunProgramFiles.ReadOne(second), [second]);
        Assert.IsTrue(session.Pump());
        Deliver(session, "Start");
        var oldHost = session.Host;
        Write("one.ges", "module one\non initialization { emit ErrorCode(99) }");
        if (failure == "read") File.Delete(second);
        if (failure == "compile") File.WriteAllText(second, "on Broken(");
        if (failure == "decode") File.WriteAllBytes(second, [0xFF]);
        if (failure == "link") File.WriteAllText(second, "on Start { emit ErrorCode(:missing.extension()) }");
        string? activePath = null;
        var exception = Assert.Throws<Exception>(() => session.Reload(ref activePath));
        var expectedType = failure switch
        {
            "read" => typeof(FileNotFoundException),
            "compile" => typeof(GameEventScriptCompileException),
            "decode" => typeof(GameEventScriptProgramFormatException),
            _ => typeof(GameEventScriptDynamicLinkException)
        };
        Assert.IsInstanceOfType(exception, expectedType);
        Assert.AreSame(oldHost, session.Host);
        Assert.AreEqual(7, session.Observer.ScriptExitCode);
        Assert.HasCount(2, session.Inventory.ActivePrograms());
        Deliver(session, "Start");
        Assert.AreEqual(7, session.Observer.ScriptExitCode);
    }

    /// <summary>Verifies configured random seeds restart and ErrorCode state resets.</summary>
    [TestMethod]
    public void ReloadResetsRandomAndExitCode()
    {
        var path = Write("random.ges", "on Draw { emit ErrorCode(random from 1 to 255) }");
        var session = Session();
        session.Inventory.Load(RunProgramFiles.ReadOne(path), [path]);
        Assert.IsTrue(session.Pump());
        Deliver(session, "Draw");
        var first = session.Observer.ScriptExitCode;
        Deliver(session, "Draw");
        string? activePath = null;
        Assert.IsTrue(session.Reload(ref activePath));
        Assert.AreEqual(0, session.Observer.ScriptExitCode);
        Deliver(session, "Draw");
        Assert.AreEqual(first, session.Observer.ScriptExitCode);
        session.Inventory.UnloadAll();
        Assert.AreEqual(first, session.Observer.ScriptExitCode);
    }

    /// <summary>Verifies initialization failures and limits cannot complete a reload.</summary>
    /// <param name="body">The replacement initialization behavior.</param>
    [TestMethod]
    [DataRow("emit ErrorCode(256)")]
    [DataRow("emit Tick")]
    public void ReloadRuntimeFailureEndsSession(string body)
    {
        var path = Write("init.ges", "on initialization {}");
        var session = Session();
        session.Inventory.Load(RunProgramFiles.ReadOne(path), [path]);
        Assert.IsTrue(session.Pump());
        Write("init.ges", "on initialization { " + body + " }\non Tick { emit Tick }");
        string? activePath = null;
        Assert.IsFalse(session.Reload(ref activePath));
    }

    /// <summary>Verifies lifecycle help is available and quiet mode suppresses lifecycle status reports.</summary>
    [TestMethod]
    public void LifecycleHelpAndQuietReports()
    {
        var result = ToolProcess.ExecuteWithInput(_directory, ":help unload\n:help unloadAll\n:help reload\n:unloadAll\n:reload\n", "run", "--interactive", "--quiet");
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, ":unload <module|@ID>");
        StringAssert.Contains(result.StandardError, ":reload");
        Assert.IsFalse(result.StandardError.Contains("Reloaded all", StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains("Unloaded 0", StringComparison.Ordinal));
        var verbose = ToolProcess.ExecuteWithInput(_directory, ":unloadAll\n:reload\n", "run", "--interactive");
        Assert.AreEqual(0, verbose.ExitCode, verbose.StandardError);
        StringAssert.Contains(verbose.StandardError, "Unloaded 0");
        StringAssert.Contains(verbose.StandardError, "Reloaded all");
    }

    private static RunSession Session() => new(42, GameEventScriptRuntimeLimits.Default, false, false, true);

    private static void Deliver(RunSession session, string name)
    {
        Assert.IsTrue(session.Host.Receive(GameEventScriptMessage.Create(name)));
        Assert.IsTrue(session.Pump());
    }

    private string Write(string name, string source)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, source);
        return path;
    }

    private string Binary(string name, string source)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, GameEventScriptProgramWriter.ToArray(GameEventScriptBuilder.Create().AddScript(source).Compile()));
        return path;
    }

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines) + Environment.NewLine;
}
