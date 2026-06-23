#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GesTypeAttribute(string typeName) : Attribute
{
    public string TypeName { get; } = typeName ?? throw new ArgumentNullException(nameof(typeName));
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class GesFieldAttribute : Attribute
{
    public GesFieldAttribute(string name, string typeName)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
    }

    public GesFieldAttribute(string name, GameEventScriptBytecodeTypeKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit: null);
        Kind = kind;
    }

    public GesFieldAttribute(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptBytecodeTypeKind? Kind { get; }

    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method)]
public sealed class GesConstructAttribute : Attribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class GesParamAttribute : Attribute
{
    public GesParamAttribute(string name, string typeName)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
    }

    public GesParamAttribute(string name, GameEventScriptBytecodeTypeKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit: null);
        Kind = kind;
    }

    public GesParamAttribute(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptBytecodeTypeKind? Kind { get; }

    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

public interface IGameEventScriptExternalTypeRegistry
{
    IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> Types { get; }

    bool TryResolve(GameEventScriptExternalTypeConstructorReference reference, out IGameEventScriptExternalTypeConstructor constructor);
}

public interface IGameEventScriptExternalTypeConstructor
{
    GameEventScriptExternalTypeConstructorDefinition Definition { get; }

    GameEventScriptValue Invoke(ReadOnlySpan<GameEventScriptValue> arguments);
}

public sealed class GameEventScriptExternalTypeDefinition
{
    public GameEventScriptExternalTypeDefinition(
        string name,
        IEnumerable<GameEventScriptExternalTypeFieldDefinition> fields,
        IEnumerable<GameEventScriptExternalTypeConstructorDefinition> constructors)
        : this(
            name,
            (fields ?? throw new ArgumentNullException(nameof(fields))).ToArray(),
            (constructors ?? throw new ArgumentNullException(nameof(constructors))).ToArray(),
            new Dictionary<string, Func<object, GameEventScriptValue>>(StringComparer.Ordinal),
            new Dictionary<string, IGameEventScriptExternalTypeConstructor>(StringComparer.Ordinal))
    {
    }

    internal GameEventScriptExternalTypeDefinition(
        string name,
        IReadOnlyList<GameEventScriptExternalTypeFieldDefinition> fields,
        IReadOnlyList<GameEventScriptExternalTypeConstructorDefinition> constructors,
        IReadOnlyDictionary<string, Func<object, GameEventScriptValue>> fieldReaders,
        IReadOnlyDictionary<string, IGameEventScriptExternalTypeConstructor> constructorBindings)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeTypeName(name);
        Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        Constructors = constructors ?? throw new ArgumentNullException(nameof(constructors));
        FieldReaders = fieldReaders ?? throw new ArgumentNullException(nameof(fieldReaders));
        ConstructorBindings = constructorBindings ?? throw new ArgumentNullException(nameof(constructorBindings));
    }

    public string Name { get; }

    public IReadOnlyList<GameEventScriptExternalTypeFieldDefinition> Fields { get; }

    public IReadOnlyList<GameEventScriptExternalTypeConstructorDefinition> Constructors { get; }

    internal IReadOnlyDictionary<string, Func<object, GameEventScriptValue>> FieldReaders { get; }

    internal IReadOnlyDictionary<string, IGameEventScriptExternalTypeConstructor> ConstructorBindings { get; }

    internal bool TryGetConstructor(IReadOnlyCollection<string> argumentLabels, out IGameEventScriptExternalTypeConstructor constructor)
    {
        var signatureId = GameEventScriptExternalTypeConstructorReference.CreateSignatureId(Name, argumentLabels);
        if (ConstructorBindings.TryGetValue(signatureId, out constructor!))
        {
            return true;
        }

        constructor = default!;
        return Constructors.Any(constructorDefinition => string.Equals(constructorDefinition.SignatureId, signatureId, StringComparison.Ordinal));
    }

    internal bool TryGetField(string fieldName, object instance, out GameEventScriptValue value)
    {
        if (FieldReaders.TryGetValue(fieldName, out var reader))
        {
            value = reader(instance);
            return true;
        }

        value = GameEventScriptValueFactory.GesNothing();
        return false;
    }
}

