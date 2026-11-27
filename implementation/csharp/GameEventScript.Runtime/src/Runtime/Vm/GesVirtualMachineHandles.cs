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
    // Keys of the per-invocation state cache: type and identity as handle equality has them, the carried version and the
    // projection.
    private sealed class HandleStateComparer : IEqualityComparer<GesHandle>
    {
        internal static readonly HandleStateComparer Instance = new();

        public bool Equals(GesHandle? left, GesHandle? right)
            => left is not null && right is not null && left.Equals(right) && GesHandleStates.VersionEquals(left.Version, right.Version) &&
               string.Equals(left.Projection, right.Projection, StringComparison.Ordinal);

        public int GetHashCode(GesHandle handle) => handle.GetHashCode();
    }

    private static readonly string[] CompositeIdKeys = ["part", "root"];
    private static readonly string[] RootIdKeys = ["root"];

    // The states the running handler invocation has read; it survives pauses and ends with the invocation.
    private Dictionary<GesHandle, GesHandleState>? _handleStates;
    private GesHandleResolveCall? _handleResolveCall;
    private GesHandleResolveBatch? _handleResolveBatch;
    private HashSet<GesHandle>? _handleBatchSeen;
    // The Lists of the message that hold handles, collected at the first uncached read of the invocation.
    private List<GesValueSlice>? _handleLists;
    private bool _handleListsCollected;
    private GesContext? _handleContext;

    // The party state reads are for: 0 for the host itself (specs/HostRuntime.md#handles).
    internal int HandleParty { get; set; }

    private void ClearHandleStates()
    {
        _handleStates?.Clear();
        _handleLists?.Clear();
        _handleListsCollected = false;
        _handleContext = null;
        ClearStoreHandles();
    }

    // The parts of a handle type text `@Name`, `@Name(:P)`, `@Name~` or `@Name(:P)~`, located without allocating.
    private readonly struct HandleTypeText
    {
        internal HandleTypeText(string text)
        {
            Text = text;
            Versioned = text[text.Length - 1] == '~';
            var end = text.Length - (Versioned ? 1 : 0);
            var open = text.IndexOf('(');
            if (open < 0 || open >= end)
            {
                NameLength = end - 1;
                ProjectionStart = 0;
                ProjectionLength = 0;
            }
            else
            {
                NameLength = open - 1;
                ProjectionStart = open + 2;
                ProjectionLength = end - 1 - ProjectionStart;
            }
        }

        internal string Text { get; }
        internal int NameLength { get; }
        internal int ProjectionStart { get; }
        internal int ProjectionLength { get; }
        internal bool Versioned { get; }
        internal bool Projected => ProjectionLength > 0;

        internal bool NamesType(string typeName) => typeName.Length == NameLength && string.CompareOrdinal(typeName, 0, Text, 1, NameLength) == 0;

        internal bool NamesProjection(string projection)
            => projection.Length == ProjectionLength && string.CompareOrdinal(projection, 0, Text, ProjectionStart, ProjectionLength) == 0;

        internal string Projection => Text.Substring(ProjectionStart, ProjectionLength);
    }

    // `@Name` matches a handle of that type with the full view, `@Name(:P)` one whose view is `P` or can narrow to it,
    // and `~` one that also carries a version.
    private bool IsHandleType(in GesValue value, string typeName)
    {
        if (value.ObjectValue is not GesHandle handle) return false;
        var text = new HandleTypeText(typeName);
        if (!text.NamesType(handle.TypeName) || text.Versioned && !handle.HasVersion) return false;
        if (!text.Projected) return handle.Projection.Length == 0;
        return handle.Projection.Length == 0 || text.NamesProjection(handle.Projection) || CanNarrow(handle, text.Projection);
    }

    // Whether the host's type knows the handle's view and the projection, and the projection narrows the view.
    private bool CanNarrow(GesHandle handle, string projection)
        => HandleBinding(handle) is { } binding && binding.Type.FindProjection(handle.Projection) is { } view &&
           binding.Type.FindProjection(projection) is { } target && target.Declaration.Narrows(view.Declaration);

    private GesHandleBinding? HandleBinding(GesHandle handle)
        => ActiveProgram!.HandleResolvers is { } resolvers && resolvers.TryGetValue(handle.TypeName, out var binding) ? binding : null;

    // A handle passing to a parameter or field of a projected type: an unpinned view drops the version, a pinned one
    // carries the projection's version (specs/Language.md#projections). Nothing when the version cannot be computed.
    private void CastHandleView(ushort destinationRegister, in GesValue value, GesHandle handle, in HandleTypeText text)
    {
        if (text.NamesProjection(handle.Projection))
        {
            SetValue(destinationRegister, in value);
            return;
        }

        var projection = text.Projection;
        GesHandle narrowed;
        if (!text.Versioned) narrowed = handle.WithView(default, projection);
        else if (HandleBinding(handle) is not { } binding || binding.Type.FindProjection(projection) is not { } target) narrowed = null!;
        else if (binding.Type.Version != GesHandleVersionKind.ContentHash) narrowed = handle.WithView(handle.Version, projection);
        else
        {
            var wider = ResolveHandleState(handle);
            if (wider.State.IsNothing) narrowed = null!;
            else
            {
                var state = GesHandleStates.Narrow(binding.Type, handle.WithView(default, projection), target, wider);
                narrowed = handle.WithView(state.Version, projection);
                (_handleStates ??= new Dictionary<GesHandle, GesHandleState>(HandleStateComparer.Instance)).TryAdd(narrowed, state);
            }
        }

        if (narrowed is null)
        {
            SetNothing(destinationRegister);
            return;
        }

        var result = new GesValue();
        result.SetHandle(narrowed);
        SetValue(destinationRegister, in result);
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
            // `@id` is `id` of a statically composite type of which one may be part-less: such a handle holds only its
            // root's id, so its identity is the root alone, without a part.
            case "@id":
                if (handle.IsComposite)
                {
                    var values = new GesValue[2];
                    values[0] = handle.Part;
                    values[1] = handle.Id;
                    SetMap(destinationRegister, new GesValueMap(CompositeIdKeys, values));
                }
                else SetMap(destinationRegister, new GesValueMap(RootIdKeys, [handle.Id]));

                return;
        }

        // `@Root` names the root type of a composite: a composite with a part keeps its root's identity and version, a
        // part-less one, which holds its root's id as its own, gives the root without a version.
        if (key.Length > 1 && key[0] == '@')
        {
            var root = new GesValue();
            root.SetHandle(GesHandle.FromWire(key.Substring(1), handle.Id, handle.IsComposite ? handle.Version : default, default, false));
            SetValue(destinationRegister, in root);
            return;
        }

        var state = ResolveHandleState(handle).State;
        if (state.ObjectValue is GesCustomObject record && record.Map.Get(key) is { } field)
        {
            SetValue(destinationRegister, in field);
            return;
        }

        SetNothing(destinationRegister);
    }

    // `h as :State` gives the whole state of a handle with the full view, `h as :P` the state of a projection that is the
    // handle's view or narrows it; anything else is nothing.
    private bool TryCastHandleState(ushort destinationRegister, GesHandle handle, string typeName)
    {
        if (HandleBinding(handle) is not { } binding) return false;
        var type = binding.Type;
        if (handle.Projection.Length == 0 && string.Equals(type.StateName, typeName, StringComparison.Ordinal))
        {
            var full = ResolveHandleState(handle).State;
            SetValue(destinationRegister, in full);
            return true;
        }

        if (type.FindProjection(typeName) is not { } target) return false;
        if (!string.Equals(handle.Projection, typeName, StringComparison.Ordinal) &&
            (handle.Projection.Length > 0 && (type.FindProjection(handle.Projection) is not { } view || !target.Declaration.Narrows(view.Declaration))))
        {
            return false;
        }

        var wider = ResolveHandleState(handle);
        var state = string.Equals(handle.Projection, typeName, StringComparison.Ordinal) || wider.State.IsNothing
            ? wider.State
            : GesSchemaValues.Project(target.Declaration, wider.State);
        SetValue(destinationRegister, in state);
        return true;
    }

    // `c as :P` of a Record of a schema the projection P narrows: the Record of P with the taken fields, read along their
    // paths (specs/Language.md#projections). The projection comes from a handle type of the host whose state P narrows.
    private bool TryProjectRecord(ushort destinationRegister, in GesValue value, string recordName, string typeName)
    {
        if (ActiveProgram!.HandleResolvers is not { } resolvers) return false;
        foreach (var binding in resolvers.Values)
        {
            if (binding.Type.FindProjection(typeName) is not { } target) continue;
            for (var source = target.Declaration.Source; source is not null; source = source.Source)
            {
                if (!string.Equals(source.Name, recordName, StringComparison.Ordinal)) continue;
                GesValue projected;
                try
                {
                    projected = GesSchemaValues.Project(target.Declaration, value);
                }
                catch (GesWireException)
                {
                    projected = default;
                }

                SetValue(destinationRegister, in projected);
                return true;
            }

            return false;
        }

        return false;
    }

    // The state of the object through the handle's view, or nothing when it is missing or not visible; the first answer
    // of the invocation is reused, and a cached full state serves narrower views.
    private GesHandleState ResolveHandleState(GesHandle handle)
    {
        var states = _handleStates ??= new Dictionary<GesHandle, GesHandleState>(HandleStateComparer.Instance);
        if (states.TryGetValue(handle, out var cached)) return cached;
        // Without a resolver, which only a read without a static type reaches, the object reads as missing.
        if (HandleBinding(handle) is not { } binding) return default;
        GesSchema? projection = null;
        if (handle.Projection.Length > 0)
        {
            projection = binding.Type.FindProjection(handle.Projection) ?? throw new GesVmException(new GesDiagnostic(GesDiagnosticPhase.Runtime,
                GesDiagnosticCodes.RuntimeInvalidHandleState, $"@{handle.TypeName} has no projection :{handle.Projection}.", Symbol: handle.ToString()));
            if (CachedFull(states, handle) is { } wider)
            {
                var narrowed = GesHandleStates.Narrow(binding.Type, handle, projection, wider);
                states[handle] = narrowed;
                return narrowed;
            }
        }

        if (!ResolveBatch(binding, handle, projection, states))
            Cache(states, handle, GesHandleStates.Resolve(binding, _handleResolveCall ??= new GesHandleResolveCall(), handle, projection, HandleParty));
        return states[handle];
    }

    // A full state the invocation already read for the same object and version, or for the object without a version.
    private static GesHandleState? CachedFull(Dictionary<GesHandle, GesHandleState> states, GesHandle handle)
    {
        if (states.TryGetValue(handle.WithView(handle.Version, ""), out var full) && !full.State.IsNothing) return full;
        if (handle.HasVersion && states.TryGetValue(handle.WithView(default, ""), out full) && !full.State.IsNothing) return full;
        return null;
    }

    private void Cache(Dictionary<GesHandle, GesHandleState> states, GesHandle handle, GesHandleState resolved)
    {
        states[handle] = resolved;
        if (resolved.NotVisible) _handleContext?.RecordHandleNotVisible(handle.ToString());
        // A read without a version picks up the resolved one, so a later read of that version sees the same state.
        if (!handle.HasVersion && !resolved.Version.IsNothing) states.TryAdd(handle.WithView(resolved.Version, handle.Projection), resolved);
        if (resolved.Full.IsNothing) return;
        var full = new GesHandleState(resolved.Full, resolved.FullVersion);
        states.TryAdd(handle.WithView(default, ""), full);
        if (!resolved.FullVersion.IsNothing) states.TryAdd(handle.WithView(resolved.FullVersion, ""), full);
    }

    // Resolves the read handle together with the other uncached handles of its type in a List of the message, with the
    // read's projection and version rule (specs/HostRuntime.md#handles). False when no such List has another handle.
    private bool ResolveBatch(GesHandleBinding binding, GesHandle handle, GesSchema? projection, Dictionary<GesHandle, GesHandleState> states)
    {
        if (!_handleListsCollected) CollectHandleLists();
        if (_handleLists is not { Count: > 0 } lists) return false;
        for (var listIndex = 0; listIndex < lists.Count; listIndex++)
        {
            var list = lists[listIndex];
            var found = false;
            for (var index = 0; index < list.Length && !found; index++)
                found = list[index].ObjectValue is GesHandle element && element.Equals(handle) &&
                        (!handle.HasVersion || GesHandleStates.VersionEquals(element.Version, handle.Version));
            if (!found) continue;
            var batch = _handleResolveBatch ??= new GesHandleResolveBatch();
            var seen = _handleBatchSeen ??= new HashSet<GesHandle>(HandleStateComparer.Instance);
            try
            {
                batch.Add(handle, binding.Type, projection, HandleParty);
                seen.Add(handle);
                for (var index = 0; index < list.Length; index++)
                {
                    if (list[index].ObjectValue is not GesHandle element || !string.Equals(element.TypeName, handle.TypeName, StringComparison.Ordinal) ||
                        element.IsComposite != handle.IsComposite)
                    {
                        continue;
                    }

                    var other = element.WithView(handle.HasVersion ? element.Version : default, handle.Projection);
                    if (states.ContainsKey(other) || !seen.Add(other)) continue;
                    batch.Add(other, binding.Type, projection, HandleParty);
                }

                if (batch.Count == 1) return false;
                GesHandleStates.ResolveMany(binding, batch);
                for (var index = 0; index < batch.Count; index++) Cache(states, batch[index].Handle, GesHandleStates.Check(binding.Type, batch[index]));
                return true;
            }
            finally
            {
                batch.End();
                seen.Clear();
            }
        }

        return false;
    }

    // Collects the Lists of the message's arguments, at any depth, that hold a handle.
    private void CollectHandleLists()
    {
        _handleListsCollected = true;
        if (ProcessingMessage is not { } message) return;
        var arguments = message.Arguments;
        Stack<GesValue>? pending = null;
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments.ValueAt(index);
            if (!argument.MayHoldHandle) continue;
            (pending ??= new Stack<GesValue>()).Push(argument);
        }

        if (pending is null) return;
        while (pending.TryPop(out var value))
        {
            switch (value.ValueKind)
            {
                case GesBytecodeTypeKind.List:
                {
                    var items = value.AsList();
                    var holds = false;
                    for (var index = 0; index < items.Length; index++)
                    {
                        if (items[index].ObjectValue is GesHandle) holds = true;
                        else if (items[index].MayHoldHandle) pending.Push(items[index]);
                    }

                    if (holds) (_handleLists ??= new List<GesValueSlice>()).Add(items);
                    break;
                }
                case GesBytecodeTypeKind.Message when value.ObjectValue is GameEventScriptMessage inner:
                    for (var index = 0; index < inner.Arguments.Count; index++)
                        if (inner.Arguments.ValueAt(index).MayHoldHandle) pending.Push(inner.Arguments.ValueAt(index));
                    break;
                case GesBytecodeTypeKind.Map or GesBytecodeTypeKind.Custom when value.AsMap() is { } map:
                    for (var index = 0; index < map.Length; index++)
                        if (map.ValueAt(index).MayHoldHandle) pending.Push(map.ValueAt(index));
                    break;
            }
        }
    }
}
