using StepH.Flow.EventScript.Experimental;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;
using static StepH.Flow.EventScript.EventScriptManager;
using static StepH.Flow.EventScript.EventScriptMessage;

namespace StepH_Flow_Tests.EventScript.Experimental;

[TestClass]
public sealed class ExperimentalOpcodeCompilerTests
{
    [TestMethod]
    public void ExperimentalCompilerCanCompileAndInvokeSimpleHandler()
    {
        const string script =
            """
            module ExperimentalSimple
            on Start(value) {
                let doubled be value * 2
                publish Done(result: doubled)
            }
            """;

        var compiled = Compile(script);
        var result = compiled.Invoke(Message("Start", ("value", 3)));

        Assert.AreEqual("Start", result.Message);
        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
        Assert.AreEqual(6L, result.EmittedEvents[0].Arguments["result"].AsInteger());
        Assert.AreEqual(6L, result.Variables["doubled"].AsInteger());
    }

    [TestMethod]
    public void ExperimentalCompilerCollectsMultipleErrorsAndThrowsSingleException()
    {
        var linked = new LinkedEventScriptModule(
            typeDefinitions: new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal),
            ruleDefinitions: new Dictionary<string, RuleDefinitionNode>(StringComparer.Ordinal)
            {
                ["wounded"] = new RuleDefinitionNode("wounded", ["unit", "unit"], new IdentifierExpressionNode("unit"))
            },
            selectDefinitions: new Dictionary<string, SelectDefinitionNode>(StringComparer.Ordinal)
            {
                ["alive"] = new SelectDefinitionNode("alive", ["units", "units"], new IdentifierExpressionNode("units"))
            },
            handlers: new Dictionary<string, IReadOnlyList<EventHandlerNode>>(StringComparer.Ordinal)
            {
                ["Start"] =
                [
                    new EventHandlerNode(
                        "Start",
                        ["player", "player"],
                        [
                            new LetStatementNode("value", null, new IntegerLiteralExpressionNode(1)),
                            new LetStatementNode("value", null, new IntegerLiteralExpressionNode(2)),
                            new PublishStatementNode(
                                new MessageLiteralExpressionNode("Done",
                                [
                                    new NamedArgumentNode("x", new IntegerLiteralExpressionNode(1)),
                                    new NamedArgumentNode("x", new IntegerLiteralExpressionNode(2))
                                ]))
                        ])
                ]
            },
            sourceCount: 1);

        var exception = Assert.ThrowsExactly<EventScriptOpcodeCompilationException>(() => ExperimentalEventScriptCompiler.Compile(linked));

