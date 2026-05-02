using System.Runtime.CompilerServices;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class GameEventScriptJsonConformanceTests
{
    private static readonly string SpecDirectory = Path.Combine(GetSourceDirectory(), "Specs");

    public static IEnumerable<object[]> ConformanceCases()
        => GameEventScriptConformanceRunner.ConformanceCases(SpecDirectory);

    [TestMethod]
    [DynamicData(nameof(ConformanceCases))]
    public void JsonConformanceCasePasses(GameEventScriptConformanceCase testCase)
        => GameEventScriptConformanceRunner.RunCase(testCase);

    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "")
        => Path.GetDirectoryName(sourceFile)!;
}
