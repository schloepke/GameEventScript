// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;

namespace StepH_GameEventScript_Tests.Native.Core;

[TestClass]
public class GesOptimizerTests
{
    [TestMethod]
    public void ModuleBuilderDoesNotAddImplicitPredicateBooleanNormalization()
    {
        const string script =
            """
            predicate high(value) be value > 3
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script, "optimizer.ges")
            .BuildModule();

        var predicate = module.Callables["high(value)"];
        Assert.AreEqual(GameEventScriptCallableKind.PredicateCall, predicate.Kind);
        Assert.IsInstanceOfType<BinaryExpressionNode>(predicate.Expression);
        Assert.IsNotNull(predicate.Expression.SourceRange);
    }

    [TestMethod]
    public void ModuleBuilderOptimizesConstantArithmeticExpressions()
    {
        const string script =
            """
            module constantmath
            on Start {
                let value be 12 + 3 * 10
                emit Done(result: value)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script, "constant-math.ges")
            .BuildModule();

        var handler = module.Handlers.Values.SelectMany(handlers => handlers).Single();
        var let = handler.Statements.OfType<LetStatementNode>().Single();

        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(let.Expression);
        Assert.AreEqual(42, ((IntegerLiteralExpressionNode)let.Expression).Value);
    }
}
