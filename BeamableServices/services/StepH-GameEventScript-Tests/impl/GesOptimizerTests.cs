using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public class GesOptimizerTests
{
    [TestMethod]
    public void ModuleBuilderDoesNotAddImplicitPredicateBooleanNormalization()
    {
        const string script =
            """
            predicate high(value) means value > 3
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script, "optimizer.es")
            .BuildModule();

        var predicate = module.Callables["high"];
        Assert.AreEqual(GameEventScriptCallableKind.Predicate, predicate.Kind);
        Assert.IsInstanceOfType<BinaryExpressionNode>(predicate.Expression);
        Assert.IsNotNull(predicate.Expression.SourceRange);
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
