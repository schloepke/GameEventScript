#pragma warning disable CS1591

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Conformance;

public enum ConformanceTestKind
{
    ScriptApi = 0,
    CompileError = 1,
    LoadError = 2,
    MessageApi = 3,
    CompileMetadata = 4,
    Bytecode = 5,
    Performance = 6,
    BytecodeSnapshot = 7,
    ValueApi = 8,
    ExternalTypeApi = 9,
    ProgramBinary = 10
}

public enum ConformanceBinaryOutcome
{
    Valid = 0,
    ReadError = 1,
    ValidationError = 2
}

public enum ConformanceTestLevel
{
    Atomic = 0,
    Scenario = 1
}

public enum ConformancePumpMode
{
    Completion = 0,
    Frames = 1,
    Enqueue = 2,
    Frame = 3
}

public enum ConformanceBinary64ComparisonMode
{
    Exact = 0,
    Ulp = 1
}

public enum ConformancePublishSinkMode
{
    Accept = 0,
    Absent = 1,
    Reject = 2,
    Throw = 3
}

public enum ConformanceExternalTypeRegistryMode
{
    Environment = 0,
    Absent = 1,
    Mismatch = 2
}

public enum ConformanceObserverEventKind
{
    Emit = 0,
    Publish = 1,
    DispatchStarted = 2,
    DispatchCompleted = 3,
    RuntimeLimit = 4,
    Diagnostic = 5
}

public enum ConformanceNativeActionKind
{
    LoadProgram = 0,
    DetachProgram = 1,
    SubscribeHandler = 2,
    UnsubscribeHandler = 3
}

public sealed class ConformanceSourceDocument
{
    internal ConformanceSourceDocument(byte[] utf8Bytes, bool hasByteOrderMark, string lineEnding)
    {
        Utf8Bytes = Array.AsReadOnly(utf8Bytes);
        HasByteOrderMark = hasByteOrderMark;
        LineEnding = lineEnding;
    }

    public IReadOnlyList<byte> Utf8Bytes { get; }
    public bool HasByteOrderMark { get; }
    public string LineEnding { get; }
}

public sealed class ConformanceDocument
{
    internal ConformanceDocument(
        int formatVersion,
        string suiteId,
        string title,
        IReadOnlyList<ConformanceCase> cases,
        ConformanceSourceDocument source,
        ConformanceSourceRange frontmatterRange)
    {
        FormatVersion = formatVersion;
        SuiteId = suiteId;
        Title = title;
        Cases = Copy(cases);
        Source = source;
        FrontmatterRange = frontmatterRange;
    }

    public int FormatVersion { get; }
    public string SuiteId { get; }
    public string Title { get; }
    public IReadOnlyList<ConformanceCase> Cases { get; }
    public ConformanceSourceDocument Source { get; }
    public ConformanceSourceRange FrontmatterRange { get; }

    internal static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        var copy = new T[source.Count];
        for (var index = 0; index < source.Count; index++) copy[index] = source[index];
        return Array.AsReadOnly(copy);
    }
}

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

    public string Id { get; }
    public string FullId { get; }
    public string SuiteId { get; }
    public string Title { get; }
    public ConformanceTestKind Kind { get; }
    public ConformanceTestLevel Level { get; }
    public IReadOnlyList<string> Categories { get; }
    public IReadOnlyList<string> Tags { get; }
    public ConformanceCapabilityRequirements Requires { get; }
    public ConformanceCompileOptions Compile { get; }
    public ConformanceRuntimeLimits RuntimeLimits { get; }
    public ConformanceComparisonOptions Comparison { get; }
    public ConformancePublishSinkMode PublishSink { get; }
    public ConformanceExternalTypeRegistryMode ExternalTypeRegistry { get; }
    public uint HostCount { get; }
    public IReadOnlyList<string> DeferredPrograms { get; }
    public ConformanceRandomConfiguration? Random { get; }
    public IReadOnlyList<ConformanceSourceInput> Sources { get; }
    public IReadOnlyList<ConformanceNativeHandler> NativeHandlers { get; }
    public IReadOnlyList<ConformanceStep> Steps { get; }
    public ConformanceExpectation Expectation { get; }
    public ConformanceMessageApiCase? MessageApi { get; }
    public ConformanceValueApiCase? ValueApi { get; }
    public ConformanceExternalTypeApiCase? ExternalTypeApi { get; }
    public ConformanceBinaryFixture? BinaryFixture { get; }
    public ConformancePerformanceWorkload? Performance { get; }
    public string? ExpectedAssembler { get; }
    public ConformanceSourceRange MetadataBlockRange { get; }
    public ConformanceSourceRange? ExpectationBlockRange { get; }
    public ConformanceSourceRange? StepsTableRange { get; }
    public ConformanceSourceRange? AssemblerBlockRange { get; }
    public ConformanceSourceRange? AssemblerPayloadRange { get; }
    public ConformanceSourceRange Range { get; }
}

