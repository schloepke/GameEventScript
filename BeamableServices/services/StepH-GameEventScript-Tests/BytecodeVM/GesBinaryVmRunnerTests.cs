using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeVM;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.BytecodeVM;

[TestClass]
public sealed class GesBinaryVmRunnerTests
{
    [TestMethod]
    public void BinaryRunnerRunsSimpleHandlerFromGameEventScriptBinary()
    {
        const string script =
            """
            module BinaryRunner

            on Start(value) {
              let total be value + 2
              emit Done(total: total)
            }
            """;

        var binary = GameEventScriptManager.Compile(script).ToGameEventScriptBinary();
        var published = new List<GameEventScriptMessage>();
        var context = new GameEventScriptContext(
            GameEventScriptRandomGenerator.FromSeed(1),
            message =>
            {
                published.Add(message);
                return true;
            });

        var runner = new GesBinaryVmRunner(binary);
        var handled = runner.RunHandler(
            Create("Start", ("value", GameEventScriptValueFactory.GesInteger(40))),
            context);

        Assert.IsTrue(handled);
        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(42), published[0].Arguments["total"]);
    }

    [TestMethod]
    public void BinaryRunnerExecutesBranchAndPublish()
    {
        const string script =
            """
            module BinaryRunnerBranch

            on Start(value) {
              if value > 10 {
                publish High(value: value)
              }
              emit Done(value: value)
            }
            """;

        var binary = GameEventScriptManager.Compile(script).ToGameEventScriptBinary();
        var emitted = new List<GameEventScriptMessage>();
        var published = new List<GameEventScriptMessage>();
        var context = new GameEventScriptContext(
            GameEventScriptRandomGenerator.FromSeed(1),
            message =>
            {
                emitted.Add(message);
                return true;
            },
            publish: message =>
            {
                published.Add(message);
                return true;
            });

        var runner = new GesBinaryVmRunner(binary);
        var handled = runner.RunHandler(
            Create("Start", ("value", GameEventScriptValueFactory.GesInteger(12))),
            context);

        Assert.IsTrue(handled);
        Assert.HasCount(1, published);
        Assert.AreEqual("High", published[0].Name);
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Done", emitted[0].Name);
    }
}
