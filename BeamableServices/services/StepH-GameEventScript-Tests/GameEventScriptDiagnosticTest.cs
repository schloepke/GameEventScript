using StepH.GameEventScript;
using StepH.GameEventScript.RegisterVM;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.GseMessage;

namespace StepH_GameEventScript_Tests;

[TestClass]
public class GameEventScriptDiagnosticTest
{
    private const string script =
        """
        module EnginePerformance

        rule high(value) means value >= 10

        on Start(startPosition) {
          let x be startPosition[:x]
          let y be startPosition[:y]
          let bearing be 120°
          publish ScanArea(bearing: bearing, range: 100m)
        }
        
        on ScanArea(bearing, range) {
          let x be :decimal(range) * bearing
          let y be (range as :decimal) * bearing 
          publish AreaScanned(x, y)
        }
        
        on AreaScanned(_ x, _ y) {
            let move2d be :vector2(x: x, y: y)
            let move3d be :meter(:vector3(:decimal(move2d))) + :vector3(z: 10m)
            publish Done(move3d, x: x, y: y)
        }
        
        on Done(_ move3d, x, y) {
            publish Finished
        }
        """;

    public TestContext TestContext { get; set; } = null!;
    
    [TestMethod]
    public void DisplayingDiagnosticsTest()
    {
        var linkedModule = GameEventScriptManager.LinkScripts(script);
        var compiledModule = RegisterGseCompiler.Compile(linkedModule, new RegisterGseCompilationOptions { EnableDiagnostics = true });
        
        var input = Message("Start", ("startPosition", GseValueFactory.Vector2(20, 15)));
        
        var collector = new GseDiagnosticTraceCollector();
        var host = GseHost.CreateBuilder()
            .WithMaxProcessedEventsPerRun(128)
            .WithDiagnosticCollector(collector)
            .Build()
            .Load(compiledModule);
        
        host.Publish(input);
        
        TestContext.WriteLine(collector.ToString());
        
        
    }
}
