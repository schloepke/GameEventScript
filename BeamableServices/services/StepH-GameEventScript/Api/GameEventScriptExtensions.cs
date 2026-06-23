#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

public interface IGameEventScriptExtensionRegistry
{
    static IGameEventScriptExtensionRegistry CreateDefault(params Type[] extensionTypes)
        => CreateBuilder().AddRange(extensionTypes).Build();

    static IGameEventScriptExtensionRegistry CreateDefault(IEnumerable<Type> extensionTypes)
        => CreateBuilder().AddRange(extensionTypes).Build();

    static IGameEventScriptExtensionRegistryBuilder CreateBuilder()
        => GameEventScriptExtensionRegistryBuilder.Create(baseRegistry: null);

    static IGameEventScriptExtensionRegistryBuilder CreateBuilder(IGameEventScriptExtensionRegistry baseRegistry)
        => GameEventScriptExtensionRegistryBuilder.Create(baseRegistry ?? throw new ArgumentNullException(nameof(baseRegistry)));

    bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function);
}

public interface IGameEventScriptExtensionRegistryBuilder
{
    IGameEventScriptExtensionRegistryBuilder Add(Type extensionType);

    IGameEventScriptExtensionRegistryBuilder Add<T>();

    IGameEventScriptExtensionRegistryBuilder AddRange(IEnumerable<Type> extensionTypes);

    IGameEventScriptExtensionRegistry Build();
}

public interface IGameEventScriptExtensionFunction
{
    GameEventScriptValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptValue> arguments);
}

public sealed class GameEventScriptExtensionReference
{
    public GameEventScriptExtensionReference(string? extensionName, string? functionName, IEnumerable<string?>? argumentLabels)
    {
        ExtensionName = NormalizeName(extensionName);
        FunctionName = NormalizeName(functionName);
        ArgumentLabels = (argumentLabels ?? Array.Empty<string?>()).Select(GameEventScriptMessageSignature.NormalizeParameterName).ToArray();
        SignatureId = $"{ExtensionName}.{FunctionName}({string.Join(",", ArgumentLabels)})";
    }

    public string ExtensionName { get; }

    public string FunctionName { get; }

    public IReadOnlyList<string> ArgumentLabels { get; }

    public string SignatureId { get; }

    private static string NormalizeName(string? name) => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GesExtensionAttribute(string name) : Attribute
{
    public string Name { get; } = GameEventScriptExternalTypeNames.NormalizeTypeName(name);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class GesFunctionAttribute(string name) : Attribute
{
    public GesFunctionAttribute(string name, GameEventScriptBytecodeTypeKind returnKind) : this(name)
    {
        ReturnTypeName = GameEventScriptExternalTypeNames.ToTypeName(returnKind, unit: null);
        ReturnKind = returnKind;
    }

    public GesFunctionAttribute(string name, GameEventScriptBytecodeTypeKind returnKind, GameEventScriptBytecodeInstructionUnit unit) : this(name)
    {
        ReturnTypeName = GameEventScriptExternalTypeNames.ToTypeName(returnKind, unit);
        ReturnKind = returnKind;
        ReturnUnit = unit.ToStoredUnit();
    }

    public string Name { get; } = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));

    public string? ReturnTypeName { get; }

    public GameEventScriptBytecodeTypeKind? ReturnKind { get; }

    public GameEventScriptBytecodeInstructionUnit ReturnUnit { get; }
}

public sealed class GameEventScriptExtensionContext(GameEventScriptSession runtimeSession)
{
    public GameEventScriptSession RuntimeSession { get; } = runtimeSession ?? throw new ArgumentNullException(nameof(runtimeSession));

    public GameEventScriptRandomGenerator Random => RuntimeSession.Random;

    public GameEventScriptRuntimeLimits RuntimeLimits => RuntimeSession.RuntimeLimits;
}
