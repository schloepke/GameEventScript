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
        Assert.AreEqual((ushort)1, binary.FormatVersion);

        var binds = binary.Bindings.Entries.ToArray();
        Assert.AreEqual((ushort)4, binary.Bindings.EntryCount);
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.MessageHandler && Resolve(binary, entry.Name) == "Start"));
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.Function && Resolve(binary, entry.Name) == "score"));
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.Predicate && Resolve(binary, entry.Name) == "high"));
        Assert.IsTrue(binds.Where(entry => (byte)entry.Kind is >= 0x10 and <= 0x1F).All(entry => entry.EntryAddress < binary.Code.Instructions.Length));

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

        var handler = binary.Bindings.Entries.Single(entry => entry.Kind == GameEventScriptBinaryBindKind.MessageNameHandler);
        Assert.AreEqual("Ping", Resolve(binary, handler.Name));
        CollectionAssert.AreEqual(new[] { "message" }, handler.ArgumentNames.Select(index => Resolve(binary, index)).ToArray());
    }

    [TestMethod]
    public void ProgramDumperFormatsMessageNameHandlersAsMessageNameDispatch()
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
    public void ProgramDumperUsesEmbeddedSourceArchiveInHeader()
    {
        const string script =
            """
            module BinarySourceDump

            on Start {
              emit Done(value: 1)
            }
            """;

        var dump = GameEventScriptManager.CreateScriptBuilder()
            .AddScript(script, "binary-source-dump.ges")
            .WithDebugInfo()
            .Compile()
            .Dump(includeInstructionAddresses: true);

        StringAssert.Contains(dump, "// -------------------------------------------------------------------------------\n.region \"Source: binary-source-dump.ges\"\n\n.segment source \"binary-source-dump.ges\"\n\n");
        StringAssert.Contains(dump, "module BinarySourceDump");
        StringAssert.Contains(dump, "on Start {");
        StringAssert.Contains(dump, ".region-end \"Source: binary-source-dump.ges\"\n// -------------------------------------------------------------------------------");
        StringAssert.Contains(dump, ".region \"Text\"\n\n.segment text");
        StringAssert.Contains(dump, ".region \"Lists\"\n\n.segment lists");
        StringAssert.Contains(dump, ".region \"Bindings\"\n\n.segment bind");
        StringAssert.Contains(dump, ".region \"Code\"\n\n.segment code");
        StringAssert.Contains(dump, ".source-line \"binary-source-dump.ges\" 3 | on Start {");
        StringAssert.Contains(dump, "// -------------------------------------------------------------------------------\n\n.gesb ");
    }

    private static string Resolve(GameEventScriptProgram binary, ushort index) => binary.StringConstants.Resolve(index);
}
