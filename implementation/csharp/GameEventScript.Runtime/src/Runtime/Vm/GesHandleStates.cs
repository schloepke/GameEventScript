// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;

namespace GameEventScript.Runtime.Vm;

// One handle type a host registered with its resolver.
internal sealed class GesHandleBinding(GesHandleType type, IGesHandleResolver resolver)
{
    internal GesHandleType Type { get; } = type;
    internal IGesHandleResolver Resolver { get; } = resolver;
}

// A resolved handle state: the normalized state Record of the read view, or Nothing for a missing or not visible object,
// and the view's version. A projected read answered with the full state also keeps that state and its version, so the
// cache serves later reads of other views.
internal readonly struct GesHandleState(GesValue state, GesValue version, bool notVisible = false, GesValue full = default, GesValue fullVersion = default)
{
    internal GesValue State { get; } = state;
    internal GesValue Version { get; } = version;
    internal bool NotVisible { get; } = notVisible;
    internal GesValue Full { get; } = full;
    internal GesValue FullVersion { get; } = fullVersion;
}

// Resolves handle states for the VM and refs for the host (specs/HostRuntime.md#handles).
internal static class GesHandleStates
{
    // Asks the type's resolver for the state of the handle's view, checks it against the schema it names and the view,
    // with defaults applied, and checks the versions. Failures throw a runtime fault that ends the handler.
    internal static GesHandleState Resolve(GesHandleBinding binding, GesHandleResolveCall call, GesHandle handle, GesSchema? projection, int party)
    {
        call.Begin(handle, binding.Type, projection, party);
        try
        {
            try
            {
                binding.Resolver.Resolve(call);
            }
            catch (GesFatalRuntimeException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Fault(GesDiagnosticCodes.RuntimeHandleResolveFailed, $"The resolver of @{handle.TypeName} failed.", handle.ToString(), exception);
            }

            return Check(binding.Type, call);
        }
        finally
        {
            call.End();
        }
    }

    // Asks the resolver for all calls of the batch at once; the caller checks each call and ends the batch.
    internal static void ResolveMany(GesHandleBinding binding, GesHandleResolveBatch batch)
    {
        try
        {
            binding.Resolver.ResolveMany(batch);
        }
        catch (GesFatalRuntimeException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var first = batch[0].Handle;
            throw Fault(GesDiagnosticCodes.RuntimeHandleResolveFailed, $"The resolver of @{first.TypeName} failed.", first.ToString(), exception);
        }
    }

    // Checks the answer of one call while it is still valid.
    internal static GesHandleState Check(GesHandleType type, GesHandleResolveCall call)
    {
        var handle = call.Handle;
        var symbol = handle.ToString();
        var answer = call.Answer;
        var passed = call.Version;
        if (answer is GesHandleResolveCall.AnswerKind.None or GesHandleResolveCall.AnswerKind.Several)
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The resolver of @{handle.TypeName} must give exactly one answer.", symbol);
        if (!passed.IsNothing && !IsVersionOf(type.Version, passed))
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The resolver of @{handle.TypeName} passed no valid version of its type.", symbol);
        if (answer is GesHandleResolveCall.AnswerKind.Missing or GesHandleResolveCall.AnswerKind.NotVisible)
            return new GesHandleState(default, Resolved(type, handle, passed, symbol, false), answer == GesHandleResolveCall.AnswerKind.NotVisible);

        var projection = call.Projection;
        var text = answer == GesHandleResolveCall.AnswerKind.Document ? call.Text! : Print(type, projection, call.Record, symbol);
        var delivered = CheckedState(type, projection, text, symbol, out var state);
        var view = projection?.Declaration ?? type.Resolved.State;
        var projected = ReferenceEquals(delivered, view) ? state : Project(view, state, symbol);
        var full = ReferenceEquals(delivered, type.Resolved.State) && projection is not null ? state : default;
        if (type.Version == GesHandleVersionKind.ContentHash)
        {
            var computed = Hash(type, delivered, state);
            if (!passed.IsNothing && !VersionEquals(passed, computed))
                throw Fault(GesDiagnosticCodes.RuntimeHandleVersionMismatch, $"The content hash of {symbol} differs.", symbol);
            var viewVersion = ReferenceEquals(delivered, view) ? computed : Hash(type, view, projected);
            if (handle.HasVersion && !VersionEquals(handle.Version, viewVersion))
                throw Fault(GesDiagnosticCodes.RuntimeHandleVersionMismatch, $"The content hash of {symbol} differs.", symbol);
            return new GesHandleState(projected, viewVersion, false, full, computed);
        }

        var version = Resolved(type, handle, passed, symbol, true);
        return new GesHandleState(projected, version, false, full, version);
    }

