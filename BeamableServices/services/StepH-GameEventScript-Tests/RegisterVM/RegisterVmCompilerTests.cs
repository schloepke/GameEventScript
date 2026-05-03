using StepH.GameEventScript;
using StepH.GameEventScript.Linker;
using StepH.GameEventScript.Parser;
using StepH.GameEventScript.RegisterVM;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.GseMessage;

namespace StepH_GameEventScript_Tests.RegisterVM;

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

        var first = RegisterBytecodeDumper.ToDebugText(GameEventScriptManager.Compile(script));
        var second = RegisterBytecodeDumper.ToDebugText(GameEventScriptManager.Compile(script));

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

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
        Assert.AreEqual(GseValueFactory.Integer(3), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void RegisterVmRejectsUnknownStatementNodesAtCompileTime()
    {
        var module = new LinkedGseModule(
            new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal),
            new Dictionary<string, LinkedCallableDefinition>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<EventHandlerNode>>(StringComparer.Ordinal)
            {
                ["Start"] =
                [
                    new EventHandlerNode(
                        "Start",
                        Array.Empty<ParameterNode>(),
                        [new UnknownStatementNode()])
                ]
            },
            sourceCount: 1);

        var exception = Assert.ThrowsExactly<GseCompilationException>(() => RegisterGseCompiler.Compile(module));
        StringAssert.Contains(exception.Message, "RegisterVM compiler does not support handler 'Start' #0");
        StringAssert.Contains(exception.Message, nameof(UnknownStatementNode));
    }

    [TestMethod]
    public void RegisterVmRunsGeneratedCollectionsByDefault()
    {
        const string script =
            """
            module Collections

            on Start {
              let values be :list[:select item from 1 to 3 -> item]
              publish Done(count: :len values)
            }
            """;

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
        Assert.AreEqual(GseValueFactory.Integer(3), published[0].Arguments["count"]);
    }

    [TestMethod]
    public void RegisterVmRunsIfElse()
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

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message("Start", ("first", GseValueFactory.Boolean(true)), ("second", GseValueFactory.Boolean(false))));
        host.Publish(Message("Start", ("first", GseValueFactory.Boolean(false)), ("second", GseValueFactory.Boolean(true))));
        host.Publish(Message("Start", ("first", GseValueFactory.Boolean(false)), ("second", GseValueFactory.Boolean(false))));

        Assert.HasCount(3, published);
        Assert.AreEqual(GseValueFactory.Integer(1), published[0].Arguments["value"]);
        Assert.AreEqual(GseValueFactory.Integer(2), published[1].Arguments["value"]);
        Assert.AreEqual(GseValueFactory.Integer(3), published[2].Arguments["value"]);
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

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message("Start", ("flag", GseValueFactory.Boolean(true))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValue.Nothing, published[0].Arguments["inner"]);
    }

    [TestMethod]
    public void RegisterVmRunsCollectionFor()
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

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message(
            "Start",
            ("items", GseValueFactory.List(
            [
                GseValueFactory.Integer(1),
                GseValueFactory.Integer(2),
                GseValueFactory.Integer(3)
            ]))));

        Assert.HasCount(2, published);
        Assert.AreEqual(GseValueFactory.Integer(4), published[0].Arguments["value"]);
        Assert.AreEqual(GseValueFactory.Integer(6), published[1].Arguments["value"]);
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

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message(
            "Start",
            ("items", GseValueFactory.List(
            [
                GseValueFactory.Integer(1)
            ]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValue.Nothing, published[0].Arguments["item"]);
        Assert.AreEqual(GseValue.Nothing, published[0].Arguments["inner"]);
    }

    [TestMethod]
    public void RegisterVmRunsTypedLet()
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

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValueFactory.Decimal(12.2m), published[0].Arguments["numberOk"]);
        Assert.IsTrue(published[0].Arguments["numberFail"].IsNaN());
        Assert.AreEqual(GseValueFactory.Integer(12), published[0].Arguments["integerOk"]);
        Assert.AreEqual(GseValueFactory.Percentage(0.05m), published[0].Arguments["percentageOk"]);
        Assert.AreEqual(GseValueFactory.Decimal(450m, GseDecimalUnit.Degree), published[0].Arguments["degreeOk"]);
        Assert.AreEqual(GseValueFactory.Text("43.9°"), published[0].Arguments["textOk"]);
        Assert.AreEqual(GseValueFactory.Decimal(43.9m), published[0].Arguments["unitErased"]);
        Assert.HasCount(2, published[0].Arguments["listOk"].AsList());
        Assert.IsFalse(published[0].Arguments["optionalNone"].AsOptional().HasValue);
    }

    [TestMethod]
    public void RegisterVmRunsMemberAndIndexedAccess()
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

        var unitOne = GseValueFactory.Dictionary(new Dictionary<string, GseValue>
        {
            ["name"] = GseValueFactory.Text("Scout")
        });
        var unitTwo = GseValueFactory.Dictionary(new Dictionary<string, GseValue>
        {
            ["name"] = GseValueFactory.Text("Knight")
        });

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message(
            "Start",
            ("player", GseValueFactory.Dictionary(new Dictionary<string, GseValue>
            {
                ["hp"] = GseValueFactory.Integer(12)
            })),
            ("key", GseValueFactory.Text("hp")),
            ("items", GseValueFactory.List(
            [
                GseValueFactory.Integer(10),
                GseValueFactory.Integer(20),
                GseValueFactory.Integer(30)
            ])),
            ("index", GseValueFactory.Integer(2)),
            ("units", GseValueFactory.List([unitOne, unitTwo]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValueFactory.Integer(12), published[0].Arguments["hpByMember"]);
        Assert.AreEqual(GseValueFactory.Integer(12), published[0].Arguments["hpByTagKey"]);
        Assert.AreEqual(GseValueFactory.Integer(12), published[0].Arguments["hpByVariableKey"]);
        Assert.AreEqual(GseValueFactory.Integer(20), published[0].Arguments["itemByIndex"]);
        Assert.AreEqual(GseValueFactory.Text("Knight"), published[0].Arguments["nestedName"]);
        Assert.AreEqual(GseValue.Nothing, published[0].Arguments["missingMember"]);
        Assert.AreEqual(GseValue.Nothing, published[0].Arguments["missingIndex"]);
    }

    [TestMethod]
    public void RegisterVmRunsCollectionLiterals()
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

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message("Start", ("seed", GseValueFactory.Integer(7))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValueFactory.Integer(7), published[0].Arguments["listFirst"]);
        Assert.AreEqual(GseValueFactory.Integer(14), published[0].Arguments["listSecond"]);
        Assert.AreEqual(GseValueFactory.Text("nested"), published[0].Arguments["nestedLabel"]);
        Assert.AreEqual(GseValueFactory.Integer(12), published[0].Arguments["hp"]);
        Assert.AreEqual(GseValueFactory.Integer(2), published[0].Arguments["nestedSecond"]);

        var list = published[0].Arguments["list"].AsList();
        Assert.HasCount(3, list);

        var set = published[0].Arguments["setValues"].AsSet();
        Assert.HasCount(2, set);
        CollectionAssert.Contains(set.ToList(), GseValueFactory.Integer(3));
        CollectionAssert.Contains(set.ToList(), GseValueFactory.Integer(7));

        var dictionary = published[0].Arguments["dict"].AsDictionary();
        Assert.AreEqual(GseValueFactory.Text("Scout"), dictionary["name"]);
        Assert.AreEqual(GseValueFactory.Integer(12), dictionary["hp"]);

        var tagValues = published[0].Arguments["tagValues"].AsSet();
        Assert.HasCount(2, tagValues);
        Assert.AreEqual(GseValueFactory.List([]), published[0].Arguments["emptyList"]);
        Assert.AreEqual(GseValueFactory.Set([]), published[0].Arguments["emptySet"]);
        Assert.AreEqual(GseValueFactory.Dictionary(new Dictionary<string, GseValue>()), published[0].Arguments["emptyDict"]);
    }

    [TestMethod]
    public void RegisterVmRunsTypeCheck()
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

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message(
            "Start",
            ("custom", GseValueFactory.CustomType("gauge", new Dictionary<string, GseValue>
            {
                ["current"] = GseValueFactory.Integer(5)
            })),
            ("msg", GseValueFactory.Message(Message("Ping", ("value", GseValueFactory.Integer(1))))),
            ("handler", GseValueFactory.Handler(GseMessageSignature.MessageSignature("Ping", ["value"])))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["intIsInteger"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["intIsDecimal"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["percentIsDecimal"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["degreeIsDecimal"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["degreeIsDegree"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["meterIsMeter"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["secondIsSecond"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["textIsText"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["tagIsTag"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["boolIsBoolean"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["listIsList"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["dictIsDictionary"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["setIsSet"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["customIsGauge"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["customIsDictionary"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["msgIsMessage"]);
        Assert.AreEqual(GseValueFactory.Boolean(false), published[0].Arguments["msgIsDictionary"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["handlerIsHandler"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["missingIsNothing"]);
    }

    [TestMethod]
    public void RegisterVmRunsDirectMessageLiteralExpression()
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

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message("Start", ("value", GseValueFactory.Integer(21))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["isMessage"]);
        Assert.AreEqual(GseValueFactory.Text("Success"), published[0].Arguments["name"]);
        Assert.AreEqual(GseValueFactory.Text("Success(message,value)"), published[0].Arguments["signature"]);
        Assert.AreEqual(GseValueFactory.Text("world"), published[0].Arguments["text"]);
        Assert.AreEqual(GseValueFactory.Decimal(42m), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void RegisterVmRunsHandlerLiteralAndBind()
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

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message("Start", ("success", GseValueFactory.Boolean(true))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["handlerIsHandler"]);
        Assert.AreEqual(GseValueFactory.Text("Success"), published[0].Arguments["handlerName"]);
        Assert.AreEqual(GseValueFactory.Text("Success(message,value)"), published[0].Arguments["handlerSignature"]);
        Assert.AreEqual(GseValueFactory.Text("value"), published[0].Arguments["secondParameter"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["messageIsMessage"]);
        Assert.AreEqual(GseValueFactory.Text("Success"), published[0].Arguments["messageName"]);
        Assert.AreEqual(GseValueFactory.Text("Success(message,value)"), published[0].Arguments["messageSignature"]);
        Assert.AreEqual(GseValueFactory.Text("hello"), published[0].Arguments["text"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["value"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["invalidIsNothing"]);
    }

    [TestMethod]
    public void RegisterVmEmitsDiagnosticsWhenEnabled()
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

        var collector = new GseDiagnosticTraceCollector();
        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithDiagnosticCollector(collector)
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(
                script,
                new RegisterGseCompilationOptions { EnableDiagnostics = true }));

        host.Publish(Message("Start", ("value", GseValueFactory.Integer(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValueFactory.Integer(7), published[0].Arguments["score"]);
        Assert.AreEqual(GseValue.Nothing, published[0].Arguments["missingValue"]);
        Assert.AreEqual(GseValueFactory.Boolean(true), published[0].Arguments["isHigh"]);
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GseDiagnosticEventKind.ParameterBound && diagnostic.Name == "value"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GseDiagnosticEventKind.HandlerInvoked && diagnostic.Name == "Start"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GseDiagnosticEventKind.LetEvaluated && diagnostic.Name == "score"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GseDiagnosticEventKind.LetEvaluated && diagnostic.Name == "missingValue"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GseDiagnosticEventKind.ExpressionEvaluatedToNothing && diagnostic.Name == "missingValue"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GseDiagnosticEventKind.RuleCalled && diagnostic.Name == "high"));
    }

    [TestMethod]
    public void RegisterVmStandardExtensionsAreIntrinsicAndDoNotRequireDynamicLinking()
    {
        const string script =
            """
            module StandardExtensions

            on Start(value, heading) {
              publish Done(floor: :integer.floor value, wrapped: :degree.wrap heading)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var dump = RegisterBytecodeDumper.ToDebugText(compiled);

        StringAssert.Contains(dump, "externalReferences[0]");
        Assert.IsFalse(dump.Contains("integer.floor(_)", StringComparison.Ordinal));
        Assert.IsFalse(dump.Contains("degree.wrap(_)", StringComparison.Ordinal));

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(compiled);

        host.Publish(Message(
            "Start",
            ("value", GseValueFactory.Decimal(10.4m)),
            ("heading", GseValueFactory.Degree(-10))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValueFactory.Integer(10), published[0].Arguments["floor"]);
        Assert.AreEqual(GseValueFactory.Degree(350), published[0].Arguments["wrapped"]);
    }

    [TestMethod]
    public void RegisterVmDynamicLinkBindsExtensionReferencesOnHostLoad()
    {
        const string script =
            """
            module Extensions

            on Start(values) {
              let floored be values[:select item -> :math.floor item]
              publish Done(first: floored[1])
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var dump = RegisterBytecodeDumper.ToDebugText(compiled);

        StringAssert.Contains(dump, "externalReferences[1]");
        StringAssert.Contains(dump, "math.floor(_)");
        var exception = Assert.ThrowsExactly<GseDynamicLinkException>(() =>
            GseHost.CreateBuilder().Build().Load(compiled));
        StringAssert.Contains(exception.Message, "math.floor(_)");
        StringAssert.Contains(exception.Message, "registry is required");

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithRegistry(TestExtensionRegistry.Instance)
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(compiled);

        host.Publish(Message(
            "Start",
            ("values", GseValueFactory.List(
            [
                GseValueFactory.Decimal(2.9m),
                GseValueFactory.Decimal(5.1m)
            ]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValueFactory.Decimal(2m), published[0].Arguments["first"]);
    }

    [TestMethod]
    public void RegisterVmDynamicLinkReportsMissingExtensionFunctionWithSignature()
    {
        const string script =
            """
            module MissingExtensions

            on Start {
              let value be :missing.floor 10.4
              publish Done(value: value)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var exception = Assert.ThrowsExactly<GseDynamicLinkException>(() =>
            GseHost.CreateBuilder()
                .WithRegistry(TestExtensionRegistry.Instance)
                .Build()
                .Load(compiled));

        StringAssert.Contains(exception.Message, "missing.floor(_)");
        StringAssert.Contains(exception.Message, "not registered in the configured registry");
    }

    [TestMethod]
    public void RegisterVmDynamicLinkKeepsLabeledArgumentsPositional()
    {
        const string script =
            """
            module OrderedLabels

            on Start(heading, target) {
              let turn be :nav.shortestTurn to: target from: heading
              publish Done(turn: turn)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var dump = RegisterBytecodeDumper.ToDebugText(compiled);

        StringAssert.Contains(dump, "nav.shortestTurn(to,from)");
        var exception = Assert.ThrowsExactly<GseDynamicLinkException>(() =>
            GseHost.CreateBuilder()
                .WithRegistry(NavExtensionRegistry.Instance)
                .Build()
                .Load(compiled));
        StringAssert.Contains(exception.Message, "nav.shortestTurn(to,from)");
    }

    [TestMethod]
    public void RegisterVmStandardExtensionsCannotBeOverriddenByRegistry()
    {
        const string script =
            """
            module StandardOverride

            on Start(value) {
              publish Done(floor: :integer.floor value)
            }
            """;

        var published = new List<GseMessage>();
        var host = GseHost.CreateBuilder()
            .WithRegistry(StandardOverrideRegistry.Instance)
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Message("Start", ("value", GseValueFactory.Decimal(10.9m))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GseValueFactory.Integer(10), published[0].Arguments["floor"]);
    }

    private sealed class TestExtensionRegistry : IGseExtensionRegistry
    {
        public static readonly TestExtensionRegistry Instance = new();

        private static readonly IGseExtensionFunction MathFloor = new DelegateExtensionFunction((_, args) =>
            GseFastValue.FromDecimal(Math.Floor(args[0].Number)));

        private TestExtensionRegistry()
        {
        }

        public bool TryResolve(GseExtensionReference reference, out IGseExtensionFunction function)
        {
            if (reference.SignatureId == "math.floor(_)")
            {
                function = MathFloor;
                return true;
            }

            function = default!;
            return false;
        }
    }

    private sealed class NavExtensionRegistry : IGseExtensionRegistry
    {
        public static readonly NavExtensionRegistry Instance = new();

        private static readonly IGseExtensionFunction ShortestTurn = new DelegateExtensionFunction((_, args) =>
        {
            var delta = (args[1].Number - args[0].Number + 540m) % 360m - 180m;
            return GseFastValue.FromDecimal(delta, GseDecimalUnit.Degree);
        });

        private NavExtensionRegistry()
        {
        }

        public bool TryResolve(GseExtensionReference reference, out IGseExtensionFunction function)
        {
            if (reference.SignatureId == "nav.shortestTurn(from,to)")
            {
                function = ShortestTurn;
                return true;
            }

            function = default!;
            return false;
        }
    }

    private sealed class StandardOverrideRegistry : IGseExtensionRegistry
    {
        public static readonly StandardOverrideRegistry Instance = new();

        private static readonly IGseExtensionFunction FakeIntegerFloor = new DelegateExtensionFunction((_, _) =>
            GseFastValue.FromInteger(999));

        private StandardOverrideRegistry()
        {
        }

        public bool TryResolve(GseExtensionReference reference, out IGseExtensionFunction function)
        {
            if (reference.SignatureId == "integer.floor(_)")
            {
                function = FakeIntegerFloor;
                return true;
            }

            function = default!;
            return false;
        }
    }

    private delegate GseFastValue ExtensionInvoke(GseExtensionContext context, ReadOnlySpan<GseFastValue> arguments);

    private sealed class DelegateExtensionFunction(ExtensionInvoke invoke) : IGseExtensionFunction
    {
        public GseFastValue Invoke(GseExtensionContext context, ReadOnlySpan<GseFastValue> arguments)
            => invoke(context, arguments);
    }

    private sealed record UnknownStatementNode : StatementNode;
}
