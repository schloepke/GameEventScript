// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Defines the contract for i game event script external type registry.
/// </summary>
public interface IGameEventScriptExternalTypeRegistry
{
    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="reference">The reference value.</param>
    /// <returns>The result of the operation.</returns>
    IGameEventScriptExternalTypeConstructor? Resolve(GameEventScriptExternalTypeConstructorReference reference);
}

/// <summary>
/// Defines the contract for i game event script external type catalog.
/// </summary>
public interface IGameEventScriptExternalTypeCatalog
{
    /// <summary>
    /// Gets the types.
    /// </summary>
    IReadOnlyList<GameEventScriptExternalTypeDefinition> Types { get; }

    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The result of the operation.</returns>
    GameEventScriptExternalTypeDefinition? Resolve(string typeName);
}

/// <summary>
/// Defines the contract for i game event script external value.
/// </summary>
public interface IGameEventScriptExternalValue
{
    /// <summary>
    /// Gets the definition.
    /// </summary>
    GameEventScriptExternalTypeDefinition Definition { get; }

    /// <summary>
    /// Gets the field.
    /// </summary>
    /// <param name="fieldName">The field name value.</param>
    /// <returns>The result of the operation.</returns>
    GesValue? GetField(string fieldName);
}

/// <summary>
/// Represents a game event script external type catalog.
/// </summary>
public sealed class GameEventScriptExternalTypeCatalog : IGameEventScriptExternalTypeCatalog
{
    private readonly Dictionary<string, GameEventScriptExternalTypeDefinition> _typesByName;

    /// <summary>
    /// Initializes a new instance of Game Event Script External Type Catalog.
    /// </summary>
    /// <param name="types">The types value.</param>
    public GameEventScriptExternalTypeCatalog(IEnumerable<GameEventScriptExternalTypeDefinition> types)
    {
        _ = types ?? throw new ArgumentNullException(nameof(types));
        var definitions = new List<GameEventScriptExternalTypeDefinition>();
        _typesByName = new Dictionary<string, GameEventScriptExternalTypeDefinition>(StringComparer.Ordinal);
        foreach (var definition in types)
        {
            _ = definition ?? throw new ArgumentException("External type catalog contains null.", nameof(types));
            if (!_typesByName.TryAdd(definition.Name, definition))
                throw new ArgumentException($"External GameEventScript type ':{definition.Name}' is declared more than once.", nameof(types));
            definitions.Add(definition);
        }

        Types = Array.AsReadOnly(definitions.ToArray());
    }

    /// <summary>
    /// Gets the types.
    /// </summary>
    public IReadOnlyList<GameEventScriptExternalTypeDefinition> Types { get; }

    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The result of the operation.</returns>
    public GameEventScriptExternalTypeDefinition? Resolve(string typeName)
    {
        var normalized = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        return _typesByName.TryGetValue(normalized, out var definition) ? definition : null;
    }
}

/// <summary>
/// Defines the contract for i game event script external type constructor.
/// </summary>
public interface IGameEventScriptExternalTypeConstructor
{
    /// <summary>
    /// Gets the definition.
    /// </summary>
    GameEventScriptExternalTypeConstructorDefinition Definition { get; }

    /// <summary>
    /// Performs the invoke operation.
    /// </summary>
    /// <param name="call">The call value.</param>
    void Invoke(GesExternalTypeConstructorCall call);
}

/// <summary>
/// Represents a ges external type constructor call.
/// </summary>
public sealed class GesExternalTypeConstructorCall
{
    private GesValueArguments _arguments = GesValueArguments.Empty;
    private Runtime.VM.GesVmState? _vmState;
    private ushort _destinationRegister;
    private string? _expectedTypeName;
    private GesValue _result;
    private bool _hasResult;

    /// <summary>
    /// Initializes a new instance of Ges External Type Constructor Call.
    /// </summary>
    public GesExternalTypeConstructorCall()
    {
    }

    /// <summary>
    /// Initializes a new instance of Ges External Type Constructor Call.
    /// </summary>
    /// <param name="arguments">The arguments value.</param>
    public GesExternalTypeConstructorCall(GesValueArguments arguments)
    {
        _arguments = arguments;
    }

    /// <summary>
    /// Gets the arguments.
    /// </summary>
    public GesValueArguments Arguments => _arguments;

    /// <summary>
    /// Gets the result.
    /// </summary>
    public GesValue Result => _hasResult ? _result : GesValue.GesNothing();

    internal bool HasResult => _hasResult;

    internal void BeginCall(Runtime.VM.GesVmState vmState, ushort destinationRegister, GesValueArguments arguments, string expectedTypeName)
    {
        _vmState = vmState ?? throw new ArgumentNullException(nameof(vmState));
        _destinationRegister = destinationRegister;
        _arguments = arguments;
        _expectedTypeName = expectedTypeName;
        _result = default;
        _hasResult = false;
    }

