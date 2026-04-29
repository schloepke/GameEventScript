using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.RegisterVM;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;
using static StepH.Flow.EventScript.EventScriptMessage;

namespace StepH_Flow_Tests.EventScript.RegisterVM;

[TestClass]
public sealed class RegisterVmCompilerTests
{
    [TestMethod]
    public void DebugDumpIsDeterministic()
    {
        const string script =
            """
            module Dump

            on Start(value) {
              let total be value + 1
              if total > 1 {
                publish Done(total: total)
              }
            }
            """;

        var first = RegisterBytecodeDumper.ToDebugText(EventScriptManager.CompileRegisterVM(script));
        var second = RegisterBytecodeDumper.ToDebugText(EventScriptManager.CompileRegisterVM(script));

        Assert.AreEqual(first, second);
        StringAssert.Contains(first, "registervm bytecode v1");
        StringAssert.Contains(first, "program");
        StringAssert.Contains(first, "EvaluateExpression");
        StringAssert.Contains(first, "Publish");
    }

    [TestMethod]
    public void RegisterVmCanRunThroughHostContract()
    {
        const string script =
            """
            module Runtime

            on Start {
              publish Done(value: 3)
            }
            """;

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(script));

        host.Publish(Message("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
        Assert.AreEqual(EventScriptValueFactory.Integer(3), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void RegisterVmAllowsCompatibilityFallbackByDefault()
    {
        const string script =
            """
            module Fallback

            on Start {
              :random with 'seed' {
                publish Done
              }
            }
            """;

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(script));

        host.Publish(Message("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
    }

    [TestMethod]
    public void RegisterVmFallbackModeThrowSurfacesUnsupportedFastPath()
    {
        const string script =
            """
            module Fallback

            on Start {
              :random with 'seed' {
                publish Done
              }
            }
            """;

        var host = EventScriptHost.CreateBuilder()
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { FallbackMode = RegisterVmFallbackMode.Throw }));

        RegisterVmFallbackException? exception = null;
        try
        {
            host.Publish(Message("Start"));
        }
        catch (RegisterVmFallbackException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual("Start", exception.MessageName);
        StringAssert.Contains(exception.HandlerSignatureId, "Start");
        StringAssert.Contains(exception.Reason, nameof(SeededRandomStatementNode));
    }

    [TestMethod]
    public void RegisterVmFallbackModeThrowRunsIfElseFastPath()
    {
        const string script =
            """
            module Branches

            on Start(first, second) {
              if first {
                let branch be 1
                publish Branch(value: branch)
              } else if second {
                let branch be 2
                publish Branch(value: branch)
              } else {
                let branch be 3
                publish Branch(value: branch)
              }
            }
            """;

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { FallbackMode = RegisterVmFallbackMode.Throw }));

        host.Publish(Message("Start", ("first", EventScriptValueFactory.Boolean(true)), ("second", EventScriptValueFactory.Boolean(false))));
        host.Publish(Message("Start", ("first", EventScriptValueFactory.Boolean(false)), ("second", EventScriptValueFactory.Boolean(true))));
        host.Publish(Message("Start", ("first", EventScriptValueFactory.Boolean(false)), ("second", EventScriptValueFactory.Boolean(false))));

        Assert.HasCount(3, published);
        Assert.AreEqual(EventScriptValueFactory.Integer(1), published[0].Arguments["value"]);
        Assert.AreEqual(EventScriptValueFactory.Integer(2), published[1].Arguments["value"]);
        Assert.AreEqual(EventScriptValueFactory.Integer(3), published[2].Arguments["value"]);
    }

    [TestMethod]
    public void RegisterVmIfBlockDoesNotLeakLocals()
    {
        const string script =
            """
            module Branches

            on Start(flag) {
              if flag {
                let inner be 7
              }

              publish Done(inner: inner)
            }
            """;

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { FallbackMode = RegisterVmFallbackMode.Throw }));

        host.Publish(Message("Start", ("flag", EventScriptValueFactory.Boolean(true))));

