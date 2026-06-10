using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.VirtualMachine;

[TestClass]
public sealed class GesVmBinaryRunnerTests
{
    [TestMethod]
    public void RegisterArrayGrowsForLargeStageSequences()
    {
        const string script =
            """
            module BinaryExecutor

            on Start {
                let values be [
                    1, 2, 3, 4, 5, 6, 7, 8,
                    9, 10, 11, 12, 13, 14, 15, 16,
                    17, 18, 19, 20, 21, 22, 23, 24,
                    25, 26, 27, 28, 29, 30, 31, 32,
                    33, 34, 35, 36, 37, 38, 39, 40
                ]
                emit Done(count: :len values)
            }
            """;

        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRandom(GameEventScriptRandomGenerator.FromSeed(1))
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CompileModule(script));
        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Done", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(40), emitted[0].Arguments["count"]);
    }

    [TestMethod]
    public void SeededRandomUsesFullInt64Seed()
    {
        const long seed = 0x1_0000_0001L;
        const string script =
            """
            module BinaryExecutor

            on Start {
                let value be :random with 4294967297 (:random from 1 to 1000000000000)
                emit Done(value: value)
            }
            """;

        var expected = GameEventScriptRandomGenerator
            .FromSeed(seed)
            .NextInclusiveInteger(1, 1_000_000_000_000L);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRandom(GameEventScriptRandomGenerator.FromSeed(1))
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CompileModule(script));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Done", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(expected), emitted[0].Arguments["value"]);
    }
    
}
