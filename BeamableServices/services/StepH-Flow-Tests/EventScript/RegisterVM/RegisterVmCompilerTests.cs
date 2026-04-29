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
    public void RegisterVmFallbackModeThrowRunsMemberAndIndexedAccessFastPath()
    {
        const string script =
            """
            module Access

            on Start(player, key, items, index, units) {
              publish Done(
                hpByMember: player.hp,
                hpByTagKey: player[:hp],
                hpByVariableKey: player[key],
                itemByIndex: items[index],
                nestedName: units[2].name,
                missingMember: player.missing,
                missingIndex: items[99])
            }
            """;

        var unitOne = EventScriptValueFactory.Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["name"] = EventScriptValueFactory.Text("Scout")
        });
        var unitTwo = EventScriptValueFactory.Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["name"] = EventScriptValueFactory.Text("Knight")
        });

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { FallbackMode = RegisterVmFallbackMode.Throw }));

        host.Publish(Message(
            "Start",
            ("player", EventScriptValueFactory.Dictionary(new Dictionary<string, EventScriptValue>
            {
                ["hp"] = EventScriptValueFactory.Integer(12)
            })),
            ("key", EventScriptValueFactory.Text("hp")),
            ("items", EventScriptValueFactory.List(
            [
                EventScriptValueFactory.Integer(10),
                EventScriptValueFactory.Integer(20),
                EventScriptValueFactory.Integer(30)
            ])),
            ("index", EventScriptValueFactory.Integer(2)),
            ("units", EventScriptValueFactory.List([unitOne, unitTwo]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(EventScriptValueFactory.Integer(12), published[0].Arguments["hpByMember"]);
        Assert.AreEqual(EventScriptValueFactory.Integer(12), published[0].Arguments["hpByTagKey"]);
        Assert.AreEqual(EventScriptValueFactory.Integer(12), published[0].Arguments["hpByVariableKey"]);
        Assert.AreEqual(EventScriptValueFactory.Integer(20), published[0].Arguments["itemByIndex"]);
        Assert.AreEqual(EventScriptValueFactory.Text("Knight"), published[0].Arguments["nestedName"]);
        Assert.AreEqual(EventScriptValue.Nothing, published[0].Arguments["missingMember"]);
        Assert.AreEqual(EventScriptValue.Nothing, published[0].Arguments["missingIndex"]);
    }

    [TestMethod]
    public void RegisterVmFallbackModeThrowRunsCollectionLiteralsFastPath()
    {
        const string script =
            """
            module Literals

            on Start(seed) {
              let doubled be seed * 2
              let list be [seed, doubled, [label: 'nested']]
              let setValues be :set[seed, seed, 3]
              let dict be [hp: seed + 5, name: 'Scout', nested: [values: [1, 2]], tags: setValues]
              let emptyList be []
              let emptySet be :set[]
              let emptyDict be [:]

              publish Done(
                list: list,
                listFirst: list[1],
                listSecond: list[2],
                nestedLabel: list[3].label,
                setValues: setValues,
                dict: dict,
                hp: dict.hp,
                nestedSecond: dict.nested.values[2],
                tagValues: dict.tags,
                emptyList: emptyList,
                emptySet: emptySet,
                emptyDict: emptyDict)
            }
            """;

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { FallbackMode = RegisterVmFallbackMode.Throw }));

        host.Publish(Message("Start", ("seed", EventScriptValueFactory.Integer(7))));

        Assert.HasCount(1, published);
        Assert.AreEqual(EventScriptValueFactory.Integer(7), published[0].Arguments["listFirst"]);
        Assert.AreEqual(EventScriptValueFactory.Integer(14), published[0].Arguments["listSecond"]);
        Assert.AreEqual(EventScriptValueFactory.Text("nested"), published[0].Arguments["nestedLabel"]);
        Assert.AreEqual(EventScriptValueFactory.Integer(12), published[0].Arguments["hp"]);
        Assert.AreEqual(EventScriptValueFactory.Integer(2), published[0].Arguments["nestedSecond"]);

        var list = published[0].Arguments["list"].AsList();
        Assert.HasCount(3, list);

        var set = published[0].Arguments["setValues"].AsSet();
        Assert.HasCount(2, set);
        CollectionAssert.Contains(set.ToList(), EventScriptValueFactory.Integer(3));
        CollectionAssert.Contains(set.ToList(), EventScriptValueFactory.Integer(7));

        var dictionary = published[0].Arguments["dict"].AsDictionary();
        Assert.AreEqual(EventScriptValueFactory.Text("Scout"), dictionary["name"]);
        Assert.AreEqual(EventScriptValueFactory.Integer(12), dictionary["hp"]);

        var tagValues = published[0].Arguments["tagValues"].AsSet();
        Assert.HasCount(2, tagValues);
        Assert.AreEqual(EventScriptValueFactory.List([]), published[0].Arguments["emptyList"]);
        Assert.AreEqual(EventScriptValueFactory.Set([]), published[0].Arguments["emptySet"]);
        Assert.AreEqual(EventScriptValueFactory.Dictionary(new Dictionary<string, EventScriptValue>()), published[0].Arguments["emptyDict"]);
    }

    [TestMethod]
    public void RegisterVmFallbackModeThrowRunsTypeCheckFastPath()
    {
        const string script =
            """
            module TypeChecks

            on Start(custom, msg, handler) {
              let integerValue be 12
              let percentValue be 5%
              let degreeValue be 90°
              let meterValue be 100m
              let secondValue be 15s
              let listValue be [1]
              let dictValue be [name: 'Ada']
              let setValue be :set[1, 1, 2]

              publish Done(
                intIsInteger: integerValue is :integer,
                intIsDecimal: integerValue is :decimal,
                percentIsDecimal: percentValue is :decimal,
                degreeIsDecimal: degreeValue is :decimal,
                degreeIsDegree: degreeValue is :degree,
                meterIsMeter: meterValue is :meter,
                secondIsSecond: secondValue is :second,
                textIsText: 'x' is :text,
                tagIsTag: :ready is :tag,
                boolIsBoolean: true is :boolean,
                listIsList: listValue is :list,
                dictIsDictionary: dictValue is :dictionary,
                setIsSet: setValue is :set,
                customIsGauge: custom is :gauge,
                customIsDictionary: custom is :dictionary,
                msgIsMessage: msg is :message,
                msgIsDictionary: msg is :dictionary,
                handlerIsHandler: handler is :handler,
                missingIsNothing: missing is :nothing)
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
            ("custom", EventScriptValueFactory.CustomType("gauge", new Dictionary<string, EventScriptValue>
            {
                ["current"] = EventScriptValueFactory.Integer(5)
            })),
            ("msg", EventScriptValueFactory.Message(Message("Ping", ("value", EventScriptValueFactory.Integer(1))))),
            ("handler", EventScriptValueFactory.Handler(EventScriptMessageSignature.MessageSignature("Ping", ["value"])))));

        Assert.HasCount(1, published);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["intIsInteger"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["intIsDecimal"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["percentIsDecimal"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["degreeIsDecimal"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["degreeIsDegree"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["meterIsMeter"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["secondIsSecond"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["textIsText"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["tagIsTag"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["boolIsBoolean"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["listIsList"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["dictIsDictionary"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["setIsSet"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["customIsGauge"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["customIsDictionary"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["msgIsMessage"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(false), published[0].Arguments["msgIsDictionary"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["handlerIsHandler"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["missingIsNothing"]);
    }

    [TestMethod]
    public void RegisterVmFallbackModeThrowRunsDirectMessageLiteralExpressionFastPath()
    {
        const string script =
            """
            module MessageExpressions

            on Start(value) {
              let scaled be value * 2
              let myMessageDirect be Success(message: 'world', value: scaled)

              publish Done(
                isMessage: myMessageDirect is :message,
                name: myMessageDirect.name,
                signature: myMessageDirect.signatureid,
                text: myMessageDirect.arguments.message,
                value: myMessageDirect.arguments.value)
            }
            """;

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { FallbackMode = RegisterVmFallbackMode.Throw }));

        host.Publish(Message("Start", ("value", EventScriptValueFactory.Integer(21))));

        Assert.HasCount(1, published);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["isMessage"]);
        Assert.AreEqual(EventScriptValueFactory.Text("Success"), published[0].Arguments["name"]);
        Assert.AreEqual(EventScriptValueFactory.Text("Success(message,value)"), published[0].Arguments["signature"]);
        Assert.AreEqual(EventScriptValueFactory.Text("world"), published[0].Arguments["text"]);
        Assert.AreEqual(EventScriptValueFactory.Decimal(42m), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void RegisterVmFallbackModeThrowRunsHandlerLiteralAndBindFastPath()
    {
        const string script =
            """
            module HandlerExpressions

            on Start(success) {
              let myHandler be Success(message, value)
              let myMessage be myHandler(message: 'hello', value: success)
              let invalidMessage be myHandler(message: 'hello', other: success)

              publish Done(
                handlerIsHandler: myHandler is :handler,
                handlerName: myHandler.name,
                handlerSignature: myHandler.signatureid,
                secondParameter: myHandler.parameters[2],
                messageIsMessage: myMessage is :message,
                messageName: myMessage.name,
                messageSignature: myMessage.signatureid,
                text: myMessage.arguments.message,
                value: myMessage.arguments.value,
                invalidIsNothing: invalidMessage is :nothing)
            }
            """;

        var published = new List<EventScriptMessage>();
        var host = EventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(EventScriptManager.CompileRegisterVM(
                script,
                new RegisterEventScriptCompilationOptions { FallbackMode = RegisterVmFallbackMode.Throw }));

        host.Publish(Message("Start", ("success", EventScriptValueFactory.Boolean(true))));

        Assert.HasCount(1, published);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["handlerIsHandler"]);
        Assert.AreEqual(EventScriptValueFactory.Text("Success"), published[0].Arguments["handlerName"]);
        Assert.AreEqual(EventScriptValueFactory.Text("Success(message,value)"), published[0].Arguments["handlerSignature"]);
        Assert.AreEqual(EventScriptValueFactory.Text("value"), published[0].Arguments["secondParameter"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["messageIsMessage"]);
        Assert.AreEqual(EventScriptValueFactory.Text("Success"), published[0].Arguments["messageName"]);
        Assert.AreEqual(EventScriptValueFactory.Text("Success(message,value)"), published[0].Arguments["messageSignature"]);
        Assert.AreEqual(EventScriptValueFactory.Text("hello"), published[0].Arguments["text"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["value"]);
        Assert.AreEqual(EventScriptValueFactory.Boolean(true), published[0].Arguments["invalidIsNothing"]);
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
