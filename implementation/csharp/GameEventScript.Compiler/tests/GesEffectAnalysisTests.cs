// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Compiler;

namespace GameEventScript.Tests.Native.Compiler;

[TestClass]
public sealed class GesEffectAnalysisTests
{
    [TestMethod]
    [DataRow("value + 1")]
    [DataRow("random 1 to 6")]
    [DataRow("forward(value)")]
    public void UnusedRecordCastIsRemovedWhenComputedFieldsHaveNoProgramEffects(string expression)
    {
        var program = Compile($$"""
            record :Sample as { value: :Number, result: :Number computed by {{expression}} }
            function forward(_ value) be finish(value)
            function finish(_ value) be value + 1
            on Start(value) { [value: value] as :Sample; emit Done }
            """);

        Assert.IsFalse(HandlerOpcodes(program).Contains(GesBytecodeOpCode.CastCustom));
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

        Assert.IsFalse(HandlerOpcodes(program).Contains(GesBytecodeOpCode.CastCustom));
    }

    [TestMethod]
    [DataRow("random 1 to 6", GesBytecodeOpCode.RandomTake)]
    [DataRow("value as :List", GesBytecodeOpCode.Cast)]
    public void RandomAndBudgetChecksDoNotKeepUnusedResultsAlive(string expression, GesBytecodeOpCode opcode)
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
        Assert.IsFalse(opcodes.Contains(GesBytecodeOpCode.CastCustom));
        Assert.IsTrue(opcodes.Contains(GesBytecodeOpCode.CallExternal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MessageEffectsPropagateFromFunctionIntoRecordCast(bool publish)
    {
        var builder = new GesBinaryBuilder();
        var message = builder.AddBind(GesBinaryBindKind.OutboundMessage, "Effect");
        var function = builder.DeclareRoutine(GesBinaryBindKind.Function, "effect", ["value"]);
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

        Assert.IsTrue(HandlerOpcodes(builder.Build()).Contains(GesBytecodeOpCode.CastCustom));
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

        Assert.IsTrue(HandlerOpcodes(builder.Build()).Contains(GesBytecodeOpCode.CastCustom));
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

        var error = Assert.ThrowsExactly<GesCompileException>(() => builder.Build());
        Assert.AreEqual(GesDiagnosticCodes.CompileCyclicCallGraph, error.Diagnostics.Single().Code);
    }

    private static GameEventScriptProgram Compile(string source)
        => new GesProgramBuilder().AddScript(source, "effects.ges").Compile(new GesCompileOptions { DebugInfo = GesDebugInfoOptions.None });

    private static GesBytecodeOpCode[] HandlerOpcodes(GameEventScriptProgram program)
    {
        var handler = program.Bindings.Entries.Single(binding => program.StringConstants.Resolve(binding.Name) == "Start");
        return program.Code.Instructions.Skip(handler.EntryAddress).TakeWhile(instruction => instruction.OpCode != GesBytecodeOpCode.ReturnVoid).Select(instruction => instruction.OpCode).ToArray();
    }
}
