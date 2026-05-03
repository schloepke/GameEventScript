using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;

namespace StepH_GameEventScript_Tests.Compiler;

[TestClass]
public sealed class GameEventScriptModuleBuildErrorTests
{
    [TestMethod]
    public void ModuleBuildErrorsUseParserSourceRanges()
    {
        const string script =
            """
            module Location

            on Start {
              let value be 1
              let value be 2
            }
            """;

        var exception = Assert.ThrowsExactly<GameEventScriptModuleBuildException>(() =>
            GameEventScriptBuilder.Create()
                .AddScript(script, "location.ges")
                .BuildModule());

        var error = exception.Errors.Single(error => error.Kind == GameEventScriptModuleBuildErrorKind.DuplicateVariable);
        Assert.AreEqual("Location", error.ModuleName);
        Assert.AreEqual("location.ges", error.SourceLocation.SourceName);
        Assert.AreEqual(5, error.SourceLocation.Line);
        Assert.AreEqual(3, error.SourceLocation.Column);
    }
}