public sealed class ConformanceCapabilityRequirements
{
    internal ConformanceCapabilityRequirements(IReadOnlyList<string> core, IReadOnlyList<string> optional)
    {
        Core = ConformanceDocument.Copy(core);
        Optional = ConformanceDocument.Copy(optional);
    }

    public IReadOnlyList<string> Core { get; }
    public IReadOnlyList<string> Optional { get; }
}

public sealed class ConformanceCompileOptions
{
    internal ConformanceCompileOptions(IReadOnlyList<string> debugInfo, bool binaryRoundTrip)
    {
        DebugInfo = ConformanceDocument.Copy(debugInfo);
        BinaryRoundTrip = binaryRoundTrip;
    }

    public IReadOnlyList<string> DebugInfo { get; }
    public bool BinaryRoundTrip { get; }
}

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

    public string Id { get; }
    public string ResourceId { get; }
    public string RelativePath { get; }
    public string Sha256 { get; }
    public string CompilerId { get; }
    public string CompilerVersion { get; }
    public ulong ProgramVersion { get; }
    public bool CompareCompiledRuntime { get; }
    public string? Derivation { get; }
}

public sealed class ConformanceRuntimeLimits
{
    internal ConformanceRuntimeLimits(IReadOnlyDictionary<string, ulong> values)
        => Values = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(new Dictionary<string, ulong>(values, StringComparer.Ordinal));

    public IReadOnlyDictionary<string, ulong> Values { get; }
}

public sealed class ConformanceComparisonOptions
{
    internal ConformanceComparisonOptions(ConformanceBinary64ComparisonMode mode, ulong maxUlps)
    {
        Binary64Mode = mode;
        MaxUlps = maxUlps;
    }

    public ConformanceBinary64ComparisonMode Binary64Mode { get; }
    public ulong MaxUlps { get; }
}

public sealed class ConformanceRandomConfiguration
{
    internal ConformanceRandomConfiguration(long? seed, IReadOnlyList<string> sequence)
    {
        Seed = seed;
        Sequence = ConformanceDocument.Copy(sequence);
    }

    public long? Seed { get; }
    public IReadOnlyList<string> Sequence { get; }
}

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

    public string Name { get; }
    public string ProgramId { get; }
    public string Text { get; }
    public ConformanceSourceRange BlockRange { get; }
    public ConformanceSourceRange PayloadRange { get; }
}

public sealed class ConformanceNativeHandler
{
    internal ConformanceNativeHandler(string id, string message, IReadOnlyList<string> parameters, bool messageNameOnly, int priority, bool initiallySubscribed, bool throws, IReadOnlyList<ConformanceNativeAction> actions, IReadOnlyList<ConformanceNativeEmit> emits)
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

