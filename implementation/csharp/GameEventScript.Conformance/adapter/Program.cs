// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Text.Json;
using GameEventScript.Api;

const int maxBytes = 32 * 1024 * 1024;
var capabilities = new[] { "compiler", "program-binary", "bytecode-snapshot", "host", "vm", "observer", "publish-sink", "external-types", "native-handlers", "value-api", "message-api" };
var implementation = new { id = "csharp", version = typeof(GesProgramBuilder).Assembly.GetName().Version!.ToString() };
try
{
    if (args.SequenceEqual(new[] { "--describe" }))
    {
        var supportedKinds = new[] { "bytecodeSnapshot", "compileError", "bytecode", "compileMetadata", "programBinary", "scriptApi", "loadError", "externalTypeApi", "valueApi", "messageApi" };
        var performanceProfiles = new List<string>();
#if !DEBUG
        performanceProfiles.Add(AllocationObservations.Profile);
        if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64) performanceProfiles.Add(TimingObservations.Profile);
#endif
        Console.WriteLine(JsonSerializer.Serialize(new { formatVersion = 1, implementation, capabilities, supportedKinds, supportedBinarySteps = true, performanceProfiles }));
        return 0;
    }
    var allocation = args.SequenceEqual(new[] { "--measure" });
    if (args.Length != 0 && !allocation) throw new ArgumentException("Use no arguments for a JSON batch on stdin, or --describe.");
    using var input = Console.OpenStandardInput();
    using var buffer = new MemoryStream();
    var chunk = new byte[8192];
    int count;
    while ((count = input.Read(chunk)) != 0)
    {
        if (buffer.Length + count > maxBytes) throw new ArgumentException("Request exceeds 32 MiB.");
        buffer.Write(chunk, 0, count);
    }
    using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 64 });
    RejectDuplicateProperties(document.RootElement);
    var request = document.RootElement;
    Fields(request, "formatVersion", "requestDigest", "cases");
    if (request.GetProperty("formatVersion").GetInt32() != 1) throw new ArgumentException("Unsupported protocol version.");
    var digest = Text(request, "requestDigest");
    if (digest.Length != 64 || !digest.All(char.IsAsciiHexDigit)) throw new ArgumentException("Invalid request digest.");
    var cases = request.GetProperty("cases");
    if (cases.ValueKind != JsonValueKind.Array || cases.GetArrayLength() is < 1 or > 10000) throw new ArgumentException("Invalid case batch.");
    var observations = new Dictionary<string, object>(StringComparer.Ordinal);
    // Validate the complete batch before invoking any compiler.
    foreach (var test in cases.EnumerateArray())
    {
        ValidateCase(test, capabilities, allocation);
        if (!observations.TryAdd(Text(test, "id"), null!)) throw new ArgumentException("Duplicate case ID.");
    }
    foreach (var test in cases.EnumerateArray())
    {
        var id = Text(test, "id");
        try
        {
            if (allocation)
            {
                observations[id] = Text(test.GetProperty("measurement"), "profile") == TimingObservations.Profile
                    ? TimingObservations.Run(test) : AllocationObservations.Run(test);
                continue;
            }
            if (Text(test, "kind") is "loadError" or "externalTypeApi" or "valueApi" or "messageApi")
            {
                observations[id] = ApiObservations.Run(test);
                continue;
            }
            if (Text(test, "kind") == "programBinary")
            {
                observations[id] = BinaryObservations.Run(test);
                continue;
            }
            if (Text(test, "kind") == "scriptApi")
            {
                var programs = new Dictionary<string, GameEventScriptProgram>(StringComparer.Ordinal);
                foreach (var source in test.GetProperty("sources").EnumerateArray())
                {
                    var programId = source.GetProperty("program").GetString()!;
                    if (!programs.ContainsKey(programId)) programs.Add(programId, AdapterCompiler.Compile(test, programId: programId));
                }
                observations[id] = new { status = "ok", runtime = RuntimeObservations.Run(programs, test.GetProperty("runtime")) };
                continue;
            }
            observations[id] = CompilerObservations.Success(Text(test, "kind"), AdapterCompiler.Compile(test));
        }
        catch (GesCompileException exception)
        {
            if (Text(test, "kind") is "programBinary" or "scriptApi" or "loadError")
            {
                observations[id] = new { status = "error", detail = "Runtime input compilation failed." };
                continue;
            }
            observations[id] = new
            {
                status = "compileError",
                detail = "Compilation failed.",
                diagnostics = exception.Diagnostics.Select(CompilerObservations.Diagnostic).ToArray()
            };
        }
        catch (Exception exception)
        {
            observations[id] = new { status = "error", detail = exception.Message };
        }
    }
    var output = JsonSerializer.SerializeToUtf8Bytes(new { formatVersion = 1, requestDigest = digest, implementation, capabilities, observations });
    if (output.Length > maxBytes) throw new ArgumentException("Response exceeds 32 MiB.");
    using var stdout = Console.OpenStandardOutput();
    stdout.Write(output);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

