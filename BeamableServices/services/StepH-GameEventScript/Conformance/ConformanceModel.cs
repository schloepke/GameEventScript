// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Conformance;

/// <summary>
/// Defines the supported conformance test kind values.
/// </summary>
public enum ConformanceTestKind
{
    /// <summary>
    /// Identifies the script api value.
    /// </summary>
    ScriptApi = 0,
    /// <summary>
    /// Identifies the compile error value.
    /// </summary>
    CompileError = 1,
    /// <summary>
    /// Identifies the load error value.
    /// </summary>
    LoadError = 2,
    /// <summary>
    /// Identifies the message api value.
    /// </summary>
    MessageApi = 3,
    /// <summary>
    /// Identifies the compile metadata value.
    /// </summary>
    CompileMetadata = 4,
    /// <summary>
    /// Identifies the bytecode value.
    /// </summary>
    Bytecode = 5,
    /// <summary>
    /// Identifies the performance value.
    /// </summary>
    Performance = 6,
    /// <summary>
    /// Identifies the bytecode snapshot value.
    /// </summary>
    BytecodeSnapshot = 7,
    /// <summary>
    /// Identifies the value api value.
    /// </summary>
    ValueApi = 8,
    /// <summary>
    /// Identifies the external type api value.
    /// </summary>
    ExternalTypeApi = 9,
    /// <summary>
    /// Identifies the program binary value.
    /// </summary>
    ProgramBinary = 10
}

/// <summary>
/// Defines the supported conformance binary outcome values.
/// </summary>
public enum ConformanceBinaryOutcome
{
    /// <summary>
    /// Identifies the valid value.
    /// </summary>
    Valid = 0,
    /// <summary>
    /// Identifies the read error value.
    /// </summary>
    ReadError = 1,
    /// <summary>
    /// Identifies the validation error value.
    /// </summary>
    ValidationError = 2
}

/// <summary>
/// Defines the supported conformance test level values.
/// </summary>
public enum ConformanceTestLevel
{
    /// <summary>
    /// Identifies the atomic value.
    /// </summary>
    Atomic = 0,
    /// <summary>
    /// Identifies the scenario value.
    /// </summary>
    Scenario = 1
}

/// <summary>
/// Defines the supported conformance pump mode values.
/// </summary>
public enum ConformancePumpMode
{
    /// <summary>
    /// Identifies the completion value.
    /// </summary>
    Completion = 0,
    /// <summary>
    /// Identifies the frames value.
    /// </summary>
    Frames = 1,
    /// <summary>
    /// Identifies the enqueue value.
    /// </summary>
    Enqueue = 2,
    /// <summary>
    /// Identifies the frame value.
    /// </summary>
    Frame = 3
}

/// <summary>
/// Defines the supported conformance binary64 comparison mode values.
/// </summary>
public enum ConformanceBinary64ComparisonMode
{
    /// <summary>
    /// Identifies the exact value.
    /// </summary>
    Exact = 0,
    /// <summary>
    /// Identifies the ulp value.
    /// </summary>
    Ulp = 1
}

/// <summary>
/// Defines the supported conformance publish sink mode values.
/// </summary>
public enum ConformancePublishSinkMode
{
    /// <summary>
    /// Identifies the accept value.
    /// </summary>
    Accept = 0,
    /// <summary>
    /// Identifies the absent value.
    /// </summary>
    Absent = 1,
    /// <summary>
    /// Identifies the reject value.
    /// </summary>
    Reject = 2,
    /// <summary>
    /// Identifies the throw value.
    /// </summary>
    Throw = 3
}

/// <summary>
/// Defines the supported conformance external type registry mode values.
/// </summary>
public enum ConformanceExternalTypeRegistryMode
{
    /// <summary>
    /// Identifies the environment value.
    /// </summary>
    Environment = 0,
    /// <summary>
    /// Identifies the absent value.
    /// </summary>
    Absent = 1,
    /// <summary>
    /// Identifies the mismatch value.
    /// </summary>
    Mismatch = 2
}

/// <summary>
/// Defines the supported conformance observer event kind values.
/// </summary>
public enum ConformanceObserverEventKind
{
    /// <summary>
    /// Identifies the emit value.
    /// </summary>
    Emit = 0,
    /// <summary>
    /// Identifies the publish value.
    /// </summary>
    Publish = 1,
    /// <summary>
    /// Identifies the dispatch started value.
    /// </summary>
    DispatchStarted = 2,
    /// <summary>
    /// Identifies the dispatch completed value.
    /// </summary>
    DispatchCompleted = 3,
    /// <summary>
    /// Identifies the runtime limit value.
    /// </summary>
    RuntimeLimit = 4,
    /// <summary>
    /// Identifies the diagnostic value.
    /// </summary>
    Diagnostic = 5
}

/// <summary>
/// Defines the supported conformance native action kind values.
/// </summary>
public enum ConformanceNativeActionKind
{
    /// <summary>
    /// Identifies the load program value.
    /// </summary>
    LoadProgram = 0,
    /// <summary>
    /// Identifies the detach program value.
    /// </summary>
    DetachProgram = 1,
    /// <summary>
    /// Identifies the subscribe handler value.
    /// </summary>
    SubscribeHandler = 2,
    /// <summary>
    /// Identifies the unsubscribe handler value.
    /// </summary>
    UnsubscribeHandler = 3
}

/// <summary>
/// Represents a conformance source document.
/// </summary>
public sealed class ConformanceSourceDocument
{
    private readonly byte[] _utf8Bytes;

    internal ConformanceSourceDocument(byte[] utf8Bytes, bool hasByteOrderMark, string lineEnding)
    {
        _utf8Bytes = utf8Bytes;
        Utf8Bytes = Array.AsReadOnly(_utf8Bytes);
        HasByteOrderMark = hasByteOrderMark;
        LineEnding = lineEnding;
    }

    /// <summary>
    /// Gets the utf8 bytes.
    /// </summary>
    public IReadOnlyList<byte> Utf8Bytes { get; }
    /// <summary>
    /// Gets a value indicating whether has byte order mark.
    /// </summary>
    public bool HasByteOrderMark { get; }
    /// <summary>
    /// Gets the line ending.
    /// </summary>
    public string LineEnding { get; }

    internal ReadOnlySpan<byte> Utf8Span => _utf8Bytes;
}

/// <summary>
/// Represents a conformance document.
/// </summary>
public sealed class ConformanceDocument
{
    internal ConformanceDocument(int formatVersion, string suiteId, string title, IReadOnlyList<ConformanceCase> cases, ConformanceSourceDocument source, ConformanceSourceRange frontmatterRange)
    {
        FormatVersion = formatVersion;
        SuiteId = suiteId;
        Title = title;
        Cases = Copy(cases);
        Source = source;
        FrontmatterRange = frontmatterRange;
    }

