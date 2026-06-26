using StepH.GameEventScript;
using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptBinaryTests
{
    [TestMethod]
    public void CompileBuildsCompactBindTable()
    {
        const string script =
            """
            module BinaryShape

            function score(value) be value + 1
            predicate high(value) be value > 3

            on Start(value) {
              let rounded be floor value
              emit Done(score: score(value: value), high: value is high, rounded: rounded)
            }
            """;

        var binary = GameEventScriptManager.Compile(script);

        Assert.AreEqual("BinaryShape", binary.ModuleName);
        Assert.AreEqual((ushort)1, binary.Header.Version);

        var binds = binary.BindTable.Entries.ToArray();
        Assert.AreEqual((ushort)4, binary.BindTable.EntryCount);
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.MessageHandler && Resolve(binary, entry.Name) == "Start"));
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.Function && Resolve(binary, entry.Name) == "score"));
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.Predicate && Resolve(binary, entry.Name) == "high"));
        Assert.IsTrue(binds.Where(entry => (byte)entry.Kind is >= 0x10 and <= 0x1F).All(entry => entry.EntryAddress < binary.InstructionTable.Length));

        var start = binds.Single(entry => entry.Kind == GameEventScriptBinaryBindKind.MessageHandler);
        CollectionAssert.AreEqual(new[] { "value" }, start.ArgumentNames.Select(index => Resolve(binary, index)).ToArray());

        var outbound = binds.Single(entry => entry.Kind == GameEventScriptBinaryBindKind.OutboundMessage);
        Assert.AreEqual("Done", Resolve(binary, outbound.Name));
        CollectionAssert.AreEqual(new[] { "score", "high", "rounded" }, outbound.ArgumentNames.Select(index => Resolve(binary, index)).ToArray());

        Assert.IsFalse(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.ExtensionCall));
    }

    [TestMethod]
    public void CompileExportsMessageNameHandlersWithDedicatedBindKind()
    {
        const string script =
            """
            module BinaryMessageName

            on Ping as message {
              emit Done(value: message.name)
            }
            """;

        var binary = GameEventScriptManager.Compile(script);

        var handler = binary.BindTable.Entries.Single(entry => entry.Kind == GameEventScriptBinaryBindKind.MessageNameHandler);
        Assert.AreEqual("Ping", Resolve(binary, handler.Name));
        CollectionAssert.AreEqual(new[] { "message" }, handler.ArgumentNames.Select(index => Resolve(binary, index)).ToArray());
    }

    [TestMethod]
    public void BinaryDumperFormatsMessageNameHandlersAsMessageNameDispatch()
    {
        const string script =
            """
            module BinaryMessageNameDump

            on Ping as message {
              emit Done(value: message.name)
            }
            """;

        var dump = GameEventScriptManager.Compile(script).Dump();

        StringAssert.Contains(dump, "// \"Ping as message\"");
        Assert.IsFalse(dump.Contains("// \"Ping(message)\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BinaryDumperCanIncludeSourceScriptInHeader()
    {
        const string script =
            """
            module BinarySourceDump

            on Start {
              emit Done(value: 1)
            }
            """;

        var dump = GameEventScriptManager.Compile(script).Dump(
            includeInstructionAddresses: true,
            scriptSource: script);

        StringAssert.Contains(dump, "// Script:");
        StringAssert.Contains(dump, "//\t\tmodule BinarySourceDump");
        StringAssert.Contains(dump, "//\t\ton Start {");
        StringAssert.Contains(dump, "// -------------------------------------------------------------------------------\n\n.gesb ");
    }

    private static string Resolve(GameEventScriptBinary binary, ushort index) => binary.TextConstantTable.Resolve(index);
}