    public string Id { get; }
    public string Message { get; }
    public IReadOnlyList<string> Parameters { get; }
    public bool MessageNameOnly { get; }
    public int Priority { get; }
    public bool InitiallySubscribed { get; }
    public bool Throws { get; }
    public IReadOnlyList<ConformanceNativeAction> Actions { get; }
    public IReadOnlyList<ConformanceNativeEmit> Emits { get; }
}

public sealed class ConformanceNativeAction
{
    internal ConformanceNativeAction(ConformanceNativeActionKind kind, string target, bool? expectedResult)
    {
        Kind = kind;
        Target = target;
        ExpectedResult = expectedResult;
    }

    public ConformanceNativeActionKind Kind { get; }
    public string Target { get; }
    public bool? ExpectedResult { get; }
}

public sealed class ConformanceNativeEmit
{
    internal ConformanceNativeEmit(string name, bool forwardArguments, IReadOnlyList<ConformanceArgument> arguments)
    {
        Name = name;
        ForwardArguments = forwardArguments;
        Arguments = ConformanceDocument.Copy(arguments);
    }

    public string Name { get; }
    public bool ForwardArguments { get; }
    public IReadOnlyList<ConformanceArgument> Arguments { get; }
}

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

    public string Id { get; }
    public string Receive { get; }
    public ConformancePumpMode Pump { get; }
    public uint? Budget { get; }
    public IReadOnlyList<ConformanceNativeAction> Actions { get; }
    public ConformanceStepExpectation Expectation { get; }
    public ConformanceSourceRange Range { get; }
}

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

    public ConformanceMessage Input { get; }
    public bool Accepted { get; }
    public IReadOnlyList<ConformanceMessage> Local { get; }
    public IReadOnlyList<ConformanceMessage> Outbound { get; }
    public bool? Paused { get; }
    public ConformanceObservationExpectation Observations { get; }
}

public sealed class ConformanceObservationExpectation
{
    internal ConformanceObservationExpectation(IReadOnlyList<ConformanceRuntimeLimitExpectation> included, IReadOnlyList<ConformanceRuntimeLimitExpectation> excluded, IReadOnlyList<ConformanceExpectedDiagnostic> diagnostics, bool traceSpecified, IReadOnlyList<ConformanceObserverEventExpectation> trace)
    {
        IncludedRuntimeLimits = ConformanceDocument.Copy(included);
        ExcludedRuntimeLimits = ConformanceDocument.Copy(excluded);
        Diagnostics = ConformanceDocument.Copy(diagnostics);
        TraceSpecified = traceSpecified;
        Trace = ConformanceDocument.Copy(trace);
    }

    public IReadOnlyList<ConformanceRuntimeLimitExpectation> IncludedRuntimeLimits { get; }
    public IReadOnlyList<ConformanceRuntimeLimitExpectation> ExcludedRuntimeLimits { get; }
    public IReadOnlyList<ConformanceExpectedDiagnostic> Diagnostics { get; }
    public bool TraceSpecified { get; }
    public IReadOnlyList<ConformanceObserverEventExpectation> Trace { get; }
}

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

    public ConformanceObserverEventKind Kind { get; }
    public ConformanceMessage? Message { get; }
    public string? SignatureId { get; }
    public bool? Accepted { get; }
    public ConformancePublishResultExpectation? PublishResult { get; }
    public ConformanceRuntimeLimitExpectation? RuntimeLimit { get; }
    public ConformanceExpectedDiagnostic? Diagnostic { get; }
}

public sealed class ConformancePublishResultExpectation
{
    internal ConformancePublishResultExpectation(bool localAccepted, bool outboundAttempted, bool outboundAccepted, bool anyAccepted)
    {
        LocalAccepted = localAccepted;
        OutboundAttempted = outboundAttempted;
        OutboundAccepted = outboundAccepted;
        AnyAccepted = anyAccepted;
    }

    public bool LocalAccepted { get; }
    public bool OutboundAttempted { get; }
    public bool OutboundAccepted { get; }
    public bool AnyAccepted { get; }
}

