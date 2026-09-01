using System;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindKind;
using static StepH.GameEventScript.Api.GameEventScriptBindingSegment;

namespace StepH.GameEventScript.Runtime.VM;

internal sealed class GesLinkedProgram
{
    internal readonly struct OutboundMessageSignature
    {
        internal OutboundMessageSignature(string name, string[] argumentNames, string signatureId)
        {
            Name = name;
            ArgumentNames = argumentNames;
            SignatureId = signatureId;
            IsValid = true;
        }

        internal bool IsValid { get; }
        internal string Name { get; }
        internal string[] ArgumentNames { get; }
        internal string SignatureId { get; }
    }

    internal readonly struct Handler(
        GameEventScriptMessageSignature signature,
        string[] requiredTags,
        string[] excludedTags,
        bool matchArguments,
        ushort entryAddress,
        ushort requiredRegisterCount,
        ushort requiredCallStackDepth)
    {
        internal GameEventScriptMessageSignature Signature { get; } = signature;
        internal string[] RequiredTags { get; } = requiredTags;
        internal string[] ExcludedTags { get; } = excludedTags;
        internal bool MatchArguments { get; } = matchArguments;
        internal ushort EntryAddress { get; } = entryAddress;
        internal ushort RequiredRegisterCount { get; } = requiredRegisterCount;
        internal ushort RequiredCallStackDepth { get; } = requiredCallStackDepth;
    }

    internal GesLinkedProgram(
        GameEventScriptProgram program,
        IGameEventScriptExtensionRegistry extensionRegistry,
        IGameEventScriptExternalTypeRegistry typeRegistry)
    {
        Program = program ?? throw new ArgumentNullException(nameof(program));
        GesProgramCallGraphValidator.Validate(program);
        ValidateResourceMetadata(program);
        StringPool = BuildStringPool(program.StringConstants);
        StringScalarCounts = BuildStringScalarCountsIfNeeded(StringPool);
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

    internal GameEventScriptProgram Program { get; }
    internal string[] StringPool { get; }
    internal int[]? StringScalarCounts { get; }
    internal ushort CodeSegmentSize { get; }
    internal OutboundMessageSignature[] OutboundMessageSignatures { get; }
    internal GameEventScriptBinaryBindEntry[] RecordConstructors { get; }
    internal GameEventScriptBinaryBindEntry[] ExtensionCallBinds { get; }
    internal GameEventScriptBinaryBindEntry[] ExternalTypeBinds { get; }
    internal IGameEventScriptExtensionFunction?[] BoundExtensionCalls { get; }
    internal IGameEventScriptExternalTypeConstructor?[] BoundExternalTypeConstructors { get; }
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
                GameEventScriptMessageSignature.Create(name, arguments),
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

    private IGameEventScriptExtensionFunction?[] BindExtensions(IGameEventScriptExtensionRegistry registry)
    {
        var result = new IGameEventScriptExtensionFunction?[ExtensionCallBinds.Length];
        for (ushort bindId = 0; bindId < ExtensionCallBinds.Length; bindId++)
        {
            var bind = ExtensionCallBinds[bindId];
            if (bind.Kind != ExtensionCall || bind.Id != bindId) continue;
            var fullName = FetchString(bind.Name);
            var separator = fullName.IndexOf('.');
            if (separator <= 0 || separator >= fullName.Length - 1)
                throw new GameEventScriptDynamicLinkException($"External extension reference '{fullName}' has an invalid name.");
            var reference = new GameEventScriptExtensionReference(fullName[..separator], fullName[(separator + 1)..], ReadStrings(bind.ArgumentNames));
            result[bindId] = registry.Resolve(reference) ?? throw new GameEventScriptDynamicLinkException(
                $"GameEventScript extension '{reference.SignatureId}' was not dynamically bound to external bind id '{bindId}'.");
        }

        return result;
    }

    private IGameEventScriptExternalTypeConstructor?[] BindExternalTypes(IGameEventScriptExternalTypeRegistry registry)
    {
        var result = new IGameEventScriptExternalTypeConstructor?[ExternalTypeBinds.Length];
        for (ushort bindId = 0; bindId < ExternalTypeBinds.Length; bindId++)
        {
            var bind = ExternalTypeBinds[bindId];
            if (bind.Kind != ExternalType || bind.Id != bindId) continue;
            var reference = new GameEventScriptExternalTypeConstructorReference(FetchString(bind.Name), ReadStrings(bind.ArgumentNames));
            result[bindId] = registry.Resolve(reference) ?? throw new GameEventScriptDynamicLinkException(
                $"GameEventScript external type constructor ':{reference.SignatureId}' was not dynamically bound to external bind id '{bindId}'.");
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
                GameEventScriptMessageSignature.CreateSignatureId(name, arguments));
        }

        return signatures;
    }

    private static GameEventScriptBinaryBindEntry[] BuildIdIndexedBindTable(GameEventScriptProgram program, GameEventScriptBinaryBindKind kind)
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

    private static string[] BuildStringPool(GameEventScriptStringConstantSegment table)
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

    private static void ValidateResourceMetadata(GameEventScriptProgram program)
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
            throw new GameEventScriptDynamicLinkException(
                $"Program resource metadata is inconsistent. Program requires {program.RequiredRegisterCount} registers and " +
                $"{program.RequiredCallStackDepth} call-stack entries, but its message handlers require maxima of " +
                $"{requiredRegisterCount} and {requiredCallStackDepth}.");
        }
    }
}
