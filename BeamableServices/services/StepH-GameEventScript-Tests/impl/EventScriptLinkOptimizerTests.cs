using StepH.GameEventScript;
using StepH.GameEventScript.Linker;
using StepH.GameEventScript.Parser;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public class EventScriptLinkOptimizerTests
{
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

        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(let.Expression);
        Assert.AreEqual(42, ((IntegerLiteralExpressionNode)let.Expression).Value);
    }
}
