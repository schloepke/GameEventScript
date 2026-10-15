// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Compiler;

namespace GameEventScript.Tests.Native.Core;

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

        var module = new GesProgramBuilder()
            .AddScript(script, "optimizer.ges")
            .BuildModule();

        var predicate = module.Callables["high(value)"];
        Assert.AreEqual(GesCallableKind.PredicateCall, predicate.Kind);
        Assert.IsInstanceOfType<GesBinaryExpressionNode>(predicate.Expression);
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

        var module = new GesProgramBuilder()
            .AddScript(script, "constant-math.ges")
            .BuildModule();

        var handler = module.Handlers.Values.SelectMany(handlers => handlers).Single();
        var let = handler.Statements.OfType<GesLetStatementNode>().Single();

        Assert.IsInstanceOfType<GesIntegerLiteralExpressionNode>(let.Expression);
        Assert.AreEqual(42, ((GesIntegerLiteralExpressionNode)let.Expression).Value);
    }

    [TestMethod]
    public void NamedConstantInliningDoesNotMutateTheDeclarationAst()
    {
        const string script =
            """
            constant $rounds be 48
            constant $items be 128
            on Main() {
                let count be $rounds * $items
                emit Done(count)
            }
            """;

        var module = new GesProgramBuilder().AddScript(script, "constants.ges").BuildModule();
        var handler = module.Handlers.Values.SelectMany(handlers => handlers).Single();
        var expression = handler.Statements.OfType<GesLetStatementNode>().Single().Expression;

        Assert.IsInstanceOfType<GesIntegerLiteralExpressionNode>(expression);
        Assert.AreEqual(6144L, ((GesIntegerLiteralExpressionNode)expression).Value);
        Assert.AreNotSame(module.Constants["rounds"], expression);
        Assert.AreEqual(48L, ((GesIntegerLiteralExpressionNode)module.Constants["rounds"]).Value);
        Assert.AreEqual(1, module.Constants["rounds"].SourceRange?.Line);
    }
}