    /// <summary>
    /// Gets the format version.
    /// </summary>
    public int FormatVersion { get; }
    /// <summary>
    /// Gets the suite id.
    /// </summary>
    public string SuiteId { get; }
    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Gets the cases.
    /// </summary>
    public IReadOnlyList<ConformanceCase> Cases { get; }
    /// <summary>
    /// Gets the source.
    /// </summary>
    public ConformanceSourceDocument Source { get; }
    /// <summary>
    /// Gets the frontmatter range.
    /// </summary>
    public ConformanceSourceRange FrontmatterRange { get; }

    internal static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        var copy = new T[source.Count];
        for (var index = 0; index < source.Count; index++) copy[index] = source[index];
        return Array.AsReadOnly(copy);
    }
}

/// <summary>
/// Represents a conformance case.
/// </summary>
public sealed class ConformanceCase
{
    internal ConformanceCase(
        string id,
        string fullId,
        string title,
        ConformanceTestKind kind,
        ConformanceTestLevel level,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> tags,
        ConformanceCapabilityRequirements requires,
        ConformanceCompileOptions compile,
        ConformanceRuntimeLimits runtimeLimits,
        ConformanceComparisonOptions comparison,
        ConformancePublishSinkMode publishSink,
        ConformanceExternalTypeRegistryMode externalTypeRegistry,
        uint hostCount,
        IReadOnlyList<string> deferredPrograms,
        ConformanceRandomConfiguration? random,
        IReadOnlyList<ConformanceSourceInput> sources,
        IReadOnlyList<ConformanceNativeHandler> nativeHandlers,
        IReadOnlyList<ConformanceStep> steps,
        ConformanceExpectation expectation,
        ConformanceMessageApiCase? messageApi,
        ConformanceValueApiCase? valueApi,
        ConformanceExternalTypeApiCase? externalTypeApi,
        ConformanceBinaryFixture? binaryFixture,
        ConformancePerformanceWorkload? performance,
        string? expectedAssembler,
        ConformanceSourceRange metadataBlockRange,
        ConformanceSourceRange? expectationBlockRange,
        ConformanceSourceRange? stepsTableRange,
        ConformanceSourceRange? assemblerBlockRange,
        ConformanceSourceRange? assemblerPayloadRange,
        ConformanceSourceRange range)
    {
        Id = id;
        FullId = fullId;
        SuiteId = fullId.Substring(0, fullId.Length - id.Length - 1);
        Title = title;
        Kind = kind;
        Level = level;
        Categories = ConformanceDocument.Copy(categories);
        Tags = ConformanceDocument.Copy(tags);
        Requires = requires;
        Compile = compile;
        RuntimeLimits = runtimeLimits;
        Comparison = comparison;
        PublishSink = publishSink;
        ExternalTypeRegistry = externalTypeRegistry;
        HostCount = hostCount;
        DeferredPrograms = ConformanceDocument.Copy(deferredPrograms);
        Random = random;
        Sources = ConformanceDocument.Copy(sources);
        NativeHandlers = ConformanceDocument.Copy(nativeHandlers);
        Steps = ConformanceDocument.Copy(steps);
        Expectation = expectation;
        MessageApi = messageApi;
        ValueApi = valueApi;
        ExternalTypeApi = externalTypeApi;
        BinaryFixture = binaryFixture;
        Performance = performance;
        ExpectedAssembler = expectedAssembler;
        MetadataBlockRange = metadataBlockRange;
        ExpectationBlockRange = expectationBlockRange;
        StepsTableRange = stepsTableRange;
        AssemblerBlockRange = assemblerBlockRange;
        AssemblerPayloadRange = assemblerPayloadRange;
        Range = range;
    }

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the full id.
    /// </summary>
    public string FullId { get; }
    /// <summary>
    /// Gets the suite id.
    /// </summary>
    public string SuiteId { get; }
    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Gets the kind.
    /// </summary>
    public ConformanceTestKind Kind { get; }
    /// <summary>
    /// Gets the level.
    /// </summary>
    public ConformanceTestLevel Level { get; }
    /// <summary>
    /// Gets the categories.
    /// </summary>
    public IReadOnlyList<string> Categories { get; }
    /// <summary>
    /// Gets the tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }
    /// <summary>
    /// Gets the requires.
    /// </summary>
    public ConformanceCapabilityRequirements Requires { get; }
    /// <summary>
    /// Gets the compile.
    /// </summary>
    public ConformanceCompileOptions Compile { get; }
    /// <summary>
    /// Gets the runtime limits.
    /// </summary>
    public ConformanceRuntimeLimits RuntimeLimits { get; }
    /// <summary>
    /// Gets the comparison.
    /// </summary>
    public ConformanceComparisonOptions Comparison { get; }
    /// <summary>
    /// Gets the publish sink.
    /// </summary>
    public ConformancePublishSinkMode PublishSink { get; }
    /// <summary>
    /// Gets the external type registry.
    /// </summary>
    public ConformanceExternalTypeRegistryMode ExternalTypeRegistry { get; }
    /// <summary>
    /// Gets the host count.
    /// </summary>
    public uint HostCount { get; }
    /// <summary>
    /// Gets the deferred programs.
    /// </summary>
    public IReadOnlyList<string> DeferredPrograms { get; }
    /// <summary>
    /// Gets the random.
    /// </summary>
    public ConformanceRandomConfiguration? Random { get; }
    /// <summary>
    /// Gets the sources.
    /// </summary>
    public IReadOnlyList<ConformanceSourceInput> Sources { get; }
    /// <summary>
    /// Gets the native handlers.
    /// </summary>
    public IReadOnlyList<ConformanceNativeHandler> NativeHandlers { get; }
    /// <summary>
    /// Gets the steps.
    /// </summary>
    public IReadOnlyList<ConformanceStep> Steps { get; }
    /// <summary>
    /// Gets the expectation.
    /// </summary>
    public ConformanceExpectation Expectation { get; }
    /// <summary>
    /// Gets the message api.
    /// </summary>
    public ConformanceMessageApiCase? MessageApi { get; }
    /// <summary>
    /// Gets the value api.
    /// </summary>
    public ConformanceValueApiCase? ValueApi { get; }
    /// <summary>
    /// Gets the external type api.
    /// </summary>
    public ConformanceExternalTypeApiCase? ExternalTypeApi { get; }
    /// <summary>
    /// Gets the binary fixture.
    /// </summary>
    public ConformanceBinaryFixture? BinaryFixture { get; }
    /// <summary>
    /// Gets the performance.
    /// </summary>
    public ConformancePerformanceWorkload? Performance { get; }
    /// <summary>
    /// Gets the expected assembler.
    /// </summary>
    public string? ExpectedAssembler { get; }
    /// <summary>
    /// Gets the metadata block range.
    /// </summary>
    public ConformanceSourceRange MetadataBlockRange { get; }
    /// <summary>
    /// Gets the expectation block range.
    /// </summary>
    public ConformanceSourceRange? ExpectationBlockRange { get; }
    /// <summary>
    /// Gets the steps table range.
    /// </summary>
    public ConformanceSourceRange? StepsTableRange { get; }
    /// <summary>
    /// Gets the assembler block range.
    /// </summary>
    public ConformanceSourceRange? AssemblerBlockRange { get; }
    /// <summary>
    /// Gets the assembler payload range.
    /// </summary>
    public ConformanceSourceRange? AssemblerPayloadRange { get; }
    /// <summary>
    /// Gets the range.
    /// </summary>
    public ConformanceSourceRange Range { get; }
}

