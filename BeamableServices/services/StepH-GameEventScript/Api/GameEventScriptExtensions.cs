#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Text;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Api;

public interface IGameEventScriptExtensionRegistry
{
    IGameEventScriptExtensionFunction? Resolve(GameEventScriptExtensionReference reference);
}

public interface IGameEventScriptExtensionFunction
{
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

public sealed class GameEventScriptExtensionReference
{
    public GameEventScriptExtensionReference(string? extensionName, string? functionName, IEnumerable<string?>? argumentLabels)
    {
        ExtensionName = NormalizeName(extensionName);
        FunctionName = NormalizeName(functionName);
        ArgumentLabels = NormalizeArgumentLabels(argumentLabels);
        SignatureId = CreateSignatureId(ExtensionName, FunctionName, ArgumentLabels);
    }

    public string ExtensionName { get; }

    public string FunctionName { get; }

    public IReadOnlyList<string> ArgumentLabels { get; }

    public string SignatureId { get; }

    private static string NormalizeName(string? name) => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

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

public sealed class GesExtensionCall
{
    private GameEventScriptSession? _runtimeSession;
    private GesValueArguments _arguments = GesValueArguments.Empty;
    private Runtime.VM.GesVmState? _vmState;
    private ushort _destinationRegister;
    private GesValue _result;
    private bool _hasResult;

    public GesExtensionCall()
    {
    }

    public GesExtensionCall(GameEventScriptSession runtimeSession)
    {
        _runtimeSession = runtimeSession ?? throw new ArgumentNullException(nameof(runtimeSession));
    }

    public GameEventScriptSession RuntimeSession => _runtimeSession ?? throw new InvalidOperationException("Extension context is not initialized.");

    public GameEventScriptRandomGenerator Random => RuntimeSession.Random;

    public GameEventScriptRuntimeLimits RuntimeLimits => RuntimeSession.RuntimeLimits;

    public GesValueArguments Arguments => _arguments;

    public GesValue Result => _hasResult ? _result : GesValue.GesNothing();

    internal bool HasResult => _hasResult;

    internal void BeginCall(Runtime.VM.GesVmState vmState, ushort destinationRegister, GameEventScriptSession runtimeSession, GesValueArguments arguments)
    {
        _vmState = vmState ?? throw new ArgumentNullException(nameof(vmState));
        _destinationRegister = destinationRegister;
        _runtimeSession = runtimeSession ?? throw new ArgumentNullException(nameof(runtimeSession));
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

    public void SetNothing() => SetValue(GesValue.GesNothing());

    public void SetValue(GesValue value)
    {
        _hasResult = true;
        _result = value;
        _vmState?.SetValue(_destinationRegister, in value);
    }

    public void SetBoolean(bool value)
    {
        _hasResult = true;
        _result = GesValue.GesBoolean(value);
        _vmState?.SetBoolean(_destinationRegister, value);
    }

    public void SetInteger(long value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        _hasResult = true;
        _result = GesValue.GesInteger(value, unit);
        _vmState?.SetInteger(_destinationRegister, value, unit);
    }

    public void SetFloat(double value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        _hasResult = true;
        _result = GesValue.GesFloat(value, unit);
        _vmState?.SetFloat(_destinationRegister, value, unit);
    }

    public void SetPercentage(double ratio)
    {
        _hasResult = true;
        _result = GesValue.GesPercentage(ratio);
        _vmState?.SetPercentage(_destinationRegister, ratio);
    }

    public void SetText(string text)
    {
        _hasResult = true;
        _result = GesValue.GesText(text);
        _vmState?.SetText(_destinationRegister, text);
    }

    public void SetTag(string tag)
    {
        _hasResult = true;
        _result = GesValue.GesTag(tag);
        _vmState?.SetTag(_destinationRegister, tag);
    }
}
