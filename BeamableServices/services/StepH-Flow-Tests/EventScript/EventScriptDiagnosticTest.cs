using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;
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

        on Start(values) {
          let total be values[:filter value where value is high][:select value -> value + 5%][:sum value -> value]
          let average be values[:filter value where value is high][:select value -> value + 5%][:average value -> value]
          let oddCount be values[:filter value where value mod 2 = 1][:count value where true]
          let firstBoosted be values[:filter value where value is high][:select value -> value + 5%][:first]
          let scaled as :meter be 100m + 5%
          let folded be (15% + 15%) * 2
          let directOddScaled be values[:filter value where value mod 2 = 1][:select value -> value * 2][:count value where value > 10]
          if scaled > 100m {
            let success be scaled + folded
            let myHandler be Success(message, value)
            let myMessage be myHandler(message: 'hello', value: success)
            let myMessageDirect be Success(message: 'world', value: scaled)
          }
          for item from 1 to 16 {
            let foldedBucket be values[:filter value where (value + item) mod 7 > 0][:select value -> (value + item) * 2][:sum value -> value]
          }
          let workTotal be values[:filter value where value >= 10][:select value -> value + 5%][:select value -> value * 2][:sum value -> value]
          publish Done(total: total, average: average, oddCount: oddCount, directOddScaled: directOddScaled, first: firstBoosted, scaled: scaled, folded: folded, workTotal: workTotal)
        }
        """;

    public TestContext TestContext { get; set; } = null!;
    
    [TestMethod]
    public void BuiltCollectionsDoNotTrackLaterSourceMutations()
    {
        var linkedModule = EventScriptManager.LinkScripts(script);
        var compiledModule = RegisterEventScriptCompiler.Compile(linkedModule, new RegisterEventScriptCompilationOptions { EnableDiagnostics = true });
        
        var input = Message("Start", ("values", EventScriptValueFactory.List(Enumerable.Range(1, 50).Select(value => EventScriptValueFactory.Integer(value)))));
        
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