using System.Collections.Generic;
using System.Text.Json;

namespace StepH_Flow_Tests.EventScript.Conformance;

public sealed class EventScriptConformanceSuite
{
    public int Version { get; set; }

    public string? Name { get; set; }

    public List<EventScriptConformanceTest> Tests { get; set; } = [];
}

public sealed class EventScriptConformanceTest
{
    public string? Kind { get; set; }

    public string? Name { get; set; }

    public string? Script { get; set; }

    public List<EventScriptSourceSpec>? Scripts { get; set; }

    public List<string>? RandomSequence { get; set; }

    public EventScriptRuntimeLimitsSpec? RuntimeLimits { get; set; }

    public int? MaxProcessedEventsPerRun { get; set; }

    public List<EventScriptApiStepSpec>? Steps { get; set; }

    public EventScriptExpectedCompileErrorSpec? ExpectedError { get; set; }

    public string? Operation { get; set; }

    public string? Direction { get; set; }

    public JsonElement Target { get; set; }

    public JsonElement Selector { get; set; }

    public JsonElement Value { get; set; }

    public JsonElement Expected { get; set; }
}

public sealed class EventScriptSourceSpec
{
    public string? SourceName { get; set; }

    public string? Text { get; set; }
}

public sealed class EventScriptApiStepSpec
{
    public JsonElement Input { get; set; }

    public List<JsonElement>? ExpectedPublished { get; set; }

    public List<EventScriptDiagnosticExpectationSpec>? ExpectedDiagnostics { get; set; }
}

public sealed class EventScriptRuntimeLimitsSpec
{
    public int? MaxExecutionSteps { get; set; }

    public int? MaxLoopIterations { get; set; }

    public int? MaxCallDepth { get; set; }

    public int? MaxRangeItems { get; set; }

    public int? MaxGeneratedCollectionItems { get; set; }

    public int? MaxDiceCount { get; set; }

    public int? MaxDiceSides { get; set; }
}

public sealed class EventScriptDiagnosticExpectationSpec
{
    public string? Kind { get; set; }

    public string? Name { get; set; }

    public string? DetailContains { get; set; }

    public string? MessageContains { get; set; }
}

public sealed class EventScriptExpectedCompileErrorSpec
{
    public string? Phase { get; set; }

    public string? Kind { get; set; }

    public string? Symbol { get; set; }

    public string? SymbolKind { get; set; }

    public string? ModuleName { get; set; }

    public string? MessageContains { get; set; }
}

public sealed class EventScriptConformanceCase(
    string suiteFile,
    string suiteName,
    EventScriptConformanceTest test)
{
    public string SuiteFile { get; } = suiteFile;

    public string SuiteName { get; } = suiteName;

    public EventScriptConformanceTest Test { get; } = test;

    public override string ToString() => $"{SuiteName}/{Test.Name}";
}
