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

// A resolved handle state: the normalized state Record, or Nothing for a missing object, and the object's version.
internal readonly struct GesHandleState(GesValue state, GesValue version)
{
    internal GesValue State { get; } = state;
    internal GesValue Version { get; } = version;
}

// Resolves handle states for the VM and refs for the host (specs/HostRuntime.md#handles).
internal static class GesHandleStates
{
    // Asks the type's resolver for the state, checks it against the state schema with defaults applied and checks the
    // versions. Failures throw a runtime fault that ends the handler.
    internal static GesHandleState Resolve(GesHandleBinding? binding, GesHandleResolveCall call, GesHandle handle)
    {
        var symbol = handle.ToString();
        if (binding is null) throw Fault(GesDiagnosticCodes.RuntimeHandleResolveFailed, $"No resolver reads the state of @{handle.TypeName}.", symbol);
        GesHandleResolveCall.AnswerKind answer;
        GesValue record, passed;
        string? text;
        call.Begin(handle, binding.Type);
        try
        {
            binding.Resolver.Resolve(call);
            answer = call.Answer;
            record = call.Record;
            text = call.Text;
            passed = call.Version;
        }
        catch (GesFatalRuntimeException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Fault(GesDiagnosticCodes.RuntimeHandleResolveFailed, $"The resolver of @{handle.TypeName} failed.", symbol, exception);
        }
        finally
        {
            call.End();
        }

        if (answer is GesHandleResolveCall.AnswerKind.None or GesHandleResolveCall.AnswerKind.Several)
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The resolver of @{handle.TypeName} must give exactly one answer.", symbol);
        var type = binding.Type;
        if (!passed.IsNothing && !IsVersionOf(type.Version, passed))
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The resolver of @{handle.TypeName} passed no valid version of its type.", symbol);
        if (answer == GesHandleResolveCall.AnswerKind.Missing) return new GesHandleState(default, Resolved(type, handle, passed, symbol));
        var state = CheckedState(type, answer == GesHandleResolveCall.AnswerKind.Document ? text! : Print(type, record, symbol), symbol);
        if (type.Version == GesHandleVersionKind.ContentHash)
        {
            Span<byte> bytes = stackalloc byte[GesHash32.Length];
            GesSchemaValues.ContentHash(type, state).CopyTo(bytes);
            var computed = GesValue.GesBytes(bytes);
            if (!passed.IsNothing && !VersionEquals(passed, computed) || handle.HasVersion && !VersionEquals(handle.Version, computed))
                throw Fault(GesDiagnosticCodes.RuntimeHandleVersionMismatch, $"The content hash of {symbol} differs.", symbol);
            return new GesHandleState(state, computed);
        }

        return new GesHandleState(state, Resolved(type, handle, passed, symbol));
    }

    // The ref of a handle turned into its version by the type's resolver.
    internal static GesHandle ResolveRef(GesHandleBinding? binding, GesHandle handle)
    {
        if (handle.Ref.IsNothing) return handle;
        var symbol = handle.ToString();
        if (binding is null) throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleResolveFailed, symbol, $"No resolver turns refs of @{handle.TypeName} into versions.");
        GesValue version;
        try
        {
            version = binding.Resolver.ResolveRef(handle);
        }
        catch (Exception exception)
        {
            throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleResolveFailed, symbol, $"The resolver of @{handle.TypeName} failed.", exception);
        }

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

    // The version of a revision or hash type: the passed one, which must match the carried one, else the carried one.
    private static GesValue Resolved(GesHandleType type, GesHandle handle, GesValue passed, string symbol)
    {
        if (type.Version == GesHandleVersionKind.None) return default;
        if (passed.IsNothing) return handle.Version;
        if (handle.HasVersion && !VersionEquals(passed, handle.Version))
            throw Fault(GesDiagnosticCodes.RuntimeHandleVersionMismatch, $"The resolver of @{handle.TypeName} delivered another version of {symbol}.", symbol);
        return passed;
    }

    // A Record answer as document text, so it is checked exactly like a document answer.
    private static string Print(GesHandleType type, GesValue record, string symbol)
    {
        if (record.ObjectValue is not GesCustomObject || !string.Equals(record.CustomTypeName, type.StateName, StringComparison.Ordinal))
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state of {symbol} is no :{type.StateName} Record.", symbol);
        try
        {
            return new GesDocument(record).Print();
        }
        catch (ArgumentException exception)
        {
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state of {symbol} is no valid :{type.StateName} Record.", symbol, exception);
        }
    }

    // The document read and checked against the state schema, as a Record with defaults applied.
    private static GesValue CheckedState(GesHandleType type, string text, string symbol)
    {
        var declaration = type.Resolved.State;
        try
        {
            var value = GesDocumentReading.Read(text, out _);
            var syntax = GesDocumentReader.Read(text);
            var named = syntax.Schema == GesDocumentSchemaSyntax.Type ? syntax.SchemaName : null;
            if (!string.Equals(named, type.StateName, StringComparison.Ordinal))
                throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state document of {symbol} does not name :{type.StateName}.", symbol);
            if (GesSchemaDocumentCheck.Failing(text, syntax.Root!, declaration, null, out var chosen) is { } failure)
                throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state of {symbol} does not fit :{type.StateName}.", symbol, failure);
            return GesSchemaValues.NormalizeState(declaration, GesSchemaValues.DocumentShapes(declaration, value, chosen), "", type.Declarations);
        }
        catch (GesDocumentException exception)
        {
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state document of {symbol} does not read.", symbol, exception);
        }
        catch (GesWireException exception)
        {
            throw Fault(GesDiagnosticCodes.RuntimeInvalidHandleState, $"The state of {symbol} does not fit :{type.StateName}.", symbol, exception);
        }
    }

    private static GesVmException Fault(string code, string message, string symbol, Exception? inner = null)
        => new(new GesDiagnostic(GesDiagnosticPhase.Runtime, code, message, Symbol: symbol,
            TechnicalDetails: inner is null ? null : GesRuntimeExceptionText.Describe(inner)), inner);

    // Values that hold a handle with a ref, which never enter the VM.
    internal static bool HoldsRef(GesValue value) => HoldsRef(value, 0);

    private static bool HoldsRef(GesValue value, int depth)
    {
        switch (value.ValueKind)
        {
            case GesBytecodeTypeKind.Handle:
                return value.Handle is { } handle && !handle.Ref.IsNothing;
            case GesBytecodeTypeKind.List when depth < 256:
            {
                var items = value.AsList();
                for (var index = 0; index < items.Length; index++)
                    if (HoldsRef(items[index], depth + 1)) return true;
                return false;
            }
            case GesBytecodeTypeKind.Message when depth < 256 && value.ObjectValue is GameEventScriptMessage message:
            {
                var arguments = message.Arguments;
                for (var index = 0; index < arguments.Count; index++)
                    if (HoldsRef(arguments.ValueAt(index), depth + 1)) return true;
                return false;
            }
            case GesBytecodeTypeKind.Map or GesBytecodeTypeKind.Custom when depth < 256 && value.AsMap() is { } map:
                for (var index = 0; index < map.Length; index++)
                    if (map.HandleKey(index) is { Ref.IsNothing: false } || HoldsRef(map.ValueAt(index), depth + 1)) return true;
                return false;
            default:
                return false;
        }
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