/// <summary>
/// Represents a conformance capability requirements.
/// </summary>
public sealed class ConformanceCapabilityRequirements
{
    internal ConformanceCapabilityRequirements(IReadOnlyList<string> core, IReadOnlyList<string> optional)
    {
        Core = ConformanceDocument.Copy(core);
        Optional = ConformanceDocument.Copy(optional);
    }

    /// <summary>
    /// Gets the core.
    /// </summary>
    public IReadOnlyList<string> Core { get; }
    /// <summary>
    /// Gets the optional.
    /// </summary>
    public IReadOnlyList<string> Optional { get; }
}

/// <summary>
/// Represents a conformance compile options.
/// </summary>
public sealed class ConformanceCompileOptions
{
    internal ConformanceCompileOptions(IReadOnlyList<string> debugInfo, bool binaryRoundTrip)
    {
        DebugInfo = ConformanceDocument.Copy(debugInfo);
        BinaryRoundTrip = binaryRoundTrip;
    }

    /// <summary>
    /// Gets the debug info.
    /// </summary>
    public IReadOnlyList<string> DebugInfo { get; }
    /// <summary>
    /// Gets the binary round trip.
    /// </summary>
    public bool BinaryRoundTrip { get; }
}

/// <summary>
/// Represents a conformance binary fixture.
/// </summary>
public sealed class ConformanceBinaryFixture
{
    internal ConformanceBinaryFixture(string id, string resourceId, string relativePath, string sha256, string compilerId, string compilerVersion, ulong programVersion, bool compareCompiledRuntime, string? derivation)
    {
        Id = id;
        ResourceId = resourceId;
        RelativePath = relativePath;
        Sha256 = sha256;
        CompilerId = compilerId;
        CompilerVersion = compilerVersion;
        ProgramVersion = programVersion;
        CompareCompiledRuntime = compareCompiledRuntime;
        Derivation = derivation;
    }

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the resource id.
    /// </summary>
    public string ResourceId { get; }
    /// <summary>
    /// Gets the relative path.
    /// </summary>
    public string RelativePath { get; }
    /// <summary>
    /// Gets the sha256.
    /// </summary>
    public string Sha256 { get; }
    /// <summary>
    /// Gets the compiler id.
    /// </summary>
    public string CompilerId { get; }
    /// <summary>
    /// Gets the compiler version.
    /// </summary>
    public string CompilerVersion { get; }
    /// <summary>
    /// Gets the program version.
    /// </summary>
    public ulong ProgramVersion { get; }
    /// <summary>
    /// Gets the compare compiled runtime.
    /// </summary>
    public bool CompareCompiledRuntime { get; }
    /// <summary>
    /// Gets the derivation.
    /// </summary>
    public string? Derivation { get; }
}

/// <summary>
/// Represents a conformance runtime limits.
/// </summary>
public sealed class ConformanceRuntimeLimits
{
    internal ConformanceRuntimeLimits(IReadOnlyDictionary<string, ulong> values)
        => Values = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(new Dictionary<string, ulong>(values, StringComparer.Ordinal));

    /// <summary>
    /// Gets the values.
    /// </summary>
    public IReadOnlyDictionary<string, ulong> Values { get; }
}

/// <summary>
/// Represents a conformance comparison options.
/// </summary>
public sealed class ConformanceComparisonOptions
{
    internal ConformanceComparisonOptions(ConformanceBinary64ComparisonMode mode, ulong maxUlps)
    {
        Binary64Mode = mode;
        MaxUlps = maxUlps;
    }

    /// <summary>
    /// Gets the binary64 mode.
    /// </summary>
    public ConformanceBinary64ComparisonMode Binary64Mode { get; }
    /// <summary>
    /// Gets the max ulps.
    /// </summary>
    public ulong MaxUlps { get; }
}

/// <summary>
/// Represents a conformance random configuration.
/// </summary>
public sealed class ConformanceRandomConfiguration
{
    internal ConformanceRandomConfiguration(long? seed, IReadOnlyList<string> sequence)
    {
        Seed = seed;
        Sequence = ConformanceDocument.Copy(sequence);
    }

    /// <summary>
    /// Gets the seed.
    /// </summary>
    public long? Seed { get; }
    /// <summary>
    /// Gets the sequence.
    /// </summary>
    public IReadOnlyList<string> Sequence { get; }
}