        Assert.IsNotNull(exception);
        Assert.IsGreaterThanOrEqualTo(5, exception.Errors.Count);
        Assert.IsTrue(exception.Errors.Any(error => error.Kind == EventScriptOpcodeCompilationErrorKind.DuplicateDefinitionParameter));
        Assert.IsTrue(exception.Errors.Any(error => error.Kind == EventScriptOpcodeCompilationErrorKind.DuplicateHandlerParameter));
        Assert.IsTrue(exception.Errors.Any(error => error.Kind == EventScriptOpcodeCompilationErrorKind.DuplicateVariable));
        Assert.IsTrue(exception.Errors.Any(error => error.Kind == EventScriptOpcodeCompilationErrorKind.DuplicatePublishArgument));
    }

    [TestMethod]
    public void ExperimentalRuntimeStaysNoThrowAndFallsBackToNothing()
    {
        const string script =
            """
            module ExperimentalNoThrow
            on Start() {
                let invalid be 'abc' - 2
                let missing be unknown.name
                publish Done(invalid: invalid, missing: missing)
            }
            """;

        var compiled = Compile(script);
        var result = compiled.Invoke(Message("Start"));

        Assert.HasCount(1, result.EmittedEvents);
        Assert.IsTrue(result.EmittedEvents[0].Arguments["invalid"].IsNaN());
        Assert.IsTrue(result.EmittedEvents[0].Arguments["missing"].isNothing());
    }

    [TestMethod]
    public void ExperimentalRuntimeSupportsRulesSelectsAndSeededRandomScopes()
    {
        const string script =
            """
            module ExperimentalDefinitions
            rule gtFive(value) means value > 5
            select twice(value) means value * 2

            on Start() {
                let isBig be gtFive(6)
                let twoX be twice(4)
                let seq be :random with 'stable-seed' :list[:select item from 1 to 3 -> :random from 1 to 10]
                publish Done(isBig: isBig, twoX: twoX, seq: seq)
            }
            """;

        var compiled = Compile(script);

        var first = compiled.Invoke(Message("Start"));
        var second = compiled.Invoke(Message("Start"));

        Assert.IsTrue(first.EmittedEvents[0].Arguments["isBig"].AsBoolean());
        Assert.AreEqual(8m, first.EmittedEvents[0].Arguments["twoX"].AsNumber());
        CollectionAssert.AreEqual(
            first.EmittedEvents[0].Arguments["seq"].AsList().Select(value => value.AsInteger()).ToList(),
            second.EmittedEvents[0].Arguments["seq"].AsList().Select(value => value.AsInteger()).ToList());
    }

    [TestMethod]
    public void ExperimentalRuntimeSupportsForFromRangeDispatch()
    {
        const string script =
            """
            module ExperimentalForRange
            on Start() {
                for value from 1 to 3 publish Tick(value: value)
            }
            """;

        var compiled = Compile(script);
        var result = compiled.Invoke(Message("Start"));

        Assert.HasCount(3, result.EmittedEvents);
        CollectionAssert.AreEqual(
            new List<long> { 1, 2, 3 },
            result.EmittedEvents.Select(it => it.Arguments["value"].AsInteger()).ToList());
    }

    [TestMethod]
    public void ExperimentalRuntimeRuleCallsAlwaysReturnBooleanWhileSelectKeepsOriginalType()
    {
        const string script =
            """
            module ExperimentalRuleBool
            rule numeric(value) means value * 2
            select numericSelect(value) means value * 2

            on Start() {
                let byRuleTwo be numeric(2)
                let byRuleZero be numeric(0)
                let bySelect be numericSelect(2)
                let byPredicate be 2 is numeric
                publish Done(
                    byRuleTwo: byRuleTwo,
                    byRuleZero: byRuleZero,
                    bySelect: bySelect,
                    byPredicate: byPredicate
                )
            }
            """;

        var args = Compile(script)
            .Invoke(Message("Start"))
            .EmittedEvents[0]
            .Arguments;

        Assert.AreEqual(EventScriptValueType.Boolean, args["byRuleTwo"].Type);
        Assert.AreEqual(EventScriptValueType.Boolean, args["byRuleZero"].Type);
        Assert.AreEqual(EventScriptValueType.Decimal, args["bySelect"].Type);
        Assert.AreEqual(EventScriptValueType.Boolean, args["byPredicate"].Type);
        Assert.IsTrue(args["byRuleTwo"].AsBoolean());
        Assert.IsFalse(args["byRuleZero"].AsBoolean());
        Assert.AreEqual(4m, args["bySelect"].AsNumber());
        Assert.IsTrue(args["byPredicate"].AsBoolean());
    }

    [TestMethod]
    public void ExperimentalRuntimeTreatsMessageAndHandlerAsFirstClassTypes()
    {
        const string script =
            """
            module ExperimentalMessageFirstClass
            on Start(unit, target) {
                let shoot as :handler be Shoot(unit, target)
                let msg as :message be shoot(unit: unit, target: target)
                publish Done(
                    msgIsMessage: msg is :message,
                    msgIsDictionary: msg is :dictionary,
                    handlerIsHandler: shoot is :handler,
                    handlerIsDictionary: shoot is :dictionary,
                    msgName: msg.name,
                    msgSignature: msg[:signatureid],
                    handlerName: shoot.name,
                    handlerParameterCount: :len shoot[:parameters],
                    messageArgumentCount: :len msg.arguments
                )
            }
            """;

        var compiled = Compile(script);
        var result = compiled.Invoke(Message("Start", ("unit", "u1"), ("target", "t1")));

        Assert.HasCount(1, result.EmittedEvents);
        var args = result.EmittedEvents[0].Arguments;
        Assert.IsTrue(args["msgIsMessage"].AsBoolean());
        Assert.IsFalse(args["msgIsDictionary"].AsBoolean());
        Assert.IsTrue(args["handlerIsHandler"].AsBoolean());
        Assert.IsFalse(args["handlerIsDictionary"].AsBoolean());
        Assert.AreEqual("Shoot", args["msgName"].AsText());
        Assert.AreEqual("Shoot(target,unit)", args["msgSignature"].AsText());
        Assert.AreEqual("Shoot", args["handlerName"].AsText());
        Assert.AreEqual(2L, args["handlerParameterCount"].AsInteger());
        Assert.AreEqual(2L, args["messageArgumentCount"].AsInteger());
    }

    private static ExperimentalCompiledEventScript Compile(string script) => ExperimentalEventScriptCompiler.Compile(LinkModules(ParseModule(script)));
}
