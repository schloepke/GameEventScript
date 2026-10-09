// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Conformance;

/// <summary>Verifies that numeric arithmetic does not allocate per executed operation.</summary>
[TestClass]
[DoNotParallelize]
public sealed class NumericArithmeticAllocationTests
{
    /// <summary>Compares warmed dispatch allocation with small and large numeric workloads.</summary>
    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Allocation")]
    [DataRow("7", "2")]
    [DataRow("7.25", "2.5")]
    [DataRow("7m", "2m")]
    [DataRow("7%", "2%")]
    [DataRow("true", "false")]
    [DataRow(":Dice[3, 4]", ":Dice[2]")]
    public void NumericOperationsHaveNoPerIterationAllocation(string left, string right)
    {
        var source = $$"""
            on Start(iterations) {
                let a be {{left}}
                let b be {{right}}
                let result be asm {
                    .register remaining, again
                    Move remaining, iterations
                    repeat:
                    Add result, a, b, #numeric
                    Subtract result, a, b, #numeric
                    Multiply result, a, b, #numeric
                    Divide result, a, b, #numeric
                    Power result, a, b, #numeric
                    IntegerDivide result, a, b, #numeric
                    Modulo result, a, b, #numeric
                    Remainder result, a, b, #numeric
                    Min result, a, b, #numeric
                    Max result, a, b, #numeric
                    Negate result, a, #numeric
                    Abs result, a, #numeric
                    Clamp result, a, b, a, #numeric
                    Move result, a, #numeric
                    Subtract remaining, remaining, 1
                    Greater again, remaining, 0
                    JumpIfTrue again, repeat
                }
            }
            """;
        var program = GameEventScriptBuilder.Create().AddScript(source).Compile();
        var host = GameEventScriptHost.CreateBuilder().WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxExecutionSteps = 0, MaxLoopIterations = 0 }).Build();
        host.Load(program);
        Assert.AreEqual(GameEventScriptStartState.Ready, host.Start().State);
        var message = GameEventScriptMessage.Create("Start", [new("iterations", GesValue.GesInteger(16))]);
        void Run()
        {
            if (!host.Receive(message) || host.RunToCompletion().State != GameEventScriptExecutionState.Completed)
                throw new InvalidOperationException("Numeric allocation workload did not complete.");
        }
        for (var index = 0; index < 10; index++) Run();
        var small = ConformanceCSharpAllocationProvider.MeasureBytes(Run).AllocatedBytes;
        message = GameEventScriptMessage.Create("Start", [new("iterations", GesValue.GesInteger(1024))]);
        for (var index = 0; index < 10; index++) Run();
        var large = ConformanceCSharpAllocationProvider.MeasureBytes(Run).AllocatedBytes;
        Assert.AreEqual(small, large, "Additional numeric instructions must not allocate additional heap storage.");
    }
}