/// <summary>
/// Represents a conformance source input.
/// </summary>
public sealed class ConformanceSourceInput
{
    internal ConformanceSourceInput(string name, string programId, string text, ConformanceSourceRange blockRange, ConformanceSourceRange payloadRange)
    {
        Name = name;
        ProgramId = programId;
        Text = text;
        BlockRange = blockRange;
        PayloadRange = payloadRange;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the program id.
    /// </summary>
    public string ProgramId { get; }
    /// <summary>
    /// Gets the text.
    /// </summary>
    public string Text { get; }
    /// <summary>
    /// Gets the block range.
    /// </summary>
    public ConformanceSourceRange BlockRange { get; }
    /// <summary>
    /// Gets the payload range.
    /// </summary>
    public ConformanceSourceRange PayloadRange { get; }
}

/// <summary>
/// Represents a conformance native handler.
/// </summary>
public sealed class ConformanceNativeHandler
{
    internal ConformanceNativeHandler(
        string id,
        string message,
        IReadOnlyList<string> parameters,
        bool messageNameOnly,
        int priority,
        bool initiallySubscribed,
        bool throws,
        IReadOnlyList<ConformanceNativeAction> actions,
        IReadOnlyList<ConformanceNativeEmit> emits)
    {
        Id = id;
        Message = message;
        Parameters = ConformanceDocument.Copy(parameters);
        MessageNameOnly = messageNameOnly;
        Priority = priority;
        InitiallySubscribed = initiallySubscribed;
        Throws = throws;
        Actions = ConformanceDocument.Copy(actions);
        Emits = ConformanceDocument.Copy(emits);
    }

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the message.
    /// </summary>
    public string Message { get; }
    /// <summary>
    /// Gets the parameters.
    /// </summary>
    public IReadOnlyList<string> Parameters { get; }
    /// <summary>
    /// Gets the message name only.
    /// </summary>
    public bool MessageNameOnly { get; }
    /// <summary>
    /// Gets the priority.
    /// </summary>
    public int Priority { get; }
    /// <summary>
    /// Gets the initially subscribed.
    /// </summary>
    public bool InitiallySubscribed { get; }
    /// <summary>
    /// Gets the throws.
    /// </summary>
    public bool Throws { get; }
    /// <summary>
    /// Gets the actions.
    /// </summary>
    public IReadOnlyList<ConformanceNativeAction> Actions { get; }
    /// <summary>
    /// Gets the emits.
    /// </summary>
    public IReadOnlyList<ConformanceNativeEmit> Emits { get; }
}

/// <summary>
/// Represents a conformance native action.
/// </summary>
public sealed class ConformanceNativeAction
{
    internal ConformanceNativeAction(ConformanceNativeActionKind kind, string target, bool? expectedResult)
    {
        Kind = kind;
        Target = target;
        ExpectedResult = expectedResult;
    }

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public ConformanceNativeActionKind Kind { get; }
    /// <summary>
    /// Gets the target.
    /// </summary>
    public string Target { get; }
    /// <summary>
    /// Gets the expected result.
    /// </summary>
    public bool? ExpectedResult { get; }
}

/// <summary>
/// Represents a conformance native emit.
/// </summary>
public sealed class ConformanceNativeEmit
{
    internal ConformanceNativeEmit(string name, bool forwardArguments, IReadOnlyList<ConformanceArgument> arguments)
    {
        Name = name;
        ForwardArguments = forwardArguments;
        Arguments = ConformanceDocument.Copy(arguments);
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the forward arguments.
    /// </summary>
    public bool ForwardArguments { get; }
    /// <summary>
    /// Gets the arguments.
    /// </summary>
    public IReadOnlyList<ConformanceArgument> Arguments { get; }
}

/// <summary>
/// Represents a conformance step.
/// </summary>
public sealed class ConformanceStep
{
    internal ConformanceStep(string id, string receive, ConformancePumpMode pump, uint? budget, IReadOnlyList<ConformanceNativeAction> actions, ConformanceStepExpectation expectation, ConformanceSourceRange range)
    {
        Id = id;
        Receive = receive;
        Pump = pump;
        Budget = budget;
        Actions = ConformanceDocument.Copy(actions);
        Expectation = expectation;
        Range = range;
    }

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the receive.
    /// </summary>
    public string Receive { get; }
    /// <summary>
    /// Gets the pump.
    /// </summary>
    public ConformancePumpMode Pump { get; }
    /// <summary>
    /// Gets the budget.
    /// </summary>
    public uint? Budget { get; }
    /// <summary>
    /// Gets the actions.
    /// </summary>
    public IReadOnlyList<ConformanceNativeAction> Actions { get; }
    /// <summary>
    /// Gets the expectation.
    /// </summary>
    public ConformanceStepExpectation Expectation { get; }
    /// <summary>
    /// Gets the range.
    /// </summary>
    public ConformanceSourceRange Range { get; }
}

/// <summary>
/// Represents a conformance step expectation.
/// </summary>
public sealed class ConformanceStepExpectation
{
    internal ConformanceStepExpectation(ConformanceMessage input, bool accepted, IReadOnlyList<ConformanceMessage> local, IReadOnlyList<ConformanceMessage> outbound, bool? paused, ConformanceObservationExpectation observations)
    {
        Input = input;
        Accepted = accepted;
        Local = ConformanceDocument.Copy(local);
        Outbound = ConformanceDocument.Copy(outbound);
        Paused = paused;
        Observations = observations;
    }

    /// <summary>
    /// Gets the input.
    /// </summary>
    public ConformanceMessage Input { get; }
    /// <summary>
    /// Gets the accepted.
    /// </summary>
    public bool Accepted { get; }
    /// <summary>
    /// Gets the local.
    /// </summary>
    public IReadOnlyList<ConformanceMessage> Local { get; }
    /// <summary>
    /// Gets the outbound.
    /// </summary>
    public IReadOnlyList<ConformanceMessage> Outbound { get; }
    /// <summary>
    /// Gets the paused.
    /// </summary>
    public bool? Paused { get; }
    /// <summary>
    /// Gets the observations.
    /// </summary>
    public ConformanceObservationExpectation Observations { get; }
}

/// <summary>
/// Represents a conformance observation expectation.
/// </summary>
public sealed class ConformanceObservationExpectation
{
    internal ConformanceObservationExpectation(
        IReadOnlyList<ConformanceRuntimeLimitExpectation> included,
        IReadOnlyList<ConformanceRuntimeLimitExpectation> excluded,
        IReadOnlyList<ConformanceExpectedDiagnostic> diagnostics,
        bool traceSpecified,
        IReadOnlyList<ConformanceObserverEventExpectation> trace)
    {
        IncludedRuntimeLimits = ConformanceDocument.Copy(included);
        ExcludedRuntimeLimits = ConformanceDocument.Copy(excluded);
        Diagnostics = ConformanceDocument.Copy(diagnostics);
        TraceSpecified = traceSpecified;
        Trace = ConformanceDocument.Copy(trace);
    }

    /// <summary>
    /// Gets the included runtime limits.
    /// </summary>
    public IReadOnlyList<ConformanceRuntimeLimitExpectation> IncludedRuntimeLimits { get; }
    /// <summary>
    /// Gets the excluded runtime limits.
    /// </summary>
    public IReadOnlyList<ConformanceRuntimeLimitExpectation> ExcludedRuntimeLimits { get; }
    /// <summary>
    /// Gets the diagnostics.
    /// </summary>
    public IReadOnlyList<ConformanceExpectedDiagnostic> Diagnostics { get; }
    /// <summary>
    /// Gets the trace specified.
    /// </summary>
    public bool TraceSpecified { get; }
    /// <summary>
    /// Gets the trace.
    /// </summary>
    public IReadOnlyList<ConformanceObserverEventExpectation> Trace { get; }
}

/// <summary>
/// Represents a conformance observer event expectation.
/// </summary>
public sealed class ConformanceObserverEventExpectation
{
    internal ConformanceObserverEventExpectation(
        ConformanceObserverEventKind kind,
        ConformanceMessage? message,
        string? signatureId,
        bool? accepted,
        ConformancePublishResultExpectation? publishResult,
        ConformanceRuntimeLimitExpectation? runtimeLimit,
        ConformanceExpectedDiagnostic? diagnostic)
    {
        Kind = kind;
        Message = message;
        SignatureId = signatureId;
        Accepted = accepted;
        PublishResult = publishResult;
        RuntimeLimit = runtimeLimit;
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public ConformanceObserverEventKind Kind { get; }
    /// <summary>
    /// Gets the message.
    /// </summary>
    public ConformanceMessage? Message { get; }
    /// <summary>
    /// Gets the signature id.
    /// </summary>
    public string? SignatureId { get; }
    /// <summary>
    /// Gets the accepted.
    /// </summary>
    public bool? Accepted { get; }
    /// <summary>
    /// Gets the publish result.
    /// </summary>
    public ConformancePublishResultExpectation? PublishResult { get; }
    /// <summary>
    /// Gets the runtime limit.
    /// </summary>
    public ConformanceRuntimeLimitExpectation? RuntimeLimit { get; }
    /// <summary>
    /// Gets the diagnostic.
    /// </summary>
    public ConformanceExpectedDiagnostic? Diagnostic { get; }
}

/// <summary>
/// Represents a conformance publish result expectation.
/// </summary>
public sealed class ConformancePublishResultExpectation
{
    internal ConformancePublishResultExpectation(bool localAccepted, bool outboundAttempted, bool outboundAccepted, bool anyAccepted)
    {
        LocalAccepted = localAccepted;
        OutboundAttempted = outboundAttempted;
        OutboundAccepted = outboundAccepted;
        AnyAccepted = anyAccepted;
    }

