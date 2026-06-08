using StepH.GameEventScript;
using StepH.GameEventScript.Api;
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
        const string script1 =
            """
            module BinaryExecutor

            predicate high(_ value) means value > 40
            predicate low(_ value) means value <= 40
            
            function divide(_ dividend, _ divisor) means dividend / divisor
            
            record :super as {
                _ xValue: :number clamped between 1 and 10,
                yValue: :number,
                zValue: :number computed by xValue * yValue + 10%
            } 
            
            record :scan as {
              distance: :quantity(m),
              angle: :quantity(°),
              strength: :percentage
            }
            
            on Start(value) {
                let xx be 3.6
                let yy be :integer.ceil x
                let rec be :super(xx, yValue: yy)
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
            
            on Done(total) {
                // do nothing
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
        
        var script = script1;
        var compiled = GameEventScriptManager.Compile(script);
        var binary = compiled.ToGameEventScriptBinary();
        var published = new List<GameEventScriptMessage>();

        TestContext.WriteLine("-----");
        TestContext.WriteLine("BytecodeVM Dump:\n" + compiled.DumpBytecode());
        TestContext.WriteLine("-----");
        TestContext.WriteLine("Binary file:\n" + binary.Dump(includeInstructionAddresses: false, script));
        TestContext.WriteLine("-----");

        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRandom(GameEventScriptRandomGenerator.FromSeed(1))
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(published.Add))
            .Build()
            .Load(GameEventScriptManager.CompileModuleNewVm(script));
        var handled = host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(40))));

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

        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRandom(GameEventScriptRandomGenerator.FromSeed(1))
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CompileModuleNewVm(script));

        host.StartSession().Update(100);

        Assert.HasCount(1, emitted);
        Assert.AreEqual("Ready", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), emitted[0].Arguments["value"]);
    }

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
            .Load(GameEventScriptManager.CompileModuleNewVm(script));
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
            .Load(GameEventScriptManager.CompileModuleNewVm(script));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Done", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(expected), emitted[0].Arguments["value"]);
    }
    
}