        Assert.HasCount(1, published);
        Assert.AreEqual(EventScriptValue.Nothing, published[0].Arguments["inner"]);
    }

    [TestMethod]
    public void RegisterVmFallbackModeThrowRunsCollectionForFastPath()
    {
        const string script =
            """
            module Loops

            on Start(items) {
              for item in items {
                if item > 1 {
                  publish Item(value: item * 2)
                }
              }
            }
            """;

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { FallbackMode = RegisterVmFallbackMode.Throw }));

        host.Publish(Message(
            "Start",
            ("items", EventScriptValueFactory.List(
            [
                EventScriptValueFactory.Integer(1),
                EventScriptValueFactory.Integer(2),
                EventScriptValueFactory.Integer(3)
            ]))));

        Assert.HasCount(2, published);
        Assert.AreEqual(EventScriptValueFactory.Integer(4), published[0].Arguments["value"]);
        Assert.AreEqual(EventScriptValueFactory.Integer(6), published[1].Arguments["value"]);
    }

    [TestMethod]
    public void RegisterVmCollectionForBlockDoesNotLeakLocals()
    {
        const string script =
            """
            module Loops

            on Start(items) {
              for item in items {
                let inner be item
              }

              publish Done(item: item, inner: inner)
            }
            """;

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { FallbackMode = RegisterVmFallbackMode.Throw }));

        host.Publish(Message(
            "Start",
            ("items", EventScriptValueFactory.List(
            [
                EventScriptValueFactory.Integer(1)
            ]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(EventScriptValue.Nothing, published[0].Arguments["item"]);
        Assert.AreEqual(EventScriptValue.Nothing, published[0].Arguments["inner"]);
    }

    [TestMethod]
    public void RegisterVmFallbackModeThrowRunsTypedLetFastPath()
    {
        const string script =
            """
            module TypedLets

            on Start {
              let numberOk as :decimal be '12.2'
              let numberFail as :decimal be 'abc'
              let integerOk as :integer be '12.7'
              let percentageOk as :percentage be 5
              let degreeOk as :degree be 450
              let textOk as :text be 43.9°
              let unitErased as :decimal be 43.9°
              let listOk as :list be 'ab'
              let optionalNone as :optional be missing
              publish Done(numberOk: numberOk, numberFail: numberFail, integerOk: integerOk, percentageOk: percentageOk, degreeOk: degreeOk, textOk: textOk, unitErased: unitErased, listOk: listOk, optionalNone: optionalNone)
            }
            """;

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { FallbackMode = RegisterVmFallbackMode.Throw }));

        host.Publish(Message("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(EventScriptValueFactory.Decimal(12.2m), published[0].Arguments["numberOk"]);
        Assert.IsTrue(published[0].Arguments["numberFail"].IsNaN());
        Assert.AreEqual(EventScriptValueFactory.Integer(12), published[0].Arguments["integerOk"]);
        Assert.AreEqual(EventScriptValueFactory.Percentage(0.05m), published[0].Arguments["percentageOk"]);
        Assert.AreEqual(EventScriptValueFactory.Decimal(450m, EventScriptDecimalUnit.Degree), published[0].Arguments["degreeOk"]);
        Assert.AreEqual(EventScriptValueFactory.Text("43.9°"), published[0].Arguments["textOk"]);
        Assert.AreEqual(EventScriptValueFactory.Decimal(43.9m), published[0].Arguments["unitErased"]);
        Assert.HasCount(2, published[0].Arguments["listOk"].AsList());
        Assert.IsFalse(published[0].Arguments["optionalNone"].AsOptional().HasValue);
    }

    [TestMethod]
    public void RegisterVmFastPathEmitsDiagnosticsWhenEnabled()
    {
        const string script =
            """
            module Diagnostics

            rule high(value) means value > 3

            on Start(value) {
              let score be value + 2
              let missingValue be missing
              let isHigh be score is high
              publish Done(score: score, missingValue: missingValue, isHigh: isHigh)
            }
            """;

        var collector = new EventScriptDiagnosticTraceCollector();
        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collector)
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { EnableDiagnostics = true }));

        host.Publish(Message("Start", ("value", EventScriptValueFactory.Integer(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(EventScriptValueFactory.Integer(7), published[0].Arguments["score"]);
        Assert.AreEqual(EventScriptValue.Nothing, published[0].Arguments["missingValue"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["isHigh"]);
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == EventScriptDiagnosticEventKind.ParameterBound && diagnostic.Name == "value"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == EventScriptDiagnosticEventKind.HandlerInvoked && diagnostic.Name == "Start"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == EventScriptDiagnosticEventKind.LetEvaluated && diagnostic.Name == "score"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == EventScriptDiagnosticEventKind.LetEvaluated && diagnostic.Name == "missingValue"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == EventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing && diagnostic.Name == "missingValue"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == EventScriptDiagnosticEventKind.RuleCalled && diagnostic.Name == "high"));
    }
}
