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

    public EventScriptCompileOptionsSpec? CompileOptions { get; set; }

    public EventScriptRuntimeLimitsSpec? RuntimeLimits { get; set; }

    public int? MaxProcessedEventsPerRun { get; set; }

    public List<EventScriptExternalSubscriberSpec>? ExternalSubscribers { get; set; }

    public List<EventScriptApiStepSpec>? Steps { get; set; }

    public EventScriptExpectedCompileErrorSpec? ExpectedError { get; set; }

    public EventScriptMessageSignatureSpec? Signature { get; set; }

    public JsonElement Message { get; set; }

    public string? ExpectedMessageName { get; set; }

    public string? ExpectedSignatureId { get; set; }

    public string? ExpectedMessageSignatureId { get; set; }

    public bool? ExpectedMatches { get; set; }

    public int? ExpectedArgumentCount { get; set; }

    public List<EventScriptMessageDefinitionExpectationSpec>? ExpectedMessageDefinitions { get; set; }
}

public sealed class EventScriptSourceSpec
{
    public string? SourceName { get; set; }

    public string? Text { get; set; }
}

public sealed class EventScriptMessageSignatureSpec
{
    public string? Name { get; set; }

    public List<string>? Parameters { get; set; }
}

public sealed class EventScriptMessageDefinitionExpectationSpec
{
    public string? Name { get; set; }

    public List<string>? SignatureIds { get; set; }

    public int? Count { get; set; }
}

public sealed class EventScriptApiStepSpec
{
    public JsonElement Input { get; set; }

    public List<JsonElement>? ExpectedPublished { get; set; }

    public string? ExpectedDiagnosticsMode { get; set; }

    public List<EventScriptDiagnosticExpectationSpec>? ExpectedDiagnostics { get; set; }

    public List<EventScriptDiagnosticExpectationSpec>? UnexpectedDiagnostics { get; set; }
}

public sealed class EventScriptCompileOptionsSpec
{
    public bool? EnableDiagnostics { get; set; }
}

public sealed class EventScriptExternalSubscriberSpec
{
    public string? Message { get; set; }

    public List<string>? Parameters { get; set; }

    public int? Priority { get; set; }

    public bool Throw { get; set; }

    public List<EventScriptExternalPublishSpec>? Publish { get; set; }
}

public sealed class EventScriptExternalPublishSpec
{
    public string? Name { get; set; }

    public bool ForwardArguments { get; set; }

    public JsonElement Args { get; set; }
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
