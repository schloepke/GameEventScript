#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Globalization;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Conformance;

public static class ConformanceRunner
{
    public static ConformanceCaseResult RunCase(
        ConformanceDocument document,
        string caseId,
        ConformanceRunnerEnvironment environment,
        ConformanceRunnerOptions? options = null,
        IConformanceResultSink? resultSink = null)
    {
        _ = document ?? throw new ArgumentNullException(nameof(document));
        _ = caseId ?? throw new ArgumentNullException(nameof(caseId));
        _ = environment ?? throw new ArgumentNullException(nameof(environment));
        for (var index = 0; index < document.Cases.Count; index++)
        {
            var testCase = document.Cases[index];
            if (!string.Equals(testCase.Id, caseId, StringComparison.Ordinal) && !string.Equals(testCase.FullId, caseId, StringComparison.Ordinal)) continue;
            var result = RunSelectedCase(testCase, environment, options ?? ConformanceRunnerOptions.Default);
            resultSink?.CaseCompleted(result);
            return result;
        }
        throw new ArgumentException($"Unknown conformance case '{caseId}'.", nameof(caseId));
    }

    public static ConformanceRunReport RunDocument(
        ConformanceDocument document,
        ConformanceRunnerEnvironment environment,
        ConformanceRunnerOptions? options = null,
        IConformanceResultSink? resultSink = null)
    {
        _ = document ?? throw new ArgumentNullException(nameof(document));
        return RunCorpus(new[] { document }, environment, options, resultSink);
    }

    public static ConformanceRunReport RunCorpus(
        IReadOnlyList<ConformanceDocument> documents,
        ConformanceRunnerEnvironment environment,
        ConformanceRunnerOptions? options = null,
        IConformanceResultSink? resultSink = null)
    {
        _ = documents ?? throw new ArgumentNullException(nameof(documents));
        _ = environment ?? throw new ArgumentNullException(nameof(environment));
        var resolvedOptions = options ?? ConformanceRunnerOptions.Default;
        _ = resolvedOptions.Limits ?? throw new ArgumentException("Runner options require limits.", nameof(options));
        var corpusError = ValidateCorpus(documents, resolvedOptions.Limits);
        var results = new List<ConformanceCaseResult>();
        if (corpusError is not null)
        {
            for (var documentIndex = 0; documentIndex < documents.Count; documentIndex++)
            {
                var invalidDocument = documents[documentIndex] ?? throw new ArgumentException("The corpus contains a null document.", nameof(documents));
                for (var caseIndex = 0; caseIndex < invalidDocument.Cases.Count; caseIndex++)
                {
                    var invalidResult = Result(invalidDocument.Cases[caseIndex], ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidModel, technical: corpusError);
                    results.Add(invalidResult);
                    resultSink?.CaseCompleted(invalidResult);
                }
            }
            return CreateReport(environment, results);
        }
        for (var documentIndex = 0; documentIndex < documents.Count; documentIndex++)
        {
            var document = documents[documentIndex] ?? throw new ArgumentException("The corpus contains a null document.", nameof(documents));
            for (var caseIndex = 0; caseIndex < document.Cases.Count; caseIndex++)
            {
                var result = RunSelectedCase(document.Cases[caseIndex], environment, resolvedOptions);
                results.Add(result);
                resultSink?.CaseCompleted(result);
            }
        }
        return CreateReport(environment, results);
    }