    internal void EndCall()
    {
        _vmState = null;
        _destinationRegister = 0;
        _arguments = GesValueArguments.Empty;
        _expectedTypeName = null;
        _result = default;
        _hasResult = false;
    }

    /// <summary>
    /// Sets the nothing.
    /// </summary>
    public void SetNothing()
    {
        var value = GesValue.GesNothing();
        SetValue(in value);
    }

    private void SetValue(in GesValue value)
    {
        _hasResult = true;
        _result = value;
        _vmState?.SetValue(_destinationRegister, in value);
    }

    /// <summary>
    /// Sets the external value.
    /// </summary>
    /// <param name="value">The value value.</param>
    public void SetExternalValue(IGameEventScriptExternalValue value)
    {
        _ = value ?? throw new ArgumentNullException(nameof(value));
        if (_expectedTypeName is not null && !string.Equals(value.Definition.Name, _expectedTypeName, StringComparison.Ordinal))
        {
            SetNothing();
            _vmState?.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidExternalTypeBinding,
                $"External constructor for ':{_expectedTypeName}' returned value of type ':{value.Definition.Name}'.");
            return;
        }
        _hasResult = true;
        _result = default;
        _result.SetExternalType(value);
        _vmState?.SetExternalType(_destinationRegister, value);
    }
}

/// <summary>
/// Represents a game event script external type definition.
/// </summary>
public sealed class GameEventScriptExternalTypeDefinition
{
    /// <summary>
    /// Initializes a new instance of Game Event Script External Type Definition.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="fields">The fields value.</param>
    /// <param name="constructors">The constructors value.</param>
    public GameEventScriptExternalTypeDefinition(string name, IEnumerable<GameEventScriptExternalTypeFieldDefinition> fields, IEnumerable<GameEventScriptExternalTypeConstructorDefinition> constructors)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeTypeName(name);
        var copiedFields = CopyFields(fields ?? throw new ArgumentNullException(nameof(fields)));
        var copiedConstructors = CopyConstructors(constructors ?? throw new ArgumentNullException(nameof(constructors)));
        ValidateFields(copiedFields);
        Fields = Array.AsReadOnly(copiedFields);
        ValidateConstructors(copiedConstructors);
        Constructors = Array.AsReadOnly(copiedConstructors);
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the fields.
    /// </summary>
    public IReadOnlyList<GameEventScriptExternalTypeFieldDefinition> Fields { get; }

    /// <summary>
    /// Gets the constructors.
    /// </summary>
    public IReadOnlyList<GameEventScriptExternalTypeConstructorDefinition> Constructors { get; }

