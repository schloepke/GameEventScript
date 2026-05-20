using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using System.Runtime.InteropServices;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.BytecodeVM;

[TestClass]
public sealed class GesBytecodeVmExecutableBuilderTests
{
    [TestMethod]
    public void DebugDumpIsDeterministic()
    {
        const string script =
            """
            module Dump

            on Start(value) {
              let total be value + 1
              if total > 1 {
                let done be Done(total: total)
                emit Done(total: total)
              }
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var first = compiled.DumpBytecode();
        var second = GameEventScriptManager.Compile(script).DumpBytecode();

        Assert.AreEqual(first, second);
        StringAssert.Contains(first, "gameeventscript bytecode v1");
        StringAssert.Contains(first, "maxFrameSlots:");
        StringAssert.Contains(first, "code[");
        StringAssert.Contains(first, "@0000");
        StringAssert.Contains(first, "handlers[1]");
        StringAssert.Contains(first, "ReserveSlots");
        StringAssert.Contains(first, "LoadInteger");
        StringAssert.Contains(first, "MoveSlot");
        StringAssert.Contains(first, "BuildMessage");
        StringAssert.Contains(first, "EmitMessage");
        Assert.IsFalse(first.Contains("maxStackDepth", StringComparison.Ordinal));
        Assert.IsFalse(first.Contains("nestedExpression", StringComparison.Ordinal));
        Assert.IsNotEmpty(compiled.Code);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReserveSlots, compiled.Code[compiled.Handlers["Start"][0].EntryAddress].OpCode);
        Assert.IsGreaterThanOrEqualTo(1, compiled.Code[compiled.Handlers["Start"][0].EntryAddress].A_U16);
        Assert.IsGreaterThanOrEqualTo(compiled.Handlers["Start"][0].EntryAddress, 0);
        Assert.IsFalse(first.Contains("EvaluateExpression", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PublicLinearBytecodePreloadsHandlerArgumentsAndReservesOnlyLocals()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              emit Done(value: value)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var handler = compiled.Handlers["Start"][0];
        var prolog = compiled.Code[handler.EntryAddress];

        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReserveSlots, prolog.OpCode);
        Assert.AreEqual((ushort)0, prolog.A_U16);
        Assert.IsFalse(compiled.Code.Any(instruction => (byte)instruction.OpCode == 0xA0), "EnterScope must not be emitted.");

        var dump = compiled.DumpBytecode();
        Assert.IsFalse(dump.Contains("BindParameter", StringComparison.Ordinal));
        Assert.IsFalse(dump.Contains("EnterScope", StringComparison.Ordinal));
        Assert.IsFalse(dump.Contains("ExitScope", StringComparison.Ordinal));
        StringAssert.Contains(dump, "locals+=");
    }

    [TestMethod]
    public void PublicLinearBytecodeLowersImplicationWithBranchingTriStateShape()
    {
        const string script =
            """
            module ShortCircuit

            on Start(missing) {
              let skipped be false -> missing
              let resolved be missing -> true
              emit Done(skipped: skipped, resolved: resolved)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.JumpIfFalse));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.Implies &&
            instruction.Dest_U16 >= 0 &&
            instruction.A_U16 == instruction.B_U16));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.Implies &&
            instruction.Dest_U16 >= 0 &&
            instruction.A_U16 != instruction.B_U16));
    }

    [TestMethod]
    public void PublicLinearBytecodeUsesGenericCastTypeCheckOpcodesAndDedicatedPercentageLoads()
    {
        const string script =
            """
            module Quantities

            on Start(value) {
              let distance be value as :quantity(m)
              let isMeter be distance is :quantity(m)
              let isSecond be distance is :quantity(s)
              let ratio be 50%
              emit Done(distance: distance, isMeter: isMeter, isSecond: isSecond, ratio: ratio)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.CastUnit &&
            instruction.UnitAndFlags == (byte)GameEventScriptBytecodeInstructionUnit.UnitMeter));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.CheckUnit &&
            instruction.UnitAndFlags == (byte)GameEventScriptBytecodeInstructionUnit.UnitMeter));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.CheckUnit &&
            instruction.UnitAndFlags == (byte)GameEventScriptBytecodeInstructionUnit.UnitSecond));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadPercentage));
        Assert.IsFalse(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.LoadFloat &&
            instruction.UnitAndFlags is not 0 and not (byte)GameEventScriptBytecodeInstructionUnit.UnitDegree and
                not (byte)GameEventScriptBytecodeInstructionUnit.UnitMeter and
                not (byte)GameEventScriptBytecodeInstructionUnit.UnitSecond));
    }

    [TestMethod]
    public void PublicLinearBytecodeStoresPublishMetadataInUShortListPool()
    {
        const string script =
            """
            module SideTables

            on Start(value) {
              let msg be Done(value: value)
              emit msg with :radio
              emit Done(value: value + 1) with :local
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var direct = compiled.Code.First(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.EmitMessageWithTags);
        Assert.IsLessThan(compiled.UShortListPool.Count, direct.A_U16);
        Assert.IsLessThan(compiled.UShortListPool.Count, direct.B_U16);
        Assert.IsLessThan(compiled.UShortListPool.Count, direct.C_U16);
        var shape = compiled.UShortListPool[direct.A_U16].Select(index => compiled.StringPool[index]).ToArray();
        CollectionAssert.AreEqual(new[] { "Done", "value" }, shape);
        Assert.HasCount(1, compiled.OutboundMessageSignatures);
        Assert.AreEqual(direct.A_U16, compiled.OutboundMessageSignatures[0]);
        Assert.HasCount(1, compiled.UShortListPool[direct.B_U16]);
        Assert.HasCount(1, compiled.UShortListPool[direct.C_U16]);
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.EmitMessageValueWithTags &&
            instruction.C_U16 < compiled.UShortListPool.Count));
        var build = compiled.Code.First(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildMessage);
        var buildShape = compiled.UShortListPool[build.A_U16].Select(index => compiled.StringPool[index]).ToArray();
        CollectionAssert.AreEqual(new[] { "Done", "value" }, buildShape);
        Assert.HasCount(1, compiled.UShortListPool[build.B_U16]);
    }

    [TestMethod]
    public void PublicLinearBytecodeStoresLoopIteratorsAndLinearRandomScopes()
    {
        const string script =
            """
            module SideTables

            on Start {
              for item from 1 to 3 emit Tick(value: item)
              :random with 7 {
                emit Done(value: :random from 1 to 6)
              }
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RangeIteratorShort));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.IteratorNext));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.IteratorClose));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPushConstant));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPop));
    }

    [TestMethod]
    public void PublicLinearBytecodeUsesIteratorOpcodesForDynamicForSources()
    {
        const string script =
            """
            module LoopIterators

            on Start(begin, finish, step) {
              for item from begin to finish emit RangeItem(value: item)
              for item from begin to finish step step emit StepItem(value: item)
              let items be [1, 2, 3]
              for item in items emit CollectionItem(value: item)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RangeIterator));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RangeIteratorWithStep));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CollectionIterator));
        Assert.IsGreaterThanOrEqualTo(3, compiled.Code.Count(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.IteratorNext));
        Assert.IsGreaterThanOrEqualTo(3, compiled.Code.Count(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.IteratorClose));
    }

    [TestMethod]
    public void BytecodeVmRangeForLoopContinuesAtIteratorNext()
    {
        const string script =
            """
            module LoopRuntime

            on Start {
              for item from 1 to 3 emit Tick(value: item)
              emit Done(value: 99)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxExecutionSteps = 200 })
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start"));

        CollectionAssert.AreEqual(new[] { "Tick", "Tick", "Tick", "Done" }, published.Select(message => message.Name).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                GameEventScriptValueFactory.GesInteger(1),
                GameEventScriptValueFactory.GesInteger(2),
                GameEventScriptValueFactory.GesInteger(3),
                GameEventScriptValueFactory.GesInteger(99)
            },
            published.Select(message => message.Arguments["value"]).ToArray());
    }

    [TestMethod]
    public void PublicLinearBytecodeUsesDynamicRandomPushForExplicitIntegerSeed()
    {
        const string script =
            """
            module RandomScopes

            on Start(seed) {
              let value be :random with (seed as :integer) 1
              emit Done(value: value)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPush));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPop));
        Assert.IsFalse(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPushConstant));
    }

    [TestMethod]
    public void PublicLinearBytecodeStoresPipelineMetadataInSideTables()
    {
        const string script =
            """
            module SideTables

            on Start(values) {
              let selected be values[:select item => item + 1]
              emit Done(count: :len selected)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineIterator &&
            instruction.B_U16 > 0));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineCollectList));
    }

    [TestMethod]
    public void BytecodeVmExecutableBuilderBuildsLinearExecutableFromPublicBytecode()
    {
        const string script =
            """
            module LinearExecutable

            function boosted(_ value) means value + 2

            on Start(value) {
              let total be boosted(value)
              emit Done(value: total) with :local
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var executable = GesBytecodeVmExecutableBuilder.Build(compiled);
        var handler = compiled.Handlers["Start"][0];
        var handlerSignatureId = GameEventScriptMessageSignature.CreateSignatureId(handler.Message, handler.SignatureLabels);
        var linearHandler = executable.LinearExecutable.Handlers.Single(entry => entry.SignatureId == handlerSignatureId);
        var callable = compiled.Callables["boosted"];
        var callableSignatureId = GameEventScriptMessageSignature.CreateSignatureId(callable.Name, callable.SignatureLabels);
        var linearCallable = executable.LinearExecutable.Callables.Single(entry => entry.SignatureId == callableSignatureId);

        Assert.HasCount(compiled.Code.Count, executable.LinearExecutable.Code);
        Assert.AreEqual(compiled.MaxFrameSlots, executable.LinearExecutable.MaxFrameSlots);
        Assert.AreEqual(handler.EntryAddress, linearHandler.EntryAddress);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReserveSlots, executable.LinearExecutable.Code[linearHandler.EntryAddress].OpCode);
        Assert.AreEqual(executable.LinearExecutable.Code[linearHandler.EntryAddress].A_U16, linearHandler.LocalSlotCount);
        Assert.AreEqual(callable.EntryAddress, linearCallable.EntryAddress);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReserveSlots, executable.LinearExecutable.Code[linearCallable.EntryAddress].OpCode);
        Assert.AreEqual(executable.LinearExecutable.Code[linearCallable.EntryAddress].A_U16, linearCallable.LocalSlotCount);
        Assert.AreEqual(callable.ReturnSlot, linearCallable.ReturnSlot);
        Assert.IsTrue(executable.LinearExecutable.Code.Any(instruction =>
            instruction.OpCode is GameEventScriptBytecodeOpCode.EmitMessage or GameEventScriptBytecodeOpCode.EmitMessageWithTags &&
            instruction.A_U16 < compiled.UShortListPool.Count));
    }

    [TestMethod]
    public void PublicLinearBytecodeStagesArgumentsForMultiArgumentPredicateCalls()
    {
        const string script =
            """
            module LinearExecutable

            predicate higher(left, right) means left > right

            on Start(left, right) {
              emit Done(ok: higher(left: left, right: right))
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var predicateInstruction = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallPredicate);
        Assert.AreEqual(compiled.Callables["higher"].EntryAddress, predicateInstruction.A_U16);
        Assert.AreEqual((ushort)0, predicateInstruction.B_U16);

        var predicateIndex = Array.FindIndex(compiled.Code.ToArray(), instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallPredicate);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.StageRegister, compiled.Code[predicateIndex - 2].OpCode);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.StageRegister, compiled.Code[predicateIndex - 1].OpCode);
    }

    [TestMethod]
    public void PublicLinearBytecodeStagesLiteralArgumentsWithImmediateOpcodes()
    {
        const string script =
            """
            module LinearExecutable

            function add(left, right) means left + right

            on Start {
              emit Done(value: add(left: 1, right: 2))
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var callInstruction = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Call);
        Assert.AreEqual(compiled.Callables["add"].EntryAddress, callInstruction.A_U16);
        Assert.AreEqual((ushort)0, callInstruction.B_U16);

        var callIndex = Array.FindIndex(compiled.Code.ToArray(), instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Call);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.StageInteger, compiled.Code[callIndex - 2].OpCode);
        Assert.AreEqual(1L, compiled.Code[callIndex - 2].I64);
        Assert.AreEqual(GameEventScriptBytecodeOpCode.StageInteger, compiled.Code[callIndex - 1].OpCode);
        Assert.AreEqual(2L, compiled.Code[callIndex - 1].I64);
    }

    [TestMethod]
    public void BytecodeVmLowersStandardExtensionCallsToDirectStandardCallOpcodesWithSlotLists()
    {
        const string script =
            """
            module LinearExecutable

            on Start(heading) {
              let fib be :series.fibonacci()
              let wrapped be :degree.wrap(heading)
              emit Done(fibIsSeries: fib is :series, wrapped: wrapped)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var standardCalls = compiled.Code
            .Where(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallStandard)
            .ToArray();

        Assert.HasCount(2, standardCalls);
        Assert.HasCount(0, compiled.ExternalReferences);
        foreach (var instruction in standardCalls)
        {
            var shape = compiled.UShortListPool[instruction.A_U16];
            var argumentSlots = compiled.UShortListPool[instruction.B_U16];
            Assert.IsGreaterThanOrEqualTo(2, shape.Count);
            Assert.HasCount(shape.Count - 2, argumentSlots);
        }
    }

    [TestMethod]
    public void BytecodeVmLowersHostExtensionCallsToDirectExternalCallOpcodesWithSlotLists()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value, heading) {
              let floored be :math.floor(value)
              let north be heading is :nav.isNorth
              emit Done(floored: floored, north: north)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var functionCall = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallExternal);
        var predicateCall = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallExternalPredicate);

        Assert.HasCount(2, compiled.ExternalReferences);
        Assert.HasCount(
            compiled.ExternalReferences[functionCall.A_U16].ArgumentLabels.Count,
            compiled.UShortListPool[functionCall.B_U16]);
        Assert.HasCount(
            compiled.ExternalReferences[predicateCall.A_U16].ArgumentLabels.Count,
            compiled.UShortListPool[predicateCall.B_U16]);
    }

    [TestMethod]
    public void PublicLinearBytecodeStoresPipelineSelectorHelperEntryAddresses()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be [1, 2, 3][:select item => item + 1]
              emit Done(first: values[1], replacement: 2)
            }
        """;

        var compiled = GameEventScriptManager.Compile(script);
        var selector = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineIterator);
        var entryAddress = selector.B_U16;
        Assert.IsGreaterThanOrEqualTo(0, entryAddress);
        Assert.IsTrue(compiled.Code
            .Skip(entryAddress)
            .TakeWhile(instruction => instruction.OpCode != GameEventScriptBytecodeOpCode.ReturnValue)
            .Any(instruction => instruction.OpCode is GameEventScriptBytecodeOpCode.Add or GameEventScriptBytecodeOpCode.IntAdd));
    }

    [TestMethod]
    public void PipelineSelectorHelperEntriesDoNotInheritLaterCallerPeakSlots()
    {
        const string script =
            """
            module LinearExecutable

            predicate high(_ value) means value > 3

            on Start(value) {
              let filtered be [1, 2, 3, 4][:filter item where item is high]
              let later be [10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21]
              emit Done(filtered: filtered, later: later[1])
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var selector = compiled.Code.First(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineIterator);
        var helperEntry = selector.B_U16;

        Assert.AreEqual(GameEventScriptBytecodeOpCode.ReserveSlots, compiled.Code[helperEntry].OpCode);
        Assert.AreEqual(1, compiled.Code[helperEntry].A_U16);
    }

    [TestMethod]
    public void BytecodeVmExecutesPipelineSelectorExpressionsFromLinearHandlerEntryAddress()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be [1, 2, 3][:select item => item + 1]
              emit Done(first: values[1], replacement: 2)
            }
        """;

        var compiled = GameEventScriptManager.Compile(script);
        var selector = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineIterator);
        var entryAddress = selector.B_U16;
        Assert.IsGreaterThanOrEqualTo(0, entryAddress);

        var originalConstant = 1L;
        var replacementConstant = 2L;
        var selectorEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            entryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.ReturnValue) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= entryAddress &&
                                           index < selectorEnd &&
                                           IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["first"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesNonFastPipelineSelectorExpressionsFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be [[1], [2]][:select item => item[1] + 5]
              emit Done(first: values[1], replacement: 6)
            }
        """;

        var compiled = GameEventScriptManager.Compile(script);
        var selector = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineIterator);
        var entryAddress = selector.B_U16;
        Assert.IsGreaterThanOrEqualTo(0, entryAddress);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var selectorEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            entryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.ReturnValue) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= entryAddress &&
                                           index < selectorEnd &&
                                           IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["first"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let blocker be [10, 11, 12][:first item where item > 0]
              let first be ([1, 2, 3][:select item => item + 1])[1] + 4
              emit Done(first: first, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineIterator));

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(8), published[0].Arguments["first"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let blocker be [10, 11, 12][:first item where item > 0]
              let first be ([1, 2, 3][:select item => item + 1])[1] + 4
              emit Done(first: first, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineIterator));

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(8), published[0].Arguments["first"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesNumericPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let total be ([1, 2, 3][:sum item => item] + [8, 10, 12][:average item => item]) + 7
              emit Done(total: total, replacement: 9)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 7L;
        var replacementConstant = 9L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(25d), published[0].Arguments["total"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(9), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsNumericPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let total be ([1, 2, 3][:sum item => item] + [8, 10, 12][:average item => item]) + 7
              emit Done(total: total, replacement: 9)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 7L;
        var replacementConstant = 9L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(25d), published[0].Arguments["total"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(9), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesPredicatePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let score be (1 when [1, 2, 3][:any item where item > 2] and [1, 2, 3][:all item where item < 4], otherwise 0) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsPredicatePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let score be (1 when [1, 2, 3][:any item where item > 2] and [1, 2, 3][:all item where item < 4], otherwise 0) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmShortCircuitsLinearPredicatePipelineRangeExpressions()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let score be (1 when (from 1 to 100)[:any item where item = 1], otherwise 0) + 5
              emit Done(score: score)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxRangeItems = 1 })
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["score"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesEdgePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let score be ([1, 2, 3][:last] + [1, 2, 3][:last item where item < 3] + [1, 2, 3][:single item where item = 2]) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(13), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsEdgePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let score be ([1, 2, 3][:last] + [1, 2, 3][:last item where item < 3] + [1, 2, 3][:single item where item = 2]) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(13), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesExtremaPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let score be ([3, 1, 2][:min item => item] + [3, 1, 2][:max item => item]) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsExtremaPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let score be ([3, 1, 2][:min item => item] + [3, 1, 2][:max item => item]) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesContainsPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let score be (1 when [1, 2, 3][:contains 2] and [1, 2, 3][:contains all [1, 3]] and [1, 2, 3][:contains any [0, 3]], otherwise 0) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsContainsPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let score be (1 when [1, 2, 3][:contains 2] and [1, 2, 3][:contains all [1, 3]] and [1, 2, 3][:contains any [0, 3]], otherwise 0) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesPrefixedContainsPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let single be [1, 2, 3][:filter item where item > 1][:select item => item + 10][:contains 99]
              let allValues be [1, 2, 3][:filter item where item > 1][:select item => item + 10][:contains all [12, 13]]
              let anyValues be [1, 2, 3][:filter item where item > 1][:select item => item + 10][:contains any [99, 13]]
              let score be (1 when single and allValues and anyValues, otherwise 0) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalNeedle = 99L;
        var replacementNeedle = 13L;
        var originalAddend = 5L;
        var replacementAddend = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalNeedle)
                ? WithIntegerLoad(instruction, replacementNeedle)
                : IsIntegerLoad(instruction, originalAddend)
                    ? WithIntegerLoad(instruction, replacementAddend)
                    : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsPrefixedContainsPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let single be [1, 2, 3][:filter item where item > 1][:select item => item + 10][:contains 99]
              let allValues be [1, 2, 3][:filter item where item > 1][:select item => item + 10][:contains all [12, 13]]
              let anyValues be [1, 2, 3][:filter item where item > 1][:select item => item + 10][:contains any [99, 13]]
              let score be (1 when single and allValues and anyValues, otherwise 0) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalNeedle = 99L;
        var replacementNeedle = 13L;
        var originalAddend = 5L;
        var replacementAddend = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalNeedle)
                ? WithIntegerLoad(instruction, replacementNeedle)
                : IsIntegerLoad(instruction, originalAddend)
                    ? WithIntegerLoad(instruction, replacementAddend)
                    : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmChecksDirectLinearContainsPipelineRangeExpressionsWithoutMaterializingRange()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let score be (1 when (from 1 to 100)[:contains 100], otherwise 0) + 5
              emit Done(score: score)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxRangeItems = 1 })
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["score"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesDictionaryPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let units be [[id: :rook, value: 1], [id: :mage, value: 2], [id: :rook, value: 3]]
              let score be (units[:map unit by unit.id][:rook].value + units[:map unit by unit.id => unit.value][:mage]) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsDictionaryPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let units be [[id: :rook, value: 1], [id: :mage, value: 2], [id: :rook, value: 3]]
              let score be (units[:map unit by unit.id][:rook].value + units[:map unit by unit.id => unit.value][:mage]) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesDistinctPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be [1, 2, 2, 3, 1]
              let units be [[faction: :melee, value: 1], [faction: :ranged, value: 2], [faction: :melee, value: 3]]
              let uniqueValues be values[:distinct]
              let uniqueUnits be units[:distinct by unit => unit.faction]
              let score be (uniqueValues[3] + uniqueUnits[2].value) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsDistinctPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be [1, 2, 2, 3, 1]
              let units be [[faction: :melee, value: 1], [faction: :ranged, value: 2], [faction: :melee, value: 3]]
              let uniqueValues be values[:distinct]
              let uniqueUnits be units[:distinct by unit => unit.faction]
              let score be (uniqueValues[3] + uniqueUnits[2].value) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesGroupByPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let units be [[faction: :melee, value: 1], [faction: :ranged, value: 2], [faction: :melee, value: 3]]
              let groups be units[:group by unit => unit.faction]
              let score be (groups[:melee][2].value + groups[:ranged][1].value) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsGroupByPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let units be [[faction: :melee, value: 1], [faction: :ranged, value: 2], [faction: :melee, value: 3]]
              let groups be units[:group by unit => unit.faction]
              let score be (groups[:melee][2].value + groups[:ranged][1].value) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 4L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesReversePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let reversed be [1, 2, 3][:reverse]
              let filteredReversed be [1, 2, 3, 4][:filter value where value > 1][:reverse]
              let invalidReverse be 123[:reverse]
              let score be (reversed[1] + filteredReversed[3]) + 5
              emit Done(score: score, replacement: 6, invalidReverse: invalidReverse)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["invalidReverse"]);
    }

    [TestMethod]
    public void BytecodeVmStepsReversePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let reversed be [1, 2, 3][:reverse]
              let filteredReversed be [1, 2, 3, 4][:filter value where value > 1][:reverse]
              let invalidReverse be 123[:reverse]
              let score be (reversed[1] + filteredReversed[3]) + 5
              emit Done(score: score, replacement: 6, invalidReverse: invalidReverse)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["invalidReverse"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesSortPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let ascending be [3, 1, 2][:sort ascending]
              let descending be [3, 1, 2][:sort descending]
              let prefixed be [1, 2, 3, 4][:filter value where value > 1][:select value => value * -1][:sort ascending]
              let sortedSet be [3, 1, 2][:sort descending]
              let score be (ascending[1] + descending[1] + prefixed[1] + sortedSet[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(9), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsSortPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let ascending be [3, 1, 2][:sort ascending]
              let descending be [3, 1, 2][:sort descending]
              let prefixed be [1, 2, 3, 4][:filter value where value > 1][:select value => value * -1][:sort ascending]
              let sortedSet be [3, 1, 2][:sort descending]
              let score be (ascending[1] + descending[1] + prefixed[1] + sortedSet[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(9), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesOrderByPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let units be [[priority: 3, value: 1], [priority: 1, value: 2], [priority: 2, value: 3]]
              let ascending be units[:order by unit => unit.priority ascending]
              let descending be units[:order by unit => unit.priority descending]
              let prefixed be units[:filter unit where unit.priority > 1][:order by unit => unit.value descending]
              let orderedSet be [3, 1, 2][:order by item => item descending]
              let score be (ascending[1].value + descending[1].value + prefixed[1].value + orderedSet[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(15), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsOrderByPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let units be [[priority: 3, value: 1], [priority: 1, value: 2], [priority: 2, value: 3]]
              let ascending be units[:order by unit => unit.priority ascending]
              let descending be units[:order by unit => unit.priority descending]
              let prefixed be units[:filter unit where unit.priority > 1][:order by unit => unit.value descending]
              let orderedSet be [3, 1, 2][:order by item => item descending]
              let score be (ascending[1].value + descending[1].value + prefixed[1].value + orderedSet[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(15), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesSequenceSlicePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be [1, 2, 3, 4]
              let firstTwo be values[:take first 2]
              let withoutLast be values[:drop last 1]
              let highestTwo be values[:take highest 2]
              let droppedLowest be values[:drop lowest 1]
              let prefixed be values[:filter value where value > 1][:take lowest 2]
              let setTaken be [1, 2, 3][:take highest 2]
              let score be (firstTwo[2] + withoutLast[3] + highestTwo[1] + droppedLowest[1] + prefixed[2] + setTaken[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(23), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsSequenceSlicePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be [1, 2, 3, 4]
              let firstTwo be values[:take first 2]
              let withoutLast be values[:drop last 1]
              let highestTwo be values[:take highest 2]
              let droppedLowest be values[:drop lowest 1]
              let prefixed be values[:filter value where value > 1][:take lowest 2]
              let setTaken be [1, 2, 3][:take highest 2]
              let score be (firstTwo[2] + withoutLast[3] + highestTwo[1] + droppedLowest[1] + prefixed[2] + setTaken[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(23), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesShufflePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let shuffled be [1, 2, 3, 4][:shuffle]
              let prefixed be [1, 2, 3, 4][:filter value where value > 1][:shuffle]
              let invalidShuffle be 123[:shuffle]
              let score be (:len shuffled + :len prefixed) + 5
              emit Done(score: score, replacement: 6, invalidShuffle: invalidShuffle)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(13), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["invalidShuffle"]);
    }

    [TestMethod]
    public void BytecodeVmStepsShufflePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let shuffled be [1, 2, 3, 4][:shuffle]
              let prefixed be [1, 2, 3, 4][:filter value where value > 1][:shuffle]
              let invalidShuffle be 123[:shuffle]
              let score be (:len shuffled + :len prefixed) + 5
              emit Done(score: score, replacement: 6, invalidShuffle: invalidShuffle)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(13), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["invalidShuffle"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesSeriesPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let naturals be :series.natural()
              let term be naturals[:term 7]
              let dropped be naturals[:drop first 3]
              let droppedTerm be dropped[:term 0]
              let firstFour be naturals[:take first 4]
              let invalidTerm be naturals[:term 'x']
              let invalidSlice be naturals[:take last 1]
              let score be term + droppedTerm + firstFour[4]
              emit Done(score: score, replacement: 8, invalidTerm: invalidTerm, invalidSlice: invalidSlice, droppedIsSeries: dropped is :series)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 7L;
        var replacementConstant = 8L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(14), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(8), published[0].Arguments["replacement"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["invalidTerm"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["invalidSlice"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["droppedIsSeries"]);
    }

    [TestMethod]
    public void BytecodeVmStepsSeriesPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let naturals be :series.natural()
              let term be naturals[:term 7]
              let dropped be naturals[:drop first 3]
              let droppedTerm be dropped[:term 0]
              let firstFour be naturals[:take first 4]
              let invalidTerm be naturals[:term 'x']
              let invalidSlice be naturals[:take last 1]
              let score be term + droppedTerm + firstFour[4]
              emit Done(score: score, replacement: 8, invalidTerm: invalidTerm, invalidSlice: invalidSlice, droppedIsSeries: dropped is :series)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 7L;
        var replacementConstant = 8L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(14), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(8), published[0].Arguments["replacement"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["invalidTerm"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["invalidSlice"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["droppedIsSeries"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesPatternPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let cards be ['Ace', 'Ace', 'King']
              let objects be [[kind: 'bot', stats: [team: 'red']], [kind: 'item', stats: [team: 'blue']]]
              let hasPair be cards[:has pair of 'Ten']
              let takenPair be cards[:take pair of 'Queen']
              let objectMatch be objects[:has [kind: 'player', stats: [team: 'green']]]
              let score be (1 when hasPair, otherwise 0) + :len takenPair + (10 when objectMatch, otherwise 0)
              emit Done(score: score, hasPair: hasPair, takenPairLen: :len takenPair, objectMatch: objectMatch)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var replacements = new Dictionary<int, int>
        {
            [FindTextConstant(compiled, "Ten")] = FindTextConstant(compiled, "Ace"),
            [FindTextConstant(compiled, "Queen")] = FindTextConstant(compiled, "Ace"),
            [FindTextConstant(compiled, "player")] = FindTextConstant(compiled, "bot"),
            [FindTextConstant(compiled, "green")] = FindTextConstant(compiled, "red")
        };
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadText &&
                                   replacements.TryGetValue(instruction.C_U16, out var replacement)
                ? instruction with { C_U16 = (ushort)replacement }
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(13), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["hasPair"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["takenPairLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["objectMatch"]);
    }

    [TestMethod]
    public void BytecodeVmStepsPatternPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let cards be ['Ace', 'Ace', 'King']
              let objects be [[kind: 'bot', stats: [team: 'red']], [kind: 'item', stats: [team: 'blue']]]
              let hasPair be cards[:has pair of 'Ten']
              let takenPair be cards[:take pair of 'Queen']
              let objectMatch be objects[:has [kind: 'player', stats: [team: 'green']]]
              let score be (1 when hasPair, otherwise 0) + :len takenPair + (10 when objectMatch, otherwise 0)
              emit Done(score: score, hasPair: hasPair, takenPairLen: :len takenPair, objectMatch: objectMatch)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var replacements = new Dictionary<int, int>
        {
            [FindTextConstant(compiled, "Ten")] = FindTextConstant(compiled, "Ace"),
            [FindTextConstant(compiled, "Queen")] = FindTextConstant(compiled, "Ace"),
            [FindTextConstant(compiled, "player")] = FindTextConstant(compiled, "bot"),
            [FindTextConstant(compiled, "green")] = FindTextConstant(compiled, "red")
        };
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadText &&
                                   replacements.TryGetValue(instruction.C_U16, out var replacement)
                ? instruction with { C_U16 = (ushort)replacement }
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(13), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["hasPair"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["takenPairLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["objectMatch"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesChoosePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let units be [[name: 'a', alive: true, value: 1, weight: 0], [name: 'b', alive: true, value: 2, weight: 10], [name: 'c', alive: false, value: 3, weight: 0], [name: 'd', alive: true, value: 4, weight: 0]]
              let firstAlive be units[:choose 1 unit where unit.alive]
              let firstTwoAlive be units[:choose 2 unit where unit.alive]
              let weighted be units[:choose 1 weighted by unit => unit.weight]
              let randomPair be units[:choose 2 at random unit where unit.alive]
              let prefixed be units[:filter unit where unit.value > 1][:choose 1 unit where unit.alive]
              let none be units[:choose 1 unit where unit.value > 99]
              let score be (firstAlive.value + firstTwoAlive[2].value + weighted.value + :len randomPair + prefixed.value) + 5
              emit Done(score: score, replacement: 6, none: none)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(15), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["none"]);
    }

    [TestMethod]
    public void BytecodeVmStepsChoosePipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let units be [[name: 'a', alive: true, value: 1, weight: 0], [name: 'b', alive: true, value: 2, weight: 10], [name: 'c', alive: false, value: 3, weight: 0], [name: 'd', alive: true, value: 4, weight: 0]]
              let firstAlive be units[:choose 1 unit where unit.alive]
              let firstTwoAlive be units[:choose 2 unit where unit.alive]
              let weighted be units[:choose 1 weighted by unit => unit.weight]
              let randomPair be units[:choose 2 at random unit where unit.alive]
              let prefixed be units[:filter unit where unit.value > 1][:choose 1 unit where unit.alive]
              let none be units[:choose 1 unit where unit.value > 99]
              let score be (firstAlive.value + firstTwoAlive[2].value + weighted.value + :len randomPair + prefixed.value) + 5
              emit Done(score: score, replacement: 6, none: none)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(15), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["none"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesDrawPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let single be [1, 2, 3, 4][:draw 1]
              let hand be [1, 2, 3, 4][:draw 3]
              let prefixed be [1, 2, 3, 4][:filter value where value > 1][:draw 2]
              let invalidDraw be 123[:draw 1]
              let score be (single + hand[3] + prefixed[2]) + 5
              emit Done(score: score, replacement: 6, invalidDraw: invalidDraw)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(13), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["invalidDraw"]);
    }

    [TestMethod]
    public void BytecodeVmStepsDrawPipelineStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let single be [1, 2, 3, 4][:draw 1]
              let hand be [1, 2, 3, 4][:draw 3]
              let prefixed be [1, 2, 3, 4][:filter value where value > 1][:draw 2]
              let invalidDraw be 123[:draw 1]
              let score be (single + hand[3] + prefixed[2]) + 5
              emit Done(score: score, replacement: 6, invalidDraw: invalidDraw)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var code = compiled.Code
            .Select(instruction => IsIntegerLoad(instruction, originalConstant)
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(13), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["invalidDraw"]);
    }

    [TestMethod]
    public void BytecodeVmExecutableBuilderRejectsInvalidLinearSideTableIndex()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              emit Done(value: 1)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var code = compiled.Code.ToArray();
        var publishInstructionIndex = Array.FindIndex(
            code,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.EmitMessage);
        Assert.IsGreaterThanOrEqualTo(0, publishInstructionIndex);
        code[publishInstructionIndex] = code[publishInstructionIndex] with { A_U16 = (ushort)compiled.UShortListPool.Count };
        var invalid = RebuildCompiledArtifactFromPublicData(compiled, code);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => GesBytecodeVmExecutableBuilder.Build(invalid));
        StringAssert.Contains(exception.Message, "message shape");
    }

    [TestMethod]
    public void CompiledArtifactDoesNotExposeBytecodeVmState()
    {
        var compiledType = typeof(GameEventScriptCompiled);
        var publicMembers = compiledType
            .GetProperties()
            .Select(property => property.PropertyType)
            .Concat(compiledType.GetFields().Select(field => field.FieldType));

        foreach (var type in publicMembers)
        {
            Assert.IsFalse(
                ContainsBytecodeVmType(type),
                $"GameEventScriptCompiled exposes BytecodeVM type '{type.FullName}'.");
        }

        Assert.IsFalse(
            compiledType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .Any(ContainsBytecodeVmType),
            "GameEventScriptCompiled stores BytecodeVM state internally.");
        Assert.IsNull(
            compiledType.GetProperty("ConstantPool"),
            "GameEventScriptCompiled should encode constants in linear load instructions instead of exposing a constant pool.");
        Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(GameEventScriptBytecodeOpCode)));
        Assert.AreEqual(12, Marshal.SizeOf<GameEventScriptBytecodeInstruction>());
        Assert.AreEqual(
            LayoutKind.Explicit,
            typeof(GameEventScriptBytecodeInstruction).StructLayoutAttribute!.Value);

        var disallowedPortableTypes = CollectPublicBytecodeBoundaryTypes(compiledType)
            .Where(IsDisallowedPortableBytecodeType)
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(
            0,
            disallowedPortableTypes,
            "GameEventScriptCompiled portable bytecode graph exposes compiler, AST, BytecodeVM, or runtime value types:\n" +
            string.Join("\n", disallowedPortableTypes));
    }

    [TestMethod]
    public void BytecodeVmPreservesParameterTypeHintsWithoutChangingSignatures()
    {
        const string script =
            """
            module Runtime

            function boosted(_ value as :integer) means value + 1

            on Start(value as :integer) {
              emit Done(value: boosted(value))
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var handler = compiled.Handlers["Start"][0];
        var callable = compiled.Callables["boosted"];
        var dump = compiled.DumpBytecode();

        Assert.AreEqual("Start(value)", GameEventScriptMessageSignature.CreateSignatureId(handler.Message, handler.SignatureLabels));
        Assert.AreEqual("boosted(_)", GameEventScriptMessageSignature.CreateSignatureId(callable.Name, callable.SignatureLabels));
        Assert.AreEqual("integer", handler.ParameterTypes[0]);
        Assert.AreEqual("integer", callable.ParameterTypes[0]);
        StringAssert.Contains(dump, "handler #0 Start(value)");
        StringAssert.Contains(dump, "params=[value as :integer]");
        StringAssert.Contains(dump, "callable #0 Function boosted(_) entry=@");
        StringAssert.Contains(dump, "params=[value as :integer]");
    }

    [TestMethod]
    public void BytecodeVmCanRunFromCompiledArtifactRebuiltFromPublicBytecodeData()
    {
        const string script =
            """
            module Runtime

            function boosted(_ value) means value + 2

            on Start(value) {
              let total be boosted(value)
              emit Done(value: total)
            }
            """;

        var bytecode = GameEventScriptManager.Compile(script);
        var rebuiltBytecode = RebuildCompiledArtifactFromPublicData(bytecode);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rebuiltBytecode);

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmRejectsUnknownStatementNodesAtCompileTime()
    {
        var module = new GesModule(
            "UnknownStatementModule",
            new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal),
            new Dictionary<string, GesCallableDefinition>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<EventHandlerNode>>(StringComparer.Ordinal)
            {
                ["Start"] =
                [
                    new EventHandlerNode(
                        "Start",
                        EventHandlerDispatchKind.ExactSignature,
                        Array.Empty<ParameterNode>(),
                        [new UnknownStatementNode()])
                ]
            });

        var exception = Assert.ThrowsExactly<GameEventScriptCompileException>(() => GesBytecodeCompiler.Compile(module));
        StringAssert.Contains(exception.Message, "GameEventScript bytecode lowerer does not support handler 'Start' #0");
        StringAssert.Contains(exception.Message, nameof(UnknownStatementNode));
    }

    private static void DrainHostWithSingleOpcodeBudget(GameEventScriptHost host)
    {
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
        }
        while (step.State != GameEventScriptRunState.Completed);
    }

    private static bool ContainsBytecodeVmType(Type type)
    {
        if ((type.Namespace ?? string.Empty).Contains("StepH.GameEventScript.BytecodeVM", StringComparison.Ordinal))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(ContainsBytecodeVmType))
        {
            return true;
        }

        return type.IsArray && type.GetElementType() is { } elementType && ContainsBytecodeVmType(elementType);
    }

    private static IReadOnlySet<Type> CollectPublicBytecodeBoundaryTypes(Type rootType)
    {
        var visited = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(rootType);

        while (pending.Count > 0)
        {
            var type = pending.Pop();
            if (!visited.Add(type))
            {
                continue;
            }

            foreach (var nestedType in ExpandType(type))
            {
                pending.Push(nestedType);
            }

            if (!IsGameEventScriptPublicModelType(type))
            {
                continue;
            }

            foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            {
                pending.Push(property.PropertyType);
            }

            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            {
                pending.Push(field.FieldType);
            }

            foreach (var constructor in type.GetConstructors())
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    pending.Push(parameter.ParameterType);
                }
            }
        }

        return visited;
    }

    private static IEnumerable<Type> ExpandType(Type type)
    {
        if (type.IsArray && type.GetElementType() is { } elementType)
        {
            yield return elementType;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                yield return argument;
            }
        }
    }

    private static bool IsGameEventScriptPublicModelType(Type type)
        => type.IsPublic &&
           type.Namespace is { } typeNamespace &&
           (string.Equals(typeNamespace, "StepH.GameEventScript.Api", StringComparison.Ordinal) ||
            string.Equals(typeNamespace, "StepH.GameEventScript.Runtime", StringComparison.Ordinal));

    private static bool IsDisallowedPortableBytecodeType(Type type)
    {
        var typeNamespace = type.Namespace ?? string.Empty;
        return typeNamespace.StartsWith("StepH.GameEventScript.Compiler", StringComparison.Ordinal) ||
               typeNamespace.StartsWith("StepH.GameEventScript.BytecodeVM", StringComparison.Ordinal) ||
               type == typeof(GameEventScriptValue) ||
               type.IsSubclassOf(typeof(GameEventScriptValue));
    }

    private static GameEventScriptCompiled RebuildCompiledArtifactFromPublicData(
        GameEventScriptCompiled original,
        IReadOnlyList<GameEventScriptBytecodeInstruction>? code = null)
        => new(
            new GameEventScriptCompileOptions
            {
                EnableDiagnostics = original.Options.EnableDiagnostics,
                EnableDebugInfo = original.Options.EnableDebugInfo,
                Optimize = original.Options.Optimize
            },
            original.ModuleName,
            original.StringPool.ToArray(),
            original.UShortListPool.Select(layout => (IReadOnlyList<ushort>)layout.ToArray()).ToArray(),
            original.OutboundMessageSignatures.ToArray(),
            original.ExternalReferences
                .Select(reference => new GameEventScriptExtensionReference(reference.ExtensionName, reference.FunctionName, reference.ArgumentLabels.ToArray()))
                .ToArray(),
            original.ExternalTypeConstructorReferences
                .Select(reference => new GameEventScriptExternalTypeConstructorReference(reference.TypeName, reference.ArgumentLabels.ToArray()))
                .ToArray(),
            original.Callables.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            original.Handlers.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<GameEventScriptBytecodeHandler>)pair.Value.ToArray(), StringComparer.Ordinal),
            original.TypeDefinitions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            code?.ToArray() ?? original.Code.ToArray(),
            original.MaxFrameSlots,
            original.DebugSegment);

    private static int FindTextConstant(GameEventScriptCompiled compiled, string value)
        => compiled.StringPool
            .Select((text, index) => (text, index))
            .Single(pair => string.Equals(pair.text, value, StringComparison.Ordinal))
            .index;

    private static bool IsIntegerLoad(GameEventScriptBytecodeInstruction instruction, long value)
        => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadInteger &&
           instruction.I64 == value;

    private static GameEventScriptBytecodeInstruction WithIntegerLoad(
        GameEventScriptBytecodeInstruction instruction,
        long value)
        => instruction with
        {
            OpCode = GameEventScriptBytecodeOpCode.LoadInteger,
            I64 = value
        };

    [TestMethod]
    public void BytecodeVmStandardExtensionsAreIntrinsicAndDoNotRequireDynamicLinking()
    {
        const string script =
            """
            module StandardExtensions

            on Start(value, heading) {
              emit Done(floor: :integer.floor value, wrapped: :degree.wrap heading)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var dump = compiled.DumpBytecode();

        StringAssert.Contains(dump, "externalReferences[0]");
        Assert.IsFalse(dump.Contains("integer.floor(_)", StringComparison.Ordinal));
        Assert.IsFalse(dump.Contains("degree.wrap(_)", StringComparison.Ordinal));

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(compiled);

        host.PublishToCompletion(Create(
            "Start",
            ("value", GameEventScriptValueFactory.GesFloat(10.4d)),
            ("heading", GameEventScriptValueFactory.GesDegree(-10))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["floor"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesDegree(350), published[0].Arguments["wrapped"]);
    }

    [TestMethod]
    public void BytecodeVmDynamicLinkBindsExtensionReferencesOnHostLoad()
    {
        const string script =
            """
            module Extensions

            on Start(values) {
              let floored be values[:select item => :math.floor item]
              emit Done(first: floored[1])
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var dump = compiled.DumpBytecode();

        StringAssert.Contains(dump, "externalReferences[1]");
        StringAssert.Contains(dump, "math.floor(_)");
        var exception = Assert.ThrowsExactly<GameEventScriptDynamicLinkException>(() =>
            GameEventScriptHost.CreateBuilder().Build().Load(compiled));
        StringAssert.Contains(exception.Message, "math.floor(_)");
        StringAssert.Contains(exception.Message, "registry is required");

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRegistry(TestExtensionRegistry.Instance)
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(compiled);

        host.PublishToCompletion(Create(
            "Start",
            ("values", GameEventScriptValueFactory.GesList(
            [
                GameEventScriptValueFactory.GesFloat(2.9d),
                GameEventScriptValueFactory.GesFloat(5.1d)
            ]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(2d), published[0].Arguments["first"]);
    }

    [TestMethod]
    public void BytecodeVmDynamicLinkReportsMissingExtensionFunctionWithSignature()
    {
        const string script =
            """
            module MissingExtensions

            on Start {
              let value be :missing.floor 10.4
              emit Done(value: value)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var exception = Assert.ThrowsExactly<GameEventScriptDynamicLinkException>(() =>
            GameEventScriptHost.CreateBuilder()
                .WithRegistry(TestExtensionRegistry.Instance)
                .Build()
                .Load(compiled));

        StringAssert.Contains(exception.Message, "missing.floor(_)");
        StringAssert.Contains(exception.Message, "not registered in the configured registry");
    }

    [TestMethod]
    public void BytecodeVmDynamicLinkKeepsLabeledArgumentsPositional()
    {
        const string script =
            """
            module OrderedLabels

            on Start(heading, target) {
              let turn be :nav.shortestTurn to: target from: heading
              emit Done(turn: turn)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var dump = compiled.DumpBytecode();

        StringAssert.Contains(dump, "nav.shortestTurn(to,from)");
        var exception = Assert.ThrowsExactly<GameEventScriptDynamicLinkException>(() =>
            GameEventScriptHost.CreateBuilder()
                .WithRegistry(NavExtensionRegistry.Instance)
                .Build()
                .Load(compiled));
        StringAssert.Contains(exception.Message, "nav.shortestTurn(to,from)");
    }

    [TestMethod]
    public void BytecodeVmStandardExtensionsCannotBeOverriddenByRegistry()
    {
        const string script =
            """
            module StandardOverride

            on Start(value) {
              emit Done(floor: :integer.floor value)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRegistry(StandardOverrideRegistry.Instance)
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesFloat(10.9d))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["floor"]);
    }

    private sealed class TestExtensionRegistry : IGameEventScriptExtensionRegistry
    {
        public static readonly TestExtensionRegistry Instance = new();

        private static readonly IGameEventScriptExtensionFunction MathFloor = new DelegateExtensionFunction((_, args) =>
            GameEventScriptFastValue.FromFloat(Math.Floor(args[0].Number)));

        private TestExtensionRegistry()
        {
        }

        public bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function)
        {
            if (reference.SignatureId == "math.floor(_)")
            {
                function = MathFloor;
                return true;
            }

            function = default!;
            return false;
        }
    }

    private sealed class NavExtensionRegistry : IGameEventScriptExtensionRegistry
    {
        public static readonly NavExtensionRegistry Instance = new();

        private static readonly IGameEventScriptExtensionFunction ShortestTurn = new DelegateExtensionFunction((_, args) =>
        {
            var delta = (args[1].Number - args[0].Number + 540d) % 360d - 180d;
            return GameEventScriptFastValue.FromFloat(delta, GameEventScriptNumericUnit.Degree);
        });

        private NavExtensionRegistry()
        {
        }

        public bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function)
        {
            if (reference.SignatureId == "nav.shortestTurn(from,to)")
            {
                function = ShortestTurn;
                return true;
            }

            function = default!;
            return false;
        }
    }

    private sealed class StandardOverrideRegistry : IGameEventScriptExtensionRegistry
    {
        public static readonly StandardOverrideRegistry Instance = new();

        private static readonly IGameEventScriptExtensionFunction FakeIntegerFloor = new DelegateExtensionFunction((_, _) =>
            GameEventScriptFastValue.FromInteger(999));

        private StandardOverrideRegistry()
        {
        }

        public bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function)
        {
            if (reference.SignatureId == "integer.floor(_)")
            {
                function = FakeIntegerFloor;
                return true;
            }

            function = default!;
            return false;
        }
    }

    private delegate GameEventScriptFastValue ExtensionInvoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments);

    private sealed class DelegateExtensionFunction(ExtensionInvoke invoke) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => invoke(context, arguments);
    }

    private sealed record UnknownStatementNode : StatementNode;
}
