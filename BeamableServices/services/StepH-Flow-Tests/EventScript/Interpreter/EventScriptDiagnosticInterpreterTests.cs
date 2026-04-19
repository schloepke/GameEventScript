using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Interpreter;

[TestClass]
public class EventScriptDiagnosticInterpreterScenarios
{
    [TestMethod]
    public void DiagnosticInterpreterInvokesOnlyTheInitialMessage()
    {
        const string script =
            """
            on Start {
                publish Next(3)
            }

            on Next(value) {
                publish Done(value)
            }
            """;

        var interpreter = EventScriptDiagnosticInterpreter.Compile(script);

        var result = interpreter.Invoke("Start");

        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Next", result.EmittedEvents[0].Message);
        Assert.AreEqual(EventScriptDiagnosticStepKind.InvocationStarted, result.Steps[0].Kind);
        Assert.AreEqual(EventScriptDiagnosticStepKind.HandlerMatched, result.Steps[1].Kind);
        Assert.IsTrue(result.Steps.Any(step => step.Kind == EventScriptDiagnosticStepKind.StatementExecuting));
        Assert.IsTrue(result.Steps.Any(step => step.Kind == EventScriptDiagnosticStepKind.ExpressionEvaluated));
        Assert.IsTrue(result.Steps.Any(step => step.Kind == EventScriptDiagnosticStepKind.EventPublished));
        Assert.AreEqual(EventScriptDiagnosticStepKind.InvocationCompleted, result.Steps[^1].Kind);
    }

    [TestMethod]
    public void DiagnosticInterpreterCapturesInitialVariables()
    {
        const string script =
            """
            on Start(value) {
                let score be value + 2
                publish Done(score)
            }
            """;

        var interpreter = EventScriptDiagnosticInterpreter.Compile(script);

        var result = interpreter.Invoke("Start", EventScriptValue.Decimal(5m));

        Assert.AreEqual(7m, result.Variables["score"].AsNumber());
        Assert.AreEqual(7m, result.EmittedEvents[0].Arguments[0].AsNumber());
    }

    [TestMethod]
    public void DiagnosticInterpreterRecordsAllMatchedHandlersInDeclarationOrder()
    {
        const string script =
            """
            on Start(value) {
                publish First(value)
            }

            on Start(value) {
                publish Second(value)
            }
            """;

        var interpreter = EventScriptDiagnosticInterpreter.Compile(script);

        var result = interpreter.Invoke("Start", EventScriptValue.Decimal(2m));

        var matchedSteps = result.Steps.Where(step => step.Kind == EventScriptDiagnosticStepKind.HandlerMatched).ToArray();

        Assert.HasCount(2, matchedSteps);
        Assert.AreEqual("Handler 'Start' with parameters (value)", matchedSteps[0].Detail);
        Assert.AreEqual("Handler 'Start' with parameters (value)", matchedSteps[1].Detail);
        Assert.AreEqual("First", result.EmittedEvents[0].Message);
        Assert.AreEqual("Second", result.EmittedEvents[1].Message);
    }

    [TestMethod]
    public void DiagnosticInterpreterRecordsWhenExpressionsEvaluateToNothing()
    {
        const string script =
            """
            on Start {
                let missing be unknownValue
                publish Done(missing)
            }
            """;

        var interpreter = EventScriptDiagnosticInterpreter.Compile(script);

        var result = interpreter.Invoke("Start");

        Assert.AreEqual(EventScriptValueType.Nothing, result.Variables["missing"].Type);
        Assert.IsTrue(result.Steps.Any(step =>
            step.Kind == EventScriptDiagnosticStepKind.ExpressionEvaluated &&
            step.Detail is not null &&
            step.Detail.Contains("nothing", StringComparison.Ordinal)));
    }
}
