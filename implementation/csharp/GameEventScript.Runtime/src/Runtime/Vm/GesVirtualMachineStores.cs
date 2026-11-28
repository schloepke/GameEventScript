// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

// Store reads `@X[:count]`, `@X[:keys]`, `@X[:values]`, `@X[:entries]` and the same of one root's parts `@X(root)`
// (specs/Language.md#stores, specs/HostRuntime.md#stores).
internal partial class GesVirtualMachine
{
    internal enum StoreSelector : byte
    {
        Count,
        Keys,
        Values,
        Entries,
    }

    private static readonly string[] StoreEntryKeys = ["key", "value"];
    private static readonly Comparison<GesHandle> HandleOrder = static (left, right) => left.CompareTo(right);

    // The handles each store or root's parts the running invocation listed, in key order; null for a type that does not
    // enumerate. It survives pauses and ends with the invocation like the state cache.
    private Dictionary<(string Type, GesHandle? Root), GesHandle[]?>? _storeHandles;

    private void ClearStoreHandles() => _storeHandles?.Clear();

    private void ReadStore(ushort destinationRegister, ushort typeTextPointer, StoreSelector selector)
    {
        var text = FetchStringByPointer(typeTextPointer);
        var typeName = text.Substring(1);
        if (StoreBinding(typeName) is not { } binding)
        {
            SetNothing(destinationRegister);
            return;
        }

        WriteStore(destinationRegister, binding, StoreHandles(binding, null), selector);
    }

    private void ReadStoreParts(ushort destinationRegister, ushort typeTextPointer, in GesValue rootValue, StoreSelector selector)
    {
        var text = FetchStringByPointer(typeTextPointer);
        var typeName = text.Substring(1);
        // Only a handle of the root type names a root; any other value, like a type without a resolver, reads nothing.
        if (StoreBinding(typeName) is not { } binding || binding.Type.Part is null || binding.Type.Root is not { } rootType ||
            rootValue.ObjectValue is not GesHandle given || given.HasPart || !string.Equals(given.TypeName, rootType.Name, StringComparison.Ordinal))
        {
            SetNothing(destinationRegister);
            return;
        }

        WriteStore(destinationRegister, binding, StoreHandles(binding, given.WithView(default, "")), selector);
    }

    private GesHandleBinding? StoreBinding(string typeName)
        => ActiveProgram!.HandleResolvers is { } resolvers && resolvers.TryGetValue(typeName, out var binding) ? binding : null;

    private void WriteStore(ushort destinationRegister, GesHandleBinding binding, GesHandle[]? handles, StoreSelector selector)
    {
        if (handles is null)
        {
            SetNothing(destinationRegister);
            return;
        }

        switch (selector)
        {
            case StoreSelector.Count:
                SetInteger(destinationRegister, handles.Length);
                return;
            case StoreSelector.Keys:
            {
                var keys = new GesValue[handles.Length];
                for (var index = 0; index < handles.Length; index++) keys[index].SetHandle(handles[index]);
                SetList(destinationRegister, keys);
                return;
            }
        }

        var states = StoreStates(binding, handles);
        if (selector == StoreSelector.Values)
        {
            SetList(destinationRegister, states);
            return;
        }

        var entries = new GesValue[handles.Length];
        for (var index = 0; index < handles.Length; index++)
        {
            var pair = new GesValue[2];
            pair[0].SetHandle(handles[index]);
            pair[1] = states[index];
            entries[index].SetMap(new GesValueMap(StoreEntryKeys, pair, 2));
        }

        SetList(destinationRegister, entries);
    }

    // The resolver's handles of the store, or of one root's parts, checked, without version or view and in key order; the
    // first answer of the invocation is reused.
    private GesHandle[]? StoreHandles(GesHandleBinding binding, GesHandle? root)
    {
        var cache = _storeHandles ??= new Dictionary<(string Type, GesHandle? Root), GesHandle[]?>();
        var key = (binding.Type.Name, root);
        if (cache.TryGetValue(key, out var cached)) return cached;
        var listed = GesHandleStates.Handles(binding, root);
        GesHandle[]? handles = null;
        if (listed is not null)
        {
            var type = binding.Type;
            var hasPart = type.Part is not null;
            handles = new GesHandle[listed.Count];
            for (var index = 0; index < handles.Length; index++)
            {
                var handle = listed[index];
                if (handle is null) throw GesHandleStates.InvalidHandles(type, "a missing handle");
                if (!string.Equals(handle.TypeName, type.Name, StringComparison.Ordinal) || handle.HasPart != hasPart)
                    throw GesHandleStates.InvalidHandles(type, $"the handle {handle} of another type");
                if (root is not null && !GesHandle.FromWire(root.TypeName, handle.Id, default, default, false).Equals(root))
                    throw GesHandleStates.InvalidHandles(type, $"the handle {handle} of another root");
                handles[index] = handle.WithView(default, "");
            }

            Array.Sort(handles, HandleOrder);
            for (var index = 1; index < handles.Length; index++)
                if (handles[index].Equals(handles[index - 1]))
                    throw GesHandleStates.InvalidHandles(type, $"the handle {handles[index]} twice");
        }

        cache[key] = handles;
        return handles;
    }

    // The full states of the handles, nothing for a missing or invisible one; uncached ones resolve in one batch.
    private GesValue[] StoreStates(GesHandleBinding binding, GesHandle[] handles)
    {
        var states = _handleStates ??= new Dictionary<GesHandle, GesHandleState>(HandleStateComparer.Instance);
        var batch = _handleResolveBatch ??= new GesHandleResolveBatch();
        try
        {
            foreach (var handle in handles)
                if (!states.ContainsKey(handle))
                    batch.Add(handle, binding.Type, null, HandleParty);
            if (batch.Count > 0)
            {
                GesHandleStates.ResolveMany(binding, batch);
                for (var index = 0; index < batch.Count; index++) Cache(states, batch[index].Handle, GesHandleStates.Check(binding.Type, batch[index]));
            }
        }
        finally
        {
            batch.End();
        }

        var values = new GesValue[handles.Length];
        for (var index = 0; index < handles.Length; index++) values[index] = states[handles[index]].State;
        return values;
    }
}
