// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>
/// Resolves the host's extension functions for the extension imports of a Program while <c>Host.Load</c> links
/// it; it is not consulted per invocation.
/// </summary>
public interface IGesExtensionRegistry
{
    /// <summary>
    /// Returns the function bound to an exact extension reference. Equal references must resolve consistently
    /// during one load, independent of culture.
    /// </summary>
    /// <param name="reference">The normalized extension name, function name and argument labels of the import.</param>
    /// <returns>The function, or <see langword="null"/> when none is registered; absence fails the link.</returns>
    IGesExtensionFunction? Resolve(GesExtensionReference reference);
}

/// <summary>
/// Synchronous host callback that implements one extension function.
/// </summary>
public interface IGesExtensionFunction
{
    /// <summary>
    /// Reads the borrowed arguments and sets the result on the call. Setting no result yields Nothing. The call,
    /// its arguments and context must not be retained after returning. The runtime restores the random boundary
    /// afterwards and classifies thrown exceptions, such as <see cref="GesExtensionFaultException"/>.
    /// </summary>
    /// <param name="call">The reusable call holding the arguments and receiving the result.</param>
    void Invoke(GesExtensionCall call);
}

internal sealed class GesEmptyExtensionRegistry : IGesExtensionRegistry
{
    public static readonly GesEmptyExtensionRegistry Instance = new();

    private GesEmptyExtensionRegistry()
    {
    }

    public IGesExtensionFunction? Resolve(GesExtensionReference reference) => null;
}

/// <summary>
/// Represents an immutable normalized extension name, function name, and ordered argument signature.
/// </summary>
public sealed class GesExtensionReference
{
    /// <summary>
    /// Creates a reference, normalizing the names and copying the argument labels in their given order.
    /// </summary>
    /// <param name="extensionName">The lower-case extension namespace, such as <c>math</c>; <see langword="null"/> becomes the empty string.</param>
    /// <param name="functionName">The lower-case function name, such as <c>distance</c>; <see langword="null"/> becomes the empty string.</param>
    /// <param name="argumentLabels">
    /// The argument labels in call order; a <see langword="null"/> or blank label becomes <c>_</c>, a positional
    /// argument. <see langword="null"/> means no arguments.
    /// </param>
    /// <exception cref="ArgumentException">A name or label violates the lower-case ASCII identifier grammar.</exception>
    public GesExtensionReference(string? extensionName, string? functionName, IEnumerable<string?>? argumentLabels)
    {
        ExtensionName = NormalizeName(extensionName);
        FunctionName = NormalizeName(functionName);
        ArgumentLabels = GesReadOnlyArray<string>.FromOwnedArray(NormalizeArgumentLabels(argumentLabels));
        SignatureId = CreateSignatureId(ExtensionName, FunctionName, ArgumentLabels);
    }

    /// <summary>
    /// Gets the normalized extension namespace.
    /// </summary>
    public string ExtensionName { get; }

    /// <summary>
    /// Gets the normalized function name.
    /// </summary>
    public string FunctionName { get; }

    /// <summary>
    /// Gets the immutable ordered argument labels copied during construction.
    /// </summary>
    public IReadOnlyList<string> ArgumentLabels { get; }

    /// <summary>
    /// Gets the canonical identity <c>extension.function(label,...)</c>, with the labels in call order.
    /// </summary>
    public string SignatureId { get; }

    private static string NormalizeName(string? name)
        => name is null ? string.Empty : GesExternalTypeNames.NormalizeIdentifier(name, nameof(name));

    private static string[] NormalizeArgumentLabels(IEnumerable<string?>? argumentLabels)
    {
        if (argumentLabels is null)
        {
            return [];
        }

        if (argumentLabels is IReadOnlyCollection<string?> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var values = new string[collection.Count];
            var index = 0;
            foreach (var label in collection)
            {
                values[index++] = GesMessageSignature.NormalizeParameterName(label);
            }

            return values;
        }

        var list = new List<string>();
        foreach (var label in argumentLabels)
        {
            list.Add(GesMessageSignature.NormalizeParameterName(label));
        }

        if (list.Count == 0)
        {
            return [];
        }

        var result = new string[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            result[index] = list[index];
        }

        return result;
    }

    private static string CreateSignatureId(string extensionName, string functionName, IReadOnlyList<string> argumentLabels)
    {
        var builder = new StringBuilder();
        builder.Append(extensionName).Append('.').Append(functionName).Append('(');
        for (var index = 0; index < argumentLabels.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(argumentLabels[index]);
        }

        return builder.Append(')').ToString();
    }
}

