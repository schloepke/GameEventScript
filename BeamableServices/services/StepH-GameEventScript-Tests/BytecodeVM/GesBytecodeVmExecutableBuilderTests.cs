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
        StringAssert.Contains(first, "LoadInteger");
        StringAssert.Contains(first, "MoveSlot");
        StringAssert.Contains(first, "BuildMessage");
        StringAssert.Contains(first, "EmitMessage");
        Assert.IsFalse(first.Contains("maxStackDepth", StringComparison.Ordinal));
        Assert.IsFalse(first.Contains("nestedExpression", StringComparison.Ordinal));
        Assert.IsNotEmpty(compiled.Code);
        Assert.IsGreaterThanOrEqualTo(compiled.Handlers["Start"][0].LocalSlotCount, compiled.MaxFrameSlots);
        Assert.IsGreaterThanOrEqualTo(compiled.Handlers["Start"][0].EntryAddress, 0);
        Assert.IsFalse(first.Contains("EvaluateExpression", StringComparison.Ordinal));
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
            instruction.OpCode == GameEventScriptBytecodeOpCode.ShortCircuitImplies &&
            instruction.Dest_U16 >= 0 &&
            instruction.A_U16 == instruction.B_U16));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.ShortCircuitImplies &&
            instruction.Dest_U16 >= 0 &&
            instruction.A_U16 != instruction.B_U16));
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
    public void SeededRandomSeedsMustBeUnitlessIntegerOrExplicitIntegerCast()
    {
        const string unitSeedScript =
            """
            module RandomScopes

            on Start {
              let value be :random with 5m 1
              emit Done(value: value)
            }
            """;
        const string unknownSeedScript =
            """
            module RandomScopes

            on Start(seed) {
              let value be :random with seed 1
              emit Done(value: value)
            }
            """;

        var unitSeed = Assert.ThrowsExactly<GameEventScriptCompileException>(() => GameEventScriptManager.Compile(unitSeedScript));
        var unknownSeed = Assert.ThrowsExactly<GameEventScriptCompileException>(() => GameEventScriptManager.Compile(unknownSeedScript));

        StringAssert.Contains(unitSeed.Message, "unitless :integer");
        StringAssert.Contains(unknownSeed.Message, "as :integer");
    }

    [TestMethod]
    public void BytecodeVmBalancesRandomScopeWhenDynamicSeedCannotBecomeInteger()
    {
        const string script =
            """
            module RandomScopes

            on Start {
              let bad as :integer be :uuid('550e8400-e29b-41d4-a716-446655440000')
              :random with bad {
                let inner be :random from 1 to 6
              }
              emit Done(value: :random from 1 to 6)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.IsTrue(published[0].Arguments.ContainsKey("value"));
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

        Assert.HasCount(1, compiled.PipelinePool);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(layout =>
            layout.Kind == GameEventScriptBytecodePipelineSelectorKind.Select &&
            layout.IdentifierSlot >= 0 &&
            layout.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline &&
            instruction.C_U16 >= 0 &&
            instruction.C_U16 < compiled.PipelinePool.Count));
        Assert.IsGreaterThanOrEqualTo(0, compiled.PipelinePool[0].SourceSlot);
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
        Assert.AreEqual(handler.LocalSlotCount, linearHandler.LocalSlotCount);
        Assert.AreEqual(callable.EntryAddress, linearCallable.EntryAddress);
        Assert.AreEqual(callable.ReturnSlot, linearCallable.ReturnSlot);
        Assert.IsTrue(executable.LinearExecutable.Code.Any(instruction =>
            instruction.OpCode is GameEventScriptBytecodeOpCode.EmitMessage or GameEventScriptBytecodeOpCode.EmitMessageWithTags &&
            instruction.A_U16 < compiled.UShortListPool.Count));
    }

    [TestMethod]
    public void BytecodeVmExecutesSimpleHandlersFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              emit Done(value: 1, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var originalConstant = 1L;
        var replacementConstant = 2L;
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
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsSimpleHandlersFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              emit Done(value: 1, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var originalConstant = 1L;
        var replacementConstant = 2L;
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
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesSimpleCallablesFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            function boosted(_ value) means value + 1

            on Start(value) {
              emit Done(value: boosted(value), replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var originalConstant = 1L;
        var replacementConstant = 2L;
        var callableEntry = compiled.Callables["boosted"].EntryAddress;
        var callableEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            callableEntry,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= callableEntry &&
                                           index < callableEnd &&
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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesCallablesFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            function boosted(_ value) means value + 1

            on Start(value) {
              let blocker be [1, 2, 3][:first item where item > 0]
              emit Done(value: boosted(value), blocker: blocker, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = 1L;
        var replacementConstant = 2L;
        var callableEntry = compiled.Callables["boosted"].EntryAddress;
        var callableEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            callableEntry,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= callableEntry &&
                                           index < callableEnd &&
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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesPredicateCallsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            predicate high(_ value) means (value + 1) > 5

            on Start(value) {
              let blocker be [1, 2, 3][:first item where item > 0]
              emit Done(ok: value is high, blocker: blocker, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        var predicateInstruction = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallPredicate);
        Assert.AreEqual(compiled.Callables["high"].EntryAddress, predicateInstruction.A_U16);
        Assert.HasCount(1, compiled.UShortListPool[predicateInstruction.B_U16]);

        var replacementConstant = 2L;
        var callableEntry = compiled.Callables["high"].EntryAddress;
        var callableEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            callableEntry,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var rewrittenConstantInstructionIndex = compiled.Code
            .Select((instruction, index) => (instruction, index))
            .First(pair => pair.index >= callableEntry &&
                           pair.index < callableEnd &&
                           pair.instruction.OpCode == GameEventScriptBytecodeOpCode.LoadInteger)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => index == rewrittenConstantInstructionIndex
                ? WithIntegerLoad(instruction, replacementConstant)
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(4))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["ok"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), published[0].Arguments["blocker"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesMultiArgumentPredicateCallsThroughCallPredicate()
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
        Assert.HasCount(2, compiled.UShortListPool[predicateInstruction.B_U16]);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(compiled);

        host.PublishToCompletion(
            Create(
                "Start",
                ("left", GameEventScriptValueFactory.GesInteger(7)),
                ("right", GameEventScriptValueFactory.GesInteger(4))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["ok"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let adjusted be value + 1
              emit Done(value: adjusted, blocker: blocker, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = 1L;
        var replacementConstant = 2L;
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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let adjusted be value + 1
              emit Done(value: adjusted, blocker: blocker, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = 1L;
        var replacementConstant = 2L;
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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesTypeConstructorStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let vector be :vector(x: value + 1, y: 2)
              emit Done(vector: vector, blocker: blocker, replacement: 3)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = 1L;
        var replacementConstant = 3L;
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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesVector(8, 2), published[0].Arguments["vector"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsTypeConstructorStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let vector be :vector(x: value + 1, y: 2)
              emit Done(vector: vector, blocker: blocker, replacement: 3)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = 1L;
        var replacementConstant = 3L;
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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesVector(8, 2), published[0].Arguments["vector"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesSimpleOperationLayoutStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let negated be -(value + 1)
              let casted be ((value + 1) as :float) + 0.5
              let member be (:vector(x: value + 1, y: 2)).x
              let typedGate be ((value + 1) is :integer) and ((value + 1) > 7)
              emit Done(negated: negated, casted: casted, member: member, typedGate: typedGate, blocker: blocker, replacement: 3)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.UnaryNegate));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CastFloat));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.MemberAccess));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.TypeCheckInteger));

        var originalConstant = 1L;
        var replacementConstant = 3L;
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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(-8), published[0].Arguments["negated"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(8.5d), published[0].Arguments["casted"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(8d), published[0].Arguments["member"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["typedGate"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsSimpleOperationLayoutStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let negated be -(value + 1)
              let casted be ((value + 1) as :float) + 0.5
              let member be (:vector(x: value + 1, y: 2)).x
              let typedGate be ((value + 1) is :integer) and ((value + 1) > 7)
              emit Done(negated: negated, casted: casted, member: member, typedGate: typedGate, blocker: blocker, replacement: 3)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.UnaryNegate));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CastFloat));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.MemberAccess));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.TypeCheckInteger));

        var originalConstant = 1L;
        var replacementConstant = 3L;
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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(-8), published[0].Arguments["negated"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(8.5d), published[0].Arguments["casted"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(8d), published[0].Arguments["member"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["typedGate"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesRangeStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let values be from value + 4 to 15 step 2
              emit Done(first: values[1], second: values[2], length: :len values, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RangeWithStep));

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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["first"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(13), published[0].Arguments["second"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["length"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsRangeStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let values be from value + 4 to 15 step 2
              emit Done(first: values[1], second: values[2], length: :len values, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RangeWithStep));

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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["first"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(13), published[0].Arguments["second"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["length"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesVariadicStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let lowest be :min of value + 4 and 15 and 20
              let highest be :max of value + 4 and 10 and 3
              emit Done(lowest: lowest, highest: highest, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Variadic));

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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["lowest"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["highest"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsVariadicStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let lowest be :min of value + 4 and 15 and 20
              let highest be :max of value + 4 and 10 and 3
              emit Done(lowest: lowest, highest: highest, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Variadic));

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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["lowest"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["highest"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesBuilderStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let listValue be [value + 4, 15]
              let sequenceValue be of value + 4 and 15
              let setValue be :set[value + 4, 15]
              let dictionaryValue be [score: value + 4, cap: 15]
              let messageValue be Success(score: value + 4, cap: 15)
              emit Done(
                listFirst: listValue[1],
                sequenceFirst: sequenceValue[1],
                setHasEleven: 11 in setValue,
                dictionaryScore: dictionaryValue.score,
                messageScore: messageValue.arguments.score,
                blocker: blocker,
                replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildList));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildSequence));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildSet));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildDictionary));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildMessage));

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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["listFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["sequenceFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["setHasEleven"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["dictionaryScore"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["messageScore"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsBuilderStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let listValue be [value + 4, 15]
              let sequenceValue be of value + 4 and 15
              let setValue be :set[value + 4, 15]
              let dictionaryValue be [score: value + 4, cap: 15]
              let messageValue be Success(score: value + 4, cap: 15)
              emit Done(
                listFirst: listValue[1],
                sequenceFirst: sequenceValue[1],
                setHasEleven: 11 in setValue,
                dictionaryScore: dictionaryValue.score,
                messageScore: messageValue.arguments.score,
                blocker: blocker,
                replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildList));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildSequence));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildSet));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildDictionary));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BuildMessage));

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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["listFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["sequenceFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["setHasEleven"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["dictionaryScore"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["messageScore"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesBindHandlerStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let myHandler be Success(message, value)
              let myMessage be myHandler(message: 'hello', value: value + 4)
              emit Done(
                messageIsMessage: myMessage is :message,
                messageValue: myMessage.arguments.value,
                blocker: blocker,
                replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BindHandler));

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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["messageIsMessage"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["messageValue"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsBindHandlerStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let myHandler be Success(message, value)
              let myMessage be myHandler(message: 'hello', value: value + 4)
              emit Done(
                messageIsMessage: myMessage is :message,
                messageValue: myMessage.arguments.value,
                blocker: blocker,
                replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.BindHandler));

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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["messageIsMessage"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["messageValue"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
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
    public void BytecodeVmExecutesExtensionCallStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let floored be :integer.floor(value + 4)
              emit Done(floored: floored, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallStandard));

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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["floored"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsExtensionCallStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let floored be :integer.floor(value + 4)
              emit Done(floored: floored, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallStandard));

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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["floored"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesFunctionCallStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            function identity(_ value) means value

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let called be identity(value + 4)
              emit Done(called: called, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        var callInstruction = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Call);
        Assert.AreEqual(compiled.Callables["identity"].EntryAddress, callInstruction.A_U16);
        Assert.HasCount(1, compiled.UShortListPool[callInstruction.B_U16]);

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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["called"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsFunctionCallStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            function identity(_ value) means value

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let called be identity(value + 4)
              emit Done(called: called, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Call));

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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(11), published[0].Arguments["called"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesPredicateCallStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            predicate high(_ value) means value > 10

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let ok be (value + 4) is high
              emit Done(ok: ok, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        var predicateInstruction = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallPredicate);
        Assert.AreEqual(compiled.Callables["high"].EntryAddress, predicateInstruction.A_U16);
        Assert.HasCount(1, compiled.UShortListPool[predicateInstruction.B_U16]);

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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["ok"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsPredicateCallStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            predicate high(_ value) means value > 10

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let ok be (value + 4) is high
              emit Done(ok: ok, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        var predicateInstruction = compiled.Code.Single(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallPredicate);
        Assert.AreEqual(compiled.Callables["high"].EntryAddress, predicateInstruction.A_U16);
        Assert.HasCount(1, compiled.UShortListPool[predicateInstruction.B_U16]);

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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.IsTrue(steps.Any(item => item.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["ok"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsSimpleCallablesFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            function boosted(_ value) means value + 1

            on Start(value) {
              emit Done(value: boosted(value), replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var originalConstant = 1L;
        var replacementConstant = 2L;
        var callableEntry = compiled.Callables["boosted"].EntryAddress;
        var callableEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            callableEntry,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= callableEntry &&
                                           index < callableEnd &&
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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5)))));
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
        }
        while (step.State != GameEventScriptRunState.Completed);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesNestedCallablesFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            function addOne(_ value) means value + 1
            function boosted(_ value) means addOne(value) + 1

            on Start(value) {
              emit Done(value: boosted(value), replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var originalConstant = 1L;
        var replacementConstant = 2L;
        var callableEntry = compiled.Callables["addOne"].EntryAddress;
        var callableEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            callableEntry,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= callableEntry &&
                                           index < callableEnd &&
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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(8), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesGuardedChoicesFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let selected be 1 when value > 0, otherwise 2
              emit Done(value: selected, replacement: 3)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        AssertGuardedChoiceLoweredToLinearJumps(compiled);
        var otherwiseRanges = GetGuardedChoiceOtherwiseRanges(compiled);

        var originalConstant = 2L;
        var replacementConstant = 3L;
        var code = compiled.Code
            .Select((instruction, index) => otherwiseRanges.Any(range => index >= range.Start && index < range.End) &&
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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(-1))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesGuardedChoiceHelpersFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [1, 2, 3][:first item where item > 0]
              let selected be 1 when value > 0, otherwise 2
              emit Done(value: selected, blocker: blocker, replacement: 3)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        AssertGuardedChoiceLoweredToLinearJumps(compiled);
        var otherwiseRanges = GetGuardedChoiceOtherwiseRanges(compiled);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = 2L;
        var replacementConstant = 3L;
        var code = compiled.Code
            .Select((instruction, index) => otherwiseRanges.Any(range => index >= range.Start && index < range.End) &&
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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(-1))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsGuardedChoicesFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let selected be 1 when value > 0, otherwise 2
              emit Done(value: selected, replacement: 3)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        AssertGuardedChoiceLoweredToLinearJumps(compiled);
        var otherwiseRanges = GetGuardedChoiceOtherwiseRanges(compiled);

        var originalConstant = 2L;
        var replacementConstant = 3L;
        var code = compiled.Code
            .Select((instruction, index) => otherwiseRanges.Any(range => index >= range.Start && index < range.End) &&
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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(-1)))));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesGuardedChoiceStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let selected be (1 when value > 0, otherwise 2) + 4
              emit Done(value: selected, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        AssertGuardedChoiceLoweredToLinearJumps(compiled);

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

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(-1))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(8), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsGuardedChoiceStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start(value) {
              let blocker be [10, 11, 12][:first item where item > 0]
              let selected be (1 when value > 0, otherwise 2) + 4
              emit Done(value: selected, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        AssertGuardedChoiceLoweredToLinearJumps(compiled);

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

        Assert.IsTrue(host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(-1)))));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(8), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesGeneratedCollectionsFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be :list[:select item from 1 to 3 => item + 1]
              emit Done(first: values[1], replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var projectionRanges = GetGeneratedCollectionLinearRanges(compiled);

        var originalConstant = 1L;
        var replacementConstant = 2L;
        var code = compiled.Code
            .Select((instruction, index) => projectionRanges.Any(range => index >= range.Start && index < range.End) &&
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
    public void BytecodeVmExecutesGeneratedCollectionHelpersFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let blocker be [1, 2, 3][:first item where item > 0]
              let values be :list[:select item from 1 to 3 => item + 1]
              emit Done(first: values[1], blocker: blocker, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var projectionRanges = GetGeneratedCollectionLinearRanges(compiled);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = 1L;
        var replacementConstant = 2L;
        var code = compiled.Code
            .Select((instruction, index) => projectionRanges.Any(range => index >= range.Start && index < range.End) &&
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
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsGeneratedCollectionsFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be :list[:select item from 1 to 3 => item + 1]
              emit Done(first: values[1], replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var projectionRanges = GetGeneratedCollectionLinearRanges(compiled);

        var originalConstant = 1L;
        var replacementConstant = 2L;
        var code = compiled.Code
            .Select((instruction, index) => projectionRanges.Any(range => index >= range.Start && index < range.End) &&
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

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["first"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesGeneratedCollectionStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let blocker be [10, 11, 12][:first item where item > 0]
              let first be (:list[:select item from 1 to 3 => item + 1])[1] + 4
              emit Done(first: first, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CollectionBuilderList));

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
    public void BytecodeVmStepsGeneratedCollectionStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let blocker be [10, 11, 12][:first item where item > 0]
              let first be (:list[:select item from 1 to 3 => item + 1])[1] + 4
              emit Done(first: first, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CollectionBuilderList));

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
    public void BytecodeVmStepsRangeForLoopsFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              for item from 1 to 3 {
                emit Each(value: item + 1, replacement: 2)
              }
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var nextInstruction = compiled.Code
            .Select((instruction, index) => (instruction, index))
            .Single(pair => pair.instruction.OpCode == GameEventScriptBytecodeOpCode.IteratorNext);
        var closeInstruction = compiled.Code
            .Select((instruction, index) => (instruction, index))
            .Single(pair => pair.index > nextInstruction.index && pair.instruction.OpCode == GameEventScriptBytecodeOpCode.IteratorClose);
        var originalConstant = 1L;
        var replacementConstant = 2L;
        var code = compiled.Code
            .Select((instruction, index) => index > nextInstruction.index &&
                                           index < closeInstruction.index &&
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

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(3, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(5), published[2].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmStepsCollectionForLoopsFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let items be [1, 2, 3]
              for item in items {
                emit Each(value: item + 1, replacement: 2)
              }
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var nextInstruction = compiled.Code
            .Select((instruction, index) => (instruction, index))
            .Single(pair => pair.instruction.OpCode == GameEventScriptBytecodeOpCode.IteratorNext);
        var closeInstruction = compiled.Code
            .Select((instruction, index) => (instruction, index))
            .Single(pair => pair.index > nextInstruction.index && pair.instruction.OpCode == GameEventScriptBytecodeOpCode.IteratorClose);
        var originalConstant = 1L;
        var replacementConstant = 2L;
        var code = compiled.Code
            .Select((instruction, index) => index > nextInstruction.index &&
                                           index < closeInstruction.index &&
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

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(3, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(5), published[2].Arguments["value"]);
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
        var selector = compiled.PipelineSelectorPool[compiled.PipelinePool.Single().TerminalSelectorIndex];
        Assert.AreEqual(GameEventScriptBytecodePipelineSelectorKind.Select, selector.Kind);
        Assert.IsGreaterThanOrEqualTo(0, selector.ExpressionEntryAddress);
        Assert.IsTrue(compiled.Code
            .Skip(selector.ExpressionEntryAddress)
            .TakeWhile(instruction => instruction.OpCode != GameEventScriptBytecodeOpCode.Return)
            .Any(instruction => instruction.OpCode is GameEventScriptBytecodeOpCode.Add or GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd));
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
        var selector = compiled.PipelineSelectorPool[compiled.PipelinePool.Single().TerminalSelectorIndex];
        Assert.IsGreaterThanOrEqualTo(0, selector.ExpressionEntryAddress);

        var originalConstant = 1L;
        var replacementConstant = 2L;
        var selectorEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            selector.ExpressionEntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= selector.ExpressionEntryAddress &&
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
        var selector = compiled.PipelineSelectorPool[compiled.PipelinePool.Single().TerminalSelectorIndex];
        Assert.AreEqual(GameEventScriptBytecodePipelineSelectorKind.Select, selector.Kind);
        Assert.IsGreaterThanOrEqualTo(0, selector.ExpressionEntryAddress);

        var originalConstant = 5L;
        var replacementConstant = 6L;
        var selectorEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            selector.ExpressionEntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= selector.ExpressionEntryAddress &&
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Sum));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Average));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Sum));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Average));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Predicate &&
                                                               selector.EdgeMode == "any"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Predicate &&
                                                               selector.EdgeMode == "all"));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Predicate &&
                                                               selector.EdgeMode == "any"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Predicate &&
                                                               selector.EdgeMode == "all"));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Edge &&
                                                               selector.EdgeMode == "last"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Edge &&
                                                               selector.EdgeMode == "single"));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Edge &&
                                                               selector.EdgeMode == "last"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Edge &&
                                                               selector.EdgeMode == "single"));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Min));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Max));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Min));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Max));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "single"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "all"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "any"));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "single"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "all"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "any"));

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
        Assert.IsTrue(compiled.PipelinePool.Any(layout =>
            layout.PrefixSelectorIndexes.Count > 0 &&
            compiled.PipelineSelectorPool[layout.TerminalSelectorIndex].Kind == GameEventScriptBytecodePipelineSelectorKind.Contains));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "single"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "all"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "any"));

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
        Assert.IsTrue(compiled.PipelinePool.Any(layout =>
            layout.PrefixSelectorIndexes.Count > 0 &&
            compiled.PipelineSelectorPool[layout.TerminalSelectorIndex].Kind == GameEventScriptBytecodePipelineSelectorKind.Contains));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "single"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "all"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Contains &&
                                                               selector.EdgeMode == "any"));

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
              let score be (units[:dictionary unit by unit.id][:rook].value + units[:dictionary unit by unit.id => unit.value][:mage]) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Dictionary &&
                                                               selector.SecondaryExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Dictionary &&
                                                               selector.SecondaryExpressionEntryAddress >= 0));

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
              let score be (units[:dictionary unit by unit.id][:rook].value + units[:dictionary unit by unit.id => unit.value][:mage]) + 4
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Dictionary &&
                                                               selector.SecondaryExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Dictionary &&
                                                               selector.SecondaryExpressionEntryAddress >= 0));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Distinct &&
                                                               selector.ExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Distinct &&
                                                               selector.ExpressionEntryAddress >= 0));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Distinct &&
                                                               selector.ExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Distinct &&
                                                               selector.ExpressionEntryAddress >= 0));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.GroupBy));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.GroupBy));

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
              let invalidReverse be :set[1, 2][:reverse]
              let score be (reversed[1] + filteredReversed[3]) + 5
              emit Done(score: score, replacement: 6, invalidReverse: invalidReverse)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Reverse));

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
              let invalidReverse be :set[1, 2][:reverse]
              let score be (reversed[1] + filteredReversed[3]) + 5
              emit Done(score: score, replacement: 6, invalidReverse: invalidReverse)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Reverse));

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
              let sortedSet be :set[3, 1, 2][:sort descending]
              let score be (ascending[1] + descending[1] + prefixed[1] + sortedSet[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Sort &&
                                                               selector.EdgeMode == "ascending"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Sort &&
                                                               selector.EdgeMode == "descending"));

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
              let sortedSet be :set[3, 1, 2][:sort descending]
              let score be (ascending[1] + descending[1] + prefixed[1] + sortedSet[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Sort &&
                                                               selector.EdgeMode == "ascending"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Sort &&
                                                               selector.EdgeMode == "descending"));

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
              let orderedSet be :set[3, 1, 2][:order by item => item descending]
              let score be (ascending[1].value + descending[1].value + prefixed[1].value + orderedSet[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.OrderBy &&
                                                               selector.EdgeMode == "ascending" &&
                                                               selector.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.OrderBy &&
                                                               selector.EdgeMode == "descending" &&
                                                               selector.ExpressionEntryAddress >= 0));

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
              let orderedSet be :set[3, 1, 2][:order by item => item descending]
              let score be (ascending[1].value + descending[1].value + prefixed[1].value + orderedSet[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.OrderBy &&
                                                               selector.EdgeMode == "ascending" &&
                                                               selector.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.OrderBy &&
                                                               selector.EdgeMode == "descending" &&
                                                               selector.ExpressionEntryAddress >= 0));

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
              let setTaken be :set[1, 2, 3][:take highest 2]
              let score be (firstTwo[2] + withoutLast[3] + highestTwo[1] + droppedLowest[1] + prefixed[2] + setTaken[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "take" &&
                                                               selector.SecondaryMode == "highest"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "drop" &&
                                                               selector.SecondaryMode == "lowest"));

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
              let setTaken be :set[1, 2, 3][:take highest 2]
              let score be (firstTwo[2] + withoutLast[3] + highestTwo[1] + droppedLowest[1] + prefixed[2] + setTaken[1]) + 5
              emit Done(score: score, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "take" &&
                                                               selector.SecondaryMode == "highest"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "drop" &&
                                                               selector.SecondaryMode == "lowest"));

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
              let invalidShuffle be :set[1, 2, 3][:shuffle]
              let score be (:len shuffled + :len prefixed) + 5
              emit Done(score: score, replacement: 6, invalidShuffle: invalidShuffle)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Shuffle));

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
              let invalidShuffle be :set[1, 2, 3][:shuffle]
              let score be (:len shuffled + :len prefixed) + 5
              emit Done(score: score, replacement: 6, invalidShuffle: invalidShuffle)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Shuffle));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.SeriesTerm &&
                                                               selector.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "take" &&
                                                               selector.SecondaryMode == "first"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "drop" &&
                                                               selector.SecondaryMode == "first"));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.SeriesTerm &&
                                                               selector.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "take" &&
                                                               selector.SecondaryMode == "first"));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "drop" &&
                                                               selector.SecondaryMode == "first"));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Pattern &&
                                                               selector.PipelinePatternIndex >= 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.TakePattern &&
                                                               selector.PipelinePatternIndex >= 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.ObjectMatch &&
                                                               selector.ObjectPatternIndex >= 0));
        Assert.IsTrue(compiled.PipelinePatternPool.Any(pattern => pattern.Kind == GameEventScriptBytecodePipelinePatternKind.Count &&
                                                                 pattern.Count == 2 &&
                                                                 pattern.FaceEntryAddress >= 0));
        Assert.IsTrue(compiled.PipelineObjectPatternPool.Any(pattern => pattern.Entries.Any(entry =>
            entry.ValueKind == GameEventScriptBytecodePipelineObjectPatternValueKind.Expression &&
            entry.ExpressionEntryAddress >= 0)));
        Assert.IsTrue(compiled.PipelineObjectPatternPool.Any(pattern => pattern.Entries.Any(entry =>
            entry.ValueKind == GameEventScriptBytecodePipelineObjectPatternValueKind.Nested &&
            entry.NestedPatternIndex >= 0)));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Pattern &&
                                                               selector.PipelinePatternIndex >= 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.TakePattern &&
                                                               selector.PipelinePatternIndex >= 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.ObjectMatch &&
                                                               selector.ObjectPatternIndex >= 0));
        Assert.IsTrue(compiled.PipelinePatternPool.Any(pattern => pattern.Kind == GameEventScriptBytecodePipelinePatternKind.Count &&
                                                                 pattern.Count == 2 &&
                                                                 pattern.FaceEntryAddress >= 0));
        Assert.IsTrue(compiled.PipelineObjectPatternPool.Any(pattern => pattern.Entries.Any(entry =>
            entry.ValueKind == GameEventScriptBytecodePipelineObjectPatternValueKind.Expression &&
            entry.ExpressionEntryAddress >= 0)));
        Assert.IsTrue(compiled.PipelineObjectPatternPool.Any(pattern => pattern.Entries.Any(entry =>
            entry.ValueKind == GameEventScriptBytecodePipelineObjectPatternValueKind.Nested &&
            entry.NestedPatternIndex >= 0)));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Choose &&
                                                               selector.Count == 1 &&
                                                               selector.ExpressionEntryAddress >= 0 &&
                                                               selector.SecondaryExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Choose &&
                                                               selector.SecondaryIdentifierSlot >= 0 &&
                                                               selector.SecondaryExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Choose &&
                                                               selector.Count == 2 &&
                                                               selector.Flag));

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
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Choose &&
                                                               selector.Count == 1 &&
                                                               selector.ExpressionEntryAddress >= 0 &&
                                                               selector.SecondaryExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Choose &&
                                                               selector.SecondaryIdentifierSlot >= 0 &&
                                                               selector.SecondaryExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Choose &&
                                                               selector.Count == 2 &&
                                                               selector.Flag));

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
              let invalidDraw be :set[1, 2, 3][:draw 1]
              let score be (single + hand[3] + prefixed[2]) + 5
              emit Done(score: score, replacement: 6, invalidDraw: invalidDraw)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Draw &&
                                                               selector.Count == 1));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Draw &&
                                                               selector.Count == 3));

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
              let invalidDraw be :set[1, 2, 3][:draw 1]
              let score be (single + hand[3] + prefixed[2]) + 5
              emit Done(score: score, replacement: 6, invalidDraw: invalidDraw)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Draw &&
                                                               selector.Count == 1));
        Assert.IsTrue(compiled.PipelineSelectorPool.Any(selector => selector.Kind == GameEventScriptBytecodePipelineSelectorKind.Draw &&
                                                               selector.Count == 3));

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
    public void BytecodeVmExecutesSeededRandomExpressionBodyFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let value be :random with 7 1
              emit Done(value: value, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPushConstant));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPop));

        var originalConstant = 1L;
        var replacementConstant = 2L;
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
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesSeededRandomExpressionBodyFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let blocker be [1, 2, 3][:first item where item > 0]
              let value be :random with 7 1
              emit Done(value: value, blocker: blocker, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var seededRandomRanges = FindRandomScopeRanges(compiled.Code);
        Assert.IsNotEmpty(seededRandomRanges);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = 1L;
        var replacementConstant = 2L;
        var code = compiled.Code
            .Select((instruction, index) => seededRandomRanges.Any(range => index > range.Start && index < range.End) &&
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
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesSeededRandomStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let blocker be [10, 11, 12][:first item where item > 0]
              let value be (:random with 7 1) + 4
              emit Done(value: value, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPushConstant));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPop));

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
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsSeededRandomStatementExpressionsFromLinearPublicCodeInHandlerStatements()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let blocker be [10, 11, 12][:first item where item > 0]
              let value be (:random with 7 1) + 4
              emit Done(value: value, blocker: blocker, replacement: 6)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPushConstant));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPop));

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
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["blocker"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsSeededRandomExpressionBodyFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let value be :random with 7 1
              emit Done(value: value, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPushConstant));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.RandomPop));

        var originalConstant = 1L;
        var replacementConstant = 2L;
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
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmStepsSeededRandomScopesFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              :random with 7 {
                emit Done(value: 1, replacement: 2)
              }
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var randomScope = FindRandomScopeRanges(compiled.Code).Single();
        var originalConstant = 1L;
        var replacementConstant = 2L;
        var code = compiled.Code
            .Select((instruction, index) => index > randomScope.Start &&
                                           index < randomScope.End &&
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

        Assert.IsTrue(host.Publish(Create("Start")));
        DrainHostWithSingleOpcodeBudget(host);

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesComputedTypeFieldsFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            record :gauge as {
              current: :integer,
              doubled: :integer computed by current + 1
            }

            on Start {
              let hp be :gauge(current: 5)
              emit Done(value: hp.doubled, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var field = compiled.TypeDefinitions["gauge"].Fields.Single(field => field.Name == "doubled");
        Assert.IsGreaterThanOrEqualTo(0, field.ComputedEntryAddress);

        var originalConstant = 1L;
        var replacementConstant = 2L;
        var computedEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            field.ComputedEntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= field.ComputedEntryAddress &&
                                           index < computedEnd &&
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
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
    }

    [TestMethod]
    public void BytecodeVmExecutesTypeFieldClampsFromLinearPublicCode()
    {
        const string script =
            """
            module LinearExecutable

            record :bounded as {
              value: :integer clamped between 1 and 3
            }

            on Start {
              let bounded be :bounded(value: 0)
              emit Done(value: bounded.value, replacement: 2)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var field = compiled.TypeDefinitions["bounded"].Fields.Single(field => field.Name == "value");
        Assert.IsGreaterThanOrEqualTo(0, field.MinimumEntryAddress);

        var originalConstant = 1L;
        var replacementConstant = 2L;
        var minimumEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            field.MinimumEntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= field.MinimumEntryAddress &&
                                           index < minimumEnd &&
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
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["replacement"]);
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
    public void BytecodeVmCanRunThroughHostContract()
    {
        const string script =
            """
            module Runtime

            on Start {
              emit Done(value: 3)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["value"]);
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
    public void BytecodeVmCoercesTypedHandlerParameters()
    {
        const string script =
            """
            module Runtime

            on Start(value as :integer) {
              emit Done(value: value + 1)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesFloat(2d))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmCoercesTypedCallableParameters()
    {
        const string script =
            """
            module Runtime

            predicate high(value as :integer) means value > 2
            function boosted(_ value as :integer) means value + 1

            on Start(value) {
              emit Done(ok: value is high, boosted: boosted(value))
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesFloat(2d))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(false), published[0].Arguments["ok"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["boosted"]);
    }

    [TestMethod]
    public void BytecodeVmCoercesTypedCustomRecordParameters()
    {
        const string script =
            """
            module Runtime

            record :gauge as {
              current: :integer
            }

            on Start(hp as :gauge) {
              emit Done(current: hp.current)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));
        var hp = GameEventScriptValueFactory.GesDictionary(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
        {
            ["current"] = GameEventScriptValueFactory.GesFloat(4d)
        });

        host.PublishToCompletion(Create("Start", ("hp", hp)));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(4), published[0].Arguments["current"]);
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

    private static (int Start, int End)[] GetGuardedChoiceOtherwiseRanges(GameEventScriptCompiled compiled)
    {
        var compiledCode = compiled.Code.ToArray();
        var ranges = new List<(int Start, int End)>();
        for (var index = 0; index < compiledCode.Length; index++)
        {
            var instruction = compiledCode[index];
            if (instruction.OpCode != GameEventScriptBytecodeOpCode.JumpIfNotTrue)
            {
                continue;
            }

            var otherwiseStart = instruction.A_U16;
            if (otherwiseStart <= index || otherwiseStart >= compiledCode.Length)
            {
                continue;
            }

            var branchJumpIndex = Array.FindIndex(
                compiledCode,
                index + 1,
                otherwiseStart - index - 1,
                candidate => candidate.OpCode == GameEventScriptBytecodeOpCode.Jump);
            if (branchJumpIndex < 0)
            {
                continue;
            }

            var branchWritesResult = compiledCode
                .Skip(index + 1)
                .Take(branchJumpIndex - index - 1)
                .Any(candidate => candidate.OpCode == GameEventScriptBytecodeOpCode.MoveSlot);
            if (!branchWritesResult)
            {
                continue;
            }

            var otherwiseEnd = compiledCode[branchJumpIndex].A_U16;
            if (otherwiseEnd > otherwiseStart && otherwiseEnd <= compiledCode.Length)
            {
                ranges.Add((otherwiseStart, otherwiseEnd));
            }
        }

        Assert.IsNotEmpty(ranges);
        Assert.IsTrue(ranges.All(range => range.End > range.Start));
        return ranges.ToArray();
    }

    private static void AssertGuardedChoiceLoweredToLinearJumps(GameEventScriptCompiled compiled)
    {
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.JumpIfNotTrue));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.MoveSlot));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Jump));
    }

    private static (int Start, int End)[] GetGeneratedCollectionLinearRanges(GameEventScriptCompiled compiled)
    {
        var compiledCode = compiled.Code.ToArray();
        var starts = new Stack<int>();
        var ranges = new List<(int Start, int End)>();
        for (var index = 0; index < compiledCode.Length; index++)
        {
            switch (compiledCode[index].OpCode)
            {
                case GameEventScriptBytecodeOpCode.CollectionBuilderList:
                case GameEventScriptBytecodeOpCode.CollectionBuilderSet:
                    starts.Push(index);
                    break;
                case GameEventScriptBytecodeOpCode.CollectionBuilderFinish when starts.Count > 0:
                    ranges.Add((starts.Pop(), index + 1));
                    break;
            }
        }

        Assert.IsNotEmpty(ranges);
        Assert.IsTrue(ranges.All(range => range.End > range.Start));
        return ranges.ToArray();
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
            original.DebugSegment,
            original.PipelinePatternPool.ToArray(),
            original.PipelineObjectPatternPool.ToArray(),
            original.PipelineSelectorPool.ToArray(),
            original.PipelinePool.ToArray());

    private static IReadOnlyList<(int Start, int End)> FindRandomScopeRanges(IReadOnlyList<GameEventScriptBytecodeInstruction> code)
    {
        var stack = new Stack<int>();
        var ranges = new List<(int Start, int End)>();
        for (var index = 0; index < code.Count; index++)
        {
            switch (code[index].OpCode)
            {
                case GameEventScriptBytecodeOpCode.RandomPush:
                case GameEventScriptBytecodeOpCode.RandomPushConstant:
                    stack.Push(index);
                    break;
                case GameEventScriptBytecodeOpCode.RandomPop when stack.Count > 0:
                    ranges.Add((stack.Pop(), index));
                    break;
            }
        }

        return ranges.OrderBy(range => range.Start).ToArray();
    }

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