    /// <summary>
    /// Gets the local accepted.
    /// </summary>
    public bool LocalAccepted { get; }
    /// <summary>
    /// Gets the outbound attempted.
    /// </summary>
    public bool OutboundAttempted { get; }
    /// <summary>
    /// Gets the outbound accepted.
    /// </summary>
    public bool OutboundAccepted { get; }
    /// <summary>
    /// Gets the any accepted.
    /// </summary>
    public bool AnyAccepted { get; }
}

/// <summary>
/// Represents a conformance runtime limit expectation.
/// </summary>
public sealed class ConformanceRuntimeLimitExpectation
{
    internal ConformanceRuntimeLimitExpectation(bool any, string? name, string? detailContains, ulong? limit)
    {
        Any = any;
        Name = name;
        DetailContains = detailContains;
        Limit = limit;
    }

    /// <summary>
    /// Gets the any.
    /// </summary>
    public bool Any { get; }
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string? Name { get; }
    /// <summary>
    /// Gets the detail contains.
    /// </summary>
    public string? DetailContains { get; }
    /// <summary>
    /// Gets the limit.
    /// </summary>
    public ulong? Limit { get; }
}

/// <summary>
/// Represents a conformance expected diagnostic.
/// </summary>
public sealed class ConformanceExpectedDiagnostic
{
    internal ConformanceExpectedDiagnostic(string phase, string code, string? symbol, string? symbolKind, string? sourceName, uint? line, uint? column, uint? endLine, uint? endColumn, string? programName, string? handlerName)
    {
        Phase = phase;
        Code = code;
        Symbol = symbol;
        SymbolKind = symbolKind;
        SourceName = sourceName;
        Line = line;
        Column = column;
        EndLine = endLine;
        EndColumn = endColumn;
        ProgramName = programName;
        HandlerName = handlerName;
    }

    /// <summary>
    /// Gets the phase.
    /// </summary>
    public string Phase { get; }
    /// <summary>
    /// Gets the code.
    /// </summary>
    public string Code { get; }
    /// <summary>
    /// Gets the symbol.
    /// </summary>
    public string? Symbol { get; }
    /// <summary>
    /// Gets the symbol kind.
    /// </summary>
    public string? SymbolKind { get; }
    /// <summary>
    /// Gets the source name.
    /// </summary>
    public string? SourceName { get; }
    /// <summary>
    /// Gets the line.
    /// </summary>
    public uint? Line { get; }
    /// <summary>
    /// Gets the column.
    /// </summary>
    public uint? Column { get; }
    /// <summary>
    /// Gets the end line.
    /// </summary>
    public uint? EndLine { get; }
    /// <summary>
    /// Gets the end column.
    /// </summary>
    public uint? EndColumn { get; }
    /// <summary>
    /// Gets the program name.
    /// </summary>
    public string? ProgramName { get; }
    /// <summary>
    /// Gets the handler name.
    /// </summary>
    public string? HandlerName { get; }
}

/// <summary>
/// Represents a conformance message.
/// </summary>
public sealed class ConformanceMessage
{
    internal ConformanceMessage(string name, IReadOnlyList<string> tags, IReadOnlyList<ConformanceArgument> arguments)
    {
        Name = name;
        Tags = ConformanceDocument.Copy(tags);
        Arguments = ConformanceDocument.Copy(arguments);
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }
    /// <summary>
    /// Gets the arguments.
    /// </summary>
    public IReadOnlyList<ConformanceArgument> Arguments { get; }
}

/// <summary>
/// Represents a conformance argument.
/// </summary>
public sealed class ConformanceArgument
{
    internal ConformanceArgument(string name, ConformanceValue value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the value.
    /// </summary>
    public ConformanceValue Value { get; }
}

/// <summary>
/// Represents a conformance value.
/// </summary>
public sealed class ConformanceValue
{
    internal ConformanceValue(string type, IReadOnlyDictionary<string, string> scalars, IReadOnlyList<ConformanceValue> items, IReadOnlyList<ConformanceValueEntry> entries, IReadOnlyList<int> rolls, ConformanceMessage? message)
    {
        Type = type;
        Value = Read(scalars, "value");
        Unit = Read(scalars, "unit");
        X = Read(scalars, "x");
        Y = Read(scalars, "y");
        Z = Read(scalars, "z");
        From = Read(scalars, "from");
        To = Read(scalars, "to");
        Step = Read(scalars, "step");
        RangeKind = Read(scalars, "rangeKind");
        Items = ConformanceDocument.Copy(items);
        Entries = ConformanceDocument.Copy(entries);
        Rolls = ConformanceDocument.Copy(rolls);
        Message = message;
    }

    /// <summary>
    /// Gets the type.
    /// </summary>
    public string Type { get; }
    /// <summary>
    /// Gets the value.
    /// </summary>
    public string? Value { get; }
    /// <summary>
    /// Gets the unit.
    /// </summary>
    public string? Unit { get; }
    /// <summary>
    /// Gets the x.
    /// </summary>
    public string? X { get; }
    /// <summary>
    /// Gets the y.
    /// </summary>
    public string? Y { get; }
    /// <summary>
    /// Gets the z.
    /// </summary>
    public string? Z { get; }
    /// <summary>
    /// Gets the from.
    /// </summary>
    public string? From { get; }
    /// <summary>
    /// Gets the to.
    /// </summary>
    public string? To { get; }
    /// <summary>
    /// Gets the step.
    /// </summary>
    public string? Step { get; }
    /// <summary>
    /// Gets the range kind.
    /// </summary>
    public string? RangeKind { get; }
    /// <summary>
    /// Gets the items.
    /// </summary>
    public IReadOnlyList<ConformanceValue> Items { get; }
    /// <summary>
    /// Gets the entries.
    /// </summary>
    public IReadOnlyList<ConformanceValueEntry> Entries { get; }
    /// <summary>
    /// Gets the rolls.
    /// </summary>
    public IReadOnlyList<int> Rolls { get; }
    /// <summary>
    /// Gets the message.
    /// </summary>
    public ConformanceMessage? Message { get; }