    internal bool HasConstructor(IReadOnlyCollection<string> argumentLabels)
    {
        var signatureId = GameEventScriptExternalTypeConstructorReference.CreateSignatureId(Name, argumentLabels);
        for (var index = 0; index < Constructors.Count; index++)
        {
            if (string.Equals(Constructors[index].SignatureId, signatureId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ValidateFields(IReadOnlyList<GameEventScriptExternalTypeFieldDefinition> fields)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < fields.Count; index++)
        {
            if (fields[index] is null)
                throw new ArgumentException("External type field list contains null.", nameof(fields));
            if (!names.Add(fields[index].Name))
                throw new ArgumentException($"External GameEventScript type ':{Name}' declares field '{fields[index].Name}' more than once.", nameof(fields));
        }
    }

    private void ValidateConstructors(IReadOnlyList<GameEventScriptExternalTypeConstructorDefinition> constructors)
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        for (var fieldIndex = 0; fieldIndex < Fields.Count; fieldIndex++) fieldNames.Add(Fields[fieldIndex].Name);
        for (var index = 0; index < constructors.Count; index++)
        {
            var constructor = constructors[index] ?? throw new ArgumentException("External type constructor list contains null.", nameof(constructors));
            if (!string.Equals(constructor.TypeName, Name, StringComparison.Ordinal))
                throw new ArgumentException($"External constructor '{constructor.SignatureId}' does not construct type ':{Name}'.", nameof(constructors));
            if (!signatures.Add(constructor.SignatureId))
                throw new ArgumentException($"External GameEventScript type ':{Name}' declares constructor '{constructor.SignatureId}' more than once.", nameof(constructors));
            for (var parameterIndex = 0; parameterIndex < constructor.Parameters.Count; parameterIndex++)
            {
                if (!fieldNames.Contains(constructor.Parameters[parameterIndex].Name))
                    throw new ArgumentException($"External constructor '{constructor.SignatureId}' parameter '{constructor.Parameters[parameterIndex].Name}' is not a declared field.", nameof(constructors));
            }
        }
    }

    private static GameEventScriptExternalTypeFieldDefinition[] CopyFields(IEnumerable<GameEventScriptExternalTypeFieldDefinition> fields)
    {
        if (fields is IReadOnlyCollection<GameEventScriptExternalTypeFieldDefinition> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var result = new GameEventScriptExternalTypeFieldDefinition[collection.Count];
            var index = 0;
            foreach (var field in collection)
            {
                result[index++] = field;
            }

            return result;
        }

        var list = new List<GameEventScriptExternalTypeFieldDefinition>();
        foreach (var field in fields)
        {
            list.Add(field);
        }

        var values = new GameEventScriptExternalTypeFieldDefinition[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            values[index] = list[index];
        }

        return values;
    }

    private static GameEventScriptExternalTypeConstructorDefinition[] CopyConstructors(IEnumerable<GameEventScriptExternalTypeConstructorDefinition> constructors)
    {
        if (constructors is IReadOnlyCollection<GameEventScriptExternalTypeConstructorDefinition> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var result = new GameEventScriptExternalTypeConstructorDefinition[collection.Count];
            var index = 0;
            foreach (var constructor in collection)
            {
                result[index++] = constructor;
            }

            return result;
        }

        var list = new List<GameEventScriptExternalTypeConstructorDefinition>();
        foreach (var constructor in constructors)
        {
            list.Add(constructor);
        }

        var values = new GameEventScriptExternalTypeConstructorDefinition[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            values[index] = list[index];
        }

        return values;
    }
}

/// <summary>
/// Represents a game event script external type field definition.
/// </summary>
public sealed class GameEventScriptExternalTypeFieldDefinition
{
    /// <summary>
    /// Initializes a new instance of Game Event Script External Type Field Definition.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="typeName">The type name value.</param>
    public GameEventScriptExternalTypeFieldDefinition(string name, string typeName)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var (kind, unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(TypeName);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    /// <summary>
    /// Initializes a new instance of Game Event Script External Type Field Definition.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="kind">The kind value.</param>
    public GameEventScriptExternalTypeFieldDefinition(string name, GameEventScriptBytecodeTypeKind kind)
        : this(name, kind, unit: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of Game Event Script External Type Field Definition.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="kind">The kind value.</param>
    /// <param name="unit">The unit value.</param>
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

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type name.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public GameEventScriptBytecodeTypeKind? Kind { get; }

    /// <summary>
    /// Gets the unit.
    /// </summary>
    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

/// <summary>
/// Represents a game event script external type parameter definition.
/// </summary>
public sealed class GameEventScriptExternalTypeParameterDefinition
{
    /// <summary>
    /// Initializes a new instance of Game Event Script External Type Parameter Definition.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="typeName">The type name value.</param>
    public GameEventScriptExternalTypeParameterDefinition(string name, string typeName)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var (kind, unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(TypeName);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    /// <summary>
    /// Initializes a new instance of Game Event Script External Type Parameter Definition.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="kind">The kind value.</param>
    public GameEventScriptExternalTypeParameterDefinition(string name, GameEventScriptBytecodeTypeKind kind)
        : this(name, kind, unit: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of Game Event Script External Type Parameter Definition.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="kind">The kind value.</param>
    /// <param name="unit">The unit value.</param>
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

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type name.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public GameEventScriptBytecodeTypeKind? Kind { get; }

    /// <summary>
    /// Gets the unit.
    /// </summary>
    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

/// <summary>
/// Represents a game event script external type constructor definition.
/// </summary>
public sealed class GameEventScriptExternalTypeConstructorDefinition
{
    /// <summary>
    /// Initializes a new instance of Game Event Script External Type Constructor Definition.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <param name="parameters">The parameters value.</param>
    public GameEventScriptExternalTypeConstructorDefinition(string typeName, IEnumerable<GameEventScriptExternalTypeParameterDefinition> parameters)
    {
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var copiedParameters = CopyParameters(parameters ?? throw new ArgumentNullException(nameof(parameters)));
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < copiedParameters.Length; index++)
        {
            var parameter = copiedParameters[index] ?? throw new ArgumentException("External constructor parameter list contains null.", nameof(parameters));
            if (!names.Add(parameter.Name))
                throw new ArgumentException($"External constructor for ':{TypeName}' declares parameter '{parameter.Name}' more than once.", nameof(parameters));
        }
        Parameters = Array.AsReadOnly(copiedParameters);
        SignatureId = GameEventScriptExternalTypeConstructorReference.CreateSignatureId(TypeName, CreateParameterNameArray(Parameters));
    }

    /// <summary>
    /// Gets the type name.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the parameters.
    /// </summary>
    public IReadOnlyList<GameEventScriptExternalTypeParameterDefinition> Parameters { get; }

    /// <summary>
    /// Gets the signature id.
    /// </summary>
    public string SignatureId { get; }

    private static GameEventScriptExternalTypeParameterDefinition[] CopyParameters(IEnumerable<GameEventScriptExternalTypeParameterDefinition> parameters)
    {
        if (parameters is IReadOnlyCollection<GameEventScriptExternalTypeParameterDefinition> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var result = new GameEventScriptExternalTypeParameterDefinition[collection.Count];
            var index = 0;
            foreach (var parameter in collection)
            {
                result[index++] = parameter;
            }

            return result;
        }

        var list = new List<GameEventScriptExternalTypeParameterDefinition>();
        foreach (var parameter in parameters)
        {
            list.Add(parameter);
        }

        var values = new GameEventScriptExternalTypeParameterDefinition[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            values[index] = list[index];
        }

        return values;
    }

    private static string[] CreateParameterNameArray(IReadOnlyList<GameEventScriptExternalTypeParameterDefinition> parameters)
    {
        if (parameters.Count == 0)
        {
            return [];
        }

        var values = new string[parameters.Count];
        for (var index = 0; index < parameters.Count; index++)
        {
            values[index] = parameters[index].Name;
        }

        return values;
    }
}

/// <summary>
/// Represents a game event script external type constructor reference.
/// </summary>
public sealed class GameEventScriptExternalTypeConstructorReference
{
    /// <summary>
    /// Initializes a new instance of Game Event Script External Type Constructor Reference.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <param name="argumentLabels">The argument labels value.</param>
    public GameEventScriptExternalTypeConstructorReference(string typeName, IEnumerable<string?>? argumentLabels)
    {
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        ArgumentLabels = NormalizeAndSortLabels(argumentLabels);
        SignatureId = CreateSignatureId(TypeName, ArgumentLabels);
    }

    /// <summary>
    /// Gets the type name.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the argument labels.
    /// </summary>
    public IReadOnlyList<string> ArgumentLabels { get; }

    /// <summary>
    /// Gets the signature id.
    /// </summary>
    public string SignatureId { get; }

    /// <summary>
    /// Creates a signature id.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <param name="argumentLabels">The argument labels value.</param>
    /// <returns>The result of the operation.</returns>
    public static string CreateSignatureId(string typeName, IEnumerable<string?>? argumentLabels)
    {
        var normalizedTypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var labels = NormalizeAndSortLabels(argumentLabels);
        var builder = new StringBuilder();
        builder.Append(normalizedTypeName).Append('(');
        for (var index = 0; index < labels.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(labels[index]);
        }

        return builder.Append(')').ToString();
    }

    private static string[] NormalizeAndSortLabels(IEnumerable<string?>? argumentLabels)
    {
        if (argumentLabels is null)
        {
            return [];
        }

        string[] values;
        if (argumentLabels is IReadOnlyCollection<string?> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            values = new string[collection.Count];
            var index = 0;
            foreach (var label in collection)
            {
                values[index++] = GameEventScriptExternalTypeNames.NormalizeIdentifier(label, nameof(argumentLabels));
            }
        }
        else
        {
            var list = new List<string>();
            foreach (var label in argumentLabels)
            {
                list.Add(GameEventScriptExternalTypeNames.NormalizeIdentifier(label, nameof(argumentLabels)));
            }

            if (list.Count == 0)
            {
                return [];
            }

            values = new string[list.Count];
            for (var index = 0; index < list.Count; index++)
            {
                values[index] = list[index];
            }
        }

        Array.Sort(values, StringComparer.Ordinal);
        return values;
    }
}

internal sealed class GameEventScriptEmptyExternalTypeCatalog : IGameEventScriptExternalTypeCatalog
{
    public static readonly GameEventScriptEmptyExternalTypeCatalog Instance = new();

    private GameEventScriptEmptyExternalTypeCatalog()
    {
    }

    public IReadOnlyList<GameEventScriptExternalTypeDefinition> Types { get; } = Array.Empty<GameEventScriptExternalTypeDefinition>();

    public GameEventScriptExternalTypeDefinition? Resolve(string typeName) => null;
}

internal sealed class GameEventScriptEmptyExternalTypeRegistry : IGameEventScriptExternalTypeRegistry
{
    public static readonly GameEventScriptEmptyExternalTypeRegistry Instance = new();

    private GameEventScriptEmptyExternalTypeRegistry()
    {
    }

    public IGameEventScriptExternalTypeConstructor? Resolve(GameEventScriptExternalTypeConstructorReference reference) => null;
}
