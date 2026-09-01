#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Text;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Api;

public interface IGameEventScriptExternalTypeRegistry
{
    IGameEventScriptExternalTypeConstructor? Resolve(GameEventScriptExternalTypeConstructorReference reference);
}

public interface IGameEventScriptExternalTypeCatalog
{
    IReadOnlyList<GameEventScriptExternalTypeDefinition> Types { get; }

    GameEventScriptExternalTypeDefinition? Resolve(string typeName);
}

public interface IGameEventScriptExternalValue
{
    GameEventScriptExternalTypeDefinition Definition { get; }

    GesValue? GetField(string fieldName);
}

public sealed class GameEventScriptExternalTypeCatalog : IGameEventScriptExternalTypeCatalog
{
    private readonly Dictionary<string, GameEventScriptExternalTypeDefinition> _typesByName;

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

    public IReadOnlyList<GameEventScriptExternalTypeDefinition> Types { get; }

    public GameEventScriptExternalTypeDefinition? Resolve(string typeName)
    {
        var normalized = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        return _typesByName.TryGetValue(normalized, out var definition) ? definition : null;
    }
}

public interface IGameEventScriptExternalTypeConstructor
{
    GameEventScriptExternalTypeConstructorDefinition Definition { get; }

    void Invoke(GesExternalTypeConstructorCall call);
}

public sealed class GesExternalTypeConstructorCall
{
    private GesValueArguments _arguments = GesValueArguments.Empty;
    private Runtime.VM.GesVmState? _vmState;
    private ushort _destinationRegister;
    private string? _expectedTypeName;
    private GesValue _result;
    private bool _hasResult;

    public GesExternalTypeConstructorCall()
    {
    }

    public GesExternalTypeConstructorCall(GesValueArguments arguments)
    {
        _arguments = arguments;
    }

    public GesValueArguments Arguments => _arguments;

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

public sealed class GameEventScriptExternalTypeDefinition
{
    public GameEventScriptExternalTypeDefinition(
        string name,
        IEnumerable<GameEventScriptExternalTypeFieldDefinition> fields,
        IEnumerable<GameEventScriptExternalTypeConstructorDefinition> constructors)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeTypeName(name);
        var copiedFields = CopyFields(fields ?? throw new ArgumentNullException(nameof(fields)));
        var copiedConstructors = CopyConstructors(constructors ?? throw new ArgumentNullException(nameof(constructors)));
        ValidateFields(copiedFields);
        Fields = Array.AsReadOnly(copiedFields);
        ValidateConstructors(copiedConstructors);
        Constructors = Array.AsReadOnly(copiedConstructors);
    }

    public string Name { get; }

    public IReadOnlyList<GameEventScriptExternalTypeFieldDefinition> Fields { get; }

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

public sealed class GameEventScriptExternalTypeConstructorDefinition
{
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

    public string TypeName { get; }

    public IReadOnlyList<GameEventScriptExternalTypeParameterDefinition> Parameters { get; }

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

public sealed class GameEventScriptExternalTypeConstructorReference
{
    public GameEventScriptExternalTypeConstructorReference(string typeName, IEnumerable<string?>? argumentLabels)
    {
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        ArgumentLabels = NormalizeAndSortLabels(argumentLabels);
        SignatureId = CreateSignatureId(TypeName, ArgumentLabels);
    }

    public string TypeName { get; }

    public IReadOnlyList<string> ArgumentLabels { get; }

    public string SignatureId { get; }

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
