using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptDebugSegmentTests
{
    private const string Script =
        """
        module DebugInfo

        on Start(value) {
          let doubled be value * 2
          emit Done(value: doubled)
        }
        """;

    [TestMethod]
    public void CompileWithoutDebugInfoKeepsDebugSegmentEmpty()
    {
        var compiled = GameEventScriptBuilder.Create()
            .AddScript(Script)
            .Compile();

        Assert.IsTrue(compiled.DebugSegment.IsEmpty);
        Assert.HasCount(0, compiled.DebugSegment.DiagnosticSites);
        Assert.IsFalse(compiled.Options.EnableDebugInfo);
    }

    [TestMethod]
    public void CompileWithDebugInfoStoresDiagnosticSitesInDebugSegment()
    {
        var compiled = GameEventScriptBuilder.Create()
            .WithDebugInfo()
            .AddScript(Script)
            .Compile();

        Assert.IsFalse(compiled.DebugSegment.IsEmpty);
        Assert.IsTrue(compiled.DebugSegment.DiagnosticSites.Any(site => site.Name == "doubled"));
        Assert.IsTrue(compiled.Options.EnableDebugInfo);
    }

    [TestMethod]
    public void CompileWithDiagnosticsAlsoEnablesDebugInfoForCollectors()
    {
        var compiled = GameEventScriptBuilder.Create()
            .AddScript(Script)
            .Compile(new GameEventScriptCompileOptions { EnableDiagnostics = true });

        Assert.IsTrue(compiled.Options.EnableDiagnostics);
        Assert.IsTrue(compiled.Options.EnableDebugInfo);
        Assert.IsFalse(compiled.DebugSegment.IsEmpty);
    }
}
