using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Interpreter;

[TestClass]
public class EventScriptDiagnosticCollectorScenarios
{
    [TestMethod]
    public void CollectorRecordsHandlerInvocationBindingsAndPublishedEvents()
    {
        const string script =
            """
            on Start(value) {
                let score be value + 2
                publish Done(arg1: score)
            }
            """;

        var compiled = EventScriptManager.Compile(script, new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true });
        var collector = new EventScriptDiagnosticTraceCollector();

        var result = compiled.Invoke(
            "Start",
            new Dictionary<string, EventScriptValue> { ["value"] = EventScriptValue.Decimal(5m) },
            invocationContext: null,
            diagnosticCollector: collector);

        Assert.AreEqual(7m, result.Variables["score"].AsNumber());
        Assert.AreEqual(7m, result.EmittedEvents[0].Arguments[0].AsNumber());
        Assert.IsTrue(collector.Events.Any(evt => evt.Kind == EventScriptDiagnosticEventKind.HandlerInvoked && evt.Name == "Start"));
        Assert.IsTrue(collector.Events.Any(evt => evt.Kind == EventScriptDiagnosticEventKind.ParameterBound && evt.Name == "value"));
        Assert.IsTrue(collector.Events.Any(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished && evt.Name == "Done"));
    }

    [TestMethod]
    public void CollectorRecordsRuleAndSelectCalls()
    {
        const string script =
            """
            rule wounded(unit) means unit.hp < unit.maxHp
            select woundedUnits(units) means units[:filter unit where unit is wounded]

            on Start(unit, units) {
                let byRule be wounded(unit)
                let bySelect be woundedUnits(units)
                publish Done(arg1: byRule, arg2: :len bySelect)
            }
            """;

        var unit = EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["hp"] = 2m,
            ["maxHp"] = 5m
        });
        var units = EventScriptValue.List([unit]);
        var compiled = EventScriptManager.Compile(script, new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true });
        var collector = new EventScriptDiagnosticTraceCollector();

        compiled.Invoke(
            "Start",
            new Dictionary<string, EventScriptValue> { ["unit"] = unit, ["units"] = units },
            invocationContext: null,
            diagnosticCollector: collector);

        Assert.IsTrue(collector.Events.Any(evt => evt.Kind == EventScriptDiagnosticEventKind.RuleCalled && evt.Name == "wounded"));
        Assert.IsTrue(collector.Events.Any(evt => evt.Kind == EventScriptDiagnosticEventKind.SelectCalled && evt.Name == "woundedUnits"));
    }

    [TestMethod]
    public void CollectorRemainsEmptyWhenDiagnosticsWereDisabledAtCompileTime()
    {
        const string script =
            """
            on Start(value) {
                publish Done(arg1: value)
            }
            """;

        var compiled = EventScriptManager.Compile(script, new EventScriptInterpreterCompilationOptions { EnableDiagnostics = false });
        var collector = new EventScriptDiagnosticTraceCollector();

        compiled.Invoke(
            "Start",
            new Dictionary<string, EventScriptValue> { ["value"] = 3m },
            invocationContext: null,
            diagnosticCollector: collector);

        Assert.IsEmpty(collector.Events);
    }
}
