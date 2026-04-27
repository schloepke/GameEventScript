using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;

namespace StepH_Flow_Tests.EventScript.Linker;

[TestClass]
public class EventScriptLinkBuilderScenarios
{
    [TestMethod]
    public void LinkBuilderCanMergeMultipleModulesIntoOneLinkedModule()
    {
        const string sharedScript =
            """
            record :meter as {
                current: :decimal,
                maximum: :decimal
            }

            rule wounded(unit) means unit.hp < unit.maxHp
            select woundedUnits(units) means units[:filter unit where unit is wounded]
            """;

        const string runtimeScript =
            """
            on Start(unit, units) {
                let hp as :meter be [current: unit.hp, maximum: unit.maxHp];
                let anyWounded be wounded(unit);
                let candidates be woundedUnits(units);
                publish Done(arg1: anyWounded, arg2: :len candidates, arg3: hp.maximum);
            }
            """;

        var linkedModule = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(sharedScript))
            .AddModule(EventScriptManager.ParseModule(runtimeScript))
            .Link();

        Assert.AreEqual(2, linkedModule.SourceCount);
        Assert.HasCount(1, linkedModule.TypeDefinitions);
        Assert.AreEqual(2, linkedModule.Callables.Count);
        Assert.IsTrue(linkedModule.Callables.TryGetValue("wounded", out var woundedCallable) && woundedCallable.Kind == LinkedCallableKind.Rule);
        Assert.IsTrue(linkedModule.Callables.TryGetValue("woundedUnits", out var woundedUnitsCallable) && woundedUnitsCallable.Kind == LinkedCallableKind.Select);
        Assert.HasCount(1, linkedModule.Handlers);
    }

    [TestMethod]
    public void LinkBuilderCollectsCaseViolationsForProgrammaticAstModules()
    {
        var module = new EventScriptModule(
            "InvalidCaseModule",
            "invalid-case.es",
            [],
            [
                new RuleDefinitionNode("Wounded", ["unit"], new BooleanLiteralExpressionNode(true))
            ],
            [
                new SelectDefinitionNode("Filter", ["Units"], new IdentifierExpressionNode("Units"))
            ],
            [
                new EventHandlerNode(
                    "start",
                    ["Target"],
                    [
                        new LetStatementNode("Value", null, new IntegerLiteralExpressionNode(1)),
                        new ExpressionStatementNode(new CallExpressionNode("Wounded", [new IdentifierExpressionNode("Target")]))
                    ])
            ]);

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() =>
            new EventScriptLinkBuilder().AddModule(module).Link());

        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.InvalidIdentifierCase &&
            error.Symbol == "Wounded"));
        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.InvalidIdentifierCase &&
            error.Symbol == "Filter"));
        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.InvalidMessageCase &&
            error.Symbol == "start"));
    }

    [TestMethod]
    public void LinkBuilderOptimizesRuleExpressionsWithBooleanNormalizationAndConstantCastFolding()
    {
        const string script =
            """
            rule always() means '12.5' as :decimal
            """;

        var linked = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(script, "optimizer.es"))
            .Link();

        var rule = linked.Callables["always"];
        Assert.AreEqual(LinkedCallableKind.Rule, rule.Kind);
        Assert.IsInstanceOfType<TypeCastExpressionNode>(rule.Expression);

        var normalized = (TypeCastExpressionNode)rule.Expression;
        Assert.AreEqual("boolean", normalized.TypeName);
        Assert.IsInstanceOfType<DecimalLiteralExpressionNode>(normalized.Value);
        Assert.IsNotNull(normalized.SourceRange);
        Assert.IsNotNull(normalized.Value.SourceRange);
    }

    [TestMethod]
    public void LinkBuilderOptimizesConstantArithmeticExpressions()
    {
        const string script =
            """
            module ConstantMath
            on Start {
                let value be 12 + 3 * 10
                publish Done(result: value)
            }
            """;

        var linked = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(script, "constant-math.es"))
            .Link();

        var handler = linked.Handlers.Values.SelectMany(handlers => handlers).Single();
        var let = handler.Statements.OfType<LetStatementNode>().Single();

        Assert.IsInstanceOfType<DecimalLiteralExpressionNode>(let.Expression);
        Assert.AreEqual(42m, ((DecimalLiteralExpressionNode)let.Expression).Value);
    }
}