    private static string? Read(IReadOnlyDictionary<string, string> values, string name)
        => values.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// Represents a conformance value entry.
/// </summary>
public sealed class ConformanceValueEntry
{
    internal ConformanceValueEntry(string key, ConformanceValue value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Gets the key.
    /// </summary>
    public string Key { get; }
    /// <summary>
    /// Gets the value.
    /// </summary>
    public ConformanceValue Value { get; }
}

/// <summary>
/// Represents a conformance expectation.
/// </summary>
public sealed class ConformanceExpectation
{
    internal ConformanceExpectation(
        ConformanceChannelExpectation initialization,
        ConformanceExpectedDiagnostic? error,
        ConformanceMessageApiExpectation? messageApi,
        ConformanceValueApiExpectation? valueApi,
        ConformanceExternalTypeApiExpectation? externalTypeApi,
        ConformanceCompileMetadataExpectation? metadata,
        ConformanceOpcodeExpectation? opcodes,
        ConformancePerformanceExpectation? performance,
        ConformanceBinaryExpectation? binary)
    {
        Initialization = initialization;
        Error = error;
        MessageApi = messageApi;
        ValueApi = valueApi;
        ExternalTypeApi = externalTypeApi;
        Metadata = metadata;
        Opcodes = opcodes;
        Performance = performance;
        Binary = binary;
    }

    /// <summary>
    /// Gets the initialization.
    /// </summary>
    public ConformanceChannelExpectation Initialization { get; }
    /// <summary>
    /// Gets the error.
    /// </summary>
    public ConformanceExpectedDiagnostic? Error { get; }
    /// <summary>
    /// Gets the message api.
    /// </summary>
    public ConformanceMessageApiExpectation? MessageApi { get; }
    /// <summary>
    /// Gets the value api.
    /// </summary>
    public ConformanceValueApiExpectation? ValueApi { get; }
    /// <summary>
    /// Gets the external type api.
    /// </summary>
    public ConformanceExternalTypeApiExpectation? ExternalTypeApi { get; }
    /// <summary>
    /// Gets the metadata.
    /// </summary>
    public ConformanceCompileMetadataExpectation? Metadata { get; }
    /// <summary>
    /// Gets the opcodes.
    /// </summary>
    public ConformanceOpcodeExpectation? Opcodes { get; }
    /// <summary>
    /// Gets the performance.
    /// </summary>
    public ConformancePerformanceExpectation? Performance { get; }
    /// <summary>
    /// Gets the binary.
    /// </summary>
    public ConformanceBinaryExpectation? Binary { get; }
}

/// <summary>
/// Represents a conformance binary expectation.
/// </summary>
public sealed class ConformanceBinaryExpectation
{
    internal ConformanceBinaryExpectation(
        ConformanceBinaryOutcome outcome,
        string? errorCode,
        long? byteOffset,
        ushort? sectionType,
        int? entryIndex,
        bool? rewriteByteExact,
        string? rewriteSha256,
        string? moduleName,
        uint? requiredRegisterCount,
        uint? requiredCallStackDepth,
        uint? opaqueSectionCount)
    {
        Outcome = outcome;
        ErrorCode = errorCode;
        ByteOffset = byteOffset;
        SectionType = sectionType;
        EntryIndex = entryIndex;
        RewriteByteExact = rewriteByteExact;
        RewriteSha256 = rewriteSha256;
        ModuleName = moduleName;
        RequiredRegisterCount = requiredRegisterCount;
        RequiredCallStackDepth = requiredCallStackDepth;
        OpaqueSectionCount = opaqueSectionCount;
    }

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public ConformanceBinaryOutcome Outcome { get; }
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string? ErrorCode { get; }
    /// <summary>
    /// Gets the byte offset.
    /// </summary>
    public long? ByteOffset { get; }
    /// <summary>
    /// Gets the section type.
    /// </summary>
    public ushort? SectionType { get; }
    /// <summary>
    /// Gets the entry index.
    /// </summary>
    public int? EntryIndex { get; }
    /// <summary>
    /// Gets the rewrite byte exact.
    /// </summary>
    public bool? RewriteByteExact { get; }
    /// <summary>
    /// Gets the rewrite sha256.
    /// </summary>
    public string? RewriteSha256 { get; }
    /// <summary>
    /// Gets the module name.
    /// </summary>
    public string? ModuleName { get; }
    /// <summary>
    /// Gets the required register count.
    /// </summary>
    public uint? RequiredRegisterCount { get; }
    /// <summary>
    /// Gets the required call stack depth.
    /// </summary>
    public uint? RequiredCallStackDepth { get; }
    /// <summary>
    /// Gets the opaque section count.
    /// </summary>
    public uint? OpaqueSectionCount { get; }
}

/// <summary>
/// Represents a conformance channel expectation.
/// </summary>
public sealed class ConformanceChannelExpectation
{
    internal ConformanceChannelExpectation(IReadOnlyList<ConformanceMessage> local, IReadOnlyList<ConformanceMessage> outbound, ConformanceObservationExpectation observations)
    {
        Local = ConformanceDocument.Copy(local);
        Outbound = ConformanceDocument.Copy(outbound);
        Observations = observations;
    }

    /// <summary>
    /// Gets the local.
    /// </summary>
    public IReadOnlyList<ConformanceMessage> Local { get; }
    /// <summary>
    /// Gets the outbound.
    /// </summary>
    public IReadOnlyList<ConformanceMessage> Outbound { get; }
    /// <summary>
    /// Gets the observations.
    /// </summary>
    public ConformanceObservationExpectation Observations { get; }
}

/// <summary>
/// Represents a conformance message api case.
/// </summary>
public sealed class ConformanceMessageApiCase
{
    internal ConformanceMessageApiCase(
        string signatureName,
        IReadOnlyList<string> parameters,
        ConformanceMessage message,
        bool argumentsWereMapping,
        IReadOnlyList<ConformanceValueEntry> unorderedArguments,
        ConformanceMessageSignatureDefinition? compareSignature,
        ConformanceMessage? compareMessage,
        ConformanceMessageSignatureDefinition? compareHandler,
        IReadOnlyList<ConformanceValue> createArguments)
    {
        SignatureName = signatureName;
        Parameters = ConformanceDocument.Copy(parameters);
        Message = message;
        ArgumentsWereMapping = argumentsWereMapping;
        UnorderedArguments = ConformanceDocument.Copy(unorderedArguments);
        CompareSignature = compareSignature;
        CompareMessage = compareMessage;
        CompareHandler = compareHandler;
        CreateArguments = ConformanceDocument.Copy(createArguments);
    }