public sealed class ConformanceRuntimeLimitExpectation
{
    internal ConformanceRuntimeLimitExpectation(bool any, string? name, string? detailContains, ulong? limit)
    {
        Any = any;
        Name = name;
        DetailContains = detailContains;
        Limit = limit;
    }

    public bool Any { get; }
    public string? Name { get; }
    public string? DetailContains { get; }
    public ulong? Limit { get; }
}

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

    public string Phase { get; }
    public string Code { get; }
    public string? Symbol { get; }
    public string? SymbolKind { get; }
    public string? SourceName { get; }
    public uint? Line { get; }
    public uint? Column { get; }
    public uint? EndLine { get; }
    public uint? EndColumn { get; }
    public string? ProgramName { get; }
    public string? HandlerName { get; }
}

public sealed class ConformanceMessage
{
    internal ConformanceMessage(string name, IReadOnlyList<string> tags, IReadOnlyList<ConformanceArgument> arguments)
    {
        Name = name;
        Tags = ConformanceDocument.Copy(tags);
        Arguments = ConformanceDocument.Copy(arguments);
    }

    public string Name { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<ConformanceArgument> Arguments { get; }
}

public sealed class ConformanceArgument
{
    internal ConformanceArgument(string name, ConformanceValue value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public ConformanceValue Value { get; }
}

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

    public string Type { get; }
    public string? Value { get; }
    public string? Unit { get; }
    public string? X { get; }
    public string? Y { get; }
    public string? Z { get; }
    public string? From { get; }
    public string? To { get; }
    public string? Step { get; }
    public string? RangeKind { get; }
    public IReadOnlyList<ConformanceValue> Items { get; }
    public IReadOnlyList<ConformanceValueEntry> Entries { get; }
    public IReadOnlyList<int> Rolls { get; }
    public ConformanceMessage? Message { get; }

    private static string? Read(IReadOnlyDictionary<string, string> values, string name)
        => values.TryGetValue(name, out var value) ? value : null;
}

public sealed class ConformanceValueEntry
{
    internal ConformanceValueEntry(string key, ConformanceValue value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }
    public ConformanceValue Value { get; }
}

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

    public ConformanceChannelExpectation Initialization { get; }
    public ConformanceExpectedDiagnostic? Error { get; }
    public ConformanceMessageApiExpectation? MessageApi { get; }
    public ConformanceValueApiExpectation? ValueApi { get; }
    public ConformanceExternalTypeApiExpectation? ExternalTypeApi { get; }
    public ConformanceCompileMetadataExpectation? Metadata { get; }
    public ConformanceOpcodeExpectation? Opcodes { get; }
    public ConformancePerformanceExpectation? Performance { get; }
    public ConformanceBinaryExpectation? Binary { get; }
}

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

    public ConformanceBinaryOutcome Outcome { get; }
    public string? ErrorCode { get; }
    public long? ByteOffset { get; }
    public ushort? SectionType { get; }
    public int? EntryIndex { get; }
    public bool? RewriteByteExact { get; }
    public string? RewriteSha256 { get; }
    public string? ModuleName { get; }
    public uint? RequiredRegisterCount { get; }
    public uint? RequiredCallStackDepth { get; }
    public uint? OpaqueSectionCount { get; }
}

public sealed class ConformanceChannelExpectation
{
    internal ConformanceChannelExpectation(IReadOnlyList<ConformanceMessage> local, IReadOnlyList<ConformanceMessage> outbound, ConformanceObservationExpectation observations)
    {
        Local = ConformanceDocument.Copy(local);
        Outbound = ConformanceDocument.Copy(outbound);
        Observations = observations;
    }

