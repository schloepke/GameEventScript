using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
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
        StringAssert.Contains(first, "LoadConstant");
        StringAssert.Contains(first, "LoadSlot");
        StringAssert.Contains(first, "BuildMessage");
        StringAssert.Contains(first, "Publish");
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
            instruction.Dest >= 0 &&
            instruction.A == instruction.B));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.ShortCircuitImplies &&
            instruction.Dest >= 0 &&
            instruction.A != instruction.B));
    }

    [TestMethod]
    public void PublicLinearBytecodeStoresPublishMetadataInSideTables()
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

        Assert.HasCount(2, compiled.PublishLayouts);
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.PublishValue &&
            instruction.Data >= 0 &&
            instruction.Data < compiled.PublishLayouts.Count));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.PublishMessageValue &&
            instruction.Data >= 0 &&
            instruction.Data < compiled.PublishLayouts.Count));

        var direct = compiled.PublishLayouts.First(layout => layout.MessageName == "Done");
        Assert.AreEqual("Done(value)", direct.SignatureId);
        Assert.HasCount(1, direct.ArgumentNames);
        Assert.HasCount(1, direct.ArgumentSlots);
        Assert.HasCount(1, direct.TagSlots);
        Assert.IsTrue(compiled.OperationLayouts.Any(layout =>
            layout.OpCode == GameEventScriptBytecodeOpCode.BuildMessage &&
            layout.Name == "Done" &&
            layout.Names.SequenceEqual(new[] { "value" })));
    }

    [TestMethod]
    public void PublicLinearBytecodeStoresLoopAndSeededRandomMetadataInSideTables()
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

        Assert.HasCount(1, compiled.LoopLayouts);
        Assert.HasCount(1, compiled.SeededRandomBlockLayouts);
        Assert.IsTrue(compiled.IterationSourceLayouts.Any(layout =>
            layout.Kind == GameEventScriptBytecodeIterationSourceKind.Range &&
            layout.RangeFromSlot >= 0 &&
            layout.RangeToSlot >= 0));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.ForRange &&
            instruction.Data >= 0 &&
            instruction.Data < compiled.LoopLayouts.Count));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.SeededRandomBlock &&
            instruction.Data >= 0 &&
            instruction.Data < compiled.SeededRandomBlockLayouts.Count));
        Assert.IsGreaterThanOrEqualTo(0, compiled.SeededRandomBlockLayouts[0].SeedSlot);
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

        Assert.HasCount(1, compiled.PipelineLayouts);
        Assert.IsTrue(compiled.SelectorLayouts.Any(layout =>
            layout.Kind == GameEventScriptBytecodeSelectorKind.Select &&
            layout.IdentifierSlot >= 0 &&
            layout.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline &&
            instruction.Data >= 0 &&
            instruction.Data < compiled.PipelineLayouts.Count));
        Assert.IsGreaterThanOrEqualTo(0, compiled.PipelineLayouts[0].SourceSlot);
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
        var linearHandler = executable.LinearExecutable.Handlers.Single(entry => entry.SignatureId == handler.SignatureId);
        var callable = compiled.Callables["boosted"];
        var linearCallable = executable.LinearExecutable.Callables.Single(entry => entry.SignatureId == callable.SignatureId);

        Assert.HasCount(compiled.Code.Count, executable.LinearExecutable.Code);
        Assert.AreEqual(compiled.MaxFrameSlots, executable.LinearExecutable.MaxFrameSlots);
        Assert.AreEqual(handler.EntryAddress, linearHandler.EntryAddress);
        Assert.AreEqual(handler.LocalSlotCount, linearHandler.LocalSlotCount);
        Assert.AreEqual(callable.EntryAddress, linearCallable.EntryAddress);
        Assert.AreEqual(callable.ReturnSlot, linearCallable.ReturnSlot);
        Assert.IsTrue(executable.LinearExecutable.Code.Any(instruction =>
            instruction.OpCode == GameEventScriptBytecodeOpCode.PublishValue &&
            instruction.Data >= 0 &&
            instruction.Data < compiled.PublishLayouts.Count));
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
        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var callableEntry = compiled.Callables["boosted"].EntryAddress;
        var callableEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            callableEntry,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= callableEntry &&
                                           index < callableEnd &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesCallablesFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var callableEntry = compiled.Callables["boosted"].EntryAddress;
        var callableEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            callableEntry,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= callableEntry &&
                                           index < callableEnd &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesPredicateTestsFromLinearPublicCodeInCompatibilityStatements()
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

        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var callableEntry = compiled.Callables["high"].EntryAddress;
        var callableEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            callableEntry,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var rewrittenConstantInstructionIndex = compiled.Code
            .Select((instruction, index) => (instruction, index))
            .First(pair => pair.index >= callableEntry &&
                           pair.index < callableEnd &&
                           pair.instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => index == rewrittenConstantInstructionIndex
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesTypeConstructorStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 3)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsTypeConstructorStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 3)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesSimpleOperationLayoutStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Unary));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Cast));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.MemberAccess));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.TypeCheck));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 3)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsSimpleOperationLayoutStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Unary));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Cast));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.MemberAccess));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.TypeCheck));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 3)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesRangeStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Range));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsRangeStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Range));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesVariadicStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsVariadicStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesBuilderStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsBuilderStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesBindHandlerStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsBindHandlerStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesExtensionCallStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallExtension));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsExtensionCallStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.CallExtension));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesFunctionCallStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsFunctionCallStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesPredicateTestStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.PredicateTest));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsPredicateTestStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.PredicateTest));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var callableEntry = compiled.Callables["boosted"].EntryAddress;
        var callableEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            callableEntry,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= callableEntry &&
                                           index < callableEnd &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var callableEntry = compiled.Callables["addOne"].EntryAddress;
        var callableEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            callableEntry,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= callableEntry &&
                                           index < callableEnd &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var otherwiseRanges = GetGuardedChoiceOtherwiseRanges(compiled);

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 3)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => otherwiseRanges.Any(range => index >= range.Start && index < range.End) &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesGuardedChoiceHelpersFromLinearPublicCodeInCompatibilityStatements()
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
        var otherwiseRanges = GetGuardedChoiceOtherwiseRanges(compiled);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 3)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => otherwiseRanges.Any(range => index >= range.Start && index < range.End) &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var otherwiseRanges = GetGuardedChoiceOtherwiseRanges(compiled);

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 3)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => otherwiseRanges.Any(range => index >= range.Start && index < range.End) &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesGuardedChoiceStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.GuardedChoice));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsGuardedChoiceStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.GuardedChoice));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var projectionRanges = GetGeneratedCollectionProjectionRanges(compiled);

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .First(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => projectionRanges.Any(range => index >= range.Start && index < range.End) &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesGeneratedCollectionHelpersFromLinearPublicCodeInCompatibilityStatements()
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
        var projectionRanges = GetGeneratedCollectionProjectionRanges(compiled);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .First(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => projectionRanges.Any(range => index >= range.Start && index < range.End) &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var projectionRanges = GetGeneratedCollectionProjectionRanges(compiled);

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .First(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => projectionRanges.Any(range => index >= range.Start && index < range.End) &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesGeneratedCollectionStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.GeneratedCollection));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsGeneratedCollectionStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.GeneratedCollection));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var loopInstruction = compiled.Code
            .Select((instruction, index) => (instruction, index))
            .Single(pair => pair.instruction.OpCode == GameEventScriptBytecodeOpCode.ForRange);
        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => index >= loopInstruction.instruction.Target &&
                                           index < loopInstruction.instruction.Target2 &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var loopInstruction = compiled.Code
            .Select((instruction, index) => (instruction, index))
            .Single(pair => pair.instruction.OpCode == GameEventScriptBytecodeOpCode.ForCollection);
        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => index >= loopInstruction.instruction.Target &&
                                           index < loopInstruction.instruction.Target2 &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var selector = compiled.SelectorLayouts[compiled.PipelineLayouts.Single().TerminalSelectorLayoutIndex];
        Assert.AreEqual(GameEventScriptBytecodeSelectorKind.Select, selector.Kind);
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
        var selector = compiled.SelectorLayouts[compiled.PipelineLayouts.Single().TerminalSelectorLayoutIndex];
        Assert.IsGreaterThanOrEqualTo(0, selector.ExpressionEntryAddress);

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var selectorEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            selector.ExpressionEntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= selector.ExpressionEntryAddress &&
                                           index < selectorEnd &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesPipelineHandlersOnlyFromLinearEntryAddress()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be [1, 2, 3][:select item => item + 1]
              emit Done(first: values[1])
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var handler = compiled.Handlers["Start"].Single();
        var handlerEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            handler.EntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return);
        Assert.IsGreaterThanOrEqualTo(0, handlerEnd);
        Assert.IsTrue(compiled.Code
            .Skip(handler.EntryAddress)
            .Take(handlerEnd - handler.EntryAddress)
            .Any(instruction => instruction.OpCode is GameEventScriptBytecodeOpCode.PublishValue or GameEventScriptBytecodeOpCode.PublishMessageValue));

        var code = compiled.Code
            .Select((instruction, index) => index >= handler.EntryAddress &&
                                           index < handlerEnd &&
                                           instruction.OpCode is GameEventScriptBytecodeOpCode.PublishValue or GameEventScriptBytecodeOpCode.PublishMessageValue
                ? instruction with { OpCode = GameEventScriptBytecodeOpCode.Nop }
                : instruction)
            .ToArray();
        var rewritten = RebuildCompiledArtifactFromPublicData(compiled, code);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rewritten);

        host.PublishToCompletion(Create("Start"));

        Assert.IsEmpty(published);
    }

    [TestMethod]
    public void BytecodeVmStepsPipelineHandlersOnlyFromLinearEntryAddress()
    {
        const string script =
            """
            module LinearExecutable

            on Start {
              let values be [1, 2, 3][:select item => item + 1]
              emit Done(first: values[1])
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var handler = compiled.Handlers["Start"].Single();
        var handlerEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            handler.EntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return);
        Assert.IsGreaterThanOrEqualTo(0, handlerEnd);
        Assert.IsTrue(compiled.Code
            .Skip(handler.EntryAddress)
            .Take(handlerEnd - handler.EntryAddress)
            .Any(instruction => instruction.OpCode is GameEventScriptBytecodeOpCode.PublishValue or GameEventScriptBytecodeOpCode.PublishMessageValue));

        var code = compiled.Code
            .Select((instruction, index) => index >= handler.EntryAddress &&
                                           index < handlerEnd &&
                                           instruction.OpCode is GameEventScriptBytecodeOpCode.PublishValue or GameEventScriptBytecodeOpCode.PublishMessageValue
                ? instruction with { OpCode = GameEventScriptBytecodeOpCode.Nop }
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

        Assert.IsEmpty(published);
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
        var selector = compiled.SelectorLayouts[compiled.PipelineLayouts.Single().TerminalSelectorLayoutIndex];
        Assert.AreEqual(GameEventScriptBytecodeSelectorKind.Select, selector.Kind);
        Assert.IsGreaterThanOrEqualTo(0, selector.ExpressionEntryAddress);

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var selectorEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            selector.ExpressionEntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= selector.ExpressionEntryAddress &&
                                           index < selectorEnd &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesNumericPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Sum));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Average));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 7)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 9)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsNumericPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Sum));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Average));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 7)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 9)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesPredicatePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Predicate &&
                                                               selector.EdgeMode == "any"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Predicate &&
                                                               selector.EdgeMode == "all"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsPredicatePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Predicate &&
                                                               selector.EdgeMode == "any"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Predicate &&
                                                               selector.EdgeMode == "all"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesEdgePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Edge &&
                                                               selector.EdgeMode == "last"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Edge &&
                                                               selector.EdgeMode == "single"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsEdgePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Edge &&
                                                               selector.EdgeMode == "last"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Edge &&
                                                               selector.EdgeMode == "single"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesExtremaPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Min));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Max));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsExtremaPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Min));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Max));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesContainsPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "single"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "all"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "any"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsContainsPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "single"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "all"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "any"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesPrefixedContainsPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.PipelineLayouts.Any(layout =>
            layout.PrefixSelectorLayoutIndexes.Count > 0 &&
            compiled.SelectorLayouts[layout.TerminalSelectorLayoutIndex].Kind == GameEventScriptBytecodeSelectorKind.Contains));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "single"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "all"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "any"));

        var originalNeedle = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 99)
            .index;
        var replacementNeedle = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 13)
            .index;
        var originalAddend = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementAddend = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalNeedle
                ? instruction with { Data = replacementNeedle }
                : instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                  instruction.Data == originalAddend
                    ? instruction with { Data = replacementAddend }
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
    public void BytecodeVmStepsPrefixedContainsPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.PipelineLayouts.Any(layout =>
            layout.PrefixSelectorLayoutIndexes.Count > 0 &&
            compiled.SelectorLayouts[layout.TerminalSelectorLayoutIndex].Kind == GameEventScriptBytecodeSelectorKind.Contains));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "single"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "all"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Contains &&
                                                               selector.EdgeMode == "any"));

        var originalNeedle = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 99)
            .index;
        var replacementNeedle = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 13)
            .index;
        var originalAddend = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementAddend = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalNeedle
                ? instruction with { Data = replacementNeedle }
                : instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                  instruction.Data == originalAddend
                    ? instruction with { Data = replacementAddend }
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
    public void BytecodeVmExecutesDictionaryPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Dictionary &&
                                                               selector.SecondaryExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Dictionary &&
                                                               selector.SecondaryExpressionEntryAddress >= 0));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsDictionaryPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Dictionary &&
                                                               selector.SecondaryExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Dictionary &&
                                                               selector.SecondaryExpressionEntryAddress >= 0));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesDistinctPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Distinct &&
                                                               selector.ExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Distinct &&
                                                               selector.ExpressionEntryAddress >= 0));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsDistinctPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Distinct &&
                                                               selector.ExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Distinct &&
                                                               selector.ExpressionEntryAddress >= 0));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesGroupByPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.GroupBy));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsGroupByPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.GroupBy));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesReversePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Reverse));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsReversePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Reverse));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesSortPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Sort &&
                                                               selector.EdgeMode == "ascending"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Sort &&
                                                               selector.EdgeMode == "descending"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsSortPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Sort &&
                                                               selector.EdgeMode == "ascending"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Sort &&
                                                               selector.EdgeMode == "descending"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesOrderByPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.OrderBy &&
                                                               selector.EdgeMode == "ascending" &&
                                                               selector.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.OrderBy &&
                                                               selector.EdgeMode == "descending" &&
                                                               selector.ExpressionEntryAddress >= 0));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsOrderByPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.OrderBy &&
                                                               selector.EdgeMode == "ascending" &&
                                                               selector.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.OrderBy &&
                                                               selector.EdgeMode == "descending" &&
                                                               selector.ExpressionEntryAddress >= 0));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesSequenceSlicePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "take" &&
                                                               selector.SecondaryMode == "highest"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "drop" &&
                                                               selector.SecondaryMode == "lowest"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsSequenceSlicePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "take" &&
                                                               selector.SecondaryMode == "highest"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "drop" &&
                                                               selector.SecondaryMode == "lowest"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesShufflePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Shuffle));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsShufflePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Shuffle));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesSeriesPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.SeriesTerm &&
                                                               selector.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "take" &&
                                                               selector.SecondaryMode == "first"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "drop" &&
                                                               selector.SecondaryMode == "first"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 7)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 8)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsSeriesPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.SeriesTerm &&
                                                               selector.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "take" &&
                                                               selector.SecondaryMode == "first"));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.SequenceSlice &&
                                                               selector.EdgeMode == "drop" &&
                                                               selector.SecondaryMode == "first"));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 7)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 8)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesPatternPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Pattern &&
                                                               selector.DicePatternLayoutIndex >= 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.TakePattern &&
                                                               selector.DicePatternLayoutIndex >= 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.ObjectMatch &&
                                                               selector.ObjectMatchPatternLayoutIndex >= 0));
        Assert.IsTrue(compiled.DicePatternLayouts.Any(pattern => pattern.Kind == GameEventScriptBytecodeDicePatternKind.Count &&
                                                                 pattern.Count == 2 &&
                                                                 pattern.FaceEntryAddress >= 0));
        Assert.IsTrue(compiled.ObjectMatchPatternLayouts.Any(pattern => pattern.Entries.Any(entry =>
            entry.ValueKind == GameEventScriptBytecodeObjectMatchValueKind.Expression &&
            entry.ExpressionEntryAddress >= 0)));
        Assert.IsTrue(compiled.ObjectMatchPatternLayouts.Any(pattern => pattern.Entries.Any(entry =>
            entry.ValueKind == GameEventScriptBytecodeObjectMatchValueKind.Nested &&
            entry.NestedPatternLayoutIndex >= 0)));

        var replacements = new Dictionary<int, int>
        {
            [FindTextConstant(compiled, "Ten")] = FindTextConstant(compiled, "Ace"),
            [FindTextConstant(compiled, "Queen")] = FindTextConstant(compiled, "Ace"),
            [FindTextConstant(compiled, "player")] = FindTextConstant(compiled, "bot"),
            [FindTextConstant(compiled, "green")] = FindTextConstant(compiled, "red")
        };
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   replacements.TryGetValue(instruction.Data, out var replacement)
                ? instruction with { Data = replacement }
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
    public void BytecodeVmStepsPatternPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Pattern &&
                                                               selector.DicePatternLayoutIndex >= 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.TakePattern &&
                                                               selector.DicePatternLayoutIndex >= 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.ObjectMatch &&
                                                               selector.ObjectMatchPatternLayoutIndex >= 0));
        Assert.IsTrue(compiled.DicePatternLayouts.Any(pattern => pattern.Kind == GameEventScriptBytecodeDicePatternKind.Count &&
                                                                 pattern.Count == 2 &&
                                                                 pattern.FaceEntryAddress >= 0));
        Assert.IsTrue(compiled.ObjectMatchPatternLayouts.Any(pattern => pattern.Entries.Any(entry =>
            entry.ValueKind == GameEventScriptBytecodeObjectMatchValueKind.Expression &&
            entry.ExpressionEntryAddress >= 0)));
        Assert.IsTrue(compiled.ObjectMatchPatternLayouts.Any(pattern => pattern.Entries.Any(entry =>
            entry.ValueKind == GameEventScriptBytecodeObjectMatchValueKind.Nested &&
            entry.NestedPatternLayoutIndex >= 0)));

        var replacements = new Dictionary<int, int>
        {
            [FindTextConstant(compiled, "Ten")] = FindTextConstant(compiled, "Ace"),
            [FindTextConstant(compiled, "Queen")] = FindTextConstant(compiled, "Ace"),
            [FindTextConstant(compiled, "player")] = FindTextConstant(compiled, "bot"),
            [FindTextConstant(compiled, "green")] = FindTextConstant(compiled, "red")
        };
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   replacements.TryGetValue(instruction.Data, out var replacement)
                ? instruction with { Data = replacement }
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
    public void BytecodeVmExecutesChoosePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Choose &&
                                                               selector.Count == 1 &&
                                                               selector.ExpressionEntryAddress >= 0 &&
                                                               selector.SecondaryExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Choose &&
                                                               selector.SecondaryIdentifierSlot >= 0 &&
                                                               selector.SecondaryExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Choose &&
                                                               selector.Count == 2 &&
                                                               selector.Flag));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsChoosePipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Choose &&
                                                               selector.Count == 1 &&
                                                               selector.ExpressionEntryAddress >= 0 &&
                                                               selector.SecondaryExpressionEntryAddress < 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Choose &&
                                                               selector.SecondaryIdentifierSlot >= 0 &&
                                                               selector.SecondaryExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Choose &&
                                                               selector.Count == 2 &&
                                                               selector.Flag));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesDrawPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Draw &&
                                                               selector.Count == 1));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Draw &&
                                                               selector.Count == 3));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsDrawPipelineStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Draw &&
                                                               selector.Count == 1));
        Assert.IsTrue(compiled.SelectorLayouts.Any(selector => selector.Kind == GameEventScriptBytecodeSelectorKind.Draw &&
                                                               selector.Count == 3));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 5)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var seededRandomInstruction = compiled.Code.First(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.SeededRandom);
        var layout = compiled.OperationLayouts[seededRandomInstruction.Data];
        Assert.IsGreaterThanOrEqualTo(0, layout.ExpressionEntryAddress);

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var bodyEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            layout.ExpressionEntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= layout.ExpressionEntryAddress &&
                                           index < bodyEnd &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesSeededRandomExpressionBodyFromLinearPublicCodeInCompatibilityStatements()
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
        var seededRandomLayouts = compiled.OperationLayouts
            .Where(entry => entry.OpCode == GameEventScriptBytecodeOpCode.SeededRandom)
            .ToArray();
        Assert.IsNotEmpty(seededRandomLayouts);
        Assert.IsTrue(seededRandomLayouts.All(layout => layout.ExpressionEntryAddress >= 0));
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Pipeline));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var compiledCode = compiled.Code.ToArray();
        var bodyRanges = seededRandomLayouts
            .Select(layout => (
                Start: layout.ExpressionEntryAddress,
                End: Array.FindIndex(
                    compiledCode,
                    layout.ExpressionEntryAddress,
                    instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1))
            .ToArray();
        Assert.IsTrue(bodyRanges.All(range => range.End > range.Start));
        var code = compiled.Code
            .Select((instruction, index) => bodyRanges.Any(range => index >= range.Start && index < range.End) &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmExecutesSeededRandomStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.SeededRandom));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsSeededRandomStatementExpressionsFromLinearPublicCodeInCompatibilityStatements()
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
        Assert.IsTrue(compiled.Code.Any(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.SeededRandom));

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 4)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 6)
            .index;
        var code = compiled.Code
            .Select(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                   instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
        var seededRandomInstruction = compiled.Code.First(instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.SeededRandom);
        var layout = compiled.OperationLayouts[seededRandomInstruction.Data];
        Assert.IsGreaterThanOrEqualTo(0, layout.ExpressionEntryAddress);

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var bodyEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            layout.ExpressionEntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= layout.ExpressionEntryAddress &&
                                           index < bodyEnd &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
    public void BytecodeVmStepsSeededRandomBlocksFromLinearPublicCode()
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
        var blockInstruction = compiled.Code
            .Select((instruction, index) => (instruction, index))
            .Single(pair => pair.instruction.OpCode == GameEventScriptBytecodeOpCode.SeededRandomBlock);
        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var code = compiled.Code
            .Select((instruction, index) => index >= blockInstruction.instruction.Target &&
                                           index < blockInstruction.instruction.Target2 &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var computedEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            field.ComputedEntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= field.ComputedEntryAddress &&
                                           index < computedEnd &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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

        var originalConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 1)
            .index;
        var replacementConstant = compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Integer && pair.constant.Integer == 2)
            .index;
        var minimumEnd = Array.FindIndex(
            compiled.Code.ToArray(),
            field.MinimumEntryAddress,
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1;
        var code = compiled.Code
            .Select((instruction, index) => index >= field.MinimumEntryAddress &&
                                           index < minimumEnd &&
                                           instruction.OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
                                           instruction.Data == originalConstant
                ? instruction with { Data = replacementConstant }
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
            instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.PublishValue);
        Assert.IsGreaterThanOrEqualTo(0, publishInstructionIndex);
        code[publishInstructionIndex] = code[publishInstructionIndex] with { Data = compiled.PublishLayouts.Count };
        var invalid = RebuildCompiledArtifactFromPublicData(compiled, code);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => GesBytecodeVmExecutableBuilder.Build(invalid));
        StringAssert.Contains(exception.Message, "publish layout");
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
        Assert.AreEqual(
            typeof(IReadOnlyList<GameEventScriptBytecodeConstant>),
            compiledType.GetProperty(nameof(GameEventScriptCompiled.ConstantPool))!.PropertyType,
            "GameEventScriptCompiled constants should use portable bytecode constants, not runtime values.");

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

        Assert.AreEqual("Start(value)", handler.SignatureId);
        Assert.AreEqual("boosted(_)", callable.SignatureId);
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
        Assert.IsNotEmpty(compiled.GuardedChoiceLayouts);
        Assert.IsTrue(compiled.GuardedChoiceLayouts.All(layout => layout.ConditionEntryAddresses.All(address => address >= 0)));
        Assert.IsTrue(compiled.GuardedChoiceLayouts.All(layout => layout.ValueEntryAddresses.All(address => address >= 0)));
        Assert.IsTrue(compiled.GuardedChoiceLayouts.All(layout => layout.OtherwiseEntryAddress >= 0));

        var compiledCode = compiled.Code.ToArray();
        var ranges = compiled.GuardedChoiceLayouts
            .Select(layout => (
                Start: layout.OtherwiseEntryAddress,
                End: Array.FindIndex(
                    compiledCode,
                    layout.OtherwiseEntryAddress,
                    instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1))
            .ToArray();
        Assert.IsTrue(ranges.All(range => range.End > range.Start));
        return ranges;
    }

    private static (int Start, int End)[] GetGeneratedCollectionProjectionRanges(GameEventScriptCompiled compiled)
    {
        Assert.IsNotEmpty(compiled.GeneratedCollectionLayouts);
        Assert.IsTrue(compiled.GeneratedCollectionLayouts.All(layout => layout.ProjectionEntryAddress >= 0));

        var compiledCode = compiled.Code.ToArray();
        var ranges = compiled.GeneratedCollectionLayouts
            .Select(layout => (
                Start: layout.ProjectionEntryAddress,
                End: Array.FindIndex(
                    compiledCode,
                    layout.ProjectionEntryAddress,
                    instruction => instruction.OpCode == GameEventScriptBytecodeOpCode.Return) + 1))
            .ToArray();
        Assert.IsTrue(ranges.All(range => range.End > range.Start));
        return ranges;
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
        var stack = new Stack<Type>();
        stack.Push(rootType);

        while (stack.Count > 0)
        {
            var type = stack.Pop();
            if (!visited.Add(type))
            {
                continue;
            }

            foreach (var nestedType in ExpandType(type))
            {
                stack.Push(nestedType);
            }

            if (!IsGameEventScriptPublicModelType(type))
            {
                continue;
            }

            foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            {
                stack.Push(property.PropertyType);
            }

            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            {
                stack.Push(field.FieldType);
            }

            foreach (var constructor in type.GetConstructors())
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    stack.Push(parameter.ParameterType);
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
                Optimize = original.Options.Optimize
            },
            original.StringPool.ToArray(),
            original.ConstantPool.Select(CloneConstant).ToArray(),
            original.Signatures.ToArray(),
            original.ExternalReferences
                .Select(reference => new GameEventScriptExtensionReference(reference.ExtensionName, reference.FunctionName, reference.ArgumentLabels.ToArray()))
                .ToArray(),
            original.ExternalTypeConstructorReferences
                .Select(reference => new GameEventScriptExternalTypeConstructorReference(reference.TypeName, reference.ArgumentLabels.ToArray()))
                .ToArray(),
            original.NamedArgumentLayouts.Select(layout => (IReadOnlyList<string>)layout.ToArray()).ToArray(),
            original.TypeMetadata.ToArray(),
            original.Callables.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            original.Handlers.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<GameEventScriptBytecodeHandler>)pair.Value.ToArray(), StringComparer.Ordinal),
            original.TypeDefinitions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            code?.ToArray() ?? original.Code.ToArray(),
            original.MaxFrameSlots,
            original.OperationLayouts.ToArray(),
            original.DiagnosticLayouts.ToArray(),
            original.PublishLayouts.ToArray(),
            original.IterationSourceLayouts.ToArray(),
            original.LoopLayouts.ToArray(),
            original.SeededRandomBlockLayouts.ToArray(),
            original.DicePatternLayouts.ToArray(),
            original.ObjectMatchPatternLayouts.ToArray(),
            original.SelectorLayouts.ToArray(),
            original.PipelineLayouts.ToArray(),
            original.GeneratedCollectionLayouts.ToArray(),
            original.GuardedChoiceLayouts.ToArray());

    private static int FindTextConstant(GameEventScriptCompiled compiled, string value)
        => compiled.ConstantPool
            .Select((constant, index) => (constant, index))
            .Single(pair => pair.constant.Kind == GameEventScriptBytecodeConstantKind.Text &&
                            string.Equals(pair.constant.Text, value, StringComparison.Ordinal))
            .index;

    private static GameEventScriptBytecodeConstant CloneConstant(GameEventScriptBytecodeConstant constant)
        => constant.Kind switch
        {
            GameEventScriptBytecodeConstantKind.Nothing => GameEventScriptBytecodeConstant.Nothing(),
            GameEventScriptBytecodeConstantKind.Boolean => GameEventScriptBytecodeConstant.FromBoolean(constant.Boolean),
            GameEventScriptBytecodeConstantKind.Integer => GameEventScriptBytecodeConstant.FromInteger(constant.Integer),
            GameEventScriptBytecodeConstantKind.Float => GameEventScriptBytecodeConstant.FromFloat(
                constant.Number,
                constant.Unit,
                constant.IsNaN,
                constant.IsInfinity,
                constant.IsNegativeInfinity),
            GameEventScriptBytecodeConstantKind.Percentage => GameEventScriptBytecodeConstant.FromPercentage(constant.Number),
            GameEventScriptBytecodeConstantKind.Text => GameEventScriptBytecodeConstant.FromText(constant.Text ?? string.Empty),
            GameEventScriptBytecodeConstantKind.Tag => GameEventScriptBytecodeConstant.FromTag(constant.Text ?? string.Empty),
            GameEventScriptBytecodeConstantKind.Handler => GameEventScriptBytecodeConstant.FromHandler(constant.Text ?? string.Empty, constant.Labels.ToArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(constant), constant.Kind, "Unknown bytecode constant kind.")
        };

    [TestMethod]
    public void BytecodeVmRunsGeneratedCollectionsByDefault()
    {
        const string script =
            """
            module Collections

            on Start {
              let values be :list[:select item from 1 to 3 => item]
              emit Done(count: :len values)
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
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["count"]);
    }

    [TestMethod]
    public void BytecodeVmRunsIfElse()
    {
        const string script =
            """
            module Branches

            on Start(first, second) {
              if first {
                let branch be 1
                emit Branch(value: branch)
              } else if second {
                let branch be 2
                emit Branch(value: branch)
              } else {
                let branch be 3
                emit Branch(value: branch)
              }
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start", ("first", GameEventScriptValueFactory.GesBoolean(true)), ("second", GameEventScriptValueFactory.GesBoolean(false))));
        host.PublishToCompletion(Create("Start", ("first", GameEventScriptValueFactory.GesBoolean(false)), ("second", GameEventScriptValueFactory.GesBoolean(true))));
        host.PublishToCompletion(Create("Start", ("first", GameEventScriptValueFactory.GesBoolean(false)), ("second", GameEventScriptValueFactory.GesBoolean(false))));

        Assert.HasCount(3, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[1].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[2].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmIfBlockDoesNotLeakLocals()
    {
        const string script =
            """
            module Branches

            on Start(flag) {
              if flag {
                let inner be 7
              }

              emit Done(inner: inner)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start", ("flag", GameEventScriptValueFactory.GesBoolean(true))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["inner"]);
    }

    [TestMethod]
    public void BytecodeVmRunsCollectionFor()
    {
        const string script =
            """
            module Loops

            on Start(items) {
              for item in items {
                if item > 1 {
                  emit Item(value: item * 2)
                }
              }
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create(
            "Start",
            ("items", GameEventScriptValueFactory.GesList(
            [
                GameEventScriptValueFactory.GesInteger(1),
                GameEventScriptValueFactory.GesInteger(2),
                GameEventScriptValueFactory.GesInteger(3)
            ]))));

        Assert.HasCount(2, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(4), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[1].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmCollectionForBlockDoesNotLeakLocals()
    {
        const string script =
            """
            module Loops

            on Start(items) {
              for item in items {
                let inner be item
              }

              emit Done(item: item, inner: inner)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create(
            "Start",
            ("items", GameEventScriptValueFactory.GesList(
            [
                GameEventScriptValueFactory.GesInteger(1)
            ]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["item"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["inner"]);
    }

    [TestMethod]
    public void BytecodeVmRunsTypedLet()
    {
        const string script =
            """
            module TypedLets

            on Start {
              let numberOk as :float be '12.2'
              let numberFail as :float be 'abc'
              let integerOk as :integer be '12.7'
              let percentageOk as :percentage be 5
              let degreeOk as :degree be 450
              let textOk as :text be 43.9°
              let unitErased as :float be 43.9°
              let listOk as :list be 'ab'
              let optionalNone as :optional be missing
              emit Done(numberOk: numberOk, numberFail: numberFail, integerOk: integerOk, percentageOk: percentageOk, degreeOk: degreeOk, textOk: textOk, unitErased: unitErased, listOk: listOk, optionalNone: optionalNone)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(12.2d), published[0].Arguments["numberOk"]);
        Assert.IsTrue(published[0].Arguments["numberFail"].IsNaN());
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), published[0].Arguments["integerOk"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesPercentage(0.05d), published[0].Arguments["percentageOk"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(450d, GameEventScriptNumericUnit.Degree), published[0].Arguments["degreeOk"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("43.9°"), published[0].Arguments["textOk"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(43.9d), published[0].Arguments["unitErased"]);
        Assert.HasCount(2, published[0].Arguments["listOk"].AsList());
        Assert.IsFalse(published[0].Arguments["optionalNone"].AsOptional().HasValue);
    }

    [TestMethod]
    public void BytecodeVmRunsMemberAndIndexedAccess()
    {
        const string script =
            """
            module Access

            on Start(player, key, items, index, units) {
              emit Done(
                hpByMember: player.hp,
                hpByTagKey: player[:hp],
                hpByVariableKey: player[key],
                itemByIndex: items[index],
                nestedName: units[2].name,
                missingMember: player.missing,
                missingIndex: items[99])
            }
            """;

        var unitOne = GameEventScriptValueFactory.GesDictionary(new Dictionary<string, GameEventScriptValue>
        {
            ["name"] = GameEventScriptValueFactory.GesText("Scout")
        });
        var unitTwo = GameEventScriptValueFactory.GesDictionary(new Dictionary<string, GameEventScriptValue>
        {
            ["name"] = GameEventScriptValueFactory.GesText("Knight")
        });

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create(
            "Start",
            ("player", GameEventScriptValueFactory.GesDictionary(new Dictionary<string, GameEventScriptValue>
            {
                ["hp"] = GameEventScriptValueFactory.GesInteger(12)
            })),
            ("key", GameEventScriptValueFactory.GesText("hp")),
            ("items", GameEventScriptValueFactory.GesList(
            [
                GameEventScriptValueFactory.GesInteger(10),
                GameEventScriptValueFactory.GesInteger(20),
                GameEventScriptValueFactory.GesInteger(30)
            ])),
            ("index", GameEventScriptValueFactory.GesInteger(2)),
            ("units", GameEventScriptValueFactory.GesList([unitOne, unitTwo]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), published[0].Arguments["hpByMember"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), published[0].Arguments["hpByTagKey"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), published[0].Arguments["hpByVariableKey"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(20), published[0].Arguments["itemByIndex"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Knight"), published[0].Arguments["nestedName"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["missingMember"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["missingIndex"]);
    }

    [TestMethod]
    public void BytecodeVmRunsCollectionLiterals()
    {
        const string script =
            """
            module Literals

            on Start(seed) {
              let doubled be seed * 2
              let list be [seed, doubled, [label: 'nested']]
              let setValues be :set[seed, seed, 3]
              let dict be [hp: seed + 5, name: 'Scout', nested: [values: [1, 2]], tags: setValues]
              let emptyList be []
              let emptySet be :set[]
              let emptyDict be [:]

              emit Done(
                list: list,
                listFirst: list[1],
                listSecond: list[2],
                nestedLabel: list[3].label,
                setValues: setValues,
                dict: dict,
                hp: dict.hp,
                nestedSecond: dict.nested.values[2],
                tagValues: dict.tags,
                emptyList: emptyList,
                emptySet: emptySet,
                emptyDict: emptyDict)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start", ("seed", GameEventScriptValueFactory.GesInteger(7))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["listFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(14), published[0].Arguments["listSecond"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("nested"), published[0].Arguments["nestedLabel"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), published[0].Arguments["hp"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["nestedSecond"]);

        var list = published[0].Arguments["list"].AsList();
        Assert.HasCount(3, list);

        var set = published[0].Arguments["setValues"].AsSet();
        Assert.HasCount(2, set);
        CollectionAssert.Contains(set.ToList(), GameEventScriptValueFactory.GesInteger(3));
        CollectionAssert.Contains(set.ToList(), GameEventScriptValueFactory.GesInteger(7));

        var dictionary = published[0].Arguments["dict"].AsDictionary();
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Scout"), dictionary["name"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), dictionary["hp"]);

        var tagValues = published[0].Arguments["tagValues"].AsSet();
        Assert.HasCount(2, tagValues);
        Assert.AreEqual(GameEventScriptValueFactory.GesList([]), published[0].Arguments["emptyList"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesSet([]), published[0].Arguments["emptySet"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesDictionary(new Dictionary<string, GameEventScriptValue>()), published[0].Arguments["emptyDict"]);
    }

    [TestMethod]
    public void BytecodeVmRunsTypeCheck()
    {
        const string script =
            """
            module TypeChecks

            on Start(custom, msg, handler) {
              let integerValue be 12
              let percentValue be 5%
              let degreeValue be 90°
              let meterValue be 100m
              let secondValue be 15s
              let listValue be [1]
              let dictValue be [name: 'Ada']
              let setValue be :set[1, 1, 2]

              emit Done(
                intIsInteger: integerValue is :integer,
                intIsFloat: integerValue is :float,
                percentIsFloat: percentValue is :float,
                degreeIsFloat: degreeValue is :float,
                degreeIsDegree: degreeValue is :degree,
                meterIsMeter: meterValue is :meter,
                secondIsSecond: secondValue is :second,
                textIsText: 'x' is :text,
                tagIsTag: :ready is :tag,
                boolIsBoolean: true is :boolean,
                listIsList: listValue is :list,
                dictIsDictionary: dictValue is :dictionary,
                setIsSet: setValue is :set,
                customIsGauge: custom is :gauge,
                customIsDictionary: custom is :dictionary,
                msgIsMessage: msg is :message,
                msgIsDictionary: msg is :dictionary,
                handlerIsHandler: handler is :handler,
                missingIsNothing: missing is :nothing)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create(
            "Start",
            ("custom", GameEventScriptValueFactory.GesCustomType("gauge", new Dictionary<string, GameEventScriptValue>
            {
                ["current"] = GameEventScriptValueFactory.GesInteger(5)
            })),
            ("msg", GameEventScriptValueFactory.GesMessage(Create("Ping", ("value", GameEventScriptValueFactory.GesInteger(1))))),
            ("handler", GameEventScriptValueFactory.GesHandler(GameEventScriptMessageSignature.Create("Ping", ["value"])))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["intIsInteger"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["intIsFloat"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["percentIsFloat"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["degreeIsFloat"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["degreeIsDegree"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["meterIsMeter"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["secondIsSecond"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["textIsText"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["tagIsTag"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["boolIsBoolean"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["listIsList"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["dictIsDictionary"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["setIsSet"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["customIsGauge"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["customIsDictionary"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["msgIsMessage"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(false), published[0].Arguments["msgIsDictionary"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["handlerIsHandler"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["missingIsNothing"]);
    }

    [TestMethod]
    public void BytecodeVmRunsDirectMessageLiteralExpression()
    {
        const string script =
            """
            module MessageExpressions

            on Start(value) {
              let scaled be value * 2
              let myMessageDirect be Success(message: 'world', value: scaled)

              emit Done(
                isMessage: myMessageDirect is :message,
                name: myMessageDirect.name,
                signature: myMessageDirect.signatureid,
                text: myMessageDirect.arguments.message,
                value: myMessageDirect.arguments.value)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(21))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["isMessage"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success"), published[0].Arguments["name"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success(message,value)"), published[0].Arguments["signature"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("world"), published[0].Arguments["text"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(42d), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmRunsHandlerLiteralAndBind()
    {
        const string script =
            """
            module HandlerExpressions

            on Start(success) {
              let myHandler be Success(message, value)
              let myMessage be myHandler(message: 'hello', value: success)
              let invalidMessage be myHandler(message: 'hello', other: success)

              emit Done(
                handlerIsHandler: myHandler is :handler,
                handlerName: myHandler.name,
                handlerSignature: myHandler.signatureid,
                secondParameter: myHandler.parameters[2],
                messageIsMessage: myMessage is :message,
                messageName: myMessage.name,
                messageSignature: myMessage.signatureid,
                text: myMessage.arguments.message,
                value: myMessage.arguments.value,
                invalidIsNothing: invalidMessage is :nothing)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.PublishToCompletion(Create("Start", ("success", GameEventScriptValueFactory.GesBoolean(true))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["handlerIsHandler"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success"), published[0].Arguments["handlerName"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success(message,value)"), published[0].Arguments["handlerSignature"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("value"), published[0].Arguments["secondParameter"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["messageIsMessage"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success"), published[0].Arguments["messageName"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success(message,value)"), published[0].Arguments["messageSignature"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("hello"), published[0].Arguments["text"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["invalidIsNothing"]);
    }

    [TestMethod]
    public void BytecodeVmEmitsDiagnosticsWhenEnabled()
    {
        const string script =
            """
            module Diagnostics

            predicate high(value) means value > 3

            on Start(value) {
              let score be value + 2
              let missingValue be missing
              let isHigh be score is high
              emit Done(score: score, missingValue: missingValue, isHigh: isHigh)
            }
            """;

        var collector = new GameEventScriptDiagnosticTraceCollector();
        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collector)
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(
                script,
                new GameEventScriptCompileOptions { EnableDiagnostics = true }));

        host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["missingValue"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["isHigh"]);
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.ParameterBound && diagnostic.Name == "value"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.HandlerInvoked && diagnostic.Name == "Start"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.LetEvaluated && diagnostic.Name == "score"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.LetEvaluated && diagnostic.Name == "missingValue"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing && diagnostic.Name == "missingValue"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.PredicateCalled && diagnostic.Name == "high"));
    }

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