    /// <summary>
    /// Gets the signature name.
    /// </summary>
    public string SignatureName { get; }
    /// <summary>
    /// Gets the parameters.
    /// </summary>
    public IReadOnlyList<string> Parameters { get; }
    /// <summary>
    /// Gets the message.
    /// </summary>
    public ConformanceMessage Message { get; }
    /// <summary>
    /// Gets the arguments were mapping.
    /// </summary>
    public bool ArgumentsWereMapping { get; }
    /// <summary>
    /// Gets the unordered arguments.
    /// </summary>
    public IReadOnlyList<ConformanceValueEntry> UnorderedArguments { get; }
    /// <summary>
    /// Gets the compare signature.
    /// </summary>
    public ConformanceMessageSignatureDefinition? CompareSignature { get; }
    /// <summary>
    /// Gets the compare message.
    /// </summary>
    public ConformanceMessage? CompareMessage { get; }
    /// <summary>
    /// Gets the compare handler.
    /// </summary>
    public ConformanceMessageSignatureDefinition? CompareHandler { get; }
    /// <summary>
    /// Gets the create arguments.
    /// </summary>
    public IReadOnlyList<ConformanceValue> CreateArguments { get; }
}

/// <summary>
/// Represents a conformance message signature definition.
/// </summary>
public sealed class ConformanceMessageSignatureDefinition
{
    internal ConformanceMessageSignatureDefinition(string name, IReadOnlyList<string> parameters)
    {
        Name = name;
        Parameters = ConformanceDocument.Copy(parameters);
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the parameters.
    /// </summary>
    public IReadOnlyList<string> Parameters { get; }
}

/// <summary>
/// Represents a conformance message api expectation.
/// </summary>
public sealed class ConformanceMessageApiExpectation
{
    internal ConformanceMessageApiExpectation(
        string? name,
        string? signatureId,
        string? messageSignatureId,
        bool? matches,
        uint? argumentCount,
        bool? signatureEquals,
        bool? signatureHashEquals,
        bool? messageEquals,
        bool? messageHashEquals,
        bool? handlerEquals,
        bool? handlerHashEquals,
        string? createdMessageSignatureId,
        string? error)
    {
        Name = name;
        SignatureId = signatureId;
        MessageSignatureId = messageSignatureId;
        Matches = matches;
        ArgumentCount = argumentCount;
        SignatureEquals = signatureEquals;
        SignatureHashEquals = signatureHashEquals;
        MessageEquals = messageEquals;
        MessageHashEquals = messageHashEquals;
        HandlerEquals = handlerEquals;
        HandlerHashEquals = handlerHashEquals;
        CreatedMessageSignatureId = createdMessageSignatureId;
        Error = error;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string? Name { get; }
    /// <summary>
    /// Gets the signature id.
    /// </summary>
    public string? SignatureId { get; }
    /// <summary>
    /// Gets the message signature id.
    /// </summary>
    public string? MessageSignatureId { get; }
    /// <summary>
    /// Gets the matches.
    /// </summary>
    public bool? Matches { get; }
    /// <summary>
    /// Gets the argument count.
    /// </summary>
    public uint? ArgumentCount { get; }
    /// <summary>
    /// Gets the signature equals.
    /// </summary>
    public bool? SignatureEquals { get; }
    /// <summary>
    /// Gets the signature hash equals.
    /// </summary>
    public bool? SignatureHashEquals { get; }
    /// <summary>
    /// Gets the message equals.
    /// </summary>
    public bool? MessageEquals { get; }
    /// <summary>
    /// Gets the message hash equals.
    /// </summary>
    public bool? MessageHashEquals { get; }
    /// <summary>
    /// Gets the handler equals.
    /// </summary>
    public bool? HandlerEquals { get; }
    /// <summary>
    /// Gets the handler hash equals.
    /// </summary>
    public bool? HandlerHashEquals { get; }
    /// <summary>
    /// Gets the created message signature id.
    /// </summary>
    public string? CreatedMessageSignatureId { get; }
    /// <summary>
    /// Gets the error.
    /// </summary>
    public string? Error { get; }
}

/// <summary>
/// Represents a conformance value api case.
/// </summary>
public sealed class ConformanceValueApiCase
{
    internal ConformanceValueApiCase(ConformanceValue value, ConformanceValue? equalTo, ConformanceValue? notEqualTo, bool mutateSourceAfterCreate)
    {
        Value = value;
        EqualTo = equalTo;
        NotEqualTo = notEqualTo;
        MutateSourceAfterCreate = mutateSourceAfterCreate;
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    public ConformanceValue Value { get; }
    /// <summary>
    /// Gets the equal to.
    /// </summary>
    public ConformanceValue? EqualTo { get; }
    /// <summary>
    /// Gets the not equal to.
    /// </summary>
    public ConformanceValue? NotEqualTo { get; }
    /// <summary>
    /// Gets the mutate source after create.
    /// </summary>
    public bool MutateSourceAfterCreate { get; }
}

/// <summary>
/// Represents a conformance value api expectation.
/// </summary>
public sealed class ConformanceValueApiExpectation
{
    internal ConformanceValueApiExpectation(ConformanceValue normalized, bool? isNumeric, bool? hasValue, bool? isNothing, bool? hasUnit, bool? asBoolean, uint? length, string? customTypeName, bool? equal, bool? equalHash, bool? notEqual)
    {
        Normalized = normalized;
        IsNumeric = isNumeric;
        HasValue = hasValue;
        IsNothing = isNothing;
        HasUnit = hasUnit;
        AsBoolean = asBoolean;
        Length = length;
        CustomTypeName = customTypeName;
        Equal = equal;
        EqualHash = equalHash;
        NotEqual = notEqual;
    }

    /// <summary>
    /// Gets the normalized.
    /// </summary>
    public ConformanceValue Normalized { get; }
    /// <summary>
    /// Gets a value indicating whether is numeric.
    /// </summary>
    public bool? IsNumeric { get; }
    /// <summary>
    /// Gets a value indicating whether has value.
    /// </summary>
    public bool? HasValue { get; }
    /// <summary>
    /// Gets a value indicating whether is nothing.
    /// </summary>
    public bool? IsNothing { get; }
    /// <summary>
    /// Gets a value indicating whether has unit.
    /// </summary>
    public bool? HasUnit { get; }
    /// <summary>
    /// Gets the as boolean.
    /// </summary>
    public bool? AsBoolean { get; }
    /// <summary>
    /// Gets the length.
    /// </summary>
    public uint? Length { get; }
    /// <summary>
    /// Gets the custom type name.
    /// </summary>
    public string? CustomTypeName { get; }
    /// <summary>
    /// Gets the equal.
    /// </summary>
    public bool? Equal { get; }
    /// <summary>
    /// Gets the equal hash.
    /// </summary>
    public bool? EqualHash { get; }
    /// <summary>
    /// Gets the not equal.
    /// </summary>
    public bool? NotEqual { get; }
}

/// <summary>
/// Represents a conformance external type api case.
/// </summary>
public sealed class ConformanceExternalTypeApiCase
{
    internal ConformanceExternalTypeApiCase(IReadOnlyList<string> typeNames)
        => TypeNames = ConformanceDocument.Copy(typeNames);

    /// <summary>
    /// Gets the type names.
    /// </summary>
    public IReadOnlyList<string> TypeNames { get; }
}

/// <summary>
/// Represents a conformance external type api expectation.
/// </summary>
public sealed class ConformanceExternalTypeApiExpectation
{
    internal ConformanceExternalTypeApiExpectation(uint? typeCount, string? error)
    {
        TypeCount = typeCount;
        Error = error;
    }

