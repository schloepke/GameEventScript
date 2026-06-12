using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Extensions;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptBinaryTests
{
    [TestMethod]
    public void FromCompiledBuildsCompactBindTable()
    {
        const string script =
            """
            module BinaryShape

            function score(value) means value + 1
            predicate high(value) means value > 3

            on Start(value) {
              let rounded be :math.floor value
              emit Done(score: score(value: value), high: value is high, rounded: rounded)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var binary = compiled;

        Assert.AreEqual("BinaryShape", compiled.ModuleName);
        Assert.AreEqual("BinaryShape", binary.ModuleName);
        Assert.AreEqual((ushort)1, binary.Header.Version);

        var binds = binary.BindTable.Entries.ToArray();
        Assert.AreEqual((ushort)5, binary.BindTable.EntryCount);
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.MessageHandler && Resolve(binary, entry.Name) == "Start"));
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.Function && Resolve(binary, entry.Name) == "score"));
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.Predicate && Resolve(binary, entry.Name) == "high"));
        Assert.IsTrue(binds.Where(entry => (byte)entry.Kind is >= 0x10 and <= 0x1F).All(entry => entry.EntryAddress < compiled.InstructionTable.Length));

        var start = binds.Single(entry => entry.Kind == GameEventScriptBinaryBindKind.MessageHandler);
        CollectionAssert.AreEqual(new[] { "value" }, start.ArgumentNames.Select(index => Resolve(binary, index)).ToArray());

        var outbound = binds.Single(entry => entry.Kind == GameEventScriptBinaryBindKind.OutboundMessage);
        Assert.AreEqual("Done", Resolve(binary, outbound.Name));
        CollectionAssert.AreEqual(new[] { "score", "high", "rounded" }, outbound.ArgumentNames.Select(index => Resolve(binary, index)).ToArray());

        var import = binds.Single(entry => entry.Kind == GameEventScriptBinaryBindKind.ExtensionCall);
        Assert.AreEqual("math.floor", Resolve(binary, import.Name));
        CollectionAssert.AreEqual(new[] { "_" }, import.ArgumentNames.Select(index => Resolve(binary, index)).ToArray());
    }

    [TestMethod]
    public void FromCompiledExportsMessageNameHandlersWithDedicatedBindKind()
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

    [TestMethod]
    public void BytecodeInstructionSerializesAsPortableHexWord()
    {
        var instruction = new GameEventScriptBytecodeInstruction
        {
            OpCode = GameEventScriptBytecodeOpCode.LoadInteger,
            DestinationSlot = 7,
            UnitAndFlags = (byte)GameEventScriptBytecodeInstructionUnit.UnitMeter,
            I64 = 42
        };

        var json = JsonSerializer.Serialize(instruction);

        Assert.AreEqual(
            """{"Opcode":"LoadInteger","Flags":"0x02","Dst":"0x0007","X":"0x0000","Y":"0x0000","Parameter":"0x000000000000002A"}""",
            json);

        var decoded = JsonSerializer.Deserialize<GameEventScriptBytecodeInstruction>(json);

        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, decoded.OpCode);
        Assert.AreEqual((byte)GameEventScriptBytecodeInstructionUnit.UnitMeter, decoded.UnitAndFlags);
        Assert.AreEqual((ushort)7, decoded.DestinationSlot);
        Assert.AreEqual(42L, decoded.I64);
    }

    [TestMethod]
    public void BinaryJsonUsesPortableInstructionShape()
    {
        const string script =
            """
            module BinaryJson

            on Start {
              emit Done(value: 1)
            }
            """;

        var binary = GameEventScriptManager.Compile(script);

        var json = JsonSerializer.Serialize(binary);

        StringAssert.Contains(json, "\"InstructionTable\":[{");
        StringAssert.Contains(json, "\"Opcode\":");
        StringAssert.Contains(json, "\"Flags\":");
        StringAssert.Contains(json, "\"Dst\":");
        StringAssert.Contains(json, "\"X\":");
        StringAssert.Contains(json, "\"Y\":");
        StringAssert.Contains(json, "\"Parameter\":");
        Assert.IsFalse(json.Contains("A_U16", StringComparison.Ordinal), "Instruction JSON must not expose overlapped typed fields.");
    }

    private static string Resolve(GameEventScriptBinary binary, ushort index) => binary.TextConstantTable.Resolve(index);
}
