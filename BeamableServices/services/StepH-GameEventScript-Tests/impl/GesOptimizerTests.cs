using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public class GesOptimizerTests
{
    [TestMethod]
    public void ModuleBuilderOptimizesRuleExpressionsWithBooleanNormalizationAndConstantCastFolding()
    {
        const string script =
            """
            rule always() means '12.5' as :decimal
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script, "optimizer.es")
            .BuildModule();

        var rule = module.Callables["always"];
        Assert.AreEqual(GameEventScriptCallableKind.Rule, rule.Kind);
        Assert.IsInstanceOfType<TypeCastExpressionNode>(rule.Expression);

        var normalized = (TypeCastExpressionNode)rule.Expression;
        Assert.AreEqual("boolean", normalized.TypeName);
        Assert.IsInstanceOfType<DecimalLiteralExpressionNode>(normalized.Value);
        Assert.IsNotNull(normalized.SourceRange);
        Assert.IsNotNull(normalized.Value.SourceRange);
    }

    [TestMethod]
    public void ModuleBuilderOptimizesConstantArithmeticExpressions()
    {
        const string script =
            """
            module ConstantMath
            on Start {
                let value be 12 + 3 * 10
                emit Done(result: value)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script, "constant-math.es")
            .BuildModule();

        var handler = module.Handlers.Values.SelectMany(handlers => handlers).Single();
        var let = handler.Statements.OfType<LetStatementNode>().Single();

        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(let.Expression);
        Assert.AreEqual(42, ((IntegerLiteralExpressionNode)let.Expression).Value);
    }
}
