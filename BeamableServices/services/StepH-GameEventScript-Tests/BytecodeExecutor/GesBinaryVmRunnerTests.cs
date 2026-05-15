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

            predicate high(_ value) means value > 40
            predicate low(_ value) means value <= 40
            
            function divide(_ dividend, _ divisor) means dividend / divisor
            
            on Start(value) {
                let short be value is high and value is low
                let implies_1 be value is high -> value is low
                let implies_2 be value is high -> not low(value)
                let x be value + 2
                if value is high {
                    publish Done(total: x)
                }
                let y be x * 4
                let z be divide(divide(y, 2), 2)
                if value is low {
                    publish Done(total: z)                
                }
                for x from 1 to 10 {
                    let aa be x * 2
                    let bb be y² + 3
                }
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
        
        var runner = new GameEventScriptVirtualMaschine(binary, 128, 128);
        var handled = runner.ExecuteMessage(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(40))), context);

        Assert.IsTrue(handled);
        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(42), published[0].Arguments["total"]);
    }
    
}