    public IReadOnlyList<ConformanceMessage> Local { get; }
    public IReadOnlyList<ConformanceMessage> Outbound { get; }
    public ConformanceObservationExpectation Observations { get; }
}

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

    public string SignatureName { get; }
    public IReadOnlyList<string> Parameters { get; }
    public ConformanceMessage Message { get; }
    public bool ArgumentsWereMapping { get; }
    public IReadOnlyList<ConformanceValueEntry> UnorderedArguments { get; }
    public ConformanceMessageSignatureDefinition? CompareSignature { get; }
    public ConformanceMessage? CompareMessage { get; }
    public ConformanceMessageSignatureDefinition? CompareHandler { get; }
    public IReadOnlyList<ConformanceValue> CreateArguments { get; }
}

public sealed class ConformanceMessageSignatureDefinition
{
    internal ConformanceMessageSignatureDefinition(string name, IReadOnlyList<string> parameters)
    {
        Name = name;
        Parameters = ConformanceDocument.Copy(parameters);
    }

    public string Name { get; }
    public IReadOnlyList<string> Parameters { get; }
}

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

    public string? Name { get; }
    public string? SignatureId { get; }
    public string? MessageSignatureId { get; }
    public bool? Matches { get; }
    public uint? ArgumentCount { get; }
    public bool? SignatureEquals { get; }
    public bool? SignatureHashEquals { get; }
    public bool? MessageEquals { get; }
    public bool? MessageHashEquals { get; }
    public bool? HandlerEquals { get; }
    public bool? HandlerHashEquals { get; }
    public string? CreatedMessageSignatureId { get; }
    public string? Error { get; }
}

public sealed class ConformanceValueApiCase
{
    internal ConformanceValueApiCase(ConformanceValue value, ConformanceValue? equalTo, ConformanceValue? notEqualTo, bool mutateSourceAfterCreate)
    {
        Value = value;
        EqualTo = equalTo;
        NotEqualTo = notEqualTo;
        MutateSourceAfterCreate = mutateSourceAfterCreate;
    }

    public ConformanceValue Value { get; }
    public ConformanceValue? EqualTo { get; }
    public ConformanceValue? NotEqualTo { get; }
    public bool MutateSourceAfterCreate { get; }
}

public sealed class ConformanceValueApiExpectation
{
    internal ConformanceValueApiExpectation(
        ConformanceValue normalized,
        bool? isNumeric,
        bool? hasValue,
        bool? isNothing,
        bool? hasUnit,
        bool? asBoolean,
        uint? length,
        string? customTypeName,
        bool? equal,
        bool? equalHash,
        bool? notEqual)
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

    public ConformanceValue Normalized { get; }
    public bool? IsNumeric { get; }
    public bool? HasValue { get; }
    public bool? IsNothing { get; }
    public bool? HasUnit { get; }
    public bool? AsBoolean { get; }
    public uint? Length { get; }
    public string? CustomTypeName { get; }
    public bool? Equal { get; }
    public bool? EqualHash { get; }
    public bool? NotEqual { get; }
}

public sealed class ConformanceExternalTypeApiCase
{
    internal ConformanceExternalTypeApiCase(IReadOnlyList<string> typeNames)
        => TypeNames = ConformanceDocument.Copy(typeNames);

    public IReadOnlyList<string> TypeNames { get; }
}

public sealed class ConformanceExternalTypeApiExpectation
{
    internal ConformanceExternalTypeApiExpectation(uint? typeCount, string? error)
    {
        TypeCount = typeCount;
        Error = error;
    }

    public uint? TypeCount { get; }
    public string? Error { get; }
}

public sealed class ConformanceCompileMetadataExpectation
{
    internal ConformanceCompileMetadataExpectation(IReadOnlyList<ConformanceMessageDefinitionExpectation> messageDefinitions, ConformanceProgramResourceExpectation? programResources, IReadOnlyList<ConformanceHandlerResourceExpectation> handlerResources)
    {
        MessageDefinitions = ConformanceDocument.Copy(messageDefinitions);
        ProgramResources = programResources;
        HandlerResources = ConformanceDocument.Copy(handlerResources);
    }

