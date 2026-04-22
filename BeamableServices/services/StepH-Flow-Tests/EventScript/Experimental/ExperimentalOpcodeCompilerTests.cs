using StepH.Flow.EventScript.Experimental;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
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

    private static ExperimentalCompiledEventScript Compile(string script) => ExperimentalEventScriptCompiler.Compile(LinkModules(ParseModule(script)));
}