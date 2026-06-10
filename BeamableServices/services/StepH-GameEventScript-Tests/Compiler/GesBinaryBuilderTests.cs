using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;

namespace StepH_GameEventScript_Tests.Compiler;

[TestClass]
public sealed class GesBinaryBuilderTests
{
    [TestMethod]
    public void BuildDeduplicatesTextConstantsAndRetargetsSingleUseTemporaryMove()
    {
        var builder = new GesBinaryBuilder().WithModuleName("BuilderSmoke");
        var left = builder.AddRegister("left");
        var right = builder.AddRegister("right");
        var result = builder.AddRegister("result");
        var textA = builder.AddRegister("textA");
        var textB = builder.AddRegister("textB");
        var temp = builder.AddTemporaryRegister();

        var binary = builder
            .LoadInteger(left, 10)
            .LoadInteger(right, 20)
            .LoadText(textA, "same")
            .LoadText(textB, "same")
            .Multiply(temp, left, right)
            .Move(result, temp)
            .Build();

        Assert.AreEqual("BuilderSmoke", binary.ModuleName);
        Assert.HasCount(1, binary.TextConstantTable.Slices);
        Assert.AreEqual("same", binary.TextConstantTable.Resolve(0));
        Assert.HasCount(5, binary.InstructionTable);

        var multiply = binary.InstructionTable[4];
        Assert.AreEqual(GameEventScriptBytecodeOpCode.Multiply, multiply.OpCode);
        Assert.AreEqual((ushort)2, multiply.DestinationSlot);
        Assert.AreEqual((ushort)0, multiply.XSlot);
        Assert.AreEqual((ushort)1, multiply.YSlot);
    }

    [TestMethod]
    public void BuildScopesCreateHandlerBindAndPatchSlotLocals()
    {
        var builder = new GesBinaryBuilder().WithModuleName("BuilderScopes");

        using (var handler = builder.BeginHandler("Start", ["value"]))
        {
            var value = handler.Argument("value");
            var result = handler.AddRegister("result");
            var temp = handler.AddTemporaryRegister();

            using (handler.BeginScope("if"))
            {
                var local = handler.AddRegister("local");
                builder.LoadInteger(local, 5);
                builder.Add(temp, value, local);
            }

            builder.Move(result, temp);
            builder.ReturnVoid();
        }

        var binary = builder.Build();

        Assert.HasCount(1, binary.BindTable.Entries);
        var bind = binary.BindTable.Entries[0];
        Assert.AreEqual(GameEventScriptBinaryBindKind.MessageHandler, bind.Kind);
        Assert.AreEqual("Start", binary.TextConstantTable.Resolve(bind.Name));
        Assert.AreEqual((ushort)0, bind.EntryAddress);
        Assert.HasCount(1, bind.ArgumentNames);
        Assert.AreEqual("value", binary.TextConstantTable.Resolve(bind.ArgumentNames[0]));

        Assert.HasCount(4, binary.InstructionTable);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.SlotLocals, binary.InstructionTable[0].OpCode);
        Assert.AreEqual((short)2, binary.InstructionTable[0].Count);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, binary.InstructionTable[1].OpCode);
        Assert.AreEqual((ushort)2, binary.InstructionTable[1].DestinationSlot);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.Add, binary.InstructionTable[2].OpCode);
        Assert.AreEqual((ushort)1, binary.InstructionTable[2].DestinationSlot);
        Assert.AreEqual((ushort)0, binary.InstructionTable[2].XSlot);
        Assert.AreEqual((ushort)2, binary.InstructionTable[2].YSlot);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnVoid, binary.InstructionTable[3].OpCode);
    }

    [TestMethod]
    public void OptimizeCanRewritePlanBeforeBuild()
    {
        var builder = new GesBinaryBuilder()
            .WithModuleName("BuilderRewrite")
            .WithOptimization(false);
        var first = builder.AddRegister("first");
        var second = builder.AddRegister("second");

        var binary = builder
            .Nop()
            .LoadInteger(first, 1)
            .ReturnVoid()
            .Optimize(context =>
            {
                context.RemoveAt(0);
                Assert.IsTrue(context.TryGetInstruction(0, out var load));
                context.ReplaceInstruction(0, load with { I64 = 2 });
                context.InsertInstructionAfter(
                    0,
                    context.CreateInstructionNear(
                        0,
                        GameEventScriptBytecodeOpCode.LoadInteger,
                        dst: GesOperand.Register(second),
                        i64: 3));
            })
            .Build();

        Assert.HasCount(3, binary.InstructionTable);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, binary.InstructionTable[0].OpCode);
        Assert.AreEqual((ushort)0, binary.InstructionTable[0].DestinationSlot);
        Assert.AreEqual(2L, binary.InstructionTable[0].I64);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, binary.InstructionTable[1].OpCode);
        Assert.AreEqual((ushort)1, binary.InstructionTable[1].DestinationSlot);
        Assert.AreEqual(3L, binary.InstructionTable[1].I64);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnVoid, binary.InstructionTable[2].OpCode);
    }
}
