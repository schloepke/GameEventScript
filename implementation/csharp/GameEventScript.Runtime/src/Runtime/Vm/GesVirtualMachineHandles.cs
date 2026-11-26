// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

// Handle members, type checks and lazy state reads (specs/Language.md#handles, specs/HostRuntime.md#handles).
internal partial class GesVirtualMachine
{
    // Keys of the per-invocation state cache: type and identity as handle equality has them, and the carried version.
    private sealed class HandleStateComparer : IEqualityComparer<GesHandle>
    {
        internal static readonly HandleStateComparer Instance = new();

        public bool Equals(GesHandle? left, GesHandle? right)
            => left is not null && right is not null && left.Equals(right) && GesHandleStates.VersionEquals(left.Version, right.Version);

        public int GetHashCode(GesHandle handle) => handle.GetHashCode();
    }

    private static readonly string[] CompositeIdKeys = ["part", "root"];

    // The states the running handler invocation has read; it survives pauses and ends with the invocation.
    private Dictionary<GesHandle, GesHandleState>? _handleStates;
    private GesHandleResolveCall? _handleResolveCall;

    private void ClearHandleStates() => _handleStates?.Clear();

    // `@Name` matches a handle of that type, `@Name~` one that also carries a version.
    private static bool IsHandleType(in GesValue value, string typeName)
    {
        if (value.ObjectValue is not GesHandle handle) return false;
        var versioned = typeName[typeName.Length - 1] == '~';
        var length = typeName.Length - 1 - (versioned ? 1 : 0);
        return handle.TypeName.Length == length && string.CompareOrdinal(handle.TypeName, 0, typeName, 1, length) == 0 && (!versioned || handle.HasVersion);
    }

    // Reads a member of a handle: the identity members need no state, every other member reads the resolved state.
    private void ReadHandleMember(ushort destinationRegister, string key, GesHandle handle)
    {
        switch (key)
        {
            case "id":
                if (handle.IsComposite)
                {
                    var values = new GesValue[2];
                    values[0] = handle.Part;
                    values[1] = handle.Id;
                    SetMap(destinationRegister, new GesValueMap(CompositeIdKeys, values));
                }
                else
                {
                    var id = handle.Id;
                    SetValue(destinationRegister, in id);
                }

                return;
            case "version":
                var version = handle.Version;
                SetValue(destinationRegister, in version);
                return;
            // Without a static type the root type is unknown, so `root` gives nothing and never reads state.
            case "root":
                SetNothing(destinationRegister);
                return;
        }

        // `@Root` names the root type of a composite, whose handle keeps the composite's identity root and version.
        if (key.Length > 1 && key[0] == '@')
        {
            if (!handle.IsComposite)
            {
                SetNothing(destinationRegister);
                return;
            }

            var root = new GesValue();
            root.SetHandle(GesHandle.FromWire(key.Substring(1), handle.Id, handle.Version, default, false));
            SetValue(destinationRegister, in root);
            return;
        }

        var state = ResolveHandleState(handle);
        if (state.ObjectValue is GesCustomObject record && record.Map.Get(key) is { } field)
        {
            SetValue(destinationRegister, in field);
            return;
        }

        SetNothing(destinationRegister);
    }

    // `h as :State`: the whole state of a handle whose type has that state schema, else nothing.
    private bool TryCastHandleState(ushort destinationRegister, GesHandle handle, string typeName)
    {
        if (ActiveProgram!.HandleResolvers is not { } resolvers || !resolvers.TryGetValue(handle.TypeName, out var binding) ||
            !string.Equals(binding.Type.StateName, typeName, StringComparison.Ordinal))
        {
            return false;
        }

        var state = ResolveHandleState(handle);
        SetValue(destinationRegister, in state);
        return true;
    }

    // The state Record of the object, or nothing when it is missing; the first answer of the invocation is reused.
    private GesValue ResolveHandleState(GesHandle handle)
    {
        if (_handleStates is not null && _handleStates.TryGetValue(handle, out var cached)) return cached.State;
        GesHandleBinding? binding = null;
        ActiveProgram!.HandleResolvers?.TryGetValue(handle.TypeName, out binding);
        var resolved = GesHandleStates.Resolve(binding, _handleResolveCall ??= new GesHandleResolveCall(), handle);
        var states = _handleStates ??= new Dictionary<GesHandle, GesHandleState>(HandleStateComparer.Instance);
        states[handle] = resolved;
        // A read without a version picks up the resolved one, so a later read of that version sees the same state.
        if (!handle.HasVersion && !resolved.Version.IsNothing) states.TryAdd(handle.WithVersion(resolved.Version), resolved);
        return resolved.State;
    }
}
