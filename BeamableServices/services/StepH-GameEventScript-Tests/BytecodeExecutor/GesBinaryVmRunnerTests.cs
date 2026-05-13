using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeExecutor;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Extensions;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.BytecodeExecutor;

[TestClass]
public sealed class BytecodeExecutorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SimpleArithmeticWorks()
    {
        const string script =
            """
            module BinaryExecutor

            on Start(value) {
              let plusTwo be value + 2
              let byThree be value * 3
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var binary = compiled.ToGameEventScriptBinary();
        var published = new List<GameEventScriptMessage>();
        var context = new GameEventScriptContext(
            GameEventScriptRandomGenerator.FromSeed(1),
            message =>
            {
                published.Add(message);
                return true;
            });

        TestContext.WriteLine("-----");
        TestContext.WriteLine("BytecodeVM Dump:\n" + compiled.DumpBytecode());
        TestContext.WriteLine("-----");
        TestContext.WriteLine("Binary file:\n" + binary.Dump());
        TestContext.WriteLine("-----");

        
        var runner = new GameEventScriptVirtualMaschine(binary, 20, 20, 20);
        var handled = runner.ExecuteMessage(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(40))), context);

        Assert.IsTrue(handled);
        //Assert.HasCount(1, published);
        //Assert.AreEqual("Done", published[0].Name);
        //Assert.AreEqual(GameEventScriptValueFactory.GesInteger(42), published[0].Arguments["total"]);
    }
    
}