    public IReadOnlyList<ConformanceMessageDefinitionExpectation> MessageDefinitions { get; }
    public ConformanceProgramResourceExpectation? ProgramResources { get; }
    public IReadOnlyList<ConformanceHandlerResourceExpectation> HandlerResources { get; }
}

public sealed class ConformanceMessageDefinitionExpectation
{
    internal ConformanceMessageDefinitionExpectation(string name, uint count, IReadOnlyList<string> signatureIds)
    {
        Name = name;
        Count = count;
        SignatureIds = ConformanceDocument.Copy(signatureIds);
    }

    public string Name { get; }
    public uint Count { get; }
    public IReadOnlyList<string> SignatureIds { get; }
}

public sealed class ConformanceProgramResourceExpectation
{
    internal ConformanceProgramResourceExpectation(uint? requiredRegisterCount, uint? requiredCallStackDepth)
    {
        RequiredRegisterCount = requiredRegisterCount;
        RequiredCallStackDepth = requiredCallStackDepth;
    }

    public uint? RequiredRegisterCount { get; }
    public uint? RequiredCallStackDepth { get; }
}

public sealed class ConformanceHandlerResourceExpectation
{
    internal ConformanceHandlerResourceExpectation(string name, string? signatureId, uint? requiredRegisterCount, uint? requiredCallStackDepth)
    {
        Name = name;
        SignatureId = signatureId;
        RequiredRegisterCount = requiredRegisterCount;
        RequiredCallStackDepth = requiredCallStackDepth;
    }

    public string Name { get; }
    public string? SignatureId { get; }
    public uint? RequiredRegisterCount { get; }
    public uint? RequiredCallStackDepth { get; }
}

public sealed class ConformanceOpcodeExpectation
{
    internal ConformanceOpcodeExpectation(IReadOnlyList<string> contains, IReadOnlyList<string> excludes, IReadOnlyDictionary<string, ulong> counts, IReadOnlyDictionary<string, ulong> minimumCounts)
    {
        Contains = ConformanceDocument.Copy(contains);
        Excludes = ConformanceDocument.Copy(excludes);
        Counts = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(new Dictionary<string, ulong>(counts, StringComparer.Ordinal));
        MinimumCounts = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(new Dictionary<string, ulong>(minimumCounts, StringComparer.Ordinal));
    }

    public IReadOnlyList<string> Contains { get; }
    public IReadOnlyList<string> Excludes { get; }
    public IReadOnlyDictionary<string, ulong> Counts { get; }
    public IReadOnlyDictionary<string, ulong> MinimumCounts { get; }
}

public sealed class ConformancePerformanceWorkload
{
    internal ConformancePerformanceWorkload(uint iterations, uint warmupIterations, uint compileWarmupIterations)
    {
        Iterations = iterations;
        WarmupIterations = warmupIterations;
        CompileWarmupIterations = compileWarmupIterations;
    }

    public uint Iterations { get; }
    public uint WarmupIterations { get; }
    public uint CompileWarmupIterations { get; }
}

public sealed class ConformancePerformanceExpectation
{
    internal ConformancePerformanceExpectation(IReadOnlyList<ConformancePerformanceProfile> profiles)
        => Profiles = ConformanceDocument.Copy(profiles);

    public IReadOnlyList<ConformancePerformanceProfile> Profiles { get; }
}

public sealed class ConformancePerformanceProfile
{
    internal ConformancePerformanceProfile(string id, IReadOnlyList<ConformancePerformanceMetric> metrics)
    {
        Id = id;
        Metrics = ConformanceDocument.Copy(metrics);
    }

    public string Id { get; }
    public IReadOnlyList<ConformancePerformanceMetric> Metrics { get; }
}

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

    public string Id { get; }
    public string Reference { get; }
    public string? Maximum { get; }
    public string? ToleranceRelative { get; }
    public string? ToleranceAbsolute { get; }
    public string Unit { get; }
    public ConformanceSourceRange ReferenceRange { get; }
}