    // The state of a narrower view derived from a cached wider state, with that view's version. A content hash the
    // handle carries must be the view's.
    internal static GesHandleState Narrow(GesHandleType type, GesHandle handle, GesSchema projection, GesHandleState wider)
    {
        if (wider.State.IsNothing) return new GesHandleState(default, wider.Version, wider.NotVisible);
        var symbol = handle.ToString();
        var projected = Project(projection.Declaration, wider.State, symbol);
        if (type.Version != GesHandleVersionKind.ContentHash) return new GesHandleState(projected, wider.Version);
        var version = Hash(type, projection.Declaration, projected);
        if (handle.HasVersion && !VersionEquals(handle.Version, version))
            throw Fault(GesDiagnosticCodes.RuntimeHandleVersionMismatch, $"The content hash of {symbol} differs.", symbol);
        return new GesHandleState(projected, version);
    }

    private static GesValue Hash(GesHandleType type, GesResolvedDeclaration declaration, GesValue state)
    {
        Span<byte> bytes = stackalloc byte[GesHash32.Length];
        GesSchemaValues.ProjectedHash(type, declaration, state).CopyTo(bytes);
        return GesValue.GesBytes(bytes);
    }

    private static GesValue Project(GesResolvedDeclaration view, GesValue state, string symbol)
    {
        try
        {
            return GesSchemaValues.Project(view, state);
        }
        catch (GesWireException exception)
        {
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state of {symbol} does not fit :{view.Name}.", symbol, exception);
        }
    }

    // The schema an answer may name: the state schema, the read projection, or a projection between them.
    private static GesResolvedDeclaration? AnswerDeclaration(GesHandleType type, GesSchema? projection, string? name)
    {
        if (name is null) return null;
        if (string.Equals(name, type.StateName, StringComparison.Ordinal)) return type.Resolved.State;
        if (projection is null) return null;
        if (string.Equals(name, projection.Name, StringComparison.Ordinal)) return projection.Declaration;
        return type.FindProjection(name) is { } between && projection.Declaration.Narrows(between.Declaration) ? between.Declaration : null;
    }

    private static string Expected(GesHandleType type, GesSchema? projection)
        => projection is null ? $":{type.StateName}" : $":{type.StateName} or :{projection.Name}";

