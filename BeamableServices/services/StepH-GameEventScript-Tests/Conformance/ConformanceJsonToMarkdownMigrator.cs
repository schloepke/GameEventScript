using System.Globalization;
using System.Text;
using System.Text.Json;

namespace StepH_GameEventScript_Tests.Conformance;

/// <summary>
/// Temporary, test-internal migration utility. It is deliberately based on the
/// legacy C# JSON model and is removed together with that model in step 5.7.
/// </summary>
internal static class ConformanceJsonToMarkdownMigrator
{
    internal const string PerformanceProfile = "csharp-dotnet-release-macos-arm64";

    internal static IReadOnlyList<MigratedSuite> MigrateCorpus(
        string specDirectory,
        string performanceReferencePath,
        string binaryDumpReferencePath)
    {
        var performance = ReadPerformanceReference(performanceReferencePath);
        var snapshots = ReadBinaryDumpReference(binaryDumpReferencePath);
        var files = Directory.EnumerateFiles(specDirectory, "*.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var result = new List<MigratedSuite>(files.Length);
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(specDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
            var cases = GameEventScriptConformanceRunner.AllConformanceCases(specDirectory, relative);
            result.Add(new MigratedSuite(
                relative[..^".json".Length] + ".md",
                WriteSuite(relative, cases, performance, snapshots),
                cases.Count,
                string.Equals(relative, "performance/core.json", StringComparison.Ordinal) ? cases.Count : 0));
        }

        return result;
    }

    private static string WriteSuite(
        string relativeFile,
        IReadOnlyList<GameEventScriptConformanceCase> cases,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> performance,
        IReadOnlyDictionary<string, string> snapshots)
    {
        var suiteId = relativeFile[..^".json".Length].Replace('/', '.');
        var suiteName = cases[0].SuiteName;
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("formatVersion: 1");
        builder.Append("suiteId: ").AppendLine(YamlString(suiteId));
        builder.Append("title: ").AppendLine(YamlString(suiteName));
        builder.AppendLine("categories: [conformance]");
        builder.AppendLine("tags: [migrated-json-v1]");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.Append("# ").AppendLine(suiteName);
        builder.AppendLine();
        builder.AppendLine("Mechanically migrated from the former JSON conformance corpus.");

        for (var index = 0; index < cases.Count; index++)
        {
            var testCase = cases[index];
            var test = testCase.Test;
            var caseId = $"case-{index + 1:D4}";
            var title = ResolveTitle(relativeFile, caseId, test.Name!);
            WriteCase(builder, testCase, title, caseId, performance);
            if (string.Equals(relativeFile, "performance/core.json", StringComparison.Ordinal))
            {
                var snapshotKey = testCase.SuiteName + "/" + test.Name;
                if (!snapshots.TryGetValue(snapshotKey, out var assembler))
                    throw new InvalidOperationException("Missing performance binary dump for '" + snapshotKey + "'.");
                WriteSnapshot(builder, testCase, title + " bytecode snapshot", $"snapshot-{index + 1:D4}", assembler);
            }
        }

        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static void WriteCase(
        StringBuilder builder,
        GameEventScriptConformanceCase testCase,
        string title,
        string id,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> performance)
    {
        var test = testCase.Test;
        var sources = GetSources(test);
        builder.AppendLine();
        builder.Append("## Test: ").AppendLine(title);
        builder.AppendLine();
        builder.AppendLine("```yaml");
        builder.AppendLine("gesBlock: case");
        builder.Append("id: ").AppendLine(id);
        builder.Append("kind: ").AppendLine(test.Kind);
        builder.Append("level: ").AppendLine(testCase.Level);
        if (sources.Any(source => source.Text.Contains(":aim", StringComparison.Ordinal)))
        {
            builder.AppendLine("requires:");
            builder.AppendLine("  core: [external-types]");
        }
        WriteCompile(builder, test);
        WriteRuntimeLimits(builder, test.RuntimeLimits, 0);
        if (test.Kind is "scriptApi" or "performance")
        {
            var maxUlps = Math.Max(0, test.MaxFloatUlps ?? GameEventScriptConformanceValueCodec.DefaultMaxFloatUlps);
            builder.AppendLine("comparison:");
            builder.AppendLine("  binary64:");
            builder.AppendLine(maxUlps == 0 ? "    mode: exact" : "    mode: ulp");
            if (maxUlps != 0) builder.Append("    maxUlps: ").AppendLine(maxUlps.ToString(CultureInfo.InvariantCulture));
        }
        if (test.RandomSequence is { Count: > 0 })
        {
            builder.AppendLine("random:");
            builder.Append("  sequence: ");
            WriteFlowStrings(builder, test.RandomSequence.Select(NormalizeBinary64).ToArray());
            builder.AppendLine();
        }
        WriteNativeHandlers(builder, test.ExternalSubscribers);
        WriteMessageApi(builder, test);
        if (test.Kind == "performance")
        {
            builder.AppendLine("performance:");
            builder.Append("  iterations: ").AppendLine((test.Iterations ?? 1000).ToString(CultureInfo.InvariantCulture));
            builder.Append("  warmupIterations: ").AppendLine((test.WarmupIterations ?? 100).ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("  compileWarmupIterations: 3");
        }

        WriteSourceDescriptors(builder, sources);
        builder.AppendLine("```");
        WriteSources(builder, sources);
        WriteStepsTable(builder, test.Steps);
        WriteExpectation(builder, testCase, performance);
    }

    private static void WriteSnapshot(
        StringBuilder builder,
        GameEventScriptConformanceCase testCase,
        string title,
        string id,
        string assembler)
    {
        builder.AppendLine();
        builder.Append("## Test: ").AppendLine(title);
        builder.AppendLine();
        builder.AppendLine("```yaml");
        builder.AppendLine("gesBlock: case");
        builder.Append("id: ").AppendLine(id);
        builder.AppendLine("kind: bytecodeSnapshot");
        builder.Append("level: ").AppendLine(testCase.Level);
        var sources = GetSources(testCase.Test);
        WriteSourceDescriptors(builder, sources);
        builder.AppendLine("```");
        WriteSources(builder, sources);
        builder.AppendLine();
        builder.AppendLine("```gesa");
        builder.Append(assembler);
        if (!assembler.EndsWith('\n')) builder.AppendLine();
        else builder.AppendLine();
        builder.AppendLine("```");
    }

    private static void WriteCompile(StringBuilder builder, GameEventScriptConformanceTest test)
    {
        if (test.CompileOptions?.EnableDebugInfo is not false && !test.BinaryRoundTrip) return;
        builder.AppendLine("compile:");
        if (test.CompileOptions?.EnableDebugInfo is false) builder.AppendLine("  debugInfo: []");
        if (test.BinaryRoundTrip) builder.AppendLine("  binaryRoundTrip: true");
    }

    private static void WriteRuntimeLimits(StringBuilder builder, GameEventScriptRuntimeLimitsSpec? limits, int indent)
    {
        if (limits is null) return;
        Indent(builder, indent).AppendLine("runtimeLimits:");
        WriteNullable(builder, indent + 2, "maxProcessedEventsPerRun", limits.MaxProcessedEventsPerRun);
        WriteNullable(builder, indent + 2, "maxQueuedMessagesPerRun", limits.MaxQueuedMessagesPerRun);
        WriteNullable(builder, indent + 2, "maxExecutionSteps", limits.MaxExecutionSteps);
        WriteNullable(builder, indent + 2, "maxRegisterValues", limits.MaxRegisterValues);
        WriteNullable(builder, indent + 2, "maxLoopIterations", limits.MaxLoopIterations);
        WriteNullable(builder, indent + 2, "maxCallDepth", limits.MaxCallDepth);
        WriteNullable(builder, indent + 2, "maxRangeItems", limits.MaxRangeItems);
        WriteNullable(builder, indent + 2, "maxGeneratedCollectionItems", limits.MaxGeneratedCollectionItems);
        WriteNullable(builder, indent + 2, "maxDiceCount", limits.MaxDiceCount);
        WriteNullable(builder, indent + 2, "maxDiceSides", limits.MaxDiceSides);
    }

    private static void WriteNativeHandlers(StringBuilder builder, IReadOnlyList<GameEventScriptExternalSubscriberSpec>? handlers)
    {
        if (handlers is not { Count: > 0 }) return;
        builder.AppendLine("nativeHandlers:");
        foreach (var handler in handlers)
        {
            builder.Append("  - message: ").AppendLine(YamlString(handler.Message!));
            if (handler.Parameters is { Count: > 0 })
            {
                builder.Append("    parameters: ");
                WriteFlowStrings(builder, handler.Parameters);
                builder.AppendLine();
            }
            if (handler.Priority is not null) builder.Append("    priority: ").AppendLine(handler.Priority.Value.ToString(CultureInfo.InvariantCulture));
            if (handler.Throw) builder.AppendLine("    throw: true");
            if (handler.Emit is not { Count: > 0 }) continue;
            builder.AppendLine("    emit:");
            foreach (var emit in handler.Emit)
            {
                builder.Append("      - name: ").AppendLine(YamlString(emit.Name!));
                if (emit.ForwardArguments) builder.AppendLine("        forwardArguments: true");
                else if (emit.Args.ValueKind != JsonValueKind.Undefined) WriteJsonProperty(builder, 8, "args", emit.Args);
            }
        }
    }

    private static void WriteMessageApi(StringBuilder builder, GameEventScriptConformanceTest test)
    {
        if (test.Kind != "messageApi") return;
        builder.AppendLine("messageApi:");
        builder.AppendLine("  signature:");
        builder.Append("    name: ").AppendLine(YamlString(test.Signature!.Name!));
        builder.Append("    parameters: ");
        WriteFlowStrings(builder, test.Signature.Parameters ?? []);
        builder.AppendLine();
        WriteJsonProperty(builder, 2, "message", test.Message);
    }

    private static void WriteSourceDescriptors(StringBuilder builder, IReadOnlyList<MigratedSource> sources)
    {
        if (sources.Count == 0) return;
        builder.AppendLine("sources:");
        foreach (var source in sources)
        {
            builder.Append("  - name: ").AppendLine(YamlString(source.Name));
            builder.Append("    program: ").AppendLine(source.Program);
        }
    }

    private static void WriteSources(StringBuilder builder, IReadOnlyList<MigratedSource> sources)
    {
        foreach (var source in sources)
        {
            builder.AppendLine();
            builder.AppendLine("```ges");
            builder.Append(source.Text);
            if (!source.Text.EndsWith('\n')) builder.AppendLine();
            else builder.AppendLine();
            builder.AppendLine("```");
        }
    }

    private static void WriteStepsTable(StringBuilder builder, IReadOnlyList<GameEventScriptApiStepSpec>? steps)
    {
        if (steps is not { Count: > 0 }) return;
        builder.AppendLine();
        builder.AppendLine("### Steps");
        builder.AppendLine();
        builder.AppendLine("| step | receive | pump | budget |");
        builder.AppendLine("| --- | --- | --- | --- |");
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var receive = step.Input.GetProperty("name").GetString();
            var frames = step.OpcodeBudget is not null;
            builder.Append("| step-").Append((index + 1).ToString("D4", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(receive).Append(" | ").Append(frames ? "frames" : "completion").Append(" | ");
            if (frames) builder.Append(step.OpcodeBudget!.Value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(" |");
        }
    }

    private static void WriteExpectation(
        StringBuilder builder,
        GameEventScriptConformanceCase testCase,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> performance)
    {
        var test = testCase.Test;
        if (test.Kind == "bytecodeSnapshot") return;
        builder.AppendLine();
        builder.AppendLine("```yaml");
        builder.AppendLine("gesBlock: expect");
        switch (test.Kind)
        {
            case "scriptApi":
            case "performance":
                WriteRuntimeExpectation(builder, test);
                if (test.Kind == "performance") WritePerformanceExpectation(builder, testCase, performance);
                break;
            case "compileError":
            case "loadError":
                builder.AppendLine("error:");
                WriteDiagnostic(builder, 2, test.ExpectedError!);
                break;
            case "messageApi":
                WriteMessageExpectation(builder, test);
                break;
            case "compileMetadata":
                WriteMetadataExpectation(builder, test);
                break;
            case "bytecode":
                WriteOpcodeExpectation(builder, test.ExpectedOpcodes!);
                break;
            default:
                throw new InvalidOperationException("Unsupported legacy conformance kind '" + test.Kind + "'.");
        }
        builder.AppendLine("```");
    }

    private static void WriteRuntimeExpectation(StringBuilder builder, GameEventScriptConformanceTest test)
    {
        if (test.ExpectedInitializationPublished is not null || test.ExpectedInitializationOutboundPublished is not null)
        {
            builder.AppendLine("initialization:");
            WriteJsonListProperty(builder, 2, "local", test.ExpectedInitializationPublished);
            WriteJsonListProperty(builder, 2, "outbound", test.ExpectedInitializationOutboundPublished);
        }
        if (test.Steps is not { Count: > 0 }) return;
        builder.AppendLine("steps:");
        for (var index = 0; index < test.Steps.Count; index++)
        {
            var step = test.Steps[index];
            builder.Append("  step-").Append((index + 1).ToString("D4", CultureInfo.InvariantCulture)).AppendLine(":");
            var input = step.Input;
            var hasTags = input.TryGetProperty("tags", out var tags);
            var hasArgs = input.TryGetProperty("args", out var args);
            if (hasTags || hasArgs)
            {
                builder.AppendLine("    input:");
                if (hasTags) WriteJsonProperty(builder, 6, "tags", tags);
                if (hasArgs) WriteJsonProperty(builder, 6, "args", args);
            }
            WriteJsonListProperty(builder, 4, "local", step.ExpectedPublished);
            WriteJsonListProperty(builder, 4, "outbound", step.ExpectedOutboundPublished);
            if (step.ExpectedPaused is not null) builder.Append("    paused: ").AppendLine(step.ExpectedPaused.Value ? "true" : "false");
            if (step.ExpectedRuntimeLimits is { Count: > 0 } || step.UnexpectedRuntimeLimits is { Count: > 0 })
            {
                builder.AppendLine("    runtimeLimits:");
                WriteRuntimeLimitExpectations(builder, 6, "include", step.ExpectedRuntimeLimits);
                WriteRuntimeLimitExpectations(builder, 6, "exclude", step.UnexpectedRuntimeLimits);
            }
            if (step.ExpectedRuntimeDiagnostics is { Count: > 0 })
            {
                builder.AppendLine("    diagnostics:");
                foreach (var diagnostic in step.ExpectedRuntimeDiagnostics)
                {
                    builder.AppendLine("      - phase: " + YamlString(diagnostic.Phase!));
                    WriteDiagnostic(builder, 8, diagnostic, skipPhase: true);
                }
            }
        }
    }

    private static void WriteRuntimeLimitExpectations(StringBuilder builder, int indent, string name, IReadOnlyList<GameEventScriptRuntimeLimitExpectationSpec>? values)
    {
        if (values is not { Count: > 0 }) return;
        Indent(builder, indent).Append(name).AppendLine(":");
        foreach (var value in values)
        {
            var first = true;
            if (value.Name is null && value.DetailContains is null && value.Limit is null)
            {
                Indent(builder, indent + 2).AppendLine("- any: true");
                continue;
            }
            if (value.Name is not null)
            {
                Indent(builder, indent + 2).Append("- name: ").AppendLine(YamlString(value.Name));
                first = false;
            }
            if (value.DetailContains is not null)
            {
                Indent(builder, indent + 2).Append(first ? "- detailContains: " : "  detailContains: ").AppendLine(YamlString(value.DetailContains));
                first = false;
            }
            if (value.Limit is not null)
                Indent(builder, indent + 2).Append(first ? "- limit: " : "  limit: ").AppendLine(value.Limit.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void WriteDiagnostic(StringBuilder builder, int indent, GameEventScriptExpectedCompileErrorSpec value, bool skipPhase = false)
    {
        if (!skipPhase && value.Phase is not null) WriteString(builder, indent, "phase", value.Phase);
        if (value.Code is not null) WriteString(builder, indent, "code", value.Code);
        if (value.Symbol is not null) WriteString(builder, indent, "symbol", value.Symbol);
        if (value.SymbolKind is not null) WriteString(builder, indent, "symbolKind", LowerCamel(value.SymbolKind));
        if (value.ProgramName is not null) WriteString(builder, indent, "programName", value.ProgramName);
        if (value.HandlerName is not null) WriteString(builder, indent, "handlerName", value.HandlerName);
        if (value.SourceName is not null) WriteString(builder, indent, "sourceName", value.SourceName);
        WriteNullable(builder, indent, "line", value.Line);
        WriteNullable(builder, indent, "column", value.Column);
        WriteNullable(builder, indent, "endLine", value.EndLine);
        WriteNullable(builder, indent, "endColumn", value.EndColumn);
    }

    private static void WriteMessageExpectation(StringBuilder builder, GameEventScriptConformanceTest test)
    {
        builder.AppendLine("message:");
        if (test.ExpectedMessageName is not null) WriteString(builder, 2, "name", test.ExpectedMessageName);
        if (test.ExpectedSignatureId is not null) WriteString(builder, 2, "signatureId", test.ExpectedSignatureId);
        if (test.ExpectedMessageSignatureId is not null) WriteString(builder, 2, "messageSignatureId", test.ExpectedMessageSignatureId);
        if (test.ExpectedMatches is not null) Indent(builder, 2).Append("matches: ").AppendLine(test.ExpectedMatches.Value ? "true" : "false");
        WriteNullable(builder, 2, "argumentCount", test.ExpectedArgumentCount);
        if (test.ExpectedMessageError is not null) WriteString(builder, 2, "error", test.ExpectedMessageError);
    }

    private static void WriteMetadataExpectation(StringBuilder builder, GameEventScriptConformanceTest test)
    {
        builder.AppendLine("metadata:");
        if (test.ExpectedMessageDefinitions is { Count: > 0 })
        {
            builder.AppendLine("  messageDefinitions:");
            foreach (var value in test.ExpectedMessageDefinitions)
            {
                builder.Append("    - name: ").AppendLine(YamlString(value.Name!));
                if (value.SignatureIds is not null)
                {
                    builder.Append("      signatureIds: ");
                    WriteFlowStrings(builder, value.SignatureIds);
                    builder.AppendLine();
                }
                WriteNullable(builder, 6, "count", value.Count);
            }
        }
        if (test.ExpectedProgramResources is not null)
        {
            builder.AppendLine("  programResources:");
            WriteNullable(builder, 4, "requiredRegisterCount", test.ExpectedProgramResources.RequiredRegisterCount);
            WriteNullable(builder, 4, "requiredCallStackDepth", test.ExpectedProgramResources.RequiredCallStackDepth);
        }
        if (test.ExpectedHandlerResources is not { Count: > 0 }) return;
        builder.AppendLine("  handlerResources:");
        foreach (var value in test.ExpectedHandlerResources)
        {
            builder.Append("    - name: ").AppendLine(YamlString(value.Name!));
            if (value.SignatureId is not null) WriteString(builder, 6, "signatureId", value.SignatureId);
            WriteNullable(builder, 6, "requiredRegisterCount", value.RequiredRegisterCount);
            WriteNullable(builder, 6, "requiredCallStackDepth", value.RequiredCallStackDepth);
        }
    }

    private static void WriteOpcodeExpectation(StringBuilder builder, GameEventScriptBytecodeOpcodeExpectationSpec value)
    {
        builder.AppendLine("opcodes:");
        WriteStringList(builder, 2, "contains", value.Contains);
        WriteStringList(builder, 2, "excludes", value.NotContains);
        WriteIntMap(builder, 2, "counts", value.Counts);
        WriteIntMap(builder, 2, "minimumCounts", value.MinCounts);
    }

    private static void WritePerformanceExpectation(
        StringBuilder builder,
        GameEventScriptConformanceCase testCase,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> performance)
    {
        var key = testCase.SuiteName + "/" + testCase.Test.Name;
        if (!performance.TryGetValue(key, out var metrics)) throw new InvalidOperationException("Missing performance reference for '" + key + "'.");
        builder.AppendLine("performance:");
        builder.AppendLine("  profiles:");
        builder.Append("    ").Append(PerformanceProfile).AppendLine(":");
        builder.AppendLine("      metrics:");
        foreach (var definition in PerformanceMetrics)
        {
            var reference = metrics[definition.LegacyKey];
            builder.Append("        ").Append(definition.Id).AppendLine(":");
            builder.Append("          reference: ").AppendLine(reference);
            if (definition.IsAllocation)
                builder.Append("          maximum: ").AppendLine(reference);
            else
            {
                var numeric = double.Parse(reference, NumberStyles.Float, CultureInfo.InvariantCulture);
                var tolerance = Math.Max(numeric * 0.15d, 1d);
                builder.Append("          toleranceAbsolute: ").AppendLine(tolerance.ToString("0.######", CultureInfo.InvariantCulture));
            }
            builder.Append("          unit: ").AppendLine(definition.Unit);
        }
    }

    private static IReadOnlyList<MigratedSource> GetSources(GameEventScriptConformanceTest test)
    {
        if (test.Scripts is { Count: > 0 })
            return test.Scripts.Select(source => new MigratedSource(source.SourceName ?? test.Name + ".ges", "main", source.Text!)).ToArray();
        if (test.Programs is { Count: > 0 })
            return test.Programs.Select((source, index) => new MigratedSource(source.SourceName ?? test.Name + ".ges", "program-" + (index + 1).ToString("D4", CultureInfo.InvariantCulture), source.Text!)).ToArray();
        if (test.Script is not null) return [new MigratedSource(test.Name + ".ges", "main", test.Script)];
        return [];
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadPerformanceReference(string path)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        Dictionary<string, string>? current = null;
        foreach (var rawLine in File.ReadAllText(path).ReplaceLineEndings("\n").Split('\n'))
        {
            if (rawLine.StartsWith("## ", StringComparison.Ordinal))
            {
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                result.Add(rawLine[3..], current);
                continue;
            }
            if (current is null) continue;
            var separator = rawLine.IndexOf('=');
            if (separator > 0) current[rawLine[..separator]] = rawLine[(separator + 1)..];
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> ReadBinaryDumpReference(string path)
    {
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        const string marker = "//  Performance Case: ";
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var search = 0;
        while (true)
        {
            var markerIndex = text.IndexOf(marker, search, StringComparison.Ordinal);
            if (markerIndex < 0) break;
            var nameStart = markerIndex + marker.Length;
            var nameEnd = text.IndexOf('\n', nameStart);
            var bodyStart = text.IndexOf("\n\n", nameEnd, StringComparison.Ordinal) + 2;
            var next = text.IndexOf("\n\n// -------------------------------------------------------------------------------\n//  Performance Case: ", bodyStart, StringComparison.Ordinal);
            var bodyEnd = next < 0 ? text.Length : next;
            var body = text[bodyStart..bodyEnd].TrimEnd('\n') + "\n";
            result.Add(text[nameStart..nameEnd], body);
            search = bodyEnd;
        }
        return result;
    }

    private static void WriteJsonListProperty(StringBuilder builder, int indent, string name, IReadOnlyList<JsonElement>? values)
    {
        if (values is not { Count: > 0 }) return;
        using var document = JsonDocument.Parse("[" + string.Join(",", values.Select(value => value.GetRawText())) + "]");
        WriteJsonProperty(builder, indent, name, document.RootElement);
    }

    private static void WriteJsonProperty(StringBuilder builder, int indent, string name, JsonElement value)
    {
        if (string.Equals(name, "entries", StringComparison.Ordinal) && value.ValueKind == JsonValueKind.Object)
        {
            WriteEntriesProperty(builder, indent, value);
            return;
        }
        Indent(builder, indent).Append(name).Append(':');
        if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0) { builder.AppendLine(" []"); return; }
        if (value.ValueKind == JsonValueKind.Object && !value.EnumerateObject().Any()) { builder.AppendLine(" {}"); return; }
        if (IsScalar(value)) { builder.Append(' ').AppendLine(YamlScalar(value)); return; }
        builder.AppendLine();
        WriteJsonNode(builder, indent + 2, value);
    }

    private static void WriteJsonNode(StringBuilder builder, int indent, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var hasType = value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String;
            var typeName = hasType ? type.GetString() : null;
            var isNothing = string.Equals(typeName, ":nothing", StringComparison.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (isNothing && string.Equals(property.Name, "value", StringComparison.Ordinal)) continue;
                if (IsBinary64Field(typeName, property.Name) && property.Value.ValueKind == JsonValueKind.String)
                {
                    WriteString(builder, indent, property.Name, NormalizeBinary64(property.Value.GetString()!));
                    continue;
                }
                WriteJsonProperty(builder, indent, property.Name, property.Value);
            }
            return;
        }
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Expected JSON container.");
        foreach (var item in value.EnumerateArray())
        {
            if (IsScalar(item)) { Indent(builder, indent).Append("- ").AppendLine(YamlScalar(item)); continue; }
            if (item.ValueKind == JsonValueKind.Object)
            {
                var properties = item.EnumerateObject().ToArray();
                if (properties.Length == 0) { Indent(builder, indent).AppendLine("- {}"); continue; }
                var itemType = item.TryGetProperty("type", out var typeProperty) && typeProperty.ValueKind == JsonValueKind.String
                    ? typeProperty.GetString()
                    : null;
                var first = properties[0];
                Indent(builder, indent).Append("- ").Append(first.Name).Append(':');
                if (IsScalar(first.Value)) builder.Append(' ').AppendLine(YamlScalar(first.Value));
                else { builder.AppendLine(); WriteJsonNode(builder, indent + 4, first.Value); }
                for (var index = 1; index < properties.Length; index++)
                {
                    var property = properties[index];
                    if (string.Equals(itemType, ":nothing", StringComparison.Ordinal) && property.Name == "value") continue;
                    if (IsBinary64Field(itemType, property.Name) && property.Value.ValueKind == JsonValueKind.String)
                        WriteString(builder, indent + 2, property.Name, NormalizeBinary64(property.Value.GetString()!));
                    else
                        WriteJsonProperty(builder, indent + 2, property.Name, property.Value);
                }
                continue;
            }
            Indent(builder, indent).AppendLine("-");
            WriteJsonNode(builder, indent + 2, item);
        }
    }

    private static bool IsScalar(JsonElement value)
        => value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;

    private static string YamlScalar(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => YamlString(value.GetString()!),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => throw new InvalidOperationException("Expected JSON scalar.")
        };

    private static void WriteEntriesProperty(StringBuilder builder, int indent, JsonElement entries)
    {
        var properties = entries.EnumerateObject().ToArray();
        if (properties.Length == 0)
        {
            Indent(builder, indent).AppendLine("entries: []");
            return;
        }
        Indent(builder, indent).AppendLine("entries:");
        foreach (var property in properties)
        {
            Indent(builder, indent + 2).Append("- key: ").AppendLine(YamlString(property.Name));
            WriteJsonProperty(builder, indent + 4, "value", property.Value);
        }
    }

    private static string YamlString(string value) => JsonSerializer.Serialize(value);

    private static void WriteFlowStrings(StringBuilder builder, IReadOnlyList<string> values)
        => builder.Append('[').Append(string.Join(", ", values.Select(YamlString))).Append(']');

    private static void WriteStringList(StringBuilder builder, int indent, string name, IReadOnlyList<string>? values)
    {
        if (values is not { Count: > 0 }) return;
        Indent(builder, indent).Append(name).Append(": ");
        WriteFlowStrings(builder, values);
        builder.AppendLine();
    }

    private static void WriteIntMap(StringBuilder builder, int indent, string name, IReadOnlyDictionary<string, int>? values)
    {
        if (values is not { Count: > 0 }) return;
        Indent(builder, indent).Append(name).AppendLine(":");
        foreach (var pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            Indent(builder, indent + 2).Append(pair.Key).Append(": ").AppendLine(pair.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteString(StringBuilder builder, int indent, string name, string value)
        => Indent(builder, indent).Append(name).Append(": ").AppendLine(YamlString(value));

    private static void WriteNullable(StringBuilder builder, int indent, string name, int? value)
    {
        if (value is not null) Indent(builder, indent).Append(name).Append(": ").AppendLine(value.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static StringBuilder Indent(StringBuilder builder, int count) => builder.Append(' ', count);

    private static string LowerCamel(string value)
        => value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static bool IsBinary64Field(string? type, string property)
        => type switch
        {
            ":float" or ":percentage" => property == "value",
            ":vector" or ":point" => property is "x" or "y" or "z",
            ":range" => property is "from" or "to" or "step",
            _ => false
        };

    private static string NormalizeBinary64(string value)
    {
        var parsed = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (parsed == 0d) return "0";
        var text = parsed.ToString("R", CultureInfo.InvariantCulture);
        var exponent = text.IndexOf('E');
        if (exponent < 0) return text;
        var exponentValue = int.Parse(text[(exponent + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        return text[..exponent] + "e" + exponentValue.ToString(CultureInfo.InvariantCulture);
    }

    private static string ResolveTitle(string relativeFile, string caseId, string title)
        => string.Equals(relativeFile, "runtime/atomic/member-index-access.json", StringComparison.Ordinal) &&
           string.Equals(caseId, "case-0018", StringComparison.Ordinal) &&
           string.Equals(title, "message member and index access", StringComparison.Ordinal)
            ? "message handler member and index access"
            : title;

    private static readonly PerformanceMetricDefinition[] PerformanceMetrics =
    [
        new("ast-build.elapsed", "astBuild.elapsedMs", "ms", false),
        new("ast-build.allocated", "astBuild.allocatedKb", "KiB", true),
        new("binary-build.elapsed", "binaryBuild.elapsedMs", "ms", false),
        new("binary-build.allocated", "binaryBuild.allocatedKb", "KiB", true),
        new("program-load.elapsed", "programLoad.elapsedMs", "ms", false),
        new("program-load.allocated", "programLoad.allocatedKb", "KiB", true),
        new("compile.elapsed", "compile.elapsedMs", "ms", false),
        new("compile.allocated", "compile.allocatedKb", "KiB", true),
        new("run.elapsed", "run.elapsedMs", "ms", false),
        new("run.allocated", "run.allocatedKb", "KiB", true),
        new("run.per-invoke-elapsed", "run.perInvokeElapsedMs", "ms", false),
        new("run.per-invoke-allocated", "run.perInvokeAllocatedKb", "KiB", true)
    ];

    internal sealed record MigratedSuite(string RelativePath, string Markdown, int LegacyCaseCount, int SnapshotCount);
    private sealed record MigratedSource(string Name, string Program, string Text);
    private sealed record PerformanceMetricDefinition(string Id, string LegacyKey, string Unit, bool IsAllocation);
}
