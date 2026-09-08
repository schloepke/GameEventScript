// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Conformance;

internal static class ConformanceSchemaBinder
{
    private static readonly string[] KnownCapabilities =
    {
        "compiler", "program-binary", "host", "vm", "message-api", "value-api", "external-types", "native-handlers", "publish-sink", "observer",
        "performance", "bytecode-snapshot"
    };

    private static readonly string[] RuntimeLimitNames =
    {
        "maxProcessedEventsPerRun", "maxQueuedMessagesPerRun", "maxExecutionSteps", "maxRegisterValues", "maxLoopIterations", "maxCallDepth", "maxRandomScopeDepth",
        "maxRangeItems", "maxGeneratedCollectionItems", "maxDiceCount", "maxDiceSides"
    };

    internal static ConformanceDocument Bind(ConformanceText text, MarkdownSyntax syntax, ConformanceParserLimits limits)
    {
        var suite = ParseYaml(syntax.Frontmatter, limits);
        Closed(suite, "formatVersion", "suiteId", "title", "kind", "level", "categories", "tags", "requires", "compile", "runtimeLimits", "comparison");
        var version = RequiredInt32(suite, "formatVersion");
        if (version != 1) throw Schema(ConformanceDiagnosticCodes.SchemaUnsupportedVersion, "Only conformance formatVersion 1 is supported.", Property(suite, "formatVersion")!.Value.Range);
        var suiteId = RequiredId(suite, "suiteId");
        var title = OptionalString(suite, "title") ?? suiteId;
        var suiteDefaults = ParseDefaults(suite, null);
        var cases = new List<ConformanceCase>(syntax.Cases.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testSyntax in syntax.Cases)
        {
            var yaml = ResolveYamlBlocks(testSyntax, limits);
            if (yaml.Case is null) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "Every test requires exactly one yaml block with 'gesBlock: case'.", text.Lines[testSyntax.StartLineIndex].Range());
            var caseNode = yaml.Case;
            Closed(
                caseNode, "gesBlock", "id", "kind", "level", "categories", "tags", "requires", "compile", "runtimeLimits", "comparison", "sources", "random", "publishSink", "externalTypeRegistry", "hostCount", "deferredPrograms", "nativeHandlers",
                "stepActions", "messageApi", "valueApi", "externalTypeApi", "binaryFixture", "performance");
            var id = RequiredId(caseNode, "id");
            if (!ids.Add(id)) throw Schema(ConformanceDiagnosticCodes.SchemaDuplicateId, $"Duplicate case ID '{id}'.", Property(caseNode, "id")!.Value.Range);
            var defaults = ParseDefaults(caseNode, suiteDefaults);
            if (defaults.Kind is null) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "The test kind is required either in frontmatter or the case block.", caseNode.Range);
            if (defaults.Level is null) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "The test level is required either in frontmatter or the case block.", caseNode.Range);
            var expectationNode = yaml.Expect;
            var sources = BindSources(suiteId, id, caseNode, testSyntax, limits);
            var nativeHandlers = BindNativeHandlers(Optional(caseNode, "nativeHandlers"));
            var random = BindRandom(Optional(caseNode, "random"));
            var publishSink = BindPublishSink(OptionalString(caseNode, "publishSink"), Optional(caseNode, "publishSink")?.Range ?? caseNode.Range);
            var externalTypeRegistry = BindExternalTypeRegistry(OptionalString(caseNode, "externalTypeRegistry"), Optional(caseNode, "externalTypeRegistry")?.Range ?? caseNode.Range);
            var hostCount = OptionalUInt32(caseNode, "hostCount") ?? 1;
            if (hostCount == 0 || limits.MaxHostsPerTest <= 0 || hostCount > (uint)limits.MaxHostsPerTest)
                throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "hostCount must be positive and within MaxHostsPerTest.", Optional(caseNode, "hostCount")?.Range ?? caseNode.Range);
            var deferredPrograms = OptionalStringList(caseNode, "deferredPrograms", ids: true) ?? new List<string>();
            if (hostCount > 1 && defaults.Kind != ConformanceTestKind.ScriptApi)
                throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "hostCount greater than one is supported only by scriptApi.", Optional(caseNode, "hostCount")?.Range ?? caseNode.Range);
            if (deferredPrograms.Count > 0 && defaults.Kind != ConformanceTestKind.ScriptApi)
                throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "deferredPrograms is supported only by scriptApi.", Optional(caseNode, "deferredPrograms")?.Range ?? caseNode.Range);
            var messageApi = BindMessageApi(Optional(caseNode, "messageApi"));
            var valueApi = BindValueApi(Optional(caseNode, "valueApi"));
            var externalTypeApi = BindExternalTypeApi(Optional(caseNode, "externalTypeApi"));
            var binaryFixture = BindBinaryFixture(Optional(caseNode, "binaryFixture"));
            var workload = BindPerformanceWorkload(Optional(caseNode, "performance"));
            var expectations = BindExpectation(defaults.Kind.Value, expectationNode);
            if ((messageApi?.CompareConformanceMessage is not null) != (expectations.MessageApi?.ConformanceEquals is not null))
                throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "compareConformanceMessage and conformanceEquals must be supplied together.", caseNode.Range);
            ValidateCardinality(defaults.Kind.Value, testSyntax, expectationNode, sources.Count, nativeHandlers.Count, messageApi, valueApi, externalTypeApi, binaryFixture, workload);
            if (defaults.Kind == ConformanceTestKind.ScriptApi && sources.Count == 0 && defaults.Compile.BinaryRoundTrip)
                throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A native-only scriptApi case cannot request a program binary roundtrip.", caseNode.Range);
            if (defaults.Kind == ConformanceTestKind.ProgramBinary && defaults.Compile.BinaryRoundTrip)
                throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A programBinary case cannot request a second binary roundtrip.", caseNode.Range);
            var stepActions = BindStepActions(Optional(caseNode, "stepActions"), testSyntax.Steps);
            ValidateHostReferences(sources, deferredPrograms, nativeHandlers, stepActions, caseNode.Range);
            if (messageApi?.ArgumentsWereMapping == true &&
                !string.Equals(expectations.MessageApi?.Error, "invalidArgumentsShape", StringComparison.Ordinal))
                throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A messageApi argument mapping is valid only with expected error 'invalidArgumentsShape'.", caseNode.Range);
            var steps = BindSteps(testSyntax.Steps, expectationNode, stepActions);
            var core = new List<string>(defaults.Requires.Core);
            var optional = new List<string>(defaults.Requires.Optional);
            AddKindCapabilities(defaults.Kind.Value, core, optional);
            if (defaults.Kind == ConformanceTestKind.ProgramBinary && steps.Count > 0)
            {
                AddUnique(core, "host");
                AddUnique(core, "vm");
                AddUnique(core, "observer");
                AddUnique(core, "publish-sink");
            }
            if (binaryFixture?.CompareCompiledRuntime == true) AddUnique(core, "compiler");
            if (defaults.Kind == ConformanceTestKind.ScriptApi && sources.Count == 0)
            {
                core.Remove("compiler");
                core.Remove("vm");
                core.Remove("publish-sink");
            }
            if (nativeHandlers.Count > 0) AddUnique(core, "native-handlers");
            if (defaults.Compile.BinaryRoundTrip) AddUnique(core, "program-binary");
            ValidateCapabilities(core, optional, caseNode.Range);
            var range = text.LinesRange(testSyntax.StartLineIndex, testSyntax.EndLineIndex);
            cases.Add(new ConformanceCase(
                id, suiteId + "/" + id, testSyntax.Title, defaults.Kind.Value, defaults.Level.Value,
                defaults.Categories, defaults.Tags, new ConformanceCapabilityRequirements(core, optional), defaults.Compile,
                defaults.RuntimeLimits, defaults.Comparison, publishSink, externalTypeRegistry, hostCount, deferredPrograms, random, sources, nativeHandlers, steps, expectations, messageApi, valueApi, externalTypeApi, binaryFixture, workload,
                testSyntax.AssemblerBlock?.Payload, testSyntax.CaseBlock!.BlockRange, testSyntax.ExpectBlock?.BlockRange, testSyntax.StepsTableRange,
                testSyntax.AssemblerBlock?.BlockRange, testSyntax.AssemblerBlock?.PayloadRange, range));
        }

        var frontmatterRange = text.LinesRange(syntax.FrontmatterStart, syntax.FrontmatterEnd);
        return new ConformanceDocument(version, suiteId, title, cases, new ConformanceSourceDocument((byte[])text.Original.Clone(), text.HasBom, text.LineEnding), frontmatterRange);
    }

    private static YamlNode ParseYaml(IReadOnlyList<ConformanceLine> lines, ConformanceParserLimits limits)
        => new RestrictedYamlParser(lines, limits).ParseRootMapping();

    private static (YamlNode? Case, YamlNode? Expect) ResolveYamlBlocks(MarkdownCaseSyntax syntax, ConformanceParserLimits limits)
    {
        YamlNode? caseNode = null;
        YamlNode? expectNode = null;
        foreach (var block in syntax.YamlBlocks)
        {
            var node = ParseYaml(block.PayloadLines, limits);
            var discriminatorNode = Required(node, "gesBlock");
            var discriminator = String(discriminatorNode);
            switch (discriminator)
            {
                case "case":
                    if (caseNode is not null) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "A test must contain exactly one yaml block with 'gesBlock: case'.", block.BlockRange);
                    caseNode = node;
                    syntax.CaseBlock = block;
                    break;
                case "expect":
                    if (expectNode is not null) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "A test may contain at most one yaml block with 'gesBlock: expect'.", block.BlockRange);
                    expectNode = node;
                    syntax.ExpectBlock = block;
                    break;
                default:
                    throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "gesBlock must be 'case' or 'expect'.", discriminatorNode.Range);
            }
        }
        return (caseNode, expectNode);
    }

    private static Defaults ParseDefaults(YamlNode node, Defaults? parent)
    {
        var kind = OptionalKind(node, "kind") ?? parent?.Kind;
        var level = OptionalLevel(node, "level") ?? parent?.Level;
        var categories = Merge(parent?.Categories, OptionalStringList(node, "categories", ids: true));
        var tags = Merge(parent?.Tags, OptionalStringList(node, "tags"));
        var requires = MergeRequirements(parent?.Requires, BindRequirements(Optional(node, "requires")));
        var compile = BindCompileOptions(Optional(node, "compile"), parent?.Compile);
        var runtime = BindRuntimeLimits(Optional(node, "runtimeLimits"), parent?.RuntimeLimits);
        var comparison = BindComparison(Optional(node, "comparison"), parent?.Comparison);
        return new Defaults(kind, level, categories, tags, requires, compile, runtime, comparison);
    }

    private static List<ConformanceSourceInput> BindSources(string suiteId, string caseId, YamlNode caseNode, MarkdownCaseSyntax syntax, ConformanceParserLimits limits)
    {
        if (syntax.Sources.Count > limits.MaxSourcesPerTest) throw Schema(ConformanceDiagnosticCodes.YamlLimitExceeded, "The test exceeds MaxSourcesPerTest.", syntax.Sources[limits.MaxSourcesPerTest].BlockRange);
        var totalBytes = 0;
        foreach (var source in syntax.Sources) totalBytes = checked(totalBytes + Encoding.UTF8.GetByteCount(source.Payload));
        if (totalBytes > limits.MaxSourceBytesPerTest) throw Schema(ConformanceDiagnosticCodes.YamlLimitExceeded, "The test exceeds MaxSourceBytesPerTest.", syntax.Sources.Count == 0 ? caseNode.Range : syntax.Sources[0].BlockRange);
        var descriptors = Optional(caseNode, "sources");
        var result = new List<ConformanceSourceInput>();
        if (descriptors is null)
        {
            if (syntax.Sources.Count == 1) result.Add(new ConformanceSourceInput(suiteId + "." + caseId + ".ges", "main", syntax.Sources[0].Payload, syntax.Sources[0].BlockRange, syntax.Sources[0].PayloadRange));
            return result;
        }
        RequireKind(descriptors, YamlNodeKind.Sequence, "sources must be a sequence.");
        if (descriptors.Items.Count != syntax.Sources.Count) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "sources must contain exactly one descriptor per ges block.", descriptors.Range);
        for (var index = 0; index < descriptors.Items.Count; index++)
        {
            var descriptor = descriptors.Items[index];
            Closed(descriptor, "name", "program");
            var name = RequiredString(descriptor, "name");
            if (name.Length == 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A source name cannot be empty.", Property(descriptor, "name")!.Value.Range);
            var program = OptionalString(descriptor, "program") ?? "main";
            RequireId(program, Optional(descriptor, "program")?.Range ?? descriptor.Range);
            result.Add(new ConformanceSourceInput(name, program, syntax.Sources[index].Payload, syntax.Sources[index].BlockRange, syntax.Sources[index].PayloadRange));
        }
        return result;
    }

    private static List<ConformanceNativeHandler> BindNativeHandlers(YamlNode? node)
    {
        var result = new List<ConformanceNativeHandler>();
        if (node is null) return result;
        RequireKind(node, YamlNodeKind.Sequence, "nativeHandlers must be a sequence.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var handlerIndex = 0; handlerIndex < node.Items.Count; handlerIndex++)
        {
            var item = node.Items[handlerIndex];
            Closed(item, "id", "message", "parameters", "messageName", "priority", "initiallySubscribed", "throw", "actions", "emit");
            var id = OptionalString(item, "id") ?? "native-" + (handlerIndex + 1).ToString("D4", CultureInfo.InvariantCulture);
            RequireId(id, Optional(item, "id")?.Range ?? item.Range);
            if (!ids.Add(id)) throw Schema(ConformanceDiagnosticCodes.SchemaDuplicateId, "Duplicate native handler ID '" + id + "'.", Optional(item, "id")?.Range ?? item.Range);
            var message = RequiredString(item, "message");
            var parameters = OptionalStringList(item, "parameters") ?? new List<string>();
            var messageNameOnly = OptionalBoolean(item, "messageName") ?? false;
            if (messageNameOnly && parameters.Count != 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A message-name native handler cannot declare parameters.", item.Range);
            var priority = OptionalInt32(item, "priority") ?? 0;
            var initiallySubscribed = OptionalBoolean(item, "initiallySubscribed") ?? true;
            var throws = OptionalBoolean(item, "throw") ?? false;
            var actions = BindNativeActions(Optional(item, "actions"));
            var emits = new List<ConformanceNativeEmit>();
            var emitNode = Optional(item, "emit");
            if (emitNode is not null)
            {
                RequireKind(emitNode, YamlNodeKind.Sequence, "nativeHandlers.emit must be a sequence.");
                foreach (var emit in emitNode.Items)
                {
                    Closed(emit, "name", "forwardArguments", "args");
                    var forward = OptionalBoolean(emit, "forwardArguments");
                    var argsNode = Optional(emit, "args");
                    if ((forward == true) == (argsNode is not null)) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "An emit requires exactly one of forwardArguments: true or args.", emit.Range);
                    emits.Add(new ConformanceNativeEmit(RequiredString(emit, "name"), forward == true, argsNode is null ? Array.Empty<ConformanceArgument>() : BindArguments(argsNode)));
                }
            }
            result.Add(new ConformanceNativeHandler(id, message, parameters, messageNameOnly, priority, initiallySubscribed, throws, actions, emits));
        }
        return result;
    }

    private static IReadOnlyList<ConformanceNativeAction> BindNativeActions(YamlNode? node)
    {
        var result = new List<ConformanceNativeAction>();
        if (node is null) return result;
        RequireKind(node, YamlNodeKind.Sequence, "nativeHandlers.actions must be a sequence.");
        foreach (var item in node.Items)
        {
            Closed(item, "loadProgram", "detachProgram", "subscribeHandler", "unsubscribeHandler", "expectResult");
            var operations = item.Properties.Where(property => property.Name != "expectResult").ToArray();
            if (operations.Length != 1) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "A native handler action requires exactly one operation.", item.Range);
            var operation = operations[0];
            var kind = operation.Name switch
            {
                "loadProgram" => ConformanceNativeActionKind.LoadProgram,
                "detachProgram" => ConformanceNativeActionKind.DetachProgram,
                "subscribeHandler" => ConformanceNativeActionKind.SubscribeHandler,
                _ => ConformanceNativeActionKind.UnsubscribeHandler
            };
            var target = String(operation.Value);
            RequireId(target, operation.Value.Range);
            result.Add(new ConformanceNativeAction(kind, target, OptionalBoolean(item, "expectResult")));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ConformanceNativeAction>> BindStepActions(YamlNode? node, IReadOnlyList<MarkdownStepSyntax> steps)
    {
        var result = new Dictionary<string, IReadOnlyList<ConformanceNativeAction>>(StringComparer.Ordinal);
        if (node is null) return result;
        RequireKind(node, YamlNodeKind.Mapping, "stepActions must be a mapping keyed by Step ID.");
        var known = new HashSet<string>(steps.Select(step => step.Id), StringComparer.Ordinal);
        foreach (var property in node.Properties)
        {
            if (!known.Contains(property.Name)) throw Schema(ConformanceDiagnosticCodes.SchemaUnknownReference, "Unknown Step ID '" + property.Name + "'.", property.NameRange);
            result.Add(property.Name, BindNativeActions(property.Value));
        }
        return result;
    }

    private static void ValidateHostReferences(
        IReadOnlyList<ConformanceSourceInput> sources,
        IReadOnlyList<string> deferredPrograms,
        IReadOnlyList<ConformanceNativeHandler> nativeHandlers,
        IReadOnlyDictionary<string, IReadOnlyList<ConformanceNativeAction>> stepActions,
        ConformanceSourceRange range)
    {
        var programs = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < sources.Count; index++) programs.Add(sources[index].ProgramId);
        var deferred = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < deferredPrograms.Count; index++)
        {
            var id = deferredPrograms[index];
            if (!deferred.Add(id)) throw Schema(ConformanceDiagnosticCodes.SchemaDuplicateId, "Duplicate deferred program ID '" + id + "'.", range);
            if (!programs.Contains(id)) throw Schema(ConformanceDiagnosticCodes.SchemaUnknownReference, "Unknown deferred program ID '" + id + "'.", range);
        }
        var handlers = new HashSet<string>(nativeHandlers.Select(value => value.Id), StringComparer.Ordinal);
        for (var handlerIndex = 0; handlerIndex < nativeHandlers.Count; handlerIndex++)
            for (var actionIndex = 0; actionIndex < nativeHandlers[handlerIndex].Actions.Count; actionIndex++)
                ValidateHostAction(nativeHandlers[handlerIndex].Actions[actionIndex], programs, deferred, handlers, range);
        foreach (var actions in stepActions.Values)
            for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                ValidateHostAction(actions[actionIndex], programs, deferred, handlers, range);
    }

    private static void ValidateHostAction(ConformanceNativeAction action, HashSet<string> programs, HashSet<string> deferred, HashSet<string> handlers, ConformanceSourceRange range)
    {
        var valid = action.Kind switch
        {
            ConformanceNativeActionKind.LoadProgram => deferred.Contains(action.Target),
            ConformanceNativeActionKind.DetachProgram => programs.Contains(action.Target),
            _ => handlers.Contains(action.Target)
        };
        if (!valid) throw Schema(ConformanceDiagnosticCodes.SchemaUnknownReference, "Unknown or invalid host-action target '" + action.Target + "'.", range);
    }

    private static ConformanceRandomConfiguration? BindRandom(YamlNode? node)
    {
        if (node is null) return null;
        Closed(node, "seed", "sequence");
        var seedNode = Optional(node, "seed");
        var sequenceNode = Optional(node, "sequence");
        if ((seedNode is null) == (sequenceNode is null)) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "random requires exactly one of seed or sequence.", node.Range);
        if (seedNode is not null) return new ConformanceRandomConfiguration(ParseInt64(seedNode), Array.Empty<string>());
        var sequence = StringList(sequenceNode!, ids: false);
        if (sequence.Count == 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "random.sequence cannot be empty.", sequenceNode!.Range);
        foreach (var value in sequence) RequireCanonicalFiniteBinary64(value, sequenceNode!.Range);
        return new ConformanceRandomConfiguration(null, sequence);
    }

    private static ConformanceMessageApiCase? BindMessageApi(YamlNode? node)
    {
        if (node is null) return null;
        Closed(node, "signature", "message", "compareSignature", "compareMessage", "compareConformanceMessage", "compareHandler", "createArguments");
        var signature = BindSignature(Required(node, "signature"));
        var messageNode = Required(node, "message");
        Closed(messageNode, "name", "tags", "args");
        var name = RequiredString(messageNode, "name");
        var tags = OptionalStringList(messageNode, "tags") ?? new List<string>();
        var args = Optional(messageNode, "args");
        if (args?.Kind == YamlNodeKind.Mapping)
        {
            var unordered = new List<ConformanceValueEntry>(args.Properties.Count);
            foreach (var property in args.Properties)
                unordered.Add(new ConformanceValueEntry(property.Name, BindValue(property.Value)));
            return new ConformanceMessageApiCase(
                signature.Name, signature.Parameters,
                new ConformanceMessage(name, tags, Array.Empty<ConformanceArgument>()), true, unordered,
                BindOptionalSignature(Optional(node, "compareSignature")), BindOptionalMessage(Optional(node, "compareMessage")),
                BindOptionalMessage(Optional(node, "compareConformanceMessage")),
                BindOptionalSignature(Optional(node, "compareHandler")), BindValues(Optional(node, "createArguments")));
        }
        var message = new ConformanceMessage(name, tags, args is null ? Array.Empty<ConformanceArgument>() : BindArguments(args));
        return new ConformanceMessageApiCase(
            signature.Name, signature.Parameters,
            message, false, Array.Empty<ConformanceValueEntry>(),
            BindOptionalSignature(Optional(node, "compareSignature")), BindOptionalMessage(Optional(node, "compareMessage")),
            BindOptionalMessage(Optional(node, "compareConformanceMessage")),
            BindOptionalSignature(Optional(node, "compareHandler")), BindValues(Optional(node, "createArguments")));
    }

    private static ConformanceMessageSignatureDefinition BindSignature(YamlNode node)
    {
        Closed(node, "name", "parameters");
        return new ConformanceMessageSignatureDefinition(RequiredString(node, "name"), OptionalStringList(node, "parameters") ?? new List<string>());
    }

    private static ConformanceMessageSignatureDefinition? BindOptionalSignature(YamlNode? node) => node is null ? null : BindSignature(node);

    private static ConformanceMessage? BindOptionalMessage(YamlNode? node) => node is null ? null : BindMessage(node);

    private static IReadOnlyList<ConformanceValue> BindValues(YamlNode? node)
    {
        var result = new List<ConformanceValue>();
        if (node is null) return result;
        RequireKind(node, YamlNodeKind.Sequence, "createArguments must be a sequence of portable values.");
        foreach (var item in node.Items) result.Add(BindValue(item));
        return result;
    }

    private static ConformanceValueApiCase? BindValueApi(YamlNode? node)
    {
        if (node is null) return null;
        Closed(node, "value", "equalTo", "notEqualTo", "mutateSourceAfterCreate");
        return new ConformanceValueApiCase(
            BindValue(Required(node, "value")),
            Optional(node, "equalTo") is { } equal ? BindValue(equal) : null,
            Optional(node, "notEqualTo") is { } notEqual ? BindValue(notEqual) : null,
            OptionalBoolean(node, "mutateSourceAfterCreate") ?? false);
    }

    private static ConformanceExternalTypeApiCase? BindExternalTypeApi(YamlNode? node)
    {
        if (node is null) return null;
        Closed(node, "typeNames");
        return new ConformanceExternalTypeApiCase(StringList(Required(node, "typeNames"), ids: false));
    }

    private static ConformanceBinaryFixture? BindBinaryFixture(YamlNode? node)
    {
        if (node is null) return null;
        Closed(node, "id", "resourceId", "relativePath", "sha256", "compilerId", "compilerVersion", "programVersion", "compareCompiledRuntime", "derivation");
        var id = RequiredString(node, "id");
        var resourceId = RequiredString(node, "resourceId");
        RequireId(id, Property(node, "id")!.Value.Range);
        RequireId(resourceId, Property(node, "resourceId")!.Value.Range);
        var relativePath = RequiredString(node, "relativePath");
        if (!ValidRelativeResourcePath(relativePath))
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "binaryFixture.relativePath must be a portable relative path without parent traversal.", Property(node, "relativePath")!.Value.Range);
        var sha256 = RequiredString(node, "sha256");
        RequireSha256(sha256, Property(node, "sha256")!.Value.Range);
        var compilerId = RequiredString(node, "compilerId");
        RequireId(compilerId, Property(node, "compilerId")!.Value.Range);
        var compilerVersion = RequiredString(node, "compilerVersion");
        if (compilerVersion.Length == 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "binaryFixture.compilerVersion cannot be empty.", Property(node, "compilerVersion")!.Value.Range);
        return new ConformanceBinaryFixture(
            id, resourceId, relativePath, sha256, compilerId, compilerVersion,
            ParseUInt64(Required(node, "programVersion")), OptionalBoolean(node, "compareCompiledRuntime") ?? false, OptionalString(node, "derivation"));
    }

    private static bool ValidRelativeResourcePath(string value)
    {
        if (value.Length == 0 || value[0] == '/' || value.Contains('\\') || value.Contains('\0')) return false;
        var parts = value.Split('/');
        for (var index = 0; index < parts.Length; index++)
            if (parts[index].Length == 0 || parts[index] is "." or "..") return false;
        return true;
    }

    private static void RequireSha256(string value, ConformanceSourceRange range)
    {
        if (value.Length != 64) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "SHA-256 must contain 64 uppercase hexadecimal digits.", range);
        for (var index = 0; index < value.Length; index++)
            if (value[index] is not (>= '0' and <= '9' or >= 'A' and <= 'F'))
                throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "SHA-256 must contain 64 uppercase hexadecimal digits.", range);
    }

    private static ConformancePerformanceWorkload? BindPerformanceWorkload(YamlNode? node)
    {
        if (node is null) return null;
        Closed(node, "iterations", "warmupIterations", "compileWarmupIterations", "observeRuntime");
        var iterations = RequiredUInt32(node, "iterations");
        if (iterations == 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "performance.iterations must be positive.", Property(node, "iterations")!.Value.Range);
        return new ConformancePerformanceWorkload(iterations, OptionalUInt32(node, "warmupIterations") ?? 0, OptionalUInt32(node, "compileWarmupIterations") ?? 0, OptionalBoolean(node, "observeRuntime") ?? true);
    }

    private static List<ConformanceStep> BindSteps(IReadOnlyList<MarkdownStepSyntax> syntaxSteps, YamlNode? expectation, IReadOnlyDictionary<string, IReadOnlyList<ConformanceNativeAction>> stepActions)
    {
        var expectationMap = expectation is null ? null : Optional(expectation, "steps");
        if (expectationMap is not null) RequireKind(expectationMap, YamlNodeKind.Mapping, "expect.steps must be a mapping keyed by Step ID.");
        var knownIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in syntaxSteps)
        {
            RequireId(step.Id, step.Range);
            if (!knownIds.Add(step.Id)) throw Schema(ConformanceDiagnosticCodes.SchemaDuplicateId, $"Duplicate step ID '{step.Id}'.", step.Range);
        }
        if (expectationMap is not null)
            foreach (var property in expectationMap.Properties)
                if (!knownIds.Contains(property.Name)) throw Schema(ConformanceDiagnosticCodes.SchemaUnknownReference, $"Unknown Step ID '{property.Name}'.", property.NameRange);
        var result = new List<ConformanceStep>(syntaxSteps.Count);
        foreach (var step in syntaxSteps)
        {
            if (step.Receive.Length == 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A step receive message cannot be empty.", step.Range);
            var mode = step.Pump switch
            {
                "completion" => ConformancePumpMode.Completion,
                "frames" => ConformancePumpMode.Frames,
                "enqueue" => ConformancePumpMode.Enqueue,
                "frame" => ConformancePumpMode.Frame,
                _ => throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A step pump is completion, frames, enqueue or frame.", step.Range)
            };
            uint? budget = null;
            if ((mode is ConformancePumpMode.Completion or ConformancePumpMode.Enqueue) && step.Budget.Length != 0) throw Schema(ConformanceDiagnosticCodes.InvalidStepsTable, "A completion or enqueue step has an empty budget.", step.Range);
            if (mode is ConformancePumpMode.Frames or ConformancePumpMode.Frame)
            {
                if (!uint.TryParse(step.Budget, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed == 0)
                    throw Schema(ConformanceDiagnosticCodes.InvalidStepsTable, "A frames step requires a positive UInt32 budget.", step.Range);
                budget = parsed;
            }
            var expected = expectationMap is null ? null : Property(expectationMap, step.Id)?.Value;
            result.Add(new ConformanceStep(step.Id, step.Receive, mode, budget, stepActions.TryGetValue(step.Id, out var actions) ? actions : Array.Empty<ConformanceNativeAction>(), BindStepExpectation(step.Receive, expected), step.Range));
        }
        return result;
    }

    private static ConformanceStepExpectation BindStepExpectation(string receive, YamlNode? node)
    {
        if (node is null)
            return new ConformanceStepExpectation(
                new ConformanceMessage(receive, Array.Empty<string>(), Array.Empty<ConformanceArgument>()), true, Array.Empty<ConformanceMessage>(), Array.Empty<ConformanceMessage>(), null, EmptyObservations());
        Closed(node, "input", "accepted", "local", "outbound", "paused", "runtimeLimits", "diagnostics", "trace");
        var inputNode = Optional(node, "input");
        var tags = new List<string>();
        IReadOnlyList<ConformanceArgument> args = Array.Empty<ConformanceArgument>();
        if (inputNode is not null)
        {
            Closed(inputNode, "tags", "args");
            tags = OptionalStringList(inputNode, "tags") ?? new List<string>();
            var argsNode = Optional(inputNode, "args");
            if (argsNode is not null) args = BindArguments(argsNode);
        }
        return new ConformanceStepExpectation(
            new ConformanceMessage(receive, tags, args), OptionalBoolean(node, "accepted") ?? true,
            BindMessages(Optional(node, "local")), BindMessages(Optional(node, "outbound")), OptionalBoolean(node, "paused"), BindObservations(node));
    }

    private static ConformanceExpectation BindExpectation(ConformanceTestKind kind, YamlNode? node)
    {
        var emptyChannel = new ConformanceChannelExpectation(Array.Empty<ConformanceMessage>(), Array.Empty<ConformanceMessage>(), EmptyObservations());
        if (node is null) return new ConformanceExpectation(emptyChannel, null, null, null, null, null, null, null, null);
        var allowed = kind switch
        {
            ConformanceTestKind.ScriptApi => new[] { "gesBlock", "steps", "initialization" },
            ConformanceTestKind.Performance => new[] { "gesBlock", "steps", "initialization", "performance" },
            ConformanceTestKind.CompileError or ConformanceTestKind.LoadError => new[] { "gesBlock", "error" },
            ConformanceTestKind.MessageApi => new[] { "gesBlock", "message" },
            ConformanceTestKind.ValueApi => new[] { "gesBlock", "value" },
            ConformanceTestKind.ExternalTypeApi => new[] { "gesBlock", "externalType" },
            ConformanceTestKind.CompileMetadata => new[] { "gesBlock", "metadata" },
            ConformanceTestKind.Bytecode => new[] { "gesBlock", "opcodes" },
            ConformanceTestKind.ProgramBinary => new[] { "gesBlock", "binary", "steps", "initialization" },
            _ => new[] { "gesBlock" }
        };
        Closed(node, allowed);
        var initNode = Optional(node, "initialization");
        var initialization = initNode is null ? emptyChannel : BindChannel(initNode);
        var errorNode = Optional(node, "error");
        var error = errorNode is null ? null : BindDiagnostic(errorNode);
        var messageNode = Optional(node, "message");
        var messageApi = messageNode is null ? null : BindMessageApiExpectation(messageNode);
        var valueNode = Optional(node, "value");
        var valueApi = valueNode is null ? null : BindValueApiExpectation(valueNode);
        var externalTypeNode = Optional(node, "externalType");
        var externalTypeApi = externalTypeNode is null ? null : BindExternalTypeApiExpectation(externalTypeNode);
        var metadataNode = Optional(node, "metadata");
        var metadata = metadataNode is null ? null : BindCompileMetadata(metadataNode);
        var opcodeNode = Optional(node, "opcodes");
        var opcodes = opcodeNode is null ? null : BindOpcodes(opcodeNode);
        var performanceNode = Optional(node, "performance");
        var performance = performanceNode is null ? null : BindPerformanceExpectation(performanceNode);
        var binaryNode = Optional(node, "binary");
        var binary = binaryNode is null ? null : BindBinaryExpectation(binaryNode);
        return new ConformanceExpectation(initialization, error, messageApi, valueApi, externalTypeApi, metadata, opcodes, performance, binary);
    }

    private static ConformanceBinaryExpectation BindBinaryExpectation(YamlNode node)
    {
        Closed(node, "outcome", "errorCode", "byteOffset", "sectionType", "entryIndex", "rewriteByteExact", "rewriteSha256", "moduleName", "requiredRegisterCount", "requiredCallStackDepth", "opaqueSectionCount");
        var outcomeText = RequiredString(node, "outcome");
        var outcome = outcomeText switch
        {
            "valid" => ConformanceBinaryOutcome.Valid,
            "readError" => ConformanceBinaryOutcome.ReadError,
            "validationError" => ConformanceBinaryOutcome.ValidationError,
            _ => throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "binary.outcome must be valid, readError, or validationError.", Property(node, "outcome")!.Value.Range)
        };
        var errorCode = OptionalString(node, "errorCode");
        if ((outcome == ConformanceBinaryOutcome.Valid && errorCode is not null) ||
            (outcome != ConformanceBinaryOutcome.Valid && errorCode is null))
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "binary.errorCode is required exactly for error outcomes.", node.Range);
        if (errorCode is not null && (!Enum.TryParse<GameEventScriptProgramFormatErrorCode>(errorCode, ignoreCase: false, out var parsedErrorCode) || parsedErrorCode.ToString() != errorCode))
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "binary.errorCode must be a canonical .gesb format error name.", Property(node, "errorCode")!.Value.Range);
        var byteOffsetNode = Optional(node, "byteOffset");
        long? byteOffset = byteOffsetNode is null ? null : ParseInt64(byteOffsetNode);
        if (byteOffset < 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "binary.byteOffset cannot be negative.", byteOffsetNode!.Range);
        var sectionTypeValue = OptionalUInt32(node, "sectionType");
        if (sectionTypeValue > ushort.MaxValue) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "binary.sectionType must fit UInt16.", Property(node, "sectionType")!.Value.Range);
        var entryIndexValue = OptionalUInt32(node, "entryIndex");
        if (entryIndexValue > int.MaxValue) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "binary.entryIndex must fit Int32.", Property(node, "entryIndex")!.Value.Range);
        var rewriteSha256 = OptionalString(node, "rewriteSha256");
        if (rewriteSha256 is not null) RequireSha256(rewriteSha256, Property(node, "rewriteSha256")!.Value.Range);
        if (outcome != ConformanceBinaryOutcome.Valid && node.Properties.Any(property => property.Name is "rewriteByteExact" or "rewriteSha256" or "moduleName" or "requiredRegisterCount" or "requiredCallStackDepth" or "opaqueSectionCount"))
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "Binary success fields are valid only for outcome: valid.", node.Range);
        return new ConformanceBinaryExpectation(
            outcome, errorCode, byteOffset, sectionTypeValue is null ? null : (ushort)sectionTypeValue.Value,
            entryIndexValue is null ? null : (int)entryIndexValue.Value,
            OptionalBoolean(node, "rewriteByteExact"), rewriteSha256, OptionalString(node, "moduleName"),
            OptionalUInt32(node, "requiredRegisterCount"), OptionalUInt32(node, "requiredCallStackDepth"), OptionalUInt32(node, "opaqueSectionCount"));
    }

    private static ConformanceChannelExpectation BindChannel(YamlNode node)
    {
        Closed(node, "local", "outbound", "runtimeLimits", "diagnostics", "trace");
        return new ConformanceChannelExpectation(BindMessages(Optional(node, "local")), BindMessages(Optional(node, "outbound")), BindObservations(node));
    }

    private static ConformanceObservationExpectation BindObservations(YamlNode node)
    {
        var runtime = Optional(node, "runtimeLimits");
        var included = new List<ConformanceRuntimeLimitExpectation>();
        var excluded = new List<ConformanceRuntimeLimitExpectation>();
        if (runtime is not null)
        {
            Closed(runtime, "include", "exclude");
            included = BindRuntimeLimitExpectations(Optional(runtime, "include"));
            excluded = BindRuntimeLimitExpectations(Optional(runtime, "exclude"));
        }
        var diagnostics = new List<ConformanceExpectedDiagnostic>();
        var diagnosticNode = Optional(node, "diagnostics");
        if (diagnosticNode is not null)
        {
            RequireKind(diagnosticNode, YamlNodeKind.Sequence, "diagnostics must be a sequence.");
            foreach (var item in diagnosticNode.Items) diagnostics.Add(BindDiagnostic(item));
        }
        var traceNode = Optional(node, "trace");
        return new ConformanceObservationExpectation(included, excluded, diagnostics, traceNode is not null, BindTrace(traceNode));
    }

    private static IReadOnlyList<ConformanceObserverEventExpectation> BindTrace(YamlNode? node)
    {
        var result = new List<ConformanceObserverEventExpectation>();
        if (node is null) return result;
        RequireKind(node, YamlNodeKind.Sequence, "trace must be a sequence.");
        foreach (var item in node.Items)
        {
            var eventName = RequiredString(item, "event");
            switch (eventName)
            {
                case "emit":
                    Closed(item, "event", "message", "accepted");
                    result.Add(new ConformanceObserverEventExpectation(ConformanceObserverEventKind.Emit, BindMessage(Required(item, "message")), null, RequiredBoolean(item, "accepted"), null, null, null));
                    break;
                case "publish":
                    Closed(item, "event", "message", "result");
                    result.Add(new ConformanceObserverEventExpectation(ConformanceObserverEventKind.Publish, BindMessage(Required(item, "message")), null, null, BindPublishResult(Required(item, "result")), null, null));
                    break;
                case "dispatchStarted":
                case "dispatchCompleted":
                    Closed(item, "event", "message", "signatureId");
                    result.Add(new ConformanceObserverEventExpectation(
                        eventName == "dispatchStarted" ? ConformanceObserverEventKind.DispatchStarted : ConformanceObserverEventKind.DispatchCompleted,
                        BindMessage(Required(item, "message")), RequiredString(item, "signatureId"), null, null, null, null));
                    break;
                case "runtimeLimit":
                    Closed(item, "event", "runtimeLimit");
                    result.Add(new ConformanceObserverEventExpectation(ConformanceObserverEventKind.RuntimeLimit, null, null, null, null, BindRuntimeLimitExpectation(Required(item, "runtimeLimit")), null));
                    break;
                case "diagnostic":
                    Closed(item, "event", "diagnostic");
                    result.Add(new ConformanceObserverEventExpectation(ConformanceObserverEventKind.Diagnostic, null, null, null, null, null, BindDiagnostic(Required(item, "diagnostic"))));
                    break;
                default:
                    throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "Unknown observer trace event '" + eventName + "'.", Property(item, "event")!.Value.Range);
            }
        }
        return result;
    }

    private static ConformancePublishResultExpectation BindPublishResult(YamlNode node)
    {
        Closed(node, "localAccepted", "outboundAttempted", "outboundAccepted", "anyAccepted");
        var local = RequiredBoolean(node, "localAccepted");
        var attempted = RequiredBoolean(node, "outboundAttempted");
        var outbound = RequiredBoolean(node, "outboundAccepted");
        var any = RequiredBoolean(node, "anyAccepted");
        if (outbound && !attempted) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A publish result cannot accept outbound without attempting outbound.", node.Range);
        if (any != (local || outbound)) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "anyAccepted must equal localAccepted or outboundAccepted.", node.Range);
        return new ConformancePublishResultExpectation(local, attempted, outbound, any);
    }

    private static List<ConformanceRuntimeLimitExpectation> BindRuntimeLimitExpectations(YamlNode? node)
    {
        var result = new List<ConformanceRuntimeLimitExpectation>();
        if (node is null) return result;
        RequireKind(node, YamlNodeKind.Sequence, "Runtime-limit expectations must be a sequence.");
        foreach (var item in node.Items) result.Add(BindRuntimeLimitExpectation(item));
        return result;
    }

    private static ConformanceRuntimeLimitExpectation BindRuntimeLimitExpectation(YamlNode item)
    {
        Closed(item, "any", "name", "detailContains", "limit");
        var any = OptionalBoolean(item, "any") ?? false;
        var name = OptionalString(item, "name");
        var detail = OptionalString(item, "detailContains");
        var limit = OptionalUInt64(item, "limit");
        if (!any && name is null && detail is null && limit is null) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "A runtime-limit expectation requires a constraint or 'any: true'.", item.Range);
        if (any && (name is not null || detail is not null || limit is not null)) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A wildcard runtime-limit expectation cannot contain additional constraints.", item.Range);
        return new ConformanceRuntimeLimitExpectation(any, name, detail, limit);
    }

    private static ConformanceExpectedDiagnostic BindDiagnostic(YamlNode node)
    {
        var names = new[] { "phase", "code", "symbol", "symbolKind", "sourceName", "line", "column", "endLine", "endColumn", "programName", "handlerName" };
        Closed(node, names);
        return new ConformanceExpectedDiagnostic(
            RequiredString(node, "phase"), RequiredString(node, "code"), OptionalString(node, "symbol"), OptionalString(node, "symbolKind"),
            OptionalString(node, "sourceName"), OptionalUInt32(node, "line"), OptionalUInt32(node, "column"), OptionalUInt32(node, "endLine"),
            OptionalUInt32(node, "endColumn"), OptionalString(node, "programName"), OptionalString(node, "handlerName"));
    }

    private static ConformanceMessageApiExpectation BindMessageApiExpectation(YamlNode node)
    {
        Closed(node, "name", "signatureId", "messageSignatureId", "matches", "argumentCount", "signatureEquals", "signatureHashEquals", "messageEquals", "messageHashEquals",
            "conformanceEquals", "handlerEquals", "handlerHashEquals", "createdMessageSignatureId", "error");
        if (node.Properties.Count == 0) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "At least one message expectation field is required.", node.Range);
        var error = OptionalString(node, "error");
        if (error is not null && node.Properties.Count != 1) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A messageApi error expectation cannot contain success fields.", node.Range);
        return new ConformanceMessageApiExpectation(
            OptionalString(node, "name"), OptionalString(node, "signatureId"), OptionalString(node, "messageSignatureId"),
            OptionalBoolean(node, "matches"), OptionalUInt32(node, "argumentCount"),
            OptionalBoolean(node, "signatureEquals"), OptionalBoolean(node, "signatureHashEquals"),
            OptionalBoolean(node, "messageEquals"), OptionalBoolean(node, "messageHashEquals"),
            OptionalBoolean(node, "conformanceEquals"),
            OptionalBoolean(node, "handlerEquals"), OptionalBoolean(node, "handlerHashEquals"),
            OptionalString(node, "createdMessageSignatureId"), error);
    }

    private static ConformanceValueApiExpectation BindValueApiExpectation(YamlNode node)
    {
        Closed(node, "normalized", "isNumeric", "hasValue", "isNothing", "hasUnit", "asBoolean", "length", "customTypeName", "equal", "equalHash", "notEqual");
        return new ConformanceValueApiExpectation(
            BindValue(Required(node, "normalized")),
            OptionalBoolean(node, "isNumeric"), OptionalBoolean(node, "hasValue"), OptionalBoolean(node, "isNothing"),
            OptionalBoolean(node, "hasUnit"), OptionalBoolean(node, "asBoolean"), OptionalUInt32(node, "length"),
            OptionalString(node, "customTypeName"), OptionalBoolean(node, "equal"), OptionalBoolean(node, "equalHash"),
            OptionalBoolean(node, "notEqual"));
    }

    private static ConformanceExternalTypeApiExpectation BindExternalTypeApiExpectation(YamlNode node)
    {
        Closed(node, "typeCount", "error");
        if (node.Properties.Count == 0) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "At least one externalType expectation field is required.", node.Range);
        var error = OptionalString(node, "error");
        if (error is not null && node.Properties.Count != 1) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "An externalTypeApi error expectation cannot contain success fields.", node.Range);
        return new ConformanceExternalTypeApiExpectation(OptionalUInt32(node, "typeCount"), error);
    }

    private static ConformanceOpcodeExpectation BindOpcodes(YamlNode node)
    {
        Closed(node, "contains", "excludes", "counts", "minimumCounts");
        var contains = OptionalStringList(node, "contains") ?? new List<string>();
        var excludes = OptionalStringList(node, "excludes") ?? new List<string>();
        var counts = BindUInt64Map(Optional(node, "counts"));
        var minimum = BindUInt64Map(Optional(node, "minimumCounts"));
        if (contains.Count + excludes.Count + counts.Count + minimum.Count == 0) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "An opcode expectation requires at least one constraint.", node.Range);
        return new ConformanceOpcodeExpectation(contains, excludes, counts, minimum);
    }

    private static ConformanceCompileMetadataExpectation BindCompileMetadata(YamlNode node)
    {
        Closed(node, "messageDefinitions", "programResources", "handlerResources");
        var expectedDefinitions = new List<ConformanceMessageDefinitionExpectation>();
        var messageDefinitions = Optional(node, "messageDefinitions");
        if (messageDefinitions is not null)
        {
            RequireKind(messageDefinitions, YamlNodeKind.Sequence, "metadata.messageDefinitions must be a sequence.");
            foreach (var item in messageDefinitions.Items)
            {
                Closed(item, "name", "count", "signatureIds");
                expectedDefinitions.Add(new ConformanceMessageDefinitionExpectation(
                    RequiredString(item, "name"), RequiredUInt32(item, "count"), StringList(Required(item, "signatureIds"), ids: false)));
            }
        }
        ConformanceProgramResourceExpectation? expectedProgramResources = null;
        var programResources = Optional(node, "programResources");
        if (programResources is not null)
        {
            Closed(programResources, "requiredRegisterCount", "requiredCallStackDepth");
            if (programResources.Properties.Count == 0) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "programResources requires at least one constraint.", programResources.Range);
            expectedProgramResources = new ConformanceProgramResourceExpectation(
                Optional(programResources, "requiredRegisterCount") is { } registers ? ParseUInt32(registers) : null,
                Optional(programResources, "requiredCallStackDepth") is { } depth ? ParseUInt32(depth) : null);
        }
        var expectedHandlers = new List<ConformanceHandlerResourceExpectation>();
        var handlerResources = Optional(node, "handlerResources");
        if (handlerResources is not null)
        {
            RequireKind(handlerResources, YamlNodeKind.Sequence, "metadata.handlerResources must be a sequence.");
            foreach (var item in handlerResources.Items)
            {
                Closed(item, "name", "signatureId", "requiredRegisterCount", "requiredCallStackDepth");
                var name = RequiredString(item, "name");
                var signatureId = Optional(item, "signatureId") is { } signature ? String(signature) : null;
                uint? registers = Optional(item, "requiredRegisterCount") is { } registerNode ? ParseUInt32(registerNode) : null;
                uint? depth = Optional(item, "requiredCallStackDepth") is { } depthNode ? ParseUInt32(depthNode) : null;
                if (item.Properties.Count < 2) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "A handlerResources entry requires at least one resource constraint.", item.Range);
                expectedHandlers.Add(new ConformanceHandlerResourceExpectation(name, signatureId, registers, depth));
            }
        }
        if (node.Properties.Count == 0) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "metadata requires at least one constraint.", node.Range);
        return new ConformanceCompileMetadataExpectation(expectedDefinitions, expectedProgramResources, expectedHandlers);
    }

    private static ConformancePerformanceExpectation BindPerformanceExpectation(YamlNode node)
    {
        Closed(node, "profiles");
        var profilesNode = Required(node, "profiles");
        RequireKind(profilesNode, YamlNodeKind.Mapping, "performance.profiles must be a mapping.");
        var profiles = new List<ConformancePerformanceProfile>();
        foreach (var profileProperty in profilesNode.Properties)
        {
            RequireId(profileProperty.Name, profileProperty.NameRange);
            var profile = profileProperty.Value;
            Closed(profile, "metrics");
            var metricsNode = Required(profile, "metrics");
            RequireKind(metricsNode, YamlNodeKind.Mapping, "profile.metrics must be a mapping.");
            var metrics = new List<ConformancePerformanceMetric>();
            foreach (var metricProperty in metricsNode.Properties)
            {
                RequireId(metricProperty.Name, metricProperty.NameRange);
                var metric = metricProperty.Value;
                Closed(metric, "reference", "maximum", "unit", "toleranceRelative", "toleranceAbsolute");
                var referenceNode = Required(metric, "reference");
                var reference = FiniteNonNegative(referenceNode);
                var maximum = OptionalFiniteNonNegative(metric, "maximum");
                var relative = OptionalFiniteNonNegative(metric, "toleranceRelative");
                var absolute = OptionalFiniteNonNegative(metric, "toleranceAbsolute");
                if (maximum is null && relative is null && absolute is null) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "A performance metric requires maximum or a tolerance.", metric.Range);
                var unit = RequiredString(metric, "unit");
                if (!ValidPerformanceUnit(unit)) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, $"Unknown performance unit '{unit}'.", Property(metric, "unit")!.Value.Range);
                metrics.Add(new ConformancePerformanceMetric(metricProperty.Name, reference, maximum, relative, absolute, unit, referenceNode.Range));
            }
            if (metrics.Count == 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "A performance profile requires at least one metric.", metricsNode.Range);
            profiles.Add(new ConformancePerformanceProfile(profileProperty.Name, metrics));
        }
        if (profiles.Count == 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "performance.profiles cannot be empty.", profilesNode.Range);
        return new ConformancePerformanceExpectation(profiles);
    }

    private static IReadOnlyList<ConformanceMessage> BindMessages(YamlNode? node)
    {
        var result = new List<ConformanceMessage>();
        if (node is null) return result;
        RequireKind(node, YamlNodeKind.Sequence, "Messages must be a sequence.");
        foreach (var item in node.Items) result.Add(BindMessage(item));
        return result;
    }

    private static ConformanceMessage BindMessage(YamlNode node)
    {
        Closed(node, "name", "tags", "args");
        var args = Optional(node, "args");
        return new ConformanceMessage(RequiredString(node, "name"), OptionalStringList(node, "tags") ?? new List<string>(), args is null ? Array.Empty<ConformanceArgument>() : BindArguments(args));
    }

    private static IReadOnlyList<ConformanceArgument> BindArguments(YamlNode node)
    {
        RequireKind(node, YamlNodeKind.Sequence, "Arguments must be an ordered sequence.");
        var result = new List<ConformanceArgument>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in node.Items)
        {
            Closed(item, "name", "value");
            var name = RequiredString(item, "name");
            if (name != GameEventScriptMessageSignature.UnlabeledParameterName && !names.Add(name))
                throw Schema(ConformanceDiagnosticCodes.SchemaDuplicateId, $"Duplicate argument name '{name}'.", Property(item, "name")!.Value.Range);
            result.Add(new ConformanceArgument(name, BindValue(Required(item, "value"))));
        }
        return result;
    }

    private static ConformanceValue BindValue(YamlNode node)
    {
        RequireKind(node, YamlNodeKind.Mapping, "A portable value must be a mapping.");
        var type = RequiredString(node, "type");
        var allowed = type switch
        {
            ":Nothing" => new[] { "type" },
            ":Text" or ":Tag" or ":Boolean" or ":Percentage" => new[] { "type", "value" },
            ":Number.int64" or ":Number.binary64" => new[] { "type", "value" },
            ":Quantity.int64" or ":Quantity.binary64" => new[] { "type", "value", "unit" },
            ":Vector" or ":Point" => new[] { "type", "x", "y", "z", "unit" },
            ":List" => new[] { "type", "items" },
            ":Map" => new[] { "type", "entries" },
            ":Dice" => new[] { "type", "rolls" },
            ":Range.int64" or ":Range.binary64" => new[] { "type", "from", "to", "step" },
            ":Message" => new[] { "type", "message" },
            _ => new[] { "type", "entries" }
        };
        Closed(node, allowed);
        var scalars = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { "value", "unit", "x", "y", "z", "from", "to", "step" })
        {
            var value = Optional(node, name);
            if (value is not null) scalars.Add(name, ScalarText(value));
        }
        var items = new List<ConformanceValue>();
        var itemsNode = Optional(node, "items");
        if (itemsNode is not null) { RequireKind(itemsNode, YamlNodeKind.Sequence, "value.items must be a sequence."); foreach (var item in itemsNode.Items) items.Add(BindValue(item)); }
        var entries = new List<ConformanceValueEntry>();
        var entriesNode = Optional(node, "entries");
        if (entriesNode is not null)
        {
            RequireKind(entriesNode, YamlNodeKind.Sequence, "value.entries must be a sequence.");
            foreach (var entry in entriesNode.Items)
            {
                Closed(entry, "key", "value");
                var key = RequiredString(entry, "key");
                entries.Add(new ConformanceValueEntry(key, BindValue(Required(entry, "value"))));
            }
        }
        var rolls = new List<int>();
        var rollsNode = Optional(node, "rolls");
        if (rollsNode is not null)
        {
            RequireKind(rollsNode, YamlNodeKind.Sequence, "dice.rolls must be a sequence.");
            foreach (var roll in rollsNode.Items) rolls.Add(ParseInt32(roll));
        }
        var messageNode = Optional(node, "message");
        var message = messageNode is null ? null : BindMessage(messageNode);
        if (type.Length < 2 || type[0] != ':') throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A value type must be a canonical name beginning with ':'.", Property(node, "type")!.Value.Range);
        ValidateValueCardinality(type, node, scalars, itemsNode, entriesNode, messageNode);
        return new ConformanceValue(type, scalars, items, entries, rolls, message);
    }

    private static void ValidateValueCardinality(string type, YamlNode node, IReadOnlyDictionary<string, string> scalars, YamlNode? items, YamlNode? entries, YamlNode? message)
    {
        bool Has(params string[] names) => names.All(scalars.ContainsKey);
        var valid = type switch
        {
            ":Nothing" => true,
            ":Text" or ":Tag" or ":Boolean" or ":Percentage" => Has("value"),
            ":Number.int64" or ":Number.binary64" or ":Quantity.int64" or ":Quantity.binary64" => Has("value"),
            ":Vector" or ":Point" => Has("x", "y", "z"),
            ":List" => items is not null,
            ":Map" => entries is not null,
            ":Dice" => Optional(node, "rolls") is not null,
            ":Range.int64" or ":Range.binary64" => Has("from", "to", "step"),
            ":Message" => message is not null,
            _ => entries is not null
        };
        if (!valid) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, $"Portable value type '{type}' is missing required fields.", node.Range);
        if (type is ":Number.int64" or ":Quantity.int64")
        {
            var integerNode = Required(node, "value");
            _ = String(integerNode);
            _ = ParseInt64(integerNode);
        }
        if (type == ":Boolean") _ = Boolean(Required(node, "value"));
        if (type is ":Text" or ":Tag") _ = RequiredString(node, "value");
        if (Optional(node, "unit") is { } unit) _ = String(unit);
        if (type is ":Number.binary64" or ":Quantity.binary64" or ":Percentage") RequireFiniteOrSpecialBinary64(RequiredString(node, "value"), Required(node, "value").Range);
        foreach (var name in new[] { "x", "y", "z", "from", "to", "step" }) if (Optional(node, name) is { } numeric) RequireFiniteOrSpecialBinary64(String(numeric), numeric.Range);
        if (type == ":Dice") RequireKind(Required(node, "rolls"), YamlNodeKind.Sequence, "dice.rolls must be a sequence.");
        if (type == ":Range.int64")
        {
            _ = ParseInt64(Required(node, "from"));
            _ = ParseInt64(Required(node, "to"));
            _ = ParseInt64(Required(node, "step"));
        }
    }

    private static void ValidateCardinality(
        ConformanceTestKind kind,
        MarkdownCaseSyntax syntax,
        YamlNode? expectation,
        int sourceCount,
        int nativeHandlerCount,
        ConformanceMessageApiCase? messageApi,
        ConformanceValueApiCase? valueApi,
        ConformanceExternalTypeApiCase? externalTypeApi,
        ConformanceBinaryFixture? binaryFixture,
        ConformancePerformanceWorkload? workload)
    {
        var apiOnly = kind is ConformanceTestKind.MessageApi or ConformanceTestKind.ValueApi or ConformanceTestKind.ExternalTypeApi;
        if (apiOnly && sourceCount != 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "API object tests do not accept GES source.", syntax.Sources[0].BlockRange);
        if (!apiOnly && kind != ConformanceTestKind.ProgramBinary && sourceCount == 0 && !(kind == ConformanceTestKind.ScriptApi && nativeHandlerCount > 0))
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "This test kind requires GES source.", syntax.CaseBlock!.BlockRange);
        if (kind == ConformanceTestKind.BytecodeSnapshot)
        {
            if (syntax.AssemblerBlock is null || expectation is not null) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "bytecodeSnapshot requires one gesa block and no expectation block.", syntax.CaseBlock!.BlockRange);
        }
        else if (syntax.AssemblerBlock is not null) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "Only bytecodeSnapshot accepts a gesa block.", syntax.AssemblerBlock.BlockRange);
        if (kind is (ConformanceTestKind.CompileError or ConformanceTestKind.LoadError or ConformanceTestKind.MessageApi or ConformanceTestKind.ValueApi or ConformanceTestKind.ExternalTypeApi or
                     ConformanceTestKind.CompileMetadata or ConformanceTestKind.Bytecode or ConformanceTestKind.Performance or ConformanceTestKind.ProgramBinary) &&
            expectation is null)
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "This test kind requires a yaml block with 'gesBlock: expect'.", syntax.CaseBlock!.BlockRange);
        if (expectation is not null)
        {
            var requiredExpectation = kind switch
            {
                ConformanceTestKind.CompileError or ConformanceTestKind.LoadError => "error",
                ConformanceTestKind.MessageApi => "message",
                ConformanceTestKind.ValueApi => "value",
                ConformanceTestKind.ExternalTypeApi => "externalType",
                ConformanceTestKind.CompileMetadata => "metadata",
                ConformanceTestKind.Bytecode => "opcodes",
                ConformanceTestKind.Performance => "performance",
                ConformanceTestKind.ProgramBinary => "binary",
                _ => null
            };
            if (requiredExpectation is not null && Optional(expectation, requiredExpectation) is null)
                throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, $"This test kind requires expectation field '{requiredExpectation}'.", expectation.Range);
        }
        if (kind == ConformanceTestKind.MessageApi && messageApi is null) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "messageApi case metadata is required.", syntax.CaseBlock!.BlockRange);
        if (kind != ConformanceTestKind.MessageApi && messageApi is not null) throw Schema(ConformanceDiagnosticCodes.SchemaUnknownField, "messageApi metadata is only valid for messageApi tests.", syntax.CaseBlock!.BlockRange);
        if (kind == ConformanceTestKind.ValueApi && valueApi is null) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "valueApi case metadata is required.", syntax.CaseBlock!.BlockRange);
        if (kind != ConformanceTestKind.ValueApi && valueApi is not null) throw Schema(ConformanceDiagnosticCodes.SchemaUnknownField, "valueApi metadata is only valid for valueApi tests.", syntax.CaseBlock!.BlockRange);
        if (kind == ConformanceTestKind.ExternalTypeApi && externalTypeApi is null) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "externalTypeApi case metadata is required.", syntax.CaseBlock!.BlockRange);
        if (kind != ConformanceTestKind.ExternalTypeApi && externalTypeApi is not null) throw Schema(ConformanceDiagnosticCodes.SchemaUnknownField, "externalTypeApi metadata is only valid for externalTypeApi tests.", syntax.CaseBlock!.BlockRange);
        if (kind == ConformanceTestKind.ProgramBinary && binaryFixture is null) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "binaryFixture case metadata is required.", syntax.CaseBlock!.BlockRange);
        if (kind != ConformanceTestKind.ProgramBinary && binaryFixture is not null) throw Schema(ConformanceDiagnosticCodes.SchemaUnknownField, "binaryFixture metadata is only valid for programBinary tests.", syntax.CaseBlock!.BlockRange);
        if (binaryFixture?.CompareCompiledRuntime == true && sourceCount == 0) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "compareCompiledRuntime requires provenance source.", syntax.CaseBlock!.BlockRange);
        if (kind == ConformanceTestKind.Performance && workload is null) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "performance workload metadata is required.", syntax.CaseBlock!.BlockRange);
        if (kind != ConformanceTestKind.Performance && workload is not null) throw Schema(ConformanceDiagnosticCodes.SchemaUnknownField, "performance workload metadata is only valid for performance tests.", syntax.CaseBlock!.BlockRange);
        if (kind is ConformanceTestKind.ScriptApi or ConformanceTestKind.Performance)
        {
            var hasInitialization = expectation is not null && Optional(expectation, "initialization") is not null;
            if (!syntax.HasStepsTable && !hasInitialization) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "A runtime test requires Steps or an initialization expectation.", syntax.CaseBlock!.BlockRange);
        }
        else if (kind == ConformanceTestKind.ProgramBinary)
        {
            var outcome = expectation is null ? null : Optional(Optional(expectation, "binary")!, "outcome");
            if (syntax.HasStepsTable && (outcome is null || String(outcome) != "valid"))
                throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "Only a valid programBinary case accepts Steps.", syntax.CaseBlock!.BlockRange);
            if (!syntax.HasStepsTable && expectation is not null && Optional(expectation, "initialization") is not null)
                throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "A programBinary initialization expectation requires Steps.", syntax.CaseBlock!.BlockRange);
        }
        else if (syntax.HasStepsTable) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "This test kind does not accept Steps.", syntax.CaseBlock!.BlockRange);
    }

    private static void AddKindCapabilities(ConformanceTestKind kind, List<string> core, List<string> optional)
    {
        void Core(params string[] values) { foreach (var value in values) AddUnique(core, value); }
        switch (kind)
        {
            case ConformanceTestKind.ScriptApi: Core("compiler", "host", "vm", "observer", "publish-sink"); break;
            case ConformanceTestKind.CompileError: Core("compiler"); break;
            case ConformanceTestKind.LoadError: Core("compiler", "host", "vm"); break;
            case ConformanceTestKind.MessageApi: Core("message-api"); break;
            case ConformanceTestKind.ValueApi: Core("value-api"); break;
            case ConformanceTestKind.ExternalTypeApi: Core("external-types"); break;
            case ConformanceTestKind.CompileMetadata:
            case ConformanceTestKind.Bytecode: Core("compiler"); break;
            case ConformanceTestKind.Performance: Core("compiler", "host", "vm", "observer", "publish-sink"); AddUnique(optional, "performance"); break;
            case ConformanceTestKind.BytecodeSnapshot: Core("compiler"); AddUnique(optional, "bytecode-snapshot"); break;
            case ConformanceTestKind.ProgramBinary: Core("program-binary"); break;
        }
    }

    private static ConformanceCapabilityRequirements BindRequirements(YamlNode? node)
    {
        if (node is null) return new ConformanceCapabilityRequirements(Array.Empty<string>(), Array.Empty<string>());
        Closed(node, "core", "optional");
        return new ConformanceCapabilityRequirements(OptionalStringList(node, "core", ids: true) ?? new List<string>(), OptionalStringList(node, "optional", ids: true) ?? new List<string>());
    }

    private static ConformanceCapabilityRequirements MergeRequirements(ConformanceCapabilityRequirements? parent, ConformanceCapabilityRequirements current)
        => new(Merge(parent?.Core, current.Core), Merge(parent?.Optional, current.Optional));

    private static ConformanceCompileOptions BindCompileOptions(YamlNode? node, ConformanceCompileOptions? parent)
    {
        var debug = parent?.DebugInfo.ToList() ?? new List<string> { "debugSymbols", "sourceMap", "sourceArchive" };
        var binary = parent?.BinaryRoundTrip ?? false;
        if (node is not null)
        {
            Closed(node, "debugInfo", "binaryRoundTrip");
            if (Optional(node, "debugInfo") is { } debugNode)
            {
                debug = StringList(debugNode, ids: false);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in debug)
                    if (item is not ("debugSymbols" or "sourceMap" or "sourceArchive") || !seen.Add(item)) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, $"Invalid or duplicate debugInfo value '{item}'.", debugNode.Range);
            }
            binary = OptionalBoolean(node, "binaryRoundTrip") ?? binary;
        }
        return new ConformanceCompileOptions(debug, binary);
    }

    private static ConformanceRuntimeLimits BindRuntimeLimits(YamlNode? node, ConformanceRuntimeLimits? parent)
    {
        var values = parent is null ? new Dictionary<string, ulong>(StringComparer.Ordinal) : new Dictionary<string, ulong>(parent.Values, StringComparer.Ordinal);
        if (node is not null)
        {
            Closed(node, RuntimeLimitNames);
            foreach (var property in node.Properties)
            {
                var value = ParseUInt64(property.Value);
                if (value == 0) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "Runtime limits must be positive.", property.Value.Range);
                values[property.Name] = value;
            }
        }
        return new ConformanceRuntimeLimits(values);
    }

    private static ConformanceComparisonOptions BindComparison(YamlNode? node, ConformanceComparisonOptions? parent)
    {
        var mode = parent?.Binary64Mode ?? ConformanceBinary64ComparisonMode.Exact;
        var maxUlps = parent?.MaxUlps ?? 0;
        if (node is not null)
        {
            Closed(node, "binary64");
            var binary = Required(node, "binary64");
            Closed(binary, "mode", "maxUlps");
            mode = RequiredString(binary, "mode") switch
            {
                "exact" => ConformanceBinary64ComparisonMode.Exact,
                "ulp" => ConformanceBinary64ComparisonMode.Ulp,
                var other => throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, $"Unknown Binary64 comparison mode '{other}'.", Property(binary, "mode")!.Value.Range)
            };
            var specifiedUlps = OptionalUInt64(binary, "maxUlps");
            if (mode == ConformanceBinary64ComparisonMode.Ulp && specifiedUlps is null) throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, "ULP comparison requires maxUlps.", binary.Range);
            if (mode == ConformanceBinary64ComparisonMode.Exact && specifiedUlps is not null) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "Exact comparison does not accept maxUlps.", Property(binary, "maxUlps")!.Value.Range);
            maxUlps = specifiedUlps ?? 0;
        }
        return new ConformanceComparisonOptions(mode, maxUlps);
    }

    private static Dictionary<string, ulong> BindUInt64Map(YamlNode? node)
    {
        var result = new Dictionary<string, ulong>(StringComparer.Ordinal);
        if (node is null) return result;
        RequireKind(node, YamlNodeKind.Mapping, "This value must be a mapping.");
        foreach (var property in node.Properties) result.Add(property.Name, ParseUInt64(property.Value));
        return result;
    }

    private static void ValidateCapabilities(IReadOnlyList<string> core, IReadOnlyList<string> optional, ConformanceSourceRange range)
    {
        var known = new HashSet<string>(KnownCapabilities, StringComparer.Ordinal);
        foreach (var value in core.Concat(optional)) if (!known.Contains(value)) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, $"Unknown V1 capability '{value}'.", range);
        var overlap = new HashSet<string>(core, StringComparer.Ordinal);
        foreach (var value in optional) if (overlap.Contains(value)) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, $"Capability '{value}' cannot be both core and optional.", range);
    }

    private static void Closed(YamlNode node, params string[] names)
    {
        RequireKind(node, YamlNodeKind.Mapping, "A mapping is required.");
        var allowed = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var property in node.Properties) if (!allowed.Contains(property.Name)) throw Schema(ConformanceDiagnosticCodes.SchemaUnknownField, $"Unknown field '{property.Name}'.", property.NameRange);
    }

    private static YamlProperty? Property(YamlNode node, string name) => node.Properties.FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.Ordinal));
    private static YamlNode? Optional(YamlNode node, string name) => Property(node, name)?.Value;
    private static YamlNode Required(YamlNode node, string name) => Optional(node, name) ?? throw Schema(ConformanceDiagnosticCodes.SchemaMissingField, $"Missing required field '{name}'.", node.Range);
    private static string RequiredString(YamlNode node, string name) => String(Required(node, name));
    private static string? OptionalString(YamlNode node, string name) => Optional(node, name) is { } value ? String(value) : null;
    private static bool? OptionalBoolean(YamlNode node, string name) => Optional(node, name) is { } value ? Boolean(value) : null;
    private static bool RequiredBoolean(YamlNode node, string name) => Boolean(Required(node, name));
    private static int? OptionalInt32(YamlNode node, string name) => Optional(node, name) is { } value ? ParseInt32(value) : null;
    private static uint? OptionalUInt32(YamlNode node, string name) => Optional(node, name) is { } value ? ParseUInt32(value) : null;
    private static ulong? OptionalUInt64(YamlNode node, string name) => Optional(node, name) is { } value ? ParseUInt64(value) : null;
    private static int RequiredInt32(YamlNode node, string name) => ParseInt32(Required(node, name));
    private static uint RequiredUInt32(YamlNode node, string name) => ParseUInt32(Required(node, name));
    private static string RequiredId(YamlNode node, string name) { var value = RequiredString(node, name); RequireId(value, Property(node, name)!.Value.Range); return value; }

    private static string String(YamlNode node)
    {
        if (node.Kind != YamlNodeKind.String) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A string is required.", node.Range);
        return node.Scalar!;
    }

    private static bool Boolean(YamlNode node)
    {
        if (node.Kind != YamlNodeKind.Boolean) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A boolean is required.", node.Range);
        return node.Scalar == "true";
    }

    private static string ScalarText(YamlNode node)
    {
        if (node.Kind is YamlNodeKind.Mapping or YamlNodeKind.Sequence or YamlNodeKind.Null) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A non-null scalar is required.", node.Range);
        return node.Scalar!;
    }

    private static long ParseInt64(YamlNode node)
    {
        var value = node.Kind == YamlNodeKind.String ? node.Scalar! : node.Kind == YamlNodeKind.Integer ? node.Scalar! : throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "An Int64 is required.", node.Range);
        if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "The value is outside Int64.", node.Range);
        return parsed;
    }
    private static int ParseInt32(YamlNode node)
    {
        var value = ParseInt64(node);
        if (value < int.MinValue || value > int.MaxValue) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "The value is outside Int32.", node.Range);
        return (int)value;
    }
    private static uint ParseUInt32(YamlNode node) { var value = ParseUInt64(node); if (value > uint.MaxValue) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "The value is outside UInt32.", node.Range); return (uint)value; }
    private static ulong ParseUInt64(YamlNode node)
    {
        if (node.Kind != YamlNodeKind.Integer || node.Scalar![0] == '-' || !ulong.TryParse(node.Scalar, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A UInt64 is required.", node.Range);
        return parsed;
    }

    private static List<string>? OptionalStringList(YamlNode node, string name, bool ids = false) => Optional(node, name) is { } value ? StringList(value, ids) : null;
    private static List<string> StringList(YamlNode node, bool ids)
    {
        RequireKind(node, YamlNodeKind.Sequence, "A string sequence is required.");
        var result = new List<string>();
        foreach (var item in node.Items) { var value = String(item); if (ids) RequireId(value, item.Range); result.Add(value); }
        return result;
    }

    private static ConformanceTestKind? OptionalKind(YamlNode node, string name)
    {
        var value = OptionalString(node, name);
        if (value is null) return null;
        return value switch
        {
            "scriptApi" => ConformanceTestKind.ScriptApi,
            "compileError" => ConformanceTestKind.CompileError,
            "loadError" => ConformanceTestKind.LoadError,
            "messageApi" => ConformanceTestKind.MessageApi,
            "compileMetadata" => ConformanceTestKind.CompileMetadata,
            "bytecode" => ConformanceTestKind.Bytecode,
            "performance" => ConformanceTestKind.Performance,
            "bytecodeSnapshot" => ConformanceTestKind.BytecodeSnapshot,
            "valueApi" => ConformanceTestKind.ValueApi,
            "externalTypeApi" => ConformanceTestKind.ExternalTypeApi,
            "programBinary" => ConformanceTestKind.ProgramBinary,
            _ => throw Schema(ConformanceDiagnosticCodes.SchemaUnknownKind, $"Unknown test kind '{value}'.", Property(node, name)!.Value.Range)
        };
    }

    private static ConformanceTestLevel? OptionalLevel(YamlNode node, string name)
    {
        var value = OptionalString(node, name);
        if (value is null) return null;
        return value switch
        {
            "atomic" => ConformanceTestLevel.Atomic,
            "scenario" => ConformanceTestLevel.Scenario,
            _ => throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, $"Unknown test level '{value}'.", Property(node, name)!.Value.Range)
        };
    }

    private static List<string> Merge(IReadOnlyList<string>? parent, IReadOnlyList<string>? current)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (parent is not null) foreach (var value in parent) if (seen.Add(value)) result.Add(value);
        if (current is not null) foreach (var value in current) if (seen.Add(value)) result.Add(value);
        return result;
    }

    private static void AddUnique(List<string> target, string value) { if (!target.Contains(value, StringComparer.Ordinal)) target.Add(value); }
    private static void RequireId(string value, ConformanceSourceRange range)
    {
        if (value.Length == 0 || value[0] is < 'a' or > 'z') throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, $"Invalid ID '{value}'.", range);
        var needAlphanumeric = false;
        for (var index = 1; index < value.Length; index++)
        {
            var c = value[index];
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') { needAlphanumeric = false; continue; }
            if ((c == '.' || c == '-') && !needAlphanumeric) { needAlphanumeric = true; continue; }
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, $"Invalid ID '{value}'.", range);
        }
        if (needAlphanumeric) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, $"Invalid ID '{value}'.", range);
    }

    private static string FiniteNonNegative(YamlNode node)
    {
        var text = ScalarText(node);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A finite non-negative Binary64 value is required.", node.Range);
        return text;
    }
    private static string? OptionalFiniteNonNegative(YamlNode node, string name) => Optional(node, name) is { } value ? FiniteNonNegative(value) : null;
    private static void RequireFiniteBinary64(string text, ConformanceSourceRange range)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsNaN(value) || double.IsInfinity(value))
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A finite Binary64 value is required.", range);
    }
    private static void RequireCanonicalFiniteBinary64(string text, ConformanceSourceRange range)
    {
        RequireFiniteBinary64(text, range);
        var value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!string.Equals(text, FormatCanonicalBinary64(value), StringComparison.Ordinal))
            throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "A Binary64 string must use canonical shortest-roundtrip spelling.", range);
    }
    private static void RequireFiniteOrSpecialBinary64(string text, ConformanceSourceRange range)
    {
        if (text is "Infinity" or "-Infinity" or "NaN") return;
        RequireCanonicalFiniteBinary64(text, range);
    }
    private static string FormatCanonicalBinary64(double value)
    {
        if (value == 0d) return "0";
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var exponentIndex = text.IndexOf('E');
        if (exponentIndex < 0) return text;
        var exponent = int.Parse(text.Substring(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        return text.Substring(0, exponentIndex) + "e" + exponent.ToString(CultureInfo.InvariantCulture);
    }
    private static bool ValidPerformanceUnit(string unit)
    {
        var baseUnit = unit.EndsWith("/iteration", StringComparison.Ordinal) ? unit.Substring(0, unit.Length - 10) : unit;
        return baseUnit is "ns" or "us" or "ms" or "s" or "B" or "KiB" or "count";
    }
    private static void RequireKind(YamlNode node, YamlNodeKind kind, string message) { if (node.Kind != kind) throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, message, node.Range); }
    private static ConformancePublishSinkMode BindPublishSink(string? value, ConformanceSourceRange range) => value switch
    {
        null or "accept" => ConformancePublishSinkMode.Accept,
        "absent" => ConformancePublishSinkMode.Absent,
        "reject" => ConformancePublishSinkMode.Reject,
        "throw" => ConformancePublishSinkMode.Throw,
        _ => throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "publishSink must be accept, absent, reject, or throw.", range)
    };

    private static ConformanceExternalTypeRegistryMode BindExternalTypeRegistry(string? value, ConformanceSourceRange range) => value switch
    {
        null or "environment" => ConformanceExternalTypeRegistryMode.Environment,
        "absent" => ConformanceExternalTypeRegistryMode.Absent,
        "mismatch" => ConformanceExternalTypeRegistryMode.Mismatch,
        _ => throw Schema(ConformanceDiagnosticCodes.SchemaInvalidValue, "externalTypeRegistry must be environment, absent or mismatch.", range)
    };

    private static ConformanceObservationExpectation EmptyObservations()
        => new(
            Array.Empty<ConformanceRuntimeLimitExpectation>(), Array.Empty<ConformanceRuntimeLimitExpectation>(), Array.Empty<ConformanceExpectedDiagnostic>(), false, Array.Empty<ConformanceObserverEventExpectation>());
    private static ConformanceFailure Schema(string code, string message, ConformanceSourceRange range) => new(code, message, range);

    private sealed class Defaults
    {
        internal Defaults(
            ConformanceTestKind? kind,
            ConformanceTestLevel? level,
            List<string> categories,
            List<string> tags,
            ConformanceCapabilityRequirements requires,
            ConformanceCompileOptions compile,
            ConformanceRuntimeLimits runtimeLimits,
            ConformanceComparisonOptions comparison)
        { Kind = kind; Level = level; Categories = categories; Tags = tags; Requires = requires; Compile = compile; RuntimeLimits = runtimeLimits; Comparison = comparison; }
        internal ConformanceTestKind? Kind { get; }
        internal ConformanceTestLevel? Level { get; }
        internal List<string> Categories { get; }
        internal List<string> Tags { get; }
        internal ConformanceCapabilityRequirements Requires { get; }
        internal ConformanceCompileOptions Compile { get; }
        internal ConformanceRuntimeLimits RuntimeLimits { get; }
        internal ConformanceComparisonOptions Comparison { get; }
    }
}
