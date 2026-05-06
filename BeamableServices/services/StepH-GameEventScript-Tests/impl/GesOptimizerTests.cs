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
            predicate always() means '12.5' as :float
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script, "optimizer.es")
            .BuildModule();

        var predicate = module.Callables["always"];
        Assert.AreEqual(GameEventScriptCallableKind.Predicate, predicate.Kind);
        Assert.IsInstanceOfType<TypeCastExpressionNode>(predicate.Expression);

        var normalized = (TypeCastExpressionNode)predicate.Expression;
        Assert.AreEqual("boolean", normalized.TypeName);
        Assert.IsInstanceOfType<FloatLiteralExpressionNode>(normalized.Value);
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
