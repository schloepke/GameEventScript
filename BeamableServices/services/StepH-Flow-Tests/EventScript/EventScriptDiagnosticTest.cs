using StepH.Flow.EventScript;
using StepH.Flow.EventScript.RegisterVM;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;
using static StepH.Flow.EventScript.EventScriptMessage;

namespace StepH_Flow_Tests.EventScript;

[TestClass]
public class EventScriptDiagnosticTest
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
        var linkedModule = EventScriptManager.LinkScripts(script);
        var compiledModule = RegisterEventScriptCompiler.Compile(linkedModule, new RegisterEventScriptCompilationOptions { EnableDiagnostics = true });
        
        var input = Message("Start", ("startPosition", EventScriptValueFactory.Vector2(20, 15)));
        
        var collector = new EventScriptDiagnosticTraceCollector();
        var host = EventScriptHost.CreateBuilder()
            .WithMaxProcessedEventsPerRun(128)
            .WithDiagnosticCollector(collector)
            .Build()
            .Load(compiledModule);
        
        host.Publish(input);
        
        TestContext.WriteLine(collector.ToString());
        
        
    }
}