static string Text(JsonElement element, string name, bool allowEmpty = false)
{
    var value = element.GetProperty(name);
    if (value.ValueKind != JsonValueKind.String || (!allowEmpty && value.GetString()!.Length == 0)) throw new ArgumentException("Invalid text field: " + name);
    return value.GetString()!;
}

static void Fields(JsonElement element, params string[] allowed)
{
    if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
        throw new ArgumentException("Unknown field or invalid object.");
    foreach (var name in allowed) _ = element.GetProperty(name);
}

static void ValidateCase(JsonElement test, IReadOnlyCollection<string> capabilities, bool allocation)
{
    if (allocation && Text(test, "kind") != "scriptApi") throw new ArgumentException("Allocation mode requires script input.");
    var binary = Text(test, "kind") == "programBinary";
    if (Text(test, "kind") is "loadError" or "externalTypeApi" or "valueApi" or "messageApi") Fields(test, "id", "kind", "sources", "compile", "requires", "api");
    else if (binary) Fields(test, "id", "kind", "sources", "compile", "requires", "binary");
    else if (Text(test, "kind") == "scriptApi")
    {
        if (allocation) Fields(test, "id", "kind", "sources", "compile", "requires", "runtime", "measurement");
        else Fields(test, "id", "kind", "sources", "compile", "requires", "runtime");
    }
    else Fields(test, "id", "kind", "sources", "compile", "requires");
    _ = Text(test, "id");
    if (Text(test, "kind") is not ("bytecodeSnapshot" or "compileError" or "bytecode" or "compileMetadata" or "programBinary" or "scriptApi" or "loadError" or "externalTypeApi" or "valueApi" or "messageApi"))
        throw new ArgumentException("Unsupported test kind.");
    var sources = test.GetProperty("sources");
    if (sources.ValueKind != JsonValueKind.Array || (!binary && Text(test, "kind") is not ("scriptApi" or "externalTypeApi" or "valueApi" or "messageApi") && sources.GetArrayLength() == 0)) throw new ArgumentException("Sources required.");
    string? program = null;
    foreach (var source in sources.EnumerateArray())
    {
        Fields(source, "name", "program", "text");
        _ = Text(source, "name");
        _ = Text(source, "text", allowEmpty: true);
        var current = Text(source, "program");
        program ??= current;
        if (program != current && Text(test, "kind") != "scriptApi") throw new ArgumentException("Compiler case requires one Program.");
    }
    if (binary)
    {
        var input = test.GetProperty("binary");
        Fields(input, "data", "includeDump", "compareCompiledRuntime", "programVersion", "runtime");
        _ = input.GetProperty("programVersion").GetUInt64();
        var runtime = input.GetProperty("runtime");
        if (runtime.ValueKind != JsonValueKind.Null)
        {
            ValidateRuntime(runtime);
            HostScenario.Validate(runtime, new HashSet<string>(StringComparer.Ordinal) { "main" });
        }
        var bytes = Convert.FromBase64String(Text(input, "data", allowEmpty: true));
        if (bytes.Length > BinaryObservations.MaxResourceBytes) throw new ArgumentException("Binary resource exceeds limit.");
        _ = input.GetProperty("includeDump").GetBoolean();
        if (input.GetProperty("compareCompiledRuntime").GetBoolean() && sources.GetArrayLength() == 0) throw new ArgumentException("Comparison requires sources.");
        if (test.GetProperty("compile").GetProperty("binaryRoundTrip").GetBoolean()) throw new ArgumentException("Binary input cannot request another roundtrip.");
    }
    if (Text(test, "kind") == "scriptApi")
    {
        ValidateRuntime(test.GetProperty("runtime"));
        HostScenario.Validate(test.GetProperty("runtime"), sources.EnumerateArray().Select(s => s.GetProperty("program").GetString()!).ToHashSet(StringComparer.Ordinal));
        if (sources.GetArrayLength() == 0 && test.GetProperty("runtime").GetProperty("nativeHandlers").GetArrayLength() == 0) throw new ArgumentException("Sources or native handlers required.");
    }
    if (Text(test, "kind") == "loadError")
    {
        var api = test.GetProperty("api");
        Fields(api, "limits", "externalTypeRegistry");
        ValidateLimits(api.GetProperty("limits"));
        if (Text(api, "externalTypeRegistry") is not ("environment" or "absent" or "mismatch")) throw new ArgumentException("Unknown external registry mode.");
    }
    if (Text(test, "kind") == "externalTypeApi")
    {
        if (sources.GetArrayLength() != 0) throw new ArgumentException("Catalog test cannot contain source.");
        var api = test.GetProperty("api");
        Fields(api, "typeNames");
        foreach (var name in api.GetProperty("typeNames").EnumerateArray())
            if (string.IsNullOrWhiteSpace(name.GetString())) throw new ArgumentException("External type name required.");
    }
    if (Text(test, "kind") == "messageApi")
    {
        if (sources.GetArrayLength() != 0) throw new ArgumentException("Message API cannot contain source.");
        MessageApiObservations.Validate(test.GetProperty("api"));
    }
    if (Text(test, "kind") == "valueApi")
    {
        if (sources.GetArrayLength() != 0) throw new ArgumentException("Value API cannot contain source.");
        ValueApiObservations.Validate(test.GetProperty("api"));
    }
    if (allocation && Text(test.GetProperty("measurement"), "profile") == TimingObservations.Profile) TimingObservations.Validate(test);
    else if (allocation) AllocationObservations.Validate(test);
    var options = test.GetProperty("compile");
    Fields(options, "debugInfo", "binaryRoundTrip");
    _ = options.GetProperty("binaryRoundTrip").GetBoolean();
    var debug = options.GetProperty("debugInfo");
    var names = new HashSet<string>(StringComparer.Ordinal);
    foreach (var item in debug.EnumerateArray())
    {
        var name = item.GetString();
        if (name is not ("debugSymbols" or "sourceMap" or "sourceArchive") || !names.Add(name)) throw new ArgumentException("Invalid debug options.");
    }
    var requires = test.GetProperty("requires");
    Fields(requires, "core", "optional");
    foreach (var category in requires.EnumerateObject())
        foreach (var capability in category.Value.EnumerateArray())
            if (!capabilities.Contains(capability.GetString())) throw new ArgumentException("Unsupported required capability.");
}

