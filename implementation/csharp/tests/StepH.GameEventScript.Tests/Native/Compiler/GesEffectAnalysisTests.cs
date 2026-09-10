// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;

namespace StepH_GameEventScript_Tests.Native.Compiler;

[TestClass]
public sealed class GesEffectAnalysisTests
{
    [TestMethod]
    [DataRow("value + 1")]
    [DataRow("random 1 to 6")]
    [DataRow("parse value")]
    [DataRow("forward(value)")]
    public void UnusedRecordCastIsRemovedWhenComputedFieldsHaveNoProgramEffects(string expression)
    {
        var program = Compile($$"""
            record :Sample as { value: :Number, result: :Number computed by {{expression}} }
            function forward(_ value) be finish(value)
            function finish(_ value) be value + 1
            on Start(value) { [value: value] as :Sample; emit Done }
            """);

        Assert.IsFalse(HandlerOpcodes(program).Contains(GameEventScriptBytecodeOpCode.CastCustom));
    }

    [TestMethod]
    [DataRow(":Inner(value: value)")]
    [DataRow("[value: value] as :Inner")]
    public void PureNestedRecordsDoNotBlockRemoval(string expression)
    {
        var program = Compile($$"""
            record :Outer as { value: :Number, inner: :Inner computed by {{expression}} }
            record :Inner as { value: :Number, result: :Number computed by random 1 to 6 }
            on Start(value) { [value: value] as :Outer; emit Done }
            """);

        Assert.IsFalse(HandlerOpcodes(program).Contains(GameEventScriptBytecodeOpCode.CastCustom));
    }

    [TestMethod]
    [DataRow("random 1 to 6", GameEventScriptBytecodeOpCode.RandomTake)]
    [DataRow("parse value", GameEventScriptBytecodeOpCode.ParseLiteral)]
    [DataRow("value as :List", GameEventScriptBytecodeOpCode.Cast)]
    public void RandomAndBudgetChecksDoNotKeepUnusedResultsAlive(string expression, GameEventScriptBytecodeOpCode opcode)
    {
        var program = Compile($$"""
            on Start(value) { {{expression}}; emit Done }
            """);

        Assert.IsFalse(HandlerOpcodes(program).Contains(opcode));
    }

    [TestMethod]
    public void RemovingPureCastRetainsItsEffectfulInput()
    {
        var program = Compile("""
            record :Sample as { value: :Number }
            on Start(value) { (:test.notify(value)) as :Sample; emit Done }
            """);

        var opcodes = HandlerOpcodes(program);
        Assert.IsFalse(opcodes.Contains(GameEventScriptBytecodeOpCode.CastCustom));
        Assert.IsTrue(opcodes.Contains(GameEventScriptBytecodeOpCode.CallExternal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MessageEffectsPropagateFromFunctionIntoRecordCast(bool publish)
    {
        var builder = new GesBinaryBuilder();
        var message = builder.AddBind(GameEventScriptBinaryBindKind.OutboundMessage, "Effect");
        var function = builder.DeclareRoutine(GameEventScriptBinaryBindKind.Function, "effect", ["value"]);
        using (var record = builder.BeginRecordConstructor("Sample", ["value"]))
        {
            var result = record.AddTemporaryRegister();
            var map = record.AddTemporaryRegister();
            var custom = record.AddTemporaryRegister();
            builder.StageRegister(record.Argument("value"));
            builder.Call(result, function.EntryLabel);
            builder.StageRegister(result);
            builder.CreateMap(map, ["value"]);
            builder.CreateRecordValue(custom, map, "Sample");
            builder.ReturnValue(custom);
        }

        using (var effect = builder.BeginRoutine(function))
        {
            if (publish) builder.PublishMessage(message, []);
            else builder.EmitMessage(message, []);
            builder.ReturnValue(effect.Argument("value"));
        }

        using (var handler = builder.BeginHandler("Start", ["value"]))
        {
            builder.CastCustom(handler.AddTemporaryRegister(), handler.Argument("value"), "Sample");
            builder.ReturnVoid();
        }

        Assert.IsTrue(HandlerOpcodes(builder.Build()).Contains(GameEventScriptBytecodeOpCode.CastCustom));
    }

    [TestMethod]
    public void UnresolvedCastTargetIsConservativelyRetained()
    {
        var builder = new GesBinaryBuilder();
        using (var handler = builder.BeginHandler("Start", ["value"]))
        {
            builder.CastCustom(handler.AddTemporaryRegister(), handler.Argument("value"), "Unknown");
            builder.ReturnVoid();
        }

        Assert.IsTrue(HandlerOpcodes(builder.Build()).Contains(GameEventScriptBytecodeOpCode.CastCustom));
    }

    [TestMethod]
    public void OptimizerDoesNotHideCyclicRecordCastsFromValidation()
    {
        var builder = new GesBinaryBuilder();
        using (var record = builder.BeginRecordConstructor("Cycle", ["value"]))
        {
            builder.CastCustom(record.AddTemporaryRegister(), record.Argument("value"), "Cycle");
            builder.ReturnValue(record.Argument("value"));
        }

        var error = Assert.ThrowsExactly<GameEventScriptCompileException>(() => builder.Build());
        Assert.AreEqual(GameEventScriptDiagnosticCodes.CompileCyclicCallGraph, error.Diagnostics.Single().Code);
    }

    private static GameEventScriptProgram Compile(string source)
        => GameEventScriptBuilder.Create().AddScript(source, "effects.ges").Compile(new GameEventScriptCompileOptions { DebugInfo = GameEventScriptDebugInfoOptions.None });

    private static GameEventScriptBytecodeOpCode[] HandlerOpcodes(GameEventScriptProgram program)
    {
        var handler = program.Bindings.Entries.Single(binding => program.StringConstants.Resolve(binding.Name) == "Start");
        return program.Code.Instructions.Skip(handler.EntryAddress).TakeWhile(instruction => instruction.OpCode != GameEventScriptBytecodeOpCode.ReturnVoid).Select(instruction => instruction.OpCode).ToArray();
    }
}
