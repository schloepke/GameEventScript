using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeExecutor;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
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
                let intA be 10
                let intB be 20.0
                
                let resultInt be intA + intB
                
                let someDice be :dice 4d6
                let someList be [1, :b, 'hello', 0.7m]
                let someMap be [a: 3, b: 'Jelly', c: 3.3m, d: :pi]
                let someFlags be [enemy:, visible:, armed:]
                let short be value is high and value is low
                let implies_1 be value is high -> value is low
                let implies_2 be value is high -> value is not low
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
        const string script2 =
            """
            module BinaryExecutor

            function time(_ x) means x * 2
            function calc(_ x) means x * 3
            
            on Start(value) {
                let myHandler be Success(param)
                let a be 10
                let b be 20
                let c be time(a)
                let d be calc(b)
                for x from 0 to 5 {
                    let e be d * x
                }
            }
            """;
        
        var compiled = GameEventScriptManager.Compile(script);
        var binary = compiled.ToGameEventScriptBinary();
        var published = new List<GameEventScriptMessage>();
        var context = new GameEventScriptSession(
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

    [TestMethod]
    public void InitializeRunsInitializationHandler()
    {
        const string script =
            """
            module BinaryExecutor

            on initialization {
                emit Ready(value: 1)
            }
            """;

        var binary = GameEventScriptManager.Compile(script).ToGameEventScriptBinary();
        var emitted = new List<GameEventScriptMessage>();
        var session = new GameEventScriptSession(
            GameEventScriptRandomGenerator.FromSeed(1),
            message =>
            {
                emitted.Add(message);
                return true;
            });

        var runner = new GameEventScriptVirtualMaschine(binary, 128, 128);
        runner.Initialize(session);

        Assert.HasCount(1, emitted);
        Assert.AreEqual("Ready", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), emitted[0].Arguments["value"]);
    }
    
}