static void RejectDuplicateProperties(JsonElement element)
{
    if (element.ValueKind == JsonValueKind.Object)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name)) throw new ArgumentException("Duplicate JSON property.");
            RejectDuplicateProperties(property.Value);
        }
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
        foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child);
    }
}

static void ValidateRuntime(JsonElement runtime)
{
    Fields(runtime, "limits", "steps", "random", "hostCount", "nativeHandlers", "deferredPrograms", "stepActions", "initializationPump", "publishSink");
    if (Text(runtime, "publishSink") is not ("accept" or "absent" or "reject" or "throw")) throw new ArgumentException("Invalid publish sink mode.");
    if (Text(runtime, "initializationPump") is not ("start" or "completion")) throw new ArgumentException("Invalid initialization pump.");
    NativeHandlers.Validate(runtime.GetProperty("nativeHandlers"));
    if (runtime.GetProperty("hostCount").GetInt32() is < 1 or > 256) throw new ArgumentException("Invalid host count.");
    RuntimeRandom.Validate(runtime.GetProperty("random"));
    ValidateLimits(runtime.GetProperty("limits"));
    var stepIds = new HashSet<string>(StringComparer.Ordinal);
    foreach (var step in runtime.GetProperty("steps").EnumerateArray())
    {
        Fields(step, "id", "input", "pump", "budget");
        var pump = Text(step, "pump");
        if (pump is not ("completion" or "enqueue" or "frame" or "frames")) throw new ArgumentException("Invalid pump mode.");
        if (pump is "frame" or "frames")
        {
            if (step.GetProperty("budget").GetInt32() <= 0) throw new ArgumentException("Invalid frame budget.");
        }
        else if (step.GetProperty("budget").ValueKind != JsonValueKind.Null) throw new ArgumentException("Unexpected frame budget.");
        if (!stepIds.Add(Text(step, "id"))) throw new ArgumentException("Duplicate step ID.");
        _ = RuntimeValues.DecodeMessage(step.GetProperty("input"));
    }

}

static void ValidateLimits(JsonElement limits)
{
    var allowedLimits = new[]
    {
        "maxProcessedEventsPerRun", "maxQueuedMessagesPerRun", "maxExecutionSteps", "maxRegisterValues", "maxLoopIterations", "maxCallDepth",
        "maxRandomScopeDepth", "maxRangeItems", "maxGeneratedCollectionItems", "maxDiceCount", "maxDiceSides"
    };
    foreach (var property in limits.EnumerateObject())
    {
        if (!allowedLimits.Contains(property.Name) || property.Value.GetInt32() <= 0) throw new ArgumentException("Invalid runtime limit.");
        if (property.Name == "maxRandomScopeDepth" && property.Value.GetInt32() > 65535) throw new ArgumentException("Random scope depth exceeds portable bound.");
    }
}
