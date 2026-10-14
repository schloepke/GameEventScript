// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBinaryBindKind;
using static GameEventScript.Api.GesBindingSegment;

namespace GameEventScript.Runtime.VM;

internal sealed class GesLinkedProgram
{
    internal readonly struct OutboundMessageSignature
    {
        internal OutboundMessageSignature(string name, string[] argumentNames, string signatureId)
        {
            Name = name;
            ArgumentNames = GesReadOnlyArray<string>.FromOwnedArray(argumentNames);
            SignatureId = signatureId;
            IsValid = true;
        }

        internal bool IsValid { get; }
        internal string Name { get; }
        internal IReadOnlyList<string> ArgumentNames { get; }
        internal string SignatureId { get; }
    }

    internal readonly struct Handler(GesMessageSignature signature, string[] requiredTags, string[] excludedTags, bool matchArguments, ushort entryAddress, ushort requiredRegisterCount, ushort requiredCallStackDepth)
    {
        internal GesMessageSignature Signature { get; } = signature;
        internal string[] RequiredTags { get; } = requiredTags;
        internal string[] ExcludedTags { get; } = excludedTags;
        internal bool MatchArguments { get; } = matchArguments;
        internal ushort EntryAddress { get; } = entryAddress;
        internal ushort RequiredRegisterCount { get; } = requiredRegisterCount;
        internal ushort RequiredCallStackDepth { get; } = requiredCallStackDepth;
    }

    internal IGesProgramProfiler? Profiler { get; set; }

    internal GesLinkedProgram(GameEventScriptProgram program, IGesExtensionRegistry extensionRegistry, IGesExternalTypeRegistry typeRegistry)
    {
        Program = program ?? throw new ArgumentNullException(nameof(program));
        GesProgramCallGraphValidator.Validate(program);
        ValidateResourceMetadata(program);
        StringPool = BuildStringPool(program.StringConstants);
        StringScalarCounts = BuildStringScalarCountsIfNeeded(StringPool);
        TextConstants = new GesValue[StringPool.Length];
        for (var i = 0; i < TextConstants.Length; i++) TextConstants[i].SetText(StringPool[i], FetchStringScalarCount((ushort)i));
        CodeSegmentSize = checked((ushort)program.Code.Length);
        RecordConstructors = BuildIdIndexedBindTable(program, Record);
        ExtensionCallBinds = BuildIdIndexedBindTable(program, ExtensionCall);
        ExternalTypeBinds = BuildIdIndexedBindTable(program, ExternalType);
        OutboundMessageSignatures = BuildOutboundMessageSignatures(program);
        BoundExtensionCalls = BindExtensions(extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance);
        BoundExternalTypeConstructors = BindExternalTypes(typeRegistry ?? GameEventScriptEmptyExternalTypeRegistry.Instance);
        Handlers = BuildHandlers(program);
        RequiredRegisterCapacity = program.RequiredRegisterCount;
    }

    private GesMapLayout?[]? _mapLayouts;
    private readonly Dictionary<uint, int[]?> _dataConstructionPositions = [];

    // Canonical key order of a constant map-literal key list, computed on first use.
    internal GesMapLayout MapLayout(ushort keyNamesIndex)
    {
        _mapLayouts ??= new GesMapLayout?[Program.UInt16IndexLists.Slices.Count];
        return _mapLayouts[keyNamesIndex] ??= new GesMapLayout(Program.UInt16IndexLists.Resolve(keyNamesIndex), StringPool);
    }

    // Argument positions of a constant data-construction type and label list, resolved on first use;
    // null when the shape is invalid and construction yields Nothing.
    internal int[]? DataConstructionPositions(ushort typeIndex, ushort labelsIndex)
    {
        var key = (uint)typeIndex << 16 | labelsIndex;
        if (_dataConstructionPositions.TryGetValue(key, out var cached)) return cached;
        var names = Program.UInt16IndexLists.Resolve(labelsIndex);
        var labels = new string[names.Length];
        for (var i = 0; i < labels.Length; i++) labels[i] = StringPool[names[i]];
        var positions = GesDataConstruction.Positions(StringPool[typeIndex], labels);
        _dataConstructionPositions[key] = positions;
        return positions;
    }

