using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace StepH_Flow_Tests.EventScript.Conformance;

[TestClass]
public sealed class EventScriptJsonConformanceTests
{
    private static readonly string SpecDirectory = Path.Combine(GetSourceDirectory(), "Specs");

    public static IEnumerable<object[]> ConformanceCases()
        => EventScriptConformanceRunner.ConformanceCases(SpecDirectory);

    [TestMethod]
    [DynamicData(nameof(ConformanceCases))]
    public void JsonConformanceCasePasses(EventScriptConformanceCase testCase)
        => EventScriptConformanceRunner.RunCase(testCase);

    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "")
        => Path.GetDirectoryName(sourceFile)!;
}
