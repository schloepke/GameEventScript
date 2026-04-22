using System;
using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Parser;

namespace StepH_Flow_Tests.EventScript.Parser;

[TestClass]
public sealed class EventScriptAstJsonSerializerTests
{
    [TestMethod]
    public void ToJsonSerializesAstWithPolymorphicKinds()
    {
        const string script =
            """
            module AstDebug
            on Start(unit, amount) {
                let nextAmount be amount + 1
                if nextAmount > 0 {
                    publish Done(unit: unit, amount: nextAmount)
                }
            }
            """;

        var module = EventScriptManager.ParseModule(script, "ast-debug.es");

        var json = module.ToJson();

        StringAssert.Contains(json, "\"ModuleName\": \"AstDebug\"");
        StringAssert.Contains(json, "\"kind\": \"letStatement\"");
        StringAssert.Contains(json, "\"kind\": \"ifStatement\"");
        StringAssert.Contains(json, "\"kind\": \"publishStatement\"");
        StringAssert.Contains(json, "\"kind\": \"binaryExpression\"");
        StringAssert.Contains(json, "\"kind\": \"messageLiteralExpression\"");
    }

    [TestMethod]
    public void ToJsonDoesNotUseReferencePreserveMetadata()
    {
        const string script =
            """
            module AstNoPreserve
            on Start {
                publish Done
            }
            """;

        var module = EventScriptManager.ParseModule(script, "ast-no-preserve.es");

        var json = module.ToJson();

        Assert.IsFalse(json.Contains("\"$id\"", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("\"$ref\"", StringComparison.Ordinal));
    }
}