    internal GameEventScriptProgram Program { get; }
    internal string[] StringPool { get; }
    internal GesValue[] TextConstants { get; }
    internal int[]? StringScalarCounts { get; }
    internal ushort CodeSegmentSize { get; }
    internal OutboundMessageSignature[] OutboundMessageSignatures { get; }
    internal GameEventScriptBinaryBindEntry[] RecordConstructors { get; }
    internal GameEventScriptBinaryBindEntry[] ExtensionCallBinds { get; }
    internal GameEventScriptBinaryBindEntry[] ExternalTypeBinds { get; }
    internal IGesExtensionFunction?[] BoundExtensionCalls { get; }
    internal IGesExternalTypeConstructor?[] BoundExternalTypeConstructors { get; }
    internal Handler[] Handlers { get; }
    internal int RequiredRegisterCapacity { get; }

    internal string FetchString(ushort index) => StringPool[index];
    internal int FetchStringScalarCount(ushort index) => StringScalarCounts is { } counts ? counts[index] : StringPool[index].Length;

    private Handler[] BuildHandlers(GameEventScriptProgram program)
    {
        var count = 0;
        foreach (var bind in program.Bindings.Entries)
        {
            if (bind.Kind is MessageHandler or MessageNameHandler) count++;
        }

        if (count == 0) return [];
        var handlers = new Handler[count];
        var handlerIndex = 0;
        foreach (var bind in program.Bindings.Entries)
        {
            if (bind.Kind is not (MessageHandler or MessageNameHandler)) continue;
            var name = FetchString(bind.Name);
            var arguments = ReadStrings(bind.ArgumentNames);
            handlers[handlerIndex++] = new Handler(
                GesMessageSignature.Create(name, arguments),
                ReadStrings(bind.RequiredTags),
                ReadStrings(bind.ExcludedTags),
                bind.Kind == MessageHandler,
                bind.EntryAddress,
                bind.RequiredRegisterCount,
                bind.RequiredCallStackDepth);
        }

        return handlers;
    }

    private string[] ReadStrings(System.Collections.Generic.IReadOnlyList<ushort> pointers)
    {
        if (pointers.Count == 0) return [];
        var values = new string[pointers.Count];
        for (var index = 0; index < values.Length; index++) values[index] = FetchString(pointers[index]);
        return values;
    }

    private IGesExtensionFunction?[] BindExtensions(IGesExtensionRegistry registry)
    {
        var result = new IGesExtensionFunction?[ExtensionCallBinds.Length];
        for (ushort bindId = 0; bindId < ExtensionCallBinds.Length; bindId++)
        {
            var bind = ExtensionCallBinds[bindId];
            if (bind.Kind != ExtensionCall || bind.Id != bindId) continue;
            var fullName = FetchString(bind.Name);
            var separator = fullName.IndexOf('.');
            if (separator <= 0 || separator >= fullName.Length - 1)
                throw LinkError(GesDiagnosticCodes.LinkInvalidExtensionReference,
                    $"External extension reference '{fullName}' has an invalid name.", fullName);
            var reference = new GesExtensionReference(fullName[..separator], fullName[(separator + 1)..], ReadStrings(bind.ArgumentNames));
            result[bindId] = registry.Resolve(reference) ?? throw LinkError(
                GesDiagnosticCodes.LinkMissingExtension,
                $"GameEventScript extension '{reference.SignatureId}' was not dynamically bound to external bind id '{bindId}'.",
                reference.SignatureId);
        }

        return result;
    }

    private IGesExternalTypeConstructor?[] BindExternalTypes(IGesExternalTypeRegistry registry)
    {
        var result = new IGesExternalTypeConstructor?[ExternalTypeBinds.Length];
        for (ushort bindId = 0; bindId < ExternalTypeBinds.Length; bindId++)
        {
            var bind = ExternalTypeBinds[bindId];
            if (bind.Kind != ExternalType || bind.Id != bindId) continue;
            var reference = new GesExternalTypeConstructorReference(FetchString(bind.Name), ReadStrings(bind.ArgumentNames));
            var constructor = registry.Resolve(reference) ?? throw LinkError(
                GesDiagnosticCodes.LinkMissingExternalTypeConstructor,
                $"GameEventScript external type constructor ':{reference.SignatureId}' was not dynamically bound to external bind id '{bindId}'.",
                reference.SignatureId);
            if (!string.Equals(constructor.Definition.SignatureId, reference.SignatureId, StringComparison.Ordinal))
                throw LinkError(
                    GesDiagnosticCodes.LinkMismatchedExternalTypeConstructor,
                    $"GameEventScript external type constructor registry returned '{constructor.Definition.SignatureId}' for requested reference '{reference.SignatureId}'.",
                    reference.SignatureId);
            result[bindId] = constructor;
        }

        return result;
    }

