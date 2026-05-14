using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;

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
        var binary = compiled.ToGameEventScriptBinary();

        Assert.AreEqual("BinaryShape", compiled.ModuleName);
        Assert.AreEqual("BinaryShape", binary.ModuleName);
        Assert.AreEqual((ushort)1, binary.Header.Version);

        var binds = binary.BindTable.Entries.ToArray();
        Assert.AreEqual((ushort)5, binary.BindTable.EntryCount);
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.MessageHandler && Resolve(binary, entry.Name) == "Start"));
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.Function && Resolve(binary, entry.Name) == "score"));
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.Predicate && Resolve(binary, entry.Name) == "high"));
        Assert.IsTrue(binds.Where(entry => (byte)entry.Kind is >= 0x10 and <= 0x1F).All(entry => entry.EntryAddress < compiled.Code.Count));

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
    public void BytecodeInstructionSerializesAsPortableHexWord()
    {
        var instruction = new GameEventScriptBytecodeInstruction(
            GameEventScriptBytecodeOpCode.LoadInteger,
            dest: 7,
            unitAndFlags: (byte)GameEventScriptBytecodeInstructionUnit.Percentage);
        instruction.I64 = 42;

        var json = JsonSerializer.Serialize(instruction);

        Assert.AreEqual(
            """{"Opcode":"LoadInteger","Flags":"0x01","Dst":"0x0007","Parameter":"0x000000000000002A"}""",
            json);

        var decoded = JsonSerializer.Deserialize<GameEventScriptBytecodeInstruction>(json);

        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, decoded.OpCode);
        Assert.AreEqual((byte)GameEventScriptBytecodeInstructionUnit.Percentage, decoded.UnitAndFlags);
        Assert.AreEqual((ushort)7, decoded.Dest_U16);
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

        var binary = GameEventScriptManager.Compile(script).ToGameEventScriptBinary();

        var json = JsonSerializer.Serialize(binary);

        StringAssert.Contains(json, "\"InstructionTable\":[{");
        StringAssert.Contains(json, "\"Opcode\":");
        StringAssert.Contains(json, "\"Flags\":");
        StringAssert.Contains(json, "\"Dst\":");
        StringAssert.Contains(json, "\"Parameter\":");
        Assert.IsFalse(json.Contains("A_U16", StringComparison.Ordinal), "Instruction JSON must not expose overlapped typed fields.");
    }

    private static string Resolve(GameEventScriptBinary binary, ushort index) => binary.TextConstantTable.Resolve(index);
}
