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
    BytecodeSnapshot = 7
}

public enum ConformanceTestLevel
{
    Atomic = 0,
    Scenario = 1
}

public enum ConformancePumpMode
{
    Completion = 0,
    Frames = 1
}

public enum ConformanceBinary64ComparisonMode
{
    Exact = 0,
    Ulp = 1
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
        ConformanceRandomConfiguration? random,
        IReadOnlyList<ConformanceSourceInput> sources,
        IReadOnlyList<ConformanceNativeHandler> nativeHandlers,
        IReadOnlyList<ConformanceStep> steps,
        ConformanceExpectation expectation,
        ConformanceMessageApiCase? messageApi,
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
        Title = title;
        Kind = kind;
        Level = level;
        Categories = ConformanceDocument.Copy(categories);
        Tags = ConformanceDocument.Copy(tags);
        Requires = requires;
        Compile = compile;
        RuntimeLimits = runtimeLimits;
        Comparison = comparison;
        Random = random;
        Sources = ConformanceDocument.Copy(sources);
        NativeHandlers = ConformanceDocument.Copy(nativeHandlers);
        Steps = ConformanceDocument.Copy(steps);
        Expectation = expectation;
        MessageApi = messageApi;
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
    public string Title { get; }
    public ConformanceTestKind Kind { get; }
    public ConformanceTestLevel Level { get; }
    public IReadOnlyList<string> Categories { get; }
    public IReadOnlyList<string> Tags { get; }
    public ConformanceCapabilityRequirements Requires { get; }
    public ConformanceCompileOptions Compile { get; }
    public ConformanceRuntimeLimits RuntimeLimits { get; }
    public ConformanceComparisonOptions Comparison { get; }
    public ConformanceRandomConfiguration? Random { get; }
    public IReadOnlyList<ConformanceSourceInput> Sources { get; }
    public IReadOnlyList<ConformanceNativeHandler> NativeHandlers { get; }
    public IReadOnlyList<ConformanceStep> Steps { get; }
    public ConformanceExpectation Expectation { get; }
    public ConformanceMessageApiCase? MessageApi { get; }
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
    internal ConformanceNativeHandler(string message, IReadOnlyList<string> parameters, int priority, bool throws, IReadOnlyList<ConformanceNativeEmit> emits)
    {
        Message = message;
        Parameters = ConformanceDocument.Copy(parameters);
        Priority = priority;
        Throws = throws;
        Emits = ConformanceDocument.Copy(emits);
    }

    public string Message { get; }
    public IReadOnlyList<string> Parameters { get; }
    public int Priority { get; }
    public bool Throws { get; }
    public IReadOnlyList<ConformanceNativeEmit> Emits { get; }
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
    internal ConformanceStep(string id, string receive, ConformancePumpMode pump, uint? budget, ConformanceStepExpectation expectation, ConformanceSourceRange range)
    {
        Id = id;
        Receive = receive;
        Pump = pump;
        Budget = budget;
        Expectation = expectation;
        Range = range;
    }

    public string Id { get; }
    public string Receive { get; }
    public ConformancePumpMode Pump { get; }
    public uint? Budget { get; }
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
    internal ConformanceObservationExpectation(IReadOnlyList<ConformanceRuntimeLimitExpectation> included, IReadOnlyList<ConformanceRuntimeLimitExpectation> excluded, IReadOnlyList<ConformanceExpectedDiagnostic> diagnostics)
    {
        IncludedRuntimeLimits = ConformanceDocument.Copy(included);
        ExcludedRuntimeLimits = ConformanceDocument.Copy(excluded);
        Diagnostics = ConformanceDocument.Copy(diagnostics);
    }

    public IReadOnlyList<ConformanceRuntimeLimitExpectation> IncludedRuntimeLimits { get; }
    public IReadOnlyList<ConformanceRuntimeLimitExpectation> ExcludedRuntimeLimits { get; }
    public IReadOnlyList<ConformanceExpectedDiagnostic> Diagnostics { get; }
}

public sealed class ConformanceRuntimeLimitExpectation
{
    internal ConformanceRuntimeLimitExpectation(string? name, string? detailContains, ulong? limit)
    {
        Name = name;
        DetailContains = detailContains;
        Limit = limit;
    }

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
        ConformanceCompileMetadataExpectation? metadata,
        ConformanceOpcodeExpectation? opcodes,
        ConformancePerformanceExpectation? performance)
    {
        Initialization = initialization;
        Error = error;
        MessageApi = messageApi;
        Metadata = metadata;
        Opcodes = opcodes;
        Performance = performance;
    }

    public ConformanceChannelExpectation Initialization { get; }
    public ConformanceExpectedDiagnostic? Error { get; }
    public ConformanceMessageApiExpectation? MessageApi { get; }
    public ConformanceCompileMetadataExpectation? Metadata { get; }
    public ConformanceOpcodeExpectation? Opcodes { get; }
    public ConformancePerformanceExpectation? Performance { get; }
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
    internal ConformanceMessageApiCase(string signatureName, IReadOnlyList<string> parameters, ConformanceMessage message)
    {
        SignatureName = signatureName;
        Parameters = ConformanceDocument.Copy(parameters);
        Message = message;
    }

    public string SignatureName { get; }
    public IReadOnlyList<string> Parameters { get; }
    public ConformanceMessage Message { get; }
}

public sealed class ConformanceMessageApiExpectation
{
    internal ConformanceMessageApiExpectation(string? name, string? signatureId, string? messageSignatureId, bool? matches, uint? argumentCount, string? error)
    {
        Name = name;
        SignatureId = signatureId;
        MessageSignatureId = messageSignatureId;
        Matches = matches;
        ArgumentCount = argumentCount;
        Error = error;
    }

    public string? Name { get; }
    public string? SignatureId { get; }
    public string? MessageSignatureId { get; }
    public bool? Matches { get; }
    public uint? ArgumentCount { get; }
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
