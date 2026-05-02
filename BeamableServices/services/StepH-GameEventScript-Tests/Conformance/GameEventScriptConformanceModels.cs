using System.Text.Json;

namespace StepH_GameEventScript_Tests.Conformance;

public sealed class GameEventScriptConformanceSuite
{
    public int Version { get; set; }

    public string? Name { get; set; }

    public List<GameEventScriptConformanceTest> Tests { get; set; } = [];
}

public sealed class GameEventScriptConformanceTest
{
    public string? Kind { get; set; }

    public string? Name { get; set; }

    public string? Script { get; set; }

    public List<GameEventScriptSourceSpec>? Scripts { get; set; }

    public List<string>? RandomSequence { get; set; }

    public GameEventScriptCompileOptionsSpec? CompileOptions { get; set; }

    public GameEventScriptRuntimeLimitsSpec? RuntimeLimits { get; set; }

    public int? MaxProcessedEventsPerRun { get; set; }

    public List<GameEventScriptExternalSubscriberSpec>? ExternalSubscribers { get; set; }

    public List<GameEventScriptApiStepSpec>? Steps { get; set; }

    public GameEventScriptExpectedCompileErrorSpec? ExpectedError { get; set; }

    public GameEventScriptMessageSignatureSpec? Signature { get; set; }

    public JsonElement Message { get; set; }

    public string? ExpectedMessageName { get; set; }

    public string? ExpectedSignatureId { get; set; }

    public string? ExpectedMessageSignatureId { get; set; }

    public bool? ExpectedMatches { get; set; }

    public int? ExpectedArgumentCount { get; set; }

    public List<GameEventScriptMessageDefinitionExpectationSpec>? ExpectedMessageDefinitions { get; set; }
}

public sealed class GameEventScriptSourceSpec
{
    public string? SourceName { get; set; }

    public string? Text { get; set; }
}

public sealed class GameEventScriptMessageSignatureSpec
{
    public string? Name { get; set; }

    public List<string>? Parameters { get; set; }
}

public sealed class GameEventScriptMessageDefinitionExpectationSpec
{
    public string? Name { get; set; }

    public List<string>? SignatureIds { get; set; }

    public int? Count { get; set; }
}

public sealed class GameEventScriptApiStepSpec
{
    public JsonElement Input { get; set; }

    public List<JsonElement>? ExpectedPublished { get; set; }

    public string? ExpectedDiagnosticsMode { get; set; }

    public List<GameEventScriptDiagnosticExpectationSpec>? ExpectedDiagnostics { get; set; }

    public List<GameEventScriptDiagnosticExpectationSpec>? UnexpectedDiagnostics { get; set; }
}

public sealed class GameEventScriptCompileOptionsSpec
{
    public bool? EnableDiagnostics { get; set; }
}

public sealed class GameEventScriptExternalSubscriberSpec
{
    public string? Message { get; set; }

    public List<string>? Parameters { get; set; }

    public int? Priority { get; set; }

    public bool Throw { get; set; }

    public List<GameEventScriptExternalPublishSpec>? Publish { get; set; }
}

public sealed class GameEventScriptExternalPublishSpec
{
    public string? Name { get; set; }

    public bool ForwardArguments { get; set; }

    public JsonElement Args { get; set; }
}

public sealed class GameEventScriptRuntimeLimitsSpec
{
    public int? MaxExecutionSteps { get; set; }

    public int? MaxLoopIterations { get; set; }

    public int? MaxCallDepth { get; set; }

    public int? MaxRangeItems { get; set; }

    public int? MaxGeneratedCollectionItems { get; set; }

    public int? MaxDiceCount { get; set; }

    public int? MaxDiceSides { get; set; }
}

public sealed class GameEventScriptDiagnosticExpectationSpec
{
    public string? Kind { get; set; }

    public string? Name { get; set; }

    public string? DetailContains { get; set; }

    public string? MessageContains { get; set; }
}

public sealed class GameEventScriptExpectedCompileErrorSpec
{
    public string? Phase { get; set; }

    public string? Kind { get; set; }

    public string? Symbol { get; set; }

    public string? SymbolKind { get; set; }

    public string? ModuleName { get; set; }

    public string? MessageContains { get; set; }
}

public sealed class GameEventScriptConformanceCase(
    string suiteFile,
    string suiteName,
    GameEventScriptConformanceTest test,
    string? engine)
{
    public string SuiteFile { get; } = suiteFile;

    public string SuiteName { get; } = suiteName;

    public GameEventScriptConformanceTest Test { get; } = test;

    public string? Engine { get; } = engine;

    public override string ToString() => string.IsNullOrWhiteSpace(Engine)
        ? $"{SuiteName}/{Test.Name}"
        : $"{SuiteName}/{Test.Name} [{Engine}]";
}
