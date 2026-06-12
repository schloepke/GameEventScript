using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests;

[TestClass]
public class GameEventScriptDiagnosticTest
{
    private const string script =
        """
        module EnginePerformance

        predicate high(value) means value >= 10

        on Start(startPosition) {
          let x be startPosition[:x]
          let y be startPosition[:y]
          let bearing be 120°
          emit ScanArea(bearing: bearing, range: 100m)
        }
        
        on ScanArea(bearing, range) {
          let x be :number(range) * bearing
          let y be (range as :number) * bearing 
          emit AreaScanned(x, y)
        }
        
        on AreaScanned(_ x, _ y) {
            let move be :vector(x: x, y: y)
            let moved be :quantity(m)(:vector(:number(move))) + :vector(z: 10m)
            emit Done(moved, x: x, y: y)
        }
        
        on Done(_ moved, x, y) {
            emit Finished
        }
        """;

    public TestContext TestContext { get; set; } = null!;
    
    [TestMethod]
    public void DisplayingDiagnosticsTest()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(script)
            .Compile(new GameEventScriptCompileOptions { EnableDiagnostics = true });
        
        var input = Create("Start", ("startPosition", GameEventScriptValueFactory.GesVector(20, 15)));
        
        var collector = new GameEventScriptDiagnosticTraceCollector();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits
            {
                MaxProcessedEventsPerRun = 128
            })
            .WithDiagnosticCollector(collector)
            .Build()
            .Load(GameEventScriptManager.CreateModule(bytecode));
        
        host.PublishToCompletion(input);
    }
}