/// <summary>
/// Reusable call-scoped adapter passed to <see cref="IGesExtensionFunction.Invoke"/>: it lends the arguments, the
/// host context and its random generator, and receives one result. The last setter call before the callback
/// returns decides the result; without one the result is Nothing. The call must not be retained.
/// </summary>
public sealed class GesExtensionCall
{
    private GesContext? _context;
    private GesValueArguments _arguments = GesValueArguments.Empty;
    private Runtime.Vm.GesVirtualMachine? _vm;
    private ushort _destinationRegister;
    private GesValue _result;
    private bool _hasResult;

    /// <summary>
    /// Creates an inactive call without context; the runtime supplies context and arguments before each invocation.
    /// </summary>
    public GesExtensionCall()
    {
    }

    /// <summary>
    /// Creates a call bound to a context and without arguments, for invoking a function outside a host.
    /// </summary>
    /// <param name="context">The context the function reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public GesExtensionCall(GesContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets the context of the host running the call.
    /// </summary>
    /// <exception cref="InvalidOperationException">The call has no context.</exception>
    public GesContext Context => _context ?? throw new InvalidOperationException("Extension context is not initialized.");

    /// <summary>
    /// Gets the private random generator of the host running the call.
    /// </summary>
    /// <exception cref="InvalidOperationException">The call has no context.</exception>
    public GesRandomGenerator Random => Context.Random;

    /// <summary>
    /// Gets the runtime limits of the host running the call.
    /// </summary>
    /// <exception cref="InvalidOperationException">The call has no context.</exception>
    public GesRuntimeLimits RuntimeLimits => Context.RuntimeLimits;

    /// <summary>
    /// Gets the borrowed arguments in call order, valid only during the invocation.
    /// </summary>
    public GesValueArguments Arguments => _arguments;

    /// <summary>
    /// Gets the result set so far, or Nothing when no result was set.
    /// </summary>
    public GesValue Result => _hasResult ? _result : GesValue.GesNothing();

    internal bool HasResult => _hasResult;

    internal void BeginCall(Runtime.Vm.GesVirtualMachine vm, ushort destinationRegister, GesContext context, GesValueArguments arguments)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _destinationRegister = destinationRegister;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _arguments = arguments;
        _result = default;
        _hasResult = false;
    }

    internal void EndCall()
    {
        _vm?.EndExtensionCall();
        _vm = null;
        _destinationRegister = 0;
        _arguments = GesValueArguments.Empty;
        _result = default;
        _hasResult = false;
    }

    /// <summary>
    /// Sets the result to Nothing.
    /// </summary>
    public void SetNothing() => SetValue(GesValue.GesNothing());

    /// <summary>
    /// Sets the result to the supplied value.
    /// </summary>
    /// <param name="value">The result value.</param>
    public void SetValue(GesValue value)
    {
        _hasResult = true;
        _result = value;
    }

    /// <summary>
    /// Sets a Boolean result.
    /// </summary>
    /// <param name="value">The result value.</param>
    public void SetBoolean(bool value)
    {
        _hasResult = true;
        _result = GesValue.GesBoolean(value);
    }

    /// <summary>
    /// Sets an exact Int64 Number result, a Quantity when a unit is given.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="unit">The unit; <see cref="GesQuantityUnit.UnitNone"/> for a plain Number.</param>
    public void SetInteger(long value, GesQuantityUnit unit = GesQuantityUnit.UnitNone)
    {
        _hasResult = true;
        _result = GesValue.GesInteger(value, unit);
    }

    /// <summary>
    /// Sets a Number result through the canonical numeric factory: NaN becomes Nothing, and an integral value
    /// that fits Int64 is stored as an integer.
    /// </summary>
    /// <param name="value">The binary64 value.</param>
    /// <param name="unit">The unit; <see cref="GesQuantityUnit.UnitNone"/> for a plain Number.</param>
    public void SetFloat(double value, GesQuantityUnit unit = GesQuantityUnit.UnitNone)
    {
        _hasResult = true;
        _result = GesValue.GesFloat(value, unit);
    }

    /// <summary>
    /// Sets a Percentage result from a ratio.
    /// </summary>
    /// <param name="ratio">The ratio, where 1 represents 100%.</param>
    public void SetPercentage(double ratio)
    {
        _hasResult = true;
        _result = GesValue.GesPercentage(ratio);
    }

    /// <summary>
    /// Sets a verbatim Text result.
    /// </summary>
    /// <param name="text">The text; <see langword="null"/> becomes the empty Text.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> is not valid Unicode.</exception>
    public void SetText(string text)
    {
        _hasResult = true;
        _result = GesValue.GesText(text);
    }

    /// <summary>
    /// Sets a Tag result from a bare name without <c>#</c>.
    /// </summary>
    /// <param name="tag">The tag name in the portable ASCII name grammar.</param>
    /// <exception cref="ArgumentException"><paramref name="tag"/> is not a valid tag name.</exception>
    public void SetTag(string tag)
    {
        _hasResult = true;
        _result = GesValue.GesTag(tag);
    }
}
