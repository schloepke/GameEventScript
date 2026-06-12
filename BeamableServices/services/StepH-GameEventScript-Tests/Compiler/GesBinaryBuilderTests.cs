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
    public void BuildHelperRoutineCreatesCodeWithoutBindEntry()
    {
        var builder = new GesBinaryBuilder().WithModuleName("BuilderHelpers");

        using (var helper = builder.BeginHelper("project_value", ["value"]))
        {
            Assert.IsFalse(helper.HasBind);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = helper.Bind);

            var value = helper.Argument("value");
            var one = helper.AddRegister("one");
            var result = helper.AddRegister("result");
            builder.LoadInteger(one, 1);
            builder.Add(result, value, one);
            builder.ReturnValue(result);
        }

        using (builder.BeginHandler("Start"))
        {
            builder.ReturnVoid();
        }

        var binary = builder.Build();

        Assert.HasCount(1, binary.BindTable.Entries);
        Assert.AreEqual(GameEventScriptBinaryBindKind.MessageHandler, binary.BindTable.Entries[0].Kind);
        Assert.AreEqual("Start", binary.TextConstantTable.Resolve(binary.BindTable.Entries[0].Name));
        Assert.AreEqual((ushort)0, binary.BindTable.Entries[0].EntryAddress);

        Assert.HasCount(6, binary.InstructionTable);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.SlotLocals, binary.InstructionTable[0].OpCode);
        Assert.AreEqual((short)0, binary.InstructionTable[0].Count);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnVoid, binary.InstructionTable[1].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.SlotLocals, binary.InstructionTable[2].OpCode);
        Assert.AreEqual((short)2, binary.InstructionTable[2].Count);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, binary.InstructionTable[3].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.Add, binary.InstructionTable[4].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnValue, binary.InstructionTable[5].OpCode);
    }

    [TestMethod]
    public void BuildNestedHelperRoutineWritesSeparateCodeBlock()
    {
        var builder = new GesBinaryBuilder().WithModuleName("BuilderNestedHelpers");

        using (var handler = builder.BeginHandler("Start"))
        {
            var result = handler.AddRegister("result");
            GesLabelRef helperEntry;

            using (var helper = builder.BeginHelper("compute"))
            {
                helperEntry = helper.EntryLabel;
                var value = helper.AddRegister("value");
                builder.LoadInteger(value, 7);
                builder.ReturnValue(value);
            }

            builder.Call(result, helperEntry);
            builder.ReturnVoid();
        }

        var binary = builder.Build();

        Assert.HasCount(1, binary.BindTable.Entries);
        Assert.AreEqual(GameEventScriptBinaryBindKind.MessageHandler, binary.BindTable.Entries[0].Kind);
        Assert.AreEqual("Start", binary.TextConstantTable.Resolve(binary.BindTable.Entries[0].Name));
        Assert.AreEqual((ushort)0, binary.BindTable.Entries[0].EntryAddress);

        Assert.HasCount(6, binary.InstructionTable);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.SlotLocals, binary.InstructionTable[0].OpCode);
        Assert.AreEqual((short)1, binary.InstructionTable[0].Count);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.Call, binary.InstructionTable[1].OpCode);
        Assert.AreEqual((ushort)0, binary.InstructionTable[1].DestinationSlot);
        Assert.AreEqual((ushort)3, binary.InstructionTable[1].TargetAddress);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnVoid, binary.InstructionTable[2].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.SlotLocals, binary.InstructionTable[3].OpCode);
        Assert.AreEqual((short)1, binary.InstructionTable[3].Count);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, binary.InstructionTable[4].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnValue, binary.InstructionTable[5].OpCode);
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

    [TestMethod]
    public void SourceRangeScopesAnnotatePlanItemsAndSurviveRewrites()
    {
        var builder = new GesBinaryBuilder()
            .WithModuleName("BuilderSourceRanges")
            .WithOptimization(false);
        var first = builder.AddRegister("first");
        var second = builder.AddRegister("second");
        var third = builder.AddRegister("third");
        var label = builder.AddLabel("start");
        var outer = new GameEventScriptSourceLocation("test.ges", 1, 2, 1, 12, "BuilderSourceRanges");
        var inner = new GameEventScriptSourceLocation("test.ges", 3, 4, 3, 18, "BuilderSourceRanges");

        using (builder.SourceRange(outer))
        {
            builder.MarkLabel(label);
            builder.LoadInteger(first, 1);
            using (builder.SourceRange(inner))
            {
                builder.LoadInteger(second, 2);
            }
        }

        builder.LoadInteger(third, 3);

        var binary = builder
            .Optimize(context =>
            {
                Assert.IsTrue(context.TryGetSourceRange(0, out var labelRange));
                Assert.AreEqual(outer, labelRange);
                Assert.IsTrue(context.TryGetInstruction(1, out var firstLoad));
                context.ReplaceInstruction(1, firstLoad with { I64 = 10 });
                Assert.IsTrue(context.TryGetSourceRange(1, out var rewrittenRange));
                Assert.AreEqual(outer, rewrittenRange);
                Assert.IsTrue(context.TryGetSourceRange(2, out var innerRange));
                Assert.AreEqual(inner, innerRange);
                Assert.IsTrue(context.TryGetSourceRange(3, out var emptyRange));
                Assert.IsNull(emptyRange);
            })
            .Build();

        Assert.HasCount(3, binary.InstructionTable);
        Assert.AreEqual(10L, binary.InstructionTable[0].I64);
        Assert.AreEqual(2L, binary.InstructionTable[1].I64);
        Assert.AreEqual(3L, binary.InstructionTable[2].I64);
    }
}