    // The ref of a handle turned into its version by the type's resolver, or null when the resolver knows no such ref.
    internal static GesHandle? ResolveRef(GesHandleBinding? binding, GesHandle handle)
    {
        if (handle.Ref.IsNothing) return handle;
        var symbol = handle.ToString();
        if (binding is null) throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleResolveFailed, symbol, $"No resolver turns refs of @{handle.TypeName} into versions.");
        GesValue version;
        try
        {
            version = binding.Resolver.ResolveRef(handle);
        }
        catch (GesFatalRuntimeException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleResolveFailed, symbol, $"The resolver of @{handle.TypeName} failed.", exception);
        }

        if (version.IsNothing) return null;
        if (!IsVersionOf(binding.Type.Version, version))
            throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleResolveFailed, symbol, $"The resolver of @{handle.TypeName} returned no valid version.");
        return handle.WithVersion(version);
    }

    // Whether the value is a version of the kind; a type without versions has none.
    internal static bool IsVersionOf(GesHandleVersionKind kind, GesValue version) => kind switch
    {
        GesHandleVersionKind.Revision => version.ValueKind == GesBytecodeTypeKind.Integer && version.Unit == GesQuantityUnit.UnitNone,
        GesHandleVersionKind.Sha1 => version.ValueKind == GesBytecodeTypeKind.Bytes && version.AsBytes().Length == GesHash20.Length,
        GesHandleVersionKind.Sha256 or GesHandleVersionKind.ContentHash => version.ValueKind == GesBytecodeTypeKind.Bytes && version.AsBytes().Length == GesHash32.Length,
        _ => false,
    };

    internal static bool VersionEquals(GesValue left, GesValue right)
    {
        if (left.ValueKind != right.ValueKind) return false;
        return left.ValueKind switch
        {
            GesBytecodeTypeKind.Integer => left.AsInteger() == right.AsInteger(),
            GesBytecodeTypeKind.Bytes => left.AsBytes().SequenceEqual(right.AsBytes()),
            _ => left.IsNothing && right.IsNothing,
        };
    }

    // The version of a revision or hash type: the passed one, which must match the carried one, else the carried one. A
    // state needs one of them; a missing object may have none.
    private static GesValue Resolved(GesHandleType type, GesHandle handle, GesValue passed, string symbol, bool required)
    {
        if (type.Version == GesHandleVersionKind.None) return default;
        if (passed.IsNothing)
        {
            if (required && !handle.HasVersion)
                throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The resolver of @{handle.TypeName} passed no version for {symbol}, which carries none.", symbol);
            return handle.Version;
        }

        if (handle.HasVersion && !VersionEquals(passed, handle.Version))
            throw Fault(GesDiagnosticCodes.RuntimeHandleVersionMismatch, $"The resolver of @{handle.TypeName} delivered another version of {symbol}.", symbol);
        return passed;
    }

    // A Record answer as document text, so it is checked exactly like a document answer.
    private static string Print(GesHandleType type, GesSchema? projection, GesValue record, string symbol)
    {
        if (record.ObjectValue is not GesCustomObject || AnswerDeclaration(type, projection, record.CustomTypeName) is null)
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state of {symbol} is no {Expected(type, projection)} Record.", symbol);
        try
        {
            return new GesDocument(record).Print();
        }
        catch (ArgumentException exception)
        {
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state of {symbol} is no valid :{record.CustomTypeName} Record.", symbol, exception);
        }
    }

    // The document read and checked against the schema it names, as a Record with defaults applied; returns that schema.
    private static GesResolvedDeclaration CheckedState(GesHandleType type, GesSchema? projection, string text, string symbol, out GesValue state)
    {
        try
        {
            var syntax = GesDocumentReader.Read(text);
            var value = GesDocumentReading.Read(text, syntax, out _);
            var named = syntax.Schema == GesDocumentSchemaSyntax.Type ? syntax.SchemaName : null;
            if (AnswerDeclaration(type, projection, named) is not { } declaration)
                throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state document of {symbol} does not name {Expected(type, projection)}.", symbol);
            if (GesSchemaDocumentCheck.Failing(text, syntax.Root!, declaration, null, out var chosen) is { } failure)
                throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state of {symbol} does not fit :{declaration.Name}.", symbol, failure);
            state = GesSchemaValues.NormalizeState(declaration, GesSchemaValues.DocumentShapes(declaration, value, chosen), "", type.Declarations);
            return declaration;
        }
        catch (GesDocumentException exception)
        {
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state document of {symbol} does not read.", symbol, exception);
        }
        catch (GesWireException exception)
        {
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state of {symbol} does not fit {Expected(type, projection)}.", symbol, exception);
        }
    }

    private static GesVmException Fault(string code, string message, string symbol, Exception? inner = null)
        => new(new GesDiagnostic(GesDiagnosticPhase.Runtime, code, message, Symbol: symbol,
            TechnicalDetails: inner is null ? null : GesRuntimeExceptionText.Describe(inner)), inner);

    // Values that hold a handle with a ref, which never enter the VM. The walk enters only containers that hold a handle;
    // beyond RecursionDepth levels it continues on an explicit stack, so no nesting escapes the check.
    internal static bool HoldsRef(GesValue value) => value.MayHoldHandle && HoldsRef(value, 0, null);

    private const int RecursionDepth = 256;

    // Checks the value and its children at once, or pushes the children onto `deep` when it is given.
    private static bool HoldsRef(GesValue value, int depth, Stack<GesValue>? deep)
    {
        switch (value.ValueKind)
        {
            case GesBytecodeTypeKind.Handle:
                return value.Handle is { } handle && !handle.Ref.IsNothing;
            case GesBytecodeTypeKind.List:
            {
                var items = value.AsList();
                for (var index = 0; index < items.Length; index++)
                    if (ChildHoldsRef(items[index], depth, deep)) return true;
                return false;
            }
            case GesBytecodeTypeKind.Message when value.ObjectValue is GameEventScriptMessage message:
            {
                var arguments = message.Arguments;
                for (var index = 0; index < arguments.Count; index++)
                    if (ChildHoldsRef(arguments.ValueAt(index), depth, deep)) return true;
                return false;
            }
            case GesBytecodeTypeKind.Map or GesBytecodeTypeKind.Custom when value.AsMap() is { } map:
                for (var index = 0; index < map.Length; index++)
                    if (map.HandleKey(index) is { Ref.IsNothing: false } || ChildHoldsRef(map.ValueAt(index), depth, deep)) return true;
                return false;
            default:
                return false;
        }
    }

    private static bool ChildHoldsRef(GesValue child, int depth, Stack<GesValue>? deep)
    {
        if (!child.MayHoldHandle) return false;
        if (deep is not null)
        {
            deep.Push(child);
            return false;
        }

        return depth + 1 < RecursionDepth ? HoldsRef(child, depth + 1, null) : DeepHoldsRef(child);
    }

    private static bool DeepHoldsRef(GesValue value)
    {
        var pending = new Stack<GesValue>();
        pending.Push(value);
        while (pending.TryPop(out var current))
            if (HoldsRef(current, 0, pending)) return true;
        return false;
    }

    // The first message argument that holds a ref, or -1.
    internal static int RefArgument(GameEventScriptMessage message)
    {
        var arguments = message.Arguments;
        for (var index = 0; index < arguments.Count; index++)
            if (HoldsRef(arguments.ValueAt(index))) return index;
        return -1;
    }

    internal static GesWireException UnresolvedRef(string path) => new(GesWireException.UnresolvedRef, path);

    // The runtime fault of a value with a ref that a callback hands to a script.
    internal static GesVmException UnresolvedRefFault(string symbol)
        => new(new GesDiagnostic(GesDiagnosticPhase.Runtime, GesWireException.UnresolvedRef, $"{symbol} returned a handle with an unresolved ref.", Symbol: symbol));
}