    /// <summary>
    /// Gets the type count.
    /// </summary>
    public uint? TypeCount { get; }
    /// <summary>
    /// Gets the error.
    /// </summary>
    public string? Error { get; }
}

/// <summary>
/// Represents a conformance compile metadata expectation.
/// </summary>
public sealed class ConformanceCompileMetadataExpectation
{
    internal ConformanceCompileMetadataExpectation(
        IReadOnlyList<ConformanceMessageDefinitionExpectation> messageDefinitions,
        ConformanceProgramResourceExpectation? programResources,
        IReadOnlyList<ConformanceHandlerResourceExpectation> handlerResources)
    {
        MessageDefinitions = ConformanceDocument.Copy(messageDefinitions);
        ProgramResources = programResources;
        HandlerResources = ConformanceDocument.Copy(handlerResources);
    }

    /// <summary>
    /// Gets the message definitions.
    /// </summary>
    public IReadOnlyList<ConformanceMessageDefinitionExpectation> MessageDefinitions { get; }
    /// <summary>
    /// Gets the program resources.
    /// </summary>
    public ConformanceProgramResourceExpectation? ProgramResources { get; }
    /// <summary>
    /// Gets the handler resources.
    /// </summary>
    public IReadOnlyList<ConformanceHandlerResourceExpectation> HandlerResources { get; }
}

/// <summary>
/// Represents a conformance message definition expectation.
/// </summary>
public sealed class ConformanceMessageDefinitionExpectation
{
    internal ConformanceMessageDefinitionExpectation(string name, uint count, IReadOnlyList<string> signatureIds)
    {
        Name = name;
        Count = count;
        SignatureIds = ConformanceDocument.Copy(signatureIds);
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the count.
    /// </summary>
    public uint Count { get; }
    /// <summary>
    /// Gets the signature ids.
    /// </summary>
    public IReadOnlyList<string> SignatureIds { get; }
}

/// <summary>
/// Represents a conformance program resource expectation.
/// </summary>
public sealed class ConformanceProgramResourceExpectation
{
    internal ConformanceProgramResourceExpectation(uint? requiredRegisterCount, uint? requiredCallStackDepth)
    {
        RequiredRegisterCount = requiredRegisterCount;
        RequiredCallStackDepth = requiredCallStackDepth;
    }

    /// <summary>
    /// Gets the required register count.
    /// </summary>
    public uint? RequiredRegisterCount { get; }
    /// <summary>
    /// Gets the required call stack depth.
    /// </summary>
    public uint? RequiredCallStackDepth { get; }
}

/// <summary>
/// Represents a conformance handler resource expectation.
/// </summary>
public sealed class ConformanceHandlerResourceExpectation
{
    internal ConformanceHandlerResourceExpectation(string name, string? signatureId, uint? requiredRegisterCount, uint? requiredCallStackDepth)
    {
        Name = name;
        SignatureId = signatureId;
        RequiredRegisterCount = requiredRegisterCount;
        RequiredCallStackDepth = requiredCallStackDepth;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the signature id.
    /// </summary>
    public string? SignatureId { get; }
    /// <summary>
    /// Gets the required register count.
    /// </summary>
    public uint? RequiredRegisterCount { get; }
    /// <summary>
    /// Gets the required call stack depth.
    /// </summary>
    public uint? RequiredCallStackDepth { get; }
}

/// <summary>
/// Represents a conformance opcode expectation.
/// </summary>
public sealed class ConformanceOpcodeExpectation
{
    internal ConformanceOpcodeExpectation(IReadOnlyList<string> contains, IReadOnlyList<string> excludes, IReadOnlyDictionary<string, ulong> counts, IReadOnlyDictionary<string, ulong> minimumCounts)
    {
        Contains = ConformanceDocument.Copy(contains);
        Excludes = ConformanceDocument.Copy(excludes);
        Counts = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(new Dictionary<string, ulong>(counts, StringComparer.Ordinal));
        MinimumCounts = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(new Dictionary<string, ulong>(minimumCounts, StringComparer.Ordinal));
    }

    /// <summary>
    /// Gets the contains.
    /// </summary>
    public IReadOnlyList<string> Contains { get; }
    /// <summary>
    /// Gets the excludes.
    /// </summary>
    public IReadOnlyList<string> Excludes { get; }
    /// <summary>
    /// Gets the counts.
    /// </summary>
    public IReadOnlyDictionary<string, ulong> Counts { get; }
    /// <summary>
    /// Gets the minimum counts.
    /// </summary>
    public IReadOnlyDictionary<string, ulong> MinimumCounts { get; }
}

/// <summary>
/// Represents a conformance performance workload.
/// </summary>
public sealed class ConformancePerformanceWorkload
{
    internal ConformancePerformanceWorkload(uint iterations, uint warmupIterations, uint compileWarmupIterations)
    {
        Iterations = iterations;
        WarmupIterations = warmupIterations;
        CompileWarmupIterations = compileWarmupIterations;
    }

    /// <summary>
    /// Gets the iterations.
    /// </summary>
    public uint Iterations { get; }
    /// <summary>
    /// Gets the warmup iterations.
    /// </summary>
    public uint WarmupIterations { get; }
    /// <summary>
    /// Gets the compile warmup iterations.
    /// </summary>
    public uint CompileWarmupIterations { get; }
}

/// <summary>
/// Represents a conformance performance expectation.
/// </summary>
public sealed class ConformancePerformanceExpectation
{
    internal ConformancePerformanceExpectation(IReadOnlyList<ConformancePerformanceProfile> profiles)
        => Profiles = ConformanceDocument.Copy(profiles);

    /// <summary>
    /// Gets the profiles.
    /// </summary>
    public IReadOnlyList<ConformancePerformanceProfile> Profiles { get; }
}

/// <summary>
/// Represents a conformance performance profile.
/// </summary>
public sealed class ConformancePerformanceProfile
{
    internal ConformancePerformanceProfile(string id, IReadOnlyList<ConformancePerformanceMetric> metrics)
    {
        Id = id;
        Metrics = ConformanceDocument.Copy(metrics);
    }

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the metrics.
    /// </summary>
    public IReadOnlyList<ConformancePerformanceMetric> Metrics { get; }
}

/// <summary>
/// Represents a conformance performance metric.
/// </summary>
public sealed class ConformancePerformanceMetric
{
    internal ConformancePerformanceMetric(string id, string reference, string? maximum, string? toleranceRelative, string? toleranceAbsolute, string unit, ConformanceSourceRange referenceRange)
    {
        Id = id;
        Reference = reference;
        Maximum = maximum;
        ToleranceRelative = toleranceRelative;
        ToleranceAbsolute = toleranceAbsolute;
        Unit = unit;
        ReferenceRange = referenceRange;
    }

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the reference.
    /// </summary>
    public string Reference { get; }
    /// <summary>
    /// Gets the maximum.
    /// </summary>
    public string? Maximum { get; }
    /// <summary>
    /// Gets the tolerance relative.
    /// </summary>
    public string? ToleranceRelative { get; }
    /// <summary>
    /// Gets the tolerance absolute.
    /// </summary>
    public string? ToleranceAbsolute { get; }
    /// <summary>
    /// Gets the unit.
    /// </summary>
    public string Unit { get; }
    /// <summary>
    /// Gets the reference range.
    /// </summary>
    public ConformanceSourceRange ReferenceRange { get; }
}
