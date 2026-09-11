// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>
/// Defines the contract for i game event script extension registry.
/// </summary>
public interface IGameEventScriptExtensionRegistry
{
    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="reference">The reference value.</param>
    /// <returns>The result of the operation.</returns>
    IGameEventScriptExtensionFunction? Resolve(GameEventScriptExtensionReference reference);
}

/// <summary>
/// Defines the contract for i game event script extension function.
/// </summary>
public interface IGameEventScriptExtensionFunction
{
    /// <summary>
    /// Performs the invoke operation.
    /// </summary>
    /// <param name="call">The call value.</param>
    void Invoke(GesExtensionCall call);
}

internal sealed class GameEventScriptEmptyExtensionRegistry : IGameEventScriptExtensionRegistry
{
    public static readonly GameEventScriptEmptyExtensionRegistry Instance = new();

    private GameEventScriptEmptyExtensionRegistry()
    {
    }

    public IGameEventScriptExtensionFunction? Resolve(GameEventScriptExtensionReference reference) => null;
}

/// <summary>
/// Represents an immutable normalized extension name, function name, and ordered argument signature.
/// </summary>
public sealed class GameEventScriptExtensionReference
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Extension Reference.
    /// </summary>
    /// <param name="extensionName">The extension name value.</param>
    /// <param name="functionName">The function name value.</param>
    /// <param name="argumentLabels">The argument labels value.</param>
    public GameEventScriptExtensionReference(string? extensionName, string? functionName, IEnumerable<string?>? argumentLabels)
    {
        ExtensionName = NormalizeName(extensionName);
        FunctionName = NormalizeName(functionName);
        ArgumentLabels = GameEventScriptReadOnlyArray<string>.FromOwnedArray(NormalizeArgumentLabels(argumentLabels));
        SignatureId = CreateSignatureId(ExtensionName, FunctionName, ArgumentLabels);
    }

    /// <summary>
    /// Gets the extension name.
    /// </summary>
    public string ExtensionName { get; }

    /// <summary>
    /// Gets the function name.
    /// </summary>
    public string FunctionName { get; }

    /// <summary>
    /// Gets the immutable ordered argument labels copied during construction.
    /// </summary>
    public IReadOnlyList<string> ArgumentLabels { get; }

    /// <summary>
    /// Gets the signature id.
    /// </summary>
    public string SignatureId { get; }

    private static string NormalizeName(string? name)
        => name is null ? string.Empty : GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));

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
                values[index++] = GameEventScriptMessageSignature.NormalizeParameterName(label);
            }

            return values;
        }

        var list = new List<string>();
        foreach (var label in argumentLabels)
        {
            list.Add(GameEventScriptMessageSignature.NormalizeParameterName(label));
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
/// Represents a ges extension call.
/// </summary>
public sealed class GesExtensionCall
{
    private GameEventScriptContext? _context;
    private GesValueArguments _arguments = GesValueArguments.Empty;
    private Runtime.VM.GesVmState? _vmState;
    private ushort _destinationRegister;
    private GesValue _result;
    private bool _hasResult;

    /// <summary>
    /// Initializes a new instance of Ges Extension Call.
    /// </summary>
    public GesExtensionCall()
    {
    }

    /// <summary>
    /// Initializes a new instance of Ges Extension Call.
    /// </summary>
    /// <param name="context">The context value.</param>
    public GesExtensionCall(GameEventScriptContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets the context.
    /// </summary>
    public GameEventScriptContext Context => _context ?? throw new InvalidOperationException("Extension context is not initialized.");

    /// <summary>
    /// Gets the random.
    /// </summary>
    public GameEventScriptRandomGenerator Random => Context.Random;

    /// <summary>
    /// Gets the runtime limits.
    /// </summary>
    public GameEventScriptRuntimeLimits RuntimeLimits => Context.RuntimeLimits;

    /// <summary>
    /// Gets the arguments.
    /// </summary>
    public GesValueArguments Arguments => _arguments;

    /// <summary>
    /// Gets the result.
    /// </summary>
    public GesValue Result => _hasResult ? _result : GesValue.GesNothing();

    internal bool HasResult => _hasResult;

    internal void BeginCall(Runtime.VM.GesVmState vmState, ushort destinationRegister, GameEventScriptContext context, GesValueArguments arguments)
    {
        _vmState = vmState ?? throw new ArgumentNullException(nameof(vmState));
        _destinationRegister = destinationRegister;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _arguments = arguments;
        _result = default;
        _hasResult = false;
    }

    internal void EndCall()
    {
        _vmState = null;
        _destinationRegister = 0;
        _arguments = GesValueArguments.Empty;
        _result = default;
        _hasResult = false;
    }

    /// <summary>
    /// Sets the nothing.
    /// </summary>
    public void SetNothing() => SetValue(GesValue.GesNothing());

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="value">The value value.</param>
    public void SetValue(GesValue value)
    {
        _hasResult = true;
        _result = value;
        _vmState?.SetValue(_destinationRegister, in value);
    }

    /// <summary>
    /// Sets the boolean.
    /// </summary>
    /// <param name="value">The value value.</param>
    public void SetBoolean(bool value)
    {
        _hasResult = true;
        _result = GesValue.GesBoolean(value);
        _vmState?.SetBoolean(_destinationRegister, value);
    }

    /// <summary>
    /// Sets the integer.
    /// </summary>
    /// <param name="value">The value value.</param>
    /// <param name="unit">The unit value.</param>
    public void SetInteger(long value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        _hasResult = true;
        _result = GesValue.GesInteger(value, unit);
        _vmState?.SetInteger(_destinationRegister, value, unit);
    }

    /// <summary>
    /// Sets the float.
    /// </summary>
    /// <param name="value">The value value.</param>
    /// <param name="unit">The unit value.</param>
    public void SetFloat(double value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        _hasResult = true;
        _result = GesValue.GesFloat(value, unit);
        _vmState?.SetFloat(_destinationRegister, value, unit);
    }

    /// <summary>
    /// Sets the percentage.
    /// </summary>
    /// <param name="ratio">The ratio value.</param>
    public void SetPercentage(double ratio)
    {
        _hasResult = true;
        _result = GesValue.GesPercentage(ratio);
        _vmState?.SetPercentage(_destinationRegister, ratio);
    }

    /// <summary>
    /// Sets the text.
    /// </summary>
    /// <param name="text">The text value.</param>
    public void SetText(string text)
    {
        _hasResult = true;
        _result = GesValue.GesText(text);
        _vmState?.SetText(_destinationRegister, text);
    }

    /// <summary>
    /// Sets the tag.
    /// </summary>
    /// <param name="tag">The tag value.</param>
    public void SetTag(string tag)
    {
        _hasResult = true;
        _result = GesValue.GesTag(tag);
        _vmState?.SetTag(_destinationRegister, tag);
    }
}