public sealed class GameEventScriptExternalTypeFieldDefinition
{
    public GameEventScriptExternalTypeFieldDefinition(string name, string typeName)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var (kind, unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(TypeName);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public GameEventScriptExternalTypeFieldDefinition(string name, GameEventScriptBytecodeTypeKind kind)
        : this(name, kind, unit: null)
    {
    }

    public GameEventScriptExternalTypeFieldDefinition(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit unit)
        : this(name, kind, (GameEventScriptBytecodeInstructionUnit?)unit)
    {
    }

    internal GameEventScriptExternalTypeFieldDefinition(string name, GameEventScriptBytecodeTypeKind? kind, GameEventScriptBytecodeInstructionUnit? unit)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = kind is { } resolvedKind
            ? GameEventScriptExternalTypeNames.ToTypeName(resolvedKind, unit)
            : throw new ArgumentException("External GameEventScript field kind must be specified.", nameof(kind));
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptBytecodeTypeKind? Kind { get; }

    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

public sealed class GameEventScriptExternalTypeParameterDefinition
{
    public GameEventScriptExternalTypeParameterDefinition(string name, string typeName)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var (kind, unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(TypeName);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public GameEventScriptExternalTypeParameterDefinition(string name, GameEventScriptBytecodeTypeKind kind)
        : this(name, kind, unit: null)
    {
    }

    public GameEventScriptExternalTypeParameterDefinition(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit unit)
        : this(name, kind, (GameEventScriptBytecodeInstructionUnit?)unit)
    {
    }

    internal GameEventScriptExternalTypeParameterDefinition(string name, GameEventScriptBytecodeTypeKind? kind, GameEventScriptBytecodeInstructionUnit? unit)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = kind is { } resolvedKind
            ? GameEventScriptExternalTypeNames.ToTypeName(resolvedKind, unit)
            : throw new ArgumentException("External GameEventScript parameter kind must be specified.", nameof(kind));
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptBytecodeTypeKind? Kind { get; }

    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

public sealed class GameEventScriptExternalTypeConstructorDefinition(string typeName, IEnumerable<GameEventScriptExternalTypeParameterDefinition> parameters)
{
    public string TypeName { get; } = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);

    public IReadOnlyList<GameEventScriptExternalTypeParameterDefinition> Parameters { get; } =
        (parameters ?? throw new ArgumentNullException(nameof(parameters))).ToArray();

    public string SignatureId => GameEventScriptExternalTypeConstructorReference.CreateSignatureId(TypeName, Parameters.Select(parameter => parameter.Name));
}

public sealed class GameEventScriptExternalTypeConstructorReference
{
    public GameEventScriptExternalTypeConstructorReference(string typeName, IEnumerable<string?>? argumentLabels)
    {
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        ArgumentLabels = (argumentLabels ?? Array.Empty<string?>())
            .Select(label => GameEventScriptExternalTypeNames.NormalizeIdentifier(label, nameof(argumentLabels)))
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();
        SignatureId = CreateSignatureId(TypeName, ArgumentLabels);
    }

    public string TypeName { get; }

    public IReadOnlyList<string> ArgumentLabels { get; }

    public string SignatureId { get; }

    public static string CreateSignatureId(string typeName, IEnumerable<string?>? argumentLabels)
    {
        var normalizedTypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var labels = (argumentLabels ?? Array.Empty<string?>())
            .Select(label => GameEventScriptExternalTypeNames.NormalizeIdentifier(label, nameof(argumentLabels)))
            .OrderBy(label => label, StringComparer.Ordinal);
        return $"{normalizedTypeName}({string.Join(",", labels)})";
    }
}

internal sealed class GameEventScriptEmptyExternalTypeRegistry : IGameEventScriptExternalTypeRegistry
{
    public static readonly GameEventScriptEmptyExternalTypeRegistry Instance = new();

    private GameEventScriptEmptyExternalTypeRegistry()
    {
    }

    public IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> Types { get; } =
        new Dictionary<string, GameEventScriptExternalTypeDefinition>(StringComparer.Ordinal);

    public bool TryResolve(GameEventScriptExternalTypeConstructorReference reference, out IGameEventScriptExternalTypeConstructor constructor)
    {
        constructor = default!;
        return false;
    }
}