    private OutboundMessageSignature[] BuildOutboundMessageSignatures(GameEventScriptProgram program)
    {
        var binds = BuildIdIndexedBindTable(program, OutboundMessage);
        if (binds.Length == 0) return [];
        var signatures = new OutboundMessageSignature[binds.Length];
        for (var index = 0; index < binds.Length; index++)
        {
            var bind = binds[index];
            if (bind.Kind != OutboundMessage || bind.Id != index) continue;
            var name = FetchString(bind.Name);
            var arguments = ReadStrings(bind.ArgumentNames);
            signatures[index] = new OutboundMessageSignature(name, arguments,
                GesMessageSignature.CreateSignatureId(name, arguments));
        }

        return signatures;
    }

    private static GameEventScriptBinaryBindEntry[] BuildIdIndexedBindTable(GameEventScriptProgram program, GesBinaryBindKind kind)
    {
        var maxId = -1;
        foreach (var entry in program.Bindings.Entries)
            if (entry.Kind == kind && entry.Id != ushort.MaxValue && entry.Id > maxId) maxId = entry.Id;
        if (maxId < 0) return [];
        var result = new GameEventScriptBinaryBindEntry[maxId + 1];
        foreach (var entry in program.Bindings.Entries)
            if (entry.Kind == kind && entry.Id != ushort.MaxValue) result[entry.Id] = entry;
        return result;
    }

    private static string[] BuildStringPool(GesStringConstantSegment table)
    {
        var strings = new string[table.Slices.Length];
        for (var index = 0; index < strings.Length; index++) strings[index] = table.Resolve((ushort)index);
        return strings;
    }

    private static int[]? BuildStringScalarCountsIfNeeded(string[] strings)
    {
        for (var index = 0; index < strings.Length; index++)
        {
            if (GameEventScriptText.CountScalars(strings[index]) == strings[index].Length) continue;
            var counts = new int[strings.Length];
            for (var countIndex = 0; countIndex < strings.Length; countIndex++)
                counts[countIndex] = GameEventScriptText.CountScalars(strings[countIndex]);
            return counts;
        }

        return null;
    }

    private void ValidateResourceMetadata(GameEventScriptProgram program)
    {
        ushort requiredRegisterCount = 0;
        ushort requiredCallStackDepth = 0;
        foreach (var bind in program.Bindings.Entries)
        {
            if (bind.Kind is not (MessageHandler or MessageNameHandler)) continue;
            requiredRegisterCount = Math.Max(requiredRegisterCount, bind.RequiredRegisterCount);
            requiredCallStackDepth = Math.Max(requiredCallStackDepth, bind.RequiredCallStackDepth);
        }

        if (program.RequiredRegisterCount != requiredRegisterCount ||
            program.RequiredCallStackDepth != requiredCallStackDepth)
        {
            throw LinkError(
                GesDiagnosticCodes.LinkInvalidProgram,
                $"Program resource metadata is inconsistent. Program requires {program.RequiredRegisterCount} registers and " +
                $"{program.RequiredCallStackDepth} call-stack entries, but its message handlers require maxima of " +
                $"{requiredRegisterCount} and {requiredCallStackDepth}.");
        }
    }

    private GesDynamicLinkException LinkError(string code, string message, string? symbol = null)
        => new(new GesDiagnostic(
            GesDiagnosticPhase.Link,
            code,
            message,
            symbol,
            ProgramName: Program.ModuleName));
}

// Canonical GesValueMap order for a fixed key list: ascending scalar keys, last duplicate wins.
internal sealed class GesMapLayout
{
    internal GesMapLayout(GesUInt16IndexList keyNames, string[] stringPool)
    {
        InputCount = keyNames.Length;
        var keys = new List<string>(keyNames.Length);
        var sources = new List<int>(keyNames.Length);
        for (var i = 0; i < keyNames.Length; i++)
        {
            var key = stringPool[keyNames[i]];
            var existing = keys.FindIndex(candidate => string.Equals(candidate, key, StringComparison.Ordinal));
            if (existing >= 0)
            {
                sources[existing] = i;
                continue;
            }

            GameEventScriptText.RequireValidUnicode(key, nameof(keyNames));
            keys.Add(key);
            sources.Add(i);
        }

        Keys = [.. keys];
        Sources = [.. sources];
        Array.Sort(Keys, Sources, GameEventScriptText.ScalarComparer);
    }

    internal int InputCount { get; }
    internal string[] Keys { get; }
    // Staged input position supplying each canonical entry.
    internal int[] Sources { get; }
}