    private static ConformanceCaseResult RunSelectedCase(ConformanceCase testCase, ConformanceRunnerEnvironment environment, ConformanceRunnerOptions options)
    {
        if (options.Limits is null || options.Limits.MaxCases <= 0 || options.Limits.MaxFramesPerStep <= 0)
            return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidEnvironment, technical: "Runner limits must be positive.");
        var environmentError = ValidateEnvironment(environment);
        if (environmentError is not null) return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidEnvironment, technical: environmentError);

        var missingCore = Missing(testCase.Requires.Core, environment.Capabilities);
        if (missingCore.Count > 0) return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.MissingCoreCapability, missing: missingCore);
        var missingOptional = Missing(testCase.Requires.Optional, environment.Capabilities);
        if (missingOptional.Count > 0) return Result(testCase, ConformanceCaseStatus.Skipped, ConformanceRunnerCodes.MissingOptionalCapability, missing: missingOptional);

        try
        {
            return testCase.Kind switch
            {
                ConformanceTestKind.ScriptApi => RunRuntime(testCase, environment, options, measurePerformance: false),
                ConformanceTestKind.Performance => RunRuntime(testCase, environment, options, measurePerformance: true),
                ConformanceTestKind.CompileError => RunCompileError(testCase, environment),
                ConformanceTestKind.LoadError => RunLoadError(testCase, environment),
                ConformanceTestKind.MessageApi => RunMessageApi(testCase),
                ConformanceTestKind.CompileMetadata => RunCompileMetadata(testCase, environment),
                ConformanceTestKind.Bytecode => RunBytecode(testCase, environment),
                ConformanceTestKind.BytecodeSnapshot => RunBytecodeSnapshot(testCase, environment, options),
                _ => Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidModel, technical: "Unsupported test kind.")
            };
        }
        catch (Exception exception)
        {
            return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.UnhandledException,
                technical: options.IncludeTechnicalDetails ? exception.GetType().Name + ": " + exception.Message : null);
        }
    }

    private static ConformanceCaseResult RunRuntime(ConformanceCase testCase, ConformanceRunnerEnvironment environment, ConformanceRunnerOptions options, bool measurePerformance)
    {
        IReadOnlyList<GameEventScriptProgram> programs;
        try { programs = CompilePrograms(testCase, environment); }
        catch (GameEventScriptCompileException exception) { return UnexpectedDiagnostics(testCase, "compile", exception.Diagnostics); }

        var collector = new RuntimeCollector();
        var builder = GameEventScriptHost.CreateBuilder()
            .WithRandom(CreateRandom(testCase.Random))
            .WithRuntimeLimits(CreateRuntimeLimits(testCase.RuntimeLimits))
            .WithRuntimeObserver(collector)
            .WithPublishSink(collector);
        if (environment.ExtensionRegistry is not null) builder.WithRegistry(environment.ExtensionRegistry);
        if (environment.ExternalTypeRegistry is not null) builder.WithExternalTypeRegistry(environment.ExternalTypeRegistry);
        var host = builder.Build();
        try
        {
            for (var index = 0; index < programs.Count; index++) host.Load(programs[index]);
        }
        catch (GameEventScriptDynamicLinkException exception)
        {
            return UnexpectedDiagnostics(testCase, "link", new[] { exception.Diagnostic });
        }

        for (var index = 0; index < testCase.NativeHandlers.Count; index++) RegisterNative(host, testCase.NativeHandlers[index]);

        var mismatches = new List<ConformanceMismatch>();
        host.RunToCompletion();
        CompareChannel("/initialization", testCase.Expectation.Initialization.Local, collector.Local, testCase.Comparison, mismatches);
        CompareChannel("/initialization/outbound", testCase.Expectation.Initialization.Outbound, collector.Outbound, testCase.Comparison, mismatches);
        CompareObservations("/initialization", testCase.Expectation.Initialization.Observations, collector, mismatches);

        for (var stepIndex = 0; stepIndex < testCase.Steps.Count; stepIndex++)
        {
            var step = testCase.Steps[stepIndex];
            collector.Clear();
            var accepted = host.Receive(ConformanceRuntimeValueCodec.DecodeMessage(step.Expectation.Input));
            var paused = false;
            if (step.Pump == ConformancePumpMode.Completion)
            {
                var execution = host.RunToCompletion();
                paused = execution.State == GameEventScriptExecutionState.Paused;
            }
            else
            {
                var frames = 0;
                GameEventScriptExecutionResult execution;
                do
                {
                    if (++frames > options.Limits.MaxFramesPerStep)
                        return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidEnvironment, diagnostics: collector.AllDiagnostics, runtimeLimits: collector.AllLimitResults, technical: "MaxFramesPerStep was exceeded.");
                    execution = host.ExecuteFrame(checked((int)step.Budget!.Value));
                    if (execution.State == GameEventScriptExecutionState.Paused) paused = true;
                }
                while (execution.State == GameEventScriptExecutionState.Paused);
            }

            var path = "/steps/" + step.Id;
            if (accepted != step.Expectation.Accepted) AddMismatch(mismatches, path + "/accepted", step.Expectation.Accepted ? "true" : "false", accepted ? "true" : "false");
            if (step.Expectation.Paused is { } expectedPaused && expectedPaused != paused) AddMismatch(mismatches, path + "/paused", expectedPaused ? "true" : "false", paused ? "true" : "false");
            CompareChannel(path + "/local", step.Expectation.Local, collector.Local, testCase.Comparison, mismatches);
            CompareChannel(path + "/outbound", step.Expectation.Outbound, collector.Outbound, testCase.Comparison, mismatches);
            CompareObservations(path, step.Expectation.Observations, collector, mismatches);
        }

        if (mismatches.Count > 0) return Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch, mismatches: mismatches, diagnostics: collector.AllDiagnostics, runtimeLimits: collector.AllLimitResults);
        if (!measurePerformance) return Result(testCase, ConformanceCaseStatus.Passed, ConformanceRunnerCodes.Passed, diagnostics: collector.AllDiagnostics, runtimeLimits: collector.AllLimitResults);
        return MeasurePerformance(testCase, environment);
    }

    private static ConformanceCaseResult MeasurePerformance(ConformanceCase testCase, ConformanceRunnerEnvironment environment)
    {
        if (environment.PerformanceProfileId is null || environment.PerformanceProvider is null)
            return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidEnvironment, technical: "Performance capability requires a profile and provider.");
        ConformancePerformanceProfile? expectedProfile = null;
        for (var index = 0; index < testCase.Expectation.Performance!.Profiles.Count; index++)
            if (string.Equals(testCase.Expectation.Performance.Profiles[index].Id, environment.PerformanceProfileId, StringComparison.Ordinal)) expectedProfile = testCase.Expectation.Performance.Profiles[index];
        if (expectedProfile is null) return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.MissingPerformanceProfile);

        var measured = environment.PerformanceProvider.Measure(testCase, environment.PerformanceProfileId);
        var measuredIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < measured.Metrics.Count; index++)
            if (!measuredIds.Add(measured.Metrics[index].Id)) return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidEnvironment, technical: "Performance provider returned a duplicate metric.");
        if (measured.Metrics.Count != expectedProfile.Metrics.Count)
            return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidEnvironment, technical: "Performance provider returned missing or unexpected metrics.");
        var metricResults = new List<ConformancePerformanceMetricResult>();
        var mismatches = new List<ConformanceMismatch>();
        for (var index = 0; index < expectedProfile.Metrics.Count; index++)
        {
            var expected = expectedProfile.Metrics[index];
            ConformanceMeasuredMetric? actual = null;
            for (var measuredIndex = 0; measuredIndex < measured.Metrics.Count; measuredIndex++)
                if (string.Equals(measured.Metrics[measuredIndex].Id, expected.Id, StringComparison.Ordinal)) actual = measured.Metrics[measuredIndex];
            if (actual is null || !string.Equals(actual.Unit, expected.Unit, StringComparison.Ordinal))
                return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidEnvironment, technical: "Performance provider returned a missing metric or mismatched unit.");
            if (!double.TryParse(actual.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value < 0)
                return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidEnvironment, technical: "Performance provider returned an invalid metric.");
            if (!string.Equals(actual.Value, ConformanceRuntimeValueCodec.FormatBinary64(value), StringComparison.Ordinal))
                return Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.InvalidEnvironment, technical: "Performance provider returned a non-canonical Binary64 metric.");
            var reference = ConformanceRuntimeValueCodec.ParseBinary64(expected.Reference);
            var allowed = double.PositiveInfinity;
            if (expected.Maximum is not null) allowed = Math.Min(allowed, ConformanceRuntimeValueCodec.ParseBinary64(expected.Maximum));
            if (expected.ToleranceRelative is not null) allowed = Math.Min(allowed, reference * (1d + ConformanceRuntimeValueCodec.ParseBinary64(expected.ToleranceRelative)));
            if (expected.ToleranceAbsolute is not null) allowed = Math.Min(allowed, reference + ConformanceRuntimeValueCodec.ParseBinary64(expected.ToleranceAbsolute));
            var passed = value <= allowed;
            metricResults.Add(new ConformancePerformanceMetricResult(expected.Id, ConformanceRuntimeValueCodec.FormatBinary64(value), expected.Reference, ConformanceRuntimeValueCodec.FormatBinary64(allowed), expected.Unit, passed));
            if (!passed) AddMismatch(mismatches, "/performance/" + expected.Id, "<= " + ConformanceRuntimeValueCodec.FormatBinary64(allowed), ConformanceRuntimeValueCodec.FormatBinary64(value));
        }
        var performance = new ConformancePerformanceResult(environment.PerformanceProfileId, metricResults);
        return mismatches.Count == 0
            ? Result(testCase, ConformanceCaseStatus.Passed, ConformanceRunnerCodes.Passed, performance: performance)
            : Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.PerformanceRegression, mismatches: mismatches, performance: performance);
    }

    private static ConformanceCaseResult RunCompileError(ConformanceCase testCase, ConformanceRunnerEnvironment environment)
    {
        try { _ = CompilePrograms(testCase, environment); }
        catch (GameEventScriptCompileException exception)
        {
            for (var index = 0; index < exception.Diagnostics.Count; index++)
                if (DiagnosticMatches(testCase.Expectation.Error!, exception.Diagnostics[index])) return Result(testCase, ConformanceCaseStatus.Passed, ConformanceRunnerCodes.Passed, diagnostics: ConvertDiagnostics(exception.Diagnostics));
            return Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch, mismatches: new[] { DiagnosticMismatch("/error", testCase.Expectation.Error!, exception.Diagnostics) }, diagnostics: ConvertDiagnostics(exception.Diagnostics));
        }
        return Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.ExpectedCompileError, mismatches: new[] { new ConformanceMismatch("/error", ConformanceRunnerCodes.ExpectedCompileError, testCase.Expectation.Error!.Code, null) });
    }

    private static ConformanceCaseResult RunLoadError(ConformanceCase testCase, ConformanceRunnerEnvironment environment)
    {
        GameEventScriptProgram program;
        try { program = CompileSingleProgram(testCase, environment); }
        catch (GameEventScriptCompileException exception) { return UnexpectedDiagnostics(testCase, "compile", exception.Diagnostics); }
        var builder = GameEventScriptHost.CreateBuilder().WithRuntimeLimits(CreateRuntimeLimits(testCase.RuntimeLimits));
        if (environment.ExtensionRegistry is not null) builder.WithRegistry(environment.ExtensionRegistry);
        if (environment.ExternalTypeRegistry is not null) builder.WithExternalTypeRegistry(environment.ExternalTypeRegistry);
        var host = builder.Build();
        try { host.Load(program); }
        catch (GameEventScriptDynamicLinkException exception)
        {
            var diagnostic = exception.Diagnostic;
            return DiagnosticMatches(testCase.Expectation.Error!, diagnostic)
                ? Result(testCase, ConformanceCaseStatus.Passed, ConformanceRunnerCodes.Passed, diagnostics: ConvertDiagnostics(new[] { diagnostic }))
                : Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch, mismatches: new[] { DiagnosticMismatch("/error", testCase.Expectation.Error!, new[] { diagnostic }) }, diagnostics: ConvertDiagnostics(new[] { diagnostic }));
        }
        return Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.ExpectedLoadError, mismatches: new[] { new ConformanceMismatch("/error", ConformanceRunnerCodes.ExpectedLoadError, testCase.Expectation.Error!.Code, null) });
    }

    private static ConformanceCaseResult RunMessageApi(ConformanceCase testCase)
    {
        var expected = testCase.Expectation.MessageApi!;
        try
        {
            var definition = testCase.MessageApi!;
            if (definition.ArgumentsWereMapping)
            {
                const string code = "invalidArgumentsShape";
                return string.Equals(code, expected.Error, StringComparison.Ordinal)
                    ? Result(testCase, ConformanceCaseStatus.Passed, ConformanceRunnerCodes.Passed)
                    : Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch,
                        mismatches: new[] { new ConformanceMismatch("/message/error", ConformanceRunnerCodes.AssertionMismatch, expected.Error, code) });
            }
            var signature = GameEventScriptMessageSignature.Create(definition.SignatureName, definition.Parameters);
            var message = ConformanceRuntimeValueCodec.DecodeMessage(definition.Message);
            if (expected.Error is not null) return Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch, mismatches: new[] { new ConformanceMismatch("/message/error", ConformanceRunnerCodes.AssertionMismatch, expected.Error, null) });
            var mismatches = new List<ConformanceMismatch>();
            CheckOptional(mismatches, "/message/name", expected.Name, message.Name);
            CheckOptional(mismatches, "/message/signatureId", expected.SignatureId, signature.SignatureId);
            CheckOptional(mismatches, "/message/messageSignatureId", expected.MessageSignatureId, message.SignatureId);
            if (expected.Matches is { } matches && matches != signature.Matches(message)) AddMismatch(mismatches, "/message/matches", matches ? "true" : "false", signature.Matches(message) ? "true" : "false");
            if (expected.ArgumentCount is { } count && count != message.Arguments.Count) AddMismatch(mismatches, "/message/argumentCount", count.ToString(CultureInfo.InvariantCulture), message.Arguments.Count.ToString(CultureInfo.InvariantCulture));
            return mismatches.Count == 0 ? Result(testCase, ConformanceCaseStatus.Passed, ConformanceRunnerCodes.Passed) : Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch, mismatches: mismatches);
        }
        catch (ArgumentException exception)
        {
            var code = MessageErrorCode(exception.Message);
            return string.Equals(code, expected.Error, StringComparison.Ordinal)
                ? Result(testCase, ConformanceCaseStatus.Passed, ConformanceRunnerCodes.Passed)
                : Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch, mismatches: new[] { new ConformanceMismatch("/message/error", ConformanceRunnerCodes.AssertionMismatch, expected.Error, code) });
        }
    }

    private static ConformanceCaseResult RunCompileMetadata(ConformanceCase testCase, ConformanceRunnerEnvironment environment)
    {
        GameEventScriptProgram program;
        try { program = CompileSingleProgram(testCase, environment); }
        catch (GameEventScriptCompileException exception) { return UnexpectedDiagnostics(testCase, "compile", exception.Diagnostics); }
        var expected = testCase.Expectation.Metadata!;
        var mismatches = new List<ConformanceMismatch>();
        for (var expectedIndex = 0; expectedIndex < expected.MessageDefinitions.Count; expectedIndex++)
        {
            var definition = expected.MessageDefinitions[expectedIndex];
            var signatures = new List<string>();
            for (var bindIndex = 0; bindIndex < program.Bindings.Entries.Count; bindIndex++)
            {
                var bind = program.Bindings.Entries[bindIndex];
                if (bind.Kind is not (GameEventScriptBinaryBindKind.MessageHandler or GameEventScriptBinaryBindKind.MessageNameHandler)) continue;
                var name = program.StringConstants.Resolve(bind.Name);
                if (!string.Equals(name, definition.Name, StringComparison.Ordinal)) continue;
                var parameters = new string[bind.ArgumentNames.Count];
                for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++) parameters[parameterIndex] = program.StringConstants.Resolve(bind.ArgumentNames[parameterIndex]);
                signatures.Add(GameEventScriptMessageSignature.CreateSignatureId(name, parameters));
            }
            if ((uint)signatures.Count != definition.Count) AddMismatch(mismatches, "/metadata/messageDefinitions/" + definition.Name + "/count", definition.Count.ToString(CultureInfo.InvariantCulture), signatures.Count.ToString(CultureInfo.InvariantCulture));
            if (!StringListsEqual(definition.SignatureIds, signatures)) AddMismatch(mismatches, "/metadata/messageDefinitions/" + definition.Name + "/signatureIds", Join(definition.SignatureIds), Join(signatures));
        }
        if (expected.ProgramResources is { } resources)
        {
            CheckOptional(mismatches, "/metadata/programResources/requiredRegisterCount", resources.RequiredRegisterCount, program.RequiredRegisterCount);
            CheckOptional(mismatches, "/metadata/programResources/requiredCallStackDepth", resources.RequiredCallStackDepth, program.RequiredCallStackDepth);
        }
        for (var expectedIndex = 0; expectedIndex < expected.HandlerResources.Count; expectedIndex++) CompareHandlerResource(program, expected.HandlerResources[expectedIndex], mismatches);
        return mismatches.Count == 0 ? Result(testCase, ConformanceCaseStatus.Passed, ConformanceRunnerCodes.Passed) : Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch, mismatches: mismatches);
    }

    private static ConformanceCaseResult RunBytecode(ConformanceCase testCase, ConformanceRunnerEnvironment environment)
    {
        GameEventScriptProgram program;
        try { program = CompileSingleProgram(testCase, environment); }
        catch (GameEventScriptCompileException exception) { return UnexpectedDiagnostics(testCase, "compile", exception.Diagnostics); }
        var counts = new Dictionary<string, ulong>(StringComparer.Ordinal);
        for (var index = 0; index < program.Code.Count; index++)
        {
            var name = program.Code[index].OpCode.ToString();
            counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
        }
        var expected = testCase.Expectation.Opcodes!;
        var mismatches = new List<ConformanceMismatch>();
        for (var index = 0; index < expected.Contains.Count; index++) if (!counts.ContainsKey(expected.Contains[index])) AddMismatch(mismatches, "/opcodes/contains/" + expected.Contains[index], "> 0", "0");
        for (var index = 0; index < expected.Excludes.Count; index++) if (counts.TryGetValue(expected.Excludes[index], out var count)) AddMismatch(mismatches, "/opcodes/excludes/" + expected.Excludes[index], "0", count.ToString(CultureInfo.InvariantCulture));
        foreach (var pair in expected.Counts) if (!counts.TryGetValue(pair.Key, out var actual) || actual != pair.Value) AddMismatch(mismatches, "/opcodes/counts/" + pair.Key, pair.Value.ToString(CultureInfo.InvariantCulture), actual.ToString(CultureInfo.InvariantCulture));
        foreach (var pair in expected.MinimumCounts) if (!counts.TryGetValue(pair.Key, out var actual) || actual < pair.Value) AddMismatch(mismatches, "/opcodes/minimumCounts/" + pair.Key, ">= " + pair.Value.ToString(CultureInfo.InvariantCulture), actual.ToString(CultureInfo.InvariantCulture));
        return mismatches.Count == 0 ? Result(testCase, ConformanceCaseStatus.Passed, ConformanceRunnerCodes.Passed) : Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch, mismatches: mismatches);
    }

    private static ConformanceCaseResult RunBytecodeSnapshot(ConformanceCase testCase, ConformanceRunnerEnvironment environment, ConformanceRunnerOptions options)
    {
        GameEventScriptProgram program;
        try { program = CompileSingleProgram(testCase, environment); }
        catch (GameEventScriptCompileException exception) { return UnexpectedDiagnostics(testCase, "compile", exception.Diagnostics); }
        var actual = NormalizeLf(program.Dump());
        var expected = NormalizeLf(testCase.ExpectedAssembler!);
        if (string.Equals(expected, actual, StringComparison.Ordinal)) return Result(testCase, ConformanceCaseStatus.Passed, ConformanceRunnerCodes.Passed, actualAssembler: options.IncludeActualAssemblerOnSuccess ? actual : null);
        var offset = FirstUtf8Difference(expected, actual);
        return Result(testCase, ConformanceCaseStatus.Failed, ConformanceRunnerCodes.AssertionMismatch,
            mismatches: new[] { new ConformanceMismatch("/assembler", ConformanceRunnerCodes.AssertionMismatch, "first difference after UTF-8 byte " + offset.ToString(CultureInfo.InvariantCulture), "snapshot differs") }, actualAssembler: actual);
    }

    private static IReadOnlyList<GameEventScriptProgram> CompilePrograms(ConformanceCase testCase, ConformanceRunnerEnvironment environment)
    {
        var groups = new List<SourceGroup>();
        for (var sourceIndex = 0; sourceIndex < testCase.Sources.Count; sourceIndex++)
        {
            var source = testCase.Sources[sourceIndex];
            SourceGroup? group = null;
            for (var index = 0; index < groups.Count; index++) if (string.Equals(groups[index].Id, source.ProgramId, StringComparison.Ordinal)) group = groups[index];
            if (group is null) { group = new SourceGroup(source.ProgramId); groups.Add(group); }
            group.Sources.Add(source);
        }
        var programs = new GameEventScriptProgram[groups.Count];
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var builder = GameEventScriptBuilder.Create();
            if (environment.ExternalTypeCatalog is not null) builder.WithExternalTypeCatalog(environment.ExternalTypeCatalog);
            for (var sourceIndex = 0; sourceIndex < groups[groupIndex].Sources.Count; sourceIndex++)
            {
                var source = groups[groupIndex].Sources[sourceIndex];
                builder.AddScript(source.Text, source.Name);
            }
            var program = builder.Compile(new GameEventScriptCompileOptions { DebugInfo = DebugInfo(testCase.Compile.DebugInfo) });
            programs[groupIndex] = testCase.Compile.BinaryRoundTrip ? GameEventScriptProgramReader.Read(GameEventScriptProgramWriter.ToArray(program)) : program;
        }
        return programs;
    }

    private static GameEventScriptProgram CompileSingleProgram(ConformanceCase testCase, ConformanceRunnerEnvironment environment)
    {
        var programs = CompilePrograms(testCase, environment);
        if (programs.Count != 1) throw new InvalidOperationException("This conformance kind requires exactly one compiled program.");
        return programs[0];
    }

    private static void RegisterNative(GameEventScriptHost host, ConformanceNativeHandler definition)
        => host.Subscribe(definition.Message, definition.Parameters, new DeclarativeNativeHandler(definition), definition.Priority);

    private sealed class DeclarativeNativeHandler : IGameEventScriptNativeMessageHandler
    {
        private readonly ConformanceNativeHandler _definition;

        internal DeclarativeNativeHandler(ConformanceNativeHandler definition)
        {
            _definition = definition;
        }

        public void Handle(GameEventScriptMessage message, GameEventScriptContext context)
        {
            if (_definition.Throws) throw new InvalidOperationException("Configured conformance native handler failure.");
            for (var emitIndex = 0; emitIndex < _definition.Emits.Count; emitIndex++)
            {
                var emit = _definition.Emits[emitIndex];
                GameEventScriptMessageArgument[] arguments;
                if (emit.ForwardArguments)
                {
                    arguments = new GameEventScriptMessageArgument[message.Arguments.Count];
                    for (var index = 0; index < arguments.Length; index++) arguments[index] = new GameEventScriptMessageArgument(message.Arguments.NameAt(index), message.Arguments.ValueAt(index));
                }
                else
                {
                    arguments = new GameEventScriptMessageArgument[emit.Arguments.Count];
                    for (var index = 0; index < arguments.Length; index++) arguments[index] = new GameEventScriptMessageArgument(emit.Arguments[index].Name, ConformanceRuntimeValueCodec.DecodeValue(emit.Arguments[index].Value));
                }
                context.Emit(GameEventScriptMessage.Create(emit.Name, arguments));
            }
        }
    }

    private sealed class RuntimeCollector : IGameEventScriptRuntimeObserver, IGameEventScriptPublishSink
    {
        internal List<GameEventScriptMessage> Local { get; } = new();
        internal List<GameEventScriptMessage> Outbound { get; } = new();
        internal List<RuntimeLimitEvent> Limits { get; } = new();
        internal List<ConformanceResultDiagnostic> Diagnostics { get; } = new();
        internal List<ConformanceRuntimeLimitResult> AllLimitResults { get; } = new();
        internal List<ConformanceResultDiagnostic> AllDiagnostics { get; } = new();
        public bool Publish(GameEventScriptMessage message) { Outbound.Add(message); return true; }
        public void MessageEmitted(GameEventScriptMessage message, bool accepted) => Local.Add(message);
        public void MessagePublished(GameEventScriptMessage message, GameEventScriptPublishResult result) => Local.Add(message);
        public void DispatchStarted(GameEventScriptMessage message, string dispatchSignatureId) { }
        public void DispatchCompleted(GameEventScriptMessage message, string dispatchSignatureId) { }
        public void RuntimeLimitReached(string limitName, string detail, int limit)
        {
            Limits.Add(new RuntimeLimitEvent(limitName, detail, limit));
            AllLimitResults.Add(new ConformanceRuntimeLimitResult(limitName, detail, limit));
        }
        public void RuntimeError(GameEventScriptDiagnostic diagnostic)
        {
            var result = ConvertDiagnostic(diagnostic);
            Diagnostics.Add(result);
            AllDiagnostics.Add(result);
        }
        internal void Clear() { Local.Clear(); Outbound.Clear(); Limits.Clear(); Diagnostics.Clear(); }
    }

    private sealed class RuntimeLimitEvent
    {
        internal RuntimeLimitEvent(string name, string detail, int limit) { Name = name; Detail = detail; Limit = limit; }
        internal string Name { get; }
        internal string Detail { get; }
        internal int Limit { get; }
    }

    private sealed class SourceGroup
    {
        internal SourceGroup(string id) { Id = id; }
        internal string Id { get; }
        internal List<ConformanceSourceInput> Sources { get; } = new();
    }

    private static void CompareChannel(string path, IReadOnlyList<ConformanceMessage> expected, IReadOnlyList<GameEventScriptMessage> actual, ConformanceComparisonOptions comparison, List<ConformanceMismatch> mismatches)
    {
        if (expected.Count != actual.Count) { AddMismatch(mismatches, path + "/length", expected.Count.ToString(CultureInfo.InvariantCulture), actual.Count.ToString(CultureInfo.InvariantCulture)); return; }
        for (var index = 0; index < expected.Count; index++)
            if (!ConformanceRuntimeValueCodec.MessagesEqual(expected[index], actual[index], comparison)) AddMismatch(mismatches, path + "/" + index.ToString(CultureInfo.InvariantCulture), expected[index].Name, ConformanceRuntimeValueCodec.Describe(actual[index]));
    }

    private static void CompareObservations(string path, ConformanceObservationExpectation expected, RuntimeCollector actual, List<ConformanceMismatch> mismatches)
    {
        var nextLimit = 0;
        for (var index = 0; index < expected.IncludedRuntimeLimits.Count; index++)
        {
            var found = false;
            for (; nextLimit < actual.Limits.Count; nextLimit++)
            {
                if (!LimitMatches(expected.IncludedRuntimeLimits[index], actual.Limits[nextLimit])) continue;
                found = true;
                nextLimit++;
                break;
            }
            if (!found) AddMismatch(mismatches, path + "/runtimeLimits/included/" + index.ToString(CultureInfo.InvariantCulture), "matching observation", null);
        }
        for (var index = 0; index < expected.ExcludedRuntimeLimits.Count; index++)
            for (var actualIndex = 0; actualIndex < actual.Limits.Count; actualIndex++)
                if (LimitMatches(expected.ExcludedRuntimeLimits[index], actual.Limits[actualIndex])) AddMismatch(mismatches, path + "/runtimeLimits/excluded/" + index.ToString(CultureInfo.InvariantCulture), "no matching observation", actual.Limits[actualIndex].Name);
        if (expected.Diagnostics.Count != actual.Diagnostics.Count)
            AddMismatch(mismatches, path + "/diagnostics/length", expected.Diagnostics.Count.ToString(CultureInfo.InvariantCulture), actual.Diagnostics.Count.ToString(CultureInfo.InvariantCulture));
        var diagnosticCount = Math.Min(expected.Diagnostics.Count, actual.Diagnostics.Count);
        for (var index = 0; index < diagnosticCount; index++)
        {
            if (!DiagnosticMatches(expected.Diagnostics[index], actual.Diagnostics[index])) AddMismatch(mismatches, path + "/diagnostics/" + index.ToString(CultureInfo.InvariantCulture), expected.Diagnostics[index].Code, actual.Diagnostics[index].Code);
        }
    }

    private static bool LimitMatches(ConformanceRuntimeLimitExpectation expected, RuntimeLimitEvent actual)
        => (expected.Name is null || string.Equals(expected.Name, actual.Name, StringComparison.Ordinal))
           && (expected.DetailContains is null || actual.Detail.Contains(expected.DetailContains, StringComparison.Ordinal))
           && (expected.Limit is null || expected.Limit == (ulong)Math.Max(0, actual.Limit));

    private static void CompareHandlerResource(GameEventScriptProgram program, ConformanceHandlerResourceExpectation expected, List<ConformanceMismatch> mismatches)
    {
        for (var bindIndex = 0; bindIndex < program.Bindings.Entries.Count; bindIndex++)
        {
            var bind = program.Bindings.Entries[bindIndex];
            if (bind.Kind is not (GameEventScriptBinaryBindKind.MessageHandler or GameEventScriptBinaryBindKind.MessageNameHandler)) continue;
            var name = program.StringConstants.Resolve(bind.Name);
            if (!string.Equals(name, expected.Name, StringComparison.Ordinal)) continue;
            var parameters = new string[bind.ArgumentNames.Count];
            for (var index = 0; index < parameters.Length; index++) parameters[index] = program.StringConstants.Resolve(bind.ArgumentNames[index]);
            var signature = GameEventScriptMessageSignature.CreateSignatureId(name, parameters);
            if (expected.SignatureId is not null && !string.Equals(expected.SignatureId, signature, StringComparison.Ordinal)) continue;
            CheckOptional(mismatches, "/metadata/handlerResources/" + signature + "/requiredRegisterCount", expected.RequiredRegisterCount, bind.RequiredRegisterCount);
            CheckOptional(mismatches, "/metadata/handlerResources/" + signature + "/requiredCallStackDepth", expected.RequiredCallStackDepth, bind.RequiredCallStackDepth);
            return;
        }
        AddMismatch(mismatches, "/metadata/handlerResources/" + expected.Name, "matching handler", null);
    }

    private static bool DiagnosticMatches(ConformanceExpectedDiagnostic expected, GameEventScriptDiagnostic actual)
        => string.Equals(expected.Phase, Phase(actual.Phase), StringComparison.Ordinal) && string.Equals(expected.Code, actual.Code, StringComparison.Ordinal)
           && OptionalEquals(expected.Symbol, actual.Symbol) && OptionalEquals(expected.SymbolKind, SymbolKind(actual.SymbolKind))
           && OptionalEquals(expected.ProgramName, actual.ProgramName) && OptionalEquals(expected.HandlerName, actual.HandlerName)
           && LocationMatches(expected, actual.SourceLocation);

    private static bool DiagnosticMatches(ConformanceExpectedDiagnostic expected, ConformanceResultDiagnostic actual)
        => string.Equals(expected.Phase, actual.Phase, StringComparison.Ordinal) && string.Equals(expected.Code, actual.Code, StringComparison.Ordinal)
           && OptionalEquals(expected.Symbol, actual.Symbol) && OptionalEquals(expected.SymbolKind, actual.SymbolKind)
           && OptionalEquals(expected.SourceName, actual.SourceName) && OptionalEquals(expected.ProgramName, actual.ProgramName) && OptionalEquals(expected.HandlerName, actual.HandlerName)
           && OptionalEquals(expected.Line, actual.Line) && OptionalEquals(expected.Column, actual.Column) && OptionalEquals(expected.EndLine, actual.EndLine) && OptionalEquals(expected.EndColumn, actual.EndColumn);

    private static bool LocationMatches(ConformanceExpectedDiagnostic expected, GameEventScriptSourceLocation? actual)
        => actual is null
            ? expected.SourceName is null && expected.Line is null && expected.Column is null && expected.EndLine is null && expected.EndColumn is null
            : OptionalEquals(expected.SourceName, actual.SourceName) && OptionalEquals(expected.Line, ToUInt(actual.Line)) && OptionalEquals(expected.Column, ToUInt(actual.Column)) && OptionalEquals(expected.EndLine, ToUInt(actual.EndLine)) && OptionalEquals(expected.EndColumn, ToUInt(actual.EndColumn));

    private static ConformanceMismatch DiagnosticMismatch(string path, ConformanceExpectedDiagnostic expected, IReadOnlyList<GameEventScriptDiagnostic> actual)
        => new(path, ConformanceRunnerCodes.AssertionMismatch, expected.Phase + ":" + expected.Code, actual.Count == 0 ? null : Phase(actual[0].Phase) + ":" + actual[0].Code, actual.Count == 0 ? null : ConvertDiagnostic(actual[0]));

    private static ConformanceCaseResult UnexpectedDiagnostics(ConformanceCase testCase, string phase, IReadOnlyList<GameEventScriptDiagnostic> diagnostics)
        => Result(testCase, ConformanceCaseStatus.Error, ConformanceRunnerCodes.UnhandledException, diagnostics: ConvertDiagnostics(diagnostics), technical: "Unexpected " + phase + " failure.");

    private static GameEventScriptDebugInfoOptions DebugInfo(IReadOnlyList<string> values)
    {
        var result = GameEventScriptDebugInfoOptions.None;
        for (var index = 0; index < values.Count; index++) result |= values[index] switch
        {
            "debugSymbols" => GameEventScriptDebugInfoOptions.DebugSymbols,
            "sourceMap" => GameEventScriptDebugInfoOptions.SourceMap,
            "sourceArchive" => GameEventScriptDebugInfoOptions.SourceArchive,
            _ => GameEventScriptDebugInfoOptions.None
        };
        return result;
    }

    private static GameEventScriptRandomGenerator CreateRandom(ConformanceRandomConfiguration? configuration)
    {
        if (configuration?.Seed is { } seed) return GameEventScriptRandomGenerator.FromSeed(seed);
        if (configuration is { Sequence.Count: > 0 })
        {
            var sequence = new double[configuration.Sequence.Count];
            for (var index = 0; index < sequence.Length; index++) sequence[index] = ConformanceRuntimeValueCodec.ParseBinary64(configuration.Sequence[index]);
            return GameEventScriptRandomGenerator.FromSequence(sequence);
        }
        return GameEventScriptRandomGenerator.FromSeed(0L);
    }

    private static GameEventScriptRuntimeLimits CreateRuntimeLimits(ConformanceRuntimeLimits limits)
    {
        int Read(string name, int fallback) => limits.Values.TryGetValue(name, out var value) ? checked((int)value) : fallback;
        var defaults = GameEventScriptRuntimeLimits.Default;
        return new GameEventScriptRuntimeLimits
        {
            MaxProcessedEventsPerRun = Read("maxProcessedEventsPerRun", defaults.MaxProcessedEventsPerRun),
            MaxQueuedMessagesPerRun = Read("maxQueuedMessagesPerRun", defaults.MaxQueuedMessagesPerRun),
            MaxExecutionSteps = Read("maxExecutionSteps", defaults.MaxExecutionSteps),
            MaxRegisterValues = Read("maxRegisterValues", defaults.MaxRegisterValues),
            MaxLoopIterations = Read("maxLoopIterations", defaults.MaxLoopIterations),
            MaxCallDepth = Read("maxCallDepth", defaults.MaxCallDepth),
            MaxRangeItems = Read("maxRangeItems", defaults.MaxRangeItems),
            MaxGeneratedCollectionItems = Read("maxGeneratedCollectionItems", defaults.MaxGeneratedCollectionItems),
            MaxDiceCount = Read("maxDiceCount", defaults.MaxDiceCount),
            MaxDiceSides = Read("maxDiceSides", defaults.MaxDiceSides)
        };
    }

    private static string? ValidateEnvironment(ConformanceRunnerEnvironment environment)
    {
        if (environment.RunnerId.Length == 0 || environment.RunnerVersion.Length == 0 || environment.ImplementationId.Length == 0 || environment.ImplementationVersion.Length == 0) return "Runner and implementation identities must not be empty.";
        for (var index = 0; index < environment.Capabilities.Count; index++)
        {
            var value = environment.Capabilities[index];
            if (value.Length == 0) return "Capability IDs must not be empty.";
            if (index > 0 && string.Equals(value, environment.Capabilities[index - 1], StringComparison.Ordinal)) return "Capability IDs must be unique.";
        }
        if (Has(environment.Capabilities, "performance") && (environment.PerformanceProfileId is null || environment.PerformanceProvider is null)) return "Performance capability requires a profile and provider.";
        if (Has(environment.Capabilities, "external-types") && (environment.ExternalTypeCatalog is null || environment.ExternalTypeRegistry is null)) return "External-types capability requires a catalog and runtime registry.";
        return null;
    }

    private static string? ValidateCorpus(IReadOnlyList<ConformanceDocument> documents, ConformanceRunnerLimits limits)
    {
        if (limits.MaxCases <= 0 || limits.MaxFramesPerStep <= 0) return "Runner limits must be positive.";
        var suites = new HashSet<string>(StringComparer.Ordinal);
        var cases = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        for (var documentIndex = 0; documentIndex < documents.Count; documentIndex++)
        {
            var document = documents[documentIndex] ?? throw new ArgumentException("The corpus contains a null document.", nameof(documents));
            if (!suites.Add(document.SuiteId)) return "Duplicate suite ID '" + document.SuiteId + "'.";
            for (var caseIndex = 0; caseIndex < document.Cases.Count; caseIndex++)
            {
                if (++count > limits.MaxCases) return "The corpus exceeds MaxCases.";
                if (!cases.Add(document.Cases[caseIndex].FullId)) return "Duplicate full case ID '" + document.Cases[caseIndex].FullId + "'.";
            }
        }
        return null;
    }

    private static ConformanceRunReport CreateReport(ConformanceRunnerEnvironment environment, IReadOnlyList<ConformanceCaseResult> results)
    {
        var passed = 0; var failed = 0; var skipped = 0; var error = 0;
        for (var index = 0; index < results.Count; index++) switch (results[index].Status) { case ConformanceCaseStatus.Passed: passed++; break; case ConformanceCaseStatus.Failed: failed++; break; case ConformanceCaseStatus.Skipped: skipped++; break; case ConformanceCaseStatus.Error: error++; break; }
        var status = error > 0 ? ConformanceCaseStatus.Error : failed > 0 ? ConformanceCaseStatus.Failed : passed > 0 ? ConformanceCaseStatus.Passed : ConformanceCaseStatus.Skipped;
        return new ConformanceRunReport(environment, status, new ConformanceRunSummary(results.Count, passed, failed, skipped, error), results);
    }

    private static ConformanceCaseResult Result(ConformanceCase testCase, ConformanceCaseStatus status, string code, IReadOnlyList<string>? missing = null, IReadOnlyList<ConformanceMismatch>? mismatches = null, IReadOnlyList<ConformanceResultDiagnostic>? diagnostics = null, IReadOnlyList<ConformanceRuntimeLimitResult>? runtimeLimits = null, string? actualAssembler = null, ConformancePerformanceResult? performance = null, string? technical = null)
        => new(testCase, status, code, missing ?? Array.Empty<string>(), mismatches ?? Array.Empty<ConformanceMismatch>(), diagnostics ?? Array.Empty<ConformanceResultDiagnostic>(), runtimeLimits ?? Array.Empty<ConformanceRuntimeLimitResult>(), actualAssembler, performance, technical);

    private static List<string> Missing(IReadOnlyList<string> required, IReadOnlyList<string> capabilities)
    {
        var result = new List<string>();
        for (var index = 0; index < required.Count; index++) if (!Has(capabilities, required[index])) result.Add(required[index]);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static bool Has(IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++) if (string.Equals(values[index], value, StringComparison.Ordinal)) return true;
        return false;
    }

    private static IReadOnlyList<ConformanceResultDiagnostic> ConvertDiagnostics(IReadOnlyList<GameEventScriptDiagnostic> diagnostics)
    {
        var result = new ConformanceResultDiagnostic[diagnostics.Count];
        for (var index = 0; index < result.Length; index++) result[index] = ConvertDiagnostic(diagnostics[index]);
        return result;
    }

    private static ConformanceResultDiagnostic ConvertDiagnostic(GameEventScriptDiagnostic diagnostic)
    {
        var location = diagnostic.SourceLocation;
        return new ConformanceResultDiagnostic(Phase(diagnostic.Phase), diagnostic.Code, diagnostic.Message, diagnostic.Symbol, SymbolKind(diagnostic.SymbolKind), location?.SourceName, ToUInt(location?.Line), ToUInt(location?.Column), ToUInt(location?.EndLine), ToUInt(location?.EndColumn), diagnostic.ProgramName, diagnostic.HandlerName);
    }

    private static string Phase(GameEventScriptDiagnosticPhase phase) => phase switch { GameEventScriptDiagnosticPhase.Parse => "parse", GameEventScriptDiagnosticPhase.Validate => "validate", GameEventScriptDiagnosticPhase.Compile => "compile", GameEventScriptDiagnosticPhase.Decode => "decode", GameEventScriptDiagnosticPhase.Link => "link", _ => "runtime" };
    private static string? SymbolKind(GameEventScriptSymbolKind value) => value == GameEventScriptSymbolKind.Unknown ? null : char.ToLowerInvariant(value.ToString()[0]) + value.ToString().Substring(1);
    private static uint? ToUInt(int? value) => value is null || value < 0 ? null : checked((uint)value.Value);
    private static bool OptionalEquals<T>(T? expected, T? actual) where T : class => expected is null || Equals(expected, actual);
    private static bool OptionalEquals(uint? expected, uint? actual) => expected is null || expected == actual;
    private static void CheckOptional(List<ConformanceMismatch> mismatches, string path, string? expected, string actual) { if (expected is not null && !string.Equals(expected, actual, StringComparison.Ordinal)) AddMismatch(mismatches, path, expected, actual); }
    private static void CheckOptional(List<ConformanceMismatch> mismatches, string path, uint? expected, ushort actual) { if (expected is not null && expected.Value != actual) AddMismatch(mismatches, path, expected.Value.ToString(CultureInfo.InvariantCulture), actual.ToString(CultureInfo.InvariantCulture)); }
    private static void AddMismatch(List<ConformanceMismatch> mismatches, string path, string? expected, string? actual) => mismatches.Add(new ConformanceMismatch(path, ConformanceRunnerCodes.AssertionMismatch, expected, actual));
    private static bool StringListsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right) { if (left.Count != right.Count) return false; for (var index = 0; index < left.Count; index++) if (!string.Equals(left[index], right[index], StringComparison.Ordinal)) return false; return true; }
    private static string Join(IReadOnlyList<string> values) => "[" + string.Join(",", values) + "]";
    private static string MessageErrorCode(string message) => message.Contains("more than once", StringComparison.Ordinal) ? "duplicateArgumentName" : message.Contains("argument", StringComparison.OrdinalIgnoreCase) ? "invalidArgument" : "invalidMessage";
    private static string NormalizeLf(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
    private static int FirstUtf8Difference(string left, string right) { var leftBytes = System.Text.Encoding.UTF8.GetBytes(left); var rightBytes = System.Text.Encoding.UTF8.GetBytes(right); var count = Math.Min(leftBytes.Length, rightBytes.Length); var index = 0; while (index < count && leftBytes[index] == rightBytes[index]) index++; return index; }
}
