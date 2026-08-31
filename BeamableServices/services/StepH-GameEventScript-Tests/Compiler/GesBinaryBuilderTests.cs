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
        Assert.HasCount(2, binary.StringConstants.Slices);
        Assert.AreEqual("same", binary.StringConstants.Resolve(0));
        Assert.HasCount(5, binary.Code.Instructions);

        var multiply = binary.Code.Instructions[4];
        Assert.AreEqual(GameEventScriptBytecodeOpCode.Multiply, multiply.OpCode);
        Assert.AreEqual((ushort)2, multiply.DestinationRegister);
        Assert.AreEqual((ushort)0, multiply.XRegister);
        Assert.AreEqual((ushort)1, multiply.YRegister);
    }

    [TestMethod]
    public void BuildEliminatesManySingleUseTemporaryMoves()
    {
        var builder = new GesBinaryBuilder().WithModuleName("BuilderMovePeepholeMany");
        const int Count = 256;
        var destinations = new GesRegisterRef[Count];

        for (var index = 0; index < Count; index++)
        {
            var destination = builder.AddRegister("value_" + index);
            var temporary = builder.AddTemporaryRegister();
            destinations[index] = destination;

            builder.LoadInteger(temporary, index);
            builder.Move(destination, temporary);
        }

        var binary = builder.Build();

        Assert.HasCount(Count, binary.Code.Instructions);
        for (var index = 0; index < Count; index++)
        {
            var instruction = binary.Code.Instructions[index];
            Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, instruction.OpCode);
            Assert.AreEqual(index, instruction.I64);
            Assert.AreEqual((ushort)index, instruction.DestinationRegister);
        }
    }

    [TestMethod]
    public void BuildScopesCreateHandlerBindAndPatchRegisterLocals()
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

        Assert.HasCount(1, binary.Bindings.Entries);
        var bind = binary.Bindings.Entries[0];
        Assert.AreEqual(GameEventScriptBinaryBindKind.MessageHandler, bind.Kind);
        Assert.AreEqual("Start", binary.StringConstants.Resolve(bind.Name));
        Assert.AreEqual((ushort)0, bind.EntryAddress);
        Assert.HasCount(1, bind.ArgumentNames);
        Assert.AreEqual("value", binary.StringConstants.Resolve(bind.ArgumentNames[0]));

        Assert.HasCount(4, binary.Code.Instructions);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.RegisterLocals, binary.Code.Instructions[0].OpCode);
        Assert.AreEqual((short)2, binary.Code.Instructions[0].Count);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, binary.Code.Instructions[1].OpCode);
        Assert.AreEqual((ushort)2, binary.Code.Instructions[1].DestinationRegister);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.Add, binary.Code.Instructions[2].OpCode);
        Assert.AreEqual((ushort)1, binary.Code.Instructions[2].DestinationRegister);
        Assert.AreEqual((ushort)0, binary.Code.Instructions[2].XRegister);
        Assert.AreEqual((ushort)2, binary.Code.Instructions[2].YRegister);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnVoid, binary.Code.Instructions[3].OpCode);
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

        Assert.HasCount(1, binary.Bindings.Entries);
        Assert.AreEqual(GameEventScriptBinaryBindKind.MessageHandler, binary.Bindings.Entries[0].Kind);
        Assert.AreEqual("Start", binary.StringConstants.Resolve(binary.Bindings.Entries[0].Name));
        Assert.AreEqual((ushort)0, binary.Bindings.Entries[0].EntryAddress);

        Assert.HasCount(6, binary.Code.Instructions);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.RegisterLocals, binary.Code.Instructions[0].OpCode);
        Assert.AreEqual((short)0, binary.Code.Instructions[0].Count);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnVoid, binary.Code.Instructions[1].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.RegisterLocals, binary.Code.Instructions[2].OpCode);
        Assert.AreEqual((short)2, binary.Code.Instructions[2].Count);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, binary.Code.Instructions[3].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.Add, binary.Code.Instructions[4].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnValue, binary.Code.Instructions[5].OpCode);
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

        Assert.HasCount(1, binary.Bindings.Entries);
        Assert.AreEqual(GameEventScriptBinaryBindKind.MessageHandler, binary.Bindings.Entries[0].Kind);
        Assert.AreEqual("Start", binary.StringConstants.Resolve(binary.Bindings.Entries[0].Name));
        Assert.AreEqual((ushort)0, binary.Bindings.Entries[0].EntryAddress);

        Assert.HasCount(6, binary.Code.Instructions);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.RegisterLocals, binary.Code.Instructions[0].OpCode);
        Assert.AreEqual((short)1, binary.Code.Instructions[0].Count);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.Call, binary.Code.Instructions[1].OpCode);
        Assert.AreEqual((ushort)0, binary.Code.Instructions[1].DestinationRegister);
        Assert.AreEqual((ushort)3, binary.Code.Instructions[1].TargetAddress);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnVoid, binary.Code.Instructions[2].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.RegisterLocals, binary.Code.Instructions[3].OpCode);
        Assert.AreEqual((short)1, binary.Code.Instructions[3].Count);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, binary.Code.Instructions[4].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnValue, binary.Code.Instructions[5].OpCode);
    }

    [TestMethod]
    public void BuildCalculatesHandlerAndProgramResourceRequirementsAcrossCallGraph()
    {
        var builder = new GesBinaryBuilder().WithModuleName("BuilderResources");

        using (var handler = builder.BeginHandler("Start"))
        {
            var handlerResult = handler.AddRegister("handlerResult");
            GesLabelRef firstHelperEntry;

            using (var firstHelper = builder.BeginHelper("firstHelper"))
            {
                firstHelperEntry = firstHelper.EntryLabel;
                var firstResult = firstHelper.AddRegister("firstResult");
                GesLabelRef secondHelperEntry;

                using (var secondHelper = builder.BeginHelper("secondHelper"))
                {
                    secondHelperEntry = secondHelper.EntryLabel;
                    var secondResult = secondHelper.AddRegister("secondResult");
                    builder.LoadInteger(secondResult, 7);
                    builder.ReturnValue(secondResult);
                }

                builder.Call(firstResult, secondHelperEntry);
                builder.ReturnValue(firstResult);
            }

            builder.Call(handlerResult, firstHelperEntry);
            builder.ReturnVoid();
        }

        var program = builder.Build();
        var handlerBind = program.Bindings.Entries[0];

        Assert.AreEqual((ushort)3, program.RequiredRegisterCount);
        Assert.AreEqual((ushort)2, program.RequiredCallStackDepth);
        Assert.AreEqual((ushort)3, handlerBind.RequiredRegisterCount);
        Assert.AreEqual((ushort)2, handlerBind.RequiredCallStackDepth);
    }

    [TestMethod]
    public void BuildRejectsDirectRecursiveCall()
    {
        var builder = new GesBinaryBuilder().WithModuleName("DirectRecursion");
        using (var handler = builder.BeginHandler("Start"))
        {
            var result = handler.AddRegister("result");
            builder.Call(result, handler.EntryLabel);
            builder.ReturnVoid();
        }

        var exception = Assert.ThrowsExactly<GameEventScriptCompileException>(() => builder.Build());
        StringAssert.Contains(exception.Message, "Start -> Start");
    }

    [TestMethod]
    public void BuildRejectsIndirectRecursiveCall()
    {
        var builder = new GesBinaryBuilder().WithModuleName("IndirectRecursion");
        using (var handler = builder.BeginHandler("Start"))
        {
            var handlerResult = handler.AddRegister("handlerResult");
            GesLabelRef helperEntry;
            using (var helper = builder.BeginHelper("helper"))
            {
                helperEntry = helper.EntryLabel;
                var helperResult = helper.AddRegister("helperResult");
                builder.Call(helperResult, handler.EntryLabel);
                builder.ReturnValue(helperResult);
            }

            builder.Call(handlerResult, helperEntry);
            builder.ReturnVoid();
        }

        var exception = Assert.ThrowsExactly<GameEventScriptCompileException>(() => builder.Build());
        StringAssert.Contains(exception.Message, "Start -> helper -> Start");
    }

    [TestMethod]
    public void HostLoadRejectsCyclicCallGraphFromUntrustedProgramData()
    {
        var builder = new GesBinaryBuilder().WithModuleName("UntrustedCycle");
        using (builder.BeginHandler("Start"))
        {
            builder.ReturnVoid();
        }

        var validProgram = builder.Build();
        var instructions = validProgram.Code.Instructions.ToArray();
        instructions[^1] = new GameEventScriptBytecodeInstruction
        {
            OpCode = GameEventScriptBytecodeOpCode.Call,
            DestinationRegister = 0,
            TargetAddress = validProgram.Bindings.Entries[0].EntryAddress
        };
        var originalBind = validProgram.Bindings.Entries[0];
        var bindings = new GameEventScriptBindingSegment([
            new GameEventScriptBindingSegment.GameEventScriptBinaryBindEntry(
                originalBind.Kind, originalBind.Name, originalBind.ArgumentNames, originalBind.EntryAddress, originalBind.Id,
                originalBind.RequiredTags, originalBind.ExcludedTags, requiredRegisterCount: 1, originalBind.RequiredCallStackDepth)
        ]);
        var untrustedProgram = new GameEventScriptProgram(
            validProgram.FormatVersion,
            validProgram.ModuleName,
            validProgram.ProgramVersion,
            1,
            validProgram.RequiredCallStackDepth,
            validProgram.StringConstants,
            validProgram.UInt16IndexLists,
            bindings,
            new GameEventScriptCodeSegment(instructions),
            buildMetadataSegment: validProgram.BuildMetadata);

        var host = GameEventScriptHost.CreateBuilder().Build();
        var exception = Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => host.Load(untrustedProgram));
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.CyclicCallGraph, exception.ErrorCode);
        StringAssert.Contains(exception.Message, "Start -> Start");
    }

    [TestMethod]
    public void HostLoadRejectsProgramResourceRequirementsAboveLimits()
    {
        var callDepthBuilder = new GesBinaryBuilder().WithModuleName("CallDepthLimit");
        using (var handler = callDepthBuilder.BeginHandler("Start"))
        {
            var result = handler.AddRegister("result");
            GesLabelRef helperEntry;
            using (var helper = callDepthBuilder.BeginHelper("helper"))
            {
                helperEntry = helper.EntryLabel;
                var helperResult = helper.AddRegister("helperResult");
                GesLabelRef nestedEntry;
                using (var nested = callDepthBuilder.BeginHelper("nested"))
                {
                    nestedEntry = nested.EntryLabel;
                    var nestedResult = nested.AddRegister("nestedResult");
                    callDepthBuilder.LoadInteger(nestedResult, 1);
                    callDepthBuilder.ReturnValue(nestedResult);
                }

                callDepthBuilder.Call(helperResult, nestedEntry);
                callDepthBuilder.ReturnValue(helperResult);
            }

            callDepthBuilder.Call(result, helperEntry);
            callDepthBuilder.ReturnVoid();
        }

        var callDepthHost = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxCallDepth = 1 })
            .Build();
        Assert.ThrowsExactly<GameEventScriptDynamicLinkException>(() => callDepthHost.Load(callDepthBuilder.Build()));

        var registerBuilder = new GesBinaryBuilder().WithModuleName("RegisterLimit");
        using (var handler = registerBuilder.BeginHandler("Start"))
        {
            for (var index = 0; index < 3; index++) handler.AddRegister("value" + index);
            registerBuilder.ReturnVoid();
        }

        var registerHost = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxRegisterValues = 2 })
            .Build();
        Assert.ThrowsExactly<GameEventScriptDynamicLinkException>(() => registerHost.Load(registerBuilder.Build()));
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
                var load = context.GetInstruction(0);
                Assert.IsNotNull(load);
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

        Assert.HasCount(3, binary.Code.Instructions);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, binary.Code.Instructions[0].OpCode);
        Assert.AreEqual((ushort)0, binary.Code.Instructions[0].DestinationRegister);
        Assert.AreEqual(2L, binary.Code.Instructions[0].I64);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, binary.Code.Instructions[1].OpCode);
        Assert.AreEqual((ushort)1, binary.Code.Instructions[1].DestinationRegister);
        Assert.AreEqual(3L, binary.Code.Instructions[1].I64);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReturnVoid, binary.Code.Instructions[2].OpCode);
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
                var labelRange = context.GetSourceRange(0);
                Assert.AreEqual(outer, labelRange);
                var firstLoad = context.GetInstruction(1);
                Assert.IsNotNull(firstLoad);
                context.ReplaceInstruction(1, firstLoad with { I64 = 10 });
                var rewrittenRange = context.GetSourceRange(1);
                Assert.AreEqual(outer, rewrittenRange);
                var innerRange = context.GetSourceRange(2);
                Assert.AreEqual(inner, innerRange);
                var emptyRange = context.GetSourceRange(3);
                Assert.IsNull(emptyRange);
            })
            .Build();

        Assert.HasCount(3, binary.Code.Instructions);
        Assert.AreEqual(10L, binary.Code.Instructions[0].I64);
        Assert.AreEqual(2L, binary.Code.Instructions[1].I64);
        Assert.AreEqual(3L, binary.Code.Instructions[2].I64);
    }
}
