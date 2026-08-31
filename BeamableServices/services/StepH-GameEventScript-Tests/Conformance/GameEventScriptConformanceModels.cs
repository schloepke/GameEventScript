using System.Text.Json;

namespace StepH_GameEventScript_Tests.Conformance;

public sealed class GameEventScriptConformanceSuite
{
    public int Version { get; set; }

    public string? Name { get; set; }

    public string? Level { get; set; }

    public List<GameEventScriptConformanceTest> Tests { get; set; } = [];
}

public sealed class GameEventScriptConformanceTest
{
    public string? Kind { get; set; }

    public string? Name { get; set; }

    public string? Level { get; set; }

    public string? Script { get; set; }

    public List<GameEventScriptSourceSpec>? Scripts { get; set; }

    public List<GameEventScriptSourceSpec>? Programs { get; set; }

    public List<string>? RandomSequence { get; set; }

    public GameEventScriptCompileOptionsSpec? CompileOptions { get; set; }

    public GameEventScriptRuntimeLimitsSpec? RuntimeLimits { get; set; }

    public int? Iterations { get; set; }

    public int? WarmupIterations { get; set; }

    public bool DumpBinary { get; set; }

    public List<GameEventScriptExternalSubscriberSpec>? ExternalSubscribers { get; set; }

    public List<JsonElement>? ExpectedInitializationPublished { get; set; }

    public List<JsonElement>? ExpectedInitializationOutboundPublished { get; set; }

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

    public GameEventScriptBytecodeOpcodeExpectationSpec? ExpectedOpcodes { get; set; }
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

public sealed class GameEventScriptBytecodeOpcodeExpectationSpec
{
    public List<string>? Contains { get; set; }

    public List<string>? NotContains { get; set; }

    public Dictionary<string, int>? Counts { get; set; }

    public Dictionary<string, int>? MinCounts { get; set; }
}

public sealed class GameEventScriptApiStepSpec
{
    public JsonElement Input { get; set; }

    public List<JsonElement>? ExpectedPublished { get; set; }

    public List<JsonElement>? ExpectedOutboundPublished { get; set; }

    public List<GameEventScriptRuntimeLimitExpectationSpec>? ExpectedRuntimeLimits { get; set; }

    public List<GameEventScriptRuntimeLimitExpectationSpec>? UnexpectedRuntimeLimits { get; set; }

    public int? OpcodeBudget { get; set; }

    public bool? ExpectedPaused { get; set; }
}

public sealed class GameEventScriptCompileOptionsSpec
{
    public bool? Optimize { get; set; }

    public bool? EnableDebugInfo { get; set; }
}

public sealed class GameEventScriptExternalSubscriberSpec
{
    public string? Message { get; set; }

    public List<string>? Parameters { get; set; }

    public int? Priority { get; set; }

    public bool Throw { get; set; }

    public List<GameEventScriptExternalEmitSpec>? Emit { get; set; }
}

public sealed class GameEventScriptExternalEmitSpec
{
    public string? Name { get; set; }

    public bool ForwardArguments { get; set; }

    public JsonElement Args { get; set; }
}

public sealed class GameEventScriptRuntimeLimitsSpec
{
    public int? MaxProcessedEventsPerRun { get; set; }

    public int? MaxQueuedMessagesPerRun { get; set; }

    public int? MaxExecutionSteps { get; set; }

    public int? MaxLoopIterations { get; set; }

    public int? MaxCallDepth { get; set; }

    public int? MaxRangeItems { get; set; }

    public int? MaxGeneratedCollectionItems { get; set; }

    public int? MaxDiceCount { get; set; }

    public int? MaxDiceSides { get; set; }
}

public sealed class GameEventScriptRuntimeLimitExpectationSpec
{
    public string? Name { get; set; }

    public string? DetailContains { get; set; }

    public int? Limit { get; set; }
}

public sealed class GameEventScriptExpectedCompileErrorSpec
{
    public string? Phase { get; set; }

    public string? Kind { get; set; }

    public string? Symbol { get; set; }

    public string? SymbolKind { get; set; }

    public string? ModuleName { get; set; }

    public string? MessageContains { get; set; }

    public string? SourceName { get; set; }

    public int? Line { get; set; }

    public int? Column { get; set; }

    public int? EndLine { get; set; }

    public int? EndColumn { get; set; }
}

public sealed class GameEventScriptConformanceCase(
    string suiteFile,
    string suiteName,
    string level,
    GameEventScriptConformanceTest test,
    string? engine)
{
    public string SuiteFile { get; } = suiteFile;

    public string SuiteName { get; } = suiteName;

    public string Level { get; } = level;

    public GameEventScriptConformanceTest Test { get; } = test;

    public string? Engine { get; } = engine;

    public override string ToString() => string.IsNullOrWhiteSpace(Engine)
        ? $"{SuiteName}/{Test.Name}"
        : $"{SuiteName}/{Test.Name} [{Engine}]";
}
